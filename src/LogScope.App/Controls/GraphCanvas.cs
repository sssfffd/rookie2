using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.App.Services;
using LogScope.App.Themes;
using LogScope.App.ViewModels;
using LogScope.Core.Model;
using LogScope.Core.Render;

namespace LogScope.App.Controls
{
    public enum ValueScaleMode { Raw, Normalized, Delta }

    /// <summary>
    /// 그래프를 직접 그리는 판.
    ///
    /// WPF 컨트롤을 겹쳐 쌓지 않고 FrameworkElement 하나에 OnRender 로 그립니다.
    /// 채널 200 개에 표본 5 만 개면 컨트롤로는 감당이 안 되기 때문입니다.
    ///
    /// 빠른 이유 두 가지:
    ///  - 표본이 픽셀보다 촘촘하면 픽셀 열마다 최소/최대만 뽑아 그립니다
    ///    (Decimator). 표본 5 만 개를 1000 픽셀에 그릴 때 선 긋기가
    ///    5 만 번에서 1000 번으로 줄어듭니다.
    ///  - 선 하나를 StreamGeometry 한 덩어리로 만들어 한 번에 넘깁니다.
    ///    점마다 DrawLine 을 부르지 않습니다.
    ///
    /// 세로 눈금 글자는 선을 그을 때 쓰는 것과 똑같은 ValueToY() 를 거칩니다.
    /// 그래서 눈금 숫자와 그래프 높이가 어긋날 수 없습니다.
    /// </summary>
    public sealed class GraphCanvas : FrameworkElement
    {
        private AppState _state;
        private List<IoRowVm> _channels = new List<IoRowVm>();

        // 보이는 시간 구간
        private double _t0, _t1;
        private bool _timeInit;

        private sealed class LaneY
        {
            public double Zoom = 1.0;
            public double Center = double.NaN;   // NaN 이면 기준 범위의 한가운데
        }

        // 레인마다의 세로 상태를 채널 이름으로 기억합니다. 번호로 기억하면
        // 그룹 순서를 바꿨을 때 엉뚱한 레인에 배율이 붙습니다.
        private readonly Dictionary<string, LaneY> _lanes = new Dictionary<string, LaneY>(StringComparer.Ordinal);

        // 겹쳐보기는 레인이 하나뿐이라 이름 대신 이 자리를 씁니다.
        // IO 이름과 겹치지 않도록 일부러 이상한 글자를 씁니다.
        private const string OverlayLaneKey = "<< 겹쳐보기 >>";

        private double _cursorA = double.NaN;
        private double _cursorB = double.NaN;

        private bool _dragging, _moved;
        private Point _dragPoint;
        private double _dragT0, _dragT1, _dragCenter;
        private string _dragLane;

        private Decimator.Column[] _colsBefore = new Decimator.Column[0];
        private Decimator.Column[] _colsAfter = new Decimator.Column[0];
        private readonly List<Point> _pts = new List<Point>(4096);

        private readonly Typeface _face = new Typeface("Segoe UI");
        private const double FontNormal = 12.0;
        private const double FontSmall = 11.0;

        public GraphCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            // 1 픽셀 선이 흐려지지 않게 합니다. 파형은 또렷해야 읽힙니다.
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            ThemeManager.Changed += delegate { InvalidateVisual(); };
        }

        // ---------------- 바깥에서 설정하는 것 ----------------

        public void Attach(AppState state)
        {
            _state = state;
            _timeInit = false;
            InvalidateVisual();
        }

        /// <summary>그릴 채널들. 왼쪽 목록에 보이는 차례 그대로 들어옵니다.</summary>
        public void SetChannels(List<IoRowVm> channels)
        {
            _channels = channels ?? new List<IoRowVm>();
            InvalidateVisual();
        }

        private bool _laneMode = true;
        public bool LaneMode
        {
            get { return _laneMode; }
            set { if (_laneMode != value) { _laneMode = value; ResetValueZoom(); } }
        }

        private ValueScaleMode _scale = ValueScaleMode.Raw;
        public ValueScaleMode Scale
        {
            get { return _scale; }
            set { if (_scale != value) { _scale = value; ResetValueZoom(); } }
        }

        private bool _fitVisible;
        public bool FitVisible
        {
            get { return _fitVisible; }
            set { if (_fitVisible != value) { _fitVisible = value; InvalidateVisual(); } }
        }

