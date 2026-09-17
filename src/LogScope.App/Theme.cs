using System;
using System.Drawing;
using System.Windows.Forms;

namespace LogScope.App
{
    /// <summary>
    /// 색과 글꼴을 한 곳에 모아 둡니다. 바탕은 흰색, 위쪽 메뉴 줄은 진한 파랑.
    /// 어두운 테마도 같은 자리에서 색만 바꿔 씁니다.
    /// </summary>
    public sealed class Theme
    {
        public static Theme Current = Light();

        public bool IsDark;

        public Color Window;
        public Color Panel;
        public Color PanelAlt;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color Accent;
        public Color AccentSoft;

        public Color TopBar;
        public Color TopBarText;
        public Color TopBarMuted;
        public Color TopBarActive;

        public Color Grid;
        public Color GridStrong;
        public Color Plot;

        public Color Before;
        public Color After;
        public Color DiffShade;

        public Color Good;
        public Color Warn;
        public Color Bad;

        public Color CursorA;
        public Color CursorB;

        /// <summary>겹쳐보기에서 채널마다 돌려쓰는 색. 서로 잘 구분되도록 골랐습니다.</summary>
        public Color[] Series;

        public static Theme Light()
        {
            var t = new Theme();
            t.IsDark = false;
            t.Window = Color.White;
            t.Panel = Color.FromArgb(0xF5, 0xF7, 0xFA);
            t.PanelAlt = Color.FromArgb(0xEC, 0xF0, 0xF6);
            t.Border = Color.FromArgb(0xD6, 0xDD, 0xE6);
            t.Text = Color.FromArgb(0x1B, 0x27, 0x33);
            t.Muted = Color.FromArgb(0x69, 0x73, 0x7F);
            t.Accent = Color.FromArgb(0x1E, 0x5A, 0xA8);
            t.AccentSoft = Color.FromArgb(0xE3, 0xEC, 0xF8);

            t.TopBar = Color.FromArgb(0x10, 0x35, 0x62);
            t.TopBarText = Color.FromArgb(0xF2, 0xF6, 0xFB);
            t.TopBarMuted = Color.FromArgb(0xA9, 0xBE, 0xDA);
            t.TopBarActive = Color.FromArgb(0x1E, 0x5A, 0xA8);

            t.Grid = Color.FromArgb(0xEC, 0xEF, 0xF4);
            t.GridStrong = Color.FromArgb(0xD8, 0xDE, 0xE7);
            t.Plot = Color.White;

            t.Before = Color.FromArgb(0x16, 0x62, 0xC9);
            t.After = Color.FromArgb(0xE0, 0x62, 0x0D);
            t.DiffShade = Color.FromArgb(46, 0xE0, 0x62, 0x0D);

            t.Good = Color.FromArgb(0x1E, 0x8E, 0x3E);
            t.Warn = Color.FromArgb(0xC7, 0x7D, 0x00);
            t.Bad = Color.FromArgb(0xC5, 0x29, 0x1A);

            t.CursorA = Color.FromArgb(0x7A, 0x3A, 0xB8);
            t.CursorB = Color.FromArgb(0x0C, 0x8A, 0x85);
            t.Series = SeriesLight();
            return t;
        }

        public static Theme Dark()
        {
            var t = new Theme();
            t.IsDark = true;
            t.Window = Color.FromArgb(0x17, 0x1A, 0x1F);
            t.Panel = Color.FromArgb(0x1F, 0x23, 0x2A);
            t.PanelAlt = Color.FromArgb(0x27, 0x2C, 0x34);
            t.Border = Color.FromArgb(0x35, 0x3C, 0x45);
            t.Text = Color.FromArgb(0xE6, 0xE9, 0xED);
            t.Muted = Color.FromArgb(0x96, 0xA0, 0xAC);
            t.Accent = Color.FromArgb(0x53, 0x9D, 0xF5);
            t.AccentSoft = Color.FromArgb(0x21, 0x31, 0x45);

            t.TopBar = Color.FromArgb(0x0C, 0x24, 0x44);
            t.TopBarText = Color.FromArgb(0xEC, 0xF1, 0xF8);
            t.TopBarMuted = Color.FromArgb(0x8F, 0xA6, 0xC4);
            t.TopBarActive = Color.FromArgb(0x1B, 0x4E, 0x91);

            t.Grid = Color.FromArgb(0x25, 0x2A, 0x31);
            t.GridStrong = Color.FromArgb(0x38, 0x3F, 0x49);
            t.Plot = Color.FromArgb(0x1A, 0x1E, 0x24);

            t.Before = Color.FromArgb(0x5A, 0xA6, 0xFF);
            t.After = Color.FromArgb(0xFF, 0xA0, 0x44);
            t.DiffShade = Color.FromArgb(56, 0xFF, 0xA0, 0x44);

            t.Good = Color.FromArgb(0x54, 0xC1, 0x76);
            t.Warn = Color.FromArgb(0xE8, 0xB3, 0x39);
            t.Bad = Color.FromArgb(0xF0, 0x6A, 0x5B);

            t.CursorA = Color.FromArgb(0xB9, 0x8B, 0xE8);
            t.CursorB = Color.FromArgb(0x4E, 0xC9, 0xC2);
            t.Series = SeriesDark();
            return t;
        }

