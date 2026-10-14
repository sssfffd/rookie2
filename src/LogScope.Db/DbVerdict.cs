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
        /// 낱말 아래에 적는 까닭. <b>판정에 안 들어간 수까지 적습니다</b> —
        /// 없어진 IO 가 스무 개인데 PASS 만 보이면 안 됩니다.
        /// </summary>
        public static string Why(DbDiffList.Tally t)
        {
            string head = "차이 발생 " + t.Changed.ToString("N0") + "개";
            if (t.Changed > 0 && t.Changed < FailFrom)
                head += " (" + FailFrom.ToString("N0") + "개부터 FAIL)";

            if (t.Gone == 0 && t.Added == 0) return head;

            return head + " · 없어진 IO " + t.Gone.ToString("N0")
                 + "개 · 새로 생긴 IO " + t.Added.ToString("N0") + "개"
                 + " (이 둘은 판정에 안 넣습니다)";
        }
    }
}
