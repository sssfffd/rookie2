using System;
using System.Collections.Generic;
using LogScope.Logs.Align;
using LogScope.Logs.Compare;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Settings;
using LogScope.Db;

namespace LogScope.App.Common
{
    /// <summary>
    /// 화면들이 함께 보는 상태. 설정, 열어 둔 로그 두 개, 비교 결과입니다.
    /// 여기에는 WPF 타입이 하나도 없습니다 — 화면에 묶이지 않은 순수 상태입니다.
    /// </summary>
    public sealed class AppState
    {
        public AppSettings Settings = new AppSettings();

        public LogDataset Before;
        public LogDataset After;
        public CompareResult Comparison;

        public string BeforePath = string.Empty;
        public string AfterPath = string.Empty;

        // ---- 분석 2 (DB) 가 마지막으로 센 수 ----
        //
        // 메인 화면 칸이 PASS · WARNING · FAIL 을 적는 데 씁니다. 수만
        // 둡니다 — 판정하는 규칙은 LogScope.Db 의 DbVerdict 한 곳입니다.
        //
        // 아직 한 번도 안 읽었으면 HasDbResult 가 거짓입니다. 0 으로 두면
        // "차이 없음(PASS)" 과 "아직 안 봄" 을 가릴 수 없습니다.

        public bool HasDbResult;
        public int DbChanged;
        public int DbGone;
        public int DbAdded;

        /// <summary>
        /// 값이 달라진 IO 와 그 값 (앞 몇 개). 메인 화면 칸의 요약이
        /// "TEMP_MAX   1 → 2" 로 한 줄씩 적습니다 — 개수만 보면 결국
        /// 분석 2 를 열어 목록을 뒤져야 합니다.
        /// </summary>
        public List<DbChangedIo> DbChangedIos = new List<DbChangedIo>();

        public bool HasAny { get { return Before != null || After != null; } }
        public bool HasBoth { get { return Before != null && After != null; } }

        /// <summary>
        /// 시간 맞추기 결과를 알립니다. 화면에 그대로 보여 주는 글입니다.
        /// 맞추기를 안 쓰면 빈 글자입니다.
        /// </summary>
        public string AlignNote = string.Empty;

        /// <summary>
        /// 사건으로 맞춰서 나온 밀기 값. 맞추기를 안 쓰거나 못 맞췄으면 NaN.
        ///
        /// <b>설정에 적지 않고 여기에만 둡니다.</b> 예전에는 이 값을
        /// Settings.ManualShift 에 적고 AutoAlign 을 꺼 버렸는데, 그러면
        ///   - 맞추기를 해제해도 AutoAlign 이 꺼진 채라 시작 시각 자동
        ///     맞춤이 돌아오지 않았고,
        ///   - 설정 창에서 손으로 넣어 둔 밀기 값이 덮여 사라졌습니다.
        /// 지금은 설정을 건드리지 않고 견줄 때 합칩니다 (BuildDiffOptions).
        /// </summary>
        public double TriggerShift = double.NaN;

        /// <summary>지금 사건으로 맞춰져 있는지.</summary>
        public bool UsesTriggerAlign
        {
            get { return Settings.AlignIo.Length > 0 && !double.IsNaN(TriggerShift); }
        }

        /// <summary>
        /// 설정에 적힌 IO 로 두 로그의 시간축을 맞춥니다.
        ///
        /// 기준 IO 를 안 골랐으면 맞추기를 풀고 <b>시작 시각 자동 맞춤으로
        /// 돌아갑니다</b> — 설정에 적힌 AutoAlign 을 그대로 따릅니다.
        ///
        /// 못 맞추면 이유만 돌려주고 밀기 값은 건드리지 않습니다.
        /// </summary>
        public AlignResult ApplyTriggerAlign()
        {
            if (Settings.AlignIo.Length == 0)
            {
                TriggerShift = double.NaN;
                AlignNote = string.Empty;
                return new AlignResult { Ok = true, Message = string.Empty };
            }

            AlignResult r = TriggerAlign.Compute(Before, After, Settings.AlignIo,
                                                 (EdgeKind)Settings.AlignEdge,
                                                 Settings.AlignOccurrence,
                                                 Settings.AlignLevel);
            TriggerShift = r.Ok ? r.Shift : double.NaN;
            AlignNote = r.Message;
            return r;
        }