        private static Color[] SeriesLight()
        {
            return new[]
            {
                Color.FromArgb(0x16, 0x62, 0xC9), Color.FromArgb(0xE0, 0x62, 0x0D),
                Color.FromArgb(0x1E, 0x8E, 0x3E), Color.FromArgb(0xA8, 0x2E, 0xA8),
                Color.FromArgb(0xC5, 0x29, 0x1A), Color.FromArgb(0x0C, 0x8A, 0x85),
                Color.FromArgb(0x7A, 0x3A, 0xB8), Color.FromArgb(0x8A, 0x6D, 0x1F),
                Color.FromArgb(0x2B, 0x6B, 0x8F), Color.FromArgb(0xB0, 0x3A, 0x6B),
            };
        }

        private static Color[] SeriesDark()
        {
            return new[]
            {
                Color.FromArgb(0x5A, 0xA6, 0xFF), Color.FromArgb(0xFF, 0xA0, 0x44),
                Color.FromArgb(0x63, 0xCE, 0x85), Color.FromArgb(0xD9, 0x8B, 0xE8),
                Color.FromArgb(0xF0, 0x6A, 0x5B), Color.FromArgb(0x4E, 0xC9, 0xC2),
                Color.FromArgb(0xA9, 0x9B, 0xFF), Color.FromArgb(0xD7, 0xBC, 0x5E),
                Color.FromArgb(0x74, 0xB6, 0xD6), Color.FromArgb(0xEF, 0x8F, 0xB8),
            };
        }

        public Color SeriesColor(int i)
        {
            if (Series == null || Series.Length == 0) return Accent;
            return Series[((i % Series.Length) + Series.Length) % Series.Length];
        }

        // ---- 글꼴 --------------------------------------------------------
        // 시스템 기본 UI 글꼴을 씁니다. 맑은 고딕이 없는 윈도우 7 에서도
        // 알아서 굴림으로 떨어지므로 글꼴 이름을 박아 넣지 않습니다.

        private static Font _ui, _uiBold, _small, _smallBold, _big, _mono;

        public static Font Ui { get { return _ui ?? (_ui = Base()); } }
        public static Font UiBold { get { return _uiBold ?? (_uiBold = new Font(Ui, FontStyle.Bold)); } }
        public static Font Small { get { return _small ?? (_small = new Font(Ui.FontFamily, Ui.SizeInPoints - 0.5f)); } }
        public static Font SmallBold { get { return _smallBold ?? (_smallBold = new Font(Small, FontStyle.Bold)); } }
        public static Font Big { get { return _big ?? (_big = new Font(Ui.FontFamily, Ui.SizeInPoints + 7f, FontStyle.Bold)); } }
        public static Font Mono
        {
            get
            {
                if (_mono == null)
                {
                    try { _mono = new Font("Consolas", Ui.SizeInPoints); }
                    catch (ArgumentException) { _mono = new Font(FontFamily.GenericMonospace, Ui.SizeInPoints); }
                    if (!_mono.Name.StartsWith("Consolas", StringComparison.OrdinalIgnoreCase))
                        _mono = new Font(FontFamily.GenericMonospace, Ui.SizeInPoints);
                }
                return _mono;
            }
        }

        private static Font Base()
        {
            try
            {
                Font f = SystemFonts.MessageBoxFont;
                if (f != null) return (Font)f.Clone();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
            return new Font(FontFamily.GenericSansSerif, 9f);
        }

        public static void Apply(string name)
        {
            Current = string.Equals(name, "dark", StringComparison.OrdinalIgnoreCase) ? Dark() : Light();
        }
    }
}

namespace LogScope.App
{
    /// <summary>
    /// 화면 배율. 창이 다른 배율의 모니터로 옮겨 가면 MainForm 이 여기를
    /// 갱신하고 화면을 다시 잡습니다. 직접 그리는 컨트롤은 크기를 px 로
    /// 박아 두지 않고 전부 Dpi.S() 를 거칩니다.
    /// </summary>
    public static class Dpi
    {
        private static float _scale = 1.0f;

        public static float Scale
        {
            get { return _scale; }
            set { _scale = value < 0.5f ? 0.5f : (value > 6f ? 6f : value); }
        }

        public static int S(int px) { return (int)System.Math.Round(px * _scale); }
        public static float F(float px) { return px * _scale; }

        public static System.Drawing.Size S(int w, int h)
        {
            return new System.Drawing.Size(S(w), S(h));
        }
    }
}
