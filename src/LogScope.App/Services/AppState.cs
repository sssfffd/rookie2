using System;
using System.Collections.Generic;
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

        public DiffOptions BuildDiffOptions()
        {
            var o = new DiffOptions();
            o.Tolerance = Settings.Tolerance;
            o.Shift = Settings.ManualShift;
            o.AutoAlign = Settings.AutoAlign;
            o.SortBy = Settings.SortMetric;
            return o;
        }

        public void Recompare(LoadProgress prog)
        {
            Comparison = (Before != null && After != null)
                ? DiffEngine.Compare(Before, After, BuildDiffOptions(), prog)
                : null;
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
