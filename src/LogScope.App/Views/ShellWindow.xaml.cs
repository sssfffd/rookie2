using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LogScope.App.Services;
using LogScope.Core.Align;
using LogScope.App.ViewModels;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.Views
{
    /// <summary>
    /// 창 하나. 위에 진한 파랑 메뉴 줄, 아래에 알림 줄, 가운데에 세 화면
    /// (대시보드 / 그래프 / 히트맵) 이 들어갑니다. 켜면 대시보드가 먼저 나옵니다.
    ///
    /// 파일을 여는 일과 비교 계산은 여기서 시작합니다. 진행 창을 띄워야 해서
    /// 뷰모델이 아니라 창이 맡습니다.
    /// </summary>
    public partial class ShellWindow : Window
    {
        private readonly AppState _state;
        private readonly ShellVm _vm;
        private readonly string[] _startupArgs;
        private bool _started;

        public ShellWindow(AppState state, string[] args)
        {
            _state = state;
            _startupArgs = args;

            InitializeComponent();

            Width = Math.Max(MinWidth, state.Settings.WindowWidth);
            Height = Math.Max(MinHeight, state.Settings.WindowHeight);
            if (state.Settings.WindowMaximized) WindowState = WindowState.Maximized;

            _vm = new ShellVm(state);
            DataContext = _vm;

            _vm.SetChanged += OnSetChanged;
            _vm.PageChanged += OnPageChanged;

            Graph.Attach(state);
            Heatmap.Attach(state);

            Dashboard.IoActivated += OnDashboardIoActivated;
            Heatmap.CellOpened += OnHeatmapCellOpened;
            // 가로축·시간 맞추기는 그래프와 히트맵이 같은 것 하나를 씁니다.
            // 그래서 어느 화면을 거치지 않고 여기서 바로 받습니다.
            _vm.Align.Changed += OnAlignmentChanged;

            Loaded += OnLoaded;
        }

        // ---------------- 시작 ----------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started) return;
            _started = true;

            if (_startupArgs != null && _startupArgs.Length > 0)
            {
                bool before = true;
                foreach (string a in _startupArgs)
                {
                    if (!File.Exists(a) || !LogReader.IsSupported(a)) continue;
                    if (LoadInto(a, before)) before = false;
                }
                AfterLoad();
                return;
            }

            // 지난번에 보던 세트를 그대로 다시 엽니다.
            LogSet s = _state.Settings.ActiveLogSet;
            bool any = (!string.IsNullOrEmpty(s.BeforePath) && File.Exists(s.BeforePath))
                    || (!string.IsNullOrEmpty(s.AfterPath) && File.Exists(s.AfterPath));
            if (any) LoadSet();
        }

        // ---------------- 로그 세트 ----------------

        private void OnSetChanged(object sender, EventArgs e)
        {
            LoadSet();
            SaveSettings();
        }

        private void LoadSet()
        {
            LogSet s = _state.Settings.ActiveLogSet;

            _state.Before = null;
            _state.After = null;
            _state.Comparison = null;
            _state.BeforePath = string.Empty;
            _state.AfterPath = string.Empty;

            if (!string.IsNullOrEmpty(s.BeforePath) && File.Exists(s.BeforePath))
                LoadInto(s.BeforePath, true);
            if (!string.IsNullOrEmpty(s.AfterPath) && File.Exists(s.AfterPath))
                LoadInto(s.AfterPath, false);

            AfterLoad();
        }

        // ---------------- 로그 열기 ----------------

        private void OnOpenBefore(object sender, RoutedEventArgs e) { OpenLog(true); }
        private void OnOpenAfter(object sender, RoutedEventArgs e) { OpenLog(false); }

        private void OpenLog(bool before)
        {
            LogSet set = _state.Settings.ActiveLogSet;

            var dlg = new Microsoft.Win32.OpenFileDialog();
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

            if (dlg.ShowDialog(this) != true) return;
            if (!LoadInto(dlg.FileName, before)) return;

            AfterLoad();
            SaveSettings();
        }

        private void OnReload(object sender, RoutedEventArgs e)
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
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AfterLoad();
        }

        /// <summary>파일 하나를 배경 스레드에서 읽습니다. 성공하면 true.</summary>
        private bool LoadInto(string path, bool before)
        {
            var options = new OpenOptions();
            object result; Exception error;
            string title = (before ? "이전 로그를 읽는 중" : "이후 로그를 읽는 중")
                         + " — " + Path.GetFileName(path);

            BackgroundJob.Run(this, title,
                delegate (LoadProgress p) { return LogReader.Open(path, options, p); },
                out result, out error);

            if (error != null || result == null)
            {
                if (error is OperationCanceledException) return false;
                MessageBox.Show(this,
                    "로그를 읽지 못했습니다.\n\n" + path + "\n\n"
                    + (error != null ? error.Message : "알 수 없는 오류"),
                    "열기 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var ds = (LogDataset)result;
            LogSet set = _state.Settings.ActiveLogSet;
            if (before) { _state.Before = ds; _state.BeforePath = path; set.BeforePath = path; }
            else { _state.After = ds; _state.AfterPath = path; set.AfterPath = path; }
            return true;
        }

        // ---------------- 비교 ----------------

        private void OnRecompare(object sender, RoutedEventArgs e)
        {
            Recompare();
            _vm.ReloadAll();
            Graph.OnDataChanged();
            Heatmap.Invalidate();
        }

        private void Recompare()
        {
            if (!_state.HasBoth)
            {
                _state.Comparison = null;
                return;
            }

            object result; Exception error;
            BackgroundJob.Run(this, "두 로그를 견주는 중",
                delegate (LoadProgress p) { _state.Recompare(p); return string.Empty; },
                out result, out error);

            if (error != null && !(error is OperationCanceledException))
            {
                MessageBox.Show(this, "비교하지 못했습니다.\n\n" + error.Message,
                    "비교 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 저장된 가로축 IO 와 시간 맞추기 IO 를 다시 적용합니다.
        ///
        /// 차례가 중요합니다 — 가로축을 갈아 끼우면 시간 값 자체가 바뀌므로
        /// <b>맞추기는 반드시 그 뒤</b>에 해야 합니다. 그리고 둘 다 끝난
        /// 뒤에 견줘야 합니다.
        ///
        /// 설정에 적힌 IO 가 이번 로그에는 없거나 조건에 안 맞을 수 있습니다.
        /// 그럴 때는 조용히 넘어가지 않고 왜 안 됐는지 알립니다 — 어긋난
        /// 그래프를 맞는 줄 알고 보는 것보다 낫습니다.
        /// </summary>
        private void ReapplyAxisAndAlign()
        {
            string axisProblem = _state.ApplyAxisChannel();
            if (axisProblem.Length > 0)
            {
                MessageBox.Show(this,
                    "가로축을 그 IO 로 둘 수 없어 로그의 시간축으로 되돌렸습니다.\n\n" + axisProblem,
                    "가로축", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 기준 IO 를 안 골랐어도 반드시 부릅니다. 그래야 예전에 맞춰
            // 뒀던 밀기 값이 지워지고 시작 시각 자동 맞춤이 돌아옵니다.
            AlignResult r = _state.ApplyTriggerAlign();
            if (!r.Ok && _state.HasBoth)
            {
                MessageBox.Show(this,
                    "시간을 맞추지 못했습니다.\n\n" + r.Message,
                    "시간 맞추기", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _vm.Align.Reload();
        }

        /// <summary>도구 줄에서 가로축이나 맞추기 기준을 바꿨을 때.</summary>
        private void OnAlignmentChanged(object sender, EventArgs e)
        {
            ReapplyAxisAndAlign();
            Recompare();
            _vm.ReloadAll();
            Graph.OnDataChanged();

            // 히트맵을 보고 있는 중이라면 바로 다시 계산합니다. 비워만 두면
            // 맞추기를 바꾼 그 자리에서 화면이 텅 비고, 왜 비었는지도
            // 알 수 없습니다. 다른 화면에 있으면 비워만 두고, 히트맵으로
            // 넘어올 때 OnPageChanged 가 계산합니다.
            Heatmap.Invalidate();
            if (_vm.Page == ShellVm.PageHeatmap && _state.HasBoth) Heatmap.Recalculate(false);

            SaveSettings();
        }

        private void AfterLoad()
        {
            ReapplyAxisAndAlign();
            Recompare();
            _vm.ReloadAll();
            Graph.OnDataChanged();
            Heatmap.Invalidate();
        }

        // ---------------- 화면 사이 이동 ----------------

        private void OnPageChanged(object sender, EventArgs e)
        {
            // 히트맵 화면을 처음 열면 한 번 계산해 둡니다.
            if (_vm.Page == ShellVm.PageHeatmap && Heatmap.Map.Result == null && _state.HasBoth)
                Heatmap.Recalculate(false);
        }

        private void OnDashboardIoActivated(object sender, DashboardView.IoEventArgs e)
        {
            _vm.GoGraph();
            Graph.FocusOnIo(e.Name);
        }

        private void OnHeatmapCellOpened(object sender, HeatmapView.CellEventArgs e)
        {
            _vm.GoGraph();
            Graph.FocusOnIo(e.Name);
            // 그 칸의 시간대로 확대해 줍니다. 앞뒤로 조금 여유를 둡니다.
            double pad = (e.End - e.Start) * 0.25;
            Graph.SetTimeRange(e.Start - pad, e.End + pad);
        }

        // ---------------- 설정 ----------------

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_state.Settings);
            dlg.Owner = this;
            dlg.ShowDialog();

            SaveSettings();
            _vm.RefreshSetNames();
            _vm.Dashboard.Refresh();
            _vm.Graph.Reload();
            Graph.OnDataChanged();
        }

        private void SaveSettings()
        {
            _state.Settings.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                _state.Settings.WindowWidth = (int)Math.Round(Width);
                _state.Settings.WindowHeight = (int)Math.Round(Height);
            }
            SettingsStore.Save(_state.Settings);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            base.OnClosing(e);
        }

        // ---------------- 단축키 ----------------

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            switch (e.Key)
            {
                case Key.F1: _vm.GoDashboard(); e.Handled = true; break;
                case Key.F2: _vm.GoGraph(); e.Handled = true; break;
                case Key.F3: _vm.GoHeatmap(); e.Handled = true; break;
                case Key.F5: OnRecompare(this, null); e.Handled = true; break;
                case Key.O:
                    if (ctrl) { OpenLog(!shift); e.Handled = true; }
                    break;
            }
        }
    }
}
