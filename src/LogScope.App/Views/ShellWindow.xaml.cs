using System;
using System.Collections.Generic;
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

            // 창 제목도 appname.txt 를 봅니다. XAML 에 적어 두면 이름을
            // 바꿀 때 한 군데가 남습니다.
            Title = _vm.WindowTitle;

            _vm.PageChanged += OnPageChanged;
            _vm.ScreenChanged += OnScreenChanged;
            Main.AnalysisOpened += OnAnalysisOpened;
            Main.GroupOpened += OnGroupOpened;
            Main.LogDropped += OnLogDropped;

            Graph.Attach(state);
            Heatmap.Attach(state);

            Dashboard.IoActivated += OnDashboardIoActivated;
            Heatmap.CellOpened += OnHeatmapCellOpened;
            // 가로축·시간 맞추기는 그래프와 히트맵이 같은 것 하나를 씁니다.
            // 그래서 어느 화면을 거치지 않고 여기서 바로 받습니다.
            _vm.Align.Changed += OnAlignmentChanged;

            // 그룹을 고치면 그 자리에서 저장합니다.
            //
            // 전에는 이 이벤트를 아무도 듣지 않았습니다. 그래서 그룹을 만들고
            // IO 를 넣어 둔 것이, 로그를 다시 열거나 시간 맞추기를 바꾸거나
            // 창을 닫을 때까지 파일에 안 적혀 있었습니다. 그 전에 프로그램이
            // 죽으면(작업 관리자로 끄거나 전원이 나가면) 묶어 둔 것이 전부
            // 사라집니다. 그룹 나누기는 한 번에 몇십 개를 손으로 넣는 일이라
            // 그게 제일 아까운 자리입니다.
            _vm.Graph.List.GroupsChanged += OnGroupsChanged;

            // DB 화면은 XAML 이 직접 만들어 제 뷰모델을 들고 있습니다.
            // 설정은 여기서 붙여 주고, 거기서 고른 자리는 여기서 저장합니다.
            // 그룹이 groups.txt 로 정해졌는지 알려 줍니다. Core 는 어느 파일이
            // 이겼는지 모르고, 그걸 아는 곳은 켜는 자리(App)뿐입니다.
            _vm.Graph.GroupsFromFile = App.GroupsFromFile;

            Db.Attach(_state);
            Db.PathsChanged += OnDbPathsChanged;

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

            // 지난번에 보던 로그는 저절로 열지 않습니다.
            //
            // 로그가 1000 만 칸까지 갑니다. 켤 때마다 몇십 초씩 읽고 앉아
            // 있는데 정작 열고 싶은 것은 대개 다른 파일입니다. 열려면
            // [이전 로그 열기] / [이후 로그 열기] 를 누르거나, 파일을 메인
            // 화면의 칸에 끌어다 놓으면 됩니다.
            //
            // 경로는 설정에 그대로 남습니다 — 파일 열기 창이 그 폴더에서
            // 시작하는 데 씁니다.
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

        // ---------------- 파일 끌어다 놓기 ----------------
        //
        // 놓을 자리는 메인 화면의 이전 / 이후 두 칸입니다. 칸 자체가
        // 과녁이고, 커서를 올리면 그 칸이 밝아집니다 (MainView.Lit).
        //
        // 여기(창 전체)는 그 밖에 놓았을 때를 받습니다. 다른 화면(분석 1·2·3)
        // 에서도 놓을 수 있어야 하니까요. 어느 쪽인지 알 수 없으므로 아래
        // TakeFiles 의 규칙을 따릅니다.

        private void OnFileDragOver(object sender, DragEventArgs e)
        {
            e.Effects = Dropped(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnFileDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            TakeFiles(Dropped(e), null);
        }

        /// <summary>
        /// 끌어 온 것 중 <b>읽을 수 있는 로그 파일</b>만 골라 이름 순으로
        /// 돌려줍니다.
        ///
        /// 이름 순인 이유가 있습니다. 두 개를 한꺼번에 놓으면 앞의 것을
        /// 이전으로 봐야 하는데, 이 프로그램은 파일 이름 앞에 날짜가 붙는다는
        /// 관행을 이미 쓰고 있습니다 (발생시간). 그러면 이름 순이 곧 시간
        /// 순입니다. 파일의 수정 시각으로 정하면 복사만 해도 뒤바뀝니다.
        /// </summary>
        private static List<string> Dropped(DragEventArgs e)
        {
            var list = new List<string>();
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return list;

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return list;

            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (!File.Exists(p) || !LogReader.IsSupported(p)) continue;
                list.Add(p);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// 끌어다 놓은 파일을 받습니다.
        ///
        /// <paramref name="slot"/> 이 정해져 있으면(메인 화면의 줄에 놓은
        /// 경우) 그 자리에 넣습니다. 정해지지 않았으면
        ///
        ///   두 개 이상 : 앞의 것을 이전, 다음을 이후
        ///   한 개      : 비어 있는 자리에. 둘 다 차 있으면 <b>물어봅니다</b>
        ///
        /// 자리가 정해져 있는데 두 개를 놓았으면 둘째는 반대쪽으로 갑니다.
        ///
        /// 둘 다 차 있을 때 말없이 한쪽을 덮으면, 방금 덮인 것이 무엇이었는지
        /// 알 수가 없습니다. 짐작하지 않고 묻습니다.
        /// </summary>
        private void TakeFiles(List<string> files, bool? slot)
        {
            if (files.Count == 0) return;

            bool any = false;
            if (slot.HasValue)
            {
                any = LoadInto(files[0], slot.Value);
                // 자리를 정해 놓고 두 개를 놓았으면 둘째는 반대쪽입니다.
                // 버리면 왜 하나만 들어왔는지 알 수가 없습니다.
                if (files.Count >= 2) any |= LoadInto(files[1], !slot.Value);
            }
            else if (files.Count >= 2)
            {
                any = LoadInto(files[0], true);
                any |= LoadInto(files[1], false);
            }
            else if (_state.Before == null)
            {
                any = LoadInto(files[0], true);
            }
            else if (_state.After == null)
            {
                any = LoadInto(files[0], false);
            }
            else
            {
                MessageBoxResult r = MessageBox.Show(this,
                    Path.GetFileName(files[0]) + "\n\n어느 쪽으로 넣을까요?\n\n"
                    + "[예] 이전 로그      [아니오] 이후 로그",
                    "로그 넣기", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                any = LoadInto(files[0], r == MessageBoxResult.Yes);
            }

            if (!any) return;
            AfterLoad();
            SaveSettings();
        }

        /// <summary>메인 화면의 이전/이후 줄에 놓았을 때.</summary>
        private void OnLogDropped(object sender, MainView.LogDropEventArgs e)
        {
            TakeFiles(e.Files, e.Before);
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
        /// <summary>
        /// 그룹이 바뀌었을 때. 설정만 저장하고 화면은 건드리지 않습니다 —
        /// 목록은 이미 제 손으로 바꿔 놓았고, 저장은 임시 파일에 쓰고
        /// 바꿔치는 방식이라 중간에 죽어도 반쪽짜리가 남지 않습니다.
        /// </summary>
        private void OnGroupsChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        /// <summary>DB 화면에서 견줄 자리를 고쳤을 때. 그 자리에서 저장합니다.</summary>
        private void OnDbPathsChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

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
            EnsureHeatmap();
        }

        /// <summary>큰 화면이 바뀌었을 때.</summary>
        private void OnScreenChanged(object sender, EventArgs e)
        {
            // 분석 1 로 돌아왔는데 히트맵을 보고 있었다면 다시 계산해 둡니다.
            // 화면을 옮겨 다니는 사이에 맞추기나 허용 오차가 바뀌었을 수 있습니다.
            EnsureHeatmap();
        }

        /// <summary>
        /// 히트맵 화면을 보고 있는데 아직 계산이 없으면 한 번 돌립니다.
        /// 큰 화면과 안쪽 화면 둘 다 이 자리를 거칩니다 — 조건이 같은데
        /// 두 군데에 적어 두면 한쪽만 고치게 됩니다.
        /// </summary>
        private void EnsureHeatmap()
        {
            if (_vm.Screen != ShellVm.ScreenAnalysis1) return;
            if (_vm.Page != ShellVm.PageHeatmap) return;
            if (Heatmap.Map.Result != null || !_state.HasBoth) return;
            Heatmap.Recalculate(false);
        }

        /// <summary>메인 화면에서 분석 칸의 이동 화살표를 눌렀을 때.</summary>
        private void OnAnalysisOpened(object sender, MainView.AnalysisEventArgs e)
        {
            _vm.GoAnalysis(e.Number);
        }

        /// <summary>
        /// 메인 화면 요약에서 그룹을 눌렀을 때. 그 그룹의 IO 만 히트맵에
        /// 남기고 그리로 넘어갑니다.
        ///
        /// 넘어가기 <b>전에</b> 추립니다. 먼저 화면을 바꾸면 OnPageChanged 가
        /// 추리기 전의 조건으로 한 번 계산해 버려서, 전체를 그렸다가 곧바로
        /// 다시 그리게 됩니다.
        /// </summary>
        private void OnGroupOpened(object sender, MainView.GroupEventArgs e)
        {
            if (!_state.HasBoth) return;

            _vm.Screen = ShellVm.ScreenAnalysis1;
            _vm.GoHeatmap();
            Heatmap.FocusGroup(e.GroupName, e.Members);
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
            // 열어 둔 로그의 IO 이름을 같이 넘깁니다. IO 별 허용 오차를
            // 걸 때 이름을 손으로 적지 않고 고를 수 있게.
            var dlg = new SettingsWindow(_state.Settings, _state.IoNames());
            dlg.Owner = this;
            dlg.ShowDialog();

            SaveSettings();
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
                // 셋 다 분석 1 의 화면이라, 다른 데 있으면 거기로 데려갑니다.
                case Key.F1: _vm.GoDashboard(); e.Handled = true; break;
                case Key.F2: _vm.GoGraph(); e.Handled = true; break;
                case Key.F3: _vm.GoHeatmap(); e.Handled = true; break;
                case Key.Escape:
                    if (_vm.Screen != ShellVm.ScreenMain) { _vm.GoMain(); e.Handled = true; }
                    break;
                case Key.F5: OnRecompare(this, null); e.Handled = true; break;
                case Key.O:
                    if (ctrl) { OpenLog(!shift); e.Handled = true; }
                    break;
            }
        }
    }
}