        /// <summary>맞추기를 풀고 시작 시각 맞추기로 되돌립니다.</summary>
        public void ClearTriggerAlign()
        {
            Settings.AlignIo = string.Empty;
            TriggerShift = double.NaN;
            AlignNote = string.Empty;
        }

        /// <summary>
        /// 지금 쓰는 기준 IO 에서 고를 수 있는 값들. 양쪽 로그 중 값 종류가
        /// 많은 쪽을 보여 줍니다 — 한쪽에만 나오는 값으로도 맞출 수 있어야
        /// 하기 때문입니다 (반대쪽은 처음부터 그 값일 수 있습니다).
        /// </summary>
        public LevelSet AlignLevels()
        {
            string name = Settings.AlignIo;
            if (name.Length == 0) return new LevelSet();

            LevelSet b = Before != null ? TriggerAlign.LevelsOf(Before, Before.FindChannel(name)) : new LevelSet();
            LevelSet a = After != null ? TriggerAlign.LevelsOf(After, After.FindChannel(name)) : new LevelSet();
            if (b.Thresholded || a.Thresholded) return b.Thresholded ? b : a;
            return a.Values.Length > b.Values.Length ? a : b;
        }

        /// <summary>
        /// 열어 둔 로그의 IO 이름 (이전 + 이후, 겹치는 것은 한 번만).
        /// 설정 창에서 IO 별 허용 오차를 걸 때 고르라고 내어 줍니다.
        ///
        /// 로그를 안 열었으면 빈 목록입니다 — 그때는 이름을 손으로 적으면
        /// 됩니다. 규칙을 미리 걸어 두는 일이 있습니다.
        /// </summary>
        public List<string> IoNames()
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int pass = 0; pass < 2; pass++)
            {
                LogDataset ds = pass == 0 ? Before : After;
                if (ds == null) continue;
                for (int i = 0; i < ds.ChannelCount; i++)
                {
                    string name = ds.Channels[i].Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!seen.Add(LogDataset.LooseKey(name))) continue;
                    list.Add(name);
                }
            }
            list.Sort(StringComparer.CurrentCultureIgnoreCase);
            return list;
        }

        /// <summary>
        /// 시간 맞추기 기준으로 쓸 수 있는 IO 이름들.
        ///
        /// 양쪽 로그에 다 있고, <b>한쪽에서라도</b> 값이 바뀌면 후보입니다.
        /// 반대쪽이 처음부터 그 값이면 거기서는 시작 지점이 그 순간이라,
        /// 그래도 맞춰집니다 — 기록을 늦게 건 로그에서 흔한 모양입니다.
        /// </summary>
        public List<string> AlignCandidates()
        {
            var list = new List<string>();
            if (Before == null || After == null) return list;

            for (int i = 0; i < Before.ChannelCount; i++)
            {
                string name = Before.Channels[i].Name;
                int j = After.FindChannel(name);
                if (j < 0) continue;
                if (!TriggerAlign.CanTrigger(Before, i) && !TriggerAlign.CanTrigger(After, j)) continue;
                list.Add(name);
            }
            return list;
        }

        public DiffOptions BuildDiffOptions()
        {
            var o = new DiffOptions();
            o.AbsoluteTolerance = Settings.AbsoluteTolerance;
            o.RelativePercent = Settings.RelativeTolerancePercent;
            o.Tolerances = ToleranceTable.From(Settings.RelativeTolerancePercent,
                                               Settings.AbsoluteTolerance,
                                               Settings.Tolerances);
            // 밀기 값은 여기서 합칩니다. 사건으로 맞춰져 있으면 그 값이
            // 바탕이 되고 시작 시각 자동 맞춤은 비켜섭니다 — 둘 다 걸면
            // 서로를 밀어 엉뚱한 자리로 갑니다. 설정 창에서 손으로 넣은
            // ManualShift 는 어느 쪽이든 그 위에 더해지는 미세 조정입니다.
            // 단계를 골랐으면 그 단계의 시작으로 맞춥니다. <b>사건 맞추기와
            // 시작 시각 맞추기보다 먼저입니다</b> — 단계를 고른 사람은 그
            // 단계를 겹쳐 보려고 고른 것인데, 다른 맞추기가 그 위에 또
            // 걸리면 단계 시작이 어긋납니다.
            //
            // 손으로 넣은 ManualShift 는 어느 쪽이든 그 위에 더해지는 미세
            // 조정이라 그대로 둡니다.
            StepPair step = PickedStep;
            bool bystep = step != null && !double.IsNaN(step.Shift);
            bool trigger = !bystep && UsesTriggerAlign;

            o.Shift = Settings.ManualShift
                    + (bystep ? step.Shift : (trigger ? TriggerShift : 0.0));
            o.AutoAlign = !bystep && !trigger && Settings.AutoAlign;
            o.SortBy = Settings.SortMetric;
            return o;
        }

        /// <summary>
        /// 마지막으로 견준 시각. 아직 안 견줬으면 <see cref="DateTime.MinValue"/>.
        ///
        /// 화면에 적어 주는 값입니다. 허용 오차나 시간 맞추기를 바꾸고 다시
        /// 견주는 일이 잦아서, 지금 보고 있는 결과가 <b>언제 나온 것인지</b>
        /// 를 알 수 없으면 옛 결과를 새것으로 착각하게 됩니다.
        /// </summary>
        public DateTime ComparedAt = DateTime.MinValue;

        public void Recompare(LoadProgress prog)
        {
            Comparison = (Before != null && After != null)
                ? DiffEngine.Compare(Before, After, BuildDiffOptions(), prog)
                : null;
            ComparedAt = Comparison != null ? DateTime.Now : DateTime.MinValue;
        }

        /// <summary>값이 달라졌거나 한쪽에만 있는 IO 이름들.</summary>
        public HashSet<string> ChangedNames()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (Comparison == null) return set;
            foreach (ChannelDiff d in Comparison.Items) if (d.Changed) set.Add(d.Name);
            foreach (ChannelDiff d in Comparison.OnlyBefore) set.Add(d.Name);
            foreach (ChannelDiff d in Comparison.OnlyAfter) set.Add(d.Name);
            return set;
        }

        /// <summary>그래프가 쓸 전체 시간 구간. 두 로그를 모두 덮습니다.</summary>
        public void FullTimeRange(out double t0, out double t1)
        {
            double shift = AppliedShift;
            bool any = false;
            t0 = 0; t1 = 1;
            if (Before != null && Before.SampleCount > 0)
            {
                t0 = Before.TimeStart; t1 = Before.TimeEnd; any = true;
            }
            if (After != null && After.SampleCount > 0)
            {
                double a0 = After.TimeStart + shift, a1 = After.TimeEnd + shift;
                if (!any) { t0 = a0; t1 = a1; any = true; }
                else { if (a0 < t0) t0 = a0; if (a1 > t1) t1 = a1; }
            }
            if (!any || !(t1 > t0)) { t0 = 0; t1 = 1; }
        }

        // ---- 단계 (분석 1) -------------------------------------------------
        //
        // 단계 목록은 로그를 읽을 때 만들어지고(StepRead), 두 로그를
        // 짝짓는 일은 여기서 한 번만 합니다. <b>화면마다 따로 지으면
        // 단계 번호가 어긋날 수 있습니다</b> — 히트맵의 "2. 가열" 과
        // 그래프의 "2. 가열" 이 다른 단계를 가리키면 아무도 못 찾습니다.
        //
        // 짝 목록에는 단계가 시작하는 <b>시각</b>이 들어 있습니다. 가로축을
        // 갈아 끼우면 그 시각이 달라지므로, 그때 반드시 버려야 합니다
        // (ForgetSteps). 안 버리면 옛 축의 시각으로 그래프가 움직입니다.

        private StepPairList _stepPairs;

        /// <summary>두 로그의 단계를 짝지은 목록. 없으면 빈 목록입니다.</summary>
        public StepPairList StepPairs()
        {
            if (_stepPairs == null) _stepPairs = StepPairList.Build(Before, After);
            return _stepPairs;
        }

        /// <summary>로그나 가로축이 바뀌었을 때. 다음에 물으면 다시 짓습니다.</summary>
        public void ForgetSteps() { _stepPairs = null; }

        /// <summary>
        /// 설정의 <b>단계 IO</b> 로 단계를 다시 찾습니다. <b>로그를 다시
        /// 읽지 않습니다</b> — 이미 메모리에 있는 채널을 한 번 훑으면 됩니다.
        ///
        /// 0.98 까지는 로그를 읽는 자리에서만 단계를 찾았습니다. 그래서
        /// 설정에 IO 이름을 적어 놓고도 파일을 다시 열어야 들어갔습니다 —
        /// 이름을 한 번에 맞게 적는 일이 드물어서(대소문자, 밑줄, 공백)
        /// 그때마다 10M 칸을 다시 읽는 꼴이 됐습니다.
        ///
        /// 돌려주는 글은 <b>못 찾았으면 그 이유</b>입니다. 빈 글자면 괜찮은
        /// 것입니다 (단계 IO 를 안 적어 둔 경우도 빈 글자입니다).
        /// </summary>
        public string ReapplySteps()
        {
            var opt = new OpenOptions();
            opt.StepIo = Settings.StepIo ?? string.Empty;

            StepRead.Fill(Before, opt);
            StepRead.Fill(After, opt);

            // 짝 목록과 고른 단계를 버립니다 — 단계가 통째로 달라졌습니다.
            ForgetSteps();
            StepPick = -1;

            string b = Before != null ? Before.StepNote : string.Empty;
            string a = After != null ? After.StepNote : string.Empty;

            if (b.Length == 0) return a;
            if (a.Length == 0 || string.Equals(a, b, StringComparison.Ordinal)) return b;
            return "이전 로그: " + b + "   이후 로그: " + a;
        }

        /// <summary>고를 수 있는 단계가 있는지 (양쪽에 다 있는 단계).</summary>
        public bool HasSteps { get { return StepPairs().UsableCount > 0; } }

        /// <summary>지금 고른 단계. -1 이면 전체를 봅니다.</summary>
        public int StepPick = -1;

        /// <summary>고른 단계의 짝. 안 골랐거나 번호가 어긋나면 null.</summary>
        public StepPair PickedStep
        {
            get
            {
                StepPairList l = StepPairs();
                if (StepPick < 0 || StepPick >= l.Count) return null;
                StepPair p = l[StepPick];
                return p.HasBoth ? p : null;
            }
        }

        /// <summary>시간축 눈금 글자를 어느 로그 기준으로 쓸지.</summary>
        public LogDataset TimeReference { get { return Before ?? After; } }

        public double AppliedShift { get { return Comparison != null ? Comparison.AppliedShift : 0.0; } }

        // ---- 그룹 손보기 ---------------------------------------------------

        public void AddGroup(string name)
        {
            Settings.Groups.Add(new GroupDef(string.IsNullOrEmpty(name) ? "새 그룹" : name));
        }

        public void RemoveGroup(int index)
        {
            if (index < 0 || index >= Settings.Groups.Count) return;
            Settings.Groups.RemoveAt(index);
        }

        public void MoveGroup(int index, int delta)
        {
            int to = index + delta;
            if (index < 0 || index >= Settings.Groups.Count) return;
            if (to < 0 || to >= Settings.Groups.Count) return;
            GroupDef g = Settings.Groups[index];
            Settings.Groups.RemoveAt(index);
            Settings.Groups.Insert(to, g);
        }

        /// <summary>IO 를 그룹에 넣습니다. 다른 그룹에 있었다면 거기서 뺍니다.</summary>
        public void PutInGroup(IEnumerable<string> names, int group)
        {
            if (group < 0 || group >= Settings.Groups.Count) return;
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                for (int g = 0; g < Settings.Groups.Count; g++) Settings.Groups[g].Members.Remove(n);
                Settings.Groups[group].Members.Add(n);
            }
        }

        public void RemoveFromGroups(IEnumerable<string> names)
        {
            foreach (string n in names)
                for (int g = 0; g < Settings.Groups.Count; g++) Settings.Groups[g].Members.Remove(n);
        }
    }
}
