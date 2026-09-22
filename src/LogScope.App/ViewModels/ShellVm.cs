using System;
using System.Collections.ObjectModel;
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

            var names = new ObservableCollection<string>();
            for (int i = 0; i < AppSettings.SetCount; i++) names.Add(state.Settings.Sets[i].DisplayName(i));
            SetNames = names;
            _activeSet = state.Settings.ActiveSet;

            UpdateStatus();
        }

        public string VersionText { get { return "v" + BuildInfo.Version; } }

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
                Raise("Screen"); Raise("ScreenName");
                Raise("ShowMain"); Raise("ShowAnalysis1");
                Raise("ShowAnalysis2"); Raise("ShowAnalysis3");
                EventHandler h = ScreenChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>큰 화면이 바뀌면 불립니다.</summary>
        public event EventHandler ScreenChanged;

        public bool ShowMain { get { return _screen == ScreenMain; } }
        public bool ShowAnalysis1 { get { return _screen == ScreenAnalysis1; } }
        public bool ShowAnalysis2 { get { return _screen == ScreenAnalysis2; } }
        public bool ShowAnalysis3 { get { return _screen == ScreenAnalysis3; } }

        /// <summary>위 줄에 적는 지금 화면 이름.</summary>
        public string ScreenName
        {
            get
            {
                switch (_screen)
                {
                    case ScreenAnalysis1: return "분석 1 — 로그 비교";
                    case ScreenAnalysis2: return "분석 2";
                    case ScreenAnalysis3: return "분석 3";
                    default: return "메인";
                }
            }
        }

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

        // ---------------- 로그 세트 ----------------

        public ObservableCollection<string> SetNames { get; private set; }

        private int _activeSet;
        /// <summary>
        /// 고른 세트. 실제로 로그를 여는 일은 창(ShellWindow)이 맡습니다 —
        /// 진행 창을 띄워야 하기 때문입니다.
        /// </summary>
        public int ActiveSet
        {
            get { return _activeSet; }
            set
            {
                if (value < 0 || value >= AppSettings.SetCount) return;
                if (!Set(ref _activeSet, value)) return;
                _state.Settings.ActiveSet = value;
                EventHandler h = SetChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public event EventHandler SetChanged;

        public void RefreshSetNames()
        {
            for (int i = 0; i < AppSettings.SetCount; i++)
                SetNames[i] = _state.Settings.Sets[i].DisplayName(i);
            if (_activeSet != _state.Settings.ActiveSet)
            {
                _activeSet = _state.Settings.ActiveSet;
                Raise("ActiveSet");
            }
        }

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
            RefreshSetNames();
            UpdateStatus();
        }
    }
}
