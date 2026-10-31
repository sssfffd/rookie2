using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Graph
{
    // 1.50: 왼쪽 IO 목록 — ChannelListVm · IoRowVm (IO 한 줄) · GroupRowVm (그룹 한 줄).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

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

        public List<IoRowVm> All { get; set; } = new List<IoRowVm>();

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
            if (!vm.InAfter && before != null && after != null) return IoRowVm.StatusOnlyBefore;
            if (!vm.InBefore && before != null && after != null) return IoRowVm.StatusOnlyAfter;
            return _changedNames.Contains(vm.Name) ? IoRowVm.StatusChanged : IoRowVm.StatusSame;
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
            bool filtering = NameSearch.IsActive(_search) || _kindFilter != 0 || _scopeFilter != 0;

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

            // 검색 규칙은 NameSearch 한 곳 (히트맵 · 분석 2 와 같음).
            if (!NameSearch.Matches(vm.Name, _search)) return false;

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

        /// <summary>
        /// 이 이름들만 고릅니다. <b>몇 개를 찾았는지 돌려줍니다</b> — 그룹에
        /// 담아 둔 IO 가 지금 열어 둔 로그에 없을 수 있고, 그때 아무 말 없이
        /// 빈 그래프를 보여 주면 왜 비었는지 알 수가 없습니다.
        ///
        /// 메인 화면에서 그룹을 눌러 그래프로 넘어올 때 씁니다.
        /// </summary>
        public int SelectOnly(IEnumerable<string> names)
        {
            for (int i = 0; i < All.Count; i++) All[i].IsSelected = false;

            int found = 0;
            if (names != null)
            {
                foreach (string n in names)
                {
                    IoRowVm vm = Find(n);
                    if (vm == null) continue;
                    vm.IsSelected = true;
                    found++;
                }
            }
            AfterSelection();
            return found;
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

    // ======================================================================
    //  IoRowVm.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 왼쪽 목록의 IO 한 줄. 이전 로그와 이후 로그를 합쳐서 본 것이라
    /// 한쪽에만 있을 수도 있습니다.
    /// </summary>
    public sealed class IoRowVm : ObservableObject
    {
        public string Name { get; private set; }
        public ChannelKind Kind { get; private set; }

        /// <summary>해당 로그에서의 채널 번호. 없으면 -1.</summary>
        public int BeforeIndex { get; set; }
        public int AfterIndex { get; set; }

        /// <summary>지금 화면에서 어느 그룹에 들어 있는지. -1 이면 그룹 없음.</summary>
        public int Group { get; set; }

        public IoRowVm(string name, ChannelKind kind)
        {
            Name = name;
            Kind = kind;
            BeforeIndex = -1;
            AfterIndex = -1;
            Group = -1;
        }

        public bool InBefore { get { return BeforeIndex >= 0; } }
        public bool InAfter { get { return AfterIndex >= 0; } }

        private bool _selected;
        public bool IsSelected
        {
            get { return _selected; }
            set { Set(ref _selected, value); }
        }

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case ChannelKind.Digital: return "디지털";
                    case ChannelKind.State: return "상태";
                    default: return "아날로그";
                }
            }
        }

        /// <summary>
        /// <see cref="Status"/> 에 들어갈 수 있는 글자. <b>여기 넷이 전부입니다.</b>
        ///
        /// 글자로 둔 이유: XAML 의 DataTrigger 가 이 값을 보고 줄 색을 정하는데,
        /// enum 을 쓰면 XAML 에서 쓰기가 번거롭습니다.
        ///
        /// <b>글자를 직접 적지 마세요.</b> 쓰는 쪽(ChannelListVm)과 읽는 쪽
        /// (아래 SideNote, GraphView.xaml 의 색 규칙 셋)에 같은 글자가 따로
        /// 적혀 있으면, 한 군데만 고쳤을 때 <b>컴파일도 되고 줄 색만 조용히
        /// 안 나옵니다.</b> XAML 쪽도 x:Static 으로 이 상수를 읽습니다.
        /// </summary>
        public const string StatusSame = "same";              // 양쪽에 있고 값도 같음
        public const string StatusChanged = "changed";        // 양쪽에 있는데 값이 다름
        public const string StatusOnlyBefore = "onlyBefore";  // 이전 로그에만 있음
        public const string StatusOnlyAfter = "onlyAfter";    // 이후 로그에만 있음

        private string _status = StatusSame;

        /// <summary>줄 색을 정하는 값. 위 넷 중 하나입니다.</summary>
        public string Status
        {
            get { return _status; }
            set { if (Set(ref _status, value)) Raise("SideNote"); }
        }

        public string SideNote
        {
            get
            {
                switch (_status)
                {
                    case StatusOnlyBefore: return "이전에만";
                    case StatusOnlyAfter: return "이후에만";
                    default: return string.Empty;
                }
            }
        }
    }

    // ======================================================================
    //  GroupRowVm.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>왼쪽 목록의 그룹 이름 줄.</summary>
    public sealed class GroupRowVm : ObservableObject
    {
        /// <summary>설정에 저장된 그룹 번호. -1 이면 "그룹에 없는 IO" 묶음입니다.</summary>
        public int Index { get; private set; }

        public GroupRowVm(int index) { Index = index; }

        /// <summary>-1 묶음은 이름을 바꾸거나 지울 수 없습니다.</summary>
        public bool CanEdit { get { return Index >= 0; } }

        private string _name = string.Empty;
        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value); }
        }

        private int _memberCount;
        public int MemberCount
        {
            get { return _memberCount; }
            set { if (Set(ref _memberCount, value)) Raise("MemberText"); }
        }

        public string MemberText { get { return _memberCount + "개"; } }

        private bool _expanded = true;
        public bool IsExpanded
        {
            get { return _expanded; }
            set { Set(ref _expanded, value); }
        }

        private bool _allSelected;
        public bool IsAllSelected
        {
            get { return _allSelected; }
            set { Set(ref _allSelected, value); }
        }

        private bool _active;
        /// <summary>아래 그룹 버튼들이 대상으로 삼는 그룹인지.</summary>
        public bool IsActive
        {
            get { return _active; }
            set { Set(ref _active, value); }
        }

        private bool _dropTarget;
        /// <summary>IO 를 끌고 와서 이 줄 위에 올려놓은 상태인지.</summary>
        public bool IsDropTarget
        {
            get { return _dropTarget; }
            set { Set(ref _dropTarget, value); }
        }
    }
}
