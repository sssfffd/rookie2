using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using LogScope.Common;

namespace LogScope.Main
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

        /// <summary>분석 1 의 이전/이후 칸에 Log 파일을 끌어다 놓았을 때.</summary>
        public event EventHandler<FileDropEventArgs> LogDropped;

        /// <summary>
        /// 분석 2 의 이전/이후 칸에 폴더나 파일을 끌어다 놓았을 때.
        ///
        /// 로그와 따로 둡니다. 받는 쪽이 다르고 (한쪽은 로그를 읽고 한쪽은
        /// DB 자리를 적습니다), 하나로 묶어 "어느 분석이냐" 를 안에 넣으면
        /// 받는 곳에서 다시 갈라야 합니다.
        /// </summary>
        public event EventHandler<FileDropEventArgs> DbDropped;

        /// <summary>분석 3 칸의 이전/이후 줄에 놓았을 때 (1.29).</summary>
        public event EventHandler<FileDropEventArgs> A3Dropped;

        /// <summary>놓은 것과, 어느 쪽 칸인지.</summary>
        public sealed class FileDropEventArgs : EventArgs
        {
            public readonly List<string> Files;
            /// <summary>참이면 이전 자리, 거짓이면 이후 자리입니다.</summary>
            public readonly bool Before;

            public FileDropEventArgs(List<string> files, bool before)
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
        private void OnDropCardDragOver(object sender, DragEventArgs e)
        {
            bool ok = HasFiles(e);
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            if (ok) Lit(sender as Border, true);
            e.Handled = true;
        }

        private void OnDropCardDragLeave(object sender, DragEventArgs e)
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
        private static void Lit(Border card, bool on)
        {
            CardTag.Lit(card, on);
        }

        /// <summary>
        /// 칸의 Tag 는 <c>"before"</c> 또는 <c>"before|Brush.Window"</c> 입니다.
        /// 앞은 어느 쪽 칸인지, 뒤는 <b>밝히기를 끝낸 뒤 되돌릴 배경</b>입니다.
        ///
        /// 되돌릴 배경을 코드에 박아 두면 안 됩니다. 0.59 에서 로그 칸이 분석 1
        /// 안으로 들어가면서 배경이 Brush.Panel 에서 Brush.Window 로 바뀌었는데,
        /// 코드가 Brush.Panel 로 되돌리고 있었습니다. 그러면 파일을 한 번
        /// 끌어다 놓은 칸만 <b>색이 달라진 채</b> 남습니다. 칸이 사라지던
        /// 0.42 와 같은 종류의 고장입니다 — 칸이 어떤 모습이어야 하는지는
        /// XAML 이 알고 코드는 모릅니다.
        /// </summary>
        /// <summary>
        /// 칸에 놓으면 <b>그 자리</b>에 넣습니다. 창 아무 데나 놓는 것과 다른
        /// 점이 이겁니다 — 어느 쪽인지 묻지 않고 바로 들어갑니다.
        ///
        /// 실제로 읽는 일은 창(ShellWindow)이 맡습니다. 진행 창을 띄워야 하고,
        /// 읽은 뒤에 다시 견줘야 하기 때문입니다.
        /// </summary>
        private void OnLogDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Lit(sender as Border, false);

            var g = sender as FrameworkElement;
            if (g == null || !HasFiles(e)) return;

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            bool before = CardTag.IsBefore(g);

            EventHandler<FileDropEventArgs> h = LogDropped;
            if (h != null) h(this, new FileDropEventArgs(new List<string>(paths), before));
        }

        /// <summary>
        /// 분석 2 칸에 놓았을 때. 적는 일도 읽는 일도 분석 2 화면이 맡습니다 —
        /// 여기서 설정을 직접 고치면 자리를 적는 곳이 둘이 됩니다.
        /// </summary>
        private void OnDbDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Lit(sender as Border, false);

            var g = sender as FrameworkElement;
            if (g == null || !HasFiles(e)) return;

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            EventHandler<FileDropEventArgs> h = DbDropped;
            if (h != null) h(this, new FileDropEventArgs(new List<string>(paths), CardTag.IsBefore(g)));
        }

        /// <summary>분석 3 칸에 놓았을 때. 분석 2 칸과 같은 길입니다.</summary>
        private void OnA3Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Lit(sender as Border, false);

            var g = sender as FrameworkElement;
            if (g == null || !HasFiles(e)) return;

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            EventHandler<FileDropEventArgs> h = A3Dropped;
            if (h != null) h(this, new FileDropEventArgs(new List<string>(paths), CardTag.IsBefore(g)));
        }

        private static bool HasFiles(DragEventArgs e)
        {
            return e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        /// <summary>
        /// 칸을 <b>눌렀을 때</b> — 파일 고르는 창을 띄웁니다.
        ///
        /// 끌어다 놓기만 되면 탐색기를 띄워 놓고 끌어와야 합니다. 누르는
        /// 쪽이 손이 덜 가는 때도 많고, 무엇보다 <b>끌어다 놓기가 된다는
        /// 것을 모르는 사람</b>에게는 그 칸이 그림일 뿐입니다.
        ///
        /// 어느 쪽 자리인지는 칸의 Tag 가 압니다 (끌어다 놓기와 같은 자리).
        /// </summary>
        public event EventHandler<PickEventArgs> LogPickRequested;

        /// <summary>분석 2 의 견줄 자리를 눌렀을 때.</summary>
        public event EventHandler<PickEventArgs> DbPickRequested;

        /// <summary>분석 3 칸의 이전/이후 줄을 눌렀을 때 (1.29).</summary>
        public event EventHandler<PickEventArgs> A3PickRequested;

        public sealed class PickEventArgs : EventArgs
        {
            /// <summary>참이면 이전 자리, 거짓이면 이후 자리입니다.</summary>
            public readonly bool Before;
            public PickEventArgs(bool before) { Before = before; }
        }

        private void OnLogCardClick(object sender, MouseButtonEventArgs e)
        {
            Pick(sender, e, LogPickRequested);
        }

        private void OnDbCardClick(object sender, MouseButtonEventArgs e)
        {
            Pick(sender, e, DbPickRequested);
        }

        private void OnA3CardClick(object sender, MouseButtonEventArgs e)
        {
            Pick(sender, e, A3PickRequested);
        }

        /// <summary>분석 3 칸 머리의 [분석] (1.35). 창이 Analysis3Link 로 넘깁니다.</summary>
        public event EventHandler A3AnalyzeRequested;

        private void OnA3Analyze(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            EventHandler h = A3AnalyzeRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void Pick(object sender, MouseButtonEventArgs e, EventHandler<PickEventArgs> h)
        {
            e.Handled = true;

            var g = sender as FrameworkElement;
            if (g == null || h == null) return;

            h(this, new PickEventArgs(CardTag.IsBefore(g)));
        }

        /// <summary>
        /// 분석 2 칸의 요약에서 IO 한 줄을 눌렀을 때. 그 화면으로 넘어가
        /// <b>그 IO 를 찾아 줍니다</b> — 화면만 열면 수백 줄 가운데서 다시
        /// 찾아야 하고, 그러면 누른 뜻이 없습니다.
        /// </summary>
        public event EventHandler<IoEventArgs> IoOpened;

        public sealed class IoEventArgs : EventArgs
        {
            public readonly string Io;

            /// <summary>어느 칸의 줄인지 (2 = 분석 2, 3 = 분석 3).</summary>
            public readonly int Card;

            public IoEventArgs(string io, int card) { Io = io ?? string.Empty; Card = card; }
        }

        private void OnCardIoClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var b = sender as Button;
            if (b == null) return;

            string io = b.Tag as string;
            if (string.IsNullOrEmpty(io)) return;

            // 어느 칸의 줄인지는 줄이 들고 있습니다 (CardLineVm.Card).
            var line = b.DataContext as CardLineVm;
            int card = line != null ? line.Card : 2;

            EventHandler<IoEventArgs> h = IoOpened;
            if (h != null) h(this, new IoEventArgs(io, card));
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
