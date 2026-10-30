using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;
using LogScope.Themes;

namespace LogScope.Analysis1.Graph
{
    // 1.50: 그래프 판 — 전에는 GraphCanvas.cs · .Draw · .Lane · .Trace · .Axis · .Input 여섯 조각(partial)이었습니다.
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

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

        /// <summary>
        /// 단계로 나눠 볼 때 그림 <b>위</b>에 두는 칸 머리 줄의 높이 (1.16).
        /// 단계 이름이 여기 적히고, 누르면 그 단계만 봅니다. 나눠 보지
        /// 않을 때는 0 이라 그림이 맨 위부터 시작합니다.
        /// </summary>
        private const double StepHeadH = 22;

        private double TopH { get { return StepMode ? StepHeadH : 0; } }

        /// <summary>
        /// 단계 칸 꼬리를 그리는 동안만: 0 = 두 로그 다, 1 = 이전만, 2 = 이후만
        /// (1.18, DrawBandPart). 평소에는 0 입니다.
        /// </summary>
        private int _sideOnly;

        private Rect PlotArea
        {
            get
            {
                double top = TopH;
                double w = Math.Max(10, ActualWidth - GutterW - 6);
                double h = Math.Max(10, ActualHeight - AxisH - top);
                return new Rect(GutterW, top, w, h);
            }
        }

        /// <summary>
        /// 칸 머리(단계 이름)를 눌렀을 때. 넘기는 값은 단계 짝 번호입니다
        /// (StepBand.Pair). 화면이 받아 그 단계만 보게 합니다.
        /// </summary>
        public event EventHandler<int> StepHeadClicked;

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

        /// <summary>
        /// 그 시각이 <b>어느 단계의 시작에서 얼마나 지난 때</b>인지.
        /// 칸으로 나눠 보는 중이 아니거나 어느 칸에도 안 들면 빈 글자입니다.
        ///
        /// 커서를 찍었을 때 아래 띠에 이 글이 붙습니다. 칸으로 나눠 보면
        /// 가로가 시각이 아니라 공정 차례라서, <b>"몇 시 몇 분" 보다
        /// "단계 시작 몇 초 뒤" 가 알고 싶은 값입니다</b> — 그래야 다른
        /// 단계의 같은 자리와 견줄 수 있습니다.
        /// </summary>
        public string StepInfo(double t)
        {
            if (!StepMode || double.IsNaN(t)) return string.Empty;

            for (int i = 0; i < _stepBands.Count; i++)
            {
                StepBand band = _stepBands[i];
                if (t < band.T0 || t > band.T1) continue;

                LogDataset refDs = _state != null ? _state.TimeReference : null;
                return band.Label + " 시작 " + TimeText.Elapsed(refDs, t - band.T0);
            }
            return string.Empty;
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
            StepBand band = BandAt(t);
            if (band != null) return band.Shift;
            return _state != null ? _state.AppliedShift : 0.0;
        }

        /// <summary>그 시각이 든 단계 칸. 나눠 보지 않거나 칸 밖이면 null.</summary>
        private StepBand BandAt(double t)
        {
            if (!StepMode || double.IsNaN(t)) return null;
            for (int i = 0; i < _stepBands.Count; i++)
            {
                StepBand band = _stepBands[i];
                if (t >= band.T0 && t <= band.T1) return band;
            }
            return null;
        }

        /// <summary>
        /// 그 시각에 이전 / 이후 로그가 <b>그 단계 안에 있는지</b> (1.19). 단계
        /// 칸 꼬리(짧은 쪽 단계가 끝난 뒤)에서는 짧은 쪽이 거짓입니다 — 그 자리는
        /// 선을 안 그리므로 커서 값도 안 적습니다 (적으면 다음 단계 값이 나옵니다).
        /// 나눠 보지 않을 때는 늘 참입니다.
        /// </summary>
        private bool InStep(double t, bool before)
        {
            StepBand band = BandAt(t);
            if (band == null) return true;
            return t <= (before ? band.BeforeEnd : band.AfterEnd);
        }

        private static double ValueToY(double v, Rect inner, double vlo, double vhi)
        {
            double span = vhi - vlo;
            if (!(span > 0)) span = 1;
            return inner.Bottom - (v - vlo) / span * inner.Height;
        }
    

        // ======================================================================
        //  GraphCanvas.Draw.cs — 1.50 에 이 파일로 합침
        // ======================================================================
        // <summary>
        // 그래프 판 — <b>한 판을 그리는 큰 틀</b>. 레인을 몇 개 둘지 정하고
        // 차례로 그립니다. 범례, 네모 확대 표시, 빈 화면 안내도 여기입니다.
        //
        // 1.49 까지는 한 클래스를 파일 여섯으로 나눴습니다 (partial). 1.50 에 한 파일로
        // 합쳤고, 덩어리는 차례대로 틀(Draw) · 레인(Lane) · 선(Trace) · 눈금(Axis) ·
        // 마우스(Input) 입니다 — ==== 줄로 찾으세요.
        // </summary>


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

            RefreshPixelsPerDip();
            int columns = (int)Math.Max(1, Math.Floor(plot.Width * _pixelsPerDip));
            EnsureBuffers(columns);

