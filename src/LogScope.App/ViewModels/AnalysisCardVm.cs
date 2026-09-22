using LogScope.App.Infrastructure;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 메인 화면의 분석 칸 하나. 이름과 점수를 들고 있고, 누르면 그 분석
    /// 화면으로 넘어갑니다.
    ///
    /// <b>점수는 아직 계산하지 않습니다.</b> 지금은 모두 만점으로 둡니다.
    /// 분석마다 무엇을 어떻게 점수로 매길지는 아직 정해지지 않았는데,
    /// 그걸 정하기 전에 아무 수식이나 넣어 두면 그 수가 진짜인 줄 알고
    /// 읽게 됩니다. 채울 자리는 <see cref="MainVm.Refresh"/> 한 곳입니다.
    /// </summary>
    public sealed class AnalysisCardVm : ObservableObject
    {
        /// <summary>몇 번 분석인지 (1 부터). 누르면 갈 화면 번호와 같습니다.</summary>
        public int Number { get; private set; }

        public string Title { get; private set; }

        /// <summary>이 분석이 무엇을 보는지 한 줄로.</summary>
        public string Summary { get; private set; }

        /// <summary>아직 만들지 않은 분석이면 거짓. 칸에 그렇게 적습니다.</summary>
        public bool Ready { get; private set; }

        public AnalysisCardVm(int number, string title, string summary, bool ready)
        {
            Number = number;
            Title = title;
            Summary = summary;
            Ready = ready;
            _score = MaxScore;
        }

        /// <summary>만점. 점수를 백분율처럼 읽을 수 있게 100 으로 둡니다.</summary>
        public const double MaxScore = 100.0;

        private double _score;
        public double Score
        {
            get { return _score; }
            set
            {
                if (_score == value) return;
                _score = value;
                Raise("Score"); Raise("ScoreText"); Raise("ScoreNote");
            }
        }

        /// <summary>칸에 크게 적는 점수.</summary>
        public string ScoreText
        {
            get { return _score.ToString("0.#"); }
        }

        /// <summary>점수 아래의 작은 글.</summary>
        public string ScoreNote
        {
            get
            {
                if (!Ready) return "아직 만들지 않았습니다";
                return _score >= MaxScore ? "만점 (" + MaxScore.ToString("0") + "점 만점)"
                                          : MaxScore.ToString("0") + "점 만점";
            }
        }

        /// <summary>칸 머리에 적는 번호표.</summary>
        public string Badge { get { return "분석 " + Number; } }
    }
}
