using System;
using System.IO;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>창 전체의 상태. 위쪽 메뉴 줄과 아래쪽 알림 줄이 여기를 봅니다.</summary>
    public sealed class ShellVm : ObservableObject
    {
        private readonly AppState _state;

        /// <summary>메인 화면. 분석 셋의 점수를 늘어놓습니다.</summary>
        public MainVm Main { get; private set; }

        public DashboardVm Dashboard { get; private set; }
        public GraphVm Graph { get; private set; }
        public HeatmapVm Heatmap { get; private set; }

        /// <summary>
        /// 가로축과 시간 맞추기. 그래프 화면과 히트맵 화면이 <b>같은 것</b>을
        /// 나눠 씁니다 — 한쪽에서 맞춰 놓으면 다른 쪽에도 그대로 보입니다.
        /// </summary>
        public AlignVm Align { get; private set; }

        public ShellVm(AppState state)
        {
            _state = state;
            Main = new MainVm(state);
            Align = new AlignVm(state);
            Dashboard = new DashboardVm(state);
            Graph = new GraphVm(state, Align);
            Heatmap = new HeatmapVm(state, Align);

            UpdateStatus();
        }

        public string VersionText { get { return "v" + BuildInfo.Version; } }

        /// <summary>
        /// 프로그램 이름. 창 제목과 왼쪽 위 글자가 이 값을 씁니다.
        ///
        /// <b>바꾸는 곳은 실행 파일 옆의 config.txt 한 곳입니다</b> (name 줄).
        /// 소스 여러 군데에 이름을 적어 두면 바꿀 때 한두 군데가 남습니다.
        /// </summary>
        public string ProductName { get { return AppConfig.Current.Name; } }

        /// <summary>판 번호 옆에 작게 적는 긴 이름. config.txt 의 fullname.</summary>
        public string FullNameText { get { return AppConfig.Current.FullName; } }

        public string WindowTitle { get { return AppConfig.Current.Name + " — IO 로그 그래프 뷰어"; } }

        // ---------------- 큰 화면 (메인 / 분석 1·2·3) ----------------
        //
        // 화면 전환이 두 겹입니다.
        //
        //   Screen : 메인 · 분석 1 · 분석 2 · 분석 3
        //   Page   : 분석 1 <b>안에서</b> 대시보드 · 그래프 · 히트맵
        //
        // 한 겹으로 합치지 않은 이유가 있습니다. 대시보드/그래프/히트맵은
        // 같은 로그 한 쌍을 세 가지로 보는 것이라 서로 상태를 나눠 씁니다
        // (고른 IO, 확대 상태, 시간 맞추기). 분석 1·2·3 은 그런 사이가
        // 아니라서, 같은 줄에 늘어놓으면 나중에 분석 2 를 만들 때
        // "이 IO 선택이 어느 화면 것인가" 가 엉킵니다.

        public const int ScreenMain = 0;
        public const int ScreenAnalysis1 = 1;
        public const int ScreenAnalysis2 = 2;
        public const int ScreenAnalysis3 = 3;

        private int _screen = ScreenMain;

        public int Screen
        {
            get { return _screen; }
            set
            {
                if (value < ScreenMain || value > ScreenAnalysis3) return;
                if (_screen == value) return;
                _screen = value;
                Raise("Screen");
                Raise("ShowMain"); Raise("ShowAnalysis1");
                Raise("ShowAnalysis2"); Raise("ShowAnalysis3");
                EventHandler h = ScreenChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>큰 화면이 바뀌면 불립니다.</summary>
        public event EventHandler ScreenChanged;

        // 위 줄의 화면 단추가 이 넷에 TwoWay 로 걸립니다. 그래서 읽기만
        // 되는 속성이 아니라 "참을 넣으면 그 화면으로" 가 되어야 합니다.
        public bool ShowMain
        {
            get { return _screen == ScreenMain; }
            set { if (value) Screen = ScreenMain; }
        }

        public bool ShowAnalysis1
        {
            get { return _screen == ScreenAnalysis1; }
            set { if (value) Screen = ScreenAnalysis1; }
        }

        public bool ShowAnalysis2
        {
            get { return _screen == ScreenAnalysis2; }
            set { if (value) Screen = ScreenAnalysis2; }
        }

        public bool ShowAnalysis3
        {
            get { return _screen == ScreenAnalysis3; }
            set { if (value) Screen = ScreenAnalysis3; }
        }

        /// <summary>
        /// 화면 단추에 적는 이름. <b>config.txt 에 적은 이름 그대로</b>입니다.
        ///
        /// "분석 1" 같은 번호를 앞에 붙이지 않습니다. 이름을 "로그 비교" 로
        /// 정해 놓고 단추에는 "분석 1" 이 적혀 있으면, 정한 이름이 어디에
        /// 쓰이는지 알 수가 없습니다. 번호는 열쇠(analysis1·2·3)가 이미
        /// 말해 줍니다.
        /// </summary>
        public string TabMain { get { return "메인"; } }
        public string TabAnalysis1 { get { return AppConfig.Current.Title(1); } }
        public string TabAnalysis2 { get { return AppConfig.Current.Title(2); } }
        public string TabAnalysis3 { get { return AppConfig.Current.Title(3); } }

        /// <summary>아직 만들지 않은 화면 가운데에 크게 적을 이름.</summary>
        public string Analysis2Name { get { return AppConfig.Current.Title(2); } }
        public string Analysis3Name { get { return AppConfig.Current.Title(3); } }

        public void GoMain() { Screen = ScreenMain; }

        /// <summary>메인 화면의 칸을 눌렀을 때. 1·2·3 이 그대로 화면 번호입니다.</summary>
        public void GoAnalysis(int number) { Screen = number; }

        // ---------------- 분석 1 안에서의 화면 전환 ----------------
        //
        // 위 메뉴의 [대시보드] [그래프] [히트맵] 라디오 버튼이 이 값을 봅니다.
        // 라디오는 "켤 때만" 반응해야 하므로 set 에서 value 가 거짓이면 무시합니다.

        public const int PageDashboard = 0;
        public const int PageGraph = 1;
        public const int PageHeatmap = 2;

        private int _page = PageDashboard;

        public int Page
        {
            get { return _page; }
            private set
            {
                if (_page == value) return;
                _page = value;
                Raise("Page");
                Raise("ShowDashboard"); Raise("ShowGraph"); Raise("ShowHeatmap");
                EventHandler h = PageChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>보고 있는 화면이 바뀌면 불립니다.</summary>
        public event EventHandler PageChanged;

        public bool ShowDashboard
        {
            get { return _page == PageDashboard; }
            set { if (value) Page = PageDashboard; }
        }

        public bool ShowGraph
        {
            get { return _page == PageGraph; }
            set { if (value) Page = PageGraph; }
        }

        public bool ShowHeatmap
        {
            get { return _page == PageHeatmap; }
            set { if (value) Page = PageHeatmap; }
        }

        // 셋 다 "분석 1 을 이 모습으로 보여 달라" 는 뜻입니다. 다른 화면에
        // 있을 때 눌러도 분석 1 로 넘어가 줘야 합니다 — 아무 일도 안 일어나면
        // 단축키가 고장 난 것처럼 보입니다.
        public void GoDashboard() { Screen = ScreenAnalysis1; Page = PageDashboard; }
        public void GoGraph() { Screen = ScreenAnalysis1; Page = PageGraph; }
        public void GoHeatmap() { Screen = ScreenAnalysis1; Page = PageHeatmap; }

        // 로그 세트(여러 쌍을 골라 가며 보기)는 없앴습니다. 쓰이지 않는데
        // 위 줄에서 자리를 제일 많이 차지했습니다. 기억된 경로와 기본 폴더는
        // 그대로 쓰므로(AppSettings 의 첫 세트) 설정은 그대로 살아 있습니다.

        // ---------------- 아래쪽 알림 줄 ----------------

        private string _status = string.Empty;
        public string StatusText
        {
            get { return _status; }
            private set { Set(ref _status, value); }
        }

        public void UpdateStatus()
        {
            string s = string.Empty;
            if (_state.Before != null) s += "이전: " + Describe(_state.Before);
            if (_state.After != null) s += (s.Length > 0 ? "        |        " : "") + "이후: " + Describe(_state.After);

            if (s.Length == 0)
            {
                StatusText = "로그를 열어 주세요.  설정 창에서 세트마다 처음 열 폴더를 정할 수 있습니다.";
                return;
            }

            string notes = string.Empty;
            if (_state.Before != null) notes += _state.Before.NotesText;
            if (_state.After != null) notes += " " + _state.After.NotesText;
            notes = notes.Trim();
            if (notes.Length > 0) s += "        |        " + notes;
            StatusText = s;
        }

        private static string Describe(LogDataset ds)
        {
            return Path.GetFileName(ds.SourcePath)
                 + " · 채널 " + ds.ChannelCount.ToString("N0")
                 + " · 표본 " + ds.SampleCount.ToString("N0")
                 + " · " + (ds.Orientation == Orientation.Cols ? "열이 IO" : "행이 IO")
                 + " · " + ds.LoadSeconds.ToString("0.0") + "초";
        }

        /// <summary>로그가 바뀐 뒤 화면 전체를 새로 고칩니다.</summary>
        public void ReloadAll()
        {
            Align.Reload();
            Graph.Reload();
            Dashboard.Refresh();
            Main.Refresh();
            UpdateStatus();
        }
    }
}
