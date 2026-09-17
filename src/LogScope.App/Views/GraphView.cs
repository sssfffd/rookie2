using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using LogScope.App.Controls;
using LogScope.Core.Model;

namespace LogScope.App.Views
{
    /// <summary>
    /// 그래프 화면. 왼쪽에 IO 목록과 그룹, 오른쪽에 그래프입니다.
    /// 대시보드에서 IO 를 눌러 들어오면 그 IO 만 켜진 채로 열리고,
    /// 메뉴로 직접 들어오면 왼쪽 목록에서 직접 고를 수 있습니다.
    /// </summary>
    public sealed class GraphView : UserControl
    {
        private readonly AppState _state;
        private readonly ChannelPanel _panel;
        private readonly GraphCanvas _canvas;
        private readonly FlowBar _bar = new FlowBar();
        private readonly Panel _readout = new Panel();
        private readonly Label _readoutText = new Label();
        private readonly SplitContainer _split = new SplitContainer();

        private readonly Buttons.Segmented _viewMode;
        private readonly Buttons.Segmented _scaleMode;
        private readonly Buttons.Toggle _fitVisible;
        private readonly Buttons.Toggle _shade;
        private readonly Buttons.Toggle _separate;
        private readonly TextBox _tolerance = new TextBox();

        public GraphView(AppState state)
        {
            _state = state;
            BackColor = Theme.Current.Window;

            _panel = new ChannelPanel(state);
            _panel.Dock = DockStyle.Fill;
            _panel.SelectionChanged += delegate { _canvas.Invalidate(); _state.RaiseSelection(); };
            _panel.GroupsChanged += delegate { _canvas.Invalidate(); _state.RaiseSelection(); };

            _canvas = new GraphCanvas(state);
            _canvas.Dock = DockStyle.Fill;
            _canvas.CursorMoved += delegate { UpdateReadout(); };

            _viewMode = new Buttons.Segmented(new[] { "레인 (채널마다 한 줄)", "겹쳐보기 (한 눈금 공유)" },
                                              state.Settings.LaneMode ? 0 : 1);
            _scaleMode = new Buttons.Segmented(new[] { "원래값", "0–1 정규화", "변화만" }, ScaleIndex(state.Settings.ValueScaleMode));
            _fitVisible = new Buttons.Toggle("보이는 구간에 맞춤");
            _shade = new Buttons.Toggle("차이 구간 음영");
            _separate = new Buttons.Toggle("이전/이후 벌려 그리기");

            BuildBar();
            BuildReadout();

            var right = new Panel();
            right.Dock = DockStyle.Fill;
            right.BackColor = Theme.Current.Window;
            right.Controls.Add(_canvas);
            right.Controls.Add(_readout);
            right.Controls.Add(_bar);
            _readout.Dock = DockStyle.Bottom;

            _split.Dock = DockStyle.Fill;
            _split.Orientation = System.Windows.Forms.Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SplitterWidth = Dpi.S(5);
            _split.Panel1MinSize = Dpi.S(220);
            _split.Panel2MinSize = Dpi.S(320);
            _split.Panel1.Controls.Add(_panel);
            _split.Panel2.Controls.Add(right);
            Controls.Add(_split);

            ApplySettings();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // SplitterDistance 는 핸들이 생긴 뒤에 잡아야 값이 튕기지 않습니다.
            try { _split.SplitterDistance = Dpi.S(304); }
            catch (InvalidOperationException) { }
            catch (ArgumentOutOfRangeException) { }
        }

