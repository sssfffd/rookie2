using System;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.Services;
using LogScope.App.ViewModels;
using LogScope.Core.Compare;
using LogScope.Core.Io;

namespace LogScope.App.Views
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

        public void Attach(AppState state) { _state = state; }

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
            _vm = DataContext as HeatmapVm;
            SyncUnit();
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
        // ---- 가로축 · 시간 맞추기 판 --------------------------------
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

        private void OnHoverTextChanged(object sender, string text)
        {
            if (_vm != null) _vm.HoverText = text;
        }

        private void OnCellActivated(object sender, Controls.HeatmapCanvas.CellEventArgs e)
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
