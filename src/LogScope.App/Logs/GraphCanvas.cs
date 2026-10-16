using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.App.Themes;
using LogScope.Logs.Compare;
using LogScope.Core.Model;
using LogScope.Logs.Align;
using LogScope.Logs.Render;

using LogScope.App.Common;
using LogScope.App.Settings;
namespace LogScope.App.Logs
{
    /// <summary>
    /// 세로 눈금을 무엇으로 읽을지.
    ///
    /// <b>0–1 정규화는 없앴습니다.</b> 레인 보기에서는 값과 눈금에 똑같은
    /// 변환이 걸려 그림이 하나도 안 바뀌었고, 겹쳐보기에서도 "이 채널의
    /// 최소~최대 안에서 몇 %" 라는 숫자는 로그를 읽을 때 쓸 데가 없었습니다.
    /// </summary>
    public enum ValueScaleMode
    {
        /// <summary>로그에 적힌 값 그대로.</summary>
        Raw,
        /// <summary>
        /// <b>두 로그의 차이</b>. 이전과 이후를 따로 그리지 않고
        /// "이후 − 이전" 선 하나를 그립니다. 같으면 0, 이후가 1 크면 +1,
        /// 1 작으면 -1.
        /// </summary>
        Delta,
    }

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
    public sealed partial class GraphCanvas : FrameworkElement
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

        /// <summary>
        /// 지난 끌기 알림에서의 자리. 칸을 끌어 옮길 때 <b>이번에 움직인
        /// 만큼</b>만 밀려면 필요합니다 — 누른 자리에서 센 값을 쓰면 알림이
        /// 올 때마다 같은 거리를 또 밀어 가속이 붙습니다.
        /// </summary>
        private Point _dragLast;
        private double _dragT0, _dragT1, _dragCenter;
        private string _dragLane;

        // 오른쪽 단추로 끌어 네모를 그리면 그 자리만큼 확대합니다.
        private bool _band;
        private Point _bandFrom, _bandTo;
        private string _bandLane;

        private Decimator.Column[] _colsBefore = new Decimator.Column[0];
        private Decimator.Column[] _colsAfter = new Decimator.Column[0];
        private float[] _bandBefore = new float[0];
        private float[] _bandAfter = new float[0];

        // 차이 눈금("이후 − 이전")에서 쓰는 자리.
        private Decimator.Column[] _colsDiff = new Decimator.Column[0];
        private double[] _diffT = new double[0];
        private float[] _diffV = new float[0];
        private readonly List<Point> _pts = new List<Point>(4096);

        // 차이 영역 표시를 이어진 덩어리째 폴리곤으로 묶을 때 쓰는 자리.
        // 매 프레임 새로 잡지 않으려고 들고 있습니다.
        private readonly List<Point> _edgeTop = new List<Point>(4096);
        private readonly List<Point> _edgeBottom = new List<Point>(4096);

        // WPF 의 좌표 단위는 픽셀이 아니라 DIP(1/96 인치) 입니다. 화면 배율이
        // 150% 면 1000 DIP 가 실제로는 1500 픽셀입니다. 접을 열 수를 DIP 로
        // 세면 실제 픽셀의 2/3 만 쓰게 되어 파형이 뭉개져 보입니다.
        private double _pixelsPerDip = 1.0;

        private void RefreshPixelsPerDip()
        {
            PresentationSource src = PresentationSource.FromVisual(this);
            if (src == null || src.CompositionTarget == null) { _pixelsPerDip = 1.0; return; }
            double m = src.CompositionTarget.TransformToDevice.M11;
            _pixelsPerDip = (m > 0.2 && m < 8) ? m : 1.0;
        }

        private readonly Typeface _face = new Typeface("Segoe UI");
        private const double FontNormal = 12.0;
        private const double FontSmall = 11.0;

        public GraphCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            // 1 픽셀 선이 흐려지지 않게 합니다. 파형은 또렷해야 읽힙니다.
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
            // 로그가 바뀌었을 수 있으니 갈무리해 둔 차이 폭을 버립니다.
            _diffRange.Clear();
            _diffRangeShift = double.NaN;
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

        // 선 하나씩 끄고 보기. 꺼진 쪽은 세로 눈금 계산에서도 빠집니다
        // (ChannelRange) — 안 보이는 선 때문에 눈금이 넓어지면 보이는 쪽이
        // 납작해져서, 끈 보람이 없습니다.
        private bool _showBefore = true;
        public bool ShowBefore
        {
            get { return _showBefore; }
            set { if (_showBefore != value) { _showBefore = value; InvalidateVisual(); } }
        }

