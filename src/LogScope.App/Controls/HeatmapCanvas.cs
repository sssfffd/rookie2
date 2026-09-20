using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.App.Themes;
using LogScope.Core.Compare;
using LogScope.Core.Model;

namespace LogScope.App.Controls
{
    /// <summary>
    /// 히트맵을 직접 그리는 판.
    ///
    /// 왼쪽에 차이가 난 IO 이름, 가로축은 시간(기본 1 분 단위)입니다.
    /// 그 시간대에 차이가 났으면 빨간 칸에 그 구간의 <b>절대 차이 평균</b>을
    /// 적고, 차이가 없었으면 "정상" 칸으로 칠합니다.
    ///
    /// 칸이 수만 개가 될 수 있으므로 <b>화면에 보이는 칸만</b> 그립니다.
    /// 줄 200 개 x 칸 500 개여도 실제로 그리는 것은 수천 개뿐입니다.
    /// </summary>
    public sealed class HeatmapCanvas : FrameworkElement
    {
        private HeatmapResult _result;

        private readonly Typeface _face = new Typeface("Segoe UI");
        private const double FontNormal = 12.0;
        private const double FontSmall = 11.0;

        /// <summary>왼쪽 이름 칸의 너비.</summary>
        private const double NameW = 196;
        /// <summary>위쪽 시간 눈금 줄의 높이.</summary>
        private const double HeaderH = 30;

        private int _hoverRow = -1;
        private int _hoverCol = -1;

        public HeatmapCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            ThemeManager.Changed += delegate { InvalidateVisual(); };
        }

        // ---------------- 바깥에서 설정하는 것 ----------------

        public void SetResult(HeatmapResult result)
        {
            _result = result;
            HorizontalOffset = 0;
            VerticalOffset = 0;
            _hoverRow = -1;
            _hoverCol = -1;
            InvalidateVisual();
        }

        public HeatmapResult Result { get { return _result; } }

        private double _cellW = 58;
        /// <summary>칸 하나의 너비. 40 이상이면 칸 안에 숫자를 적습니다.</summary>
        public double CellWidth
        {
            get { return _cellW; }
            set
            {
                double v = value < 6 ? 6 : (value > 200 ? 200 : value);
                if (_cellW == v) return;
                _cellW = v;
                InvalidateVisual();
            }
        }

        private double _rowH = 26;
        public double RowHeight
        {
            get { return _rowH; }
            set
            {
                double v = value < 14 ? 14 : (value > 60 ? 60 : value);
                if (_rowH == v) return;
                _rowH = v;
                InvalidateVisual();
            }
        }

        private bool _showPercent;

        /// <summary>
        /// 칸에 적는 차이를 퍼센트로 볼지, 값 그대로 볼지.
        ///
        /// 퍼센트는 이전 값 대비입니다 — 설정에 적는 허용 오차와 <b>같은
        /// 잣대</b>라, 허용 오차 0.1% 와 칸의 0.3% 를 눈으로 바로 견줄 수
        /// 있습니다. 반대로 "몇 도 틀어졌나" 처럼 값 자체가 알고 싶을 때가
        /// 있어서 둘 다 볼 수 있게 두었습니다.
        ///
        /// 둘은 <b>같은 순간</b>(가장 크게 벌어진 표본)의 두 가지 표현입니다.
        /// </summary>
        public bool ShowPercent
        {
            get { return _showPercent; }
            set
            {
                if (_showPercent == value) return;
                _showPercent = value;
                InvalidateVisual();
            }
        }

        private double _tolerancePercent = LogScope.Core.Compare.ToleranceRule.DefaultPercent;

        /// <summary>
        /// 지금 그려진 히트맵을 만들 때 쓴 허용 오차 퍼센트.
        /// 설명 줄에 그대로 적어서, 칸에 적힌 퍼센트와 바로 견줄 수 있게 합니다.
        /// 값이 바뀌어도 다시 그릴 필요는 없습니다 — 설명 줄은 마우스를
        /// 올릴 때마다 새로 만듭니다.
        /// </summary>
        public double TolerancePercent
        {
            get { return _tolerancePercent; }
            set { _tolerancePercent = value; }
        }

