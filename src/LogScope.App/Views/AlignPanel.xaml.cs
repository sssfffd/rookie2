using System;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.ViewModels;

namespace LogScope.App.Views
{
    /// <summary>
    /// 가로축과 시간 맞추기를 고르는 판. 그래프 화면과 히트맵 화면이
    /// 팝업으로 띄워 같이 씁니다.
    ///
    /// 도구 줄에 늘어놓지 않은 이유는 자리 때문입니다. 콤보 세 개와 입력
    /// 칸 하나를 한 줄에 펴면 그것만으로 도구 줄 한 줄을 다 먹습니다.
    /// 한 번 정해 놓고 계속 쓰는 값들이라 접어 두는 편이 맞습니다.
    /// </summary>
    public partial class AlignPanel : UserControl
    {
        public AlignPanel()
        {
            InitializeComponent();
        }

        /// <summary>[닫기] 를 눌렀을 때. 팝업을 띄운 쪽이 받아 닫습니다.</summary>
        public event EventHandler CloseRequested;

        private void OnClearAlign(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AlignVm;
            if (vm != null) vm.ClearAlign();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            EventHandler h = CloseRequested;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
