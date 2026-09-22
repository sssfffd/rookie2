using System.Collections.Generic;
using LogScope.App.Infrastructure;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 점수를 어느 색으로 보여 줄지 가르는 자리.
    ///
    /// <b>숫자는 여기 한 곳에만 둡니다.</b> 칸의 점수 색과 화면 아래의
    /// 색 설명이 같은 값을 봐야 합니다 — 따로 적어 두면 한쪽만 고치게 되고,
    /// 그러면 설명이 화면과 다른 말을 하게 됩니다.
    /// </summary>
    public static class ScoreBands
    {
        /// <summary>이 점수부터 초록.</summary>
        public const double GoodFrom = 90;

        /// <summary>이 점수부터 노랑. 그 아래는 빨강입니다.</summary>
        public const double WarnFrom = 70;

        public const string GradeNone = "none";
        public const string GradeGood = "good";
        public const string GradeWarn = "warn";
        public const string GradeBad = "bad";

        public static string GradeOf(double score)
        {
            if (score >= GoodFrom) return GradeGood;
            if (score >= WarnFrom) return GradeWarn;
            return GradeBad;
        }

        /// <summary>화면 아래에 적는 색 설명. 위에서부터 좋은 쪽입니다.</summary>
        public static List<BandVm> Legend()
        {
            return new List<BandVm>
            {
                new BandVm(GradeGood, Range(GoodFrom, 100), "이상 없음"),
                new BandVm(GradeWarn, Range(WarnFrom, GoodFrom - 1), "확인 필요"),
                new BandVm(GradeBad,  Range(0, WarnFrom - 1),        "점검 필요"),
            };
        }

        private static string Range(double a, double b)
        {
            return a.ToString("0") + " ~ " + b.ToString("0");
        }
    }

    /// <summary>색 설명 한 줄. "초록  90 ~ 100  이상 없음".</summary>
    public sealed class BandVm
    {
        public string Grade { get; private set; }
        public string RangeText { get; private set; }
        public string Meaning { get; private set; }

        public BandVm(string grade, string rangeText, string meaning)
        {
            Grade = grade;
            RangeText = rangeText;
            Meaning = meaning;
        }
    }

    /// <summary>
    /// 분석 칸 아래 요약의 한 줄. <b>그룹 하나</b>입니다.
    ///
    /// 누르면 그 그룹의 IO 만 히트맵에 남깁니다 — 어디가 어떻게 어긋났는지는
    /// 그 화면이 제일 잘 보여 줍니다.
    /// </summary>
    public sealed class GroupSummaryVm
    {
        public string GroupName { get; private set; }

        /// <summary>이 그룹에 담긴 IO 이름들. 히트맵을 추릴 때 그대로 넘깁니다.</summary>
        public List<string> Members { get; private set; }

        public int MemberCount { get; private set; }
        public int ChangedCount { get; private set; }

        public GroupSummaryVm(string groupName, List<string> members, int memberCount, int changedCount)
        {
            GroupName = groupName ?? string.Empty;
            Members = members ?? new List<string>();
            MemberCount = memberCount;
            ChangedCount = changedCount;
        }

        public string CountText
        {
            get
            {
                if (MemberCount == 0) return "IO 없음";
                return ChangedCount.ToString("N0") + " / " + MemberCount.ToString("N0") + " 달라짐";
            }
        }

        /// <summary>색을 가르는 값. 하나도 안 달라졌으면 초록입니다.</summary>
        public string Grade
        {
            get
            {
                if (MemberCount == 0) return ScoreBands.GradeNone;
                if (ChangedCount == 0) return ScoreBands.GradeGood;
                // 절반 넘게 어긋났으면 빨강. 몇 개만이면 노랑.
                return ChangedCount * 2 >= MemberCount ? ScoreBands.GradeBad : ScoreBands.GradeWarn;
            }
        }

        public bool CanOpen { get { return MemberCount > 0; } }

        // ---- 그룹 줄 아래: 어떤 차이가 났는지 --------------------------
        //
        // <b>자리만 잡아 둔 것입니다.</b> 무엇을 어떤 말로 적을지 아직
        // 정해지지 않았습니다 — "밸브 3 개가 늦게 열림" 처럼 사람이 읽는
        // 문장이 될 텐데, 그걸 무엇으로 판단할지가 먼저입니다.
        //
        // 미리 자리를 잡아 두는 이유는, 나중에 글이 들어올 때 줄 높이가
        // 달라져 목록이 통째로 다시 접히지 않게 하려는 것입니다.
        //
        // 채울 자리는 여기 한 곳입니다.
        private string _detail = string.Empty;

        public string DetailText
        {
            get { return _detail.Length > 0 ? _detail : "미구현"; }
        }

        public void SetDetail(string text)
        {
            _detail = text ?? string.Empty;
        }
    }

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
            Raise("ScoreText"); Raise("ScoreNote"); Raise("Grade");
        }

        /// <summary>
        /// 점수 색을 가르는 값. 아직 안 재 봤으면 "none" (회색) 입니다 —
        /// 안 재 본 값에 초록이나 빨강을 칠하면 재 본 값으로 읽힙니다.
        /// </summary>
        public string Grade
        {
            get { return _has ? ScoreBands.GradeOf(_score) : ScoreBands.GradeNone; }
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

        /// <summary>점수 아래의 작은 글.</summary>
        public string ScoreNote
        {
            get
            {
                if (!Ready) return "아직 만들지 않았습니다";
                if (!_has) return "로그를 견주면 점수가 나옵니다";
                if (_score >= MaxScore) return "만점";
                switch (ScoreBands.GradeOf(_score))
                {
                    case ScoreBands.GradeGood: return "이상 없음";
                    case ScoreBands.GradeWarn: return "확인 필요";
                    default: return "점검 필요";
                }
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

        // ---- 칸 아래의 요약 (그룹별) --------------------------------------

        private List<GroupSummaryVm> _groups = new List<GroupSummaryVm>();
        public List<GroupSummaryVm> Groups { get { return _groups; } }

        public void SetGroups(List<GroupSummaryVm> groups)
        {
            _groups = groups ?? new List<GroupSummaryVm>();
            Raise("Groups"); Raise("HasGroups"); Raise("GroupNote");
        }

        public bool HasGroups { get { return _groups.Count > 0; } }

        /// <summary>요약에 적을 것이 없을 때 그 자리에 적는 글.</summary>
        public string GroupNote
        {
            get
            {
                if (_groups.Count > 0) return string.Empty;
                if (!Ready) return "아직 만들지 않았습니다.";
                return "그룹이 없거나 아직 견주지 않았습니다. 그래프 화면 왼쪽에서 IO 를 그룹으로 묶을 수 있습니다.";
            }
        }
    }
}
