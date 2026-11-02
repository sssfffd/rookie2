using System;
using System.Windows;
using LogScope.Analysis1.Judge;

namespace LogScope.Main
{
    /// <summary>
    /// "파형은 같고 조금 어긋난 IO" 창 (1.74). 고른 줄을 한꺼번에 허용하고, [그래프] 를 누르면 창을 닫고
    /// 그 IO 의 첫 구간을 <see cref="GraphItem"/> 으로 넘깁니다 (메인 화면이 받아 그래프로).
    /// </summary>
    public partial class SimilarIosWindow : Window
    {
        private readonly SimilarIosVm _vm;

        public SimilarIosWindow(SimilarIosVm vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        /// <summary>[그래프] 를 누른 IO 의 첫 오차. 창을 닫은 뒤 부르는 쪽이 읽습니다.</summary>
        public JudgeItem GraphItem { get; private set; }

        private void OnSelectAll(object sender, RoutedEventArgs e) { _vm.SelectAll(true); }
        private void OnSelectNone(object sender, RoutedEventArgs e) { _vm.SelectAll(false); }
        private void OnAllowOnce(object sender, RoutedEventArgs e) { _vm.AllowSelected(AllowChoice.Once); }
        private void OnAllowAlways(object sender, RoutedEventArgs e) { _vm.AllowSelected(AllowChoice.Always); }

        private void OnGraph(object sender, RoutedEventArgs e)
        {
            var b = sender as FrameworkElement;
            var row = b != null ? b.DataContext as SimilarIoRowVm : null;
            if (row == null || row.First == null) return;
            GraphItem = row.First;
            Close();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