        private bool _shade = true;
        public bool ShadeDifference
        {
            get { return _shade; }
            set { if (_shade != value) { _shade = value; InvalidateVisual(); } }
        }

        private bool _separate;
        public bool SeparateTraces
        {
            get { return _separate; }
            set { if (_separate != value) { _separate = value; InvalidateVisual(); } }
        }

        private double _tolerance;
        public double Tolerance
        {
            get { return _tolerance; }
            set { if (_tolerance != value) { _tolerance = value; InvalidateVisual(); } }
        }

        /// <summary>커서나 보이는 구간이 바뀌면 알립니다. 아래 띠의 글자를 고쳐 씁니다.</summary>
        public event EventHandler ViewChanged;

        private void RaiseViewChanged()
        {
            EventHandler h = ViewChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        public double CursorA { get { return _cursorA; } }
        public double CursorB { get { return _cursorB; } }
        public double VisibleStart { get { return _t0; } }
        public double VisibleEnd { get { return _t1; } }

        // ---------------- 세로 스크롤 (레인이 많을 때) ----------------

        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register("VerticalOffset", typeof(double), typeof(GraphCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double VerticalOffset
        {
            get { return (double)GetValue(VerticalOffsetProperty); }
            set { SetValue(VerticalOffsetProperty, value); }
        }

        public static readonly DependencyProperty ScrollMaximumProperty =
            DependencyProperty.Register("ScrollMaximum", typeof(double), typeof(GraphCanvas),
                new PropertyMetadata(0.0));

        public double ScrollMaximum
        {
            get { return (double)GetValue(ScrollMaximumProperty); }
            private set { SetValue(ScrollMaximumProperty, value); }
        }

        public static readonly DependencyProperty ScrollViewportProperty =
            DependencyProperty.Register("ScrollViewport", typeof(double), typeof(GraphCanvas),
                new PropertyMetadata(1.0));

        public double ScrollViewport
        {
            get { return (double)GetValue(ScrollViewportProperty); }
            private set { SetValue(ScrollViewportProperty, value); }
        }

        // ---------------- 확대 / 이동 ----------------

        public void ResetTime()
        {
            if (_state == null) { _t0 = 0; _t1 = 1; }
            else _state.FullTimeRange(out _t0, out _t1);
            _timeInit = true;
            InvalidateVisual();
            RaiseViewChanged();
        }

        public void ResetValueZoom()
        {
            _lanes.Clear();
            InvalidateVisual();
        }

        /// <summary>보이는 시간 구간을 그대로 정합니다. 히트맵에서 칸을 눌러
        /// 넘어올 때 그 구간으로 맞추는 데 씁니다.</summary>
        public void SetTimeRange(double t0, double t1)
        {
            if (!(t1 > t0)) return;
            _t0 = t0;
            _t1 = t1;
            _timeInit = true;
            InvalidateVisual();
            RaiseViewChanged();
        }

        public void ZoomTime(double factor)
        {
            ZoomTimeAround((_t0 + _t1) * 0.5, factor);
            InvalidateVisual();
            RaiseViewChanged();
        }

        public void ZoomValue(double factor)
        {
            if (_lanes.Count == 0)
            {
                // 아직 손대지 않았으면 지금 보이는 레인들을 먼저 만들어 둡니다.
                if (_laneMode) { foreach (IoRowVm vm in _channels) LaneFor(vm.Name); }
                else LaneFor(OverlayLaneKey);
            }
            foreach (KeyValuePair<string, LaneY> kv in _lanes) kv.Value.Zoom *= factor;
            ClampZooms();
            InvalidateVisual();
        }

        private void ClampZooms()
        {
            foreach (KeyValuePair<string, LaneY> kv in _lanes)
            {
                if (kv.Value.Zoom < 1e-4) kv.Value.Zoom = 1e-4;
                if (kv.Value.Zoom > 1e7) kv.Value.Zoom = 1e7;
            }
        }

        private LaneY LaneFor(string key)
        {
            LaneY y;
            if (!_lanes.TryGetValue(key, out y)) { y = new LaneY(); _lanes[key] = y; }
            return y;
        }

        private void ZoomTimeAround(double anchor, double factor)
        {
            double span = (_t1 - _t0) / factor;
            double full0, full1;
            if (_state != null) _state.FullTimeRange(out full0, out full1);
            else { full0 = 0; full1 = 1; }

            double minSpan = (full1 - full0) * 1e-7;
            if (minSpan <= 0) minSpan = 1e-9;
            if (span < minSpan) span = minSpan;
            double maxSpan = (full1 - full0) * 20.0;
            if (span > maxSpan) span = maxSpan;

            double f = (_t1 - _t0) > 0 ? (anchor - _t0) / (_t1 - _t0) : 0.5;
            _t0 = anchor - f * span;
            _t1 = _t0 + span;
        }

        // ---------------- 자리 ----------------

        private const double GutterW = 66;
        private const double AxisH = 26;
        private const double HeaderH = 21;
        private const double MinLaneH = 64;

        private Rect PlotArea
        {
            get
            {
                double w = Math.Max(10, ActualWidth - GutterW - 6);
                double h = Math.Max(10, ActualHeight - AxisH);
                return new Rect(GutterW, 0, w, h);
            }
        }

        private double XToTime(double x)
        {
            Rect p = PlotArea;
            if (p.Width <= 0) return _t0;
            return _t0 + (x - p.Left) * (_t1 - _t0) / p.Width;
        }

        private double TimeToX(double t)
        {
            Rect p = PlotArea;
            double span = _t1 - _t0;
            if (span <= 0) return p.Left;
            return p.Left + (t - _t0) * p.Width / span;
        }

        private static double ValueToY(double v, Rect inner, double vlo, double vhi)
        {
            double span = vhi - vlo;
            if (!(span > 0)) span = 1;
            return inner.Bottom - (v - vlo) / span * inner.Height;
        }

        // ---------------- 그리기 ----------------

        protected override void OnRender(DrawingContext dc)
        {
            Palette p = ThemeManager.Palette;

            // 바탕을 칠해 두어야 이 요소가 마우스를 받습니다.
            dc.DrawRectangle(Frozen(p.Window), null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (!_timeInit && _state != null)
            {
                _state.FullTimeRange(out _t0, out _t1);
                _timeInit = true;
            }

            if (_channels.Count == 0)
            {
                DrawEmpty(dc, p);
                UpdateScrollInfo(0, 1);
                return;
            }

            Rect plot = PlotArea;
            int lanes = _laneMode ? _channels.Count : 1;
            double laneH = _laneMode ? Math.Max(MinLaneH, plot.Height / Math.Max(1, lanes)) : plot.Height;
            double needed = _laneMode ? laneH * lanes : plot.Height;

            UpdateScrollInfo(needed, plot.Height);
            double scrollY = needed > plot.Height ? VerticalOffset : 0;

            int columns = (int)Math.Max(1, Math.Floor(plot.Width));
            EnsureBuffers(columns);

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, plot.Bottom)));
            if (_laneMode)
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    double top = i * laneH - scrollY;
                    if (top + laneH < 0 || top > plot.Height) continue;
                    var rect = new Rect(plot.Left, top, plot.Width, laneH);
                    DrawLane(dc, p, rect, new[] { _channels[i] }, _channels[i].Name, columns);
                }
            }
            else
            {
                var rect = new Rect(plot.Left, 0, plot.Width, plot.Height);
                DrawLane(dc, p, rect, _channels.ToArray(), OverlayLaneKey, columns);
            }
            dc.Pop();

