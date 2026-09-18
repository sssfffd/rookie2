using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogScope.App.Controls;
using LogScope.App.Services;
using LogScope.App.ViewModels;
using LogScope.Core.Compare;

namespace LogScope.App.Views
{
    /// <summary>
    /// 그래프 화면. 왼쪽에 IO 목록과 그룹, 오른쪽에 그래프입니다.
    ///
    /// 그래프 판을 만지는 일(확대, 이동, 커서)은 여기 코드가 맡습니다.
    /// 화면 요소를 직접 다뤄야 하는 일이라 뷰모델에 넣지 않았습니다.
    /// </summary>
    public partial class GraphView : UserControl
    {
        /// <summary>끌어다 놓을 때 쓰는 꼬리표. 이 프로그램 안에서만 씁니다.</summary>
        private const string DragFormat = "LogScope.IoNames";

        private GraphVm _vm;
        private int _lastClickedRow = -1;
        private Point _dragStart;
        private bool _mayDrag;
        private GroupRowVm _dropTarget;

        public GraphView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Plot.ViewChanged += OnCanvasViewChanged;
        }

        /// <summary>창이 상태를 넘겨 줍니다.</summary>
        public void Attach(AppState state)
        {
            Plot.Attach(state);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.OptionsChanged -= OnOptionsChanged;
                _vm.List.SelectionChanged -= OnSelectionChanged;
            }

            _vm = DataContext as GraphVm;
            if (_vm == null) return;

            _vm.OptionsChanged += OnOptionsChanged;
            _vm.List.SelectionChanged += OnSelectionChanged;

