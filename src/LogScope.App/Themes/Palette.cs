using System.Windows.Media;

namespace LogScope.App.Themes
{
    /// <summary>
    /// 그래프를 직접 그릴 때 쓰는 색.
    ///
    /// 화면의 나머지(버튼, 목록 등)는 XAML 의 브러시 자원을 DynamicResource 로
    /// 가져다 쓰므로 테마를 바꾸면 바로 반영됩니다. 하지만 GraphCanvas 는
    /// OnRender 안에서 색을 직접 쓰기 때문에, 그쪽에서 쓸 값만 여기에
    /// 모아 둡니다. ThemeManager 가 테마를 바꿀 때 이 값도 같이 갈아 끼웁니다.
    ///
    /// Brush 는 만들자마자 Freeze 합니다. 얼린 브러시는 스레드 잠금 없이
    /// 그려지고, 매 프레임 새로 만들 필요도 없어집니다.
    /// </summary>
    public sealed class Palette
    {
        public bool IsDark;

        public Color Window;
        public Color Panel;
        public Color Plot;
        public Color Grid;
        public Color GridStrong;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color Accent;

        public Color Before;
        public Color After;
        public Color DiffShade;

        public Color CursorA;
        public Color CursorB;

        // 히트맵 칸 색
        public Color HeatNoData;    // 기록이 없는 칸
        public Color HeatNormal;    // 차이 없음 (정상)
        public Color HeatLow;       // 차이 남 - 약함
        public Color HeatHigh;      // 차이 남 - 강함
        public Color HeatStripe;    // 줄 번갈아 칠하기

        public Color[] Series;

        // 자주 쓰는 것들은 얼린 채로 들고 있습니다.
        public Brush PlotBrush;
        public Brush AxisStripBrush;
        public Brush HeatNoDataBrush;
        public Brush HeatNormalBrush;
        public Brush HeatStripeBrush;
        public Brush TextBrush;
        public Brush MutedBrush;
        public Brush DiffBrush;
        public Pen GridPen;
        public Pen BorderPen;
        public Pen BeforePen;
        public Pen AfterPen;
        /// <summary>"이후 − 이전" 차이 선. 이전/이후 어느 쪽 색도 아니어야
        /// 값 눈금과 헷갈리지 않습니다.</summary>
        public Pen DiffPen;
        public Pen CursorAPen;
        public Pen CursorBPen;

        private readonly Pen[] _seriesPens;

        public Pen SeriesPen(int i)
        {
            int n = _seriesPens.Length;
            return _seriesPens[((i % n) + n) % n];
        }

        public Color SeriesColor(int i)
        {
            int n = Series.Length;
            return Series[((i % n) + n) % n];
        }

        private Palette(Color[] series)
        {
            Series = series;
            _seriesPens = new Pen[series.Length];
        }