            DrawTimeAxis(dc, p, plot);
            DrawCursor(dc, p, plot, _cursorA, p.CursorAPen, p.CursorA, "A");
            DrawCursor(dc, p, plot, _cursorB, p.CursorBPen, p.CursorB, "B");
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private void UpdateScrollInfo(double extent, double viewport)
        {
            double max = Math.Max(0, extent - viewport);
            if (Math.Abs(ScrollMaximum - max) > 0.5) ScrollMaximum = max;
            if (Math.Abs(ScrollViewport - viewport) > 0.5) ScrollViewport = Math.Max(1, viewport);
            if (VerticalOffset > max) VerticalOffset = max;
        }

        private void DrawEmpty(DrawingContext dc, Palette p)
        {
            string msg = (_state != null && _state.HasAny)
                ? "왼쪽 목록에서 IO 를 골라 주세요.\n처음에는 아무것도 선택되어 있지 않습니다."
                : "위쪽 메뉴의 '이전 로그 열기' / '이후 로그 열기' 로 파일을 열어 주세요.";
            FormattedText ft = Text(msg, FontNormal, p.MutedBrush);
            ft.MaxTextWidth = Math.Max(100, ActualWidth - 48);
            dc.DrawText(ft, new Point(24, 24));
        }

        private void EnsureBuffers(int columns)
        {
            if (_colsBefore.Length < columns)
            {
                _colsBefore = new Decimator.Column[columns];
                _colsAfter = new Decimator.Column[columns];
            }
        }

