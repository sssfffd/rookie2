using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LogScope.App.Controls;
using LogScope.Core.Compare;

namespace LogScope.App.Views
{
    /// <summary>
    /// 프로그램을 열면 먼저 나오는 화면.
    /// 이전/이후 로그를 견준 결과를 네모 칸으로 요약해 보여 주고, 아래의
    /// 목록에서 IO 를 누르면 그래프 화면으로 넘어갑니다.
    /// </summary>
    public sealed class DashboardView : UserControl
    {
        private readonly AppState _state;

        private readonly FlowBar _header = new FlowBar();
        private readonly Label _files = new Label();
        private readonly FlowLayoutPanel _tiles = new FlowLayoutPanel();
        private readonly SplitContainer _body = new SplitContainer();
        private readonly SplitContainer _sides = new SplitContainer();

        private readonly Tile _tileBefore = new Tile("이전 로그 IO 개수");
        private readonly Tile _tileAfter = new Tile("이후 로그 IO 개수");
        private readonly Tile _tileChanged = new Tile("차이 발생 IO 개수");
        private readonly Tile _tileSamples = new Tile("표본 수 (이전 / 이후)");

        private readonly ListView _changed = new ListView();
        private readonly ListView _onlyBefore = new ListView();
        private readonly ListView _onlyAfter = new ListView();
        private readonly ComboBox _metric = new ComboBox();
        private readonly Label _warning = new Label();

        /// <summary>목록에서 IO 를 눌렀을 때. 그래프 화면으로 넘어갑니다.</summary>
        public event EventHandler<IoEventArgs> IoActivated;

        public sealed class IoEventArgs : EventArgs
        {
            public readonly string Name;
            public IoEventArgs(string name) { Name = name; }
        }

        public DashboardView(AppState state)
        {
            _state = state;
            BackColor = Theme.Current.Window;

            BuildHeader();
            BuildTiles();
            BuildLists();

            Controls.Add(_body);
            Controls.Add(_tiles);
            Controls.Add(_header);
            UpdateAll();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                _body.SplitterDistance = Math.Max(Dpi.S(160), _body.Height * 6 / 10);
                _sides.SplitterDistance = Math.Max(Dpi.S(120), _sides.Width / 2);
            }
            catch (InvalidOperationException) { }
            catch (ArgumentOutOfRangeException) { }
        }

        // ---------------- 위쪽 ----------------

        private void BuildHeader()
        {
            Theme t = Theme.Current;
            var title = new Label();
            title.Text = "대시보드";
            title.Font = Theme.UiBold;
            title.ForeColor = t.Text;
            title.AutoSize = true;
            title.Margin = new Padding(Dpi.S(2), Dpi.S(6), Dpi.S(12), Dpi.S(2));
            _header.Controls.Add(title);

            _files.Text = "로그를 열어 주세요.";
            _files.AutoSize = true;
            _files.ForeColor = t.Muted;
            _files.Margin = new Padding(Dpi.S(2), Dpi.S(7), Dpi.S(8), Dpi.S(2));
            _header.Controls.Add(_files);

            _warning.AutoSize = true;
            _warning.ForeColor = t.Bad;
            _warning.Margin = new Padding(Dpi.S(8), Dpi.S(7), Dpi.S(4), Dpi.S(2));
            _header.Controls.Add(_warning);
            _header.AddBreak();

            _header.AddLabel("정렬 기준");
            _metric.DropDownStyle = ComboBoxStyle.DropDownList;
            _metric.Items.AddRange(new object[]
            {
                "최대 차이", "평균 차이", "RMS", "차이 면적", "차이 시간 비율", "차이 표본 수", "차이 구간 수"
            });
            _metric.SelectedIndex = (int)_state.Settings.SortMetric;
            _metric.Width = Dpi.S(130);
            _metric.Margin = new Padding(Dpi.S(3), Dpi.S(4), Dpi.S(6), Dpi.S(3));
            _metric.SelectedIndexChanged += delegate
            {
                _state.Settings.SortMetric = (DiffMetric)_metric.SelectedIndex;
                if (_state.Comparison != null)
                    DiffEngine.SortByMetric(_state.Comparison.Items, _state.Settings.SortMetric);
                FillChanged();
            };
            _header.Controls.Add(_metric);
        }

        private void BuildTiles()
        {
            _tiles.Dock = DockStyle.Top;
            _tiles.AutoSize = true;
            _tiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _tiles.WrapContents = true;
            _tiles.BackColor = Theme.Current.Window;
            _tiles.Padding = new Padding(Dpi.S(10), Dpi.S(10), Dpi.S(10), Dpi.S(4));

            _tiles.Controls.Add(_tileBefore);
            _tiles.Controls.Add(_tileAfter);
            _tiles.Controls.Add(_tileChanged);
            _tiles.Controls.Add(_tileSamples);
        }

        // ---------------- 목록 ----------------

