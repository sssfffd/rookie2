using System.Collections.Generic;
using System;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Compare
{
    // 1.50: 두 로그 견주기 — DiffEngine · DiffTypes (옵션 · IO 하나의 차이 · 결과).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

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

            // 허용 오차는 IO 마다 다를 수 있습니다. 표본마다 표를 뒤지지 않게
            // 채널 하나당 한 번만 풀어 둡니다.
            double limitAbs = opt.AbsoluteTolerance, limitPct = opt.RelativePercent;
            if (opt.Tolerances != null)
            {
                limitAbs = opt.Tolerances.AbsoluteFor(bc.Name);
                limitPct = opt.Tolerances.PercentFor(bc.Name);
            }

            d.BeforeIndex = bi;
            d.AfterIndex = ai;

            bool byName = bc.Kind == ChannelKind.State || ac.Kind == ChannelKind.State;
            d.ByName = byName;

            // 오차는 그 순간의 값끼리 견줍니다 (ToleranceRule 참고).
            // 채널마다 기준값을 따로 만들지 않습니다 — 어느 채널이든
            // 설정에 적은 퍼센트 하나가 그대로 기준입니다.
            double sum = 0, sumSq = 0, area = 0, maxAbs = 0, maxPct = 0;
            double overTime = 0, totalTime = 0;
            int compared = 0, overCount = 0, segments = 0;
            bool inSegment = false;
            double prevT = 0, prevD = 0;
            bool prevOver = false, havePrev = false;

            for (int k = 0; k < grid.Length; k++)
            {
                double t = grid[k];
                double bv = before.SampleAt(bi, t);
                double av = after.SampleAt(ai, t - shift);

                double diff, pct;
                bool over;

                if (byName)
                {
                    string bs = StateText(bc, bv);
                    string as_ = StateText(ac, av);
                    if (bs == null && as_ == null) { havePrev = false; continue; }

                    bool same = string.Equals(bs, as_, StringComparison.Ordinal);
                    diff = same ? 0.0 : 1.0;
                    pct = same ? 0.0 : ToleranceRule.NameMismatchPercent;
                    // 이름이 다르면 허용 오차와 상관없이 차이입니다.
                    over = !same && ToleranceRule.NameMismatchIsOver;
                }
                else
                {
                    if (double.IsNaN(bv) || double.IsNaN(av)) { havePrev = false; continue; }
                    diff = Math.Abs(bv - av);
                    pct = ToleranceRule.ErrorPercent(bv, av);
                    over = ToleranceRule.IsOver(bv, av, limitAbs, limitPct);
                }

                compared++;
                sum += diff;
                sumSq += diff * diff;
                if (diff > maxAbs) maxAbs = diff;
                if (!double.IsNaN(pct) && pct > maxPct) maxPct = pct;

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
                        if (over || prevOver) overTime += dt;
                    }
                }
                prevT = t; prevD = diff; prevOver = over; havePrev = true;
            }

            d.ComparedSamples = compared;
            d.MaxAbs = maxAbs;
            d.MaxPercent = maxPct;
            d.MeanAbs = compared > 0 ? sum / compared : 0;
            d.Rms = compared > 0 ? Math.Sqrt(sumSq / compared) : 0;
            d.Area = area;
            d.TimeRatio = totalTime > 0 ? overTime / totalTime : 0;
            d.DiffSamples = overCount;
            d.Segments = segments;
            // 한 순간이라도 넘었으면 차이입니다. 그 "가장 크게 넘은 순간" 이
            // 곧 MaxPercent 라, 목록에 적히는 숫자와 판정이 늘 맞습니다.
            d.Changed = overCount > 0;
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

        /// <summary>차이량 기준으로 정렬합니다. 값이 같으면 이름 순.</summary>
        public static void SortByMetric(List<ChannelDiff> items, DiffMetric m)
        {
            SortByMetric(items, m, true);
        }

        public static void SortByMetric(List<ChannelDiff> items, DiffMetric m, bool descending)
        {
            items.Sort(delegate (ChannelDiff a, ChannelDiff b)
            {
                int c = descending
                    ? b.Value(m).CompareTo(a.Value(m))
                    : a.Value(m).CompareTo(b.Value(m));
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>IO 이름 순으로 정렬합니다.</summary>
        public static void SortByName(List<ChannelDiff> items, bool ascending)
        {
            items.Sort(delegate (ChannelDiff a, ChannelDiff b)
            {
                int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return ascending ? c : -c;
            });
        }

        /// <summary>"값 다름 / 상태 다름" 구분으로 정렬합니다.</summary>
        public static void SortByKind(List<ChannelDiff> items, bool ascending)
        {
            items.Sort(delegate (ChannelDiff a, ChannelDiff b)
            {
                int c = a.ByName.CompareTo(b.ByName);
                if (!ascending) c = -c;
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    // ======================================================================
    //  DiffTypes.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    public enum Presence
    {
        Both = 0,
        OnlyBefore = 1,
        OnlyAfter = 2,
    }

    public sealed class DiffOptions
    {
        /// <summary>
        /// 절대 허용 오차. 이 값 이하의 차이는 같은 것으로 봅니다.
        /// 기본 0 (끔). 비율 오차와 함께 쓰이며 둘 중 큰 쪽이 기준이 됩니다.
        /// </summary>
        public double AbsoluteTolerance = 0.0;

        /// <summary>
        /// 허용 오차 퍼센트. 설정에 적는 그 값 그대로입니다 (0.1 이 0.1%).
        ///
        /// 그 순간의 오차 = |이후 − 이전| / |이전| x 100 과 견줍니다.
        /// 채널마다 기준을 따로 만들지 않습니다. 자세한 규칙은 ToleranceRule.
        /// </summary>
        public double RelativePercent = ToleranceRule.DefaultPercent;

        /// <summary>
        /// IO 별 허용 오차. <b>있으면 이것이 이깁니다</b> — 위의 두 값 대신
        /// 표의 기본값과 IO 별 값을 씁니다. 없으면(null) 위의 두 값 그대로.
        /// </summary>
        public ToleranceTable Tolerances;

        /// <summary>이후 로그의 시간을 이만큼 밉니다 (시간축 단위).</summary>
        public double Shift = 0.0;

        /// <summary>두 로그의 시작 시각을 자동으로 맞춥니다.</summary>
        public bool AutoAlign = true;

        /// <summary>비교에 쓸 표본 수 상한. 크면 정확하지만 느려집니다.</summary>
        public int MaxComparePoints = 20000;

        /// <summary>목록을 정렬할 기준.</summary>
        public DiffMetric SortBy = DiffMetric.MaxAbs;

        public DiffOptions Clone() { return (DiffOptions)MemberwiseClone(); }
    }

    /// <summary>채널 하나의 비교 결과.</summary>
    public sealed class ChannelDiff
    {
        public string Name;
        public Presence Presence;
        public int BeforeIndex = -1;
        public int AfterIndex = -1;

        public double MaxAbs;

        /// <summary>
        /// 가장 크게 벌어진 순간의 오차(%). 이전 값 대비입니다.
        /// "차이 났다" 는 판정도 이 값이 허용 오차를 넘었는지로 내리므로,
        /// 목록에 적히는 숫자와 판정이 어긋날 수 없습니다.
        /// </summary>
        public double MaxPercent;
        public double MeanAbs;
        public double Rms;
        public double Area;
        public double TimeRatio;
        public int DiffSamples;
        public int Segments;
        public int ComparedSamples;

        /// <summary>상태/문자열 채널이라 "같다/다르다" 로만 비교한 경우.</summary>
        public bool ByName;

        public bool Changed;

        public double Value(DiffMetric m)
        {
            switch (m)
            {
                case DiffMetric.MaxAbs: return MaxAbs;
                case DiffMetric.MeanAbs: return MeanAbs;
                case DiffMetric.Rms: return Rms;
                case DiffMetric.Area: return Area;
                case DiffMetric.TimeRatio: return TimeRatio;
                case DiffMetric.SampleCount: return DiffSamples;
                case DiffMetric.SegmentCount: return Segments;
                case DiffMetric.MaxPercent: return MaxPercent;
                default: return MaxAbs;
            }
        }

        public static string MetricLabel(DiffMetric m)
        {
            switch (m)
            {
                case DiffMetric.MaxAbs: return "최대 차이";
                case DiffMetric.MeanAbs: return "평균 차이";
                case DiffMetric.Rms: return "RMS";
                case DiffMetric.Area: return "차이 면적";
                case DiffMetric.TimeRatio: return "차이 시간 비율";
                case DiffMetric.SampleCount: return "차이 표본 수";
                case DiffMetric.SegmentCount: return "차이 구간 수";
                case DiffMetric.MaxPercent: return "최대 오차 %";
                default: return "최대 차이";
            }
        }

        public string Format(DiffMetric m)
        {
            double v = Value(m);
            switch (m)
            {
                case DiffMetric.TimeRatio: return (v * 100.0).ToString("0.0") + " %";
                case DiffMetric.MaxPercent:
                    // 허용 오차와 같은 잣대라 소수점을 넉넉히 둡니다.
                    // 0.1% 기준과 0.09% 를 눈으로 가려야 하기 때문입니다.
                    return NumberText.Plain(v) + " %";
                case DiffMetric.SampleCount:
                case DiffMetric.SegmentCount: return ((long)v).ToString("N0");
                default:
                    // 지수 표기(1.2e+07)를 쓰지 않습니다 — NumberText 참고.
                    return NumberText.Plain(v);
            }
        }
    }

    public sealed class CompareResult
    {
        public List<ChannelDiff> Items = new List<ChannelDiff>();
        public List<ChannelDiff> OnlyBefore = new List<ChannelDiff>();
        public List<ChannelDiff> OnlyAfter = new List<ChannelDiff>();

        public double AppliedShift;
        public double OverlapStart;
        public double OverlapEnd;
        public int ComparePoints;

        public int BeforeChannelCount;
        public int AfterChannelCount;
        public int CommonCount;
        public int ChangedCount;

        /// <summary>겹치는 구간이 하나도 없으면 여기에 이유가 들어갑니다.</summary>
        public string Warning = string.Empty;

        public List<ChannelDiff> Changed()
        {
            var r = new List<ChannelDiff>();
            for (int i = 0; i < Items.Count; i++) if (Items[i].Changed) r.Add(Items[i]);
            return r;
        }
    }
}