        /// <summary>마우스가 칸 위에 올라갔을 때의 설명 글.</summary>
        public event EventHandler<string> HoverTextChanged;

        /// <summary>칸을 눌렀을 때. 그래프 화면으로 넘어가는 데 씁니다.</summary>
        public event EventHandler<CellEventArgs> CellActivated;

        public sealed class CellEventArgs : EventArgs
        {
            public readonly string Name;
            public readonly double Start;
            public readonly double End;
            public CellEventArgs(string name, double start, double end)
            {
                Name = name; Start = start; End = end;
            }
        }

        // ---------------- 스크롤 ----------------

        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register("VerticalOffset", typeof(double), typeof(HeatmapCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double VerticalOffset
        {
            get { return (double)GetValue(VerticalOffsetProperty); }
            set { SetValue(VerticalOffsetProperty, value); }
        }

        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register("HorizontalOffset", typeof(double), typeof(HeatmapCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double HorizontalOffset
        {
            get { return (double)GetValue(HorizontalOffsetProperty); }
            set { SetValue(HorizontalOffsetProperty, value); }
        }

        public static readonly DependencyProperty VerticalMaximumProperty =
            DependencyProperty.Register("VerticalMaximum", typeof(double), typeof(HeatmapCanvas),
                new PropertyMetadata(0.0));

        public double VerticalMaximum
        {
            get { return (double)GetValue(VerticalMaximumProperty); }
            private set { SetValue(VerticalMaximumProperty, value); }
        }

        public static readonly DependencyProperty VerticalViewportProperty =
            DependencyProperty.Register("VerticalViewport", typeof(double), typeof(HeatmapCanvas),
                new PropertyMetadata(1.0));

        public double VerticalViewport
        {
            get { return (double)GetValue(VerticalViewportProperty); }
            private set { SetValue(VerticalViewportProperty, value); }
        }

        public static readonly DependencyProperty HorizontalMaximumProperty =
            DependencyProperty.Register("HorizontalMaximum", typeof(double), typeof(HeatmapCanvas),
                new PropertyMetadata(0.0));

        public double HorizontalMaximum
        {
            get { return (double)GetValue(HorizontalMaximumProperty); }
            private set { SetValue(HorizontalMaximumProperty, value); }
        }

        public static readonly DependencyProperty HorizontalViewportProperty =
            DependencyProperty.Register("HorizontalViewport", typeof(double), typeof(HeatmapCanvas),
                new PropertyMetadata(1.0));

        public double HorizontalViewport
        {
            get { return (double)GetValue(HorizontalViewportProperty); }
            private set { SetValue(HorizontalViewportProperty, value); }
        }

        private double GridWidth { get { return Math.Max(10, ActualWidth - NameW); } }
        private double GridHeight { get { return Math.Max(10, ActualHeight - HeaderH); } }

        // ---------------- 그리기 ----------------

        protected override void OnRender(DrawingContext dc)
        {
            Palette p = ThemeManager.Palette;
            dc.DrawRectangle(Frozen(p.Window), null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (_result == null || _result.Rows.Count == 0)
            {
                string msg = _result == null
                    ? "이전 로그와 이후 로그를 연 다음 [히트맵 다시 계산] 을 눌러 주세요."
                    : (_result.Warning.Length > 0 ? _result.Warning : "보여 줄 줄이 없습니다.");
                FormattedText empty = Text(msg, FontNormal, p.MutedBrush);
                empty.MaxTextWidth = Math.Max(120, ActualWidth - 48);
                dc.DrawText(empty, new Point(24, 24));
                UpdateScroll(0, 1, 0, 1);
                return;
            }

            int rows = _result.Rows.Count;
            int cols = _result.BucketCount;

            double extentY = rows * _rowH;
            double extentX = cols * _cellW;
            UpdateScroll(extentY, GridHeight, extentX, GridWidth);

            double offY = extentY > GridHeight ? VerticalOffset : 0;
            double offX = extentX > GridWidth ? HorizontalOffset : 0;

            int firstRow = Math.Max(0, (int)(offY / _rowH));
            int lastRow = Math.Min(rows - 1, (int)((offY + GridHeight) / _rowH));
            int firstCol = Math.Max(0, (int)(offX / _cellW));
            int lastCol = Math.Min(cols - 1, (int)((offX + GridWidth) / _cellW));

            bool showNumbers = _cellW >= 40 && _rowH >= 18;

            // ---- 칸 ----
            dc.PushClip(new RectangleGeometry(new Rect(NameW, HeaderH, GridWidth, GridHeight)));
            for (int r = firstRow; r <= lastRow; r++)
            {
                HeatRow row = _result.Rows[r];
                double y = HeaderH + r * _rowH - offY;

                if ((r & 1) == 1)
                    dc.DrawRectangle(p.HeatStripeBrush, null, new Rect(NameW, y, GridWidth, _rowH));

                for (int c = firstCol; c <= lastCol; c++)
                {
                    double x = NameW + c * _cellW - offX;
                    var cell = new Rect(x + 1, y + 1, Math.Max(1, _cellW - 2), Math.Max(1, _rowH - 2));
                    HeatCell hc = row.Cells[c];

                    Brush fill;
                    Color textColor;
                    string label;

                    if (!hc.HasData)
                    {
                        fill = p.HeatNoDataBrush;
                        textColor = p.Muted;
                        label = showNumbers ? "기록 없음" : null;
                    }
                    else if (hc.OverSamples > 0)
                    {
                        // 차이가 난 칸. 숫자도 색도 "가장 크게 벌어진 순간의
                        // 오차(%)" 하나에서 나옵니다. 진하기는 그 줄에서
                        // 가장 큰 값 대비입니다.
                        double pct = row.PercentOf(hc);
                        double peak = row.PeakMean > 0 ? row.PeakMean : 1;
                        double f = Math.Min(1.0, pct / peak);
                        Color c1 = Blend(p.HeatLow, p.HeatHigh, 0.25 + 0.75 * f);
                        fill = Frozen(c1);
                        textColor = Readable(c1);
                        label = showNumbers ? CellText(row, hc) : null;
                    }
                    else
                    {
                        fill = p.HeatNormalBrush;
                        textColor = p.Muted;
                        label = showNumbers ? "정상" : null;
                    }

                    dc.DrawRectangle(fill, null, cell);

                    if (r == _hoverRow && c == _hoverCol)
                        dc.DrawRectangle(null, p.BorderPen, cell);

                    if (label == null) continue;
                    FormattedText ft = Text(label, FontSmall, Frozen(textColor));
                    if (ft.Width > cell.Width - 4) continue;
                    dc.DrawText(ft, new Point(cell.Left + (cell.Width - ft.Width) * 0.5,
                                              cell.Top + (cell.Height - ft.Height) * 0.5));
                }
            }
            dc.Pop();

            DrawHeader(dc, p, firstCol, lastCol, offX);
            DrawNames(dc, p, firstRow, lastRow, offY);
        }

        private void DrawHeader(DrawingContext dc, Palette p, int firstCol, int lastCol, double offX)
        {
            var strip = new Rect(NameW, 0, GridWidth, HeaderH);
            dc.DrawRectangle(p.AxisStripBrush, null, strip);
            dc.DrawLine(p.BorderPen, new Point(NameW, HeaderH), new Point(ActualWidth, HeaderH));

            LogDataset refDs = _result.TimeReference;

            // 칸이 좁으면 눈금 글자가 겹치므로 몇 칸에 하나만 적습니다.
            double need = 62;
            int every = Math.Max(1, (int)Math.Ceiling(need / _cellW));

            dc.PushClip(new RectangleGeometry(strip));
            for (int c = firstCol; c <= lastCol; c++)
            {
                if (c % every != 0) continue;
                double x = NameW + c * _cellW - offX;
                dc.DrawLine(p.BorderPen, new Point(x, HeaderH - 5), new Point(x, HeaderH));

                double t = _result.BucketStart(c);
                string label = refDs != null ? refDs.FormatTime(t) : t.ToString("0.###");
                FormattedText ft = Text(label, FontSmall, p.MutedBrush);
                dc.DrawText(ft, new Point(x + 3, (HeaderH - ft.Height) * 0.5));
            }
            dc.Pop();
        }

        private void DrawNames(DrawingContext dc, Palette p, int firstRow, int lastRow, double offY)
        {
            // 이름 칸은 가로로 굴러가지 않습니다. 칸들 위에 덮어 그립니다.
            var col = new Rect(0, 0, NameW, ActualHeight);
            dc.DrawRectangle(p.AxisStripBrush, null, col);
            dc.DrawLine(p.BorderPen, new Point(NameW, 0), new Point(NameW, ActualHeight));

            FormattedText caption = Text("차이가 난 IO", FontNormal, p.TextBrush);
            caption.SetFontWeight(FontWeights.SemiBold);
            dc.DrawText(caption, new Point(10, (HeaderH - caption.Height) * 0.5));
            dc.DrawLine(p.BorderPen, new Point(0, HeaderH), new Point(NameW, HeaderH));

            dc.PushClip(new RectangleGeometry(new Rect(0, HeaderH, NameW, GridHeight)));
            for (int r = firstRow; r <= lastRow; r++)
            {
                HeatRow row = _result.Rows[r];
                double y = HeaderH + r * _rowH - offY;

                if (r == _hoverRow)
                    dc.DrawRectangle(Frozen(p.HeatStripe), null, new Rect(0, y, NameW, _rowH));

                FormattedText ft = Text(row.Name, FontNormal, p.TextBrush);
                ft.MaxTextWidth = NameW - 62;
                ft.MaxLineCount = 1;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(10, y + (_rowH - ft.Height) * 0.5));

                FormattedText count = Text(row.OverBuckets + "칸", FontSmall, p.MutedBrush);
                dc.DrawText(count, new Point(NameW - count.Width - 8, y + (_rowH - count.Height) * 0.5));
            }
            dc.Pop();
        }

        private void UpdateScroll(double extentY, double viewY, double extentX, double viewX)
        {
            double maxY = Math.Max(0, extentY - viewY);
            double maxX = Math.Max(0, extentX - viewX);
            if (Math.Abs(VerticalMaximum - maxY) > 0.5) VerticalMaximum = maxY;
            if (Math.Abs(HorizontalMaximum - maxX) > 0.5) HorizontalMaximum = maxX;
            if (Math.Abs(VerticalViewport - viewY) > 0.5) VerticalViewport = Math.Max(1, viewY);
            if (Math.Abs(HorizontalViewport - viewX) > 0.5) HorizontalViewport = Math.Max(1, viewX);
            if (VerticalOffset > maxY) VerticalOffset = maxY;
            if (HorizontalOffset > maxX) HorizontalOffset = maxX;
        }

        // ---------------- 잡다한 것 ----------------

        private FormattedText Text(string s, double size, Brush brush)
        {
            return new FormattedText(s ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, _face, size, brush);
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Color Blend(Color a, Color b, double f)
        {
            if (f < 0) f = 0;
            if (f > 1) f = 1;
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * f),
                (byte)(a.G + (b.G - a.G) * f),
                (byte)(a.B + (b.B - a.B) * f));
        }

        /// <summary>바탕색 위에서 읽히는 글자색 (밝으면 검정, 어두우면 흰색).</summary>
        private static Color Readable(Color back)
        {
            double lum = (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) / 255.0;
            return lum > 0.55 ? Color.FromRgb(0x20, 0x20, 0x20) : Colors.White;
        }

        /// <summary>
        /// 칸 안에 적을 글자. 단위는 칸이 좁아 넣지 않고 설명 줄에만 적습니다.
        ///
        /// 퍼센트든 값이든 <b>같은 순간</b>(가장 크게 벌어진 표본)에서 나옵니다.
        /// 보기를 바꿔도 가리키는 자리가 달라지지 않습니다.
        /// </summary>
        private string CellText(HeatRow row, HeatCell cell)
        {
            if (_showPercent) return NumberText.Short(row.PercentOf(cell)) + "%";
            return NumberText.Short(row.ValueOf(cell));
        }

        /// <summary>설명 줄에 쓸, 값과 단위를 붙인 글자.</summary>
        private static string WithUnit(double v, HeatRow row)
        {
            string s = Compact(v);
            return row.Unit.Length > 0 ? s + " " + row.Unit : s;
        }

        /// <summary>
        /// 칸에 들어갈 만큼 짧게. 지수 표기는 쓰지 않습니다 (NumberText 참고).
        /// </summary>
        private static string Compact(double v)
        {
            return NumberText.Short(v);
        }

        // ---------------- 마우스 ----------------

        private bool HitCell(Point at, out int row, out int col)
        {
            row = -1; col = -1;
            if (_result == null || _result.Rows.Count == 0) return false;
            if (at.X < NameW || at.Y < HeaderH) return false;

            double extentY = _result.Rows.Count * _rowH;
            double extentX = _result.BucketCount * _cellW;
            double offY = extentY > GridHeight ? VerticalOffset : 0;
            double offX = extentX > GridWidth ? HorizontalOffset : 0;

            row = (int)((at.Y - HeaderH + offY) / _rowH);
            col = (int)((at.X - NameW + offX) / _cellW);

            if (row < 0 || row >= _result.Rows.Count) { row = -1; return false; }
            if (col < 0 || col >= _result.BucketCount) { col = -1; return false; }
            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int r, c;
            bool hit = HitCell(e.GetPosition(this), out r, out c);
            if (r == _hoverRow && c == _hoverCol) return;

            _hoverRow = hit ? r : -1;
            _hoverCol = hit ? c : -1;
            InvalidateVisual();

            EventHandler<string> h = HoverTextChanged;
            if (h != null) h(this, hit ? Describe(r, c) : string.Empty);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverRow < 0 && _hoverCol < 0) return;
            _hoverRow = -1; _hoverCol = -1;
            InvalidateVisual();
            EventHandler<string> h = HoverTextChanged;
            if (h != null) h(this, string.Empty);
        }

        private string Describe(int r, int c)
        {
            HeatRow row = _result.Rows[r];
            HeatCell cell = row.Cells[c];
            LogDataset refDs = _result.TimeReference;

            double t0 = _result.BucketStart(c);
            double t1 = t0 + _result.BucketSpan;
            string when = refDs != null
                ? refDs.FormatTime(t0) + " ~ " + refDs.FormatTime(t1)
                : t0.ToString("0.###") + " ~ " + t1.ToString("0.###");

            if (!cell.HasData) return row.Name + "   " + when + "   기록 없음";

            string bar = row.ByName
                ? "   기준 없음 (이름이 다르면 차이)"
                : "   허용 오차 " + NumberText.Plain(_tolerancePercent) + "%";

            if (cell.OverSamples == 0)
                return row.Name + "   " + when + "   정상"
                     + "   구간 최대 차이 " + WithUnit(cell.Max, row) + bar
                     + "   (표본 " + cell.Samples + "개 모두 허용 오차 안)";

            // 칸에 적히는 값과 허용 오차는 같은 잣대입니다. 그래서 둘을
            // 나란히 적어 두면 왜 빨간지 바로 읽힙니다.
            string peak = row.ByName
                ? "이름이 다름"
                : NumberText.Plain(row.PercentOf(cell)) + "%"
                  + " (" + WithUnit(row.ValueOf(cell), row) + ")";

            return row.Name + "   " + when + "   차이 발생"
                 + "   가장 크게 벌어진 순간 " + peak
                 + bar
                 + "   (허용 오차를 넘은 표본 " + cell.OverSamples + " / " + cell.Samples + ")"
                 + "   구간 최대 차이 " + WithUnit(cell.Max, row)
                 + "   구간 평균 차이 " + WithUnit(cell.Mean, row);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            int r, c;
            if (!HitCell(e.GetPosition(this), out r, out c)) return;

            HeatRow row = _result.Rows[r];
            double t0 = _result.BucketStart(c);
            EventHandler<CellEventArgs> h = CellActivated;
            if (h != null) h(this, new CellEventArgs(row.Name, t0, t0 + _result.BucketSpan));
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                // Ctrl + 휠 = 칸 너비 조절
                CellWidth = CellWidth * Math.Pow(1.15, notches);
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                double v = HorizontalOffset - notches * _cellW * 2;
                HorizontalOffset = Math.Max(0, Math.Min(HorizontalMaximum, v));
            }
            else
            {
                double v = VerticalOffset - notches * _rowH * 3;
                VerticalOffset = Math.Max(0, Math.Min(VerticalMaximum, v));
            }
            e.Handled = true;
        }
    }
}