        private void Seal()
        {
            PlotBrush = Frozen(Plot);
            AxisStripBrush = Frozen(Panel);
            HeatNoDataBrush = Frozen(HeatNoData);
            HeatNormalBrush = Frozen(HeatNormal);
            HeatStripeBrush = Frozen(HeatStripe);
            TextBrush = Frozen(Text);
            MutedBrush = Frozen(Muted);
            DiffBrush = Frozen(DiffShade);
            GridPen = FrozenPen(Grid, 1);
            BorderPen = FrozenPen(GridStrong, 1);
            BeforePen = FrozenPen(Before, 1.4);
            AfterPen = FrozenPen(After, 1.4);
            // 차이 선은 강조색으로 조금 굵게 긋습니다. 이전/이후 어느 쪽
            // 색도 아니어야 "이건 두 로그를 뺀 선" 이라는 게 한눈에 보입니다.
            DiffPen = FrozenPen(Accent, 1.6);
            CursorAPen = FrozenPen(CursorA, 1.2);
            CursorBPen = FrozenPen(CursorB, 1.2);
            for (int i = 0; i < Series.Length; i++) _seriesPens[i] = FrozenPen(Series[i], 1.4);
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Pen FrozenPen(Color c, double thickness)
        {
            var p = new Pen(Frozen(c), thickness);
            p.LineJoin = PenLineJoin.Bevel;
            p.StartLineCap = PenLineCap.Flat;
            p.EndLineCap = PenLineCap.Flat;
            p.Freeze();
            return p;
        }

        private static Color Rgb(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }
        private static Color Argb(byte a, byte r, byte g, byte b) { return Color.FromArgb(a, r, g, b); }

        public static Palette Light()
        {
            var p = new Palette(new[]
            {
                Rgb(0x16, 0x62, 0xC9), Rgb(0xE0, 0x62, 0x0D), Rgb(0x1E, 0x8E, 0x3E), Rgb(0xA8, 0x2E, 0xA8),
                Rgb(0xC5, 0x29, 0x1A), Rgb(0x0C, 0x8A, 0x85), Rgb(0x7A, 0x3A, 0xB8), Rgb(0x8A, 0x6D, 0x1F),
                Rgb(0x2B, 0x6B, 0x8F), Rgb(0xB0, 0x3A, 0x6B),
            });
            p.IsDark = false;
            p.Window = Colors.White;
            p.Panel = Rgb(0xF5, 0xF7, 0xFA);
            p.Plot = Colors.White;
            p.Grid = Rgb(0xEC, 0xEF, 0xF4);
            p.GridStrong = Rgb(0xD6, 0xDD, 0xE6);
            p.Border = Rgb(0xD6, 0xDD, 0xE6);
            p.Text = Rgb(0x1B, 0x27, 0x33);
            p.Muted = Rgb(0x69, 0x73, 0x7F);
            p.Accent = Rgb(0x1E, 0x5A, 0xA8);
            p.Before = Rgb(0x16, 0x62, 0xC9);
            p.After = Rgb(0xE0, 0x62, 0x0D);
            p.DiffShade = Argb(46, 0xE0, 0x62, 0x0D);
            p.CursorA = Rgb(0x7A, 0x3A, 0xB8);
            p.CursorB = Rgb(0x0C, 0x8A, 0x85);
            p.HeatNoData = Rgb(0xEE, 0xF1, 0xF5);
            p.HeatNormal = Rgb(0xE4, 0xF3, 0xE7);
            p.HeatLow = Rgb(0xFA, 0xD4, 0xCC);
            p.HeatHigh = Rgb(0xB3, 0x18, 0x12);
            p.HeatStripe = Rgb(0xFA, 0xFB, 0xFD);
            p.Seal();
            return p;
        }

        public static Palette Dark()
        {
            var p = new Palette(new[]
            {
                Rgb(0x5A, 0xA6, 0xFF), Rgb(0xFF, 0xA0, 0x44), Rgb(0x63, 0xCE, 0x85), Rgb(0xD9, 0x8B, 0xE8),
                Rgb(0xF0, 0x6A, 0x5B), Rgb(0x4E, 0xC9, 0xC2), Rgb(0xA9, 0x9B, 0xFF), Rgb(0xD7, 0xBC, 0x5E),
                Rgb(0x74, 0xB6, 0xD6), Rgb(0xEF, 0x8F, 0xB8),
            });
            p.IsDark = true;
            p.Window = Rgb(0x17, 0x1A, 0x1F);
            p.Panel = Rgb(0x1F, 0x23, 0x2A);
            p.Plot = Rgb(0x1A, 0x1E, 0x24);
            p.Grid = Rgb(0x25, 0x2A, 0x31);
            p.GridStrong = Rgb(0x38, 0x3F, 0x49);
            p.Border = Rgb(0x35, 0x3C, 0x45);
            p.Text = Rgb(0xE6, 0xE9, 0xED);
            p.Muted = Rgb(0x96, 0xA0, 0xAC);
            p.Accent = Rgb(0x53, 0x9D, 0xF5);
            p.Before = Rgb(0x5A, 0xA6, 0xFF);
            p.After = Rgb(0xFF, 0xA0, 0x44);
            p.DiffShade = Argb(56, 0xFF, 0xA0, 0x44);
            p.CursorA = Rgb(0xB9, 0x8B, 0xE8);
            p.CursorB = Rgb(0x4E, 0xC9, 0xC2);
            p.HeatNoData = Rgb(0x23, 0x27, 0x2E);
            p.HeatNormal = Rgb(0x1E, 0x33, 0x25);
            p.HeatLow = Rgb(0x5A, 0x2A, 0x24);
            p.HeatHigh = Rgb(0xE5, 0x4B, 0x3C);
            p.HeatStripe = Rgb(0x1B, 0x1F, 0x25);
            p.Seal();
            return p;
        }
    }
}
