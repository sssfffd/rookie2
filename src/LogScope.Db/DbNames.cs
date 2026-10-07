namespace LogScope.Db
{
    /// <summary>
    /// DB 분석 목록의 <b>칸 이름이 적혀 있는 유일한 곳</b>입니다.
    ///
    /// <b>이름을 바꾸려면 이 파일의 한 줄만 고치면 됩니다.</b> 화면 머리글 ·
    /// CSV 머리글 · 마우스 설명 글이 전부 여기를 봅니다.
    ///
    /// <code>
    ///   public const string V1 = "설정값";
    ///   public const string V2 = "단위";
    /// </code>
    ///
    /// 전에는 같은 이름이 네 군데 흩어져 있었습니다 — 화면(DbView.xaml),
    /// CSV 머리글, 마우스 설명 글, 그리고 시험. 이름 하나 바꾸려고 네 파일을
    /// 열어야 했고, 한 군데를 빠뜨리면 화면과 CSV 가 서로 다른 이름을
    /// 쓰게 됩니다. 이제 셋 다 이 파일을 봅니다.
    ///
    /// 0.75 에서는 config.txt 로도 바꿀 수 있게 했는데, 0.77 에서 되돌렸습니다
    /// (DB 설정을 손으로 고치는 파일에 두지 않기로 했습니다). 그래서 이름을
    /// 바꾸면 <b>다시 빌드해야</b> 합니다 — 한 줄이고, 한 곳입니다.
    /// </summary>
    public static class DbNames
    {
        public const string Table = "표";
        public const string Io = "IO명";
        public const string Kind = "갈래";
        public const string Change = "구분";
        public const string Where = "행";
        public const string Column = "열";
        public const string Before = "이전 value";
        public const string After = "이후 value";
        public const string V1 = "v1";
        public const string V2 = "v2";
        public const string Note = "설명";

        /// <summary>
        /// CSV 머리글에 들어갈 이름들. <b>순서가 CSV 가 쓰는 순서입니다</b> —
        /// <c>DbDiffList.ToCsv</c> 가 이 배열대로 머리글을 찍습니다. 칸을
        /// 더하거나 순서를 바꿀 때는 여기와 그 쪽 값 쓰는 줄을 같이 고치세요.
        ///
        /// 화면에 보이는 칸은 이보다 적습니다 (갈래·구분·행·설명은 CSV 에만).
        /// 그래서 화면 쪽은 위 상수를 하나씩 가져다 씁니다.
        /// </summary>
        public static string[] CsvNames()
        {
            return new[]
            {
                Table, Io, Kind, Change, Where, Column, Before, After, V1, V2, Note,
            };
        }
    }
}