            PushOptions();
            PushChannels();
        }

        // ---------------- 뷰모델 -> 그래프 판 ----------------

        private void OnOptionsChanged(object sender, EventArgs e)
        {
            PushOptions();
        }

        private void PushOptions()
        {
            if (_vm == null) return;
            Plot.LaneMode = _vm.LaneMode;
            Plot.Scale = _vm.ScaleNormalized ? ValueScaleMode.Normalized
                       : (_vm.ScaleDelta ? ValueScaleMode.Delta : ValueScaleMode.Raw);
            Plot.FitVisible = _vm.FitVisible;
            Plot.ShadeDifference = _vm.ShadeDifference;
            Plot.SeparateTraces = _vm.SeparateTraces;

            double percent;
            if (double.TryParse(_vm.RelativeTolerancePercentText, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out percent))
                Plot.RelativeTolerance = ToleranceRule.FromPercent(percent);
            Plot.AbsoluteTolerance = _vm.AbsoluteTolerance;
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            PushChannels();
        }

        private void PushChannels()
        {
            if (_vm == null) return;
            Plot.SetChannels(_vm.List.DrawOrder());
            UpdateReadout();
        }

        private void OnCanvasViewChanged(object sender, EventArgs e)
        {
            UpdateReadout();
        }

        private void UpdateReadout()
        {
            if (_vm == null) return;
            _vm.UpdateReadout(Plot.CursorA, Plot.CursorB, Plot.VisibleStart, Plot.VisibleEnd);
        }

        // ---------------- 로그가 바뀌었을 때 ----------------

        public void OnDataChanged()
        {
            PushOptions();
            PushChannels();
            Plot.ResetTime();
            Plot.ResetValueZoom();
            _lastClickedRow = -1;
        }

        /// <summary>대시보드에서 IO 하나를 눌러 넘어왔을 때.</summary>
        public void FocusOnIo(string name)
        {
            if (_vm == null) return;
            _vm.List.SelectOnly(name);
            IoRowVm row = _vm.List.Find(name);
            if (row != null)
            {
                int index = RowList.Items.IndexOf(row);
                if (index >= 0) RowList.ScrollIntoView(row);
            }
            Plot.ResetValueZoom();
            PushChannels();
        }

        /// <summary>히트맵에서 칸을 눌러 넘어왔을 때 그 시간대로 맞춥니다.</summary>
        public void SetTimeRange(double t0, double t1)
        {
            Plot.SetTimeRange(t0, t1);
        }

        // ---------------- 왼쪽 위: 검색과 선택 ----------------

        private void OnClearSearch(object sender, RoutedEventArgs e)
        {
            if (_vm != null) _vm.List.Search = string.Empty;
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            if (_vm != null) _vm.List.SelectAllVisible(true);
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            if (_vm != null) _vm.List.ClearSelection();
        }

        // ---------------- 목록의 줄 ----------------

        private static T DataOf<T>(object sender) where T : class
        {
            var fe = sender as FrameworkElement;
            return fe != null ? fe.DataContext as T : null;
        }

        private void OnGroupRowDown(object sender, MouseButtonEventArgs e)
        {
            var head = DataOf<GroupRowVm>(sender);
            if (head == null || _vm == null) return;

            if (head.CanEdit) _vm.List.ActiveGroup = head.Index;
            _vm.List.SetExpanded(head, !head.IsExpanded);
            e.Handled = true;
        }

        private void OnGroupCheck(object sender, RoutedEventArgs e)
        {
            var head = DataOf<GroupRowVm>(sender);
            if (head == null || _vm == null) return;
            if (head.CanEdit) _vm.List.ActiveGroup = head.Index;
            _vm.List.ToggleGroup(head);
            e.Handled = true;   // 줄 전체의 접기/펴기까지 같이 일어나지 않게
        }

        private void OnIoCheck(object sender, RoutedEventArgs e)
        {
            var row = DataOf<IoRowVm>(sender);
            if (row == null || _vm == null) return;
            _vm.List.Toggle(row);
            _lastClickedRow = RowList.Items.IndexOf(row);
            e.Handled = true;
        }

        private void OnIoRowDown(object sender, MouseButtonEventArgs e)
        {
            var row = DataOf<IoRowVm>(sender);
            if (row == null || _vm == null) return;

            int index = RowList.Items.IndexOf(row);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _lastClickedRow >= 0)
            {
                // Shift 범위는 화면에 보이는 순서를 따릅니다. (요구사항 3번)
                _vm.List.SelectRange(_lastClickedRow, index);
            }
            else
            {
                // 같은 IO 를 다시 눌러야 꺼집니다. 다른 IO 를 눌러도
                // 이미 켜 둔 것은 그대로 남습니다. (요구사항 3번)
                _vm.List.Toggle(row);
                _lastClickedRow = index;
            }

            _mayDrag = true;
            _dragStart = e.GetPosition(this);
            e.Handled = true;
        }

        private void OnIoRowMove(object sender, MouseEventArgs e)
        {
            if (!_mayDrag || e.LeftButton != MouseButtonState.Pressed) { _mayDrag = false; return; }
            if (_vm == null) return;

            Point now = e.GetPosition(this);
            if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            _mayDrag = false;
            List<string> names = _vm.List.SelectedNames();
            if (names.Count == 0)
            {
                var row = DataOf<IoRowVm>(sender);
                if (row == null) return;
                names = new List<string> { row.Name };
            }

            var data = new DataObject(DragFormat, names);
            DragDrop.DoDragDrop(RowList, data, DragDropEffects.Move);
        }

        // ---------------- 끌어다 놓기 ----------------

        private void OnGroupDragOver(object sender, DragEventArgs e)
        {
            bool ok = e.Data.GetDataPresent(DragFormat);
            e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            var head = DataOf<GroupRowVm>(sender);
            if (!ok || head == null) return;
            if (_dropTarget != null && _dropTarget != head) _dropTarget.IsDropTarget = false;
            head.IsDropTarget = true;
            _dropTarget = head;
        }

        private void OnGroupDragLeave(object sender, DragEventArgs e)
        {
            var head = DataOf<GroupRowVm>(sender);
            if (head != null) head.IsDropTarget = false;
            if (_dropTarget == head) _dropTarget = null;
        }

        private void OnGroupDrop(object sender, DragEventArgs e)
        {
            var head = DataOf<GroupRowVm>(sender);
            if (head != null) head.IsDropTarget = false;
            _dropTarget = null;

            if (_vm == null || !e.Data.GetDataPresent(DragFormat)) return;
            var names = e.Data.GetData(DragFormat) as List<string>;
            if (names == null || head == null) return;

            // -1 묶음에 떨어뜨리면 "그룹에서 빼기" 입니다.
            _vm.List.MoveToGroup(names, head.Index);
            e.Handled = true;
        }

        // ---------------- 왼쪽 아래: 그룹 버튼 ----------------

        private Window OwnerWindow { get { return Window.GetWindow(this); } }

        private bool RequireGroup()
        {
            if (_vm != null && _vm.List.HasActiveGroup) return true;
            MessageBox.Show(OwnerWindow,
                "먼저 목록에서 그룹 이름 줄을 한 번 눌러 그룹을 골라 주세요.",
                "그룹을 고르지 않았습니다", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void OnAddGroup(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            string name;
            if (!TextInputWindow.Ask(OwnerWindow, "새 그룹", "그룹 이름을 적어 주세요.",
                    "그룹 " + (_vm.List.Groups.Count + 1), out name)) return;
            _vm.List.AddGroup(name);
        }

        private void OnRenameGroup(object sender, RoutedEventArgs e)
        {
            if (!RequireGroup()) return;
            string current = _vm.List.Groups[_vm.List.ActiveGroup].Name;
            string name;
            if (!TextInputWindow.Ask(OwnerWindow, "그룹 이름", "새 이름을 적어 주세요.", current, out name)) return;
            _vm.List.RenameActiveGroup(name);
        }

        private void OnMoveGroupUp(object sender, RoutedEventArgs e)
        {
            if (!RequireGroup()) return;
            _vm.List.MoveActiveGroup(-1);
        }

        private void OnMoveGroupDown(object sender, RoutedEventArgs e)
        {
            if (!RequireGroup()) return;
            _vm.List.MoveActiveGroup(+1);
        }

        private void OnPutSelected(object sender, RoutedEventArgs e)
        {
            if (!RequireGroup()) return;
            _vm.List.PutSelectedInActiveGroup();
        }

        private void OnDeleteGroup(object sender, RoutedEventArgs e)
        {
            if (!RequireGroup()) return;
            string name = _vm.List.Groups[_vm.List.ActiveGroup].Name;
            MessageBoxResult r = MessageBox.Show(OwnerWindow,
                "'" + name + "' 그룹을 지웁니다.\nIO 자체는 지워지지 않고 그룹 묶음만 없어집니다.",
                "그룹 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            _vm.List.DeleteActiveGroup();
        }

        // ---------------- 도구 줄: 그래프 판 조작 ----------------

        private void OnResetTime(object sender, RoutedEventArgs e) { Plot.ResetTime(); }
        private void OnZoomTimeIn(object sender, RoutedEventArgs e) { Plot.ZoomTime(1.35); }
        private void OnZoomTimeOut(object sender, RoutedEventArgs e) { Plot.ZoomTime(1 / 1.35); }
        private void OnZoomValueIn(object sender, RoutedEventArgs e) { Plot.ZoomValue(1.35); }
        private void OnZoomValueOut(object sender, RoutedEventArgs e) { Plot.ZoomValue(1 / 1.35); }
        private void OnResetValueZoom(object sender, RoutedEventArgs e) { Plot.ResetValueZoom(); }
    }
}
