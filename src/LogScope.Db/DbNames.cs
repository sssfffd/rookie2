namespace LogScope.Db
{
    /// <summary>
    /// DB 분석이 쓰는 <b>이름과 자리 번호가 적혀 있는 유일한 곳</b>입니다.
    ///
    /// 이름을 바꾸려면 이 파일의 한 줄만 고치면 됩니다. 화면 머리글 ·
    /// CSV 머리글 · 마우스 설명 글이 전부 여기를 봅니다.
    /// </summary>
    public static class DbNames
    {
        // ---------------- 목록 칸 이름 ----------------

        public const string Io = "IO명";
        public const string Kind = "갈래";
        public const string Change = "구분";
        public const string Where = "행";
        public const string Column = "열";
        public const string Before = "이전 value";
        public const string After = "이후 value";
        public const string Note = "설명";

        /// <summary>
        /// CSV 머리글에 들어갈 이름들. <b>순서가 CSV 가 쓰는 순서입니다</b> —
        /// <c>DbDiffList.ToCsv</c> 가 이 배열대로 머리글을 찍습니다.
        ///
        /// 0.87 에서 "표" 칸을 뺐습니다. 파일 하나를 넣어 견주는 쓰임이라
        /// 표를 따로 가를 일이 없습니다.
        /// </summary>
        public static string[] CsvNames()
        {
            return new[] { Io, Kind, Change, Where, Column, Before, After, Note };
        }

        // ---------------- .csv 의 자리 번호 ----------------

        /// <summary>
        /// <b>.csv 에서 어느 열을 읽을지.</b> 1 부터 셉니다 (사람이 세는 대로).
        ///
        /// 쓰는 .csv 는 이렇게 생겼습니다.
        /// <code>
        ///   1,2,3,4,5,6        ← 첫 줄은 열 이름
        ///   …,…,IO 이름,값,…,…  ← 둘째 줄부터 자료
        /// </code>
        ///
        /// <b>3 번으로 행을 짝짓고, 4 번 값이 달라진 것을 찾습니다.</b>
        /// 나머지 열은 읽지 않습니다 — 읽어 두면 "안 본 열인데 왜 차이에
        /// 안 나오지" 가 되고, 견주면 묻지 않은 차이가 쏟아집니다.
        /// 안 읽었다는 것은 화면에 적습니다.
        ///
        /// 설비마다 자리가 다르면 <b>이 두 줄만</b> 고치면 됩니다.
        /// </summary>
        public const int CsvKeyColumn = 3;
        public const int CsvValueColumn = 4;
    }
}