            // 레인은 plot.Top 부터 쌓습니다 — 단계로 나눠 보면 그 위에 칸
            // 머리 줄(StepHeadH)이 있습니다 (1.16).
            dc.PushClip(new RectangleGeometry(new Rect(0, plot.Top, ActualWidth, plot.Height)));
            if (_laneMode)
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    double top = plot.Top + i * laneH - scrollY;
                    if (top + laneH < plot.Top || top > plot.Bottom) continue;
                    var rect = new Rect(plot.Left, top, plot.Width, laneH);
                    DrawLane(dc, p, rect, new[] { _channels[i] }, _channels[i].Name, columns);
                }
            }
            else
            {
                var rect = new Rect(plot.Left, plot.Top, plot.Width, plot.Height);
                DrawLane(dc, p, rect, _channels.ToArray(), OverlayLaneKey, columns);
            }
            dc.Pop();

            if (!Diffing && !_showBefore && !_showAfter) DrawBothOff(dc, p, plot);

            DrawTimeAxis(dc, p, plot);
            if (StepMode) DrawStepHead(dc, p, plot);
            DrawCursor(dc, p, plot, _cursorA, p.CursorAPen, p.CursorA, "A");
            DrawCursor(dc, p, plot, _cursorB, p.CursorBPen, p.CursorB, "B");
            DrawLegend(dc, p, plot);
            DrawBand(dc, p, plot);
        }

        /// <summary>
        /// 어느 선이 이전이고 어느 선이 이후인지.
        ///
        /// 색만 다르게 그려 놓고 어디에도 적어 두지 않으면, 두 선 중 무엇이
        /// 무엇인지 알 수가 없습니다. 도구 줄의 글자로 적어 두는 것으로는
        /// 부족합니다 — 보고 있는 자리에서 멀고, 겹쳐보기에서는 IO 마다 색이
        /// 달라 설명이 성립하지 않습니다.
        ///
        /// 그래서 <b>그 화면에서 실제로 쓰는 선을 그대로</b> 보여 줍니다.
        ///   레인   : 파란 선 = 이전, 빨간 선 = 이후 (어느 IO 든 같습니다)
        ///   겹쳐보기 : 첫 IO 의 두 선. IO 마다 색이 짝을 이룹니다
        ///   차이 눈금 : 선이 하나뿐이라 그 하나만
        ///
        /// 자리는 <b>그림 오른쪽 위</b>, 레인 머리글 아래입니다. 0.60 에 왼쪽
        /// IO 칸에도 같은 설명을 뒀다가 0.67 에 뺐습니다 — 같은 말이 두
        /// 군데에 적혀 있었고, 왼쪽 칸이 그만큼 좁아졌습니다. 그림 위의 글은
        /// 선에 가려질 수 있어서 판을 살짝 투명하게 깔아 둡니다.
        /// </summary>
        private void DrawLegend(DrawingContext dc, Palette p, Rect plot)
        {
            if (_channels.Count == 0) return;

            Pen firstPen, secondPen;
            string firstText, secondText, note;

            if (Diffing)
            {
                firstPen = p.DiffPen; firstText = "차이 (이후 − 이전)";
                secondPen = null; secondText = null;
                note = null;
            }
            else if (!_showBefore && !_showAfter)
            {
                return;   // 그릴 선이 없습니다. 아래 안내가 대신 적힙니다.
            }
            else
            {
                Pen bPen = ColorByIo ? p.SeriesPen(0) : p.BeforePen;
                Pen aPen = ColorByIo ? PairPen(p, 0) : p.AfterPen;
                note = ColorByIo ? "IO 마다 색이 짝을 이룹니다" : null;

                // 색이 이전/이후를 가리킬 때만 색 이름을 적습니다.
                //
                // 겹쳐 보기에서 IO 가 여럿이면 색이 IO 를 가리키므로
                // "파란 선 = 이전" 이 거짓이 됩니다. 그때는 색 이름을 떼고
                // 옆의 선 조각으로만 보여 줍니다 — 그 조각은 그 화면에서
                // 실제로 쓰는 펜이라 틀릴 수가 없습니다.
                string before = ColorByIo ? "이전" : "파란 선 = 이전";
                string after = ColorByIo ? "이후" : "빨간 선 = 이후";

                // 꺼 둔 쪽은 적지 않습니다. 없는 선을 설명하면 그게 어디
                // 있는지 찾게 됩니다.
                if (_showBefore && _showAfter)
                {
                    firstPen = bPen; firstText = before;
                    secondPen = aPen; secondText = after;
                }
                else if (_showBefore)
                {
                    firstPen = bPen; firstText = before + " (이것만 보기)";
                    secondPen = null; secondText = null;
                }
                else
                {
                    firstPen = aPen; firstText = after + " (이것만 보기)";
                    secondPen = null; secondText = null;
                }
            }

            const double sample = 18, gap = 6, padX = 8, padY = 5, lineH = 16;

            FormattedText t1 = Text(firstText, FontSmall, p.TextBrush);
            FormattedText t2 = secondText != null ? Text(secondText, FontSmall, p.TextBrush) : null;
            FormattedText t3 = note != null ? Text(note, FontSmall, p.MutedBrush) : null;

            double textW = t1.Width;
            if (t2 != null) textW = Math.Max(textW, t2.Width);
            double w = sample + gap + textW + padX * 2;
            if (t3 != null) w = Math.Max(w, t3.Width + padX * 2);

            int rows = 1 + (t2 != null ? 1 : 0);
            double h = rows * lineH + (t3 != null ? lineH : 0) + padY * 2;

            // 오른쪽 위, 레인 머리글 아래. 머리글 오른쪽에는 커서 값이
            // 적히므로 그 줄을 비켜야 합니다.
            double right = plot.Right - 6, top = plot.Top + HeaderH + 6;
            var box = new Rect(right - w, top, w, h);
            if (box.Left < plot.Left || box.Bottom > plot.Bottom) return;   // 너무 좁으면 접습니다

            // 살짝 투명하게. 뒤에 선이 지나가도 아주 가려 버리지는 않습니다.
            var back = new SolidColorBrush(p.Panel);
            back.Opacity = 0.88;
            back.Freeze();
            dc.DrawRectangle(back, p.BorderPen, box);

            double y = box.Top + padY + lineH * 0.5;
            double x0 = box.Left + padX, x1 = x0 + sample;

            dc.DrawLine(firstPen, new Point(x0, Snap(y)), new Point(x1, Snap(y)));
            dc.DrawText(t1, new Point(x1 + gap, y - t1.Height * 0.5));

            if (t2 != null)
            {
                y += lineH;
                dc.DrawLine(secondPen, new Point(x0, Snap(y)), new Point(x1, Snap(y)));
                dc.DrawText(t2, new Point(x1 + gap, y - t2.Height * 0.5));
            }

            if (t3 != null)
            {
                y += lineH;
                dc.DrawText(t3, new Point(x0, y - t3.Height * 0.5));
            }
        }

        /// <summary>
        /// 이전·이후를 둘 다 꺼 두었을 때. 말없이 비워 두면 고장으로 보입니다.
        /// </summary>
        private void DrawBothOff(DrawingContext dc, Palette p, Rect plot)
        {
            FormattedText ft = Text("이전 선과 이후 선을 둘 다 꺼 두었습니다.\n왼쪽 IO 칸의 [그릴 로그] 에서 하나를 켜 주세요.",
                                    FontNormal, p.MutedBrush);
            dc.DrawText(ft, new Point(plot.Left + Math.Max(0, (plot.Width - ft.Width) * 0.5),
                                      plot.Top + Math.Max(0, (plot.Height - ft.Height) * 0.5)));
        }

        /// <summary>오른쪽 단추로 끌고 있는 동안의 네모.</summary>
        private void DrawBand(DrawingContext dc, Palette p, Rect plot)
        {
            if (!_band) return;

            Rect r = BandRect(plot);
            if (r.Width < 1 && r.Height < 1) return;

            var fill = new SolidColorBrush(p.Accent);
            fill.Opacity = 0.16;
            fill.Freeze();
            dc.DrawRectangle(fill, p.CursorAPen, r);
        }

        private Rect BandRect(Rect plot)
        {
            double x0 = Math.Min(_bandFrom.X, _bandTo.X), x1 = Math.Max(_bandFrom.X, _bandTo.X);
            double y0 = Math.Min(_bandFrom.Y, _bandTo.Y), y1 = Math.Max(_bandFrom.Y, _bandTo.Y);

            x0 = Clamp(x0, plot.Left, plot.Right);
            x1 = Clamp(x1, plot.Left, plot.Right);
            y0 = Clamp(y0, plot.Top, plot.Bottom);
            y1 = Clamp(y1, plot.Top, plot.Bottom);
            return new Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
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
                ? "왼쪽 목록에서 IO 를 골라 주세요."
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
                _bandBefore = new float[columns];
                _bandAfter = new float[columns];
                _colsDiff = new Decimator.Column[columns];
                // 두 로그의 격자를 합쳐 훑으므로 표본이 열 수보다 많을 수
                // 있습니다. 넉넉히 두고, 그래도 넘치면 접은 쪽으로 넘어갑니다.
                _diffT = new double[columns * 2 + 8];
                _diffV = new float[columns * 2 + 8];
            }
        }

        /// <summary>열 번호를 화면 가로 좌표(DIP)로.</summary>
        private double ColumnToX(Rect inner, int column)
        {
            return inner.Left + column / _pixelsPerDip;
        }

        /// <summary>
        /// 1 픽셀짜리 선을 장치 픽셀 한가운데로 맞춥니다.
        /// 예전에는 요소 전체를 EdgeMode.Aliased 로 두어 눈금선을 또렷하게
        /// 했는데, 그러면 비스듬한 파형까지 계단처럼 보였습니다. 지금은 파형은
        /// 부드럽게 두고 눈금선만 이 함수로 맞춥니다.
        /// </summary>
        private double Snap(double v)
        {
            return (Math.Round(v * _pixelsPerDip) + 0.5) / _pixelsPerDip;
        }

        /// <summary>시각을 화면 가로 좌표(DIP)로. t0/t1 은 그 로그의 시간 기준입니다.</summary>
        private static double TimeToXIn(Rect inner, double t, double t0, double t1)
        {
            double span = t1 - t0;
            if (!(span > 0)) return inner.Left;
            return inner.Left + (t - t0) / span * inner.Width;
        }

        private FormattedText Text(string s, double size, Brush brush)
        {
            return new FormattedText(s ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, _face, size, brush);
        }
    

        // ======================================================================
        //  GraphCanvas.Lane.cs — 1.50 에 이 파일로 합침
        // ======================================================================
        // <summary>
        // 그래프 판 — <b>레인 하나</b>. 그 레인에 들어갈 IO 들의 값 범위를
        // 정하고, 머리글(이름·값·차이)을 적고, 선 그리기를 부릅니다.
        //
        // 값 범위를 정하는 셈이 여기 모여 있습니다. 세로 눈금 글자와 선의
        // 높이가 어긋나지 않는 까닭은 둘이 같은 범위를 쓰기 때문입니다.
        // </summary>


        /// <summary>레인 하나(겹쳐보기면 전체)를 그립니다.</summary>
        private void DrawLane(DrawingContext dc, Palette p, Rect rect, IoRowVm[] ios, string laneKey, int columns)
        {
            // 칸으로 나눠 그릴 때는 레인 바탕을 <b>창 색</b>으로 둡니다.
            // 그러면 칸 사이의 틈이 그 색으로 비어서 칸이 따로따로 보입니다.
            // 그림 색으로 두면 틈과 칸이 같은 색이라 띄운 뜻이 사라집니다.
            dc.DrawRectangle(StepMode ? Frozen(p.Window) : p.PlotBrush, p.BorderPen, rect);

            var inner = new Rect(rect.X, rect.Y + HeaderH, rect.Width,
                                 Math.Max(8, rect.Height - HeaderH - 4));

            double lo, hi;
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);

            LaneY ly = LaneFor(laneKey);
            double span = (hi - lo) / ly.Zoom;
            if (!(span > 0)) span = 1;
            double center = CenterOf(ly, lo, hi, usable);
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            DrawValueAxis(dc, p, rect, inner, vlo, vhi, ios);

            if (StepMode) DrawLaneInBands(dc, p, inner, ios, vlo, vhi);
            else
            {
                for (int k = 0; k < ios.Length; k++)
                {
                    Pen before = ColorByIo ? p.SeriesPen(k) : p.BeforePen;
                    Pen after = ColorByIo ? PairPen(p, k) : p.AfterPen;
                    DrawChannel(dc, p, inner, ios[k], vlo, vhi, before, after, columns);
                }
            }

            DrawLaneHeader(dc, p, rect, ios);
        }

        /// <summary>
        /// 레인 하나를 <b>단계 칸으로 나눠</b> 그립니다.
        ///
        /// 칸마다 보이는 구간(_t0·_t1)과 밀기 값(_stepShift)을 바꿔 끼우고,
        /// 자리를 그 칸만큼 좁혀서 평소에 쓰는 그리기 함수를 그대로 부릅니다.
        /// 세로 눈금은 레인에 하나뿐이라 <b>칸끼리 높이를 그대로 견줄 수
        /// 있습니다</b> (칸마다 세로를 다시 맞추면 같은 값이 칸마다 다른
        /// 높이로 그려집니다).
        ///
        /// 칸 사이에는 금을 하나 긋습니다. 금이 없으면 두 칸의 선이 이어진
        /// 하나로 보이는데, 칸 사이는 시간이 <b>건너뛴</b> 자리입니다.
        /// </summary>
        private void DrawLaneInBands(DrawingContext dc, Palette p, Rect inner, IoRowVm[] ios,
                                     double vlo, double vhi)
        {
            double[] xs, ws;
            StepLayout(new Rect(inner.X, inner.Y, inner.Width, inner.Height), out xs, out ws);

            double saveT0 = _t0, saveT1 = _t1;

            for (int bi = 0; bi < _stepBands.Count; bi++)
            {
                var sub = new Rect(xs[bi], inner.Y, Math.Max(1, ws[bi]), inner.Height);

                // 칸이 선 한 줄만큼도 안 되면 그리지 않습니다 (그려도 안
                // 보이는데 셈은 다 합니다).
                if (sub.Width < 2) continue;

                // 칸을 하나의 판처럼 그립니다 — 바탕을 칠하고 테두리를
                // 두릅니다. 칸 사이의 틈은 레인 바탕이 그대로 보여서
                // "여기는 시간이 건너뛴 자리" 가 눈에 들어옵니다.
                dc.DrawRectangle(p.PlotBrush, p.BorderPen, sub);

                StepView(bi, out _t0, out _t1);
                _stepShift = _stepBands[bi].Shift;

                int cols = (int)Math.Max(1, Math.Floor(sub.Width * _pixelsPerDip));

                // 두 로그 단계 길이가 다르면(1.18) — 둘 다 있는 자리까지는
                // 평소대로, 그 뒤 꼬리는 <b>긴 쪽 로그만</b> 그립니다. 짧은
                // 쪽은 제 단계 끝에서 선이 끊깁니다. 안 끊으면 그 꼬리에
                // 짧은 쪽의 다음 단계 값이 이어져 그려집니다.
                StepBand band = _stepBands[bi];
                double xB = BandX(sub, band.BeforeEnd), xA = BandX(sub, band.AfterEnd);
                double xBoth = Math.Min(xB, xA);

                DrawBandPart(dc, p, sub, sub.Left, xBoth, ios, vlo, vhi, cols, 0);
                if (xB > xBoth) DrawBandPart(dc, p, sub, xBoth, xB, ios, vlo, vhi, cols, 1);
                if (xA > xBoth) DrawBandPart(dc, p, sub, xBoth, xA, ios, vlo, vhi, cols, 2);
            }

            _t0 = saveT0;
            _t1 = saveT1;
            _stepShift = double.NaN;
        }

        /// <summary>
        /// 단계 칸 하나에서 [x0, x1] 사이만 그립니다. side: 0 = 두 로그 다,
        /// 1 = 이전만, 2 = 이후만 (_sideOnly 를 그동안만 바꿔 끼웁니다).
        /// </summary>
        private void DrawBandPart(DrawingContext dc, Palette p, Rect sub, double x0, double x1,
                                  IoRowVm[] ios, double vlo, double vhi, int cols, int side)
        {
            if (!(x1 > x0)) return;
            _sideOnly = side;
            dc.PushClip(new RectangleGeometry(new Rect(x0, sub.Top, x1 - x0, sub.Height)));
            for (int k = 0; k < ios.Length; k++)
            {
                Pen before = ColorByIo ? p.SeriesPen(k) : p.BeforePen;
                Pen after = ColorByIo ? PairPen(p, k) : p.AfterPen;
                DrawChannel(dc, p, sub, ios[k], vlo, vhi, before, after, cols);
            }
            dc.Pop();
            _sideOnly = 0;
        }

        /// <summary>단계 칸 안에서 그 시각의 x (칸 밖이면 칸 끝으로 가둠).</summary>
        private double BandX(Rect sub, double t)
        {
            if (double.IsNaN(t)) return sub.Right;
            double span = _t1 - _t0;
            if (!(span > 0)) return sub.Right;
            double x = sub.Left + (t - _t0) * sub.Width / span;
            return Math.Max(sub.Left, Math.Min(sub.Right, x));
        }

        /// <summary>
        /// 이 레인의 세로 가운데. <b>기준 범위 밖으로 나가지 못하게 가둡니다.</b>
        ///
        /// 가운데 값은 끌기와 Shift+휠, [구간 맞춤] 이 적어 두는 절대값입니다.
        /// 그런데 적어 두는 그 순간에 쓸 만한 범위가 없었으면(레인/겹쳐보기를
        /// 오가는 사이, 한쪽 로그에만 있는 IO, 겹치는 구간이 없는 차이 눈금 …)
        /// 0~1 이라는 가짜 범위에서 나온 값이 적힙니다. 그러면 그 뒤로 값이
        /// 제대로 돌아와도 눈금은 0 언저리에 붙박이고, 선은 화면 밖에 있어
        /// <b>IO 값이 전부 0 으로 고정된 것처럼</b> 보입니다.
        ///
        /// 가두면 그런 자리가 아예 생기지 않습니다. 적어 둔 값이 범위 밖이면
        /// 가장 가까운 끝으로 끌어당겨, 데이터가 언제나 화면 안에 들어옵니다.
        /// </summary>
        private static double CenterOf(LaneY ly, double lo, double hi, bool usable)
        {
            double mid = (lo + hi) * 0.5;
            if (double.IsNaN(ly.Center)) return mid;

            // 쓸 만한 범위가 없으면 적어 둔 값도 믿을 수 없습니다.
            if (!usable) return mid;

            if (ly.Center < lo) return lo;
            if (ly.Center > hi) return hi;
            return ly.Center;
        }

        private readonly Dictionary<int, Pen> _pairPens = new Dictionary<int, Pen>();

        /// <summary>겹쳐보기에서 "이후" 쪽 선. 같은 계열의 다른 밝기로 구분합니다.</summary>
        /// <summary>
        /// 색이 <b>IO 를 가리키는지</b> (참) 아니면 <b>이전/이후를 가리키는지</b>
        /// (거짓).
        ///
        /// 레인으로 나눠 보면 IO 마다 칸이 따로 있으니 색으로 IO 를 가릴
        /// 필요가 없습니다 — 그때 색은 이전(파랑) / 이후(빨강)입니다.
        /// 겹쳐 보기에서 IO 가 여럿이면 거꾸로입니다. 모두 파랑·빨강으로
        /// 그리면 어느 선이 어느 IO 인지 알 수 없어서, 색이 IO 를 가리키고
        /// 이전/이후는 같은 색의 짙고 옅음으로 나뉩니다.
        ///
        /// <b>겹쳐 보기라도 IO 가 하나면 거짓입니다.</b> 가릴 IO 가 없으니
        /// 파랑·빨강이 더 잘 보입니다.
        ///
        /// 범례와 선 그리기가 <b>같은 답</b>을 봐야 합니다. 두 곳에 따로
        /// 적어 두면 범례가 "파랑은 이전" 이라고 하면서 선은 다른 색인 일이
        /// 생깁니다.
        /// </summary>
        private bool ColorByIo
        {
            get { return TraceColors.ByIo(_laneMode, _channels.Count); }
        }

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

        /// <summary>
        /// 그 자리에 그 로그 값이 없을 때 적는 글 (단계 칸 꼬리, 1.19).
        /// 글자는 Texts/GraphTexts.NoValue 에서 고칩니다.
        /// </summary>
        private const string NoValue = GraphTexts.NoValue;

        private string ValueReadout(IoRowVm vm)
        {
            if (double.IsNaN(_cursorA) || _state == null) return string.Empty;
            string s = string.Empty;
            if (vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                s += "이전 " + (InStep(_cursorA, true)
                    ? c.FormatValue(_state.Before.SampleAt(vm.BeforeIndex, _cursorA))
                    : NoValue);
            }
            if (vm.InAfter && _state.After != null)
            {
                if (s.Length > 0) s += "    ";
                Channel c = _state.After.Channels[vm.AfterIndex];
                // 커서가 든 칸의 밀기 값으로 읽습니다 (단계별로 나눠
                // 보는 중이면 칸마다 다릅니다).
                double t = _cursorA - ShiftAt(_cursorA);
                s += "이후 " + (InStep(_cursorA, false)
                    ? c.FormatValue(_state.After.SampleAt(vm.AfterIndex, t))
                    : NoValue);
            }
            return s + DiffNote(vm);
        }

        /// <summary>
        /// 차이 눈금일 때 값 뒤에 붙이는 "차이 +0.3".
        ///
        /// 이 눈금에서 화면에 그려진 선은 두 값이 아니라 <b>그 차이</b>입니다.
        /// 이전/이후 값만 적으면 커서가 가리키는 숫자와 선이 서로 다른 것을
        /// 말하게 되고, 차이만 적으면 값이 얼마인지 알 수 없어서 둘 다 적습니다.
        /// </summary>
        private string DiffNote(IoRowVm vm)
        {
            if (!Diffing || !CanDiff(vm)) return string.Empty;
            // 한쪽 단계가 끝난 자리에는 견줄 상대가 없습니다 (선도 안 그림).
            if (!InStep(_cursorA, true) || !InStep(_cursorA, false)) return string.Empty;

            double d = DifferenceSeries.At(_state.Before, vm.BeforeIndex,
                                           _state.After, vm.AfterIndex,
                                           ShiftAt(_cursorA), _cursorA);
            if (double.IsNaN(d)) return string.Empty;

            // 상태 채널은 0/1 이 "같다/다르다" 라는 뜻이라 숫자로 적으면 헷갈립니다.
            if (DifferenceSeries.ByName(_state.Before.Channels[vm.BeforeIndex],
                                        _state.After.Channels[vm.AfterIndex]))
                return "    " + (d == 0 ? "차이 없음" : "상태 다름");

            return "    차이 " + (d > 0 ? "+" : "") + NumberText.Plain(d);
        }

        /// <summary>
        /// 레인의 기준 세로 범위. 기본은 채널 전체 범위라서 시간축을 밀어도
        /// 세로 배율이 갑자기 풀리지 않습니다. "보이는 구간에 맞춤" 을 켰을
        /// 때만 지금 보이는 구간으로 다시 잡습니다. (요구사항 2번)
        /// </summary>
        private void BaseRange(IoRowVm[] ios, out double lo, out double hi)
        {
            bool any;
            BaseRange(ios, out lo, out hi, out any);
        }

        /// <summary>
        /// 레인의 기준 범위와, <b>그게 진짜 값에서 나온 것인지</b>.
        ///
        /// 쓸 만한 값이 하나도 없으면 0~1 을 돌려주되 <paramref name="usable"/>
        /// 을 거짓으로 둡니다. 그 0~1 은 "그릴 게 없어서 아무 눈금이나 그린
        /// 것" 이지 데이터가 아닙니다. 그걸 데이터인 줄 알고 세로 위치를
        /// 적어 두면, 나중에 값이 돌아와도 눈금이 0 언저리에 붙박여
        /// <b>IO 값이 0 으로 고정된 것처럼</b> 보입니다.
        /// </summary>
        private void BaseRange(IoRowVm[] ios, out double lo, out double hi, out bool usable)
        {
            double a = double.PositiveInfinity, b = double.NegativeInfinity;
            for (int k = 0; k < ios.Length; k++)
            {
                double clo, chi;
                if (!ChannelRange(ios[k], out clo, out chi)) continue;
                if (clo < a) a = clo;
                if (chi > b) b = chi;
            }
            usable = a <= b;
            if (!usable) { a = 0; b = 1; }

            // 차이는 두 로그가 같은 동안 계속 0 이라, 0 이 눈금에 들어 있지
            // 않으면 어디가 "차이 없음" 인지 알 수 없습니다.
            if (_scale == ValueScaleMode.Delta)
            {
                if (a > 0) a = 0;
                if (b < 0) b = 0;
            }

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

            // 차이 눈금은 두 로그를 함께 읽어야 나오는 값이라, 채널에 적어 둔
            // 최소/최대를 쓸 수가 없습니다. 대신 겹치는 구간을 훑습니다.
            //
            // "계속 맞춤" 이 꺼져 있으면 겹치는 구간 전체를 봅니다.
            // 그래야 시간축을 밀어도 세로 배율이 흔들리지 않는데, 그 계산은
            // 로그를 통째로 훑는 일이라 프레임마다 하면 못 씁니다. 그래서
            // 갈무리해 두고 로그나 밀기 값이 바뀔 때만 버립니다.
            if (Diffing)
            {
                if (!CanDiff(vm)) return false;
                if (_fitVisible)
                {
                    // 단계별로 나눠 보는 중이면 <b>칸들만</b> 모아 봅니다.
                    // 칸 사이의 빈틈은 그리지 않는 자리라, 거기 값까지
                    // 넣으면 보이지도 않는 값이 눈금을 벌려 놓습니다.
                    bool got = false;
                    foreach (Span sp in Spans())
                    {
                        if (!DifferenceSeries.RangeIn(_state.Before, vm.BeforeIndex,
                                                      _state.After, vm.AfterIndex,
                                                      sp.Shift, sp.T0, sp.T1, out x0, out x1)) continue;
                        lo = got ? Math.Min(lo, x0) : x0;
                        hi = got ? Math.Max(hi, x1) : x1;
                        got = true;
                    }
                    return got;
                }
                return CachedDiffRange(vm, out lo, out hi);
            }

            if (_fitVisible)
            {
                foreach (Span sp in Spans())
                {
                    if (ShownB && vm.InBefore && _state.Before != null
                        && Decimator.RangeIn(_state.Before, vm.BeforeIndex, sp.T0, sp.T1, out x0, out x1))
                    { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                    if (ShownA && vm.InAfter && _state.After != null
                        && Decimator.RangeIn(_state.After, vm.AfterIndex,
                            sp.T0 - sp.Shift, sp.T1 - sp.Shift, out x0, out x1))
                    { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                }
                if (any) return true;
            }

            if (ShownB && vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            if (ShownA && vm.InAfter && _state.After != null)
            {
                Channel c = _state.After.Channels[vm.AfterIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            return any;
        }

        /// <summary>
        /// 값 범위를 잴 구간 하나. 평소에는 보이는 구간 하나뿐이고,
        /// 단계별로 나눠 보는 중에는 칸마다 하나씩입니다 (밀기 값도 칸마다
        /// 다릅니다).
        /// </summary>
        private struct Span
        {
            public double T0;
            public double T1;
            public double Shift;
        }

        /// <summary>
        /// 지금 화면이 실제로 보여 주는 구간들. <b>값 범위를 재는 코드가
        /// 한 줄 시간이든 단계 칸이든 같은 모양으로 돌게</b> 하려고 냅니다 —
        /// 두 갈래로 적어 두면 한쪽만 고치는 날이 옵니다.
        /// </summary>
        private IEnumerable<Span> Spans()
        {
            if (StepMode)
            {
                for (int i = 0; i < _stepBands.Count; i++)
                {
                    StepBand b = _stepBands[i];
                    var sp = new Span();
                    sp.T0 = b.T0; sp.T1 = b.T1; sp.Shift = b.Shift;
                    yield return sp;
                }
                yield break;
            }

            var one = new Span();
            one.T0 = _t0; one.T1 = _t1;
            one.Shift = _state != null ? _state.AppliedShift : 0.0;
            yield return one;
        }

        // 겹치는 구간 전체의 차이 폭. IO 마다 한 번만 재고 들고 있습니다.
        private readonly Dictionary<string, double[]> _diffRange =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private double _diffRangeShift = double.NaN;

        private bool CachedDiffRange(IoRowVm vm, out double lo, out double hi)
        {
            lo = 0; hi = 1;

            // 단계별로 나눠 보는 중이면 밀기 값이 칸마다 다릅니다. 갈무리를
            // 가르는 열쇠로 NaN 을 씁니다 (칸 목록이 바뀌면 StepBands 가
            // 갈무리를 버립니다).
            double shift = StepMode ? double.NaN : _state.AppliedShift;

            // 밀기 값이 바뀌면 겹치는 자리가 통째로 달라집니다. 옛 값을 그대로
            // 쓰면 맞추기를 바꿔도 세로 눈금이 안 따라옵니다.
            bool same = double.IsNaN(shift) ? double.IsNaN(_diffRangeShift) : _diffRangeShift == shift;
            if (!same) { _diffRange.Clear(); _diffRangeShift = shift; }

            double[] got;
            if (!_diffRange.TryGetValue(vm.Name, out got))
            {
                double a = 0, b = 1;
                bool any2 = false;
                foreach (Span sp in Spans())
                {
                    // 평소에는 보이는 구간이 아니라 <b>겹치는 구간 전체</b>를
                    // 봅니다 (시간축을 밀어도 세로 배율이 안 흔들리게).
                    // 단계 칸은 그 칸이 곧 보는 구간입니다.
                    double t0 = StepMode ? sp.T0
                        : Math.Max(_state.Before.TimeStart, _state.After.TimeStart + sp.Shift);
                    double t1 = StepMode ? sp.T1
                        : Math.Min(_state.Before.TimeEnd, _state.After.TimeEnd + sp.Shift);

                    double x0, x1;
                    if (!DifferenceSeries.RangeIn(_state.Before, vm.BeforeIndex,
                                                  _state.After, vm.AfterIndex,
                                                  sp.Shift, t0, t1, out x0, out x1)) continue;
                    a = any2 ? Math.Min(a, x0) : x0;
                    b = any2 ? Math.Max(b, x1) : x1;
                    any2 = true;
                }
                got = any2 ? new double[] { a, b } : null;
                _diffRange[vm.Name] = got;
            }
            if (got == null) return false;
            lo = got[0]; hi = got[1];
            return true;
        }

        /// <summary>
        /// 지금 차이(이후−이전) 눈금인지.
        ///
        /// 이 눈금에서는 선이 <b>하나</b>입니다. 이전과 이후를 각각 그리는
        /// 대신 "이후 − 이전" 을 그립니다. 그래서 값 하나를 바꿔 주는
        /// 함수로는 처리할 수 없습니다 — 두 로그를 같은 시각에서 함께 읽어야
        /// 나오는 값이라, 접기 · 범위 · 그리기가 모두 다른 길로 갑니다.
        /// </summary>
        private bool Diffing { get { return _scale == ValueScaleMode.Delta; } }

        /// <summary>
        /// 이 IO 를 차이로 그릴 수 있는지. 한쪽 로그에만 있으면 견줄 상대가
        /// 없습니다. 그 레인은 비워 두고, 왜 비었는지는 레인 머리글의
        /// "이전에만 / 이후에만" 이 말해 줍니다.
        /// </summary>
        private bool CanDiff(IoRowVm vm)
        {
            return _state != null && vm != null
                && vm.InBefore && vm.InAfter
                && _state.Before != null && _state.After != null;
        }
    

        // ======================================================================
        //  GraphCanvas.Trace.cs — 1.50 에 이 파일로 합침
        // ======================================================================
        // <summary>
        // 그래프 판 — <b>선 하나</b>. 표본을 픽셀 열로 접고(Decimator),
        // StreamGeometry 한 덩어리로 만들어 한 번에 넘깁니다.
        //
        // 여기가 로그 10M 칸을 버티는 자리입니다. 고칠 때는 "선 하나에
        // DrawLine 을 몇 번 부르는가" 를 먼저 보세요.
        // </summary>


        private void DrawChannel(DrawingContext dc, Palette p, Rect inner, IoRowVm vm,
                                 double vlo, double vhi, Pen beforePen, Pen afterPen, int columns)
        {
            if (_state == null || inner.Width <= 1) return;

            bool haveB = ShownB && vm.InBefore && _state.Before != null && _sideOnly != 2;
            bool haveA = ShownA && vm.InAfter && _state.After != null && _sideOnly != 1;

            double shift = ShiftNow;   // 단계 칸을 그리는 중이면 그 칸의 값
            double bt0 = _t0, bt1 = _t1;                  // 이전 로그의 시간 기준
            double at0 = _t0 - shift, at1 = _t1 - shift;  // 이후 로그의 시간 기준

            // 한쪽만 켜 두었으면 벌리지 않습니다. 벌릴 상대가 없는데 비켜
            // 그리면 그 선만 눈금에서 어긋나 보입니다.
            double sep = (_separate && haveB && haveA) ? inner.Height * 0.02 : 0.0;

            // ---- 차이 눈금: 선 하나 -------------------------------------
            if (Diffing)
            {
                // 한쪽 로그만 남은 자리(단계 칸 꼬리)에는 차이가 없습니다.
                if (_sideOnly != 0) return;
                if (!CanDiff(vm)) return;     // 한쪽에만 있는 IO 는 견줄 상대가 없습니다.

                if (_shade)
                {
                    // 칠하려면 양쪽 값이 같은 자리에서 하나씩 있어야 합니다.
                    Decimator.SampleColumns(_state.Before, vm.BeforeIndex, bt0, bt1, _bandBefore, columns);
                    Decimator.SampleColumns(_state.After, vm.AfterIndex, at0, at1, _bandAfter, columns);
                    ShadeGap(dc, p, inner, columns, vlo, vhi, 0);
                }

                DrawDifference(dc, p, inner, vm, bt0, bt1, shift, columns, vlo, vhi);
                return;
            }

            if (_shade && haveB && haveA && _sideOnly == 0)
            {
                // 칠하려면 양쪽 값이 같은 자리에서 하나씩 있어야 합니다.
                // 접은 값(최소/최대)이 아니라 열마다 한 값씩 뽑아 씁니다.
                Decimator.SampleColumns(_state.Before, vm.BeforeIndex, bt0, bt1, _bandBefore, columns);
                Decimator.SampleColumns(_state.After, vm.AfterIndex, at0, at1, _bandAfter, columns);
                ShadeGap(dc, p, inner, columns, vlo, vhi, sep);
            }

            if (haveB) DrawSide(dc, inner, _state.Before, vm.BeforeIndex, bt0, bt1,
                                _colsBefore, columns, vlo, vhi, beforePen, -sep);
            if (haveA) DrawSide(dc, inner, _state.After, vm.AfterIndex, at0, at1,
                                _colsAfter, columns, vlo, vhi, afterPen, +sep);
        }

        /// <summary>
        /// 두 로그의 차이 선 하나. "이후 − 이전" 입니다.
        ///
        /// 한쪽 로그만 그릴 때(DrawSide)와 갈림길이 같습니다 — 표본이 픽셀보다
        /// 성기면 그대로 잇고, 촘촘하면 열마다 최소/최대만 뽑아 그립니다.
        /// 다만 두 로그의 시간 격자가 서로 달라서 표본 개수를 미리 알 수 없으니,
        /// 한 번 훑으면서 둘 다 채우고 넘치면 접은 쪽을 씁니다.
        /// </summary>
        private void DrawDifference(DrawingContext dc, Palette p, Rect inner, IoRowVm vm,
                                    double t0, double t1, double shift, int columns,
                                    double vlo, double vhi)
        {
            int count = DifferenceSeries.Build(_state.Before, vm.BeforeIndex,
                                               _state.After, vm.AfterIndex,
                                               shift, t0, t1,
                                               _colsDiff, columns,
                                               _diffT, _diffV, _diffT.Length);

            // 0 선. 어디가 "차이 없음" 인지 눈에 보여야 합니다.
            double zero = ValueToY(0, inner, vlo, vhi);
            if (zero >= inner.Top && zero <= inner.Bottom)
            {
                dc.DrawLine(p.GridPen, new Point(inner.Left, Snap(zero)),
                                       new Point(inner.Right, Snap(zero)));
            }

            Pen pen = p.DiffPen;
            if (count < 0)
            {
                DrawColumns(dc, inner, _colsDiff, columns, vlo, vhi, pen, 0);
                return;
            }
            if (count == 0) return;

            // 상태 채널의 차이는 0/1 이라 계단으로 그립니다. 그 사이에
            // "반쯤 다름" 같은 값은 없습니다.
            bool stepped = DifferenceSeries.ByName(_state.Before.Channels[vm.BeforeIndex],
                                                   _state.After.Channels[vm.AfterIndex]);

            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                _pts.Clear();
                double prevY = 0;
                for (int i = 0; i < count; i++)
                {
                    double x = TimeToXIn(inner, _diffT[i], t0, t1);
                    double y = Clamp(ValueToY(_diffV[i], inner, vlo, vhi), top, bottom);

                    if (i == 0) { ctx.BeginFigure(new Point(x, y), false, false); }
                    else
                    {
                        if (stepped) _pts.Add(new Point(x, prevY));
                        _pts.Add(new Point(x, y));
                    }
                    prevY = y;
                }
                if (_pts.Count > 0) ctx.PolyLineTo(_pts, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        /// <summary>
        /// 한쪽 로그의 선 하나. 표본이 픽셀보다 촘촘한지 성긴지에 따라
        /// 그리는 방식을 바꿉니다.
        ///
        /// 촘촘하면 열마다 최소/최대만 뽑아 그립니다(Decimator). 그러지 않으면
        /// 한 픽셀 폭에 수천 번 선을 긋게 됩니다.
        ///
        /// 성기면 <b>표본을 그대로</b> 잇습니다. 예전에는 이때도 접어서 그렸는데,
        /// 표본이 없는 열을 "값이 끊긴 자리" 로 보고 선을 끊는 바람에 표본마다
        /// 점 하나짜리 도형이 되어 <b>아무것도 안 보였습니다</b>. 확대할수록
        /// 심해지던 그 증상이 이것입니다.
        /// </summary>
        private void DrawSide(DrawingContext dc, Rect inner, LogDataset ds, int ch,
                              double t0, double t1, Decimator.Column[] cols, int columns,
                              double vlo, double vhi, Pen pen, double dy)
        {
            int first, last;
            if (!Decimator.VisibleRange(ds, t0, t1, out first, out last)) return;

            long visible = (long)last - first + 1;
            if (visible <= columns)
                DrawSamples(dc, inner, ds, ch, first, last, t0, t1, vlo, vhi, pen, dy);
            else
            {
                Decimator.Build(ds, ch, t0, t1, cols, columns);
                DrawColumns(dc, inner, cols, columns, vlo, vhi, pen, dy);
            }
        }

        /// <summary>
        /// 표본을 그대로 잇습니다. 디지털과 상태 채널은 계단으로 그립니다 —
        /// 값이 다음 표본까지 그대로 유지되다가 거기서 한 번에 바뀌는 것이므로,
        /// 비스듬한 선으로 이으면 없던 중간값을 그린 셈이 됩니다.
        ///
        /// </summary>
        private void DrawSamples(DrawingContext dc, Rect inner, LogDataset ds, int ch,
                                 int first, int last, double t0, double t1,
                                 double vlo, double vhi, Pen pen, double dy)
        {
            Channel c = ds.Channels[ch];
            float[] v = c.Values;
            double[] times = ds.Times;
            bool stepped = c.IsStepped;

            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();

            using (StreamGeometryContext ctx = geo.Open())
            {
                bool open = false;
                _pts.Clear();

                for (int i = first; i <= last; i++)
                {
                    float value = v[i];
                    if (float.IsNaN(value)) { if (open) Flush(ctx, ref open); continue; }

                    double x = TimeToXIn(inner, times[i], t0, t1);
                    double y = Clamp(ValueToY(value, inner, vlo, vhi) + dy,
                                     top, bottom);

                    if (!open) { ctx.BeginFigure(new Point(x, y), false, false); open = true; }
                    else _pts.Add(new Point(x, y));

                    // 계단: 다음 표본 시각까지 이 값을 그대로 끌고 갑니다.
                    // 그 다음 바퀴가 세로 변을 그립니다.
                    if (stepped && i < last && !float.IsNaN(v[i + 1]))
                        _pts.Add(new Point(TimeToXIn(inner, times[i + 1], t0, t1), y));
                }
                if (open) Flush(ctx, ref open);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        /// <summary>
        /// 접은 열을 그립니다.
        ///
        /// 한 열은 가로로 1 픽셀이라 그 열의 점들은 <b>x 가 전부 같습니다.</b>
        /// 결국 최소~최대를 잇는 세로 선 하나입니다. 그래서 위끝과 아래끝
        /// <b>두 점</b>이면 충분합니다. 예전에는 첫값과 끝값까지 네 점을
        /// 찍었는데, 어차피 같은 세로 선 위에 겹쳐 찍히는 점이라 화면에는
        /// 아무 차이가 없으면서 점 개수만 두 배였습니다. 0.1 초마다 값이
        /// 바뀌는 채널처럼 모든 열이 위아래로 꽉 찬 경우에 특히 무거웠습니다.
        ///
        /// 두 점을 찍는 <b>차례</b>는 직전 열이 끝난 높이에 맞춥니다. 가까운
        /// 쪽부터 찍어야 열과 열 사이를 잇는 선이 쓸데없이 가로지르지 않습니다.
        ///
        /// 여기서 값이 없는 열은 정말로 기록이 끊긴 자리입니다 — 표본이 픽셀보다
        /// 촘촘한 경우에만 이 길로 오기 때문입니다.
        /// </summary>
        private void DrawColumns(DrawingContext dc, Rect inner, Decimator.Column[] cols, int columns,
                                 double vlo, double vhi, Pen pen, double dy)
        {
            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();

            using (StreamGeometryContext ctx = geo.Open())
            {
                bool open = false;
                double prevY = double.NaN;
                _pts.Clear();

                for (int x = 0; x < columns; x++)
                {
                    Decimator.Column c = cols[x];
                    if (!c.HasValue) { if (open) Flush(ctx, ref open); prevY = double.NaN; continue; }

                    double px = ColumnToX(inner, x);
                    // 화면 y 는 값이 클수록 작아집니다. yTop 이 c.Max, yBottom 이 c.Min.
                    double yTop = Clamp(ValueToY(c.Max, inner, vlo, vhi) + dy, top, bottom);
                    double yBottom = Clamp(ValueToY(c.Min, inner, vlo, vhi) + dy, top, bottom);

                    // 직전 열이 끝난 높이에서 가까운 쪽부터 찍습니다.
                    double enter = yTop, leave = yBottom;
                    if (!double.IsNaN(prevY) && Math.Abs(prevY - yBottom) < Math.Abs(prevY - yTop))
                    {
                        enter = yBottom; leave = yTop;
                    }

                    if (!open) { ctx.BeginFigure(new Point(px, enter), false, false); open = true; }
                    else _pts.Add(new Point(px, enter));

                    if (leave != enter) _pts.Add(new Point(px, leave));
                    prevY = leave;
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
        /// 열마다 뽑아 둔 값(_bandBefore / _bandAfter)을 씁니다. 접은 값이
        /// 아니라 "그 시각의 값" 이라, 확대해서 표본이 성겨져도 띠가 끊기지
        /// 않습니다.
        ///
        /// 1 픽셀짜리 네모를 열마다 하나씩 담되, 전부 한 도형에 모아 한 번에
        /// 칠합니다. 붙어 있는 네모들이 모여 자연스러운 띠가 됩니다.
        ///
        /// 그 안쪽 차이는 칠하지 않습니다 — 대시보드가 "차이 없음" 으로 세는
        /// 것과 같은 기준이라야 화면끼리 말이 맞습니다.
        /// </summary>
        /// <summary>
        /// 이전/이후가 벌어진 구간을 칠합니다.
        ///
        /// 예전에는 <b>픽셀 열마다 닫힌 사각형을 하나씩</b> 만들었습니다.
        /// 값이 잔잔한 채널은 칠할 열이 몇 개 안 되니 티가 안 났는데,
        /// 0.1 초마다 값이 바뀌는 채널처럼 거의 모든 열이 벌어져 있으면
        /// 도형이 열 수만큼(2000~3500 개) 생깁니다. WPF 는 닫힌 도형마다
        /// 따로 삼각형으로 쪼개므로, 이게 프레임마다 반복되면서 화면이
        /// 눈에 띄게 굼떠졌습니다.
        ///
        /// 이제 <b>이어져 있는 열들을 한 덩어리로 묶어</b> 폴리곤 하나로
        /// 칠합니다. 위쪽 경계를 왼쪽에서 오른쪽으로 따라간 뒤, 아래쪽
        /// 경계를 오른쪽에서 왼쪽으로 되짚어 닫습니다. 점 개수는 비슷하지만
        /// 도형 개수가 수천 개에서 몇 개로 줄어듭니다.
        /// </summary>
        private void ShadeGap(DrawingContext dc, Palette p, Rect inner, int columns,
                              double vlo, double vhi, double sep)
        {
            var geo = new StreamGeometry();
            bool any = false;
            double width = 1.0 / _pixelsPerDip;   // 한 열의 가로 폭 (DIP)

            using (StreamGeometryContext ctx = geo.Open())
            {
                int x = 0;
                while (x < columns)
                {
                    if (!Differs(x)) { x++; continue; }

                    _edgeTop.Clear();
                    _edgeBottom.Clear();
                    double lastPx = 0;

                    while (x < columns && Differs(x))
                    {
                        // 차이 눈금에서는 선이 하나라 그 선과 0 선 사이를 칠합니다.
                        // 값 눈금에서는 이전 선과 이후 선 사이입니다.
                        double y1, y2;
                        if (Diffing)
                        {
                            y1 = ValueToY(_bandAfter[x] - _bandBefore[x], inner, vlo, vhi);
                            y2 = ValueToY(0, inner, vlo, vhi);
                        }
                        else
                        {
                            y1 = ValueToY(_bandBefore[x], inner, vlo, vhi) - sep;
                            y2 = ValueToY(_bandAfter[x], inner, vlo, vhi) + sep;
                        }
                        double t = Clamp(Math.Min(y1, y2), inner.Top, inner.Bottom);
                        double b = Clamp(Math.Max(y1, y2), inner.Top, inner.Bottom);
                        if (b - t < 1) b = t + 1;

                        lastPx = ColumnToX(inner, x);
                        _edgeTop.Add(new Point(lastPx, t));
                        _edgeBottom.Add(new Point(lastPx, b));
                        x++;
                    }

                    // 마지막 열의 오른쪽 변까지 덮습니다.
                    _edgeTop.Add(new Point(lastPx + width, _edgeTop[_edgeTop.Count - 1].Y));
                    _edgeBottom.Add(new Point(lastPx + width, _edgeBottom[_edgeBottom.Count - 1].Y));
                    _edgeBottom.Reverse();

                    ctx.BeginFigure(_edgeTop[0], true, true);
                    ctx.PolyLineTo(_edgeTop, false, false);
                    ctx.PolyLineTo(_edgeBottom, false, false);
                    any = true;
                }
            }

            _edgeTop.Clear();
            _edgeBottom.Clear();
            if (!any) return;

            geo.Freeze();
            dc.DrawGeometry(p.DiffBrush, null, geo);
        }

        /// <summary>
        /// 그 열에서 두 로그가 허용 오차를 넘어 벌어졌는지.
        /// 대시보드·히트맵과 같은 함수를 씁니다.
        /// </summary>
        private bool Differs(int x)
        {
            return ToleranceRule.IsOver(_bandBefore[x], _bandAfter[x],
                                        _absoluteTolerance, _relativePercent);
        }
    

        // ======================================================================
        //  GraphCanvas.Axis.cs — 1.50 에 이 파일로 합침
        // ======================================================================
        // <summary>
        // 그래프 판 — <b>눈금</b>. 세로 눈금(값)과 가로 눈금(시간), 그리고
        // 커서 줄입니다.
        //
        // 눈금 글자는 선을 그을 때 쓰는 것과 똑같은 ValueToY/TimeToX 를
        // 거칩니다. 그래서 숫자와 그림이 어긋날 수 없습니다.
        // </summary>


        // ---------------- 눈금 ----------------

        /// <summary>
        /// 세로 눈금.
        ///
        /// 눈금 글자는 선을 그은 바로 그 y 에 붙입니다. 그래서 숫자와 그래프
        /// 높이가 어긋날 수 없습니다. (요구사항 2번)
        ///
        /// 채널 종류에 따라 눈금을 다르게 답니다.
        ///  - 상태 채널: 숫자 대신 <b>상태 이름</b>. 값이 상태 표의 번호라서
        ///    0, 1, 2 를 적어 봐야 아무 뜻이 없습니다.
        ///  - 디지털 채널: 0 과 1 에만. 0.5 같은 눈금은 있을 수 없는 값입니다.
        ///  - 그 밖: 보기 좋은 간격으로 숫자.
        /// </summary>
        private void DrawValueAxis(DrawingContext dc, Palette p, Rect lane, Rect inner,
                                   double vlo, double vhi, IoRowVm[] ios)
        {
            DrawAxisCaption(dc, p, lane, ios);

            Channel single = SingleChannel(ios);

            // 눈금이 하나도 안 걸릴 만큼 확대했을 수 있습니다. 그러면 눈금선이
            // 통째로 사라져 버리니, 아래의 보통 숫자 눈금으로 넘어갑니다.
            if (_scale == ValueScaleMode.Raw && single != null)
            {
                if (single.Kind == ChannelKind.State && single.States != null
                    && single.States.Length > 0 && single.States.Length <= 12)
                {
                    if (DrawStateTicks(dc, p, inner, vlo, vhi, single)) return;
                }
                else if (single.Kind == ChannelKind.Digital)
                {
                    int drawn = 0;
                    if (DrawTick(dc, p, inner, vlo, vhi, 0, "0")) drawn++;
                    if (DrawTick(dc, p, inner, vlo, vhi, 1, "1")) drawn++;
                    if (drawn > 0) return;
                }
            }

            double step = NiceStep(vhi - vlo, Math.Max(2, (int)(inner.Height / 34)));
            if (!(step > 0)) return;

            double first = Math.Ceiling(vlo / step) * step;
            for (double v = first; v <= vhi + step * 0.001; v += step)
                DrawTick(dc, p, inner, vlo, vhi, v, FormatTick(v, step));
        }

        /// <summary>레인에 채널이 하나일 때 그 채널. 겹쳐보기면 null.</summary>
        private Channel SingleChannel(IoRowVm[] ios)
        {
            if (_state == null || ios == null || ios.Length != 1) return null;
            IoRowVm vm = ios[0];
            if (vm.InBefore && _state.Before != null) return _state.Before.Channels[vm.BeforeIndex];
            if (vm.InAfter && _state.After != null) return _state.After.Channels[vm.AfterIndex];
            return null;
        }

        /// <summary>눈금 하나. 보이는 자리가 아니면 그리지 않고 거짓을 돌려줍니다.</summary>
        private bool DrawTick(DrawingContext dc, Palette p, Rect inner,
                              double vlo, double vhi, double value, string label)
        {
            double y = ValueToY(value, inner, vlo, vhi);
            if (y < inner.Top - 1 || y > inner.Bottom + 1) return false;

            double gy = Snap(y);
            dc.DrawLine(p.GridPen, new Point(inner.Left + 1, gy), new Point(inner.Right - 2, gy));

            FormattedText ft = Text(label, FontSmall, p.MutedBrush);
            ft.MaxTextWidth = GutterW - 10;
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            // 글자가 레인 밖으로 삐져나가지 않게 가둡니다.
            double ty = gy - ft.Height * 0.5;
            if (ty < inner.Top) ty = inner.Top;
            if (ty + ft.Height > inner.Bottom) ty = inner.Bottom - ft.Height;

            dc.DrawText(ft, new Point(Math.Max(2, GutterW - 8 - ft.Width), ty));
            return true;
        }

        /// <summary>상태 이름을 눈금 글자로. 하나라도 그렸으면 참.</summary>
        private bool DrawStateTicks(DrawingContext dc, Palette p, Rect inner,
                                    double vlo, double vhi, Channel c)
        {
            int drawn = 0;
            for (int i = 0; i < c.States.Length; i++)
                if (DrawTick(dc, p, inner, vlo, vhi, i, c.States[i])) drawn++;
            return drawn > 0;
        }

        /// <summary>
        /// 세로축이 무엇을 뜻하는지 레인 왼쪽 위에 적습니다.
        /// 단위가 있으면 단위를, 눈금 모드가 기존 값이 아니면 그 모드를 적습니다.
        /// 숫자만 늘어놓으면 무슨 값인지 알 수 없어서 붙였습니다.
        /// </summary>
        private void DrawAxisCaption(DrawingContext dc, Palette p, Rect lane, IoRowVm[] ios)
        {
            string caption = AxisCaption(ios);
            if (caption.Length == 0) return;

            FormattedText ft = Text(caption, FontSmall, p.MutedBrush);
            ft.MaxTextWidth = GutterW - 10;
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            // 눈금 숫자와 오른쪽 끝을 맞춥니다. 레인 머리글(채널 이름)은 이
            // 자리보다 오른쪽, 눈금 글자는 이 자리보다 아래라 겹치지 않습니다.
            dc.DrawText(ft, new Point(Math.Max(2, GutterW - 8 - ft.Width), lane.Y + 4));
        }

        private string AxisCaption(IoRowVm[] ios)
        {
            switch (_scale)
            {
                case ValueScaleMode.Delta:
                    {
                        // Δ 는 "이후 − 이전" 이라는 뜻입니다. 눈금 칸이 좁아서
                        // 그걸 다 적을 수 없으니 기호로 줄이고, 자세한 것은
                        // 도구 줄의 풍선 도움말이 말해 줍니다.
                        string u = UnitOf(ios);
                        return u.Length > 0 ? "Δ " + u : "차이";
                    }
                default:
                    {
                        string u = UnitOf(ios);
                        if (u.Length > 0) return u;
                        Channel single = SingleChannel(ios);
                        if (single != null && single.Kind == ChannelKind.State) return "상태";
                        return ios != null && ios.Length > 1 ? "여러 채널" : string.Empty;
                    }
            }
        }

        /// <summary>
        /// 레인의 단위. 여러 채널이 한 눈금을 쓰는데 단위가 서로 다르면,
        /// 어느 하나를 적으면 거짓말이 되므로 아무것도 적지 않습니다.
        /// </summary>
        private string UnitOf(IoRowVm[] ios)
        {
            if (_state == null || ios == null || ios.Length == 0) return string.Empty;

            string unit = null;
            for (int k = 0; k < ios.Length; k++)
            {
                IoRowVm vm = ios[k];
                string u = string.Empty;
                if (vm.InBefore && _state.Before != null) u = _state.Before.Channels[vm.BeforeIndex].Unit;
                if (u.Length == 0 && vm.InAfter && _state.After != null) u = _state.After.Channels[vm.AfterIndex].Unit;
                if (u.Length == 0) continue;

                if (unit == null) unit = u;
                else if (!string.Equals(unit, u, StringComparison.Ordinal)) return string.Empty;
            }
            return unit ?? string.Empty;
        }

        private static string FormatTick(double v, double step)
        {
            if (Math.Abs(v) < step * 1e-9) v = 0;

            // 눈금 간격에 맞춰 소수점 자리를 정합니다. 지수 표기로는 절대
            // 바뀌지 않습니다 — 눈금에 "1.2e+06" 이 적히면 읽을 수가 없습니다.
            double a = Math.Abs(v);
            if (a >= 1e6) return NumberText.Plain(v);
            if (a != 0 && a < 1e-3) return NumberText.Plain(v);

            int dec = 0;
            double st = Math.Abs(step);
            while (st < 1 && dec < 6) { st *= 10; dec++; }
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

            if (StepMode) { DrawStepAxis(dc, p, plot, strip); return; }

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

                double gx = Snap(x);
                dc.DrawLine(p.GridPen, new Point(gx, plot.Top), new Point(gx, plot.Bottom));
                dc.DrawLine(p.BorderPen, new Point(gx, strip.Top), new Point(gx, strip.Top + 4));

                string label = TimeLabel(refDs, t);
                FormattedText ft = Text(label, FontSmall, p.MutedBrush);
                dc.DrawText(ft, new Point(x - ft.Width * 0.5, strip.Top + 5));
            }
        }

        /// <summary>
        /// 단계별로 나눠 볼 때의 가로 눈금. 칸마다 <b>단계 이름</b>을 적고,
        /// 칸이 넓으면 그 아래에 구간도 적습니다.
        ///
        /// 시각 눈금을 칸마다 몇 개씩 적으면 글자가 서로 붙어 버립니다.
        /// 단계로 나눠 본다는 것은 "몇 시 몇 분" 이 아니라 "어느 단계" 로
        /// 보겠다는 뜻이라, 이름이 먼저입니다.
        /// </summary>
        private void DrawStepAxis(DrawingContext dc, Palette p, Rect plot, Rect strip)
        {
            double[] xs, ws;
            StepLayout(plot, out xs, out ws);
            LogDataset refDs = _state != null ? _state.TimeReference : null;

            dc.PushClip(new RectangleGeometry(strip));
            for (int i = 0; i < _stepBands.Count; i++)
            {
                double x = xs[i];
                double width = ws[i];

                // 칸 밑에 테두리를 둘러 그림 쪽 칸과 같은 자리로 보이게 합니다.
                dc.DrawLine(p.BorderPen, new Point(Snap(x), strip.Top),
                                         new Point(Snap(x), strip.Top + 4));
                dc.DrawLine(p.BorderPen, new Point(Snap(x + width), strip.Top),
                                         new Point(Snap(x + width), strip.Top + 4));

                // 단계 이름은 <b>위</b> 칸 머리 줄에 적습니다 (1.16,
                // DrawStepHead). 여기 아래 줄에는 그 칸이 지금 보여 주는
                // 구간만 적습니다 — 칸을 확대하면 단계 전체가 아니라 그
                // 일부를 보고 있으니, 단계 구간을 적어 두면 거짓이 됩니다.
                double a0, a1;
                StepView(i, out a0, out a1);
                string when = refDs != null
                    ? refDs.FormatTime(a0) + " ~ " + refDs.FormatTime(a1)
                    : NumberText.Plain(a0) + " ~ " + NumberText.Plain(a1);
                FormattedText ft = Text(when, FontSmall, p.MutedBrush);
                if (ft.Width + 6 <= width)
                    dc.DrawText(ft, new Point(x + (width - ft.Width) * 0.5, strip.Top + 5));
            }
            dc.Pop();
        }

        /// <summary>
        /// 단계로 나눠 볼 때 그림 <b>위</b>의 칸 머리 줄 (1.16). 칸마다 단계
        /// 이름을 적고, <b>누르면 그 단계만</b> 봅니다 (다시 누르면 모든 칸).
        ///
        /// 1.15 까지는 이름이 아래 시간 줄에 있었습니다. 위에 있어야 칸을
        /// 내려다보기 전에 "이 칸이 어느 단계인가" 가 먼저 읽히고, 누르는
        /// 자리(머리)와 그림이 겹치지 않습니다.
        ///
        /// 한 단계만 보는 중이면 머리 끝에 "◀ 전체" 를 붙여, 다시 누르면
        /// 돌아간다는 것을 알려 줍니다.
        /// </summary>
        private void DrawStepHead(DrawingContext dc, Palette p, Rect plot)
        {
            var strip = new Rect(0, 0, ActualWidth, StepHeadH);
            dc.DrawRectangle(p.AxisStripBrush, null, strip);
            dc.DrawLine(p.BorderPen, new Point(0, StepHeadH - 0.5), new Point(ActualWidth, StepHeadH - 0.5));

            double[] xs, ws;
            StepLayout(plot, out xs, out ws);
            // 한 단계만 고른 상태인지 — 칸 수가 아니라 고른 것으로 봅니다
            // (단계가 원래 하나뿐인 로그에서 "◀ 전체" 가 뜨면 안 됩니다).
            bool single = _state != null && _state.StepPick >= 0;

            dc.PushClip(new RectangleGeometry(strip));
            for (int i = 0; i < _stepBands.Count; i++)
            {
                double x = xs[i], width = ws[i];
                var cell = new Rect(x, 2, Math.Max(1, width), StepHeadH - 5);
                dc.DrawRectangle(Frozen(p.Window), p.BorderPen, cell);

                string label = _stepBands[i].Label + (single ? "   ◀ 전체" : "");
                FormattedText name = Text(label, FontSmall, p.TextBrush);
                name.SetFontWeight(FontWeights.SemiBold);
                if (name.Width + 6 > width)
                {
                    // 칸이 좁으면 번호만. 이름을 잘라 적으면 옆 칸 글자와
                    // 붙어 한 낱말처럼 읽힙니다.
                    name = Text((i + 1).ToString(CultureInfo.InvariantCulture), FontSmall, p.TextBrush);
                    if (name.Width + 2 > width) continue;
                }
                dc.DrawText(name, new Point(x + (width - name.Width) * 0.5,
                                            cell.Top + (cell.Height - name.Height) * 0.5));
            }
            dc.Pop();
        }

        private void DrawCursor(DrawingContext dc, Palette p, Rect plot, double t, Pen pen, Color color, string tag)
        {
            if (double.IsNaN(t)) return;
            if (!StepMode && (t < _t0 || t > _t1)) return;

            double xx = TimeToX(t);
            // 단계별로 나눠 보는 중에는 칸 밖의 시각이 있습니다 (빈틈).
            // 그 커서는 그릴 자리가 없습니다.
            if (double.IsNaN(xx)) return;
            double x = Snap(xx);
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));

            var box = new Rect(x - 9, plot.Top + 1, 18, 15);
            dc.DrawRectangle(Frozen(color), null, box);

            FormattedText ft = Text(tag, FontSmall, Brushes.White);
            ft.SetFontWeight(FontWeights.Bold);
            dc.DrawText(ft, new Point(box.Left + (box.Width - ft.Width) * 0.5,
                                      box.Top + (box.Height - ft.Height) * 0.5));
        }
    

        // ======================================================================
        //  GraphCanvas.Input.cs — 1.50 에 이 파일로 합침
        // ======================================================================
        // <summary>
        // 그래프 판 — <b>마우스와 키보드</b>. 휠 확대, 끌어 이동, 오른쪽
        // 끌기로 네모 확대, 커서 A·B 찍기, 방향키.
        //
        // 여기서는 그리지 않습니다 — 보이는 구간과 커서만 고치고
        // InvalidateVisual 로 다시 그리게 합니다.
        // </summary>


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
            else if (StepMode)
            {
                // 커서를 올려 둔 <b>그 칸만</b> 확대합니다. 다른 칸은 그대로
                // 남아야 견줄 바탕이 됩니다.
                //
                // 칸 위가 아니면 (칸 사이의 틈 · 왼쪽 눈금 자리 · 아래 눈금
                // 띠) <b>모든 칸을 같이</b> 확대합니다. 전에는 아무 일도
                // 안 일어났는데, 틈이 10 px 이라 거기서 굴린 사람은 확대가
                // 안 되는 줄로 읽습니다.
                double left, width;
                int k = StepAt(at.X, PlotArea, out left, out width);
                if (k >= 0) ZoomStep(k, XToTime(at.X), factor);
                else ZoomAllSteps(factor);
            }
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
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);
            if (!usable) return;      // 그릴 값이 없는 레인은 세로 위치를 적지 않습니다.

            LaneY ly = LaneFor(key);
            double span = (hi - lo) / ly.Zoom;
            double center = CenterOf(ly, lo, hi, true);

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

            int idx = (int)((at.Y - plot.Top + scrollY) / laneH);
            if (idx < 0 || idx >= _channels.Count) return false;

            double top = plot.Top + idx * laneH - scrollY;
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

            // 칸 머리(단계 이름)를 눌렀으면 그 단계만 봅니다 (1.16).
            // 그림을 누르는 것(커서 찍기 · 끌기)과는 자리가 따로라 섞이지
            // 않습니다.
            int head = StepHeadAt(e.GetPosition(this));
            if (head >= 0)
            {
                EventHandler<int> h = StepHeadClicked;
                if (h != null) h(this, _stepBands[head].Pair);
                e.Handled = true;
                return;
            }

            _dragging = true;
            _moved = false;
            _dragPoint = e.GetPosition(this);
            _dragLast = _dragPoint;
            _dragT0 = _t0; _dragT1 = _t1;

            string key; Rect inner; IoRowVm[] ios;
            _dragLane = null;
            if (LaneAt(_dragPoint, out key, out inner, out ios))
            {
                double lo, hi;
                bool usable;
                BaseRange(ios, out lo, out hi, out usable);
                if (usable)
                {
                    _dragLane = key;
                    _dragCenter = CenterOf(LaneFor(key), lo, hi, true);
                }
            }

            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>그 자리가 몇 번째 칸의 머리인지. 머리가 아니면 -1.</summary>
        private int StepHeadAt(Point at)
        {
            if (!StepMode || at.Y < 0 || at.Y >= StepHeadH) return -1;
            double left, width;
            return StepAt(at.X, PlotArea, out left, out width);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 칸 머리 위에서는 손 모양 — 누를 수 있다는 표시입니다.
            if (!_dragging && !_band)
                Cursor = StepHeadAt(e.GetPosition(this)) >= 0 ? Cursors.Hand : null;

            if (_band)
            {
                _bandTo = e.GetPosition(this);
                InvalidateVisual();
                return;
            }

            if (!_dragging) return;

            Point now = e.GetPosition(this);
            double dx = now.X - _dragPoint.X, dy = now.Y - _dragPoint.Y;
            if (!_moved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
            _moved = true;

            Rect plot = PlotArea;
            if (plot.Width > 0 && StepMode)
            {
                // 누른 칸만 옮깁니다. 끌기는 조금씩 여러 번 오므로 <b>이번에
                // 움직인 만큼</b>만 밀어야 합니다 (누를 때의 자리에서 센
                // 값을 쓰면 가속이 붙습니다).
                double left, width;
                int k = StepAt(_dragPoint.X, plot, out left, out width);
                if (k >= 0 && width > 0)
                {
                    double moved = e.GetPosition(this).X - _dragLast.X;
                    PanStep(k, -moved / width);
                }
            }
            else if (plot.Width > 0)
            {
                double span = _dragT1 - _dragT0;
                double shift = -dx * span / plot.Width;
                _t0 = _dragT0 + shift;
                _t1 = _dragT1 + shift;
            }

            _dragLast = e.GetPosition(this);

            if (_dragLane != null)
            {
                string key; Rect inner; IoRowVm[] ios;
                if (LaneAt(_dragPoint, out key, out inner, out ios) && inner.Height > 0)
                {
                    double lo, hi;
                    bool usable;
                    BaseRange(ios, out lo, out hi, out usable);
                    if (usable)
                    {
                        LaneY ly = LaneFor(key);
                        double vspan = (hi - lo) / ly.Zoom;
                        ly.Center = _dragCenter + dy * vspan / inner.Height;
                    }
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
            // 칸 사이의 금을 누르면 가리키는 시각이 없습니다 (NaN).
            // 커서를 거기 두면 값이 전부 "-" 로 나오니 그냥 둡니다.
            if (double.IsNaN(t)) return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _cursorB = t; else _cursorA = t;
            InvalidateVisual();
            RaiseViewChanged();
        }

        // ---------------- 오른쪽 단추로 네모 확대 ----------------
        //
        // 휠로 확대하면 가운데를 기준으로 커지고 작아지는데, "이 구간만 크게"
        // 를 하려면 휠과 끌기를 번갈아 여러 번 해야 했습니다. 네모를 그리면
        // 그만큼 한 번에 맞춰 줍니다.
        //
        // 가로만 끌면 시간만 확대합니다. 세로로도 끌었으면 그 레인의
        // 값 범위까지 같이 맞춥니다 — 네모를 그렸는데 위아래가 그대로면
        // "영역만큼" 이라는 말과 어긋납니다.

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Focus();
            if (_state == null || _channels.Count == 0) return;

            Point at = e.GetPosition(this);
            Rect plot = PlotArea;
            if (at.X < plot.Left || at.X > plot.Right || at.Y < plot.Top || at.Y > plot.Bottom) return;

            _band = true;
            _bandFrom = at;
            _bandTo = at;

            // 어느 레인에서 시작했는지 적어 둡니다. 세로 확대는 그 레인에만
            // 걸어야 합니다 — 레인을 넘나든 네모의 값 범위는 뜻이 없습니다.
            string key; Rect inner; IoRowVm[] ios;
            _bandLane = LaneAt(at, out key, out inner, out ios) ? key : null;

            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (!_band) return;

            _band = false;
            ReleaseMouseCapture();
            e.Handled = true;

            Rect plot = PlotArea;
            _bandTo = e.GetPosition(this);
            Rect r = BandRect(plot);

            // 손이 떨린 정도는 확대가 아닙니다. 그냥 누른 것으로 봅니다.
            if (r.Width < 6 && r.Height < 6) { InvalidateVisual(); return; }

            // 세로 먼저. 시간을 바꾸면 BaseRange(보이는 구간 맞춤)가 달라져
            // 값 범위 계산이 어긋납니다.
            if (r.Height >= 6) ZoomBandValue(r);
            if (r.Width >= 6)
            {
                if (StepMode) ZoomStepToBox(r);
                else ZoomBandTime(r);
            }

            InvalidateVisual();
            RaiseViewChanged();
        }

        /// <summary>
        /// 네모로 자른 구간으로 그 칸을 확대합니다. 네모가 칸 하나 안에
        /// 있을 때만 — 여러 칸에 걸쳐 그었으면 어느 칸을 확대할지 알 수
        /// 없으므로 아무 일도 하지 않습니다 (세로 확대는 그대로 걸립니다).
        /// </summary>
        private void ZoomStepToBox(Rect r)
        {
            Rect plot = PlotArea;
            double left, width;
            int k = StepAt(r.Left, plot, out left, out width);
            double rightLeft, rightWidth;
            int k2 = StepAt(r.Right, plot, out rightLeft, out rightWidth);
            if (k < 0 || k != k2) return;

            double a = XToTime(r.Left), b = XToTime(r.Right);
            if (double.IsNaN(a) || double.IsNaN(b) || !(b > a)) return;

            StepBand band = _stepBands[k];
            double min = band.Length * 1e-7;
            if (min <= 0) min = 1e-9;
            if (b - a < min) { double mid = (a + b) * 0.5; a = mid - min * 0.5; b = mid + min * 0.5; }

            EnsureStepViews();
            if (a < band.T0) a = band.T0;
            if (b > band.T1) b = band.T1;
            if (!(b > a)) return;

            _stepT0[k] = a;
            _stepT1[k] = b;
        }

        private void ZoomBandTime(Rect r)
        {
            double a = XToTime(r.Left), b = XToTime(r.Right);
            if (!(b > a)) return;

            // 너무 얇게 자르는 것은 막습니다 (휠 확대와 같은 한계).
            double full0, full1;
            _state.FullTimeRange(out full0, out full1);
            double minSpan = (full1 - full0) * 1e-7;
            if (minSpan <= 0) minSpan = 1e-9;
            if (b - a < minSpan)
            {
                double mid = (a + b) * 0.5;
                a = mid - minSpan * 0.5;
                b = mid + minSpan * 0.5;
            }

            _t0 = a;
            _t1 = b;
        }

        /// <summary>
        /// 네모의 위아래만큼 그 레인의 값 범위를 좁힙니다.
        ///
        /// 그리는 쪽과 <b>같은 식을 거꾸로</b> 풉니다 (FitLaneToVisible 과 같은
        /// 자리입니다) — 기준 범위를 Zoom 으로 나누고 Center 에 놓는 것이
        /// 그리는 규칙이므로, 원하는 vlo~vhi 에서 Zoom 과 Center 를 냅니다.
        /// </summary>
        private void ZoomBandValue(Rect r)
        {
            if (_bandLane == null) return;

            // [구간 맞춤] 이 켜져 있으면 값 범위는 보이는 시간 구간을 따라
            // 저절로 맞춰집니다. 거기에 네모 값을 적어 둬도 다음 그림에서
            // 덮이므로, 손대지 않습니다 — 시간만 좁혀도 위아래는 알아서
            // 그 구간에 맞습니다.
            if (_fitVisible) return;

            string key; Rect inner; IoRowVm[] ios;
            if (!LaneAt(_bandFrom, out key, out inner, out ios)) return;
            if (key != _bandLane || inner.Height <= 0) return;

            double lo, hi;
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);
            if (!usable) return;

            double full = hi - lo;
            if (!(full > 0)) return;

            LaneY ly = LaneFor(key);
            double span = full / ly.Zoom;
            if (!(span > 0)) return;
            double center = CenterOf(ly, lo, hi, true);
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            // 네모가 레인 밖으로 나갔으면 그 레인 안으로 잘라 씁니다.
            double y0 = Clamp(r.Top, inner.Top, inner.Bottom);
            double y1 = Clamp(r.Bottom, inner.Top, inner.Bottom);
            if (y1 - y0 < 4) return;

            double vTop = YToValue(y0, inner, vlo, vhi);
            double vBottom = YToValue(y1, inner, vlo, vhi);
            if (!(vTop > vBottom)) return;

            ly.Center = (vTop + vBottom) * 0.5;
            ly.Zoom = full / (vTop - vBottom);
            ClampZooms();
        }

        /// <summary>ValueToY 의 거꾸로. 둘이 어긋나면 네모와 다른 자리로 확대됩니다.</summary>
        private static double YToValue(double y, Rect inner, double vlo, double vhi)
        {
            if (!(inner.Height > 0)) return vlo;
            return vlo + (inner.Bottom - y) * (vhi - vlo) / inner.Height;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double span = _t1 - _t0;

            // 단계별로 나눠 보는 중에는 가로로 움직이는 키를 안 받습니다
            // (휠·끌기와 같은 까닭). 세로 확대 키는 그대로입니다.
            switch (e.Key)
            {
                // 키보드는 <b>모든 칸을 같이</b> 움직입니다 — 단계마다
                // 같은 자리를 보게 해서 서로 견주려는 것입니다. 휠과 끌기는
                // 커서를 올려 둔 칸 하나만 움직입니다.
                case Key.Left:
                    if (StepMode) { PanAllSteps(-0.1); break; }
                    _t0 -= span * 0.1; _t1 -= span * 0.1; break;
                case Key.Right:
                    if (StepMode) { PanAllSteps(+0.1); break; }
                    _t0 += span * 0.1; _t1 += span * 0.1; break;
                case Key.Home: ResetTime(); e.Handled = true; return;
                // Delete = 커서 A · B 를 둘 다 지웁니다 (1.15). 그래프를
                // 한 번 누르면 이 판이 키를 받습니다 (누를 때 Focus()).
                case Key.Delete:
                    if (double.IsNaN(_cursorA) && double.IsNaN(_cursorB)) return;
                    _cursorA = double.NaN; _cursorB = double.NaN;
                    e.Handled = true;
                    InvalidateVisual();
                    RaiseViewChanged();
                    return;
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
