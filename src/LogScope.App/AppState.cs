using System;
using System.Collections.Generic;
using LogScope.Core.Compare;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App
{
    /// <summary>
    /// 창들이 함께 보는 상태. 설정, 열어 둔 로그 두 개, 비교 결과, 그리고
    /// 왼쪽 목록 모델을 담습니다. 화면은 여기서 값을 읽고 이벤트로 갱신됩니다.
    /// </summary>
    public sealed class AppState
    {
        public AppSettings Settings = new AppSettings();
        public LogDataset Before;
        public LogDataset After;
        public CompareResult Comparison;
        public ChannelView View = new ChannelView();

        public event EventHandler DataChanged;
        public event EventHandler SelectionChanged;

        public string BeforePath = string.Empty;
        public string AfterPath = string.Empty;

        public void RaiseData() { var h = DataChanged; if (h != null) h(this, EventArgs.Empty); }
        public void RaiseSelection() { var h = SelectionChanged; if (h != null) h(this, EventArgs.Empty); }

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

        /// <summary>
        /// 로그가 바뀐 뒤 목록과 그룹을 다시 맞춥니다. 저장된 그룹이 없으면
        /// 10 개씩 묶은 기본 그룹을 만들어 둡니다.
        /// </summary>
        public void RebuildView()
        {
            View.Rebuild(Before, After, Settings.Groups);
            if (Settings.Groups.Count == 0 && View.Count > 0)
            {
                Settings.Groups = View.MakeDefaultGroups(10);
                View.Rebuild(Before, After, Settings.Groups);
            }
            SyncChangedNames();
            View.RebuildRows();
        }

        public void SyncChangedNames()
        {
            View.ChangedNames.Clear();
            if (Comparison == null) return;
            foreach (ChannelDiff d in Comparison.Items) if (d.Changed) View.ChangedNames.Add(d.Name);
            foreach (ChannelDiff d in Comparison.OnlyBefore) View.ChangedNames.Add(d.Name);
            foreach (ChannelDiff d in Comparison.OnlyAfter) View.ChangedNames.Add(d.Name);
        }

        public void Recompare(LoadProgress prog)
        {
            Comparison = (Before != null && After != null)
                ? DiffEngine.Compare(Before, After, BuildDiffOptions(), prog)
                : null;
            SyncChangedNames();
        }

        /// <summary>그래프가 쓸 전체 시간 구간. 두 로그를 모두 덮습니다.</summary>
        public void FullTimeRange(out double t0, out double t1)
        {
            double shift = Comparison != null ? Comparison.AppliedShift : 0.0;
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

        // ---- 그룹 손보기 -------------------------------------------------

        public void AddGroup(string name)
        {
            Settings.Groups.Add(new GroupDef(string.IsNullOrEmpty(name) ? "새 그룹" : name));
            View.RebuildRows();
        }

        public void RemoveGroup(int index)
        {
            if (index < 0 || index >= Settings.Groups.Count) return;
            Settings.Groups.RemoveAt(index);
            View.RebuildRows();
        }

        public void MoveGroup(int index, int delta)
        {
            int to = index + delta;
            if (index < 0 || index >= Settings.Groups.Count) return;
            if (to < 0 || to >= Settings.Groups.Count) return;
            GroupDef g = Settings.Groups[index];
            Settings.Groups.RemoveAt(index);
            Settings.Groups.Insert(to, g);
            View.RebuildRows();
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
            View.RebuildRows();
        }

        public void RemoveFromGroups(IEnumerable<string> names)
        {
            foreach (string n in names)
                for (int g = 0; g < Settings.Groups.Count; g++) Settings.Groups[g].Members.Remove(n);
            View.RebuildRows();
        }
    }
}