        private FormattedText Text(string s, double size, Brush brush)
        {
            return new FormattedText(s ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, _face, size, brush);
        }

        /// <summary>레인 하나(겹쳐보기면 전체)를 그립니다.</summary>
        private void DrawLane(DrawingContext dc, Palette p, Rect rect, IoRowVm[] ios, string laneKey, int columns)
        {
            dc.DrawRectangle(p.PlotBrush, p.BorderPen, rect);

            var inner = new Rect(rect.X, rect.Y + HeaderH, rect.Width,
                                 Math.Max(8, rect.Height - HeaderH - 4));

            double lo, hi;
            BaseRange(ios, out lo, out hi);

            LaneY ly = LaneFor(laneKey);
            double span = (hi - lo) / ly.Zoom;
            if (!(span > 0)) span = 1;
            double center = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            DrawValueAxis(dc, p, inner, vlo, vhi);

            for (int k = 0; k < ios.Length; k++)
            {
                Pen before = _laneMode ? p.BeforePen : p.SeriesPen(k);
                Pen after = _laneMode ? p.AfterPen : PairPen(p, k);
                DrawChannel(dc, p, inner, ios[k], vlo, vhi, before, after, columns);
            }

            DrawLaneHeader(dc, p, rect, ios);
        }

        private readonly Dictionary<int, Pen> _pairPens = new Dictionary<int, Pen>();

        /// <summary>겹쳐보기에서 "이후" 쪽 선. 같은 계열의 다른 밝기로 구분합니다.</summary>
        private Pen PairPen(Palette p, int index)
        {
            Pen pen;
            if (_pairPens.TryGetValue(index, out pen)) return pen;

            Color c = p.SeriesColor(index);
            Color paired = p.IsDark
                ? Color.FromRgb(Mix(c.R, 255), Mix(c.G, 255), Mix(c.B, 255))
                : Color.FromRgb((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55));

            var brush = new SolidColorBrush(paired);
            brush.Freeze();
            pen = new Pen(brush, 1.4);
            pen.Freeze();
            _pairPens[index] = pen;
            return pen;
        }

        private static byte Mix(byte a, byte b)
        {
            return (byte)((a + b) / 2);
        }

        private void DrawLaneHeader(DrawingContext dc, Palette p, Rect rect, IoRowVm[] ios)
        {
            double x = rect.X + 7;
            double y = rect.Y + 3;
            double right = rect.Right - 8;

            if (ios.Length == 1)
            {
                IoRowVm vm = ios[0];
                string extra = string.Empty;
                if (!vm.InAfter && _state != null && _state.After != null) extra = "   (이후 로그에 없음)";
                else if (!vm.InBefore && _state != null && _state.Before != null) extra = "   (이전 로그에 없음)";

                FormattedText name = Text(vm.Name + extra, FontNormal, p.TextBrush);
                name.SetFontWeight(FontWeights.SemiBold);
                name.MaxTextWidth = Math.Max(40, right - x - 210);
                name.MaxLineCount = 1;
                name.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(name, new Point(x, y));

                string readout = ValueReadout(vm);
                if (readout.Length > 0)
                {
                    FormattedText ft = Text(readout, FontSmall, p.MutedBrush);
                    ft.MaxLineCount = 1;
                    double rx = Math.Max(x + name.Width + 12, right - ft.Width);
                    if (rx + ft.Width <= right + 1) dc.DrawText(ft, new Point(rx, y + 1));
                }
                return;
            }

            // 겹쳐보기: 색 표시와 이름을 나란히
            for (int k = 0; k < ios.Length && x < right - 60; k++)
            {
                var swatch = new Rect(x, y + 4, 11, 8);
                dc.DrawRectangle(Frozen(p.SeriesColor(k)), null, swatch);
                x += 15;

                FormattedText ft = Text(ios[k].Name, FontSmall, p.TextBrush);
                ft.MaxLineCount = 1;
                dc.DrawText(ft, new Point(x, y + 1));
                x += ft.Width + 14;
            }
        }

