using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LogScope.Core.Model;
using LogScope.Core.Render;

namespace LogScope.App.Controls
{
    public enum ValueScaleMode { Raw, Normalized, Delta }

    /// <summary>
    /// 그래프를 직접 그리는 판.
    ///
    /// 성능의 핵심은 두 가지입니다.
    ///  - 표본이 픽셀보다 촘촘하면 픽셀 열마다 최소/최대만 뽑아 그립니다
    ///    (Decimator). 표본 5 만 개를 1000 픽셀에 그릴 때 선 긋기가
    ///    5 만 번에서 1000 번으로 줄어듭니다.
    ///  - 이중 버퍼로 그려서 시간축을 밀 때 깜빡이지 않습니다.
    ///
    /// 세로 눈금 글자는 실제로 선을 그릴 때 쓰는 것과 똑같은 변환으로
    /// 자리를 잡습니다. 그래서 눈금 숫자와 그래프 높이가 어긋날 수 없습니다.
    /// </summary>
    public sealed class GraphCanvas : Control
    {
        private readonly AppState _state;
        private readonly VScrollBar _vscroll = new VScrollBar();

        // 보이는 시간 구간
        private double _t0, _t1;
        private bool _timeInit;

        // 채널마다의 세로 상태. 겹쳐보기는 -1 번 자리를 함께 씁니다.
        private sealed class LaneY
        {
            public double Zoom = 1.0;
            public double Center = double.NaN;   // NaN 이면 기준 범위의 한가운데
        }
        private readonly Dictionary<int, LaneY> _lanes = new Dictionary<int, LaneY>();

        // 커서
        private double _cursorA = double.NaN;
        private double _cursorB = double.NaN;

        // 끌기
        private bool _dragging;
        private Point _dragPoint;
        private double _dragT0, _dragT1;
        private int _dragLane = -1;
        private double _dragCenter;
        private bool _moved;

        private Decimator.Column[] _colsBefore = new Decimator.Column[0];
        private Decimator.Column[] _colsAfter = new Decimator.Column[0];
        private PointF[] _pts = new PointF[0];

        public GraphCanvas(AppState state)
        {
            _state = state;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Current.Window;

            _vscroll.Dock = DockStyle.Right;
            _vscroll.SmallChange = 1;
            _vscroll.Visible = false;
            _vscroll.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_vscroll);
        }

        // ---------------- 바깥에서 쓰는 것 ----------------

        public bool LaneMode = true;
        public ValueScaleMode Scale = ValueScaleMode.Raw;
        public bool FitVisible;
        public bool ShadeDifference = true;
        public bool SeparateTraces;
        public double Tolerance;

        public event EventHandler CursorMoved;

        public double CursorA { get { return _cursorA; } }
        public double CursorB { get { return _cursorB; } }
        public double VisibleStart { get { return _t0; } }
        public double VisibleEnd { get { return _t1; } }

        public void ResetTime()
        {
            _state.FullTimeRange(out _t0, out _t1);
            _timeInit = true;
            Invalidate();
        }

        public void ResetValueZoom()
        {
            _lanes.Clear();
            Invalidate();
        }

        public void ZoomTime(double factor)
        {
            double mid = (_t0 + _t1) * 0.5;
            ZoomTimeAround(mid, factor);
        }

        public void ZoomValue(double factor)
        {
            foreach (KeyValuePair<int, LaneY> kv in _lanes) kv.Value.Zoom *= factor;
            if (_lanes.Count == 0) LaneFor(LaneMode ? FirstDrawn() : -1).Zoom *= factor;
            ClampZooms();
            Invalidate();
        }

        private int FirstDrawn()
        {
            List<int> order = _state.View.DrawOrder();
            return order.Count > 0 ? order[0] : -1;
        }

        private void ClampZooms()
        {
            foreach (KeyValuePair<int, LaneY> kv in _lanes)
            {
                if (kv.Value.Zoom < 1e-4) kv.Value.Zoom = 1e-4;
                if (kv.Value.Zoom > 1e7) kv.Value.Zoom = 1e7;
            }
        }

