using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LogScope.App.Controls;
using LogScope.App.Dialogs;
using LogScope.App.Views;
using LogScope.Core;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App
{
    /// <summary>
    /// 창 하나. 위쪽에 진한 파랑 메뉴 줄, 아래에 대시보드 또는 그래프 화면.
    /// 프로그램을 켜면 대시보드가 먼저 나옵니다.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly AppState _state;

        private readonly FlowLayoutPanel _top = new FlowLayoutPanel();
        private readonly Panel _content = new Panel();
        private readonly Label _status = new Label();

        private readonly Button _navDashboard;
        private readonly Button _navGraph;
        private readonly ComboBox _setBox = new ComboBox();

        private DashboardView _dashboard;
        private GraphView _graph;
        private bool _showingGraph;
        private bool _switchingSet;

        public string[] PendingCommandLine;

        public MainForm(AppState state)
        {
            _state = state;
            Theme t = Theme.Current;

            Text = "LogScope — IO 로그 그래프 뷰어";
            BackColor = t.Window;
            ForeColor = t.Text;
            Font = Theme.Ui;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Dpi.S(900), Dpi.S(560));
            ClientSize = new Size(Math.Max(Dpi.S(900), state.Settings.WindowWidth),
                                  Math.Max(Dpi.S(560), state.Settings.WindowHeight));
            if (state.Settings.WindowMaximized) WindowState = FormWindowState.Maximized;
            KeyPreview = true;

            _navDashboard = TopButton("대시보드", delegate { ShowDashboard(); });
            _navGraph = TopButton("그래프", delegate { ShowGraph(); });

            BuildTopBar();
            BuildStatus();

            _content.Dock = DockStyle.Fill;
            _content.BackColor = t.Window;

            Controls.Add(_content);
            Controls.Add(_status);
            Controls.Add(_top);

            _dashboard = new DashboardView(_state);
            _dashboard.Dock = DockStyle.Fill;
            _dashboard.IoActivated += delegate (object s, DashboardView.IoEventArgs e)
            {
                ShowGraph();
                _graph.FocusOnIo(e.Name, true);
            };

            _graph = new GraphView(_state);
            _graph.Dock = DockStyle.Fill;

            ShowDashboard();
            UpdateSetBox();
            UpdateStatus();
        }

        // ---------------- 위쪽 메뉴 줄 ----------------

        private void BuildTopBar()
        {
            Theme t = Theme.Current;
            _top.Dock = DockStyle.Top;
            _top.AutoSize = true;
            _top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _top.WrapContents = true;
            _top.BackColor = t.TopBar;
            _top.Padding = new Padding(Dpi.S(12), Dpi.S(7), Dpi.S(12), Dpi.S(7));

            var brand = new Label();
            brand.Text = "LogScope";
            brand.Font = new Font(Theme.Ui.FontFamily, Theme.Ui.SizeInPoints + 2.5f, FontStyle.Bold);
            brand.ForeColor = t.TopBarText;
            brand.AutoSize = true;
            brand.Margin = new Padding(Dpi.S(2), Dpi.S(6), Dpi.S(6), Dpi.S(2));
            _top.Controls.Add(brand);

            var ver = new Label();
            ver.Text = "v" + BuildInfo.Version;
            ver.ForeColor = t.TopBarMuted;
            ver.Font = Theme.Small;
            ver.AutoSize = true;
            ver.Margin = new Padding(Dpi.S(2), Dpi.S(11), Dpi.S(14), Dpi.S(2));
            _top.Controls.Add(ver);

            _top.Controls.Add(_navDashboard);
            _top.Controls.Add(_navGraph);

            var sep = new Label();
            sep.Text = "로그 세트";
            sep.ForeColor = t.TopBarMuted;
            sep.AutoSize = true;
            sep.Margin = new Padding(Dpi.S(16), Dpi.S(9), Dpi.S(4), Dpi.S(2));
            _top.Controls.Add(sep);

            _setBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _setBox.Width = Dpi.S(150);
            _setBox.Margin = new Padding(Dpi.S(2), Dpi.S(5), Dpi.S(10), Dpi.S(3));
            _setBox.SelectedIndexChanged += delegate
            {
                if (_switchingSet) return;
                SwitchSet(_setBox.SelectedIndex);
            };
            _top.Controls.Add(_setBox);

            _top.Controls.Add(TopButton("이전 로그 열기", delegate { OpenLog(true); }));
            _top.Controls.Add(TopButton("이후 로그 열기", delegate { OpenLog(false); }));
            _top.Controls.Add(TopButton("다시 읽기", delegate { ReloadBoth(); }));
            _top.Controls.Add(TopButton("비교 다시 하기", delegate { RunCompare(true); }));
            _top.Controls.Add(TopButton("설정", delegate { OpenSettings(); }));
        }

        private Button TopButton(string text, EventHandler onClick)
        {
            Theme t = Theme.Current;
            var b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.FlatStyle = FlatStyle.Flat;
            b.Font = Theme.Ui;
            b.ForeColor = t.TopBarText;
            b.BackColor = t.TopBar;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = t.TopBarActive;
            b.FlatAppearance.MouseOverBackColor = t.TopBarActive;
            b.Margin = new Padding(Dpi.S(3), Dpi.S(4), Dpi.S(3), Dpi.S(4));
            b.Padding = new Padding(Dpi.S(10), Dpi.S(4), Dpi.S(10), Dpi.S(4));
            if (onClick != null) b.Click += onClick;
            return b;
        }

        private void MarkNav()
        {
            Theme t = Theme.Current;
            _navDashboard.BackColor = _showingGraph ? t.TopBar : t.TopBarActive;
            _navGraph.BackColor = _showingGraph ? t.TopBarActive : t.TopBar;
        }

        private void BuildStatus()
        {
            Theme t = Theme.Current;
            _status.Dock = DockStyle.Bottom;
            _status.Height = Dpi.S(26);
            _status.BackColor = t.Panel;
            _status.ForeColor = t.Muted;
            _status.Font = Theme.Small;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(Dpi.S(12), 0, Dpi.S(12), 0);
            _status.AutoEllipsis = true;
        }

        // ---------------- 화면 전환 ----------------

        private void ShowDashboard()
        {
            _showingGraph = false;
            Swap(_dashboard);
            if (_dashboard != null) _dashboard.UpdateAll();
            MarkNav();
        }

        private void ShowGraph()
        {
            _showingGraph = true;
            Swap(_graph);
            if (_graph != null) _graph.RefreshLists();
            MarkNav();
        }

        private void Swap(Control view)
        {
            if (view == null) return;
            if (_content.Controls.Count == 1 && _content.Controls[0] == view) return;
            _content.SuspendLayout();
            _content.Controls.Clear();
            _content.Controls.Add(view);
            _content.ResumeLayout();
        }

        // ---------------- 로그 세트 ----------------

        private void UpdateSetBox()
        {
            _switchingSet = true;
            _setBox.Items.Clear();
            for (int i = 0; i < AppSettings.SetCount; i++)
                _setBox.Items.Add(_state.Settings.Sets[i].DisplayName(i));
            _setBox.SelectedIndex = _state.Settings.ActiveSet;
            _switchingSet = false;
        }

        private void SwitchSet(int index)
        {
            if (index < 0 || index >= AppSettings.SetCount) return;
            _state.Settings.ActiveSet = index;
            LogSet s = _state.Settings.ActiveLogSet;

            _state.Before = null;
            _state.After = null;
            _state.Comparison = null;
            _state.BeforePath = string.Empty;
            _state.AfterPath = string.Empty;

            bool loaded = false;
            if (!string.IsNullOrEmpty(s.BeforePath) && File.Exists(s.BeforePath))
                loaded |= LoadInto(s.BeforePath, true);
            if (!string.IsNullOrEmpty(s.AfterPath) && File.Exists(s.AfterPath))
                loaded |= LoadInto(s.AfterPath, false);

            _state.RebuildView();
            if (loaded) RunCompare(false);
            AfterDataChanged();
            SaveSettings();
        }

        // ---------------- 로그 열기 ----------------

        private void OpenLog(bool before)
        {
            LogSet set = _state.Settings.ActiveLogSet;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = before ? "이전 로그 열기" : "이후 로그 열기";
                dlg.Filter = LogReader.FileDialogFilter;
                dlg.CheckFileExists = true;

                string start = before ? set.BeforeFolder : set.AfterFolder;
                if (string.IsNullOrEmpty(start))
                {
                    string last = before ? set.BeforePath : set.AfterPath;
                    if (!string.IsNullOrEmpty(last))
                    {
                        try { start = Path.GetDirectoryName(last); }
                        catch (ArgumentException) { start = null; }
                    }
                }
                if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) dlg.InitialDirectory = start;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!LoadInto(dlg.FileName, before)) return;
            }

            _state.RebuildView();
            RunCompare(false);
            AfterDataChanged();
            SaveSettings();
        }

        private void ReloadBoth()
        {
            LogSet set = _state.Settings.ActiveLogSet;
            bool any = false;
            if (!string.IsNullOrEmpty(set.BeforePath) && File.Exists(set.BeforePath))
                any |= LoadInto(set.BeforePath, true);
            if (!string.IsNullOrEmpty(set.AfterPath) && File.Exists(set.AfterPath))
                any |= LoadInto(set.AfterPath, false);

            if (!any)
            {
                MessageBox.Show(this, "이 세트에 기억된 로그 파일이 없습니다.", "다시 읽기",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _state.RebuildView();
            RunCompare(false);
            AfterDataChanged();
        }

        /// <summary>파일 하나를 배경 스레드에서 읽습니다. 성공하면 true.</summary>
        private bool LoadInto(string path, bool before)
        {
            var options = new OpenOptions();
            object result; Exception error;
            string title = (before ? "이전 로그를 읽는 중" : "이후 로그를 읽는 중") + " — " + Path.GetFileName(path);

            bool ok = LoadingDialog.Run(this, title,
                delegate (LoadProgress p) { return LogReader.Open(path, options, p); },
                out result, out error);

            if (!ok)
            {
                if (error is OperationCanceledException) return false;
                MessageBox.Show(this,
                    "로그를 읽지 못했습니다.\n\n" + path + "\n\n"
                    + (error != null ? error.Message : "알 수 없는 오류"),
                    "열기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var ds = (LogDataset)result;
            LogSet set = _state.Settings.ActiveLogSet;
            if (before) { _state.Before = ds; _state.BeforePath = path; set.BeforePath = path; }
            else { _state.After = ds; _state.AfterPath = path; set.AfterPath = path; }
            return true;
        }

        private void RunCompare(bool alsoRefresh)
        {
            if (!_state.HasBoth)
            {
                _state.Comparison = null;
                _state.SyncChangedNames();
                if (alsoRefresh) AfterDataChanged();
                return;
            }

            object result; Exception error;
            LoadingDialog.Run(this, "두 로그를 견주는 중",
                delegate (LoadProgress p) { _state.Recompare(p); return string.Empty; },
                out result, out error);

            if (error != null && !(error is OperationCanceledException))
            {
                MessageBox.Show(this, "비교하지 못했습니다.\n\n" + error.Message,
                    "비교 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _state.SyncChangedNames();
            if (alsoRefresh) AfterDataChanged();
        }

        private void AfterDataChanged()
        {
            _state.RebuildView();
            if (_graph != null) _graph.OnDataChanged();
            if (_dashboard != null) _dashboard.UpdateAll();
            UpdateSetBox();
            UpdateStatus();
            _state.RaiseData();
        }

        private void UpdateStatus()
        {
            string s = string.Empty;
            if (_state.Before != null)
                s += "이전: " + Describe(_state.Before);
            if (_state.After != null)
                s += (s.Length > 0 ? "     |     " : "") + "이후: " + Describe(_state.After);
            if (s.Length == 0) s = "로그를 열어 주세요.  설정 창에서 세트별 기본 폴더를 정할 수 있습니다.";
            else
            {
                string notes = string.Empty;
                if (_state.Before != null) notes += _state.Before.NotesText;
                if (_state.After != null) notes += " " + _state.After.NotesText;
                notes = notes.Trim();
                if (notes.Length > 0) s += "     |     " + notes;
            }
            _status.Text = s;
        }

        private static string Describe(LogDataset ds)
        {
            return Path.GetFileName(ds.SourcePath)
                 + " · 채널 " + ds.ChannelCount.ToString("N0")
                 + " · 표본 " + ds.SampleCount.ToString("N0")
                 + " · " + (ds.Orientation == LogScope.Core.Model.Orientation.Cols ? "열이 IO" : "행이 IO")
                 + " · " + ds.LoadSeconds.ToString("0.0") + "초";
        }

        // ---------------- 설정 ----------------

        private void OpenSettings()
        {
            string themeBefore = _state.Settings.Theme;
            using (var dlg = new SettingsDialog(_state.Settings))
            {
                dlg.ShowDialog(this);
            }
            SaveSettings();
            UpdateSetBox();
            if (_graph != null) { _graph.ApplySettings(); _graph.RefreshLists(); }
            if (_dashboard != null) _dashboard.UpdateAll();

            if (!string.Equals(themeBefore, _state.Settings.Theme, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "테마는 프로그램을 다시 켜면 화면 전체에 적용됩니다.",
                    "테마 바뀜", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveSettings()
        {
            _state.Settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            if (WindowState == FormWindowState.Normal)
            {
                _state.Settings.WindowWidth = ClientSize.Width;
                _state.Settings.WindowHeight = ClientSize.Height;
            }
            SettingsStore.Save(_state.Settings);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateDpiFromHandle();

            if (PendingCommandLine != null)
            {
                bool before = true;
                foreach (string a in PendingCommandLine)
                {
                    if (!File.Exists(a) || !LogReader.IsSupported(a)) continue;
                    if (LoadInto(a, before)) before = false;
                }
                PendingCommandLine = null;
                _state.RebuildView();
                RunCompare(false);
                AfterDataChanged();
                return;
            }

            // 지난번에 보던 세트를 그대로 다시 엽니다.
            LogSet s = _state.Settings.ActiveLogSet;
            if ((!string.IsNullOrEmpty(s.BeforePath) && File.Exists(s.BeforePath))
                || (!string.IsNullOrEmpty(s.AfterPath) && File.Exists(s.AfterPath)))
            {
                SwitchSet(_state.Settings.ActiveSet);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            base.OnFormClosing(e);
        }

        // ---------------- 모니터별 DPI ----------------

        private const int WM_DPICHANGED = 0x02E0;

        private void UpdateDpiFromHandle()
        {
            try
            {
                using (Graphics g = CreateGraphics())
                {
                    float scale = g.DpiX / 96f;
                    if (Math.Abs(scale - Dpi.Scale) > 0.01f) Dpi.Scale = scale;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                int newDpi = (int)((uint)m.WParam.ToInt64() & 0xFFFF);
                if (newDpi > 0) Dpi.Scale = newDpi / 96f;

                // 운영체제가 알려 준 새 창 자리로 옮깁니다.
                if (m.LParam != IntPtr.Zero)
                {
                    var r = (NativeRect)System.Runtime.InteropServices.Marshal
                        .PtrToStructure(m.LParam, typeof(NativeRect));
                    SetBounds(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                }
                // 직접 그리는 컨트롤은 Dpi.S() 를 다시 읽으므로 새로 그리기만 하면 됩니다.
                Invalidate(true);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1: ShowDashboard(); return true;
                case Keys.F2: ShowGraph(); return true;
                case Keys.Control | Keys.O: OpenLog(true); return true;
                case Keys.Control | Keys.Shift | Keys.O: OpenLog(false); return true;
                case Keys.F5: RunCompare(true); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