        private static int ScaleIndex(string mode)
        {
            if (string.Equals(mode, "normalized", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(mode, "delta", StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }

        private static string ScaleName(int index)
        {
            switch (index) { case 1: return "normalized"; case 2: return "delta"; default: return "raw"; }
        }

        private void BuildBar()
        {
            Theme t = Theme.Current;

            _bar.AddLabel("보기 방식");
            _viewMode.IndexChanged += delegate
            {
                _canvas.LaneMode = _viewMode.Index == 0;
                _state.Settings.LaneMode = _canvas.LaneMode;
                _canvas.ResetValueZoom();
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_viewMode);

            _bar.AddLabel("세로 눈금");
            _scaleMode.IndexChanged += delegate
            {
                _canvas.Scale = (ValueScaleMode)_scaleMode.Index;
                _state.Settings.ValueScaleMode = ScaleName(_scaleMode.Index);
                _canvas.ResetValueZoom();
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_scaleMode);

            _fitVisible.CheckedChangedEx += delegate
            {
                _canvas.FitVisible = _fitVisible.On;
                _state.Settings.FitVisible = _fitVisible.On;
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_fitVisible);
            _bar.AddBreak();

            _bar.AddLabel("시간축");
            _bar.Controls.Add(Buttons.Make("전체 구간 보기", delegate { _canvas.ResetTime(); UpdateReadout(); }));
            _bar.Controls.Add(Buttons.Make("시간 확대", delegate { _canvas.ZoomTime(1.35); _canvas.Invalidate(); UpdateReadout(); }));
            _bar.Controls.Add(Buttons.Make("시간 축소", delegate { _canvas.ZoomTime(1 / 1.35); _canvas.Invalidate(); UpdateReadout(); }));

            _bar.AddLabel("값 축");
            _bar.Controls.Add(Buttons.Make("값 확대", delegate { _canvas.ZoomValue(1.35); }));
            _bar.Controls.Add(Buttons.Make("값 축소", delegate { _canvas.ZoomValue(1 / 1.35); }));
            _bar.Controls.Add(Buttons.Make("값 배율 되돌리기", delegate { _canvas.ResetValueZoom(); }));
            _bar.AddBreak();

            _bar.AddLabel("두 로그 비교");
            _shade.CheckedChangedEx += delegate
            {
                _canvas.ShadeDifference = _shade.On;
                _state.Settings.ShadeDifference = _shade.On;
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_shade);

            _separate.CheckedChangedEx += delegate
            {
                _canvas.SeparateTraces = _separate.On;
                _state.Settings.SeparateTraces = _separate.On;
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_separate);

            _bar.AddLabel("허용 오차");
            _tolerance.Width = Dpi.S(70);
            _tolerance.BorderStyle = BorderStyle.FixedSingle;
            _tolerance.BackColor = t.IsDark ? t.PanelAlt : Color.White;
            _tolerance.ForeColor = t.Text;
            _tolerance.Margin = new Padding(Dpi.S(3), Dpi.S(5), Dpi.S(3), Dpi.S(3));
            _tolerance.TextChanged += delegate
            {
                double v;
                if (!double.TryParse(_tolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                if (v < 0) return;
                _state.Settings.Tolerance = v;
                _canvas.Tolerance = v;
                _canvas.Invalidate();
            };
            _bar.Controls.Add(_tolerance);

            var hint = new Label();
            hint.Text = "휠 = 시간 확대,  Shift + 휠 = 값 확대,  끌기 = 이동,  누르기 = 커서 A,  Shift + 누르기 = 커서 B";
            hint.AutoSize = true;
            hint.ForeColor = t.Muted;
            hint.Font = Theme.Small;
            hint.Margin = new Padding(Dpi.S(10), Dpi.S(8), Dpi.S(4), Dpi.S(2));
            _bar.Controls.Add(hint);
        }

        private void BuildReadout()
        {
            Theme t = Theme.Current;
            _readout.Height = Dpi.S(30);
            _readout.BackColor = t.Panel;
            _readout.Padding = new Padding(Dpi.S(10), Dpi.S(6), Dpi.S(10), Dpi.S(4));

            _readoutText.Dock = DockStyle.Fill;
            _readoutText.ForeColor = t.Text;
            _readoutText.Font = Theme.Ui;
            _readoutText.AutoEllipsis = true;
            _readout.Controls.Add(_readoutText);
            UpdateReadout();
        }

        private void UpdateReadout()
        {
            LogDataset refDs = _state.TimeReference;
            string a = double.IsNaN(_canvas.CursorA) ? "없음"
                : (refDs != null ? refDs.FormatTime(_canvas.CursorA) : _canvas.CursorA.ToString("0.###"));
            string b = double.IsNaN(_canvas.CursorB) ? "없음"
                : (refDs != null ? refDs.FormatTime(_canvas.CursorB) : _canvas.CursorB.ToString("0.###"));
            string d = "-";
            if (!double.IsNaN(_canvas.CursorA) && !double.IsNaN(_canvas.CursorB))
            {
                double diff = _canvas.CursorB - _canvas.CursorA;
                d = refDs != null && refDs.TimeKind == TimeKind.ClockMs
                    ? (diff / 1000.0).ToString("0.###") + " 초"
                    : diff.ToString("0.####");
            }
            string range = refDs != null
                ? refDs.FormatTime(_canvas.VisibleStart) + " ~ " + refDs.FormatTime(_canvas.VisibleEnd)
                : string.Empty;

            _readoutText.Text = "커서 A: " + a + "     커서 B: " + b + "     B − A: " + d
                              + "     보이는 구간: " + range
                              + "     고른 IO: " + _state.View.SelectedCount + "개";
        }

        /// <summary>설정값을 화면에 반영합니다.</summary>
        public void ApplySettings()
        {
            _canvas.LaneMode = _state.Settings.LaneMode;
            _viewMode.Index = _state.Settings.LaneMode ? 0 : 1;

            _canvas.Scale = (ValueScaleMode)ScaleIndex(_state.Settings.ValueScaleMode);
            _scaleMode.Index = ScaleIndex(_state.Settings.ValueScaleMode);

            _fitVisible.On = _state.Settings.FitVisible;
            _canvas.FitVisible = _state.Settings.FitVisible;

            _shade.On = _state.Settings.ShadeDifference;
            _canvas.ShadeDifference = _state.Settings.ShadeDifference;

            _separate.On = _state.Settings.SeparateTraces;
            _canvas.SeparateTraces = _state.Settings.SeparateTraces;

            _canvas.Tolerance = _state.Settings.Tolerance;
            _tolerance.Text = _state.Settings.Tolerance.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>로그를 새로 열었을 때.</summary>
        public void OnDataChanged()
        {
            _panel.RefreshAll();
            _canvas.ResetTime();
            _canvas.ResetValueZoom();
            UpdateReadout();
            _canvas.Invalidate();
        }

        /// <summary>대시보드에서 IO 하나를 눌러 넘어왔을 때.</summary>
        public void FocusOnIo(string name, bool exclusive)
        {
            if (exclusive) _state.View.ClearSelection();
            _state.View.SetSelectedByName(name, true);
            _panel.RefreshAll();
            _panel.RevealIo(name);
            _canvas.ResetValueZoom();
            _canvas.Invalidate();
            UpdateReadout();
        }

        public void RefreshLists()
        {
            _panel.RefreshAll();
            UpdateReadout();
            _canvas.Invalidate();
        }
    }
}