        private LaneY LaneFor(int io)
        {
            LaneY y;
            if (!_lanes.TryGetValue(io, out y)) { y = new LaneY(); _lanes[io] = y; }
            return y;
        }

        // ---------------- 자리 계산 ----------------

        private int GutterW { get { return Dpi.S(68); } }
        private int AxisH { get { return Dpi.S(26); } }
        private int HeaderH { get { return Dpi.S(22); } }
        private int MinLaneH { get { return Dpi.S(62); } }

        private Rectangle PlotArea
        {
            get
            {
                int right = _vscroll.Visible ? _vscroll.Width : 0;
                return new Rectangle(GutterW, 0,
                    Math.Max(10, ClientSize.Width - GutterW - right - Dpi.S(8)),
                    Math.Max(10, ClientSize.Height - AxisH));
            }
        }

        private double XToTime(int x)
        {
            Rectangle p = PlotArea;
            if (p.Width <= 0) return _t0;
            return _t0 + (x - p.Left) * (_t1 - _t0) / p.Width;
        }

        private float TimeToX(double t)
        {
            Rectangle p = PlotArea;
            double span = _t1 - _t0;
            if (span <= 0) return p.Left;
            return (float)(p.Left + (t - _t0) * p.Width / span);
        }

        // ---------------- 그리기 ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme th = Theme.Current;
            Graphics g = e.Graphics;
            g.Clear(th.Window);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (!_timeInit)
            {
                // 그리는 중이므로 Invalidate 는 부르지 않습니다.
                _state.FullTimeRange(out _t0, out _t1);
                _timeInit = true;
            }

            List<int> order = _state.View.DrawOrder();
            if (order.Count == 0)
            {
                DrawEmpty(g, th);
                return;
            }

            Rectangle plot = PlotArea;
            int lanes = LaneMode ? order.Count : 1;
            int laneH = LaneMode ? Math.Max(MinLaneH, plot.Height / Math.Max(1, lanes)) : plot.Height;

            int needed = LaneMode ? laneH * lanes : plot.Height;
            bool needScroll = needed > plot.Height;
            if (needScroll != _vscroll.Visible)
            {
                _vscroll.Visible = needScroll;
                plot = PlotArea;
                laneH = LaneMode ? Math.Max(MinLaneH, plot.Height / Math.Max(1, lanes)) : plot.Height;
                needed = LaneMode ? laneH * lanes : plot.Height;
            }
            if (needScroll)
            {
                _vscroll.Minimum = 0;
                _vscroll.Maximum = Math.Max(0, needed - 1);
                _vscroll.LargeChange = Math.Max(1, plot.Height);
                if (_vscroll.Value > _vscroll.Maximum - _vscroll.LargeChange + 1)
                    _vscroll.Value = Math.Max(0, _vscroll.Maximum - _vscroll.LargeChange + 1);
            }
            int scrollY = needScroll ? _vscroll.Value : 0;

            EnsureBuffers(plot.Width);

            Region old = g.Clip;
            g.SetClip(new Rectangle(0, 0, ClientSize.Width, plot.Bottom));

