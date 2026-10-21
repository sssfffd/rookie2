using System;
using System.Windows;
using System.Windows.Controls;
using LogScope.Logs.Compare;
using LogScope.Core.Io;

using LogScope.App.Common;
using LogScope.App.Settings;
namespace LogScope.App.Logs
{
    /// <summary>
    /// 히트맵 화면.
    ///
    /// 왼쪽에 차이가 난 IO, 가로축은 시간(기본 1 분 단위)입니다.
    /// 그 시간대에 차이가 났으면 빨간 칸에 그 구간의 절대 차이 평균을 적고,
    /// 차이가 없었으면 "정상" 칸으로 칠합니다.
    ///
    /// 계산은 채널 수 x 표본 수만큼 걸리므로 배경 스레드에서 돌립니다.
    /// </summary>
    public partial class HeatmapView : UserControl
    {
        private AppState _state;
        private HeatmapVm _vm;

        public HeatmapView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Map.HoverTextChanged += OnHoverTextChanged;
            Map.CellActivated += OnCellActivated;
        }

        public void Attach(AppState state)
        {
            _state = state;
            SyncLayout();
        }

        /// <summary>칸을 눌렀을 때. 창이 받아 그래프 화면으로 넘어갑니다.</summary>
        public event EventHandler<CellEventArgs> CellOpened;

        public sealed class CellEventArgs : EventArgs
        {
            public readonly string Name;
            public readonly double Start;
            public readonly double End;
            public CellEventArgs(string name, double start, double end)
            {
                Name = name; Start = start; End = end;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as HeatmapVm;
            if (_vm != null) _vm.PropertyChanged += OnVmChanged;
            SyncUnit();
            SyncLayout();
        }

        private void OnVmChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Search") SyncLayout();
        }

        // ---------------- 그룹 · 검색 (1.07) ----------------

        /// <summary>
        /// 판에 그룹과 검색 글을 넘깁니다. 그룹은 <b>설정에 든 그것</b>
        /// (그래프 화면이 고치는 것)을 그대로 넘깁니다 — 사본을 들고 있으면
        /// 그래프에서 그룹을 고쳐도 히트맵은 옛 그룹으로 남습니다.
        /// </summary>
        private void SyncLayout()
        {
            if (_state == null || _vm == null) return;
            Map.SetLayout(_state.Settings.Groups, _vm.Search);
        }

        /// <summary>그래프 화면에서 그룹을 고쳤을 때 (창이 부릅니다).</summary>
        public void RefreshGroups()
        {
            SyncLayout();
        }

        private void OnClearSearch(object sender, RoutedEventArgs e)
        {
            if (_vm != null) _vm.Search = string.Empty;
        }

        /// <summary>
        /// 값/퍼센트 고르기를 판에 알립니다.
        ///
        /// 판(HeatmapCanvas)은 직접 그리는 요소라 라디오 단추에 바로 묶지
        /// 않고, 화면이 값을 받아 넘겨 줍니다. 저장은 설정이 맡습니다.
        /// </summary>
        private void OnUnitChanged(object sender, RoutedEventArgs e)
        {
            SyncUnit();
        }

        private void SyncUnit()
        {
            if (_vm != null) Map.ShowPercent = _vm.ShowPercent;
        }

        // 판이 바뀐 결과는 셸 창이 AlignVm.Changed 로 받아 다시 계산합니다.
        // ---- 시간 맞추기 판 --------------------------------
        //
        // 팝업은 바깥을 누르면 닫힙니다(StaysOpen="False"). 그런데 그 "바깥"
        // 에는 팝업을 연 단추도 들어갑니다. 팝업이 먼저 그 누름을 받아 닫고,
        // 그 다음에 단추의 Click 이 도착해 다시 엽니다 — 눌러도 안 닫히는
        // 것처럼 보이는 유명한 자리입니다.
        //
        // 그래서 방금 닫혔으면 그 한 번은 무시합니다.
        private DateTime _alignClosedAt;

        private void OnAlignPopupClosed(object sender, EventArgs e)
        {
            _alignClosedAt = DateTime.UtcNow;
        }

