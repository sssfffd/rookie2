using LogScope.App.Infrastructure;
using LogScope.Core.Model;

namespace LogScope.App.ViewModels
{
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

        private string _status = "same";
        /// <summary>
        /// 줄 색을 정하는 값. XAML 의 DataTrigger 가 이 글자를 봅니다.
        ///   same        양쪽에 있고 값도 같음
        ///   changed     양쪽에 있는데 값이 다름
        ///   onlyBefore  이전 로그에만 있음
        ///   onlyAfter   이후 로그에만 있음
        /// </summary>
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
                    case "onlyBefore": return "이전에만";
                    case "onlyAfter": return "이후에만";
                    default: return string.Empty;
                }
            }
        }
    }
}