        private void BuildLists()
        {
            _body.Dock = DockStyle.Fill;
            _body.Orientation = System.Windows.Forms.Orientation.Horizontal;
            _body.SplitterWidth = Dpi.S(6);
            _body.Panel1MinSize = Dpi.S(120);
            _body.Panel2MinSize = Dpi.S(100);
            _body.BackColor = Theme.Current.Window;

            _body.Panel1.Controls.Add(Boxed("차이가 난 IO 목록  (한 줄을 누르면 그래프 화면으로 넘어갑니다)",
                                            SetupChanged()));

            _sides.Dock = DockStyle.Fill;
            _sides.Orientation = System.Windows.Forms.Orientation.Vertical;
            _sides.SplitterWidth = Dpi.S(6);
            _sides.Panel1MinSize = Dpi.S(140);
            _sides.Panel2MinSize = Dpi.S(140);
            _sides.Panel1.Controls.Add(Boxed("이전 로그에만 있는 IO", SetupOnly(_onlyBefore)));
            _sides.Panel2.Controls.Add(Boxed("이후 로그에만 있는 IO", SetupOnly(_onlyAfter)));
            _body.Panel2.Controls.Add(_sides);
        }

        private static Control Boxed(string caption, Control inner)
        {
            Theme t = Theme.Current;
            var host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = new Padding(Dpi.S(10), Dpi.S(4), Dpi.S(10), Dpi.S(8));
            host.BackColor = t.Window;

            var frame = new Panel();
            frame.Dock = DockStyle.Fill;
            frame.BackColor = t.Window;
            frame.BorderStyle = BorderStyle.FixedSingle;
            frame.Padding = new Padding(Dpi.S(1));

            var label = new Label();
            label.Text = caption;
            label.Dock = DockStyle.Top;
            label.Height = Dpi.S(24);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(Dpi.S(8), 0, 0, 0);
            label.BackColor = t.PanelAlt;
            label.ForeColor = t.Text;
            label.Font = Theme.SmallBold;

            inner.Dock = DockStyle.Fill;
            frame.Controls.Add(inner);
            frame.Controls.Add(label);
            host.Controls.Add(frame);
            return host;
        }

