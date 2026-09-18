using System;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.ViewModels;

namespace LogScope.App.Views
{
    /// <summary>
    /// 대시보드 화면. 목록에서 IO 를 누르면 IoActivated 가 나가고,
    /// 창이 그것을 받아 그래프 화면으로 넘어갑니다.
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private bool _navigating;

        public DashboardView()
        {
            InitializeComponent();
        }

        /// <summary>목록에서 IO 를 골랐을 때. 값은 IO 이름입니다.</summary>
        public event EventHandler<IoEventArgs> IoActivated;

        public sealed class IoEventArgs : EventArgs
        {
            public readonly string Name;
            public IoEventArgs(string name) { Name = name; }
        }

        private void Activate(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            EventHandler<IoEventArgs> h = IoActivated;
            if (h != null) h(this, new IoEventArgs(name));
        }

        /// <summary>목록 제목 칸을 눌렀을 때. 그 기준으로 정렬합니다.</summary>
        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            var header = e.OriginalSource as GridViewColumnHeader;
            if (header == null) return;

            // 맨 오른쪽 여백(패딩용 칸)에는 Header 가 없습니다.
            var column = header.Content as ColumnHeaderVm;
            if (column == null) return;

            var vm = DataContext as DashboardVm;
            if (vm != null) vm.SortBy(column);
        }

        private void OnChangedSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_navigating) return;
            var row = ChangedList.SelectedItem as DiffRowVm;
            if (row == null) return;

            // 고른 표시를 지워 둡니다. 같은 줄을 다시 눌러도 또 넘어가게.
            _navigating = true;
            ChangedList.SelectedIndex = -1;
            _navigating = false;

            Activate(row.Name);
        }

        private void OnOnlySelected(object sender, SelectionChangedEventArgs e)
        {
            if (_navigating) return;
            var list = sender as ListBox;
            if (list == null) return;
            var name = list.SelectedItem as string;
            if (name == null) return;

            _navigating = true;
            list.SelectedIndex = -1;
            _navigating = false;

            Activate(name);
        }
    }
}