            if (LaneMode)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    int top = i * laneH - scrollY;
                    if (top + laneH < 0 || top > plot.Height) continue;
                    var rect = new Rectangle(plot.Left, top, plot.Width, laneH);
                    DrawLane(g, th, rect, new[] { order[i] }, order[i]);
                }
            }
            else
            {
                var rect = new Rectangle(plot.Left, 0, plot.Width, plot.Height);
                DrawLane(g, th, rect, order.ToArray(), -1);
            }

            g.Clip = old;

            DrawTimeAxis(g, th, plot);
            DrawCursors(g, th, plot);
        }

        private void DrawEmpty(Graphics g, Theme th)
        {
            string msg = _state.HasAny
                ? "왼쪽 목록에서 IO 를 골라 주세요.\n처음에는 아무것도 선택되어 있지 않습니다."
                : "위쪽 메뉴의 '이전 로그 열기' / '이후 로그 열기' 로 파일을 열어 주세요.";
            TextRenderer.DrawText(g, msg, Theme.Ui,
                new Rectangle(Dpi.S(24), Dpi.S(24), ClientSize.Width - Dpi.S(48), Dpi.S(120)),
                th.Muted, TextFormatFlags.WordBreak);
        }

        private void EnsureBuffers(int width)
        {
            if (_colsBefore.Length < width)
            {
                _colsBefore = new Decimator.Column[width];
                _colsAfter = new Decimator.Column[width];
                _pts = new PointF[width * 4 + 8];
            }
        }

        /// <summary>한 레인(또는 겹쳐보기 전체)을 그립니다.</summary>
        private void DrawLane(Graphics g, Theme th, Rectangle rect, int[] ios, int laneKey)
        {
            using (var back = new SolidBrush(th.Plot)) g.FillRectangle(back, rect);
            using (var p = new Pen(th.GridStrong)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

            Rectangle inner = new Rectangle(rect.X, rect.Y + HeaderH, rect.Width, Math.Max(8, rect.Height - HeaderH - Dpi.S(4)));

            double lo, hi;
            BaseRange(ios, out lo, out hi);
            LaneY ly = LaneFor(laneKey);
            double span = (hi - lo) / ly.Zoom;
            if (!(span > 0)) span = 1;
            double center = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            DrawValueAxis(g, th, inner, vlo, vhi);

            for (int k = 0; k < ios.Length; k++)
            {
                int io = ios[k];
                IoRef r = _state.View.All[io];
                Color cBefore = LaneMode ? th.Before : th.SeriesColor(k);
                Color cAfter = LaneMode ? th.After : ControlPaint.Dark(th.SeriesColor(k), 0.25f);
                DrawChannel(g, th, inner, io, r, vlo, vhi, cBefore, cAfter);
            }

            DrawLaneHeader(g, th, rect, ios);
        }

        private void DrawLaneHeader(Graphics g, Theme th, Rectangle rect, int[] ios)
        {
            var head = new Rectangle(rect.X + Dpi.S(6), rect.Y + Dpi.S(3), rect.Width - Dpi.S(12), HeaderH - Dpi.S(4));
            if (ios.Length == 1)
            {
                IoRef r = _state.View.All[ios[0]];
                string extra = string.Empty;
                if (!r.InAfter && _state.After != null) extra = "  (이후 로그에 없음)";
                else if (!r.InBefore && _state.Before != null) extra = "  (이전 로그에 없음)";
                TextRenderer.DrawText(g, r.Name + extra, Theme.UiBold, head, th.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                string vals = ValueReadout(ios[0]);
                if (!string.IsNullOrEmpty(vals))
                    TextRenderer.DrawText(g, vals, Theme.Small, head, th.Muted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            }
            else
            {
                int x = head.X;
                for (int k = 0; k < ios.Length && k < 24; k++)
                {
                    IoRef r = _state.View.All[ios[k]];
                    Color c = th.SeriesColor(k);
                    using (var b = new SolidBrush(c))
                        g.FillRectangle(b, x, head.Y + head.Height / 2 - Dpi.S(4), Dpi.S(10), Dpi.S(8));
                    x += Dpi.S(13);
                    Size sz = TextRenderer.MeasureText(g, r.Name, Theme.Small);
                    TextRenderer.DrawText(g, r.Name, Theme.Small,
                        new Rectangle(x, head.Y, Math.Min(sz.Width + Dpi.S(2), head.Right - x), head.Height),
                        th.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    x += sz.Width + Dpi.S(12);
                    if (x > head.Right - Dpi.S(40)) break;
                }
            }
        }

        private string ValueReadout(int io)
        {
            if (double.IsNaN(_cursorA)) return string.Empty;
            IoRef r = _state.View.All[io];
            string s = string.Empty;
            if (r.InBefore && _state.Before != null)
                s += "이전 " + _state.Before.Channels[r.BeforeIndex]
                        .FormatValue(_state.Before.SampleAt(r.BeforeIndex, _cursorA));
            if (r.InAfter && _state.After != null)
            {
                if (s.Length > 0) s += "   ";
                s += "이후 " + _state.After.Channels[r.AfterIndex]
                        .FormatValue(_state.After.SampleAt(r.AfterIndex, _cursorA - _state.AppliedShift));
            }
            return s;
        }

        /// <summary>
        /// 레인의 기준 세로 범위. 기본은 채널 전체 범위라서 시간축을 밀어도
        /// 세로 배율이 갑자기 풀리지 않습니다. "보이는 구간에 맞춤" 을 켰을
        /// 때만 지금 보이는 구간으로 다시 잡습니다.
        /// </summary>
        private void BaseRange(int[] ios, out double lo, out double hi)
        {
            if (Scale == ValueScaleMode.Normalized) { lo = -0.05; hi = 1.05; return; }

            double a = double.PositiveInfinity, b = double.NegativeInfinity;
            for (int k = 0; k < ios.Length; k++)
            {
                IoRef r = _state.View.All[ios[k]];
                double clo, chi;
                if (!ChannelRange(r, out clo, out chi)) continue;
                if (Scale == ValueScaleMode.Delta)
                {
                    double bas = Baseline(r);
                    clo -= bas; chi -= bas;
                }
                if (clo < a) a = clo;
                if (chi > b) b = chi;
            }
            if (a > b) { a = 0; b = 1; }
            if (b - a < 1e-12) { double m = (a + b) * 0.5; a = m - 0.5; b = m + 0.5; }

            double pad = (b - a) * 0.08;
            lo = a - pad; hi = b + pad;
        }

        private bool ChannelRange(IoRef r, out double lo, out double hi)
        {
            lo = double.PositiveInfinity; hi = double.NegativeInfinity;
            bool any = false;

            if (FitVisible)
            {
                double x0, x1;
                if (r.InBefore && _state.Before != null
                    && Decimator.RangeIn(_state.Before, r.BeforeIndex, _t0, _t1, out x0, out x1))
                { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                if (r.InAfter && _state.After != null
                    && Decimator.RangeIn(_state.After, r.AfterIndex,
                        _t0 - _state.AppliedShift, _t1 - _state.AppliedShift, out x0, out x1))
                { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                if (any) return true;
            }

            if (r.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[r.BeforeIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            if (r.InAfter && _state.After != null)
            {
                Channel c = _state.After.Channels[r.AfterIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            return any;
        }

        /// <summary>"변화만" 모드에서 빼 줄 기준값. 채널의 첫 유효 값입니다.</summary>
        private double Baseline(IoRef r)
        {
            if (r.InBefore && _state.Before != null)
            {
                float[] v = _state.Before.Channels[r.BeforeIndex].Values;
                for (int i = 0; i < v.Length; i++) if (!float.IsNaN(v[i])) return v[i];
            }
            if (r.InAfter && _state.After != null)
            {
                float[] v = _state.After.Channels[r.AfterIndex].Values;
                for (int i = 0; i < v.Length; i++) if (!float.IsNaN(v[i])) return v[i];
            }
            return 0;
        }

        private double Transform(IoRef r, double v, double chLo, double chHi, double baseline)
        {
            if (double.IsNaN(v)) return double.NaN;
            switch (Scale)
            {
                case ValueScaleMode.Normalized:
                    return (chHi - chLo) > 1e-12 ? (v - chLo) / (chHi - chLo) : 0.5;
                case ValueScaleMode.Delta:
                    return v - baseline;
                default:
                    return v;
            }
        }

        private void DrawChannel(Graphics g, Theme th, Rectangle inner, int io, IoRef r,
                                 double vlo, double vhi, Color cBefore, Color cAfter)
        {
            double chLo, chHi;
            if (!ChannelRange(r, out chLo, out chHi)) { chLo = 0; chHi = 1; }
            double baseline = Scale == ValueScaleMode.Delta ? Baseline(r) : 0;

            int width = inner.Width;
            if (width <= 1) return;

            bool haveB = r.InBefore && _state.Before != null;
            bool haveA = r.InAfter && _state.After != null;

            if (haveB) Decimator.Build(_state.Before, r.BeforeIndex, _t0, _t1, _colsBefore, width);
            if (haveA) Decimator.Build(_state.After, r.AfterIndex,
                                       _t0 - _state.AppliedShift, _t1 - _state.AppliedShift, _colsAfter, width);

            float sep = SeparateTraces ? inner.Height * 0.02f : 0f;

            if (ShadeDifference && haveB && haveA)
                ShadeGap(g, th, inner, width, vlo, vhi, r, chLo, chHi, baseline, sep);

            if (haveB) DrawTrace(g, inner, _colsBefore, width, vlo, vhi, r, chLo, chHi, baseline, cBefore, -sep);
            if (haveA) DrawTrace(g, inner, _colsAfter, width, vlo, vhi, r, chLo, chHi, baseline, cAfter, +sep);
        }

        private float ValueToY(double v, Rectangle inner, double vlo, double vhi)
        {
            double span = vhi - vlo;
            if (!(span > 0)) span = 1;
            double f = (v - vlo) / span;
            return (float)(inner.Bottom - f * inner.Height);
        }

        private void DrawTrace(Graphics g, Rectangle inner, Decimator.Column[] cols, int width,
                               double vlo, double vhi, IoRef r, double chLo, double chHi,
                               double baseline, Color color, float dy)
        {
            int n = 0;
            float top = inner.Top - 4, bottom = inner.Bottom + 4;

            using (var pen = new Pen(color, Math.Max(1f, Dpi.F(1.2f))))
            {
                pen.LineJoin = LineJoin.Bevel;
                for (int x = 0; x < width; x++)
                {
                    Decimator.Column c = cols[x];
                    if (!c.HasValue)
                    {
                        // 값이 끊긴 자리. 여기까지 그린 뒤 선을 끊습니다.
                        if (n > 1) g.DrawLines(pen, Slice(n));
                        n = 0;
                        continue;
                    }

                    float px = inner.Left + x;
                    // 화면 y 는 값이 클수록 작아집니다. yTop 이 c.Max, yBottom 이 c.Min.
                    float yFirst = ValueToY(Transform(r, c.First, chLo, chHi, baseline), inner, vlo, vhi) + dy;
                    float yTop = ValueToY(Transform(r, c.Max, chLo, chHi, baseline), inner, vlo, vhi) + dy;
                    float yBottom = ValueToY(Transform(r, c.Min, chLo, chHi, baseline), inner, vlo, vhi) + dy;
                    float yLast = ValueToY(Transform(r, c.Last, chLo, chHi, baseline), inner, vlo, vhi) + dy;

                    Add(ref n, px, Clamp(yFirst, top, bottom));
                    if (yTop != yFirst) Add(ref n, px, Clamp(yTop, top, bottom));
                    if (yBottom != yTop) Add(ref n, px, Clamp(yBottom, top, bottom));
                    if (yLast != yBottom) Add(ref n, px, Clamp(yLast, top, bottom));

                    if (n >= _pts.Length - 8) { g.DrawLines(pen, Slice(n)); PointF keep = _pts[n - 1]; n = 0; Add(ref n, keep.X, keep.Y); }
                }
                if (n > 1) g.DrawLines(pen, Slice(n));
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (float.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        private void Add(ref int n, float x, float y)
        {
            if (n >= _pts.Length) return;
            _pts[n].X = x; _pts[n].Y = y; n++;
        }

        // DrawLines 는 길이가 딱 맞는 배열을 요구합니다. 매 프레임 새로
        // 잡으면 쓰레기가 쌓이므로, 길이가 같은 동안은 같은 배열을 다시 씁니다.
        // DrawLines 는 배열을 붙들고 있지 않으므로 안전합니다.
        private PointF[] _slice = new PointF[0];
        private PointF[] Slice(int n)
        {
            if (_slice.Length != n) _slice = new PointF[n];
            Array.Copy(_pts, _slice, n);
            return _slice;
        }

        /// <summary>
        /// 이전과 이후가 벌어진 구간을 옅은 색으로 채웁니다. 점선을 쓰지
        /// 않는 이유는, 점선이 파형의 빈 구간과 섞여 파형을 읽기 어렵게
        /// 만들기 때문입니다.
        /// </summary>
        private void ShadeGap(Graphics g, Theme th, Rectangle inner, int width,
                              double vlo, double vhi, IoRef r, double chLo, double chHi,
                              double baseline, float sep)
        {
            using (var b = new SolidBrush(th.DiffShade))
            {
                for (int x = 0; x < width; x++)
                {
                    Decimator.Column cb = _colsBefore[x], ca = _colsAfter[x];
                    if (!cb.HasValue || !ca.HasValue) continue;
                    if (Math.Abs(cb.Last - ca.Last) <= Tolerance) continue;

                    float y1 = ValueToY(Transform(r, cb.Last, chLo, chHi, baseline), inner, vlo, vhi) - sep;
                    float y2 = ValueToY(Transform(r, ca.Last, chLo, chHi, baseline), inner, vlo, vhi) + sep;
                    float top = Clamp(Math.Min(y1, y2), inner.Top, inner.Bottom);
                    float bot = Clamp(Math.Max(y1, y2), inner.Top, inner.Bottom);
                    if (bot - top < 1f) bot = top + 1f;
                    g.FillRectangle(b, inner.Left + x, top, 1f, bot - top);
                }
            }
        }

        // ---------------- 눈금 ----------------

        private void DrawValueAxis(Graphics g, Theme th, Rectangle inner, double vlo, double vhi)
        {
            double step = NiceStep(vhi - vlo, Math.Max(2, inner.Height / Dpi.S(34)));
            if (!(step > 0)) return;

            double first = Math.Ceiling(vlo / step) * step;
            using (var grid = new Pen(th.Grid))
            {
                for (double v = first; v <= vhi + step * 0.001; v += step)
                {
                    float y = ValueToY(v, inner, vlo, vhi);
                    if (y < inner.Top - 1 || y > inner.Bottom + 1) continue;
                    g.DrawLine(grid, inner.Left + 1, y, inner.Right - 2, y);
                    // 눈금 글자는 선을 그은 바로 그 y 에 붙입니다. 그래서
                    // 숫자와 그래프 높이가 어긋날 수 없습니다.
                    TextRenderer.DrawText(g, FormatTick(v, step), Theme.Small,
                        new Rectangle(0, (int)y - Dpi.S(8), GutterW - Dpi.S(6), Dpi.S(16)),
                        th.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        private static string FormatTick(double v, double step)
        {
            if (Math.Abs(v) < step * 1e-9) v = 0;
            double a = Math.Abs(v);
            if (a != 0 && (a >= 1e6 || a < 1e-3)) return v.ToString("G4");
            int dec = 0;
            double s = Math.Abs(step);
            while (s < 1 && dec < 6) { s *= 10; dec++; }
            return v.ToString("N" + dec);
        }

        private static double NiceStep(double span, int wanted)
        {
            if (!(span > 0) || wanted < 1) return 0;
            double raw = span / wanted;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm <= 1 ? 1 : (norm <= 2 ? 2 : (norm <= 5 ? 5 : 10));
            return nice * mag;
        }

        private void DrawTimeAxis(Graphics g, Theme th, Rectangle plot)
        {
            var strip = new Rectangle(0, plot.Bottom, ClientSize.Width, AxisH);
            using (var b = new SolidBrush(th.Panel)) g.FillRectangle(b, strip);
            using (var p = new Pen(th.Border)) g.DrawLine(p, 0, strip.Top, strip.Right, strip.Top);

            LogDataset refDs = _state.TimeReference;
            double span = _t1 - _t0;
            if (!(span > 0)) return;

            int wanted = Math.Max(2, plot.Width / Dpi.S(110));
            double step = NiceStep(span, wanted);
            if (!(step > 0)) return;

            double first = Math.Ceiling(_t0 / step) * step;
            using (var grid = new Pen(th.Grid))
            using (var tick = new Pen(th.Border))
            {
                for (double t = first; t <= _t1; t += step)
                {
                    float x = TimeToX(t);
                    if (x < plot.Left || x > plot.Right) continue;
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                    g.DrawLine(tick, x, strip.Top, x, strip.Top + Dpi.S(4));
                    string label = refDs != null ? refDs.FormatTime(t) : t.ToString("0.###");
                    TextRenderer.DrawText(g, label, Theme.Small,
                        new Rectangle((int)x - Dpi.S(55), strip.Top + Dpi.S(5), Dpi.S(110), Dpi.S(16)),
                        th.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawCursors(Graphics g, Theme th, Rectangle plot)
        {
            DrawCursor(g, th, plot, _cursorA, th.CursorA, "A");
            DrawCursor(g, th, plot, _cursorB, th.CursorB, "B");
        }

        private void DrawCursor(Graphics g, Theme th, Rectangle plot, double t, Color c, string tag)
        {
            if (double.IsNaN(t) || t < _t0 || t > _t1) return;
            float x = TimeToX(t);
            using (var p = new Pen(c, Math.Max(1f, Dpi.F(1.2f))))
                g.DrawLine(p, x, plot.Top, x, plot.Bottom);
            var box = new Rectangle((int)x - Dpi.S(8), plot.Top + Dpi.S(1), Dpi.S(16), Dpi.S(14));
            using (var b = new SolidBrush(c)) g.FillRectangle(b, box);
            TextRenderer.DrawText(g, tag, Theme.SmallBold, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // ---------------- 마우스 ----------------

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);

            // 한 칸에 너무 많이 커지지 않게 아주 조금씩 바꿉니다.
            double factor = Math.Pow(1.14, notches);

            if ((ModifierKeys & Keys.Shift) != 0) ZoomValueAt(e.Location, factor);
            else ZoomTimeAround(XToTime(e.X), factor);
            Invalidate();
        }

        private void ZoomTimeAround(double anchor, double factor)
        {
            double span = (_t1 - _t0) / factor;
            double full0, full1;
            _state.FullTimeRange(out full0, out full1);
            double minSpan = (full1 - full0) * 1e-7;
            if (minSpan <= 0) minSpan = 1e-9;
            if (span < minSpan) span = minSpan;
            double maxSpan = (full1 - full0) * 20.0;
            if (span > maxSpan) span = maxSpan;

            double f = (_t1 - _t0) > 0 ? (anchor - _t0) / (_t1 - _t0) : 0.5;
            _t0 = anchor - f * span;
            _t1 = _t0 + span;
        }

        /// <summary>
        /// Shift + 휠. 커서가 가리키던 값이 그 자리에 그대로 남도록
        /// 가운데를 다시 잡습니다.
        /// </summary>
        private void ZoomValueAt(Point at, double factor)
        {
            int laneKey; Rectangle inner;
            if (!LaneAt(at, out laneKey, out inner)) return;

            int[] ios = IosForLane(laneKey);
            double lo, hi;
            BaseRange(ios, out lo, out hi);

            LaneY ly = LaneFor(laneKey);
            double span = (hi - lo) / ly.Zoom;
            double center = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;

            double f = inner.Height > 0 ? (double)(inner.Bottom - at.Y) / inner.Height : 0.5;
            double valueAt = center - span * 0.5 + f * span;

            ly.Zoom *= factor;
            ClampZooms();
            double newSpan = (hi - lo) / ly.Zoom;
            ly.Center = valueAt - (f - 0.5) * newSpan;
        }

        private int[] IosForLane(int laneKey)
        {
            if (laneKey >= 0) return new[] { laneKey };
            return _state.View.DrawOrder().ToArray();
        }

        private bool LaneAt(Point at, out int laneKey, out Rectangle inner)
        {
            laneKey = -1;
            inner = Rectangle.Empty;
            Rectangle plot = PlotArea;
            if (!plot.Contains(at)) return false;

            List<int> order = _state.View.DrawOrder();
            if (order.Count == 0) return false;

            if (!LaneMode)
            {
                inner = new Rectangle(plot.Left, plot.Top + HeaderH, plot.Width,
                                      Math.Max(8, plot.Height - HeaderH - Dpi.S(4)));
                laneKey = -1;
                return true;
            }

            int laneH = Math.Max(MinLaneH, plot.Height / Math.Max(1, order.Count));
            int scrollY = _vscroll.Visible ? _vscroll.Value : 0;
            int idx = (at.Y + scrollY) / laneH;
            if (idx < 0 || idx >= order.Count) return false;

            int top = idx * laneH - scrollY;
            laneKey = order[idx];
            inner = new Rectangle(plot.Left, top + HeaderH, plot.Width, Math.Max(8, laneH - HeaderH - Dpi.S(4)));
            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;

            _dragging = true;
            _moved = false;
            _dragPoint = e.Location;
            _dragT0 = _t0; _dragT1 = _t1;

            Rectangle inner;
            if (LaneAt(e.Location, out _dragLane, out inner))
            {
                LaneY ly = LaneFor(_dragLane);
                double lo, hi;
                BaseRange(IosForLane(_dragLane), out lo, out hi);
                _dragCenter = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;
            }
            else _dragLane = int.MinValue;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            int dx = e.X - _dragPoint.X, dy = e.Y - _dragPoint.Y;
            if (!_moved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
            _moved = true;

            Rectangle plot = PlotArea;
            if (plot.Width > 0)
            {
                double span = _dragT1 - _dragT0;
                double shift = -dx * span / plot.Width;
                _t0 = _dragT0 + shift;
                _t1 = _dragT1 + shift;
            }

            if (_dragLane != int.MinValue)
            {
                int laneKey; Rectangle inner;
                if (LaneAt(_dragPoint, out laneKey, out inner) && inner.Height > 0)
                {
                    double lo, hi;
                    BaseRange(IosForLane(laneKey), out lo, out hi);
                    LaneY ly = LaneFor(laneKey);
                    double vspan = (hi - lo) / ly.Zoom;
                    ly.Center = _dragCenter + dy * vspan / inner.Height;
                }
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging) { return; }
            _dragging = false;
            if (_moved) return;

            // 끌지 않고 그냥 눌렀으면 커서를 놓습니다.
            double t = XToTime(e.X);
            if ((ModifierKeys & Keys.Shift) != 0) _cursorB = t; else _cursorA = t;
            var h = CursorMoved; if (h != null) h(this, EventArgs.Empty);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.Home: case Keys.End: return true;
                default: return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double span = _t1 - _t0;
            switch (e.KeyCode)
            {
                case Keys.Left: _t0 -= span * 0.1; _t1 -= span * 0.1; Invalidate(); break;
                case Keys.Right: _t0 += span * 0.1; _t1 += span * 0.1; Invalidate(); break;
                case Keys.Home: ResetTime(); break;
                case Keys.Add: case Keys.Oemplus: ZoomTime(1.3); Invalidate(); break;
                case Keys.Subtract: case Keys.OemMinus: ZoomTime(1 / 1.3); Invalidate(); break;
            }
        }
    }
}
