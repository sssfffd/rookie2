using LogScope.Core.Settings;

namespace LogScope.Core.Db
{
    /// <summary>
    /// DB 분석 목록의 <b>칸 이름이 적혀 있는 유일한 곳</b>입니다.
    ///
    /// 전에는 같은 이름이 네 군데 흩어져 있었습니다 — 화면(DbView.xaml),
    /// CSV 머리글, 마우스 설명 글, 그리고 시험. 이름 하나 바꾸려고 네 파일을
    /// 열어야 했고, 한 군데를 빠뜨리면 화면과 CSV 가 서로 다른 이름을
    /// 쓰게 됩니다. 이제 셋 다 이 파일을 봅니다.
    ///
    /// <b>이름만 바꾸려면 코드를 안 고쳐도 됩니다.</b> out\config.txt 에
    /// 한 줄 적고 프로그램을 다시 켜면 됩니다. 빌드도 필요 없습니다.
    /// <code>
    ///   db.col.v1 = 설정값
    ///   db.col.v2 = 단위
    /// </code>
    ///
    /// 기본값(아무것도 안 적었을 때 나오는 이름)을 바꾸려면 아래
    /// <c>Default...</c> 한 줄만 고치면 화면·CSV·설명 글이 같이 바뀝니다.
    /// </summary>
    public static class DbNames
    {
        // ---- 기본값. config.txt 에 줄을 안 적었으면 이게 나옵니다 ----

        public const string DefaultTable = "표";
        public const string DefaultIo = "IO명";
        public const string DefaultKind = "갈래";
        public const string DefaultChange = "구분";
        public const string DefaultWhere = "행";
        public const string DefaultColumn = "열";
        public const string DefaultBefore = "이전 value";
        public const string DefaultAfter = "이후 value";
        public const string DefaultV1 = "v1";
        public const string DefaultV2 = "v2";
        public const string DefaultNote = "설명";

        // ---- 지금 쓰는 이름 ----
        //
        // 화면(XAML)은 x:Static 으로 이 속성을 바로 읽습니다. 목록 칸은
        // 화면 나무(visual tree)에 없어서 보통 Binding 이 듣지 않습니다.

        public static string Table { get { return Of("table", DefaultTable); } }
        public static string Io { get { return Of("io", DefaultIo); } }
        public static string Kind { get { return Of("kind", DefaultKind); } }
        public static string Change { get { return Of("change", DefaultChange); } }
        public static string Where { get { return Of("where", DefaultWhere); } }
        public static string Column { get { return Of("column", DefaultColumn); } }
        public static string Before { get { return Of("before", DefaultBefore); } }
        public static string After { get { return Of("after", DefaultAfter); } }
        public static string V1 { get { return Of("v1", DefaultV1); } }
        public static string V2 { get { return Of("v2", DefaultV2); } }
        public static string Note { get { return Of("note", DefaultNote); } }

        /// <summary>
        /// CSV 머리글에 들어갈 이름들. <b>순서가 CSV 가 쓰는 순서입니다</b> —
        /// <c>DbDiffList.ToCsv</c> 가 이 배열대로 머리글을 찍습니다. 칸을
        /// 더하거나 순서를 바꿀 때는 여기와 그 쪽 값 쓰는 줄을 같이 고치세요.
        ///
        /// 화면에 보이는 칸은 이보다 적습니다 (갈래·구분·행·설명은 CSV 에만).
        /// 그래서 화면 쪽은 위 속성을 하나씩 가져다 씁니다.
        /// </summary>
        public static string[] CsvNames()
        {
            return new[]
            {
                Table, Io, Kind, Change, Where, Column, Before, After, V1, V2, Note,
            };
        }

        /// <summary>config.txt 에 적어 둔 이름이 있으면 그것, 없으면 기본값.</summary>
        private static string Of(string key, string fallback)
        {
            AppConfig cfg = AppConfig.Current;
            return cfg == null ? fallback : cfg.ColumnName(key, fallback);
        }
    }
}
