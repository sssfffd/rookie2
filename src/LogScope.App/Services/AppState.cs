using System;
using System.Collections.Generic;
using LogScope.Core.Align;
using LogScope.Core.Compare;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.Services
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
        /// 설정에 적힌 IO 를 가로축으로 끼웁니다. 두 로그 모두에 겁니다.
        /// 한쪽이라도 못 쓰면 <b>양쪽 다 원래 시간축으로 되돌립니다</b> —
        /// 한쪽만 다른 축으로 그리면 두 로그가 아예 다른 이야기가 됩니다.
        /// </summary>
        public string ApplyAxisChannel()
        {
            string name = Settings.AxisIo;
            if (string.IsNullOrEmpty(name))
            {
                if (Before != null) Before.ClearAxisChannel();
                if (After != null) After.ClearAxisChannel();
                return string.Empty;
            }

            string problem;
            if (Before != null && !Before.SetAxisChannel(name, out problem))
            {
                ResetAxis();
                return "이전 로그: " + problem;
            }
            if (After != null && !After.SetAxisChannel(name, out problem))
            {
                ResetAxis();
                return "이후 로그: " + problem;
            }
            return string.Empty;
        }

        private void ResetAxis()
        {
            Settings.AxisIo = string.Empty;
            if (Before != null) Before.ClearAxisChannel();
            if (After != null) After.ClearAxisChannel();
        }

        /// <summary>가로축을 그 IO 로 놓았을 때 알아 둘 점. 없으면 빈 글자.</summary>
        public string AxisNote
        {
            get
            {
                if (Before != null && Before.AxisNote.Length > 0) return Before.AxisNote;
                if (After != null && After.AxisNote.Length > 0) return After.AxisNote;
                return string.Empty;
            }
        }

        /// <summary>
        /// 가로축으로 쓸 수 있는 IO 이름들. 양쪽 로그에 다 있고 양쪽 모두
        /// 오름차순이어야 합니다.
        /// </summary>
        public List<string> AxisCandidates()
        {
            var list = new List<string>();
            LogDataset baseDs = Before ?? After;
            if (baseDs == null) return list;

            for (int i = 0; i < baseDs.ChannelCount; i++)
            {
                if (!baseDs.CanBeAxis(i)) continue;
                string name = baseDs.Channels[i].Name;
                LogDataset other = ReferenceEquals(baseDs, Before) ? After : Before;
                if (other != null)
                {
                    int j = other.FindChannel(name);
                    if (j < 0 || !other.CanBeAxis(j)) continue;
                }
                list.Add(name);
            }
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
            // 밀기 값은 여기서 합칩니다. 사건으로 맞춰져 있으면 그 값이
            // 바탕이 되고 시작 시각 자동 맞춤은 비켜섭니다 — 둘 다 걸면
            // 서로를 밀어 엉뚱한 자리로 갑니다. 설정 창에서 손으로 넣은
            // ManualShift 는 어느 쪽이든 그 위에 더해지는 미세 조정입니다.
            bool trigger = UsesTriggerAlign;
            o.Shift = Settings.ManualShift + (trigger ? TriggerShift : 0.0);
            o.AutoAlign = !trigger && Settings.AutoAlign;
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
