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

        // ---------------- 화면 전환 ----------------
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

        public void GoDashboard() { Page = PageDashboard; }
        public void GoGraph() { Page = PageGraph; }
        public void GoHeatmap() { Page = PageHeatmap; }

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
            RefreshSetNames();
            UpdateStatus();
        }
    }
}
