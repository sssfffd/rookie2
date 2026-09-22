using System;
using System.Windows;
using System.Windows.Controls;

namespace LogScope.App.Views
{
    /// <summary>
    /// 메인 화면. 분석 칸을 누르면 그 분석 화면으로 넘어갑니다.
    ///
    /// 어디로 넘어갈지는 창(ShellWindow)이 정합니다. 화면끼리 서로를 부르면
    /// 누가 누구를 여는지 금방 엉키므로, 여기서는 "몇 번을 눌렀다" 만 올려
    /// 보냅니다.
    /// </summary>
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        public event EventHandler<AnalysisEventArgs> AnalysisOpened;

        public sealed class AnalysisEventArgs : EventArgs
        {
            public readonly int Number;
            public AnalysisEventArgs(int number) { Number = number; }
        }

        private void OnCardClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null || !(b.Tag is int)) return;

            EventHandler<AnalysisEventArgs> h = AnalysisOpened;
            if (h != null) h(this, new AnalysisEventArgs((int)b.Tag));
        }
    }
}
