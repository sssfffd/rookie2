using System.Collections.Generic;
using LogScope.App.Infrastructure;
using LogScope.App.Services;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 메인 화면. 분석 셋을 칸으로 늘어놓고 각각의 점수를 보여 줍니다.
    /// 칸을 누르면 그 분석 화면으로 넘어갑니다.
    /// </summary>
    public sealed class MainVm : ObservableObject
    {
        private readonly AppState _state;

        public List<AnalysisCardVm> Cards { get; private set; }

        public MainVm(AppState state)
        {
            _state = state;
            Cards = new List<AnalysisCardVm>
            {
                new AnalysisCardVm(1, "로그 비교",
                    "이전 로그와 이후 로그를 견줍니다. 대시보드 · 그래프 · 히트맵.", true),
                new AnalysisCardVm(2, "분석 2",
                    "아직 정해지지 않았습니다.", false),
                new AnalysisCardVm(3, "분석 3",
                    "아직 정해지지 않았습니다.", false),
            };
            Refresh();
        }

        /// <summary>
        /// 점수를 다시 셉니다.
        ///
        /// <b>지금은 모두 만점입니다.</b> 분석마다 무엇을 어떻게 점수로 매길지
        /// 아직 정해지지 않았습니다. 여기에 아무 수식이나 넣어 두면 그 수가
        /// 진짜인 줄 알고 읽게 되므로, 정해질 때까지 만점으로 둡니다.
        /// 나중에 채울 자리는 여기 한 곳입니다.
        /// </summary>
        public void Refresh()
        {
            for (int i = 0; i < Cards.Count; i++) Cards[i].Score = AnalysisCardVm.MaxScore;
            Raise("HasLogs"); Raise("Hint");
        }

        public bool HasLogs { get { return _state.HasAny; } }

        /// <summary>칸 아래에 적는 안내.</summary>
        public string Hint
        {
            get
            {
                return _state.HasAny
                    ? "칸을 누르면 그 분석 화면으로 넘어갑니다. 왼쪽 위 [LogScope] 로도 화면을 바꿀 수 있습니다."
                    : "위쪽의 [이전 로그 열기] / [이후 로그 열기] 로 파일을 먼저 열어 주세요.";
            }
        }
    }
}
