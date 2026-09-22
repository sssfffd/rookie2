using System;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.ViewModels;

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

        /// <summary>요약에서 그룹 한 줄을 눌렀을 때.</summary>
        public event EventHandler<GroupEventArgs> GroupOpened;

        public sealed class GroupEventArgs : EventArgs
        {
            public readonly string GroupName;
            public readonly System.Collections.Generic.List<string> Members;

            public GroupEventArgs(string groupName, System.Collections.Generic.List<string> members)
            {
                GroupName = groupName;
                Members = members;
            }
        }

        /// <summary>
        /// 요약의 그룹 줄. 그 그룹의 IO 만 히트맵에 남기고 그리로 넘어갑니다.
        /// 어디로 어떻게 넘어갈지는 창(ShellWindow)이 정합니다.
        /// </summary>
        private void OnGroupClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var vm = DataContext as MainVm;
            var b = sender as Button;
            if (vm == null || b == null) return;

            string name = b.Tag as string;
            if (string.IsNullOrEmpty(name)) return;

            GroupSummaryVm g = vm.FindGroup(name);
            if (g == null || !g.CanOpen) return;

            EventHandler<GroupEventArgs> h = GroupOpened;
            if (h != null) h(this, new GroupEventArgs(g.GroupName, g.Members));
        }

        /// <summary>
        /// [지금 결과 저장]. 이름을 물어보고 한 건 남깁니다.
        ///
        /// 이름은 비워 둘 수 있습니다 — 그러면 저장 시각으로 적힙니다.
        /// 뭘 적을지 몰라 저장을 안 하게 되는 것보다 낫습니다.
        /// </summary>
        private void OnSaveResult(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainVm;
            if (vm == null) return;

            string label;
            if (!TextInputWindow.Ask(Window.GetWindow(this), "분석 결과 저장",
                    "이 결과에 붙일 이름 (비워 두면 저장 시각으로 적습니다)",
                    string.Empty, out label)) return;

            string problem = vm.SaveCurrent(label);
            if (problem.Length > 0)
            {
                MessageBox.Show(Window.GetWindow(this), problem, "저장",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>저장된 줄을 눌렀을 때. 그때의 기록을 펼쳐 보는 창을 띄웁니다.</summary>
        private void OnOpenSaved(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainVm;
            var b = sender as Button;
            if (vm == null || b == null) return;

            string id = b.Tag as string;
            if (string.IsNullOrEmpty(id)) return;

            SavedResultWindow.Show(Window.GetWindow(this), vm.SavedById(id));
        }

        private void OnDeleteSaved(object sender, RoutedEventArgs e)
        {
            // 이 단추는 줄 단추 밖에 있지만, Click 은 위로 올라가는 사건이라
            // 혹시 모를 자리에서 겹치지 않게 여기서 끊습니다.
            e.Handled = true;

            var vm = DataContext as MainVm;
            var b = sender as Button;
            if (vm == null || b == null) return;

            string id = b.Tag as string;
            if (string.IsNullOrEmpty(id)) return;

            // 지우면 되돌릴 수 없습니다. 한 번 묻습니다.
            if (MessageBox.Show(Window.GetWindow(this),
                    "저장된 분석 한 건을 지웁니다. 되돌릴 수 없습니다.", "삭제",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

            vm.DeleteSaved(id);
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