        private bool _showAfter = true;
        public bool ShowAfter
        {
            get { return _showAfter; }
            set { if (_showAfter != value) { _showAfter = value; InvalidateVisual(); } }
        }

        /// <summary>
        /// 차이 눈금은 두 로그가 다 필요한 값이라 한쪽만 끌 수가 없습니다.
        /// 그 모드에서는 켜고 끄기를 <b>무시</b>합니다.
        /// </summary>
        private bool ShownB { get { return Diffing || _showBefore; } }
        private bool ShownA { get { return Diffing || _showAfter; } }

        // 허용 오차. 그 순간의 값끼리 견줍니다 — |이후 − 이전| / |이전| x 100.
        // 실제 계산은 Core 의 ToleranceRule 이 하고, 대시보드·히트맵도 같은
        // 함수를 씁니다. 그래야 세 화면이 같은 IO 를 같게 판정합니다.

        private double _absoluteTolerance;
        public double AbsoluteTolerance
        {
            get { return _absoluteTolerance; }
            set { if (_absoluteTolerance != value) { _absoluteTolerance = value; InvalidateVisual(); } }
        }

        private double _relativePercent = ToleranceRule.DefaultPercent;
        /// <summary>허용 오차 퍼센트. 설정에 적는 그 값 그대로 (0.1 이 0.1%).</summary>
        public double RelativePercent
        {
            get { return _relativePercent; }
            set { if (_relativePercent != value) { _relativePercent = value; InvalidateVisual(); } }
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
        /// <summary>
        /// 지금 보이는 구간. <b>단계별로 나눠 보는 중이면 첫 칸부터 마지막
        /// 칸까지</b>입니다 — 그 사이에 안 보이는 빈틈이 있지만, 아래 줄에
        /// 적는 "보이는 구간" 으로는 그게 맞습니다.
        /// </summary>
        public double VisibleStart
        {
            get
            {
                if (!StepMode) return _t0;
                double a, b; StepView(0, out a, out b);
                return a;
            }
        }

        public double VisibleEnd
        {
            get
            {
                if (!StepMode) return _t1;
                double a, b; StepView(_stepBands.Count - 1, out a, out b);
                return b;
            }
        }

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
            // 칸으로 나눠 보는 중이면 칸들을 단계 전체로 되돌립니다
            // (그게 이 모드의 "전체" 입니다).
            ResetStepViews();

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

        // ---- 가로축 0 자리 ----
        //
        // 단계 하나를 볼 때 가로축을 <b>그 단계 시작부터</b> 세고 싶습니다.
        // 그러면 단계를 바꿔 가며 봐도 눈금이 늘 0 에서 시작해서, 다른
        // 단계끼리도 "시작 몇 초 뒤" 를 바로 견줄 수 있습니다.
        //
        // 그림은 그대로 그립니다 — <b>눈금 글자만</b> 바뀝니다. 선을 옮기면
        // 커서 값·차이 음영·이후 로그 밀기까지 다 어긋납니다.

        private double _zeroAt = double.NaN;

        /// <summary>
        /// 가로축 글자를 이 시각부터 셉니다. NaN 이면 로그의 시간을 그대로
        /// 적습니다.
        /// </summary>
        public double ZeroAt
        {
            get { return _zeroAt; }
            set
            {
                if (_zeroAt.Equals(value)) return;
                _zeroAt = value;
                InvalidateVisual();
            }
        }

        /// <summary>눈금 한 칸의 글자.</summary>
        private string TimeLabel(LogDataset refDs, double t)
        {
            if (!double.IsNaN(_zeroAt)) return TimeText.Elapsed(refDs, t - _zeroAt);
            return refDs != null ? refDs.FormatTime(t) : t.ToString("0.###");
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

        /// <summary>
        /// 지금 보이는 시간 구간에 세로 눈금을 <b>한 번</b> 맞춥니다.
        ///
        /// "보이는 구간에 맞춤" 토글과 다릅니다. 토글은 계속 따라다니면서
        /// 시간축을 밀 때마다 세로 배율을 바꿔 버립니다. 이건 한 번 맞춰 놓고
        /// 손을 떼서, 그 뒤로는 사용자가 잡은 배율이 유지됩니다.
        ///
        /// 히트맵에서 칸을 눌러 넘어올 때 씁니다. 가로축만 그 구간으로
        /// 맞춰 주고 세로축을 로그 전체 범위에 둬 버리면, 4000 짜리 채널에서
        /// 3 벌어진 것은 선 두 개가 딱 붙어 보여서 아무것도 알 수 없습니다.
        /// </summary>
        public void FitValueToVisibleOnce()
        {
            if (_channels.Count == 0) return;

            if (_laneMode)
            {
                for (int i = 0; i < _channels.Count; i++)
                    FitLaneToVisible(new[] { _channels[i] }, _channels[i].Name);
            }
            else
            {
                FitLaneToVisible(_channels.ToArray(), OverlayLaneKey);
            }

            ClampZooms();
            InvalidateVisual();
        }

        /// <summary>
        /// 레인 하나를 보이는 구간에 맞춥니다.
        ///
        /// 그리는 쪽은 "기준 범위를 Zoom 으로 나누고 Center 에 놓는다" 로
        /// 돼 있습니다(DrawLane). 그래서 여기서도 <b>같은 식을 거꾸로</b> 풀어
        /// Zoom 과 Center 를 냅니다. 따로 계산하면 화면과 어긋납니다.
        /// </summary>
        private void FitLaneToVisible(IoRowVm[] ios, string key)
        {
            double lo, hi;
            bool saved = _fitVisible;
            bool ok1, ok2;

            _fitVisible = false;
            BaseRange(ios, out lo, out hi, out ok1);    // 기준 범위 (배율 1 일 때)
            double full = hi - lo;

            double vlo, vhi;
            _fitVisible = true;
            BaseRange(ios, out vlo, out vhi, out ok2);  // 지금 보이는 구간의 범위
            _fitVisible = saved;

            // 둘 중 하나라도 진짜 값에서 나온 게 아니면 손대지 않습니다.
            // 가짜 0~1 에서 뽑은 자리를 적어 두면 그 뒤로 눈금이 0 에 붙박입니다.
            if (!ok1 || !ok2) return;
            double span = vhi - vlo;
            if (!(full > 0) || !(span > 0)) return;

            LaneY ly = LaneFor(key);
            ly.Center = (vlo + vhi) * 0.5;
            ly.Zoom = full / span;
        }

        public void ZoomTime(double factor)
        {
            // 칸으로 나눠 보는 중이면 모든 칸을 같은 배율로 — 단계마다
            // 같은 자리를 보게 해서 서로 견주려는 것입니다.
            if (StepMode) ZoomAllSteps(factor);
            else ZoomTimeAround((_t0 + _t1) * 0.5, factor);

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

        private const double GutterW = 80;
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

            // 단계별로 나눠 보는 중이면 칸마다 시간이 따로 흐릅니다.
            if (StepMode)
            {
                double left, width;
                int k = StepAt(x, p, out left, out width);
                if (k < 0) return double.NaN;     // 칸 사이의 틈
                double a, b;
                StepView(k, out a, out b);
                return a + (x - left) * (b - a) / Math.Max(1e-9, width);
            }

            return _t0 + (x - p.Left) * (_t1 - _t0) / p.Width;
        }

        /// <summary>
        /// 그 시각이 그려지는 자리. <b>단계별로 나눠 보는 중에는 칸 밖의
        /// 시각이 있습니다</b> (단계 사이의 빈틈) — 그때는 NaN 입니다.
        /// 부르는 쪽이 그리지 않고 넘겨야 합니다.
        /// </summary>
        private double TimeToX(double t)
        {
            Rect p = PlotArea;

            if (StepMode)
            {
                double[] xs, ws;
                StepLayout(p, out xs, out ws);
                for (int i = 0; i < xs.Length; i++)
                {
                    double a, b;
                    StepView(i, out a, out b);
                    if (t >= a && t <= b)
                        return xs[i] + (t - a) * ws[i] / Math.Max(1e-9, b - a);
                }
                return double.NaN;
            }

            double span = _t1 - _t0;
            if (span <= 0) return p.Left;
            return p.Left + (t - _t0) * p.Width / span;
        }

        // ---------------- 단계별로 나눠 보기 ----------------
        //
        // 가로를 한 줄 시간으로 쭉 늘어놓는 대신, <b>단계마다 칸을 하나씩</b>
        // 두고 나란히 놓습니다. 칸마다 밀기 값이 따로라서 이전·이후 로그가
        // 그 단계 안에서 맞춰집니다 — 공정이 중간에 늘어진 로그는 한 줄
        // 시간으로 보면 뒤로 갈수록 어긋나는데, 칸으로 끊으면 그 어긋남이
        // 칸 안에서 끝납니다.
        //
        // 그리는 코드는 그대로 씁니다. 칸마다 _t0·_t1 과 밀기 값만 바꿔
        // 끼우고, 레인 자리를 칸만큼 좁혀서 같은 함수를 다시 부릅니다.
        // 그리는 함수들이 보이는 구간과 자리를 <b>인자로</b> 받고 있어서
        // 가능했습니다.

        private StepBands _stepBands;

        /// <summary>
        /// 칸 목록. null 이거나 비어 있으면 평소대로 한 줄 시간으로 그립니다.
        /// </summary>
        public StepBands StepBands
        {
            get { return _stepBands; }
            set
            {
                _stepBands = value;
                _stepX = null;
                _stepW = null;
                _stepT0 = null;
                _stepT1 = null;

                // 밀기 값이 칸마다 달라지므로 값 범위 갈무리를 버립니다.
                // 안 버리면 한 줄 시간으로 재 둔 범위로 칸을 그립니다.
                _diffRange.Clear();
                _diffRangeShift = double.NaN;

                InvalidateVisual();
                RaiseViewChanged();
            }
        }

        private bool StepMode { get { return _stepBands != null && !_stepBands.IsEmpty; } }

        /// <summary>
        /// 칸 하나의 최소 폭. 짧은 단계가 <b>선 한 줄</b>로 찌그러지면 그
        /// 단계는 아예 못 봅니다.
        /// </summary>
        private const double StepMinWidth = 44;

        /// <summary>
        /// 칸 사이의 틈. 금 한 줄만 긋는 것보다 띄우는 쪽이 낫습니다 —
        /// 칸 사이는 시간이 <b>건너뛴</b> 자리인데, 선 하나로만 가르면
        /// 두 칸의 선이 이어진 하나로 읽힙니다.
        /// </summary>
        private const double StepGap = 10;

        // 나눠 둔 자리. 그리는 쪽·눈금 글자·마우스가 <b>같은 자리</b>를
        // 봐야 해서 한 번 나누고 그 값을 모두가 씁니다. 창 너비가 바뀌면
        // 다시 나눕니다.
        private double[] _stepX;
        private double[] _stepW;
        private double _stepLayoutFor = double.NaN;
        private double _stepLayoutLeft = double.NaN;

        private void StepLayout(Rect plot, out double[] xs, out double[] ws)
        {
            if (_stepX == null || _stepX.Length != _stepBands.Count
                || _stepLayoutFor != plot.Width || _stepLayoutLeft != plot.Left)
            {
                _stepBands.Layout(plot.Left, plot.Width, StepGap, StepMinWidth, out _stepX, out _stepW);
                _stepLayoutFor = plot.Width;
                _stepLayoutLeft = plot.Left;
            }
            xs = _stepX;
            ws = _stepW;
        }

        /// <summary>그 x 가 몇 번 칸인지. 칸 사이의 틈이면 -1.</summary>
        private int StepAt(double x, Rect plot, out double left, out double width)
        {
            left = plot.Left; width = 0;
            if (!StepMode) return -1;

            double[] xs, ws;
            StepLayout(plot, out xs, out ws);
            for (int i = 0; i < xs.Length; i++)
            {
                if (x >= xs[i] && x <= xs[i] + ws[i]) { left = xs[i]; width = ws[i]; return i; }
            }
            return -1;
        }

        // ---- 칸마다 보는 구간 ----
        //
        // 칸도 확대·이동이 됩니다. <b>칸마다 따로</b>입니다 — 커서를 올려
        // 둔 칸만 움직입니다. 그래야 한 단계를 들여다보는 동안 다른 칸은
        // 그대로 남아 견줄 바탕이 됩니다.
        //
        // 칸의 단계 구간 밖으로는 나가지 않습니다. 나가면 그 칸에 옆 단계의
        // 그림이 들어오는데, 그러면 "이 칸 = 이 단계" 가 깨집니다.

        private double[] _stepT0;
        private double[] _stepT1;

        private void EnsureStepViews()
        {
            int n = _stepBands.Count;
            if (_stepT0 != null && _stepT0.Length == n) return;

            _stepT0 = new double[n];
            _stepT1 = new double[n];
            for (int i = 0; i < n; i++) { _stepT0[i] = _stepBands[i].T0; _stepT1[i] = _stepBands[i].T1; }
        }

        /// <summary>칸들의 보는 구간을 단계 전체로 되돌립니다.</summary>
        private void ResetStepViews()
        {
            _stepT0 = null;
            _stepT1 = null;
            if (StepMode) EnsureStepViews();
        }

        /// <summary>
        /// 칸 하나를 확대·축소합니다. <paramref name="at"/> 가 가리키던
        /// 시각이 그 자리에 그대로 남습니다 (휠 확대와 같은 약속).
        /// </summary>
        private void ZoomStep(int k, double at, double factor)
        {
            EnsureStepViews();
            if (k < 0 || k >= _stepT0.Length) return;

            StepBand band = _stepBands[k];
            double t0 = _stepT0[k], t1 = _stepT1[k];
            double span = t1 - t0;
            if (!(span > 0) || !(factor > 0)) return;

            if (double.IsNaN(at)) at = (t0 + t1) * 0.5;

            double f = (at - t0) / span;
            double next = span / factor;

            // 아래로는 단계 길이의 천만분의 일까지 (휠 확대와 같은 한계),
            // 위로는 단계 전체까지.
            double full = band.Length;
            double min = full * 1e-7;
            if (min <= 0) min = 1e-9;
            if (next < min) next = min;
            if (next > full) next = full;

            double a = at - f * next;
            double b = a + next;

            // 단계 밖으로 밀려 나가면 안으로 당겨 옵니다.
            if (a < band.T0) { a = band.T0; b = a + next; }
            if (b > band.T1) { b = band.T1; a = b - next; }
            if (a < band.T0) a = band.T0;

            _stepT0[k] = a;
            _stepT1[k] = b;
        }

        /// <summary>칸 하나를 좌우로 옮깁니다 (단계 안에서만).</summary>
        private void PanStep(int k, double fraction)
        {
            EnsureStepViews();
            if (k < 0 || k >= _stepT0.Length) return;

            StepBand band = _stepBands[k];
            double span = _stepT1[k] - _stepT0[k];
            if (!(span > 0)) return;

            double d = span * fraction;
            double a = _stepT0[k] + d, b = _stepT1[k] + d;
            if (a < band.T0) { a = band.T0; b = a + span; }
            if (b > band.T1) { b = band.T1; a = b - span; }

            _stepT0[k] = a;
            _stepT1[k] = b;
        }

        /// <summary>모든 칸을 같은 배율로. 키보드 +/- 와 좌우 키가 씁니다.</summary>
        private void ZoomAllSteps(double factor)
        {
            EnsureStepViews();
            for (int i = 0; i < _stepT0.Length; i++)
                ZoomStep(i, (_stepT0[i] + _stepT1[i]) * 0.5, factor);
        }

        private void PanAllSteps(double fraction)
        {
            EnsureStepViews();
            for (int i = 0; i < _stepT0.Length; i++) PanStep(i, fraction);
        }

        /// <summary>칸 하나가 지금 보여 주는 구간.</summary>
        private void StepView(int k, out double t0, out double t1)
        {
            EnsureStepViews();
            if (k >= 0 && k < _stepT0.Length) { t0 = _stepT0[k]; t1 = _stepT1[k]; return; }
            t0 = 0; t1 = 1;
        }

        // ---- 밀기 값 ----
        //
        // 지금 그리고 있는 칸의 밀기 값입니다. 칸을 그리는 동안만 값이 들어
        // 있고, 칸 밖에서는 NaN 이라 설정의 밀기 값을 씁니다.

        private double _stepShift = double.NaN;

        /// <summary>지금 그리는 자리의 밀기 값.</summary>
        private double ShiftNow
        {
            get
            {
                if (!double.IsNaN(_stepShift)) return _stepShift;
                return _state != null ? _state.AppliedShift : 0.0;
            }
        }

        /// <summary>
        /// 그 시각 자리의 밀기 값. 커서 값을 읽을 때 씁니다 — 커서는 칸
        /// 밖(레인 머리글)에서 읽는데, 그 값은 커서가 <b>든 칸</b>의 밀기
        /// 값으로 읽어야 맞습니다.
        /// </summary>
        private double ShiftAt(double t)
        {
            if (StepMode && !double.IsNaN(t))
            {
                for (int i = 0; i < _stepBands.Count; i++)
                {
                    StepBand band = _stepBands[i];
                    if (t >= band.T0 && t <= band.T1) return band.Shift;
                }
            }
            return _state != null ? _state.AppliedShift : 0.0;
        }

        private static double ValueToY(double v, Rect inner, double vlo, double vhi)
        {
            double span = vhi - vlo;
            if (!(span > 0)) span = 1;
            return inner.Bottom - (v - vlo) / span * inner.Height;
        }
    }
}