        private ListView SetupChanged()
        {
            Style(_changed);
            _changed.Columns.Add("IO 이름", Dpi.S(220));
            _changed.Columns.Add("상태", Dpi.S(90));
            _changed.Columns.Add("최대 차이", Dpi.S(90), HorizontalAlignment.Right);
            _changed.Columns.Add("평균 차이", Dpi.S(90), HorizontalAlignment.Right);
            _changed.Columns.Add("RMS", Dpi.S(80), HorizontalAlignment.Right);
            _changed.Columns.Add("차이 면적", Dpi.S(100), HorizontalAlignment.Right);
            _changed.Columns.Add("차이 시간", Dpi.S(80), HorizontalAlignment.Right);
            _changed.Columns.Add("구간 수", Dpi.S(70), HorizontalAlignment.Right);
            _changed.ItemActivate += delegate { Activate(_changed); };
            _changed.DoubleClick += delegate { Activate(_changed); };
            _changed.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) Activate(_changed);
            };
            _changed.Click += delegate { Activate(_changed); };
            return _changed;
        }

        private ListView SetupOnly(ListView lv)
        {
            Style(lv);
            lv.Columns.Add("IO 이름", Dpi.S(240));
            lv.Click += delegate { Activate(lv); };
            lv.ItemActivate += delegate { Activate(lv); };
            return lv;
        }

        private static void Style(ListView lv)
        {
            Theme t = Theme.Current;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.GridLines = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = t.Window;
            lv.ForeColor = t.Text;
            lv.Font = Theme.Ui;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        }

        private void Activate(ListView lv)
        {
            if (lv.SelectedItems.Count == 0) return;
            ListViewItem sel = lv.SelectedItems[0];
            if (sel.Tag as string == "placeholder") return;   // 안내 문구 줄
            string name = sel.Text;
            var h = IoActivated;
            if (h != null) h(this, new IoEventArgs(name));
        }

        // ---------------- 채우기 ----------------

        public void UpdateAll()
        {
            Theme t = Theme.Current;
            CompareResult c = _state.Comparison;

            string bf = string.IsNullOrEmpty(_state.BeforePath) ? "(없음)" : System.IO.Path.GetFileName(_state.BeforePath);
            string af = string.IsNullOrEmpty(_state.AfterPath) ? "(없음)" : System.IO.Path.GetFileName(_state.AfterPath);
            _files.Text = "이전: " + bf + "     이후: " + af;

            int beforeCount = _state.Before != null ? _state.Before.ChannelCount : 0;
            int afterCount = _state.After != null ? _state.After.ChannelCount : 0;
            int onlyB = c != null ? c.OnlyBefore.Count : 0;
            int onlyA = c != null ? c.OnlyAfter.Count : 0;

            _tileBefore.Set(beforeCount.ToString("N0") + "개",
                onlyB > 0 ? "이름이 이후에 없는 IO: " + onlyB + "개" : "이름이 이후에 없는 IO: 없음",
                onlyB > 0 ? t.Bad : t.Muted);

            _tileAfter.Set(afterCount.ToString("N0") + "개",
                onlyA > 0 ? "이름이 이전에 없는 IO: " + onlyA + "개" : "이름이 이전에 없는 IO: 없음",
                onlyA > 0 ? t.Good : t.Muted);

            int changed = c != null ? c.ChangedCount : 0;
            int common = c != null ? c.CommonCount : 0;
            _tileChanged.Set(changed.ToString("N0") + "개",
                "양쪽에 다 있는 IO " + common + "개 중 / 허용 오차 "
                    + _state.Settings.Tolerance.ToString("0.######"),
                changed > 0 ? t.Warn : t.Good);

            string sb = _state.Before != null ? _state.Before.SampleCount.ToString("N0") : "-";
            string sa = _state.After != null ? _state.After.SampleCount.ToString("N0") : "-";
            _tileSamples.Set(sb + " / " + sa, LoadSummary(), t.Muted);

            _warning.Text = c != null ? c.Warning : string.Empty;

            FillChanged();
            FillOnly(_onlyBefore, c != null ? c.OnlyBefore : null);
            FillOnly(_onlyAfter, c != null ? c.OnlyAfter : null);
        }

        private string LoadSummary()
        {
            var parts = new List<string>();
            if (_state.Before != null) parts.Add("이전 " + _state.Before.LoadSeconds.ToString("0.0") + "초");
            if (_state.After != null) parts.Add("이후 " + _state.After.LoadSeconds.ToString("0.0") + "초");
            return parts.Count > 0 ? "여는 데 걸린 시간: " + string.Join(", ", parts.ToArray()) : "로그 없음";
        }

        private void FillChanged()
        {
            _changed.BeginUpdate();
            _changed.Items.Clear();
            CompareResult c = _state.Comparison;
            if (c != null)
            {
                foreach (ChannelDiff d in c.Items)
                {
                    if (!d.Changed) continue;
                    var it = new ListViewItem(d.Name);
                    it.SubItems.Add(d.ByName ? "상태 다름" : "값 다름");
                    it.SubItems.Add(d.Format(DiffMetric.MaxAbs));
                    it.SubItems.Add(d.Format(DiffMetric.MeanAbs));
                    it.SubItems.Add(d.Format(DiffMetric.Rms));
                    it.SubItems.Add(d.Format(DiffMetric.Area));
                    it.SubItems.Add(d.Format(DiffMetric.TimeRatio));
                    it.SubItems.Add(d.Format(DiffMetric.SegmentCount));
                    _changed.Items.Add(it);
                }
            }
            if (_changed.Items.Count == 0)
            {
                var it = new ListViewItem(c == null ? "이전 로그와 이후 로그를 모두 열면 여기에 차이가 나옵니다."
                                                    : "허용 오차 안에서 차이가 난 IO 가 없습니다.");
                it.ForeColor = Theme.Current.Muted;
                it.Tag = "placeholder";
                _changed.Items.Add(it);
            }
            _changed.EndUpdate();
        }

        private void FillOnly(ListView lv, List<ChannelDiff> items)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            if (items != null)
                foreach (ChannelDiff d in items) lv.Items.Add(new ListViewItem(d.Name));
            if (lv.Items.Count == 0)
            {
                var it = new ListViewItem("없음");
                it.ForeColor = Theme.Current.Muted;
                it.Tag = "placeholder";
                lv.Items.Add(it);
            }
            lv.EndUpdate();
        }

        // ---------------- 네모 칸 ----------------

        private sealed class Tile : Panel
        {
            private readonly Label _caption = new Label();
            private readonly Label _value = new Label();
            private readonly Label _sub = new Label();

            public Tile(string caption)
            {
                Theme t = Theme.Current;
                Size = new Size(Dpi.S(250), Dpi.S(104));
                Margin = new Padding(Dpi.S(6));
                BackColor = t.Panel;
                BorderStyle = BorderStyle.FixedSingle;
                Padding = new Padding(Dpi.S(12), Dpi.S(9), Dpi.S(12), Dpi.S(9));

                _caption.Text = caption;
                _caption.Dock = DockStyle.Top;
                _caption.Height = Dpi.S(18);
                _caption.Font = Theme.Small;
                _caption.ForeColor = t.Muted;

                _value.Text = "-";
                _value.Dock = DockStyle.Top;
                _value.Height = Dpi.S(40);
                _value.Font = Theme.Big;
                _value.ForeColor = t.Text;

                _sub.Text = string.Empty;
                _sub.Dock = DockStyle.Top;
                _sub.Height = Dpi.S(32);
                _sub.Font = Theme.Small;
                _sub.ForeColor = t.Muted;

                Controls.Add(_sub);
                Controls.Add(_value);
                Controls.Add(_caption);
            }

            public void Set(string value, string sub, Color subColor)
            {
                _value.Text = value;
                _sub.Text = sub;
                _sub.ForeColor = subColor;
            }
        }
    }
}
