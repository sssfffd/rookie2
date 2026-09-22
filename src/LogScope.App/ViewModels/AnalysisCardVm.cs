using System.Collections.Generic;
using LogScope.App.Infrastructure;

namespace LogScope.App.ViewModels
{
    /// <summary>분석 칸 안에 적는 숫자 하나. "달라진 IO  12개" 같은 것.</summary>
    public sealed class StatVm
    {
        public string Label { get; private set; }
        public string Value { get; private set; }

        public StatVm(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>
    /// 메인 화면의 분석 칸 하나. 이름과 점수를 들고 있고, 누르면 그 분석
    /// 화면으로 넘어갑니다.
    /// </summary>
    public sealed class AnalysisCardVm : ObservableObject
    {
        /// <summary>몇 번 분석인지 (1 부터). 누르면 갈 화면 번호와 같습니다.</summary>
        public int Number { get; private set; }

        public string Title { get; private set; }

        /// <summary>이 분석이 무엇을 보는지 한 줄로.</summary>
        public string Summary { get; private set; }

        /// <summary>아직 만들지 않은 분석이면 거짓.</summary>
        public bool Ready { get; private set; }

        public AnalysisCardVm(int number, string title, string summary, bool ready)
        {
            Number = number;
            Title = title;
            Summary = summary;
            Ready = ready;
            _stats = new List<StatVm>();
        }

        /// <summary>만점. 점수를 백분율처럼 읽을 수 있게 100 으로 둡니다.</summary>
        public const double MaxScore = 100.0;

        // ---- 점수 -------------------------------------------------------
        //
        // 로그를 견주기 전에는 <b>점수가 없습니다.</b> 0 점으로 두면 "다 틀렸다"
        // 는 뜻이 되고, 만점으로 두면 "다 맞았다" 는 뜻이 됩니다. 아직 아무것도
        // 재지 않았는데 둘 다 거짓말입니다. 그래서 "??" 로 비워 둡니다.

        private bool _has;
        private double _score;

        /// <summary>점수가 있는지. 아직 안 재 봤으면 거짓입니다.</summary>
        public bool HasScore { get { return _has; } }

        public double Score { get { return _score; } }

        /// <summary>점수를 지웁니다 (아직 안 재 본 상태로).</summary>
        public void ClearScore()
        {
            if (!_has) return;
            _has = false;
            RaiseScore();
        }

        public void SetScore(double value)
        {
            if (_has && _score == value) return;
            _has = true;
            _score = value;
            RaiseScore();
        }

        private void RaiseScore()
        {
            Raise("HasScore"); Raise("Score");
            Raise("ScoreText"); Raise("ScoreNote"); Raise("IsFull");
        }

        /// <summary>칸에 크게 적는 점수. 아직 안 재 봤으면 "??".</summary>
        public string ScoreText
        {
            get { return _has ? _score.ToString("0.#") : "??"; }
        }

        /// <summary>점수 뒤에 붙는 "/ 100".</summary>
        public string ScoreOutOf
        {
            get { return "/ " + MaxScore.ToString("0"); }
        }

        /// <summary>만점인지. 만점일 때만 초록으로 칠합니다.</summary>
        public bool IsFull { get { return _has && _score >= MaxScore; } }

        /// <summary>점수 아래의 작은 글.</summary>
        public string ScoreNote
        {
            get
            {
                if (!Ready) return "아직 만들지 않았습니다";
                if (!_has) return "로그를 견주면 점수가 나옵니다";
                return _score >= MaxScore ? "만점" : "감점 " + (MaxScore - _score).ToString("0.#");
            }
        }

        /// <summary>칸 머리에 적는 번호표.</summary>
        public string Badge { get { return "분석 " + Number; } }

        // ---- 칸 안에 적는 숫자들 -----------------------------------------

        private List<StatVm> _stats;
        public List<StatVm> Stats
        {
            get { return _stats; }
        }

        public void SetStats(List<StatVm> stats)
        {
            _stats = stats ?? new List<StatVm>();
            Raise("Stats"); Raise("HasStats");
        }

        public bool HasStats { get { return _stats.Count > 0; } }
    }
}
