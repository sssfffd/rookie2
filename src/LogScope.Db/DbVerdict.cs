using System.Collections.Generic;

namespace LogScope.Db
{
    /// <summary>
    /// 분석 2 의 판정 — <b>PASS · WARNING · FAIL</b>.
    ///
    /// 메인 화면 칸에 점수(0~100) 대신 이 세 낱말이 적힙니다. 점수는
    /// "무엇을 어떻게 100 점으로 환산했나" 를 묻게 만들지만, DB 견주기에서
    /// 사람이 알고 싶은 것은 <b>"그냥 넘겨도 되나"</b> 하나입니다.
    ///
    /// 기준은 <b>차이 발생(값이 달라진 IO) 개수</b>입니다.
    /// <code>
    ///   0 개            → PASS
    ///   1 ~ 99 개       → WARNING
    ///   100 개 이상     → FAIL
    /// </code>
    ///
    /// <b>이 한 곳에만 있습니다.</b> 화면이 같은 가름을 한 번 더 짜면
    /// 한쪽만 고쳐지는 날이 옵니다 (칸의 색과 낱말이 어긋납니다).
    ///
    /// <b>없어진 IO 와 새로 생긴 IO 는 판정에 넣지 않습니다.</b> 그렇게
    /// 정하신 것이라 그대로 따랐습니다. 다만 그 둘이 0 이 아닐 때 PASS 만
    /// 보이면 "아무 일도 없었다" 로 읽힙니다 — 그래서 <see cref="Why"/> 가
    /// 낱말 아래에 그 수를 함께 적습니다.
    /// </summary>
    public static class DbVerdict
    {
        /// <summary>차이 발생이 이 수부터 FAIL.</summary>
        public const int FailFrom = 100;

        public const string Pass = "PASS";
        public const string Warning = "WARNING";
        public const string Fail = "FAIL";

        /// <summary>차이 발생 개수로 가른 판정.</summary>
        public static string Of(int changed)
        {
            if (changed <= 0) return Pass;
            return changed >= FailFrom ? Fail : Warning;
        }

        /// <summary>
        /// 이름을 몇 개까지 적을지. 수백 개를 한 줄에 늘어놓으면 칸을
        /// 넘기고, 넘긴 글은 아무도 못 읽습니다. 나머지는 "외 N 개" 로
        /// 셈해 적습니다 — <b>몇 개를 안 적었는지는 말해 줍니다.</b>
        /// </summary>
        public const int MaxNames = 8;

        /// <summary>
        /// 메인 화면 <b>요약 칸</b>에 줄로 적을 IO 수. 한 줄에 늘어놓는
        /// 이름(<see cref="MaxNames"/>)보다 넉넉합니다 — 요약 칸은 굴러
        /// 가니까요. 넘으면 "외 N 개" 한 줄을 붙입니다.
        /// </summary>
        public const int MaxListIos = 60;

        /// <summary>
        /// 낱말 아래에 적는 요약. 판정마다 <b>다음에 뭘 해야 하는지</b>를
        /// 적습니다.
        ///
        /// <code>
        ///   FAIL    → 차이가 너무 많습니다. 파일을 다시 확인해 주세요.
        ///   WARNING → 어떤 IO 가 달라졌는지 이름을 적습니다.
        ///   PASS    → 차이 없습니다.
        /// </code>
        ///
        /// 세 경우를 한 함수에 둔 까닭: 화면 두 곳(메인 칸과 분석 2 화면)이
        /// 같은 글을 보여 주는데, 각자 짜면 한쪽만 고쳐지는 날이 옵니다.
        ///
        /// <b>판정에 안 들어간 수는 어느 경우든 덧붙입니다.</b> 없어진 IO 가
        /// 스무 개인데 "차이 없습니다" 만 보이면 거짓말이 됩니다.
        /// </summary>
        /// <param name="names">
        /// 달라진 IO 이름들 (<see cref="DbDiffList.ChangedIos"/>). WARNING 일
        /// 때만 씁니다. null 이면 이름 없이 개수만 적습니다.
        /// </param>
        public static string Why(DbDiffList.Tally t, IList<string> names)
        {
            return Head(t, names) + Tail(t);
        }

        /// <summary>이름 없이. 이름을 아직 모으지 않은 자리에서 씁니다.</summary>
        public static string Why(DbDiffList.Tally t)
        {
            return Why(t, null);
        }

        private static string Head(DbDiffList.Tally t, IList<string> names)
        {
            string word = Of(t.Changed);

            if (word == Fail)
            {
                // 이름을 적지 않습니다. 100 개가 넘는 자리에서 여덟 개만
                // 적으면 "이 여덟 개가 문제" 로 읽힙니다 — 그게 아니라
                // 파일이 잘못 걸렸을 가능성이 먼저입니다.
                return "차이가 너무 많습니다 (" + t.Changed.ToString("N0")
                     + "개). 파일을 다시 확인해 주세요.";
            }

            if (word == Pass) return "차이 없습니다.";

            string head = "차이 발생 " + t.Changed.ToString("N0") + "개";
            if (names == null || names.Count == 0)
                return head + " (" + FailFrom.ToString("N0") + "개부터 FAIL)";

            // 여기서 적는 것은 <b>한 줄</b>이라 여덟 개까지입니다. 넘겨받은
            // 목록이 더 길 수 있어서 (요약 칸은 더 많이 적습니다) 여기서
            // 자릅니다 — 자르는 자리가 한 곳이어야 "외 N 개" 가 안 어긋납니다.
            int used = names.Count < MaxNames ? names.Count : MaxNames;
            head += " — " + Join(names, used);
            int more = t.Changed - used;
            if (more > 0) head += " 외 " + more.ToString("N0") + "개";
            return head;
        }

        /// <summary>판정에 안 들어간 수. 0 이면 빈 글자입니다.</summary>
        private static string Tail(DbDiffList.Tally t)
        {
            if (t.Gone == 0 && t.Added == 0) return string.Empty;

            return "   (판정에 안 넣음: 없어진 IO " + t.Gone.ToString("N0")
                 + "개 · 새로 생긴 IO " + t.Added.ToString("N0") + "개)";
        }

        private static string Join(IList<string> names, int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }
    }
}
