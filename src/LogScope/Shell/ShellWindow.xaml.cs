using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LogScope;
using LogScope.Analysis1.Align;
using LogScope.Analysis1.Dashboard;
using LogScope.Analysis1.Heatmap;
using LogScope.Analysis1.LogFile;
using LogScope.Analysis2;
using LogScope.Common;
using LogScope.Main;
using LogScope.Settings;

namespace LogScope.Shell
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

            // 분석 3 의 지난 자리를 <b>화면을 만들기 전에</b> 되살립니다 — 분석 3 화면은
            // 아래 InitializeComponent 안에서 만들어지므로, 그 생성자가 자리를 읽을 수
            // 있어야 합니다. 알리지는 않습니다 (켜자마자 저절로 돌지 않게).
            Analysis3Link.Restore(state.Settings.A3BeforePath, state.Settings.A3AfterPath);

            InitializeComponent();

            LoadAnalysis3();

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
            Main.IoOpened += OnMainIoOpened;
            Main.LogPickRequested += OnMainLogPick;
            Main.DbPickRequested += OnMainDbPick;
            Main.LogDropped += OnLogDropped;
            Main.DbDropped += OnMainDbDropped;
            Main.A3PickRequested += OnMainA3Pick;
            Main.A3Dropped += OnMainA3Dropped;
            Main.A3AnalyzeRequested += OnMainA3Analyze;
            Main.LogAnalyzeRequested += OnMainLogAnalyze;
            Main.DbAnalyzeRequested += OnMainDbAnalyze;
            Main.LlmRunRequested += OnLlmRun;
            Main.LlmCancelRequested += OnLlmCancel;

            // LLM 분석 기능을 꽂습니다 (1.45) — Main/LlmSetup.cs 의 한 줄.
            LlmSetup.Register();

            // LLM 서버에 닿는지 (1.51). 확인은 배경 스레드에서 끝나므로 화면 스레드로 넘겨 다시 그립니다.
            // 안 닿으면 분석 3 화면 · 메인 분석 3 칸 · 종합 분석 결과가 회색으로 덮입니다.
            LlmServer.Changed += OnLlmServerChanged;

            Graph.Attach(state);
            Heatmap.Attach(state);

            Dashboard.IoActivated += OnDashboardIoActivated;
            Heatmap.CellOpened += OnHeatmapCellOpened;
            // 시간 맞추기는 그래프와 히트맵이 같은 것 하나를 씁니다.
            // 그래서 어느 화면을 거치지 않고 여기서 바로 받습니다.
            _vm.Align.Changed += OnAlignmentChanged;
            _vm.Step.Changed += OnStepChanged;

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
            Db.ResultChanged += OnDbResultChanged;
            _vm.Judge.Changed += OnJudgeChanged;

            // 분석 3 은 갈아 끼우는 폴더라 그 안의 클래스를 직접 부르지 않고
            // Common/Analysis3Link 게시판으로만 주고받습니다 (1.29).
            Analysis3Link.PathsChanged += OnA3PathsChanged;
            Analysis3Link.ResultChanged += OnA3ResultChanged;

            Loaded += OnLoaded;
        }

        // ---------------- 시작 ----------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started) return;
            _started = true;

            // 켤 때 한 번, 지금 설정의 주소로 LLM 서버 연결 확인 (1.51) — 막지 않고 뒤에서.
            LlmServer.CheckAsync(_state.Settings.LlmServer);

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
            string path = ChooseLog(before);
            if (path == null) return;
            if (!LoadInto(path, before)) return;

            AfterLoad();
            SaveSettings();
        }

        /// <summary>
        /// 로그 고르는 창. 고른 경로, 안 골랐으면 null. 바로 읽는 [이전 로그 열기] 와
        /// 자리만 적는 메인 칸 (1.53) 이 같이 씁니다 — 고르는 규칙이 한 곳.
        /// </summary>
        private string ChooseLog(bool before)
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

            if (dlg.ShowDialog(this) != true) return null;
            return dlg.FileName;
        }

        /// <summary>파일 하나를 배경 스레드에서 읽습니다. 성공하면 true.</summary>
        private bool LoadInto(string path, bool before)
        {
            var options = new OpenOptions();
            // 단계를 담은 IO 이름. 비어 있으면 단계를 찾지 않습니다.
            // 로그를 읽는 그 자리에서 단계까지 찾습니다 — 나중에 따로
            // 찾으면 "읽었는데 단계가 없는" 상태가 잠깐 생기고, 그 틈에
            // 그려지는 화면은 단계가 없는 화면입니다.
            options.StepIo = _state.Settings.StepIo;
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

            // 로그가 바뀌면 단계 짝 목록을 버립니다. 안 버리면 새 로그에
            // 없는 단계가 목록에 남고, 그걸 고르면 옛 로그의 시각으로
            // 그래프가 움직입니다.
            _state.ForgetSteps();
            _state.StepPick = -1;
            // "이번만 허용" 은 그 로그 짝에서만 (1.56).
            _state.AllowOnce.Clear();

            LogSet set = _state.Settings.ActiveLogSet;
            if (before) { _state.Before = ds; _state.BeforePath = path; set.BeforePath = path; }
            else { _state.After = ds; _state.AfterPath = path; set.AfterPath = path; }

            // 메인 칸에 [분석] 을 기다리던 파일이 있었으면 이제 읽었으니 지웁니다 (1.53).
            // 분석 1 화면에서 다른 파일을 바로 열었어도 지웁니다 — 남겨 두면 나중에 [분석] 이
            // 방금 연 것을 옛 자리로 덮습니다.
            _vm.Main.ForgetWaitingLog(before);
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
            // 메인 화면이면 자리만 적고 [분석] 을 기다립니다 (1.53). 다른 화면은 바로 읽습니다.
            TakeFiles(Dropped(e), null, _vm.Screen == ShellVm.ScreenMain);
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
            return Readable(paths);
        }

        /// <summary>읽을 수 있는 로그 파일만, 이름 순으로 (위 설명).</summary>
        private static List<string> Readable(IEnumerable<string> paths)
        {
            var list = new List<string>();
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
        ///
        /// <paramref name="wait"/> 이면 (메인 화면, 1.53) 읽지 않고 자리만 적습니다 —
        /// 칸 머리의 [분석] 이 읽습니다. 비어 있는 자리는 기다리는 파일까지 보고 셉니다.
        /// </summary>
        private void TakeFiles(List<string> files, bool? slot, bool wait)
        {
            if (files.Count == 0) return;

            bool any = false;
            if (slot.HasValue)
            {
                any = Put(files[0], slot.Value, wait);
                // 자리를 정해 놓고 두 개를 놓았으면 둘째는 반대쪽입니다.
                // 버리면 왜 하나만 들어왔는지 알 수가 없습니다.
                if (files.Count >= 2) any |= Put(files[1], !slot.Value, wait);
            }
            else if (files.Count >= 2)
            {
                any = Put(files[0], true, wait);
                any |= Put(files[1], false, wait);
            }
            else if (!Filled(true, wait))
            {
                any = Put(files[0], true, wait);
            }
            else if (!Filled(false, wait))
            {
                any = Put(files[0], false, wait);
            }
            else
            {
                MessageBoxResult r = MessageBox.Show(this,
                    Path.GetFileName(files[0]) + "\n\n어느 쪽으로 넣을까요?\n\n"
                    + "[예] 이전 로그      [아니오] 이후 로그",
                    "로그 넣기", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return;
                any = Put(files[0], r == MessageBoxResult.Yes, wait);
            }

            if (!any || wait) return;
            AfterLoad();
            SaveSettings();
        }

        /// <summary>읽거나 (wait 아님) 메인 칸에 자리만 적습니다 (wait).</summary>
        private bool Put(string path, bool before, bool wait)
        {
            if (!wait) return LoadInto(path, before);
            _vm.Main.SetWaitingLog(before, path);
            return true;
        }

        /// <summary>그 자리가 차 있는지. wait 면 [분석] 을 기다리는 파일도 찬 것으로 봅니다.</summary>
        private bool Filled(bool before, bool wait)
        {
            if (wait) return _vm.Main.HasLogFor(before);
            return (before ? _state.Before : _state.After) != null;
        }

        /// <summary>
        /// 메인 화면의 로그 칸을 <b>눌렀을</b> 때. 파일 고르는 창을 띄웁니다
        /// — 위쪽 [이전 로그 열기] 와 같은 길입니다 (고르는 규칙이 한 곳).
        /// </summary>
        private void OnMainLogPick(object sender, MainView.PickEventArgs e)
        {
            // 1.53: 고르기만 하고 읽지 않습니다 — 칸 머리의 [분석] 이 읽습니다.
            string path = ChooseLog(e.Before);
            if (path != null) _vm.Main.SetWaitingLog(e.Before, path);
        }

        /// <summary>
        /// 분석 1 칸 머리의 [분석] (1.53). 기다리던 로그를 읽고 견줍니다.
        ///
        ///   기다리는 파일이 있음  → 그 쪽(들)만 읽고 견줌 (이미 읽은 쪽은 다시 안 읽음)
        ///   둘 다 이미 읽었음     → 다시 견주기만 (분석 1 화면의 [다시 비교] 와 같음)
        ///   한쪽이라도 비었음     → 알리고 끝
        ///
        /// 못 읽은 쪽은 기다리는 자리를 그대로 둡니다 — 파일을 고친 뒤 다시 누르면 됩니다.
        /// </summary>
        private void OnMainLogAnalyze(object sender, EventArgs e)
        {
            if (!_vm.Main.HasLogFor(true) || !_vm.Main.HasLogFor(false))
            {
                MessageBox.Show(this, MainTexts.LogNeedBoth, MainTexts.Label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool waited = false, any = false;
            foreach (bool before in new[] { true, false })
            {
                string path = _vm.Main.WaitingLog(before);
                if (path.Length == 0) continue;
                waited = true;
                if (LoadInto(path, before)) any = true;
            }

            if (any)
            {
                AfterLoad();
                SaveSettings();
            }
            else if (!waited)
            {
                OnRecompare(this, null);
            }
            _vm.Main.Refresh();
        }

        /// <summary>분석 2 칸 머리의 [분석] (1.53). 분석 2 화면의 [읽기] 와 같습니다.</summary>
        private void OnMainDbAnalyze(object sender, EventArgs e)
        {
            Db.ReadNow();
        }

        /// <summary>
        /// 메인 화면의 견줄 자리를 눌렀을 때. 분석 2 화면의 [파일] 과 같은
        /// 길입니다. 폴더는 끌어다 놓거나 분석 2 의 [폴더] 로 고릅니다.
        /// </summary>
        private void OnMainDbPick(object sender, MainView.PickEventArgs e)
        {
            // 1.53: 자리만 적습니다. 칸 머리의 [분석] 이 읽습니다.
            if (Db.PickFileFor(e.Before)) _vm.Main.SetDbWaiting(e.Before);
        }

        /// <summary>
        /// 메인 화면의 이전/이후 줄에 놓았을 때. 1.53 부터는 자리만 적고 [분석] 을 기다립니다.
        /// 읽을 수 있는 로그만 고릅니다 — 1.52 까지는 바로 읽어서 못 읽는 파일이면 그 자리에서
        /// 오류가 났는데, 이제는 [분석] 을 누를 때까지 모르게 되므로 놓을 때 거릅니다.
        /// </summary>
        private void OnLogDropped(object sender, MainView.FileDropEventArgs e)
        {
            List<string> files = Readable(e.Files);
            if (files.Count == 0)
            {
                MessageBox.Show(this, MainTexts.LogNotSupported, MainTexts.Label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            TakeFiles(files, e.Before, true);
        }

        /// <summary>
        /// 메인 화면의 <b>분석 2 칸</b>에 놓았을 때. 분석 2 화면으로 그대로
        /// 넘깁니다 — 자리를 적고 (둘 다 찼으면) 읽는 일은 거기서 합니다.
        ///
        /// 여기서 설정을 직접 고치지 않는 이유: 자리를 적는 곳이 둘이 되면
        /// 한쪽만 알림을 내보내는 날이 오고, 그러면 화면에는 들어갔는데
        /// 저장은 안 되는 일이 생깁니다.
        /// </summary>
        private void OnMainDbDropped(object sender, MainView.FileDropEventArgs e)
        {
            Db.TakePaths(e.Files, e.Before);
            if (e.Files.Count > 0) _vm.Main.SetDbWaiting(e.Before);
        }

        // ---------------- 분석 3 (1.29) ----------------
        //
        // 메인 칸에서 자리를 정하면 Analysis3Link 에 적기만 합니다. 읽고 돌리는 일은
        // 분석 3 화면이 PathsChanged 를 듣고 합니다 — 무엇을 돌릴지는 그 폴더만 압니다.

        /// <summary>
        /// 분석 3 화면에 <b>Attach</b> 가 있으면 불러 줍니다 (1.33). 화면을 만든 직후에 부릅니다.
        ///
        /// 분석 2 화면(DbView)이 <c>Attach(AppState)</c> 로 설정 · 로그를 받는 꼴이라, 분석 3 도
        /// 그 꼴로 만든 경우가 있습니다. 창은 갈아 끼우는 폴더의 클래스를 직접 모르므로 이름으로
        /// 찾습니다. 받는 꼴은 둘 — <c>public void Attach(AppState state)</c> (먼저 찾음) 또는
        /// <c>public void Attach()</c>. 꼴을 정해 찾으므로 Attach 가 여럿이어도
        /// AmbiguousMatchException 이 나지 않습니다. 없으면 아무것도 안 하고 false.
        ///
        /// 이 창 파일 하나에만 둡니다 (1.34) — 이 파일만 바꿔 끼워도 되게.
        /// Attach 안에서 난 오류는 TargetInvocationException 에 싸여 그대로 올라갑니다.
        /// </summary>
        public static bool CallAttach(object view, AppState state)
        {
            if (view == null) return false;
            Type t = view.GetType();
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            System.Reflection.MethodInfo m = t.GetMethod("Attach", flags, null, new[] { typeof(AppState) }, null);
            if (m != null)
            {
                m.Invoke(view, new object[] { state });
                return true;
            }

            m = t.GetMethod("Attach", flags, null, Type.EmptyTypes, null);
            if (m != null)
            {
                m.Invoke(view, null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 분석 3 화면을 찾아 자리에 넣습니다 (1.30).
        ///
        /// 사용자의 Analysis3 폴더는 저장소에 없어서, 새로 받은 직후에는 비어 있을 수
        /// 있습니다. 이름을 XAML 에 적으면 그때 빌드가 깨지므로, 빌드된 프로그램 안에서
        /// 이름으로 찾습니다. 없으면 자리 화면, 만들다 오류가 나면 그 까닭을 자리 화면에.
        /// 그 화면 오류로 프로그램 전체가 안 켜지는 것보다 낫습니다.
        /// </summary>
        private void LoadAnalysis3()
        {
            const string name = "LogScope.Analysis3.Analysis3View";
            Type t = typeof(ShellWindow).Assembly.GetType(name, false);
            if (t == null || !typeof(FrameworkElement).IsAssignableFrom(t))
            {
                A3Host.Content = new Analysis3Placeholder();
                return;
            }

            string step = "만들다";
            try
            {
                var view = (FrameworkElement)Activator.CreateInstance(t);

                // 자리에 먼저 넣고 Attach 를 부릅니다 — Attach 안에서 Window.GetWindow(this)
                // 같은 것을 쓰면 창 안에 들어 있어야 합니다.
                A3Host.Content = view;

                // Attach(AppState) 가 있으면 설정 · 로그를 넘겨 줍니다 (1.33, DbView 와 같은 꼴).
                // 이게 없던 1.32 까지는 Attach 로 받는 화면이 빈 상태로 돌다가 폴더를 열 때
                // 오류가 났습니다.
                step = "Attach 하다";
                CallAttach(view, _state);
            }
            catch (Exception ex)
            {
                // 생성자 · Attach 안에서 난 오류는 TargetInvocationException 에 싸여 옵니다.
                Exception inner = ex.InnerException ?? ex;
                A3Host.Content = new Analysis3Placeholder(
                    "Analysis3View 를 " + step + " 오류가 났습니다.\n" + inner.GetType().Name + ": " + inner.Message);
            }
        }

        /// <summary>
        /// 메인 화면 분석 3 칸을 눌렀을 때 — <b>폴더</b> 고르는 창 (1.35, 전에는 파일).
        ///
        /// WPF 에는 폴더 고르는 창이 없어 .NET Framework 의 FolderBrowserDialog 를 씁니다
        /// (설정 창과 같음 — 바깥 라이브러리가 아님). 파일 하나를 쓰려면 칸에 끌어다 놓으면 됩니다.
        /// </summary>
        private void OnMainA3Pick(object sender, MainView.PickEventArgs e)
        {
            string start = e.Before ? Analysis3Link.BeforePath : Analysis3Link.AfterPath;
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = (e.Before ? "이전" : "이후") + " 폴더 — " + AppConfig.Current.Title(3);
                dlg.ShowNewFolderButton = false;
                try
                {
                    // 지난번 자리가 파일이면 그 파일이 든 폴더에서 시작합니다.
                    if (start.Length > 0)
                    {
                        string dir = Directory.Exists(start) ? start : Path.GetDirectoryName(start);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.SelectedPath = dir;
                    }
                }
                catch (ArgumentException) { }
                catch (PathTooLongException) { }

                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                Analysis3Link.SetPath(e.Before, dlg.SelectedPath);   // PathsChanged 가 저장 · 다시 그리기
            }
        }

        /// <summary>
        /// 메인 화면 분석 3 칸의 [분석] (1.35). 무엇을 할지는 분석 3 폴더의 코드가
        /// Analysis3Link.AnalyzeRequested 에 연결해 정합니다. 아무도 안 들으면 알려 줍니다
        /// — 눌렀는데 아무 일도 없으면 고장으로 보입니다.
        /// </summary>
        private void OnMainA3Analyze(object sender, EventArgs e)
        {
            if (!Analysis3Link.RequestAnalyze()) SayNoA3Handler("Analysis3Link.AnalyzeRequested");
        }

        // ---------------- 종합 분석 결과 (LLM, 1.45) ----------------
        //
        // [LLM 분석 실행] → LlmRequest 를 만들어 배경 스레드에서 LlmLink.Analyzer.Analyze 를 부르고,
        // 진행 · 결과 · 오류를 화면 스레드로 넘겨 메인 칸(MainVm.Llm)에 적습니다. 진행 창(모달)을 쓰지 않는
        // 것은, LLM 이 수십 초~수 분 걸릴 수 있어 그동안 다른 화면을 볼 수 있게 하려는 것입니다.

        private LoadProgress _llmProgress;
        private int _llmRun;   // 몇 번째 실행인지 — 취소 뒤 늦게 온 결과가 새 실행을 덮지 않게

        private void OnLlmRun(object sender, EventArgs e)
        {
            LlmPanelVm panel = _vm.Main.Llm;
            if (panel.IsRunning) return;

            ILlmAnalyzer analyzer = LlmLink.Analyzer;
            if (analyzer == null)
            {
                MessageBox.Show(this, MainTexts.LlmNoAnalyzer, MainTexts.Label,
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LlmRequest request = _vm.Main.BuildLlmRequest(Db.Diff);
            if (!request.Any) return;
            string used = LlmPanelVm.UsedText(request.Use1, request.Use2, request.Use3);

            int run = ++_llmRun;
            panel.Start();
            var progress = new LoadProgress(delegate (string stage, double f)
            {
                Dispatcher.BeginInvoke(new Action(delegate { if (run == _llmRun) panel.Report(stage, f); }));
            });
            _llmProgress = progress;

            var thread = new System.Threading.Thread(delegate ()
            {
                string result = null;
                Exception error = null;
                try { result = analyzer.Analyze(request, progress); }
                catch (Exception ex) { error = ex; }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (run != _llmRun) return;
                    _llmProgress = null;
                    if (error is OperationCanceledException || (error == null && progress.IsCancelled)) panel.Cancelled();
                    else if (error != null) panel.Fail(error.GetType().Name + ": " + error.Message);
                    else panel.Finish(result, used, DateTime.Now);
                }));
            });
            thread.IsBackground = true;
            thread.Name = "LogScope LLM 분석";
            thread.Start();
        }

        /// <summary>[취소] — 분석 기능이 progress.IsCancelled 를 보고 멈춥니다. 안 보면 끝날 때까지 돕니다.</summary>
        private void OnLlmCancel(object sender, EventArgs e)
        {
            if (_llmProgress != null) _llmProgress.Cancel();
            _vm.Main.Llm.Report("취소하는 중 (분석 기능이 멈출 때까지)", -1);
        }

        /// <summary>LLM 서버 상태가 바뀜 (1.51) — 배경 스레드에서 올 수 있어 화면 스레드로.</summary>
        private void OnLlmServerChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate { _vm.RefreshLlmServer(); }));
        }

        private void SayNoA3Handler(string eventName)
        {
            MessageBox.Show(this, MainTexts.A3NoHandler + "\n\n연결할 곳: " + eventName,
                AppConfig.Current.Title(3), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 메인 화면 분석 3 칸에 놓았을 때. 분석 2 칸처럼 <b>첫 번째 것만</b> 그 자리에
        /// 적습니다 (파일도 폴더도 됨).
        /// </summary>
        private void OnMainA3Dropped(object sender, MainView.FileDropEventArgs e)
        {
            if (e.Files == null || e.Files.Count == 0) return;
            Analysis3Link.SetPath(e.Before, e.Files[0]);
        }

        /// <summary>분석 3 의 자리가 바뀌었을 때 — 저장하고 메인 칸을 다시 그립니다.</summary>
        private void OnA3PathsChanged(object sender, EventArgs e)
        {
            // 분석 3 화면이 배경 작업 안에서 SetPath 를 불러도 되게 화면 스레드로 넘깁니다.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnA3PathsChanged(sender, e)));
                return;
            }
            _state.Settings.A3BeforePath = Analysis3Link.BeforePath;
            _state.Settings.A3AfterPath = Analysis3Link.AfterPath;
            SaveSettings();
            _vm.Main.Refresh();
        }

        /// <summary>분석 3 이 결과를 올렸을 때 — 메인 칸을 다시 그립니다.</summary>
        private void OnA3ResultChanged(object sender, EventArgs e)
        {
            // Publish 는 배경 작업 안에서 불릴 수 있습니다 (BackgroundJob).
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnA3ResultChanged(sender, e)));
                return;
            }
            _vm.Main.Refresh();
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
        private void ReapplyAlign()
        {
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

        /// <summary>
        /// 그룹이 바뀌었을 때. 설정만 저장하고 화면은 건드리지 않습니다 —
        /// 목록은 이미 제 손으로 바꿔 놓았고, 저장은 임시 파일에 쓰고
        /// 바꿔치는 방식이라 중간에 죽어도 반쪽짜리가 남지 않습니다.
        /// </summary>
        private void OnGroupsChanged(object sender, EventArgs e)
        {
            SaveSettings();

            // 히트맵도 같은 그룹으로 나눕니다 (1.07). 다시 계산하지는 않고
            // 이미 센 줄을 새 그룹으로 늘어놓기만 합니다.
            Heatmap.RefreshGroups();
        }

        /// <summary>
        /// 견줄 자리가 바뀌었을 때 (분석 2 화면에서 고쳤든, 메인 화면 칸에
        /// 놓았든). 저장하고, <b>메인 화면 칸의 글도 다시 그립니다</b> —
        /// 자리는 설정 한 곳에 있으므로 다시 읽기만 하면 됩니다.
        /// </summary>
        private void OnDbPathsChanged(object sender, EventArgs e)
        {
            SaveSettings();
            _vm.Main.Refresh();
        }

        /// <summary>
        /// 분석 2 가 읽고 견준 뒤. 센 수를 상태에 옮기고 메인 화면을
        /// 다시 그립니다 — 메인 칸이 PASS · WARNING · FAIL 을 적습니다.
        ///
        /// 뷰모델이 상태를 직접 만지지 않고 창이 옮기는 것은 자리가
        /// 바뀔 때와 같은 방식입니다.
        /// </summary>
        /// <summary>
        /// 오차 목록에서 허용을 바꿨을 때 (1.56). "앞으로도 허용" 은 설정에 있으므로 저장하고,
        /// 메인 칸의 판정 · 개수를 다시 그립니다.
        /// </summary>
        private void OnJudgeChanged(object sender, EventArgs e)
        {
            SaveSettings();
            _vm.Main.Refresh();
        }

        private void OnDbResultChanged(object sender, EventArgs e)
        {
            // 읽고 견줬으면 메인 칸의 "분석 대기" 를 지웁니다 (1.53). 못 읽었으면 남깁니다.
            if (Db.HasTally) _vm.Main.ClearDbWaiting();
            _state.HasDbResult = Db.HasTally;
            _state.DbChanged = Db.Tally.Changed;
            _state.DbGone = Db.Tally.Gone;
            _state.DbAdded = Db.Tally.Added;
            _state.DbChangedIos = new List<DbChangedIo>(Db.ChangedValues);
            _vm.Main.Refresh();
        }

        private void OnAlignmentChanged(object sender, EventArgs e)
        {
            ReapplyAlign();
            Recompare();
            _vm.ReloadAll();
            Graph.OnDataChanged();

            // 단계를 고른 채로 축이나 맞추기를 바꿨을 수 있습니다. 위
            // OnDataChanged 가 구간을 전체로 되돌리므로 다시 걸어 줍니다 —
            // 안 걸면 고른 단계가 그대로인데 화면은 전체가 됩니다.
            Graph.ApplyStep();

            // 히트맵을 보고 있는 중이라면 바로 다시 계산합니다. 비워만 두면
            // 맞추기를 바꾼 그 자리에서 화면이 텅 비고, 왜 비었는지도
            // 알 수 없습니다. 다른 화면에 있으면 비워만 두고, 히트맵으로
            // 넘어올 때 OnPageChanged 가 계산합니다.
            Heatmap.Invalidate();
            if (_vm.Page == ShellVm.PageHeatmap && _state.HasBoth) Heatmap.Recalculate(false);

            SaveSettings();
        }

        /// <summary>
        /// 그래프에서 단계를 골랐을 때 (또는 [시작을 0 으로] 를 눌렀을 때).
        ///
        /// 차례가 중요합니다. <b>다시 견준 뒤에</b> 구간을 맞춰야 합니다 —
        /// 단계로 맞춘 밀기 값이 들어가야 이후 로그가 제자리에 오고,
        /// 그래야 보여 줄 구간도 맞습니다.
        ///
        /// 그래프는 <c>OnDataChanged</c> 로 되돌리지 않습니다. 그 함수는
        /// 보이는 구간을 전체로 되돌리는데, 단계를 고른 사람은 그 단계만
        /// 보려고 고른 것입니다.
        /// </summary>
        private void OnStepChanged(object sender, EventArgs e)
        {
            Recompare();
            _vm.ReloadAll();
            Graph.PushAll();
            Graph.ApplyStep();

            // 히트맵도 밀기 값이 달라졌으니 옛 그림입니다. 보고 있는 중이면
            // 다시 계산하고, 아니면 비워 둡니다 (넘어올 때 계산합니다).
            Heatmap.Invalidate();
            if (_vm.Page == ShellVm.PageHeatmap && _state.HasBoth) Heatmap.Recalculate(false);

            SaveSettings();
        }

        private void AfterLoad()
        {
            ReapplyAlign();
            Recompare();
            _vm.ReloadAll();
            Graph.OnDataChanged();

            // 로그가 바뀌면 고른 단계가 풀립니다 (ForgetSteps). 여기서
            // 걸어 주면 가로축 0 자리도 같이 풀립니다 — 안 풀면 옛 로그의
            // 단계 시작부터 센 눈금이 새 로그에 그대로 남습니다.
            Graph.ApplyStep();

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
        /// <summary>
        /// 메인 화면 분석 2 칸의 요약에서 IO 한 줄을 눌렀을 때.
        /// 그 화면으로 넘어가 그 줄을 골라 줍니다.
        /// </summary>
        private void OnMainIoOpened(object sender, MainView.IoEventArgs e)
        {
            // 분석 3 칸의 줄이면 분석 3 화면으로 가서, 무엇을 눌렀는지 게시판으로 알립니다.
            if (e.Card == 3)
            {
                _vm.Screen = ShellVm.ScreenAnalysis3;
                Analysis3Link.RequestOpen(e.Io);
                return;
            }

            _vm.Screen = ShellVm.ScreenAnalysis2;
            Db.FocusIo(e.Io);
        }

        private void OnGroupOpened(object sender, MainView.GroupEventArgs e)
        {
            if (!_state.HasBoth) return;

            // 그래프로 갑니다 (0.92 에 히트맵에서 바꿨습니다). 그룹을 누르는
            // 뜻은 "이 IO 들이 어떻게 달라졌나" 를 보는 것인데, 히트맵은
            // 어느 시간대가 심한지를 보는 화면입니다. 값이 어떻게 달라졌는지는
            // 그래프에서 봐야 합니다.
            _vm.Screen = ShellVm.ScreenAnalysis1;
            _vm.GoGraph();
            Graph.FocusOnGroup(e.GroupName, e.Members);
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
            // 닫은 뒤에 "단계 IO" 가 바뀌었는지 보려고 적어 둡니다.
            string stepIo = _state.Settings.StepIo ?? string.Empty;
            string llmServer = _state.Settings.LlmServer ?? string.Empty;

            var dlg = new SettingsWindow(_state.Settings, _state.IoNames());
            dlg.Owner = this;
            dlg.ShowDialog();

            SaveSettings();

            // LLM 서버 주소를 바꿨으면 그 주소로 다시 확인 (1.51). [연결 확인] 을 안 눌렀어도 —
            // 바꾼 주소와 화면의 상태가 어긋나지 않게.
            if ((_state.Settings.LlmServer ?? string.Empty) != llmServer
                || (_state.Settings.LlmServer ?? string.Empty) != LlmServer.Address)
                LlmServer.CheckAsync(_state.Settings.LlmServer);

            // 단계 IO 를 고쳤으면 <b>그 자리에서</b> 단계를 다시 찾습니다.
            // 로그를 다시 읽지 않습니다 — 이미 메모리에 있는 채널을 한 번
            // 훑으면 됩니다. 이름을 한 번에 맞게 적는 일이 드물어서
            // (대소문자·밑줄·공백) 그때마다 파일을 다시 열게 두면 못 씁니다.
            if ((_state.Settings.StepIo ?? string.Empty) != stepIo)
            {
                string why = _state.ReapplySteps();

                if (why.Length > 0 && (_state.Settings.StepIo ?? string.Empty).Length > 0)
                {
                    // 못 찾았으면 말해 줍니다. 조용히 두면 적어 놓고도
                    // 왜 단추가 안 켜지는지 알 수가 없습니다.
                    MessageBox.Show(this, why, "단계 IO",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                _vm.Step.Reload();
                _vm.Heatmap.Reload();
                Heatmap.Invalidate();
                if (_vm.Page == ShellVm.PageHeatmap && _state.HasBoth) Heatmap.Recalculate(false);
            }

            _vm.Dashboard.Refresh();
            _vm.Graph.Reload();
            Graph.OnDataChanged();
            Graph.ApplyStep();
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
                case Key.F4: _vm.GoJudge(); e.Handled = true; break;
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
