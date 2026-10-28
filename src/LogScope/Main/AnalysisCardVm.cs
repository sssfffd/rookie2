using System.Collections.Generic;
using LogScope.Common;

namespace LogScope.Main
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
    /// <summary>
    /// 요약 칸의 글 한 줄. 분석 1 은 그룹 줄(<see cref="GroupSummaryVm"/>)을
    /// 쓰고, <b>분석 2(DB)는 이 줄</b>을 씁니다 — 거기에는 그룹이 없고
    /// "차이 발생 12개" 처럼 센 수가 들어갑니다.
    ///
    /// 그룹 줄과 따로 둔 까닭: 그룹 줄은 누르면 그 그룹의 IO 만 골라
    /// 그래프로 넘어가는 줄입니다. 거기에 DB 수를 억지로 끼우면 "이
    /// 줄을 누르면 무엇이 되는지" 가 줄마다 달라집니다.
    /// </summary>
    public sealed class CardLineVm
    {
        public string Label { get; private set; }
        public string Value { get; private set; }

        /// <summary>점 색 (<see cref="ScoreBands"/> 의 등급). 빈 글자면 회색.</summary>
        public string Grade { get; private set; }

        /// <summary>줄 아래에 작게 붙이는 글. 없으면 빈 글자.</summary>
        public string Detail { get; private set; }

        /// <summary>
        /// 누르면 그 분석 화면에서 찾아 줄 것. IO 이름입니다. 빈 글자면
        /// 화면만 열고 아무것도 안 찾습니다.
        /// </summary>
        public string Target { get; private set; }

        /// <summary>누를 수 있는 줄인지 (찾아 줄 것이 있는지).</summary>
        public bool CanOpen { get { return Target.Length > 0; } }

        /// <summary>
        /// 참이면 오른쪽 값(<see cref="Value"/>)을 <b>빨간 글씨</b>로 (1.08).
        /// 분석 2 의 "1 → 2" 처럼 값이 달라졌다는 뜻의 값에만 씁니다 —
        /// 분석 1 의 "3/20" 같은 수까지 빨개지면 빨강의 뜻이 흐려집니다.
        /// </summary>
        public bool ValueIsChange { get; set; }

        /// <summary>
        /// 이 줄이 어느 칸의 줄인지 — 누르면 그 분석 화면으로 갑니다 (1.29).
        /// 줄을 적는 칸이 분석 2 하나이던 때의 뜻을 지키려고 기본은 2.
        /// </summary>
        public int Card { get; set; }

        public CardLineVm(string label, string value, string grade, string detail)
            : this(label, value, grade, detail, string.Empty) { }

        public CardLineVm(string label, string value, string grade, string detail, string target)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Grade = grade ?? ScoreBands.GradeNone;
            Detail = detail ?? string.Empty;
            Target = target ?? string.Empty;
            Card = 2;
        }
    }

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
        // 자리만 잡아 둔 것입니다. 무엇을 어떤 말로 적을지 아직
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

        /// <summary>
        /// 이 칸 안에 <b>이전/이후 로그 칸</b>을 넣을지. 로그를 여는 일은
        /// 로그 비교(분석 1)의 준비라서 그 칸 안에 둡니다. 번호로 보지 않고
        /// 따로 둔 이유는, 분석 1 의 이름과 차례는 config.txt 로 바뀔 수
        /// 있어도 "로그를 쓰는 분석" 은 하나라는 뜻을 담으려는 것입니다.
        /// </summary>
        public bool HasLogBox { get; private set; }

        /// <summary>
        /// 이 칸 안에 <b>이전/이후 DB 칸</b>을 넣을지. 로그 칸과 같은 뜻으로,
        /// 견줄 자리를 정하는 일은 DB 비교(분석 2)의 준비입니다.
        /// </summary>
        public bool HasDbBox { get; private set; }

        /// <summary>
        /// 이 칸 안에 <b>분석 3 의 이전/이후 칸</b>을 넣을지 (1.29). 자리는
        /// Common/Analysis3Link 에 있고, 분석 3 화면이 거기서 읽습니다.
        /// </summary>
        public bool HasA3Box { get; private set; }

        /// <summary>
        /// 결과가 낱말일 수도 점수일 수도 있는 칸 (분석 3). 결과가 오기 전에는
        /// 판정 칸처럼 아무것도 크게 적지 않고 ("?? / 100" 을 띄우지 않음),
        /// 점수가 오면 점수 칸이 됩니다.
        /// </summary>
        private readonly bool _scoreOptional;

        /// <summary>
        /// 점수(0~100) 자리를 <b>쓰지 않는</b> 칸인지. 분석 2 가 그렇습니다 —
        /// 점수가 아니라 PASS·WARNING·FAIL 을 적습니다.
        ///
        /// 이 깃발이 없던 0.99 까지, 분석 2 칸은 DB 를 읽기 전에
        /// <c>?? / 100</c> 과 "Log 파일을 비교한 후 점수가 나옵니다" 를
        /// 띄웠습니다. <b>DB 칸에 로그 이야기가 적혀 있었습니다.</b>
        /// </summary>
        public bool UsesVerdict
        {
            get { return Number == 2 || (_scoreOptional && !_has); }
        }

        public AnalysisCardVm(int number, string title, string summary, bool ready)
        {
            Number = number;
            Title = title;
            Summary = summary;
            Ready = ready;
            HasLogBox = number == 1;
            HasDbBox = number == 2;
            HasA3Box = number == 3;
            _scoreOptional = number == 3;
            _stats = new List<StatVm>();
        }

        /// <summary>만점. 점수를 백분율처럼 읽을 수 있게 100 으로 둡니다.</summary>
        public const double MaxScore = 100.0;

        // ---- 점수 -------------------------------------------------------
        //
        // 로그를 견주기 전에는 점수가 없습니다. 0 점으로 두면 "다 틀렸다"
        // 는 뜻이 되고, 만점으로 두면 "다 맞았다" 는 뜻이 됩니다. 아직 아무것도
        // 재지 않았는데 둘 다 거짓말입니다. 그래서 "??" 로 비워 둡니다.

        private bool _has;
        private double _score;

        // ---- 점수 대신 낱말 (PASS · WARNING · FAIL) ----
        //
        // 분석 2(DB)는 점수가 아니라 판정입니다. "무엇을 어떻게 100 점으로
        // 환산했나" 를 묻게 만들 바에, 사람이 알고 싶은 것 하나만
        // 적습니다 — 그냥 넘겨도 되나.
        //
        // 점수 자리를 그대로 씁니다 (ScoreText · Grade · ScoreNote). 그래서
        // 화면은 고칠 것이 거의 없고, 칸마다 다른 틀을 둘 일도 없습니다.
        // 낱말이 들어 있으면 점수는 무시됩니다.

        private string _word = string.Empty;
        private string _wordGrade = ScoreBands.GradeNone;
        private string _wordNote = string.Empty;

        /// <summary>점수 대신 낱말을 쓰는 칸인지.</summary>
        public bool HasVerdict { get { return _word.Length > 0; } }

        /// <summary>
        /// 낱말과 색, 그리고 그 아래 작은 글을 함께 받습니다.
        ///
        /// 셋을 따로 받으면 "낱말은 PASS 인데 색은 빨강" 같은 어긋남이
        /// 생길 수 있습니다. 가르는 쪽(DbVerdict)이 한 번에 넘깁니다.
        /// </summary>
        public void SetVerdict(string word, string grade, string note)
        {
            SetVerdict(word, grade, note, string.Empty);
        }

        /// <param name="count">
        /// 낱말 아래 <b>크게</b> 적을 수 ("12"). 분석 2 의 차이 발생 개수입니다.
        /// 빈 글자면 그 줄을 숨깁니다.
        /// </param>
        public void SetVerdict(string word, string grade, string note, string count)
        {
            _word = word ?? string.Empty;
            _wordGrade = grade ?? ScoreBands.GradeNone;
            _wordNote = note ?? string.Empty;
            _verdictCount = count ?? string.Empty;
            RaiseScore();
            Raise("HasVerdict"); Raise("VerdictCount");
        }

        private string _verdictCount = string.Empty;

        /// <summary>
        /// 낱말 아래 크게 적는 수 — 분석 2 의 <b>차이 발생 개수</b>.
        ///
        /// 1.02 까지는 이 수가 작은 글 한 줄 안에 IO 이름들과 섞여 있었습니다
        /// ("차이 발생 3개 — TEMP_MAX, PRES_SET …"). 보는 사람이 제일 먼저
        /// 찾는 수가 그 줄에서 제일 안 보였습니다. 이름은 아래 요약 칸이
        /// 한 줄씩 적으므로 여기서는 수만 크게 적습니다.
        /// </summary>
        public string VerdictCount { get { return _verdictCount; } }

        /// <summary>
        /// 점수 칸(분석 1)에서 점수 오른쪽에 적을 "차이 발생 N 개" (1.21). 분석 2 의
        /// 판정 옆 개수와 <b>같은 자리 · 같은 크기</b>라 두 칸의 줄이 그대로 맞습니다.
        /// 빈 글자면 숨깁니다.
        /// </summary>
        public void SetCount(string count)
        {
            string v = count ?? string.Empty;
            if (_verdictCount == v) return;
            _verdictCount = v;
            Raise("VerdictCount");
        }

        public void ClearVerdict()
        {
            if (_word.Length == 0) return;
            _word = string.Empty;
            _wordGrade = ScoreBands.GradeNone;
            _wordNote = string.Empty;
            _verdictCount = string.Empty;
            RaiseScore();
            Raise("HasVerdict"); Raise("VerdictCount");
        }

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
            Raise("ScoreOutOf"); Raise("ScoreSize"); Raise("HasScoreText");
        }

        /// <summary>
        /// 점수 색을 가르는 값. 아직 안 재 봤으면 "none" (회색) 입니다 —
        /// 안 재 본 값에 초록이나 빨강을 칠하면 재 본 값으로 읽힙니다.
        /// </summary>
        public string Grade
        {
            get
            {
                if (HasVerdict) return _wordGrade;
                return _has ? ScoreBands.GradeOf(_score) : ScoreBands.GradeNone;
            }
        }

        /// <summary>칸에 크게 적는 글. 점수거나 낱말이거나, 아직이면 "??".</summary>
        public string ScoreText
        {
            get
            {
                if (HasVerdict) return _word;

                // 판정 칸은 점수를 안 씁니다. 아직 읽기 전이면 <b>아무것도</b>
                // 적지 않습니다 — "??" 는 "점수가 있을 자리인데 아직 안
                // 나왔다" 는 뜻이고, 이 칸에는 점수가 올 자리가 없습니다.
                // 읽기 전이라는 말은 아래 요약 칸이 합니다.
                if (UsesVerdict) return string.Empty;

                return _has ? _score.ToString("0.#") : "??";
            }
        }

        /// <summary>
        /// 크게 적는 글이 있는지 (없으면 그 줄을 아예 숨깁니다).
        ///
        /// 위의 <see cref="HasScore"/> 와 다릅니다 — 그쪽은 "점수를 재
        /// 봤는지" 이고, 이쪽은 "적을 글자가 있는지" 입니다. 분석 2 는
        /// 점수를 아예 안 쓰므로 읽기 전에는 적을 글자가 없습니다.
        /// </summary>
        public bool HasScoreText { get { return ScoreText.Length > 0; } }

        /// <summary>점수 뒤에 붙는 "/ 100". 낱말 칸에는 안 붙습니다.</summary>
        public string ScoreOutOf
        {
            get
            {
                if (HasVerdict || UsesVerdict) return string.Empty;
                return "/ " + MaxScore.ToString("0");
            }
        }

        /// <summary>
        /// 크게 적는 글의 글자 크기. <b>분석 1 점수 · 분석 2 판정 낱말 · 차이
        /// 개수가 모두 같은 크기</b>입니다 — 다르면 칸마다 점수 줄 높이가
        /// 달라져 그 아래 이전/이후 줄이 나란히 안 놓입니다.
        ///
        /// 30 (1.09, 전에는 24). "WARNING  차이 발생 152 개" 가 한 줄에 약
        /// 290 px 이라, 창 폭 1280 이상이면 칸 안에 들어갑니다. 더 키우면
        /// 좁은 창에서 오른쪽이 잘립니다.
        /// </summary>
        public double ScoreSize { get { return 30.0; } }

        /// <summary>점수 아래의 작은 글.</summary>
        public string ScoreNote
        {
            get
            {
                if (HasVerdict) return _wordNote;
                if (!Ready) return "아직 만들지 않았습니다";

                // 판정 칸은 점수 이야기를 적지 않습니다 (위 ScoreText 참고).
                if (UsesVerdict) return string.Empty;

                if (!_has) return "Log 파일을 비교한 후 점수가 나옵니다";

                // 점수가 나온 뒤에는 아무것도 안 적습니다 (1.06). "이상 없음 ·
                // 확인 필요 · 점검 필요" 를 적었는데, 점수 색과 화면 아래 색
                // 설명이 이미 같은 말을 합니다.
                return string.Empty;
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

        // ---- 요약 칸의 글 줄 (분석 2) ----
        //
        // 분석 1 은 그룹 줄을 쓰고, 분석 2 는 센 수를 줄로 적습니다.
        // 칸은 하나라서 둘 중 들어 있는 쪽이 보입니다.

        private List<CardLineVm> _lines = new List<CardLineVm>();
        private string _lineNote = string.Empty;

        public IList<CardLineVm> Lines { get { return _lines; } }
        public bool HasLines { get { return _lines.Count > 0; } }

        /// <summary>
        /// 요약 칸에 적을 줄들. <paramref name="note"/> 는 줄이 없을 때 그
        /// 자리에 적는 글입니다 — <b>빈 칸으로 두면 "아직 안 봤다" 와
        /// "봤는데 아무것도 없다" 를 가릴 수 없습니다.</b>
        /// </summary>
        public void SetLines(List<CardLineVm> lines, string note)
        {
            _lines = lines ?? new List<CardLineVm>();
            _lineNote = note ?? string.Empty;
            Raise("Lines"); Raise("HasLines"); Raise("GroupNote");
        }

        /// <summary>요약에 적을 것이 없을 때 그 자리에 적는 글.</summary>
        public string GroupNote
        {
            get
            {
                if (_groups.Count > 0 || _lines.Count > 0) return string.Empty;
                if (_lineNote.Length > 0) return _lineNote;
                if (!Ready) return "아직 만들지 않았습니다.";
                return "그룹이 없거나 아직 비교하지 않았습니다. 그래프 화면 왼쪽에서 IO 를 그룹으로 묶을 수 있습니다.";
            }
        }
    }
}
