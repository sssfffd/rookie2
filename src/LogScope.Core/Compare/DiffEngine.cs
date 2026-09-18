using System;
using System.Collections.Generic;
using LogScope.Core.Io;
using LogScope.Core.Model;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// 이전 로그와 이후 로그를 채널 이름으로 맞춰 견줍니다.
    ///
    /// 두 로그는 시간 격자가 다를 수 있으므로, 겹치는 구간에 공통 격자를 깔고
    /// 양쪽에서 그 시각의 값을 읽어 비교합니다. 공통 격자는 이전 로그의
    /// 실제 표본 시각을 씁니다 (없는 시각을 지어내지 않기 위해서). 표본이
    /// 너무 많으면 일정 간격으로 건너뛰어 상한을 지킵니다.
    /// </summary>
    public static class DiffEngine
    {
        public static CompareResult Compare(LogDataset before, LogDataset after,
                                            DiffOptions opt, LoadProgress prog)
        {
            if (opt == null) opt = new DiffOptions();
            var res = new CompareResult();
            if (before == null || after == null)
            {
                res.Warning = "비교하려면 이전 로그와 이후 로그가 모두 있어야 합니다.";
                return res;
            }

            res.BeforeChannelCount = before.ChannelCount;
            res.AfterChannelCount = after.ChannelCount;

            // ---- 1. 시간 맞추기 ------------------------------------------
            double shift = opt.Shift;
            if (opt.AutoAlign) shift += before.TimeStart - after.TimeStart;
            res.AppliedShift = shift;

            double aStart = after.TimeStart + shift;
            double aEnd = after.TimeEnd + shift;
            double t0 = Math.Max(before.TimeStart, aStart);
            double t1 = Math.Min(before.TimeEnd, aEnd);
            res.OverlapStart = t0;
            res.OverlapEnd = t1;

            bool haveOverlap = t1 > t0;
            if (!haveOverlap)
                res.Warning = "두 로그의 시간 구간이 겹치지 않습니다. 설정에서 밀기 값을 조정해 보세요.";

            // ---- 2. 공통 격자 --------------------------------------------
            double[] grid = haveOverlap ? BuildGrid(before, t0, t1, opt.MaxComparePoints) : new double[0];
            res.ComparePoints = grid.Length;

            // ---- 3. 채널 짝짓기 -------------------------------------------
            var afterUsed = new bool[after.ChannelCount];
            int done = 0;

            for (int i = 0; i < before.ChannelCount; i++)
            {
                if (prog != null && (i & 0x1F) == 0)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("채널을 견주는 중… " + i + " / " + before.ChannelCount,
                                before.ChannelCount > 0 ? (double)i / before.ChannelCount : -1);
                }

                Channel bc = before.Channels[i];
                int j = after.FindChannel(bc.Name);
                if (j < 0)
                {
                    var only = new ChannelDiff();
                    only.Name = bc.Name;
                    only.Presence = Presence.OnlyBefore;
                    only.BeforeIndex = i;
                    only.Changed = true;
                    res.OnlyBefore.Add(only);
                    continue;
                }

                afterUsed[j] = true;
                ChannelDiff d = CompareOne(before, i, after, j, grid, shift, opt);
                res.Items.Add(d);
                done++;
            }

            for (int j = 0; j < after.ChannelCount; j++)
            {
                if (afterUsed[j]) continue;
                var only = new ChannelDiff();
                only.Name = after.Channels[j].Name;
                only.Presence = Presence.OnlyAfter;
                only.AfterIndex = j;
                only.Changed = true;
                res.OnlyAfter.Add(only);
            }

            res.CommonCount = done;
            int changed = 0;
            for (int i = 0; i < res.Items.Count; i++) if (res.Items[i].Changed) changed++;
            res.ChangedCount = changed;

            SortByMetric(res.Items, opt.SortBy);
            return res;
        }

        /// <summary>
        /// 이전 로그의 실제 표본 시각 중 겹치는 구간 안의 것들. 개수가 상한을
        /// 넘으면 일정 간격으로 건너뜁니다.
        /// </summary>
        private static double[] BuildGrid(LogDataset before, double t0, double t1, int maxPoints)
        {
            int i0 = before.IndexAtOrBefore(t0);
            if (i0 < 0) i0 = 0;
            while (i0 < before.SampleCount && before.Times[i0] < t0) i0++;

            int i1 = before.IndexAtOrBefore(t1);
            if (i1 < i0) i1 = i0;

            int n = i1 - i0 + 1;
            if (n <= 0) return new double[0];
            if (maxPoints <= 0) maxPoints = 20000;

            int stride = 1;
            if (n > maxPoints) stride = (int)Math.Ceiling((double)n / maxPoints);

            int outN = (n + stride - 1) / stride;
            double[] grid = new double[outN];
            int k = 0;
            for (int i = i0; i <= i1 && k < outN; i += stride) grid[k++] = before.Times[i];
            if (k < outN) Array.Resize(ref grid, k);
            return grid;
        }

        private static ChannelDiff CompareOne(LogDataset before, int bi, LogDataset after, int ai,
                                              double[] grid, double shift, DiffOptions opt)
        {
            var d = new ChannelDiff();
            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];
            d.Name = bc.Name;
            d.Presence = Presence.Both;
            d.BeforeIndex = bi;
            d.AfterIndex = ai;

            bool byName = bc.Kind == ChannelKind.State || ac.Kind == ChannelKind.State;
            d.ByName = byName;

            // 기준값은 채널마다 다릅니다. 범위가 몇천인 아날로그와 0/1 만 오가는
            // 디지털에 같은 절대값을 들이댈 수 없기 때문입니다.
            double tol = ToleranceRule.For(bc, ac, opt.AbsoluteTolerance, opt.RelativeTolerance);
            if (byName && tol >= 1.0) tol = 0.0;   // 상태 채널의 차이는 0/1 로만 나옵니다.
            d.Threshold = tol;

            double sum = 0, sumSq = 0, area = 0, maxAbs = 0, overTime = 0, totalTime = 0;
            int compared = 0, overCount = 0, segments = 0;
            bool inSegment = false;
            double prevT = 0, prevD = 0;
            bool havePrev = false;

            for (int k = 0; k < grid.Length; k++)
            {
                double t = grid[k];
                double bv = before.SampleAt(bi, t);
                double av = after.SampleAt(ai, t - shift);

                double diff;
                if (byName)
                {
                    string bs = StateText(bc, bv);
                    string as_ = StateText(ac, av);
                    if (bs == null && as_ == null) { havePrev = false; continue; }
                    diff = string.Equals(bs, as_, StringComparison.Ordinal) ? 0.0 : 1.0;
                }
                else
                {
                    if (double.IsNaN(bv) || double.IsNaN(av)) { havePrev = false; continue; }
                    diff = Math.Abs(bv - av);
                }

                compared++;
                sum += diff;
                sumSq += diff * diff;
                if (diff > maxAbs) maxAbs = diff;

                bool over = diff > tol;
                if (over) overCount++;
                if (over && !inSegment) { segments++; inSegment = true; }
                else if (!over) inSegment = false;

                if (havePrev)
                {
                    double dt = t - prevT;
                    if (dt > 0)
                    {
                        area += 0.5 * (diff + prevD) * dt;
                        totalTime += dt;
                        if (over || prevD > tol) overTime += dt;
                    }
                }
                prevT = t; prevD = diff; havePrev = true;
            }

            d.ComparedSamples = compared;
            d.MaxAbs = maxAbs;
            d.MeanAbs = compared > 0 ? sum / compared : 0;
            d.Rms = compared > 0 ? Math.Sqrt(sumSq / compared) : 0;
            d.Area = area;
            d.TimeRatio = totalTime > 0 ? overTime / totalTime : 0;
            d.DiffSamples = overCount;
            d.Segments = segments;
            d.Changed = compared > 0 && maxAbs > tol;
            return d;
        }

        private static string StateText(Channel c, double v)
        {
            if (double.IsNaN(v)) return null;
            if (c.Kind == ChannelKind.State && c.States != null)
            {
                int i = (int)Math.Round(v);
                if (i >= 0 && i < c.States.Length) return c.States[i];
                return "?";
            }
            return v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static void SortByMetric(List<ChannelDiff> items, DiffMetric m)
        {
            items.Sort(delegate (ChannelDiff a, ChannelDiff b)
            {
                int c = b.Value(m).CompareTo(a.Value(m));
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
