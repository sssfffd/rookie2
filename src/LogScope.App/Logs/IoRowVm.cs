using LogScope.Core.Model;

using LogScope.App.Common;
using LogScope.App.Settings;
namespace LogScope.App.Logs
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
}
