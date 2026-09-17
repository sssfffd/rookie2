using System;
using System.Collections.Generic;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App
{
    /// <summary>두 로그를 합쳐 본 IO 하나. 한쪽에만 있을 수도 있습니다.</summary>
    public sealed class IoRef
    {
        public string Name;
        public int BeforeIndex = -1;
        public int AfterIndex = -1;
        public ChannelKind Kind = ChannelKind.Analog;

        public bool InBefore { get { return BeforeIndex >= 0; } }
        public bool InAfter { get { return AfterIndex >= 0; } }
        public bool BothSides { get { return InBefore && InAfter; } }
    }

    public enum RowKind { GroupHeader, Io }

    public struct ViewRow
    {
        public RowKind Kind;
        public int Group;   // 그룹 번호. -1 이면 "그룹 없음" 묶음.
        public int Io;      // All 안에서의 번호. 그룹 머리줄이면 -1.
    }

    public enum KindFilter { All, Digital, Analog, State }

    /// <summary>
    /// 왼쪽 목록에 실제로 보이는 줄들을 만듭니다.
    ///
    /// 여기서 만든 순서가 곧 그래프에 그려지는 순서입니다. 그래서 그룹 순서를
    /// 바꾸면 그래프 순서도 같이 바뀌고, Shift 범위 선택도 "화면에 보이는
    /// 순서" 를 그대로 따릅니다.
    /// </summary>
    public sealed class ChannelView
    {
        public List<IoRef> All = new List<IoRef>();
        public List<ViewRow> Rows = new List<ViewRow>();

        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        public List<GroupDef> Groups = new List<GroupDef>();

        public string Search = string.Empty;
        public KindFilter Kind = KindFilter.All;
        public bool SelectedOnly;
        public bool ChangedOnly;

        /// <summary>값이 달라진 IO 이름들. ChangedOnly 필터가 씁니다.</summary>
        public HashSet<string> ChangedNames = new HashSet<string>(StringComparer.Ordinal);

        public int Count { get { return All.Count; } }
        public int SelectedCount { get { return _selected.Count; } }

        // ---------------- 만들기 ----------------

        public void Rebuild(LogDataset before, LogDataset after, List<GroupDef> groups)
        {
            All.Clear();
            _indexByName.Clear();
            Groups = groups ?? new List<GroupDef>();

            if (before != null)
            {
                for (int i = 0; i < before.ChannelCount; i++)
                {
                    Channel c = before.Channels[i];
                    var r = new IoRef();
                    r.Name = c.Name;
                    r.BeforeIndex = i;
                    r.Kind = c.Kind;
                    if (after != null) r.AfterIndex = after.FindChannel(c.Name);
                    Add(r);
                }
            }
            if (after != null)
            {
                for (int j = 0; j < after.ChannelCount; j++)
                {
                    Channel c = after.Channels[j];
                    if (_indexByName.ContainsKey(c.Name)) continue;
                    if (before != null && before.FindChannel(c.Name) >= 0) continue;
                    var r = new IoRef();
                    r.Name = c.Name;
                    r.AfterIndex = j;
                    r.Kind = c.Kind;
                    Add(r);
                }
            }

            // 없어진 IO 를 가리키는 선택은 버립니다.
            var stale = new List<string>();
            foreach (string s in _selected) if (!_indexByName.ContainsKey(s)) stale.Add(s);
            foreach (string s in stale) _selected.Remove(s);

            RebuildRows();
        }

        private void Add(IoRef r)
        {
            _indexByName[r.Name] = All.Count;
            All.Add(r);
        }

        public int IndexOf(string name)
        {
            int i;
            return name != null && _indexByName.TryGetValue(name, out i) ? i : -1;
        }

        /// <summary>
        /// 10 개씩 묶은 기본 그룹을 만듭니다. 저장된 그룹이 하나도 없을 때만
        /// 부릅니다. 이후에는 사용자가 만든 그룹이 그대로 유지됩니다.
        /// </summary>
        public List<GroupDef> MakeDefaultGroups(int perGroup)
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
            return list;
        }

        // ---------------- 보이는 줄 ----------------

        public void RebuildRows()
        {
            Rows.Clear();
            var placed = new HashSet<string>(StringComparer.Ordinal);

            for (int g = 0; g < Groups.Count; g++)
            {
                var members = new List<int>();
                foreach (string name in Groups[g].Members)
                {
                    int i = IndexOf(name);
                    if (i < 0 || placed.Contains(name)) continue;
                    placed.Add(name);
                    if (Passes(All[i])) members.Add(i);
                }

                // 검색으로 아무것도 안 남은 그룹은 머리줄도 감춥니다.
                bool searching = !string.IsNullOrEmpty(Search) || Kind != KindFilter.All
                                 || SelectedOnly || ChangedOnly;
                if (members.Count == 0 && searching) continue;

                var head = new ViewRow();
                head.Kind = RowKind.GroupHeader; head.Group = g; head.Io = -1;
                Rows.Add(head);

                if (!Groups[g].Expanded) continue;
                foreach (int i in members)
                {
                    var row = new ViewRow();
                    row.Kind = RowKind.Io; row.Group = g; row.Io = i;
                    Rows.Add(row);
                }
            }

            // 어느 그룹에도 없는 IO
            var rest = new List<int>();
            for (int i = 0; i < All.Count; i++)
            {
                if (placed.Contains(All[i].Name)) continue;
                if (Passes(All[i])) rest.Add(i);
            }
            if (rest.Count > 0)
            {
                var head = new ViewRow();
                head.Kind = RowKind.GroupHeader; head.Group = -1; head.Io = -1;
                Rows.Add(head);
                foreach (int i in rest)
                {
                    var row = new ViewRow();
                    row.Kind = RowKind.Io; row.Group = -1; row.Io = i;
                    Rows.Add(row);
                }
            }
        }

        private bool Passes(IoRef r)
        {
            if (SelectedOnly && !_selected.Contains(r.Name)) return false;
            if (ChangedOnly && !ChangedNames.Contains(r.Name)) return false;

            switch (Kind)
            {
                case KindFilter.Digital: if (r.Kind != ChannelKind.Digital) return false; break;
                case KindFilter.Analog: if (r.Kind != ChannelKind.Analog) return false; break;
                case KindFilter.State: if (r.Kind != ChannelKind.State) return false; break;
            }

            if (!string.IsNullOrEmpty(Search)
                && r.Name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0) return false;

            return true;
        }

        // ---------------- 선택 ----------------

        public bool IsSelected(int io)
        {
            return io >= 0 && io < All.Count && _selected.Contains(All[io].Name);
        }

        public bool IsSelectedName(string name) { return _selected.Contains(name); }

        /// <summary>
        /// 한 번 누르면 켜지고, 같은 것을 다시 누르면 꺼집니다.
        /// 다른 IO 를 눌러도 이미 켠 것은 그대로 둡니다.
        /// </summary>
        public void Toggle(int io)
        {
            if (io < 0 || io >= All.Count) return;
            string n = All[io].Name;
            if (!_selected.Remove(n)) _selected.Add(n);
        }

        public void SetSelected(int io, bool on)
        {
            if (io < 0 || io >= All.Count) return;
            if (on) _selected.Add(All[io].Name); else _selected.Remove(All[io].Name);
        }

        public void SetSelectedByName(string name, bool on)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (on) _selected.Add(name); else _selected.Remove(name);
        }

        /// <summary>
        /// Shift 범위. 화면에 보이는 줄 순서를 따릅니다. 그룹 순서를 바꾼
        /// 뒤에도 "보이는 대로" 잡히는 이유가 이것입니다.
        /// </summary>
        public void SelectRangeByRow(int rowA, int rowB)
        {
            if (rowA > rowB) { int t = rowA; rowA = rowB; rowB = t; }
            for (int r = rowA; r <= rowB && r < Rows.Count; r++)
            {
                if (r < 0) continue;
                if (Rows[r].Kind != RowKind.Io) continue;
                SetSelected(Rows[r].Io, true);
            }
        }

        public void SelectAllVisible(bool on)
        {
            for (int r = 0; r < Rows.Count; r++)
                if (Rows[r].Kind == RowKind.Io) SetSelected(Rows[r].Io, on);
        }

        public void ClearSelection() { _selected.Clear(); }

        public void SelectGroup(int group, bool on)
        {
            for (int r = 0; r < Rows.Count; r++)
                if (Rows[r].Kind == RowKind.Io && Rows[r].Group == group) SetSelected(Rows[r].Io, on);
        }

        public bool GroupFullySelected(int group)
        {
            bool any = false;
            for (int r = 0; r < Rows.Count; r++)
            {
                if (Rows[r].Kind != RowKind.Io || Rows[r].Group != group) continue;
                any = true;
                if (!IsSelected(Rows[r].Io)) return false;
            }
            return any;
        }

        /// <summary>그래프에 그릴 IO 들. 왼쪽 목록에 보이는 차례 그대로입니다.</summary>
        public List<int> DrawOrder()
        {
            var list = new List<int>();
            var seen = new HashSet<int>();
            for (int r = 0; r < Rows.Count; r++)
            {
                if (Rows[r].Kind != RowKind.Io) continue;
                int io = Rows[r].Io;
                if (!IsSelected(io) || !seen.Add(io)) continue;
                list.Add(io);
            }
            // 필터 때문에 화면에서 빠진 선택 IO 도 그래프에는 남겨 둡니다.
            if (list.Count < _selected.Count)
            {
                for (int i = 0; i < All.Count; i++)
                    if (IsSelected(i) && seen.Add(i)) list.Add(i);
            }
            return list;
        }

        public List<string> SelectedNames()
        {
            var list = new List<string>();
            foreach (int i in DrawOrder()) list.Add(All[i].Name);
            return list;
        }
    }
}
