using System;
using System.Windows;
using LogScope.Analysis1.Judge;

namespace LogScope.Main
{
    /// <summary>
    /// IO 의 구간별 오차 창 (1.62). 줄 단추로 허용하고, [그래프] 를 누르면 창을 닫고
    /// <see cref="GraphRequested"/> 로 알립니다 (메인 화면이 받아 그래프로 넘김).
    /// </summary>
    public partial class IoErrorsWindow : Window
    {
        private readonly IoErrorsVm _vm;

        public IoErrorsWindow(IoErrorsVm vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        /// <summary>[그래프] 를 누른 오차. 창을 닫은 뒤 부르는 쪽이 읽습니다.</summary>
        public JudgeItem GraphItem { get; private set; }

        private static ErrorRowVm RowOf(object sender)
        {
            var b = sender as FrameworkElement;
            return b != null ? b.DataContext as ErrorRowVm : null;
        }

        private void OnAllowOnce(object sender, RoutedEventArgs e) { _vm.Allow(RowOf(sender), AllowChoice.Once); }
        private void OnAllowAlways(object sender, RoutedEventArgs e) { _vm.Allow(RowOf(sender), AllowChoice.Always); }
        private void OnUnallow(object sender, RoutedEventArgs e) { _vm.Allow(RowOf(sender), AllowChoice.Unallow); }

        private void OnErrorGraph(object sender, RoutedEventArgs e)
        {
            ErrorRowVm row = RowOf(sender);
            if (row == null) return;
            GraphItem = row.Item;
            Close();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
