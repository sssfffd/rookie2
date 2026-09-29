using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

            _vm.SetChanged += OnSetChanged;
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

        // ---------------- 파일 끌어다 놓기 ----------------
        //
        // 파일이 창에 들어오는 순간 화면을 반으로 갈라 크게 보여 줍니다.
        // 왼쪽 = 이전, 오른쪽 = 이후. 가는 글씨 한 줄을 겨냥해서 놓게 하면
        // 어디가 어느 쪽인지 알 수가 없어서 이렇게 했습니다.
        //
        // 메인 화면의 이전/이후 줄도 그대로 놓을 수 있습니다 (안내 판이
        // 안 뜬 경우를 위해 남겨 뒀습니다).

        private void OnFileDragEnter(object sender, DragEventArgs e)
        {
            ShowDropOverlay(Dropped(e));
        }

        private void OnFileDragOver(object sender, DragEventArgs e)
        {
            List<string> files = Dropped(e);
            e.Effects = files.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;

            // DragEnter 를 놓쳤을 때(다른 창에서 바로 들어온 경우 등)를 위해
            // 여기서도 한 번 열어 둡니다.
            if (files.Count > 0 && DropOverlay.Visibility != Visibility.Visible) ShowDropOverlay(files);
            e.Handled = true;
        }

        /// <summary>
        /// 자식 사이를 지날 때도 DragLeave 가 여기까지 올라옵니다. 그때마다
        /// 안내 판을 닫으면 깜빡이므로, <b>창 밖으로 나갔을 때만</b> 닫습니다.
        /// </summary>
        private void OnFileDragLeave(object sender, DragEventArgs e)
        {
            Point p = e.GetPosition(this);
            if (p.X <= 0.5 || p.Y <= 0.5 || p.X >= ActualWidth - 0.5 || p.Y >= ActualHeight - 0.5)
                HideDropOverlay();
        }

        private void OnFileDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            HideDropOverlay();
            TakeFiles(Dropped(e), null);
        }

        /// <summary>
        /// 끌기가 끝났는데 안내 판이 남아 있는 경우를 위한 보험입니다.
        /// 끌고 있는 동안에는 MouseMove 가 오지 않으므로(마우스를 끌기 원본이
        /// 잡고 있습니다), MouseMove 가 왔다는 것은 끌기가 끝났다는 뜻입니다.
        /// </summary>
        private void OnWindowMouseMove(object sender, MouseEventArgs e)
        {
            if (DropOverlay.Visibility == Visibility.Visible) HideDropOverlay();
        }

        private void ShowDropOverlay(List<string> files)
        {
            if (files.Count == 0) return;

            DropTitle.Text = files.Count == 1
                ? "끌고 온 파일 : " + Path.GetFileName(files[0])
                : "끌고 온 파일 " + files.Count + " 개 : "
                  + Path.GetFileName(files[0]) + ", " + Path.GetFileName(files[1])
                  + (files.Count > 2 ? " …" : string.Empty);

            DropZoneBeforeNow.Text = SlotNote(_state.BeforePath, _state.Before != null);
            DropZoneAfterNow.Text = SlotNote(_state.AfterPath, _state.After != null);

            DropHint.Text = files.Count >= 2
                ? "두 개를 한꺼번에 놓으면 어느 쪽에 놓아도 이름 순으로 앞의 것이 이전, 다음이 이후로 들어갑니다."
                : "창 밖으로 끌고 나가면 취소됩니다.";

            Lit(null);
            DropOverlay.Visibility = Visibility.Visible;
        }

        private void HideDropOverlay()
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>그 자리에 지금 무엇이 들어 있는지. 덮어쓰게 되면 그것도 알립니다.</summary>
        private static string SlotNote(string path, bool loaded)
        {
            if (!loaded) return "지금 비어 있습니다";
            string name = string.IsNullOrEmpty(path) ? "열어 둔 로그" : Path.GetFileName(path);
            return "지금 : " + name + "\n놓으면 이것을 덮어씁니다";
        }

        /// <summary>어느 쪽에 올려 뒀는지 밝혀 줍니다. null 이면 둘 다 끕니다.</summary>
        private void Lit(Border hot)
        {
            var zone = (Brush)FindResource("Brush.DropZone");
            var zoneHot = (Brush)FindResource("Brush.DropZoneHot");

            DropZoneBefore.Background = ReferenceEquals(hot, DropZoneBefore) ? zoneHot : zone;
            DropZoneAfter.Background = ReferenceEquals(hot, DropZoneAfter) ? zoneHot : zone;
            DropZoneBefore.BorderThickness = new Thickness(ReferenceEquals(hot, DropZoneBefore) ? 4 : 2);
            DropZoneAfter.BorderThickness = new Thickness(ReferenceEquals(hot, DropZoneAfter) ? 4 : 2);
        }

        private void OnZoneDragOver(object sender, DragEventArgs e)
        {
            // e.Handled 을 두지 않습니다. 창까지 올라가야 DragLeave 로 판을
            // 닫는 판단을 할 수 있습니다.
            Lit(sender as Border);
            e.Effects = Dropped(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnZoneDragLeave(object sender, DragEventArgs e)
        {
            Lit(null);
        }

        /// <summary>
        /// 반쪽에 놓았을 때. <b>여기서 e.Handled 을 반드시 세웁니다</b> —
        /// 안 세우면 창의 Drop 까지 올라가서 같은 파일을 두 번 읽습니다.
        /// </summary>
        private void OnZoneDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            HideDropOverlay();

            var zone = sender as FrameworkElement;
            string tag = zone == null ? null : zone.Tag as string;
            TakeFiles(Dropped(e), tag == "before" ? true : tag == "after" ? (bool?)false : null);
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

        // ---- 왼쪽 위 [LogScope] 의 화면 목록 ----
        //
        // 팝업은 바깥을 누르면 닫히는데, 그 "바깥" 에는 팝업을 연 단추도
        // 들어갑니다. 팝업이 먼저 그 누름을 받아 닫고 그 다음에 Click 이
        // 도착해 다시 엽니다. 그래서 방금 닫혔으면 그 한 번은 무시합니다.
        // (가로축·시간 맞추기 판과 같은 자리입니다.)
        private DateTime _brandClosedAt;

        private void OnBrandMenuClosed(object sender, EventArgs e)
        {
            _brandClosedAt = DateTime.UtcNow;
        }

        private void OnBrandClick(object sender, RoutedEventArgs e)
        {
            if ((DateTime.UtcNow - _brandClosedAt).TotalMilliseconds < 250) return;
            BrandMenu.IsOpen = true;
        }

        private void OnGoScreen(object sender, RoutedEventArgs e)
        {
            BrandMenu.IsOpen = false;

            var b = sender as System.Windows.Controls.Button;
            if (b == null) return;

            int n;
            if (!int.TryParse(b.Tag as string, System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out n)) return;
            _vm.Screen = n;
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
                // 셋 다 분석 1 의 화면이라, 다른 데 있으면 거기로 데려갑니다.
                case Key.F1: _vm.GoDashboard(); e.Handled = true; break;
                case Key.F2: _vm.GoGraph(); e.Handled = true; break;
                case Key.F3: _vm.GoHeatmap(); e.Handled = true; break;
                case Key.Escape:
                    if (BrandMenu.IsOpen) { BrandMenu.IsOpen = false; e.Handled = true; }
                    else if (_vm.Screen != ShellVm.ScreenMain) { _vm.GoMain(); e.Handled = true; }
                    break;
                case Key.F5: OnRecompare(this, null); e.Handled = true; break;
                case Key.O:
                    if (ctrl) { OpenLog(!shift); e.Handled = true; }
                    break;
            }
        }
    }
}
