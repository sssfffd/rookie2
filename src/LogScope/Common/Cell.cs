namespace LogScope.Common
{
    public enum CellKind : byte
    {
        Empty = 0,
        Number = 1,
        Text = 2,
        Bool = 3,
        /// <summary>엑셀 날짜 서식이 걸린 숫자. Num 은 1900 기준 일련값입니다.</summary>
        DateSerial = 4,
    }

    /// <summary>
    /// 표의 칸 하나. 숫자인지 글자인지 구분을 유지해야 머리 행/열을 찾고
    /// 채널 타입을 판별하는 일이 정확해집니다.
    /// 읽는 동안만 쓰는 임시 구조이고, 표 전체를 이 모양으로 들고 있지는 않습니다.
    /// </summary>
    public struct Cell
    {
        public CellKind Kind;
        public double Num;
        public string Text;

        /// <summary>
        /// 파일에 적힌 글자 그대로 (1.74, csv 만). 머리 칸을 IO 이름으로 쓸 때 — "HIGH" · "ON" · "F" 같은 이름이
        /// 참거짓으로 읽혀 "TRUE" · "FALSE" 로 바뀌던 것, "007" 이 "7" 로 바뀌던 것을 막습니다. 없으면 null.
        /// </summary>
        public string Source;

        /// <summary>이름으로 쓸 글 — 적힌 글자 그대로, 없으면 Display.</summary>
        public string Name()
        {
            return !string.IsNullOrEmpty(Source) ? Source : Display();
        }

        public bool IsEmpty { get { return Kind == CellKind.Empty; } }
        public bool IsTextLike { get { return Kind == CellKind.Text; } }
        public bool IsNumberLike
        {
            get { return Kind == CellKind.Number || Kind == CellKind.Bool || Kind == CellKind.DateSerial; }
        }

        public static Cell Empty { get { return new Cell(); } }

        public static Cell FromNumber(double v)
        {
            Cell c = new Cell(); c.Kind = CellKind.Number; c.Num = v; return c;
        }
        public static Cell FromDateSerial(double v)
        {
            Cell c = new Cell(); c.Kind = CellKind.DateSerial; c.Num = v; return c;
        }
        public static Cell FromBool(bool v)
        {
            Cell c = new Cell(); c.Kind = CellKind.Bool; c.Num = v ? 1 : 0; return c;
        }
        public static Cell FromText(string s)
        {
            Cell c = new Cell();
            if (string.IsNullOrEmpty(s)) return c;
            c.Kind = CellKind.Text; c.Text = s; return c;
        }

        public string Display()
        {
            switch (Kind)
            {
                case CellKind.Text: return Text;
                case CellKind.Bool: return Num != 0 ? "TRUE" : "FALSE";
                case CellKind.Empty: return string.Empty;
                default: return Num.ToString("0.######");
            }
        }
    }
}
