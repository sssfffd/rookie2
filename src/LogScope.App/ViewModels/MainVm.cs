using System;
using System.Collections.Generic;
using System.IO;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Compare;
using LogScope.Core.History;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
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
                new AnalysisCardVm(2, names.Title(2), names.Summary(2), false),
                new AnalysisCardVm(3, names.Title(3), names.Summary(3), false),
            };
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
                Cards[0].SetStats(new List<StatVm>());
            }
            else
            {
                Cards[0].SetScore(AnalysisCardVm.MaxScore);   // ← 진짜 점수가 들어갈 자리

                // IO 개수는 칸에서 뺐습니다. 세 군데에 같은 수가 적혀 있었습니다
                // (칸 · 종합 · 대시보드). 칸은 "요약" 을 보는 자리로 비워 두고,
                // 수는 위의 종합 칸 한 곳에서 셉니다.
                //
                // 채우는 길(SetStats)은 남겨 둡니다 — 분석 2(DB)가 "표 12개 중
                // 3 개 달라짐" 을 적을 자리입니다. 비어 있으면 칸 자체가
                // 사라지므로 빈 자리가 보이지는 않습니다.
                Cards[0].SetStats(new List<StatVm>());
            }

            Cards[0].SetGroups(BuildGroups(r));

            // 분석 2·3 은 아직 없습니다. 점수를 비워 둬야 "안 만들었다" 가
            // 그대로 읽힙니다.
            for (int i = 1; i < Cards.Count; i++)
            {
                Cards[i].ClearScore();
                Cards[i].SetGroups(new List<GroupSummaryVm>());
            }

            RefreshDeltas();

            Raise("HasLogs"); Raise("HasBoth"); Raise("Hint"); Raise("CanSave");
            Raise("BeforeTime"); Raise("AfterTime");
            Raise("BeforeFileName"); Raise("AfterFileName");
            Raise("BeforeText"); Raise("AfterText");
            Raise("BeforePath"); Raise("AfterPath");
            Raise("ComparedText");
            Raise("OverallHeadline"); Raise("OverallGrade");
            Raise("OverallBasis");
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

        /// <summary>풍선 도움말에 띄울 전체 경로. 이름을 잘라 보여 주므로 필요합니다.</summary>
        public string BeforePath { get { return _state.BeforePath; } }
        public string AfterPath { get { return _state.AfterPath; } }

        /// <summary>
        /// 지금 보고 있는 결과가 언제 나온 것인지.
        ///
        /// 허용 오차나 시간 맞추기를 바꾸고 다시 견주는 일이 잦습니다.
        /// 이걸 안 적어 두면 옛 결과를 새것으로 착각하게 됩니다.
        /// </summary>
        public string ComparedText
        {
            get
            {
                if (_state.Comparison == null) return string.Empty;
                if (_state.ComparedAt == DateTime.MinValue) return string.Empty;
                return "비교 실행  " + _state.ComparedAt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        // ---------------- 종합 분석 결과 ----------------
        //
        // 메인 화면 맨 위, 전에 "열어 둔 로그" 가 있던 자리입니다. 로그 칸은
        // 분석 1 안으로 들어갔습니다 — 로그를 여는 일은 분석 1 의 준비이고,
        // 메인 화면이 먼저 답해야 하는 것은 "그래서 결과가 어떤가" 입니다.

        /// <summary>한 줄 판정. 메인 화면에서 제일 먼저 읽는 글입니다.</summary>
        public string OverallHeadline
        {
            get
            {
                if (!_state.HasBoth) return "아직 비교하지 않았습니다";
                CompareResult r = _state.Comparison;
                if (r == null) return "아직 비교하지 않았습니다";
                if (r.ChangedCount > 0) return "달라진 데가 있습니다";
                if (r.OnlyBefore.Count + r.OnlyAfter.Count > 0) return "값은 같고, 한쪽에만 있는 IO 가 있습니다";
                return "다른 데가 없습니다";
            }
        }

        /// <summary>
        /// 판정 옆의 점 색. 점수 색과 <b>같은 뜻</b>으로 씁니다.
        /// 안 견준 상태는 빈 글자 — 회색으로 둡니다. 안 재 본 것에 색을 칠하면
        /// 재 본 것으로 읽힙니다.
        /// </summary>
        public string OverallGrade
        {
            get
            {
                CompareResult r = _state.Comparison;
                if (!_state.HasBoth || r == null) return string.Empty;
                if (r.ChangedCount > 0) return "bad";
                if (r.OnlyBefore.Count + r.OnlyAfter.Count > 0) return "warn";
                return "good";
            }
        }

        /// <summary>
        /// 어느 기준으로 본 결과인지. 허용 오차를 바꾸고 다시 견주는 일이
        /// 잦아서, 숫자만 있고 기준이 없으면 무엇과 견준 수인지 알 수 없습니다.
        /// 저장된 분석에 적는 글과 <b>같은 틀</b>입니다.
        /// </summary>
        public string OverallBasis
        {
            get
            {
                if (_state.Comparison == null) return string.Empty;
                AppSettings st = _state.Settings;
                var rec = new AnalysisRecord();
                rec.TolerancePercent = st.RelativeTolerancePercent;
                rec.AbsoluteTolerance = st.AbsoluteTolerance;
                rec.ToleranceRuleCount = st.Tolerances != null ? st.Tolerances.Count : 0;
                rec.AlignIo = st.AlignIo ?? string.Empty;
                rec.AxisIo = st.AxisIo ?? string.Empty;
                return rec.BasisText;
            }
        }

        /// <summary>
        /// 화면 오른쪽 아래의 색 설명. 점수를 어느 색으로 칠하는지와 그 색이
        /// 무슨 뜻인지 알려 줍니다. 칸의 점수 색과 <b>같은 값</b>을 봅니다.
        /// </summary>
        public List<BandVm> Bands { get { return ScoreBands.Legend(); } }

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

        // ---------------- 저장된 분석 ----------------
        //
        // 지금 결과를 남겨 두고, 다음에 분석했을 때 <b>지난번과 얼마나 달라졌는지</b>
        // 를 봅니다. 로그 값 자체는 담지 않습니다 — 요약된 몇 개의 수만 남깁니다.
        // 왜 그렇게 했는지는 AnalysisRecord 에 적어 뒀습니다.

        private List<HistoryRowVm> _history = new List<HistoryRowVm>();
        public List<HistoryRowVm> History { get { return _history; } }

        public bool HasHistory { get { return _history.Count > 0; } }

        /// <summary>견준 결과가 있어야 저장할 것이 있습니다.</summary>
        public bool CanSave { get { return _state.Comparison != null; } }

        /// <summary>저장해 둔 것을 다시 읽습니다. 창을 열 때와 저장·삭제 뒤에 부릅니다.</summary>
        public void ReloadHistory()
        {
            var rows = new List<HistoryRowVm>();
            foreach (AnalysisRecord r in HistoryStore.Load(HistoryStore.ResolveFolder()))
                rows.Add(new HistoryRowVm(r));
            _history = rows;
            RefreshDeltas();
            Raise("History"); Raise("HasHistory"); Raise("HistoryNote");
        }

        /// <summary>줄마다 "지금 결과와 얼마나 다른지" 를 다시 셉니다.</summary>
        private void RefreshDeltas()
        {
            CompareResult r = _state.Comparison;
            bool live = r != null;
            double pct = _state.Settings.RelativeTolerancePercent;
            double abs = _state.Settings.AbsoluteTolerance;
            string align = _state.Settings.AlignIo;
            string axis = _state.Settings.AxisIo;
            string rules = ToleranceSignature();

            for (int i = 0; i < _history.Count; i++)
            {
                HistoryRowVm row = _history[i];
                row.Against(live,
                            live && row.Record.SameBasis(pct, abs, align, axis, rules),
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
            CompareResult r = _state.Comparison;
            if (r == null) return "비교 결과가 없습니다. Log 파일 두 개를 열고 비교한 후에 저장할 수 있습니다.";

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

            rec.ComparedCount = r.CommonCount;
            rec.ChangedCount = r.ChangedCount;
            rec.OneSidedCount = r.OnlyBefore.Count + r.OnlyAfter.Count;

            rec.TolerancePercent = _state.Settings.RelativeTolerancePercent;
            rec.AbsoluteTolerance = _state.Settings.AbsoluteTolerance;
            rec.ToleranceRules = ToleranceSignature();
            rec.ToleranceRuleCount = Table().OverrideCount;
            rec.AlignIo = _state.Settings.AlignIo;
            rec.AxisIo = _state.Settings.AxisIo;
            rec.AppliedShift = r.AppliedShift;

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
            if (HistoryStore.Delete(HistoryStore.ResolveFolder(), id)) ReloadHistory();
        }

        private static string NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try { return Path.GetFileName(path); }
            catch (ArgumentException) { return string.Empty; }
        }

        /// <summary>목록 위에 적는 안내.</summary>
        public string HistoryNote
        {
            get
            {
                if (_history.Count == 0)
                    return "아직 저장한 것이 없습니다. Log 파일을 비교한 후 [지금 결과 저장] 을 누르면 여기에 쌓입니다.";
                if (_state.Comparison == null)
                    return "Log 파일을 비교한 후에는 각 줄에 지금 결과와의 차이가 같이 뜹니다.";
                return "각 줄의 오른쪽이 지금 결과와의 차이입니다. 달라진 IO 가 늘면 빨강, 줄면 초록입니다.";
            }
        }

        /// <summary>저장 폴더. 화면에서 "어디에 쌓이는지" 를 알려 줍니다.</summary>
        public string HistoryFolder { get { return HistoryStore.ResolveFolder(); } }

        /// <summary>칸 위에 적는 안내.</summary>
        public string Hint
        {
            get
            {
                if (!_state.HasAny)
                    return "위쪽의 [이전 로그 열기] / [이후 로그 열기] 로 파일을 먼저 열어 주세요. 창에 끌어다 놓아도 됩니다.";
                if (!_state.HasBoth)
                    return "Log 파일이 하나뿐입니다. 두 개를 다 열어야 비교할 수 있습니다.";
                return "칸을 누르면 그 분석 화면으로 넘어갑니다.";
            }
        }
    }
}
