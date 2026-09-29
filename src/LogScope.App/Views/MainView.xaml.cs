using System;
using System.Windows;
using System.Collections.Generic;
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

        /// <summary>이전/이후 줄에 Log 파일을 끌어다 놓았을 때.</summary>
        public event EventHandler<LogDropEventArgs> LogDropped;

        public sealed class LogDropEventArgs : EventArgs
        {
            public readonly List<string> Files;
            /// <summary>참이면 이전 로그, 거짓이면 이후 로그 자리입니다.</summary>
            public readonly bool Before;

            public LogDropEventArgs(List<string> files, bool before)
            {
                Files = files;
                Before = before;
            }
        }

        /// <summary>
        /// 칸 위에 올라와 있는 동안 <b>그 칸을 밝힙니다</b>. 놓기 전에 어느
        /// 쪽으로 들어갈지 보여야 해서입니다. 이게 없으면 커서 옆의 파일
        /// 아이콘이 글자를 가려 어느 칸인지 알 수가 없습니다.
        /// </summary>
        private void OnLogRowDragOver(object sender, DragEventArgs e)
        {
            bool ok = HasFiles(e);
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            if (ok) Lit(sender as Border, true);
            e.Handled = true;
        }

        private void OnLogRowDragLeave(object sender, DragEventArgs e)
        {
            Lit(sender as Border, false);
            e.Handled = true;
        }

        /// <summary>
        /// 칸을 밝히거나 되돌립니다.
        ///
        /// <b>ClearValue 로 되돌리면 안 됩니다.</b> XAML 에 적은
        /// <c>Background="{DynamicResource Brush.Panel}"</c> 도 "직접 쓴 값"
        /// 자리에 들어갑니다. 지우면 XAML 값이 되살아나는 게 아니라 <b>같이
        /// 지워져서</b>, 배경도 테두리도 없는 — 칸이 사라진 — 모습이 됩니다.
        /// 0.39 에서 그렇게 해 놓아서, 파일을 놓거나 칸 밖으로 끌고 나가는
        /// 순간 네모 칸이 사라졌습니다.
        ///
        /// 그래서 되돌릴 때도 <b>값을 다시 걸어 줍니다.</b>
        /// SetResourceReference 는 DynamicResource 를 코드로 거는 것이라,
        /// 테마를 바꾸면 이 칸도 같이 따라갑니다.
        /// </summary>
        private void Lit(Border card, bool on)
        {
            if (card == null) return;

            bool before = (card.Tag as string) == "before";

            card.SetResourceReference(Border.BackgroundProperty,
                on ? "Brush.DropTarget" : "Brush.Panel");
            card.SetResourceReference(Border.BorderBrushProperty,
                on ? (before ? "Brush.Before" : "Brush.After") : "Brush.Border");
            card.BorderThickness = new Thickness(on ? 2 : 1);
        }

        /// <summary>
        /// 칸에 놓으면 <b>그 자리</b>에 넣습니다. 창 아무 데나 놓는 것과 다른
        /// 점이 이겁니다 — 어느 쪽인지 묻지 않고 바로 들어갑니다.
        ///
        /// 실제로 읽는 일은 창(ShellWindow)이 맡습니다. 진행 창을 띄워야 하고,
        /// 읽은 뒤에 다시 견줘야 하기 때문입니다.
        /// </summary>
        private void OnLogRowDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Lit(sender as Border, false);

            var g = sender as FrameworkElement;
            if (g == null || !HasFiles(e)) return;

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            bool before = (g.Tag as string) == "before";

            EventHandler<LogDropEventArgs> h = LogDropped;
            if (h != null) h(this, new LogDropEventArgs(new List<string>(paths), before));
        }

        private static bool HasFiles(DragEventArgs e)
        {
            return e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
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