        private string ValueReadout(IoRowVm vm)
        {
            if (double.IsNaN(_cursorA) || _state == null) return string.Empty;
            string s = string.Empty;
            if (vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                s += "이전 " + c.FormatValue(_state.Before.SampleAt(vm.BeforeIndex, _cursorA));
            }
            if (vm.InAfter && _state.After != null)
            {
                if (s.Length > 0) s += "    ";
                Channel c = _state.After.Channels[vm.AfterIndex];
                s += "이후 " + c.FormatValue(_state.After.SampleAt(vm.AfterIndex, _cursorA - _state.AppliedShift));
            }
            return s;
        }

        /// <summary>
        /// 레인의 기준 세로 범위. 기본은 채널 전체 범위라서 시간축을 밀어도
        /// 세로 배율이 갑자기 풀리지 않습니다. "보이는 구간에 맞춤" 을 켰을
        /// 때만 지금 보이는 구간으로 다시 잡습니다. (요구사항 2번)
        /// </summary>
        private void BaseRange(IoRowVm[] ios, out double lo, out double hi)
        {
            if (_scale == ValueScaleMode.Normalized) { lo = -0.05; hi = 1.05; return; }

            double a = double.PositiveInfinity, b = double.NegativeInfinity;
            for (int k = 0; k < ios.Length; k++)
            {
                double clo, chi;
                if (!ChannelRange(ios[k], out clo, out chi)) continue;
                if (_scale == ValueScaleMode.Delta)
                {
                    double bas = Baseline(ios[k]);
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

        private bool ChannelRange(IoRowVm vm, out double lo, out double hi)
        {
            lo = double.PositiveInfinity; hi = double.NegativeInfinity;
            if (_state == null) return false;
            bool any = false;
            double x0, x1;

            if (_fitVisible)
            {
                if (vm.InBefore && _state.Before != null
                    && Decimator.RangeIn(_state.Before, vm.BeforeIndex, _t0, _t1, out x0, out x1))
                { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                if (vm.InAfter && _state.After != null
                    && Decimator.RangeIn(_state.After, vm.AfterIndex,
                        _t0 - _state.AppliedShift, _t1 - _state.AppliedShift, out x0, out x1))
                { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                if (any) return true;
            }

            if (vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            if (vm.InAfter && _state.After != null)
            {
                Channel c = _state.After.Channels[vm.AfterIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            return any;
        }

        /// <summary>"변화만" 모드에서 빼 줄 기준값. 채널의 첫 유효 값입니다.</summary>
        private double Baseline(IoRowVm vm)
        {
            if (_state == null) return 0;
            if (vm.InBefore && _state.Before != null)
            {
                float[] v = _state.Before.Channels[vm.BeforeIndex].Values;
                for (int i = 0; i < v.Length; i++) if (!float.IsNaN(v[i])) return v[i];
            }
            if (vm.InAfter && _state.After != null)
            {
                float[] v = _state.After.Channels[vm.AfterIndex].Values;
                for (int i = 0; i < v.Length; i++) if (!float.IsNaN(v[i])) return v[i];
            }
            return 0;
        }

        private double Transform(double v, double chLo, double chHi, double baseline)
        {
            if (double.IsNaN(v)) return double.NaN;
            switch (_scale)
            {
                case ValueScaleMode.Normalized:
                    return (chHi - chLo) > 1e-12 ? (v - chLo) / (chHi - chLo) : 0.5;
                case ValueScaleMode.Delta:
                    return v - baseline;
                default:
                    return v;
            }
        }

        private void DrawChannel(DrawingContext dc, Palette p, Rect inner, IoRowVm vm,
                                 double vlo, double vhi, Pen beforePen, Pen afterPen, int columns)
        {
            if (_state == null || inner.Width <= 1) return;

            double chLo, chHi;
            if (!ChannelRange(vm, out chLo, out chHi)) { chLo = 0; chHi = 1; }
            double baseline = _scale == ValueScaleMode.Delta ? Baseline(vm) : 0;

            bool haveB = vm.InBefore && _state.Before != null;
            bool haveA = vm.InAfter && _state.After != null;

            if (haveB) Decimator.Build(_state.Before, vm.BeforeIndex, _t0, _t1, _colsBefore, columns);
            if (haveA) Decimator.Build(_state.After, vm.AfterIndex,
                                       _t0 - _state.AppliedShift, _t1 - _state.AppliedShift, _colsAfter, columns);

            double sep = _separate ? inner.Height * 0.02 : 0.0;

            if (_shade && haveB && haveA)
                ShadeGap(dc, p, inner, columns, vlo, vhi, chLo, chHi, baseline, sep);

            if (haveB) DrawTrace(dc, inner, _colsBefore, columns, vlo, vhi, chLo, chHi, baseline, beforePen, -sep);
            if (haveA) DrawTrace(dc, inner, _colsAfter, columns, vlo, vhi, chLo, chHi, baseline, afterPen, +sep);
        }

        /// <summary>
        /// 열 요약을 선 하나로 만들어 그립니다. 값이 끊긴 자리(NaN)에서는
        /// 도형을 끊어 선을 잇지 않습니다.
        /// </summary>
        private void DrawTrace(DrawingContext dc, Rect inner, Decimator.Column[] cols, int columns,
                               double vlo, double vhi, double chLo, double chHi,
                               double baseline, Pen pen, double dy)
        {
            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();

            using (StreamGeometryContext ctx = geo.Open())
            {
                bool open = false;
                _pts.Clear();

                for (int x = 0; x < columns; x++)
                {
                    Decimator.Column c = cols[x];
                    if (!c.HasValue)
                    {
                        if (open) Flush(ctx, ref open);
                        continue;
                    }

                    double px = inner.Left + x;
                    // 화면 y 는 값이 클수록 작아집니다. yTop 이 c.Max, yBottom 이 c.Min.
                    double yFirst = Clamp(ValueToY(Transform(c.First, chLo, chHi, baseline), inner, vlo, vhi) + dy, top, bottom);
                    double yTop = Clamp(ValueToY(Transform(c.Max, chLo, chHi, baseline), inner, vlo, vhi) + dy, top, bottom);
                    double yBottom = Clamp(ValueToY(Transform(c.Min, chLo, chHi, baseline), inner, vlo, vhi) + dy, top, bottom);
                    double yLast = Clamp(ValueToY(Transform(c.Last, chLo, chHi, baseline), inner, vlo, vhi) + dy, top, bottom);

                    if (!open)
                    {
                        ctx.BeginFigure(new Point(px, yFirst), false, false);
                        open = true;
                    }
                    else _pts.Add(new Point(px, yFirst));

                    if (yTop != yFirst) _pts.Add(new Point(px, yTop));
                    if (yBottom != yTop) _pts.Add(new Point(px, yBottom));
                    if (yLast != yBottom) _pts.Add(new Point(px, yLast));
                }
                if (open) Flush(ctx, ref open);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        private void Flush(StreamGeometryContext ctx, ref bool open)
        {
            if (_pts.Count > 0)
            {
                ctx.PolyLineTo(_pts, true, false);
                _pts.Clear();
            }
            open = false;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        /// <summary>
        /// 이전과 이후가 벌어진 구간을 옅은 색으로 채웁니다.
        /// 점선을 쓰지 않는 이유는, 점선이 파형의 빈 구간과 섞여 파형을
        /// 읽기 어렵게 만들기 때문입니다. (요구사항 5번)
        ///
        /// 1 픽셀짜리 네모를 열마다 하나씩 담되, 전부 한 도형에 모아
        /// 한 번에 칠합니다. 붙어 있는 네모들이 모여 자연스러운 띠가 됩니다.
        /// </summary>
        private void ShadeGap(DrawingContext dc, Palette p, Rect inner, int columns,
                              double vlo, double vhi, double chLo, double chHi,
                              double baseline, double sep)
        {
            var geo = new StreamGeometry();
            bool any = false;

            using (StreamGeometryContext ctx = geo.Open())
            {
                for (int x = 0; x < columns; x++)
                {
                    Decimator.Column cb = _colsBefore[x], ca = _colsAfter[x];
                    if (!cb.HasValue || !ca.HasValue) continue;
                    if (Math.Abs(cb.Last - ca.Last) <= _tolerance) continue;

                    double y1 = ValueToY(Transform(cb.Last, chLo, chHi, baseline), inner, vlo, vhi) - sep;
                    double y2 = ValueToY(Transform(ca.Last, chLo, chHi, baseline), inner, vlo, vhi) + sep;
                    double t = Clamp(Math.Min(y1, y2), inner.Top, inner.Bottom);
                    double b = Clamp(Math.Max(y1, y2), inner.Top, inner.Bottom);
                    if (b - t < 1) b = t + 1;

                    double px = inner.Left + x;
                    ctx.BeginFigure(new Point(px, t), true, true);
                    ctx.LineTo(new Point(px + 1, t), false, false);
                    ctx.LineTo(new Point(px + 1, b), false, false);
                    ctx.LineTo(new Point(px, b), false, false);
                    any = true;
                }
            }
            if (!any) return;
            geo.Freeze();
            dc.DrawGeometry(p.DiffBrush, null, geo);
        }

        // ---------------- 눈금 ----------------

        private void DrawValueAxis(DrawingContext dc, Palette p, Rect inner, double vlo, double vhi)
        {
            double step = NiceStep(vhi - vlo, Math.Max(2, (int)(inner.Height / 34)));
            if (!(step > 0)) return;

            double first = Math.Ceiling(vlo / step) * step;
            for (double v = first; v <= vhi + step * 0.001; v += step)
            {
                double y = ValueToY(v, inner, vlo, vhi);
                if (y < inner.Top - 1 || y > inner.Bottom + 1) continue;

                dc.DrawLine(p.GridPen, new Point(inner.Left + 1, y), new Point(inner.Right - 2, y));

                // 눈금 글자는 선을 그은 바로 그 y 에 붙입니다. 그래서 숫자와
                // 그래프 높이가 어긋날 수 없습니다. (요구사항 2번)
                FormattedText ft = Text(FormatTick(v, step), FontSmall, p.MutedBrush);
                dc.DrawText(ft, new Point(Math.Max(2, GutterW - 8 - ft.Width), y - ft.Height * 0.5));
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

        private void DrawTimeAxis(DrawingContext dc, Palette p, Rect plot)
        {
            var strip = new Rect(0, plot.Bottom, ActualWidth, AxisH);
            dc.DrawRectangle(p.AxisStripBrush, null, strip);
            dc.DrawLine(p.BorderPen, new Point(0, strip.Top), new Point(strip.Right, strip.Top));

            LogDataset refDs = _state != null ? _state.TimeReference : null;
            double span = _t1 - _t0;
            if (!(span > 0)) return;

            int wanted = Math.Max(2, (int)(plot.Width / 110));
            double step = NiceStep(span, wanted);
            if (!(step > 0)) return;

            double first = Math.Ceiling(_t0 / step) * step;
            for (double t = first; t <= _t1; t += step)
            {
                double x = TimeToX(t);
                if (x < plot.Left || x > plot.Right) continue;

                dc.DrawLine(p.GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
                dc.DrawLine(p.BorderPen, new Point(x, strip.Top), new Point(x, strip.Top + 4));

                string label = refDs != null ? refDs.FormatTime(t) : t.ToString("0.###");
                FormattedText ft = Text(label, FontSmall, p.MutedBrush);
                dc.DrawText(ft, new Point(x - ft.Width * 0.5, strip.Top + 5));
            }
        }

        private void DrawCursor(DrawingContext dc, Palette p, Rect plot, double t, Pen pen, Color color, string tag)
        {
            if (double.IsNaN(t) || t < _t0 || t > _t1) return;
            double x = TimeToX(t);
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));

            var box = new Rect(x - 9, plot.Top + 1, 18, 15);
            dc.DrawRectangle(Frozen(color), null, box);

            FormattedText ft = Text(tag, FontSmall, Brushes.White);
            ft.SetFontWeight(FontWeights.Bold);
            dc.DrawText(ft, new Point(box.Left + (box.Width - ft.Width) * 0.5,
                                      box.Top + (box.Height - ft.Height) * 0.5));
        }

        // ---------------- 마우스 ----------------

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_state == null) return;

            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);

            // 한 칸에 너무 많이 커지지 않게 아주 조금씩 바꿉니다. (요구사항 2번)
            double factor = Math.Pow(1.14, notches);
            Point at = e.GetPosition(this);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) ZoomValueAt(at, factor);
            else ZoomTimeAround(XToTime(at.X), factor);

            InvalidateVisual();
            RaiseViewChanged();
            e.Handled = true;
        }

        /// <summary>
        /// Shift + 휠. 커서가 가리키던 값이 그 자리에 그대로 남도록
        /// 가운데를 다시 잡습니다. (요구사항 2번)
        /// </summary>
        private void ZoomValueAt(Point at, double factor)
        {
            string key; Rect inner; IoRowVm[] ios;
            if (!LaneAt(at, out key, out inner, out ios)) return;

            double lo, hi;
            BaseRange(ios, out lo, out hi);

            LaneY ly = LaneFor(key);
            double span = (hi - lo) / ly.Zoom;
            double center = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;

            double f = inner.Height > 0 ? (inner.Bottom - at.Y) / inner.Height : 0.5;
            double valueAt = center - span * 0.5 + f * span;

            ly.Zoom *= factor;
            ClampZooms();
            double newSpan = (hi - lo) / ly.Zoom;
            ly.Center = valueAt - (f - 0.5) * newSpan;
        }

        private bool LaneAt(Point at, out string key, out Rect inner, out IoRowVm[] ios)
        {
            key = OverlayLaneKey;
            inner = Rect.Empty;
            ios = new IoRowVm[0];

            Rect plot = PlotArea;
            if (!plot.Contains(at) || _channels.Count == 0) return false;

            if (!_laneMode)
            {
                inner = new Rect(plot.Left, plot.Top + HeaderH, plot.Width,
                                 Math.Max(8, plot.Height - HeaderH - 4));
                ios = _channels.ToArray();
                return true;
            }

            double laneH = Math.Max(MinLaneH, plot.Height / Math.Max(1, _channels.Count));
            double needed = laneH * _channels.Count;
            double scrollY = needed > plot.Height ? VerticalOffset : 0;

            int idx = (int)((at.Y + scrollY) / laneH);
            if (idx < 0 || idx >= _channels.Count) return false;

            double top = idx * laneH - scrollY;
            key = _channels[idx].Name;
            inner = new Rect(plot.Left, top + HeaderH, plot.Width, Math.Max(8, laneH - HeaderH - 4));
            ios = new[] { _channels[idx] };
            return true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (_state == null) return;

            _dragging = true;
            _moved = false;
            _dragPoint = e.GetPosition(this);
            _dragT0 = _t0; _dragT1 = _t1;

            string key; Rect inner; IoRowVm[] ios;
            if (LaneAt(_dragPoint, out key, out inner, out ios))
            {
                _dragLane = key;
                double lo, hi;
                BaseRange(ios, out lo, out hi);
                LaneY ly = LaneFor(key);
                _dragCenter = double.IsNaN(ly.Center) ? (lo + hi) * 0.5 : ly.Center;
            }
            else _dragLane = null;

            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            Point now = e.GetPosition(this);
            double dx = now.X - _dragPoint.X, dy = now.Y - _dragPoint.Y;
            if (!_moved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
            _moved = true;

            Rect plot = PlotArea;
            if (plot.Width > 0)
            {
                double span = _dragT1 - _dragT0;
                double shift = -dx * span / plot.Width;
                _t0 = _dragT0 + shift;
                _t1 = _dragT1 + shift;
            }

            if (_dragLane != null)
            {
                string key; Rect inner; IoRowVm[] ios;
                if (LaneAt(_dragPoint, out key, out inner, out ios) && inner.Height > 0)
                {
                    double lo, hi;
                    BaseRange(ios, out lo, out hi);
                    LaneY ly = LaneFor(key);
                    double vspan = (hi - lo) / ly.Zoom;
                    ly.Center = _dragCenter + dy * vspan / inner.Height;
                }
            }

            InvalidateVisual();
            RaiseViewChanged();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            if (_moved) return;

            // 끌지 않고 그냥 눌렀으면 커서를 놓습니다.
            double t = XToTime(e.GetPosition(this).X);
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _cursorB = t; else _cursorA = t;
            InvalidateVisual();
            RaiseViewChanged();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double span = _t1 - _t0;
            switch (e.Key)
            {
                case Key.Left: _t0 -= span * 0.1; _t1 -= span * 0.1; break;
                case Key.Right: _t0 += span * 0.1; _t1 += span * 0.1; break;
                case Key.Home: ResetTime(); e.Handled = true; return;
                case Key.OemPlus: case Key.Add: ZoomTime(1.3); e.Handled = true; return;
                case Key.OemMinus: case Key.Subtract: ZoomTime(1 / 1.3); e.Handled = true; return;
                default: return;
            }
            e.Handled = true;
            InvalidateVisual();
            RaiseViewChanged();
        }
    }
}
