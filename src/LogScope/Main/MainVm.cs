using System;
using System.Collections.Generic;
using System.IO;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.Judge;
using LogScope.Analysis1.LogFile;
using LogScope.Analysis2;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면. 지금 열어 둔 로그를 한 줄로 알려 주고, 분석 셋을 칸으로
    /// 늘어놓아 각각의 점수를 보여 줍니다. 칸을 누르면 그 분석 화면으로
    /// 넘어갑니다.
    /// </summary>
    public sealed class MainVm : ObservableObject
    {
        private readonly AppState _state;

        public List<AnalysisCardVm> Cards { get; private set; }

        public MainVm(AppState state)
        {
            _state = state;
            // 칸 이름과 한 줄 설명은 실행 파일 옆의 config.txt 에서 옵니다.
            // 여기에 적어 두면 이름을 바꿀 때마다 다시 빌드해야 합니다.
            AppConfig names = AppConfig.Current;
            Cards = new List<AnalysisCardVm>
            {
                new AnalysisCardVm(1, names.Title(1), names.Summary(1), true),
                // 분석 2 는 만들어져 있습니다 (0.93 까지는 거짓으로 남아
                // 있었는데, 그러면 요약 칸에 "아직 만들지 않았습니다" 가 떴습니다).
                new AnalysisCardVm(2, names.Title(2), names.Summary(2), true),
                // 분석 3 은 Analysis3 폴더를 갈아 끼워 붙입니다 (1.29 부터 칸이 살아 있음).
                // 결과는 Common/Analysis3Link 로 올라옵니다.
                new AnalysisCardVm(3, names.Title(3), names.Summary(3), true),
            };
            Llm = new LlmPanelVm();
            ReloadHistory();   // Refresh 까지 안에서 같이 돕니다.
            Refresh();
        }

        /// <summary>
        /// 점수와 숫자들을 다시 셉니다. 로그를 열거나 다시 견줄 때마다 불립니다.
        ///
        /// <b>아직 점수를 계산하지는 않습니다.</b> 견주기 전에는 "??" 로 비워
        /// 두고, 견주고 나면 만점으로 둡니다. 무엇을 어떻게 점수로 매길지
        /// 정해지기 전에 아무 수식이나 넣어 두면 그 수가 진짜인 줄 알고 읽게
        /// 됩니다. 나중에 채울 자리는 여기 한 곳입니다.
        /// </summary>
        public void Refresh()
        {
            CompareResult r = _state.Comparison;

            if (r == null)
            {
                Cards[0].ClearScore();
                Cards[0].ClearVerdict();
                Cards[0].SetCount(string.Empty);
                Cards[0].SetStats(new List<StatVm>());
            }
            else
            {
                // 1.56: 100 점 고정 자리에 <b>정상 판정</b> (PASS · WARNING · FAIL) 과 허용 안 된 오차 수.
                // 판정과 오차 목록은 분석 1 의 [오차 목록] 화면 (Analysis1/Judge).
                Cards[0].ClearScore();
                JudgeResult judge = _state.Judgement;
                if (judge != null)
                {
                    Cards[0].SetVerdict(judge.Word, JudgeVm.GradeOf(judge.Grade), string.Empty,
                                        judge.OpenCount.ToString("N0"));
                }
                else
                {
                    Cards[0].ClearVerdict();
                    // 견준 결과를 칸에 적습니다.
                    //
                    // 0.85 에 뺐던 것을 0.92 에 되넣었습니다. 뺀 까닭은 같은 수가
                    // 세 군데(칸 · 종합 · 대시보드)에 적혀 있던 것이었는데, 파일을
                    // <b>끌어다 놓자마자</b> 읽고 견주는 흐름에서는 칸이 그대로
                    // 있으면 "넣었는데 아무 일도 안 했다" 로 보입니다. 메인 화면만
                    // 보고도 견줘졌다는 것을 알아야 합니다.
                    //
                    // 종합 칸의 수와 같은 값을 씁니다 (둘 다 CompareResult 를
                    // 그대로 읽습니다) — 따로 셈하면 두 수가 어긋납니다.
                    //
                    // 1.21: 차이 난 IO 개수는 <b>점수 오른쪽</b>으로 옮겼습니다 (분석 2 의
                    // "FAIL 차이 발생 N 개" 와 같은 자리 · 같은 크기 — 줄 맞춤이 그대로).
                    // "한쪽에만 있는 IO" 는 뺐습니다 (대시보드에 목록으로 있음).
                    // 그래서 숫자 칸(Stats)은 비고, 비면 그 줄째 안 보입니다.
                    Cards[0].SetCount(r.ChangedCount.ToString("N0"));
                }
                Cards[0].SetStats(new List<StatVm>());
            }

            Cards[0].SetGroups(BuildGroups(r));

            // 분석 2 · 3 은 그룹 줄을 쓰지 않습니다. 점수는 비워 두고, 분석 3 은
            // 바로 아래 ShowA3Result 가 올라온 결과로 다시 적습니다.
            for (int i = 1; i < Cards.Count; i++)
            {
                Cards[i].ClearScore();
                Cards[i].SetGroups(new List<GroupSummaryVm>());
            }

            ShowDbVerdict();
            ShowA3Result();

            // History 를 펼쳐 보는 중이면 지금 결과 위에 그때의 요약을 덮습니다 (1.42).
            // 지금 결과는 위에서 늘 새로 적으므로 [지금 결과로] 를 누르면 그대로 돌아옵니다.
            ApplyViewing();

            RefreshDeltas();

            RefreshLlmChecks();
            RefreshLlmServer();

            Raise("HasLogs"); Raise("HasBoth"); Raise("CanSave");
            Raise("IsViewingHistory"); Raise("ViewingText");
            Raise("BeforeTime"); Raise("AfterTime");
            Raise("BeforeFileName"); Raise("AfterFileName");
            Raise("BeforeText"); Raise("AfterText");
            Raise("BeforePath"); Raise("AfterPath");
            Raise("DbBeforePath"); Raise("DbAfterPath");
            Raise("DbBeforeName"); Raise("DbAfterName");
            Raise("DbBeforeNote"); Raise("DbAfterNote");
            Raise("BeforeHint"); Raise("AfterHint");
            Raise("BeforeShown"); Raise("AfterShown");
            Raise("BeforeDetail"); Raise("AfterDetail");
            Raise("DbBeforeHint"); Raise("DbAfterHint");
            Raise("DbBeforeKind"); Raise("DbAfterKind");
            Raise("A3BeforePath"); Raise("A3AfterPath");
            Raise("A3BeforeName"); Raise("A3AfterName");
            Raise("A3BeforeHint"); Raise("A3AfterHint");
            Raise("A3BeforeKind"); Raise("A3AfterKind");
        }

        /// <summary>
        /// 분석 2(DB) 칸에 <b>PASS · WARNING · FAIL</b> 을 적습니다.
        ///
        /// 점수가 아닌 이유는 <see cref="LogScope.Analysis2.DbVerdict"/> 에 적어
        /// 두었습니다. 가르는 규칙도 거기 한 곳입니다 — 여기서 또 가르면
        /// 낱말과 색이 어긋나는 날이 옵니다.
        ///
        /// 한 번도 안 읽었으면 <b>비워 둡니다.</b> 0 개를 PASS 로 적으면
        /// "봤는데 차이가 없다" 가 되어, 아직 안 본 것과 가릴 수 없습니다.
        /// </summary>
        private void ShowDbVerdict()
        {
            if (Cards.Count < 2) return;
            AnalysisCardVm card = Cards[1];

            if (!_state.HasDbResult)
            {
                card.ClearVerdict();
                card.SetLines(new List<CardLineVm>(),
                    "아직 읽지 않았습니다. 위 칸에 견줄 자리를 넣고 [분석] 을 누르면 읽습니다.");
                return;
            }

            var t = new DbDiffList.Tally();
            t.Changed = _state.DbChanged;
            t.Gone = _state.DbGone;
            t.Added = _state.DbAdded;

            string word = DbVerdict.Of(t.Changed);
            // 이름까지 넘깁니다 — WARNING 이면 어떤 IO 가 달라졌는지
            // 낱말 아래에 그대로 적힙니다.
            // 낱말 아래에는 <b>차이 발생 개수만</b> 크게 적습니다 (1.02).
            // 전에는 이 자리에 문장과 IO 이름이 섞여 있어서 수가 안 보였습니다.
            // 이름은 아래 요약 칸이 한 줄씩 적고, 없어진·새로 생긴 IO 는
            // 분석 2 화면 위쪽에 셋 다 적혀 있습니다.
            card.SetVerdict(word, GradeOf(word), string.Empty, t.Changed.ToString("N0"));
            List<CardLineVm> lines = DbLines(t);
            card.SetLines(lines, lines.Count == 0 ? NoDbLines(t) : string.Empty);
        }

        /// <summary>
        /// 분석 3 칸 (1.29). 분석 3 화면이 <see cref="Analysis3Link.Publish"/> 로 올린
        /// 결과를 적습니다 — 낱말이 있으면 분석 2 처럼, 없고 점수가 있으면 분석 1 처럼.
        ///
        /// 결과가 없으면 <b>비워 둡니다</b> (분석 2 와 같은 까닭: 0 이나 PASS 를 적으면
        /// "봤는데 괜찮다" 로 읽힙니다).
        /// </summary>
        /// <summary>
        /// 분석 3 이 올린 결과를 메인 칸에 적을지. 1.36 에 껐다가 1.37 에 다시 켰습니다 —
        /// 분석 3 안에서 돌린 결과가 메인 칸에 안 떠서. false 로 두면 Publish 해도 칸의
        /// 결과 자리(낱말 · 점수 · 개수 · 요약 줄)가 비고, 이전 · 이후 · 단추는 그대로입니다.
        /// </summary>
        public static readonly bool A3ResultOnCard = true;

        /// <summary>
        /// 분석 3 칸의 <b>머리 줄</b>(크게 적는 낱말 · 점수, 오른쪽 "차이 발생 N 개", 작은 글 Note)을
        /// 적을지 (1.38). <b>지금은 끔</b> — 사용자 요청으로 요약 줄(Lines)만 보입니다. 그 자리의
        /// 높이는 그대로 남아 칸 셋의 이전 · 이후 줄이 나란히 놓입니다. 켜려면 true.
        /// </summary>
        public static readonly bool A3HeadlineOnCard = false;

        private void ShowA3Result()
        {
            if (Cards.Count < 3) return;
            AnalysisCardVm card = Cards[2];
            Analysis3Result r = Analysis3Link.Result;

            if (!A3ResultOnCard)
            {
                card.ClearVerdict();
                card.ClearScore();
                card.SetCount(string.Empty);
                card.SetLines(new List<CardLineVm>(), string.Empty);
                return;
            }

            if (r == null)
            {
                card.ClearVerdict();
                card.ClearScore();
                card.SetCount(string.Empty);
                // 결과가 오기 전에는 안내 글도 적지 않고 비워 둡니다 (1.36 에 비워 달라고 함).
                card.SetLines(new List<CardLineVm>(), string.Empty);
                return;
            }

            string word = (r.Verdict ?? string.Empty).Trim();
            if (!A3HeadlineOnCard)
            {
                card.ClearVerdict();
                card.ClearScore();
                card.SetCount(string.Empty);
            }
            else if (word.Length > 0)
            {
                card.ClearScore();
                card.SetVerdict(word, A3Grade(r.Grade, word), r.Note ?? string.Empty, r.Count ?? string.Empty);
            }
            else
            {
                card.ClearVerdict();
                // 범위 밖 점수는 끝으로 붙입니다 — "120 / 100" 은 읽는 사람을 헷갈리게 합니다.
                if (double.IsNaN(r.Score) || double.IsInfinity(r.Score)) card.ClearScore();
                else card.SetScore(Math.Max(0.0, Math.Min(AnalysisCardVm.MaxScore, r.Score)));
                card.SetCount(r.Count ?? string.Empty);
            }

            var lines = new List<CardLineVm>();
            if (r.Lines != null)
            {
                for (int i = 0; i < r.Lines.Count; i++)
                {
                    Analysis3Line l = r.Lines[i];
                    if (l == null) continue;
                    var line = new CardLineVm(l.Label, l.Value, A3Grade(l.Grade, string.Empty), l.Detail, l.Target);
                    line.ValueIsChange = l.ValueIsChange;
                    line.Card = 3;
                    lines.Add(line);
                }
            }
            string empty = string.IsNullOrEmpty(r.EmptyText) ? "적을 것이 없습니다." : r.EmptyText;
            card.SetLines(lines, lines.Count == 0 ? empty : string.Empty);
        }

        /// <summary>
        /// 분석 3 의 색. 정해 왔으면 그대로, 아니면 낱말로 고릅니다 —
        /// PASS · OK 초록, WARNING · WARN 노랑, FAIL · NG 빨강, 그 밖은 회색.
        /// </summary>
        public static string A3Grade(Analysis3Grade grade, string word)
        {
            switch (grade)
            {
                case Analysis3Grade.Good: return ScoreBands.GradeGood;
                case Analysis3Grade.Warn: return ScoreBands.GradeWarn;
                case Analysis3Grade.Bad: return ScoreBands.GradeBad;
            }
            string w = (word ?? string.Empty).Trim().ToUpperInvariant();
            if (w == "PASS" || w == "OK") return ScoreBands.GradeGood;
            if (w == "WARNING" || w == "WARN") return ScoreBands.GradeWarn;
            if (w == "FAIL" || w == "NG") return ScoreBands.GradeBad;
            return ScoreBands.GradeNone;
        }

        /// <summary>판정 낱말에 맞는 색. 칸의 색 이름으로 옮겨 줍니다.</summary>
        /// <summary>
        /// 분석 2 칸의 <b>요약</b> — <b>차이 난 IO 를 한 줄씩</b>.
        ///
        /// 분석 1 처럼 여러 묶음으로 가르지 않습니다. 분석 2 에는 그룹이
        /// 없고, 보는 사람이 찾는 것은 "어느 IO 가 달라졌나" 하나입니다.
        /// 수는 낱말 아래 한 줄(<see cref="DbVerdict.Why"/>)이 이미 적습니다.
        ///
        /// <b>줄을 누르면 분석 2 화면에서 그 IO 를 찾아 줍니다.</b>
        ///
        /// 줄 오른쪽에 <b>이전 → 이후</b> 값을 적습니다 (1.04). 이름만 있으면
        /// "얼마나 달라졌나" 를 보러 결국 분석 2 로 가야 합니다. 열이 여럿
        /// 달라진 IO 는 줄 아래 작은 글에 열마다 적습니다.
        ///
        /// FAIL 이어도 이름을 적습니다 — 여기는 굴러가는 목록이라 "이 몇
        /// 개가 문제" 로 읽힐 자리가 아닙니다 (한 줄에 늘어놓는 쪽은
        /// DbVerdict 가 여덟 개로 끊습니다).
        /// </summary>
        /// <summary>한 IO 에서 더 달라진 열을 줄 아래에 몇 개까지 적나.</summary>
        private const int MaxOtherColumns = 3;

        private List<CardLineVm> DbLines(DbDiffList.Tally t)
        {
            var lines = new List<CardLineVm>();
            IList<DbChangedIo> ios = _state.DbChangedIos;
            if (ios == null) return lines;

            for (int i = 0; i < ios.Count; i++)
            {
                DbChangedIo io = ios[i];
                // 메인 칸은 좁아서 "(+1)" 같은 차이는 안 붙이고 값만 빨갛게
                // 칠합니다 (1.08). 차이는 분석 2 목록에 적힙니다.
                var line = new CardLineVm(io.Io, io.Change, ScoreBands.GradeBad,
                                          io.OthersText(MaxOtherColumns), io.Io);
                line.ValueIsChange = true;
                lines.Add(line);
            }

            // 다 못 적었으면 몇 개가 더 있는지 적습니다. 이 줄은 누를 것이
            // 없으므로 Target 을 비웁니다.
            int more = t.Changed - ios.Count;
            if (more > 0)
            {
                lines.Add(new CardLineVm("외 " + more.ToString("N0") + "개 더",
                                         string.Empty, ScoreBands.GradeNone,
                                         "분석 2 화면에서 다 볼 수 있습니다."));
            }
            return lines;
        }

        /// <summary>
        /// 차이 난 IO 가 없을 때 요약 칸에 적는 글. <b>빈 칸으로 두면
        /// "아직 안 봄" 과 가릴 수 없습니다.</b>
        /// </summary>
        private static string NoDbLines(DbDiffList.Tally t)
        {
            string s = "값이 달라진 IO 가 없습니다.";
            if (t.Gone > 0 || t.Added > 0)
            {
                s += "  없어진 IO " + t.Gone.ToString("N0") + "개 · 새로 생긴 IO "
                   + t.Added.ToString("N0") + "개는 분석 2 화면 [차이 발생] 에서 볼 수 있습니다.";
            }
            return s;
        }

        private static string GradeOf(string verdict)
        {
            if (verdict == DbVerdict.Pass) return ScoreBands.GradeGood;
            if (verdict == DbVerdict.Fail) return ScoreBands.GradeBad;
            return ScoreBands.GradeWarn;
        }

        private static string Count(int n)
        {
            return n.ToString("N0") + "개";
        }

        /// <summary>
        /// 그룹마다 "몇 개 중 몇 개가 달라졌나" 를 셉니다.
        ///
        /// 그룹은 설정에 담긴 것을 그대로 씁니다. 그룹에 안 담긴 IO 가 남으면
        /// <b>"묶지 않은 IO"</b> 라는 줄을 하나 더 붙입니다 — 그걸 빼 버리면
        /// 칸의 "달라진 IO 12개" 와 요약의 합이 안 맞아서, 어느 쪽이 틀린 건지
        /// 알 수가 없게 됩니다.
        /// </summary>
        private List<GroupSummaryVm> BuildGroups(CompareResult r)
        {
            var list = new List<GroupSummaryVm>();
            if (r == null) return list;

            // 달라진 IO 를 빨리 찾을 수 있게 이름을 모아 둡니다.
            var changed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < r.Items.Count; i++)
                if (r.Items[i].Changed) changed.Add(LogDataset.LooseKey(r.Items[i].Name));

            // 견준 IO 전체. 그룹에 안 담긴 것을 가려내는 데 씁니다.
            var rest = new HashSet<string>(StringComparer.Ordinal);
            var restNames = new List<string>();
            for (int i = 0; i < r.Items.Count; i++)
            {
                string key = LogDataset.LooseKey(r.Items[i].Name);
                if (rest.Add(key)) restNames.Add(r.Items[i].Name);
            }

            List<GroupDef> groups = _state.Settings.Groups;
            for (int g = 0; g < groups.Count; g++)
            {
                GroupDef def = groups[g];
                var members = new List<string>();
                int changedHere = 0;

                for (int m = 0; m < def.Members.Count; m++)
                {
                    string key = LogDataset.LooseKey(def.Members[m]);
                    if (!rest.Contains(key)) continue;   // 이 로그에 없는 IO
                    members.Add(def.Members[m]);
                    if (changed.Contains(key)) changedHere++;
                    rest.Remove(key);
                }

                list.Add(new GroupSummaryVm(def.Name, members, members.Count, changedHere));
            }

            // 어느 그룹에도 안 담긴 IO. 하나라도 남으면 줄을 붙입니다.
            if (rest.Count > 0)
            {
                var left = new List<string>();
                int changedLeft = 0;
                for (int i = 0; i < restNames.Count; i++)
                {
                    string key = LogDataset.LooseKey(restNames[i]);
                    if (!rest.Contains(key)) continue;
                    left.Add(restNames[i]);
                    if (changed.Contains(key)) changedLeft++;
                }
                list.Add(new GroupSummaryVm("묶지 않은 IO", left, left.Count, changedLeft));
            }

            return list;
        }

        // ---------------- 지금 열어 둔 로그 ----------------
        //
        // 여기 있던 것이 아래 알림 줄 한 줄뿐이었습니다. 메인 화면이라면
        // "지금 무엇을 보고 있는가" 가 제일 먼저 보여야 합니다.

        public bool HasLogs { get { return _state.HasAny; } }
        public bool HasBoth { get { return _state.HasBoth; } }

        /// <summary>
        /// 로그가 언제 것인지. <b>파일 이름 앞부분</b>을 그대로 씁니다.
        ///
        /// 로그 파일 안에는 "이 로그가 언제 기록된 것인가" 가 적혀 있지
        /// 않습니다. 시간 열은 0 부터 세는 경과 시간일 수도 있고, 시각이어도
        /// 날짜가 없을 수 있습니다. 대신 파일 이름 앞에 날짜를 붙이는 것이
        /// 보통이라 그걸 그대로 보여 줍니다 — 짐작해서 만들어 내지 않습니다.
        /// </summary>
        public string BeforeTime { get { return Occurred(_state.Before); } }
        public string AfterTime { get { return Occurred(_state.After); } }

        /// <summary>이름에서 앞부분을 떼어 내는 글자 수.</summary>
        private const int OccurredLength = 10;

        private static string Occurred(LogDataset ds)
        {
            if (ds == null) return string.Empty;

            string name;
            try { name = Path.GetFileNameWithoutExtension(ds.SourcePath); }
            catch (ArgumentException) { name = string.Empty; }
            if (string.IsNullOrEmpty(name)) return string.Empty;

            if (name.Length > OccurredLength) name = name.Substring(0, OccurredLength);

            // 자른 자리에 구분 기호가 걸리면 지저분합니다 ("2026-03-14_" 같은).
            return name.TrimEnd(' ', '_', '-', '.');
        }

        /// <summary>
        /// 열어 둔 파일의 이름. 발생시간은 이름 앞부분만 떼어 온 것이라,
        /// 같은 날 두 번 딴 로그를 가리려면 이름이 같이 보여야 합니다.
        /// </summary>
        public string BeforeFileName { get { return NameOf(_state.BeforePath); } }
        public string AfterFileName { get { return NameOf(_state.AfterPath); } }

        public string BeforeText { get { return Describe(_state.Before, "이전 로그를 열어 주세요"); } }
        public string AfterText { get { return Describe(_state.After, "이후 로그를 열어 주세요"); } }

        private static string Describe(LogDataset ds, string none)
        {
            if (ds == null) return none;
            return "IO " + ds.ChannelCount.ToString("N0")
                 + "  ·  표본 " + ds.SampleCount.ToString("N0");
        }

        /// <summary>
        /// 풍선 도움말에 띄울 전체 경로. 이름을 잘라 보여 주므로 필요합니다.
        /// [분석] 을 기다리는 파일이 있으면 그 경로입니다 (1.53) — 줄에 보이는 이름과 맞춥니다.
        /// </summary>
        public string BeforePath { get { return HasText(_waitBefore) ? _waitBefore : _state.BeforePath; } }
        public string AfterPath { get { return HasText(_waitAfter) ? _waitAfter : _state.AfterPath; } }

        // ---- [분석] 을 기다리는 자리 (1.53) ----
        //
        // 메인 칸에서 고르거나 놓은 것은 바로 읽지 않고 여기에 적어만 둡니다. 칸 머리의
        // [분석] 을 누르면 창(ShellWindow)이 이 자리를 읽고 견줍니다 — 분석 3 과 같은 흐름.
        // 1.52 까지는 놓자마자 읽고 견줘서, 큰 로그는 이전만 바꿔도 한참 기다려야 했습니다.
        //
        // 분석 1 · 2 화면 안의 [열기] · [읽기] · 끌어다 놓기는 지금처럼 바로 돕니다.
        // 그 화면은 결과를 보려고 들어간 곳이라 기다릴 까닭이 없습니다.

        private string _waitBefore = string.Empty;
        private string _waitAfter = string.Empty;
        private bool _dbWaitBefore, _dbWaitAfter;

        private static bool HasText(string s) { return !string.IsNullOrEmpty(s); }

        /// <summary>[분석] 을 기다리는 로그 경로. 없으면 빈 글자.</summary>
        public string WaitingLog(bool before) { return before ? _waitBefore : _waitAfter; }

        /// <summary>로그 자리를 적어 둡니다. 빈 글자면 지웁니다 (읽은 뒤).</summary>
        public void SetWaitingLog(bool before, string path)
        {
            if (before) _waitBefore = path ?? string.Empty;
            else _waitAfter = path ?? string.Empty;
            Refresh();
        }

        /// <summary>
        /// 그 쪽을 읽었으면 기다리던 자리를 지웁니다. 다시 그리지는 않습니다 — 읽은 뒤에
        /// 창이 어차피 다시 그립니다 (AfterLoad).
        /// </summary>
        public void ForgetWaitingLog(bool before)
        {
            if (before) _waitBefore = string.Empty;
            else _waitAfter = string.Empty;
        }

        /// <summary>그 쪽에 로그가 (읽었든 기다리든) 있는지.</summary>
        public bool HasLogFor(bool before)
        {
            return HasText(WaitingLog(before)) || (before ? _state.Before : _state.After) != null;
        }

        /// <summary>DB 자리를 메인 칸에서 바꿨는데 아직 안 읽었다고 적습니다.</summary>
        public void SetDbWaiting(bool before)
        {
            if (before) _dbWaitBefore = true;
            else _dbWaitAfter = true;
            Refresh();
        }

        /// <summary>분석 2 가 읽고 나면 지웁니다.</summary>
        public void ClearDbWaiting()
        {
            _dbWaitBefore = _dbWaitAfter = false;
        }

        // ---- 카드 아래 파일 줄 (1.02) ----
        //
        // 한 줄에 셋을 적습니다: 이전/이후 · 파일 이름 · 작은 덧글.
        // 아직 안 열었으면 이름 자리에 무엇을 하면 되는지를 흐리게 적습니다
        // (빈 줄로 두면 "열었는데 이름이 없다" 와 가릴 수 없습니다).

        /// <summary>안 열었을 때 이름 자리에 흐리게 적는 말. 열었으면 빈 글자.</summary>
        public string BeforeHint { get { return _state.Before == null && !HasText(_waitBefore) ? LogHint : string.Empty; } }
        public string AfterHint { get { return _state.After == null && !HasText(_waitAfter) ? LogHint : string.Empty; } }

        /// <summary>
        /// 연 뒤의 파일 이름. <b>안 열었으면 빈 글자</b> — 위 Hint 와 정확히
        /// 엇갈립니다 (하나가 차면 하나가 빕니다).
        ///
        /// 화면이 이 둘로 "열기 전 상자" 와 "연 뒤 한 줄" 중 하나를 고릅니다.
        /// BeforeFileName 으로 고르면 안 됩니다 — 그쪽은 경로(_state.BeforePath)
        /// 에서 나오고 Hint 는 데이터셋에서 나와서, 둘이 어긋나는 순간
        /// 두 모양이 겹치거나 둘 다 사라집니다.
        /// </summary>
        public string BeforeShown { get { return Shown(_waitBefore, _state.Before, BeforeFileName); } }
        public string AfterShown { get { return Shown(_waitAfter, _state.After, AfterFileName); } }

        private static string Shown(string waiting, LogDataset ds, string openName)
        {
            if (HasText(waiting)) return NameOf(waiting);
            return ds == null ? string.Empty : openName;
        }

        /// <summary>
        /// 오른쪽 작은 덧글 — IO 수와 표본 수. 안 열었으면 빈 글자.
        /// [분석] 을 기다리는 중이면 "분석 대기" (1.53).
        /// </summary>
        public string BeforeDetail { get { return HasText(_waitBefore) ? MainTexts.Waiting : Detail(_state.Before); } }
        public string AfterDetail { get { return HasText(_waitAfter) ? MainTexts.Waiting : Detail(_state.After); } }

        private const string LogHint = "누르거나 끌어다 놓아 여세요";

        private static string Detail(LogDataset ds)
        {
            if (ds == null) return string.Empty;
            return "IO " + ds.ChannelCount.ToString("N0") + " · 표본 " + ds.SampleCount.ToString("N0");
        }

        // ---------------- 분석 2 가 견줄 자리 (DB) ----------------
        //
        // 분석 1 의 로그 칸과 같은 모양으로 메인 화면에 둡니다. 여기 있는
        // 것은 <b>보여 주기</b>만 합니다 — 놓은 것을 받아 넣는 일은 분석 2
        // 화면(DbView)이 맡습니다. 그래야 자리를 적는 곳이 한 곳입니다.
        //
        // 자리 자체는 설정에 그대로 있습니다. 메인 화면용으로 따로 베껴
        // 두면, 분석 2 화면에서 고친 날 두 화면에 다른 자리가 적힙니다.

        public string DbBeforePath { get { return DbPath(true); } }
        public string DbAfterPath { get { return DbPath(false); } }

        public string DbBeforeName { get { return DbPathText.Name(DbPath(true)); } }
        public string DbAfterName { get { return DbPathText.Name(DbPath(false)); } }

        // 메인 화면 칸에는 단추도 타 넣는 자리도 없습니다 — 누르면 파일
        // 고르는 창이 뜹니다. 그래서 아직 안 정했을 때의 말이 분석 2
        // 화면과 다릅니다 (고르는 자리는 DbPathText 한 곳입니다).
        public string DbBeforeNote
        {
            get { return DbPathText.Note(DbPath(true), DbPathText.NoneOnCard); }
        }

        /// <summary>안 정했을 때 이름 자리에 흐리게 적는 말. 정했으면 빈 글자.</summary>
        public string DbBeforeHint { get { return DbPath(true).Length == 0 ? DbPathText.NoneOnCard : string.Empty; } }
        public string DbAfterHint { get { return DbPath(false).Length == 0 ? DbPathText.NoneOnCard : string.Empty; } }

        /// <summary>오른쪽 작은 덧글 — 폴더 · 파일 · 없는 자리. 안 정했으면 빈 글자.</summary>
        public string DbBeforeKind { get { return DbKind(true, _dbWaitBefore); } }
        public string DbAfterKind { get { return DbKind(false, _dbWaitAfter); } }

        private string DbKind(bool before, bool waiting)
        {
            string p = DbPath(before);
            if (p.Length == 0) return string.Empty;
            return waiting ? MainTexts.Waiting : DbPathText.Note(p);
        }

        public string DbAfterNote
        {
            get { return DbPathText.Note(DbPath(false), DbPathText.NoneOnCard); }
        }

        // ---- 분석 3 의 이전 · 이후 자리 (1.29) — 분석 2 칸과 같은 말 ----
        //
        // 자리는 Common/Analysis3Link 한 곳에 있습니다. 분석 3 화면도 거기서 읽으므로
        // 메인 칸과 분석 3 화면이 다른 자리를 보는 일이 없습니다.

        public string A3BeforePath { get { return Analysis3Link.BeforePath; } }
        public string A3AfterPath { get { return Analysis3Link.AfterPath; } }
        public string A3BeforeName { get { return DbPathText.Name(Analysis3Link.BeforePath); } }
        public string A3AfterName { get { return DbPathText.Name(Analysis3Link.AfterPath); } }
        // 분석 3 칸은 누르면 <b>폴더</b>를 고릅니다 (1.35) — 분석 2 칸의 말과 다릅니다.
        public string A3BeforeHint { get { return Analysis3Link.BeforePath.Length == 0 ? MainTexts.A3NoneOnCard : string.Empty; } }
        public string A3AfterHint { get { return Analysis3Link.AfterPath.Length == 0 ? MainTexts.A3NoneOnCard : string.Empty; } }
        public string A3BeforeKind { get { return A3Kind(Analysis3Link.BeforePath); } }
        public string A3AfterKind { get { return A3Kind(Analysis3Link.AfterPath); } }

        private static string A3Kind(string path)
        {
            return path.Length == 0 ? string.Empty : DbPathText.Note(path);
        }

        private string DbPath(bool before)
        {
            AppSettings s = _state.Settings;
            if (s == null) return string.Empty;
            return (before ? s.DbBeforePath : s.DbAfterPath) ?? string.Empty;
        }

        // ---------------- 종합 분석 결과 (LLM, 1.45) ----------------
        //
        // 1.44 까지 여기 있던 "달라진 데가 있습니다" 한 줄 · 기준 · 비교 시각 · 안내는 뺐습니다.
        // 그 자리는 사용자가 만든 LLM 분석의 결과가 차지합니다 (LlmPanelVm · Common/LlmLink).

        /// <summary>맨 위 종합 분석 결과 (LLM) 칸.</summary>
        public LlmPanelVm Llm { get; private set; }

        /// <summary>
        /// 분석 셋이 지금 결과가 있는지 체크 표시를 다시 적습니다. 무엇을 보고 "완료" 로 보는지:
        ///   분석 1 — 이전 · 이후 로그를 비교한 결과(<c>AppState.Comparison</c>)가 있음
        ///   분석 2 — 이전 · 이후 DB 를 읽어 견줌(<c>AppState.HasDbResult</c>)
        ///   분석 3 — 분석 3 이 결과를 올림(<c>Analysis3Link.Result</c>). Publish 를 안 부르면 알 수 없음.
        /// </summary>
        private void RefreshLlmChecks()
        {
            AppConfig names = AppConfig.Current;
            CompareResult r = _state.Comparison;
            Llm.SetReady(1, names.Title(1), r != null,
                r != null ? "로그 두 개를 비교함 · 달라진 IO " + r.ChangedCount.ToString("N0") + "개"
                : (_state.HasBoth ? "비교 결과가 없습니다. 분석 1 칸의 [분석] 을 눌러 주세요."
                                  : "이전 · 이후 로그를 넣고 분석 1 칸의 [분석] 을 누르면 비교됩니다."));
            Llm.SetReady(2, names.Title(2), _state.HasDbResult,
                _state.HasDbResult ? "DB 를 견줌 · " + DbVerdict.Of(_state.DbChanged) + " · 차이 발생 " + _state.DbChanged.ToString("N0") + "개"
                                   : "이전 · 이후 DB 를 넣고 분석 2 칸의 [분석] 을 누르면 읽고 견줍니다.");
            bool a3 = Analysis3Link.Result != null;
            Llm.SetReady(3, names.Title(3), a3,
                a3 ? "분석 3 이 결과를 보냄 (Analysis3Link.Publish)"
                   : "분석 3 이 결과를 보내야(Analysis3Link.Publish) 완료로 표시됩니다.");
        }

        /// <summary>
        /// LLM 서버에 닿는지에 따라 분석 3 칸 · 종합 분석 결과를 덮거나 엽니다 (1.51). 창이 LlmServer.Changed 를
        /// 받아 화면 스레드에서 부릅니다 (Refresh 안에서도 부름).
        /// </summary>
        public void RefreshLlmServer()
        {
            bool ok = LlmServer.IsOk;
            string text = LlmServer.BlockText, detail = LlmServer.BlockDetail;
            Llm.SetServer(ok, text, detail);
            if (Cards.Count >= 3) Cards[2].SetBlocked(!ok, text, detail);
        }

        /// <summary>[LLM 분석 실행] 을 누른 순간의 재료. 창이 배경 스레드로 넘깁니다.</summary>
        public LlmRequest BuildLlmRequest(DbDiffResult dbDiff)
        {
            // 1.54: 결과가 있고 <b>체크한</b> 분석만 참. 안 고른 분석 2 · 3 의 재료는 비워서 넘깁니다.
            // 분석 1 은 State 안에 있어 비울 수 없습니다 (설정도 State 에 있음) — 받는 쪽이 Use1 을 봐야 합니다.
            var q = new LlmRequest();
            q.Use1 = _state.Comparison != null && Llm.Checks[0].Selected;
            q.Use2 = _state.HasDbResult && Llm.Checks[1].Selected;
            q.Use3 = Analysis3Link.Result != null && Llm.Checks[2].Selected;
            for (int i = 0; i < 3; i++) q.Titles[i] = AppConfig.Current.Title(i + 1);
            q.State = _state;
            q.DbDiff = q.Use2 ? dbDiff : null;
            q.Analysis3 = q.Use3 ? Analysis3Link.Result : null;
            q.Analysis3Before = q.Use3 ? Analysis3Link.BeforePath : string.Empty;
            q.Analysis3After = q.Use3 ? Analysis3Link.AfterPath : string.Empty;
            return q;
        }

        /// <summary>요약에서 누른 그룹을 찾습니다. 분석 1 의 것만 있습니다.</summary>
        public GroupSummaryVm FindGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return null;
            for (int c = 0; c < Cards.Count; c++)
            {
                List<GroupSummaryVm> gs = Cards[c].Groups;
                for (int i = 0; i < gs.Count; i++)
                    if (gs[i].GroupName == groupName) return gs[i];
            }
            return null;
        }

        // ---------------- History (전 "저장된 분석") ----------------
        //
        // 저장 한 번에 칸 셋(분석 1 · 2 · 3)의 요약을 찍어 남깁니다 (1.42). 줄을 누르면
        // 그때의 요약을 칸에 펼쳐 보고, 다시 누르거나 [지금 결과로] 를 누르면 돌아옵니다.
        // 로그 값 자체는 담지 않습니다 — 칸에 보이는 요약만 (CardSnapshot).

        /// <summary>칸에 펼쳐 보고 있는 기록. 없으면 지금 결과.</summary>
        private AnalysisRecord _viewing;

        /// <summary>History 기록을 펼쳐 보는 중인지.</summary>
        public bool IsViewingHistory { get { return _viewing != null; } }

        /// <summary>펼쳐 보는 기록의 이름 (칸 위 띠에 적음).</summary>
        public string ViewingText
        {
            get { return _viewing == null ? string.Empty : "History — " + _viewing.DisplayName; }
        }

        /// <summary>
        /// History 줄을 눌렀을 때. 그 기록의 요약을 칸 셋에 펼칩니다. 이미 펼쳐 보던 줄을
        /// 다시 누르면 지금 결과로 돌아갑니다.
        /// </summary>
        public void ShowSaved(string id)
        {
            AnalysisRecord rec = SavedById(id);
            if (rec == null || rec == _viewing) { ShowLive(); return; }
            _viewing = rec;
            Refresh();
        }

        /// <summary>펼쳐 보던 것을 접고 지금 결과로.</summary>
        public void ShowLive()
        {
            if (_viewing == null) return;
            _viewing = null;
            Refresh();
        }

        private void ApplyViewing()
        {
            for (int i = 0; i < _history.Count; i++)
                _history[i].IsSelected = _viewing != null && _history[i].Record == _viewing;
            if (_viewing == null) return;

            // 줄의 칸이 쓰는 요약을 그대로 펼칩니다 — 1.41 까지의 기록은 칸이 점수만으로
            // 만든 요약이라, 기록을 직접 읽으면 칸 · 줄이 서로 다른 것을 보입니다.
            HistoryRowVm row = RowOf(_viewing);
            for (int i = 0; i < Cards.Count; i++)
            {
                CardSnapshot s = row != null && i < row.Cells.Count ? row.Cells[i].Snapshot : _viewing.CardAt(i + 1);
                if (s != null) Cards[i].ApplySnapshot(s);
            }
        }

        private HistoryRowVm RowOf(AnalysisRecord rec)
        {
            for (int i = 0; i < _history.Count; i++)
                if (_history[i].Record == rec) return _history[i];
            return null;
        }

        private List<HistoryRowVm> _history = new List<HistoryRowVm>();
        public List<HistoryRowVm> History { get { return _history; } }

        public bool HasHistory { get { return _history.Count > 0; } }

        /// <summary>
        /// 분석 하나라도 결과가 있어야 저장할 것이 있습니다 (1.42 — 전에는 분석 1 만 봤음).
        /// </summary>
        public bool CanSave
        {
            get { return _state.Comparison != null || _state.HasDbResult || Analysis3Link.Result != null; }
        }

        /// <summary>저장해 둔 것을 다시 읽습니다. 창을 열 때와 저장·삭제 뒤에 부릅니다.</summary>
        public void ReloadHistory()
        {
            var rows = new List<HistoryRowVm>();
            foreach (AnalysisRecord r in HistoryStore.Load(HistoryStore.ResolveFolder()))
                rows.Add(new HistoryRowVm(r));
            _history = rows;

            // 펼쳐 보던 기록이 다시 읽은 목록에 없으면(지워졌으면) 지금 결과로.
            if (_viewing != null)
            {
                AnalysisRecord again = null;
                for (int i = 0; i < rows.Count; i++) if (rows[i].Id == _viewing.Id) again = rows[i].Record;
                _viewing = again;
            }
            RefreshDeltas();
            Raise("History"); Raise("HasHistory");
        }

        /// <summary>줄마다 "지금 결과와 얼마나 다른지" 를 다시 셉니다.</summary>
        private void RefreshDeltas()
        {
            CompareResult r = _state.Comparison;
            bool live = r != null;
            double pct = _state.Settings.RelativeTolerancePercent;
            double abs = _state.Settings.AbsoluteTolerance;
            string align = _state.Settings.AlignIo;
            string rules = ToleranceSignature();

            for (int i = 0; i < _history.Count; i++)
            {
                HistoryRowVm row = _history[i];
                row.Against(live,
                            live && row.Record.SameBasis(pct, abs, align, rules),
                            live, AnalysisCardVm.MaxScore,
                            live ? r.ChangedCount : 0);
            }
        }

        /// <summary>
        /// 지금 결과를 한 건으로 남깁니다. 저장하지 못하면 이유를 돌려줍니다
        /// (성공이면 빈 글자).
        /// </summary>
        public string SaveCurrent(string label)
        {
            if (!CanSave) return "저장할 결과가 없습니다. 분석 1 · 2 · 3 중 하나라도 결과가 나온 뒤에 저장할 수 있습니다.";

            // 펼쳐 보던 기록이 칸에 있으면 그걸 다시 저장하게 됩니다 — 지금 결과로 돌리고 찍습니다.
            if (_viewing != null) { _viewing = null; Refresh(); }

            CompareResult r = _state.Comparison;
            var rec = new AnalysisRecord();
            rec.SavedAt = _state.ComparedAt != DateTime.MinValue ? _state.ComparedAt : DateTime.Now;
            rec.Label = label ?? string.Empty;

            rec.BeforeName = NameOf(_state.BeforePath);
            rec.AfterName = NameOf(_state.AfterPath);
            rec.BeforePath = _state.BeforePath ?? string.Empty;
            rec.AfterPath = _state.AfterPath ?? string.Empty;
            rec.BeforeTime = Occurred(_state.Before);
            rec.AfterTime = Occurred(_state.After);

            // 분석 셋의 점수를 그대로 남깁니다. 아직 없는 분석은 "없음" 으로
            // 둡니다 — 0 점으로 적으면 "다 틀렸다" 는 뜻이 됩니다.
            for (int i = 0; i < Cards.Count && i < AnalysisRecord.AnalysisCount; i++)
                rec.SetScore(i + 1, Cards[i].HasScore, Cards[i].Score);

            // 칸 셋의 요약을 그대로 (1.42). 무엇과 무엇을 견줬는지도 칸마다.
            for (int i = 0; i < Cards.Count && i < AnalysisRecord.AnalysisCount; i++)
            {
                CardSnapshot s = Cards[i].TakeSnapshot();
                if (i == 0) { s.BeforeName = rec.BeforeName; s.AfterName = rec.AfterName; }
                else if (i == 1) { s.BeforeName = DbBeforeName; s.AfterName = DbAfterName; }
                else { s.BeforeName = A3BeforeName; s.AfterName = A3AfterName; }
                rec.Cards.Add(s);
            }

            if (r != null)
            {
                rec.ComparedCount = r.CommonCount;
                rec.ChangedCount = r.ChangedCount;
                rec.OneSidedCount = r.OnlyBefore.Count + r.OnlyAfter.Count;
                rec.AppliedShift = r.AppliedShift;
            }

            rec.TolerancePercent = _state.Settings.RelativeTolerancePercent;
            rec.AbsoluteTolerance = _state.Settings.AbsoluteTolerance;
            rec.ToleranceRules = ToleranceSignature();
            rec.ToleranceRuleCount = Table().OverrideCount;
            rec.AlignIo = _state.Settings.AlignIo;

            string problem;
            HistoryStore.Save(HistoryStore.ResolveFolder(), rec, out problem);
            if (problem.Length > 0) return "저장하지 못했습니다.\n\n" + problem;

            ReloadHistory();
            return string.Empty;
        }

        /// <summary>
        /// 지금의 IO 별 허용 오차를 한 줄로. 저장할 때와 "같은 기준인지"
        /// 가릴 때가 같은 값을 써야 해서 한 곳에 둡니다.
        /// </summary>
        private string ToleranceSignature()
        {
            return Table().Signature();
        }

        private ToleranceTable Table()
        {
            return ToleranceTable.From(_state.Settings.RelativeTolerancePercent,
                                       _state.Settings.AbsoluteTolerance,
                                       _state.Settings.Tolerances);
        }

        /// <summary>목록에서 한 건을 찾습니다. 팝업을 띄울 때 씁니다.</summary>
        public AnalysisRecord SavedById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _history.Count; i++)
                if (_history[i].Id == id) return _history[i].Record;
            return null;
        }

        /// <summary>한 건을 지웁니다.</summary>
        public void DeleteSaved(string id)
        {
            if (!HistoryStore.Delete(HistoryStore.ResolveFolder(), id)) return;
            bool viewing = _viewing != null;
            ReloadHistory();
            // 펼쳐 보던 것을 지웠으면 칸을 지금 결과로, 다른 것을 지웠으면 고른 줄 표시를 다시.
            if (viewing) Refresh();
        }

        private static string NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try { return Path.GetFileName(path); }
            catch (ArgumentException) { return string.Empty; }
        }

        /// <summary>저장 폴더. 화면에서 "어디에 쌓이는지" 를 알려 줍니다.</summary>
        public string HistoryFolder { get { return HistoryStore.ResolveFolder(); } }
    }
}
