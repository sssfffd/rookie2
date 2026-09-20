using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 왼쪽 IO 목록 + 그룹.
    ///
    /// Rows 에 담긴 순서가 곧 화면에 보이는 순서이고, 그래프에 그려지는
    /// 순서이기도 합니다. 그래서 그룹 순서를 바꾸면 그래프 순서도 같이
    /// 바뀌고, Shift 범위 선택도 "보이는 대로" 잡힙니다.
    /// </summary>
    public sealed class ChannelListVm : ObservableObject
    {
        private readonly AppState _state;

        public List<IoRowVm> All = new List<IoRowVm>();

        private ObservableCollection<object> _rows = new ObservableCollection<object>();
        /// <summary>그룹 이름 줄(GroupRowVm)과 IO 줄(IoRowVm)이 섞여 있습니다.</summary>
        public ObservableCollection<object> Rows
        {
            get { return _rows; }
            private set { Set(ref _rows, value); }
        }

        private readonly Dictionary<string, IoRowVm> _byName =
            new Dictionary<string, IoRowVm>(StringComparer.Ordinal);

        private HashSet<string> _changedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>선택이 바뀌면 그래프가 다시 그려져야 합니다.</summary>
        public event EventHandler SelectionChanged;

        /// <summary>그룹이 바뀌면 설정을 저장해야 합니다.</summary>
        public event EventHandler GroupsChanged;

        public ChannelListVm(AppState state) { _state = state; }

        // ---------------- 필터 ----------------

        private string _search = string.Empty;
        public string Search
        {
            get { return _search; }
            set { if (Set(ref _search, value ?? string.Empty)) RebuildRows(); }
        }

        private int _kindFilter;
        /// <summary>0 전체 / 1 디지털 / 2 아날로그 / 3 상태</summary>
        public int KindFilter
        {
            get { return _kindFilter; }
            set { if (Set(ref _kindFilter, value)) RebuildRows(); }
        }

        private int _scopeFilter;
        /// <summary>0 모든 IO / 1 고른 IO만 / 2 달라진 IO만</summary>
        public int ScopeFilter
        {
            get { return _scopeFilter; }
            set { if (Set(ref _scopeFilter, value)) RebuildRows(); }
        }

        public string CountText
        {
            get { return SelectedCount + " / " + All.Count + " 선택"; }
        }

        public int SelectedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < All.Count; i++) if (All[i].IsSelected) n++;
                return n;
            }
        }

        // ---------------- 만들기 ----------------

        /// <summary>
        /// 로그가 바뀐 뒤 목록을 다시 만듭니다. 선택 상태는 이름 기준으로
        /// 살려 둡니다 — 다른 파일을 열어도 같은 이름이면 그대로 켜집니다.
        /// </summary>
        public void Rebuild(LogDataset before, LogDataset after, HashSet<string> changedNames)
        {
            var keepSelected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < All.Count; i++) if (All[i].IsSelected) keepSelected.Add(All[i].Name);

            _changedNames = changedNames ?? new HashSet<string>(StringComparer.Ordinal);
            All.Clear();
            _byName.Clear();

            if (before != null)
            {
                for (int i = 0; i < before.ChannelCount; i++)
                {
                    Channel c = before.Channels[i];
                    var vm = new IoRowVm(c.Name, c.Kind);
                    vm.BeforeIndex = i;
                    if (after != null) vm.AfterIndex = after.FindChannel(c.Name);
                    Add(vm);
                }
            }
            if (after != null)
            {
                for (int j = 0; j < after.ChannelCount; j++)
                {
                    Channel c = after.Channels[j];
                    if (_byName.ContainsKey(c.Name)) continue;
                    if (before != null && before.FindChannel(c.Name) >= 0) continue;
                    var vm = new IoRowVm(c.Name, c.Kind);
                    vm.AfterIndex = j;
                    Add(vm);
                }
            }

            for (int i = 0; i < All.Count; i++)
            {
                IoRowVm vm = All[i];
                vm.IsSelected = keepSelected.Contains(vm.Name);
                vm.Status = StatusOf(vm, before, after);
            }

            // 저장된 그룹이 없으면 10 개씩 묶어 기본 그룹을 만듭니다.
            if (_state.Settings.Groups.Count == 0 && All.Count > 0) MakeDefaultGroups(10);

            RebuildRows();
            Raise("CountText");
        }

        private string StatusOf(IoRowVm vm, LogDataset before, LogDataset after)
        {
            if (!vm.InAfter && before != null && after != null) return "onlyBefore";
            if (!vm.InBefore && before != null && after != null) return "onlyAfter";
            return _changedNames.Contains(vm.Name) ? "changed" : "same";
        }

        private void Add(IoRowVm vm)
        {
            _byName[vm.Name] = vm;
            All.Add(vm);
        }

        public IoRowVm Find(string name)
        {
            IoRowVm vm;
            return name != null && _byName.TryGetValue(name, out vm) ? vm : null;
        }

        private void MakeDefaultGroups(int perGroup)
        {
            if (perGroup < 1) perGroup = 10;
            var list = new List<GroupDef>();
            for (int i = 0; i < All.Count; i += perGroup)
            {
                int last = Math.Min(i + perGroup, All.Count);
                var g = new GroupDef("그룹 " + (list.Count + 1));
                for (int j = i; j < last; j++) g.Members.Add(All[j].Name);
                list.Add(g);
            }
            _state.Settings.Groups = list;
        }

        // ---------------- 보이는 줄 ----------------

        public void RebuildRows()
        {
            List<GroupDef> groups = _state.Settings.Groups;
            var rows = new ObservableCollection<object>();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            bool filtering = !string.IsNullOrEmpty(_search) || _kindFilter != 0 || _scopeFilter != 0;

            for (int g = 0; g < groups.Count; g++)
            {
                var members = new List<IoRowVm>();
                foreach (string name in groups[g].Members)
                {
                    IoRowVm vm = Find(name);
                    if (vm == null || placed.Contains(name)) continue;
                    placed.Add(name);
                    vm.Group = g;
                    if (Passes(vm)) members.Add(vm);
                }

                // 검색으로 아무것도 안 남은 그룹은 이름 줄도 감춥니다.
                if (members.Count == 0 && filtering) continue;

                var head = new GroupRowVm(g);
                head.Name = groups[g].Name;
                head.IsExpanded = groups[g].Expanded;
                head.MemberCount = members.Count;
                head.IsAllSelected = AllSelected(members);
                head.IsActive = (g == _activeGroup);
                rows.Add(head);

                if (!head.IsExpanded) continue;
                foreach (IoRowVm vm in members) rows.Add(vm);
            }

            // 어느 그룹에도 없는 IO
            var rest = new List<IoRowVm>();
            for (int i = 0; i < All.Count; i++)
            {
                IoRowVm vm = All[i];
                if (placed.Contains(vm.Name)) continue;
                vm.Group = -1;
                if (Passes(vm)) rest.Add(vm);
            }
            if (rest.Count > 0)
            {
                var head = new GroupRowVm(-1);
                head.Name = "그룹에 없는 IO";
                head.MemberCount = rest.Count;
                head.IsAllSelected = AllSelected(rest);
                head.IsExpanded = true;
                rows.Add(head);
                foreach (IoRowVm vm in rest) rows.Add(vm);
            }

            Rows = rows;
            Raise("CountText");
        }

        private static bool AllSelected(List<IoRowVm> items)
        {
            if (items.Count == 0) return false;
            for (int i = 0; i < items.Count; i++) if (!items[i].IsSelected) return false;
            return true;
        }

        private bool Passes(IoRowVm vm)
        {
            if (_scopeFilter == 1 && !vm.IsSelected) return false;
            if (_scopeFilter == 2 && !_changedNames.Contains(vm.Name)) return false;

            switch (_kindFilter)
            {
                case 1: if (vm.Kind != ChannelKind.Digital) return false; break;
                case 2: if (vm.Kind != ChannelKind.Analog) return false; break;
                case 3: if (vm.Kind != ChannelKind.State) return false; break;
            }

            if (!string.IsNullOrEmpty(_search)
                && vm.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) return false;

            return true;
        }

        // ---------------- 선택 ----------------

        /// <summary>
        /// 한 번 누르면 켜지고, 같은 것을 다시 누르면 꺼집니다.
        /// 다른 IO 를 눌러도 이미 켠 것은 그대로 둡니다. (요구사항 3번)
        /// </summary>
        public void Toggle(IoRowVm vm)
        {
            if (vm == null) return;
            vm.IsSelected = !vm.IsSelected;
            AfterSelection();
        }

        public void SetSelected(IoRowVm vm, bool on)
        {
            if (vm == null || vm.IsSelected == on) return;
            vm.IsSelected = on;
        }

        /// <summary>
        /// Shift 범위. Rows 의 줄 번호로 훑기 때문에 화면에 보이는 순서를
        /// 그대로 따릅니다 (그룹 순서를 바꾼 뒤에도).
        /// </summary>
        public void SelectRange(int rowA, int rowB)
        {
            if (rowA > rowB) { int t = rowA; rowA = rowB; rowB = t; }
            for (int r = Math.Max(0, rowA); r <= rowB && r < Rows.Count; r++)
            {
                var vm = Rows[r] as IoRowVm;
                if (vm != null) vm.IsSelected = true;
            }
            AfterSelection();
        }

        public void SelectAllVisible(bool on)
        {
            foreach (object o in Rows)
            {
                var vm = o as IoRowVm;
                if (vm != null) vm.IsSelected = on;
            }
            AfterSelection();
        }

        public void ClearSelection()
        {
            for (int i = 0; i < All.Count; i++) All[i].IsSelected = false;
            AfterSelection();
        }

        public void SelectOnly(string name)
        {
            for (int i = 0; i < All.Count; i++) All[i].IsSelected = false;
            IoRowVm vm = Find(name);
            if (vm != null) vm.IsSelected = true;
            AfterSelection();
        }

        public void ToggleGroup(GroupRowVm head)
        {
            if (head == null) return;
            bool on = !head.IsAllSelected;
            foreach (object o in Rows)
            {
                var vm = o as IoRowVm;
                if (vm != null && vm.Group == head.Index) vm.IsSelected = on;
            }
            head.IsAllSelected = on;
            AfterSelection();
        }

        private void AfterSelection()
        {
            RefreshGroupChecks();
            Raise("CountText");
            // "고른 IO만" 보기일 때는 목록 자체가 바뀌어야 합니다.
            if (_scopeFilter == 1) RebuildRows();
            EventHandler h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void RefreshGroupChecks()
        {
            var counts = new Dictionary<int, bool>();
            foreach (object o in Rows)
            {
                var vm = o as IoRowVm;
                if (vm == null) continue;
                bool all;
                if (!counts.TryGetValue(vm.Group, out all)) all = true;
                counts[vm.Group] = all && vm.IsSelected;
            }
            foreach (object o in Rows)
            {
                var head = o as GroupRowVm;
                if (head == null) continue;
                bool all;
                head.IsAllSelected = counts.TryGetValue(head.Index, out all) && all;
            }
        }

        /// <summary>그래프에 그릴 IO 들. 화면에 보이는 차례 그대로입니다.</summary>
        public List<IoRowVm> DrawOrder()
        {
            var list = new List<IoRowVm>();
            var seen = new HashSet<IoRowVm>();
            foreach (object o in Rows)
            {
                var vm = o as IoRowVm;
                if (vm == null || !vm.IsSelected || !seen.Add(vm)) continue;
                list.Add(vm);
            }
            // 필터 때문에 화면에서 빠진 선택 IO 도 그래프에는 남겨 둡니다.
            for (int i = 0; i < All.Count; i++)
                if (All[i].IsSelected && seen.Add(All[i])) list.Add(All[i]);
            return list;
        }

        public List<string> SelectedNames()
        {
            var names = new List<string>();
            foreach (IoRowVm vm in DrawOrder()) names.Add(vm.Name);
            return names;
        }

        // ---------------- 그룹 ----------------

        private int _activeGroup = -1;

        /// <summary>아래 그룹 버튼들이 대상으로 삼는 그룹. -1 이면 고르지 않은 상태.</summary>
        public int ActiveGroup
        {
            get { return _activeGroup; }
            set
            {
                if (!Set(ref _activeGroup, value)) return;
                foreach (object o in Rows)
                {
                    var head = o as GroupRowVm;
                    if (head != null) head.IsActive = (head.Index == value);
                }
                Raise("ActiveGroupText");
                Raise("HasActiveGroup");
            }
        }

        public bool HasActiveGroup
        {
            get { return _activeGroup >= 0 && _activeGroup < _state.Settings.Groups.Count; }
        }

        public string ActiveGroupText
        {
            get
            {
                return HasActiveGroup
                    ? "고른 그룹: " + _state.Settings.Groups[_activeGroup].Name
                    : "고른 그룹 없음  (그룹 이름 줄을 한 번 누르면 골라집니다)";
            }
        }

        public void SetExpanded(GroupRowVm head, bool expanded)
        {
            if (head == null || head.Index < 0) return;
            if (head.Index >= _state.Settings.Groups.Count) return;
            _state.Settings.Groups[head.Index].Expanded = expanded;
            RebuildRows();
            RaiseGroups();
        }

        public void AddGroup(string name)
        {
            _state.AddGroup(name);
            ActiveGroup = _state.Settings.Groups.Count - 1;
            RebuildRows();
            RaiseGroups();
        }

        public void RenameActiveGroup(string name)
        {
            if (!HasActiveGroup || string.IsNullOrEmpty(name)) return;
            _state.Settings.Groups[_activeGroup].Name = name;
            RebuildRows();
            Raise("ActiveGroupText");
            RaiseGroups();
        }

        public void MoveActiveGroup(int delta)
        {
            if (!HasActiveGroup) return;
            int to = _activeGroup + delta;
            if (to < 0 || to >= _state.Settings.Groups.Count) return;
            _state.MoveGroup(_activeGroup, delta);
            _activeGroup = to;
            Raise("ActiveGroup");
            Raise("ActiveGroupText");
            RebuildRows();
            RaiseGroups();
            NotifySelection();
        }

        public void DeleteActiveGroup()
        {
            if (!HasActiveGroup) return;
            _state.RemoveGroup(_activeGroup);
            ActiveGroup = -1;
            RebuildRows();
            RaiseGroups();
            NotifySelection();
        }

        public void PutSelectedInActiveGroup()
        {
            if (!HasActiveGroup) return;
            _state.PutInGroup(SelectedNames(), _activeGroup);
            RebuildRows();
            RaiseGroups();
            NotifySelection();
        }

        /// <summary>끌어다 놓기. group 이 -1 이면 그룹에서 빼는 뜻입니다.</summary>
        public void MoveToGroup(IEnumerable<string> names, int group)
        {
            if (group < 0) _state.RemoveFromGroups(names);
            else _state.PutInGroup(names, group);
            RebuildRows();
            RaiseGroups();
            NotifySelection();
        }

        public void RemoveFromGroups(IEnumerable<string> names)
        {
            _state.RemoveFromGroups(names);
            RebuildRows();
            RaiseGroups();
            NotifySelection();
        }

        private void RaiseGroups()
        {
            EventHandler h = GroupsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void NotifySelection()
        {
            EventHandler h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>목록에 그룹이 하나라도 있는지. 메뉴를 만들 때 씁니다.</summary>
        public List<GroupDef> Groups { get { return _state.Settings.Groups; } }
    }
}
