using LogScope.App.Infrastructure;

namespace LogScope.App.ViewModels
{
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