        private void OnAlignOpen(object sender, RoutedEventArgs e)
        {
            if ((DateTime.UtcNow - _alignClosedAt).TotalMilliseconds < 250) return;
            AlignPopup.IsOpen = true;
        }

        private void OnAlignPanelClose(object sender, EventArgs e)
        {
            AlignPopup.IsOpen = false;
        }

        /// <summary>[모든 IO 보기]. 한 그룹만 보던 것을 풀고 다시 계산합니다.</summary>
        private void OnClearFocus(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.ClearFocus();
            Recalculate(false);
        }

        /// <summary>
        /// 메인 화면에서 그룹을 눌러 넘어왔을 때. 그 그룹의 IO 만 남기고
        /// 다시 계산합니다.
        /// </summary>
        public void FocusGroup(string groupName, System.Collections.Generic.List<string> names)
        {
            if (_vm == null) return;
            _vm.FocusGroup(groupName, names);
            Map.SetResult(null);
            Recalculate(false);
        }

        private void OnHoverTextChanged(object sender, string text)
        {
            if (_vm != null) _vm.HoverText = text;
        }

        private void OnCellActivated(object sender, HeatmapCanvas.CellEventArgs e)
        {
            EventHandler<CellEventArgs> h = CellOpened;
            if (h != null) h(this, new CellEventArgs(e.Name, e.Start, e.End));
        }

        // ---------------- 계산 ----------------

        private Window OwnerWindow { get { return Window.GetWindow(this); } }

        private void OnRecalculate(object sender, RoutedEventArgs e)
        {
            Recalculate(true);
        }

        /// <summary>
        /// 히트맵을 다시 계산합니다. showMessage 가 거짓이면 로그가 없어도
        /// 조용히 넘어갑니다 (화면을 열 때 자동으로 부르는 경우).
        /// </summary>
        public void Recalculate(bool showMessage)
        {
            if (_state == null || _vm == null) return;

            if (!_state.HasBoth)
            {
                if (showMessage)
                {
                    MessageBox.Show(OwnerWindow,
                        "히트맵은 이전 로그와 이후 로그를 견주는 화면입니다.\n두 로그를 모두 열어 주세요.",
                        "로그가 부족합니다", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                Map.SetResult(null);
                _vm.Describe(null);
                return;
            }

            HeatmapOptions options = _vm.BuildOptions();
            object result; Exception error;

            BackgroundJob.Run(OwnerWindow, "구간별 차이를 세는 중",
                delegate (LoadProgress p) { return HeatmapBuilder.Build(_state.Before, _state.After, options, p); },
                out result, out error);

            if (error != null)
            {
                if (error is OperationCanceledException) return;
                MessageBox.Show(OwnerWindow, "히트맵을 만들지 못했습니다.\n\n" + error.Message,
                    "히트맵 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var map = result as HeatmapResult;
            Map.TolerancePercent = options.RelativePercent;
            // 그룹 목록이 통째로 바뀌었을 수 있어서 (기본 그룹을 새로 만든
            // 날 등) 결과를 넘기기 전에 한 번 더 받아 둡니다.
            SyncLayout();
            Map.SetResult(map);
            _vm.Describe(map);
        }

        /// <summary>로그가 바뀌면 지금까지의 히트맵은 더 이상 맞지 않습니다.</summary>
        public void Invalidate()
        {
            Map.SetResult(null);
            if (_vm != null) _vm.Describe(null);
        }

        // ---------------- 칸 크기 ----------------

        private void OnWider(object sender, RoutedEventArgs e) { Map.CellWidth = Map.CellWidth * 1.25; }
        private void OnNarrower(object sender, RoutedEventArgs e) { Map.CellWidth = Map.CellWidth / 1.25; }
        private void OnTaller(object sender, RoutedEventArgs e) { Map.RowHeight = Map.RowHeight + 3; }
        private void OnShorter(object sender, RoutedEventArgs e) { Map.RowHeight = Map.RowHeight - 3; }
    }
}
