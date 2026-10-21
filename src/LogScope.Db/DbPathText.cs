using System;
using System.IO;

namespace LogScope.Db
{
    /// <summary>
    /// 견줄 자리(폴더나 파일)를 <b>사람이 읽는 말</b>로 바꿉니다 — 이름 한
    /// 조각과, 그 아래 한 줄.
    ///
    /// <b>왜 따로 두었나.</b> 이 글이 보이는 자리가 둘입니다 — 분석 2 화면의
    /// 칸과 메인 화면의 칸. 각자 적어 두면 한쪽만 고치는 날이 오고, 그러면
    /// <b>같은 자리를 보는데 두 화면에 다른 말이 적힙니다.</b> 그 둘 중 어느
    /// 쪽이 참인지는 화면만 봐서는 알 수 없습니다.
    ///
    /// 화면 글인데 LogScope.Db 에 둔 것은 <see cref="DbVerdict"/> 와 같은
    /// 까닭입니다 — WPF 를 쓰지 않으니 <b>시험이 닿습니다.</b>
    /// </summary>
    public static class DbPathText
    {
        /// <summary>아직 자리를 안 정했을 때 아래에 적는 말.</summary>
        /// <summary>
        /// 아직 자리를 안 정했을 때 아래에 적는 말 — <b>분석 2 화면</b>용.
        /// 거기에는 칸 안에 타 넣는 자리와 [폴더]·[파일] 단추가 있습니다.
        /// </summary>
        public const string None = "끌어다 놓거나, 타 넣거나, 단추로 고르세요";

        /// <summary>
        /// 같은 자리의 말 — <b>메인 화면 칸</b>용. 거기에는 단추도 타 넣는
        /// 자리도 없고, 칸을 누르면 파일 고르는 창이 뜹니다.
        ///
        /// 두 화면이 같은 글을 쓰면 한쪽이 거짓이 됩니다 — 그래서 글은
        /// 둘이지만 <b>자리는 여기 한 곳</b>입니다.
        /// </summary>
        public const string NoneOnCard = "누르면 파일 고르기 · 폴더는 끌어다 놓기";

        /// <summary>
        /// 경로의 <b>마지막 조각</b>. 칸이 좁아서 전체 경로는 들어가지
        /// 않습니다 (전체는 풍선 도움말로 보여 줍니다).
        ///
        /// <c>Path.GetFileName</c> 을 쓰지 않습니다. 저 함수는 <b>돌고 있는
        /// 운영체제의 구분 기호만</b> 봅니다 — 리눅스에서 돌리는 제 시험에서는
        /// <c>C:\db\io</c> 가 통째로 "이름" 이 되어, Windows 에서만 드러나는
        /// 차이가 생깁니다. 두 기호를 다 보는 쪽이 어디서 돌려도 같습니다.
        /// </summary>
        public static string Name(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // 폴더를 끌어다 놓으면 끝에 구분 기호가 붙어 오는 일이 있습니다.
            string s = path.TrimEnd('\\', '/');
            if (s.Length == 0) return path;   // "\" 하나만 남은 경우

            int cut = s.LastIndexOfAny(new[] { '\\', '/' });
            string tail = cut < 0 ? s : s.Substring(cut + 1);

            // 뿌리만 적어도 빈 칸이 되어서는 안 됩니다 — 빈 칸은
            // "안 넣었다" 로 읽힙니다. "C:\" 는 "C:" 가 남고, 구분 기호
            // 하나뿐이면 (위에서) 적은 그대로 돌려줍니다.
            return tail.Length > 0 ? tail : path;
        }

        /// <summary>
        /// 이름 아래 작게 적는 한 줄. <b>그 자리가 무엇인지</b> 말해 줍니다 —
        /// 폴더인지 파일인지, 그리고 <b>있기는 한지</b>.
        ///
        /// 없는 자리를 말해 주는 것이 중요합니다. 경로를 손으로 타 넣다가
        /// 한 글자 틀리면 [DB 읽기] 를 눌러야 알게 되는데, 그때 나오는 말은
        /// "읽은 표가 없습니다" 라서 왜인지 알 수가 없습니다.
        /// </summary>
        public static string Note(string path)
        {
            return Note(path, None);
        }

        /// <summary>아직 안 정했을 때의 말을 부르는 쪽이 고릅니다.</summary>
        public static string Note(string path, string none)
        {
            if (string.IsNullOrEmpty(path)) return none;

            try
            {
                if (Directory.Exists(path)) return "폴더";
                if (File.Exists(path)) return "파일";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is ArgumentException || e is NotSupportedException)
            {
                return "들여다볼 수 없는 자리입니다";
            }
            return "이 자리에 아무것도 없습니다";
        }
    }
}
