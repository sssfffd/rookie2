using System;
using System.Collections.Generic;
using LogScope.Core.Io;
using LogScope.Core.Model;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// 한 칸(시간 구간 하나 x IO 하나)의 요약.
    ///
    /// 차이량으로 무엇을 쓸지가 중요합니다.
    ///  - 부호 있는 합계: +1 / -1 로 진동하면 0 이 되어 버려 차이를 놓칩니다.
    ///  - 절대값 합계: 구간이 길수록 무조건 커져서 구간끼리 비교가 안 됩니다.
    ///  - 절대 차이의 평균: 앞 둘을 피하지만, 잠깐 크게 튄 것이 묻힙니다.
    ///  - <b>가장 크게 벌어진 순간의 오차(%)</b>: 구간 길이에 휘둘리지 않고,
    ///    진동해도 0 이 되지 않고, 튄 것도 묻히지 않습니다. 무엇보다 설정에
    ///    적는 허용 오차와 <b>같은 잣대</b>라 눈으로 바로 견줄 수 있습니다.
    ///    그래서 칸에 적는 숫자는 이 값입니다.
    /// 평균 · 최대 · RMS 도 함께 담아 설명 줄에 보여 줍니다.
    /// </summary>
    public struct HeatCell
    {
        /// <summary>
        /// 이 구간에서 <b>가장 크게 벌어진 순간의 오차(%)</b>.
        /// 칸에 적히는 값이고, "차이 났다" 는 판정도 이 값으로 내립니다.
        ///
        /// 이 값은 허용 오차를 <b>바꿔도 변하지 않습니다.</b> 예전에는 칸에
        /// "기준을 넘은 표본만의 평균" 을 적었는데, 기준을 바꾸면 평균 낼
        /// 표본이 바뀌어서 숫자가 따라 움직였습니다. 재는 값이 재는 잣대에
        /// 딸려 있으면 안 됩니다.
        ///
        /// 최대를 쓰면 구간이 길다고 커지지도 않고(합계의 문제), 진동해도
        /// 0 이 되지 않습니다(부호 있는 합계의 문제). 잠깐 크게 튄 것도
        /// 평균에 묻히지 않습니다.
        /// </summary>
        public float PeakPercent;

        /// <summary>
        /// PeakPercent 가 나온 <b>그 순간</b>의 절대 차이. 칸을 값으로 볼 때
        /// 적히는 숫자입니다. 같은 표본에서 나온 값이라 %로 보다가 값으로
        /// 바꿔도 가리키는 순간이 달라지지 않습니다.
        /// </summary>
        public float PeakDiff;

        /// <summary>구간 안 모든 표본의 절대 차이 평균. 설명 줄에만 씁니다.</summary>
        public float Mean;

        /// <summary>이 구간에서 가장 크게 벌어진 절대 차이.</summary>
        public float Max;

        /// <summary>제곱평균제곱근. 큰 차이에 더 무게를 둡니다.</summary>
        public float Rms;

        /// <summary>견준 표본 수. 0 이면 이 구간에 기록이 없습니다.</summary>
        public int Samples;

        /// <summary>허용 오차를 넘은 표본 수.</summary>
        public int OverSamples;

        public bool HasData { get { return Samples > 0; } }
    }

    /// <summary>IO 하나의 가로 한 줄.</summary>
    public sealed class HeatRow
    {
        public string Name;
        public int BeforeIndex = -1;
        public int AfterIndex = -1;

        public HeatCell[] Cells;

        /// <summary>
        /// 차이가 난 칸들 중 가장 큰 오차(%). <b>색 진하기에만</b> 씁니다.
        ///
        /// 한 줄 안에서 "어느 시간대가 제일 심한가" 를 색으로 보여 주는 값이라
        /// 칸 폭이 바뀌면 같이 바뀝니다 — 1 분씩 따로 세던 것을 5 분으로 묶어
        /// 평균 내면 봉우리가 낮아집니다. 줄 차례를 매기는 데 쓰면 안 됩니다.
        /// </summary>
        public double PeakMean;

        /// <summary>
        /// 차이가 난 칸의 개수.
        /// <b>칸 폭에 따라 달라집니다</b> — 1 분 칸 5 개가 5 분 칸 1 개가 되면
        /// 이 값은 5 분의 1 이 됩니다. 그래서 줄 차례를 매기는 데 쓰면 안 됩니다.
        /// </summary>
        public int OverBuckets;

        /// <summary>
        /// 기준을 넘은 표본의 총 개수. 칸을 어떻게 자르든 같습니다.
        /// </summary>
        public int TotalOverSamples;

        /// <summary>
        /// 줄 전체에서 가장 크게 벌어진 순간의 오차(%).
        /// 칸을 어떻게 자르든 같습니다.
        /// </summary>
        public double PeakMax;

        /// <summary>
        /// 값이 아니라 이름(상태 문자열)으로 견준 줄인지.
        /// 이런 줄은 이름이 다르면 허용 오차와 상관없이 차이이고,
        /// 오차는 0% 아니면 100% 입니다.
        /// </summary>
        public bool ByName;

        /// <summary>값의 단위. 칸 설명에 같이 적습니다. 없으면 빈 문자열.</summary>
        public string Unit = string.Empty;

        public bool HasDifference { get { return OverBuckets > 0; } }

        /// <summary>
        /// 칸에 적을 값(%). 색을 정하는 것과 같은 값입니다.
        /// 넘은 표본이 없으면 0 이고, 그 칸은 정상입니다.
        /// </summary>
        public double PercentOf(HeatCell c)
        {
            if (!c.HasData || c.OverSamples == 0) return 0;
            return c.PeakPercent;
        }

        /// <summary>칸을 값으로 볼 때 적을 숫자. PercentOf 와 같은 순간입니다.</summary>
        public double ValueOf(HeatCell c)
        {
            if (!c.HasData || c.OverSamples == 0) return 0;
            return c.PeakDiff;
        }

        /// <summary>
        /// 이 IO 를 얼마나 먼저 봐야 하는지. 줄 차례를 매기는 값입니다.
        ///
        /// 줄 전체에서 가장 크게 벌어진 순간의 오차(%)입니다. 두 가지를
        /// 동시에 만족해야 해서 이렇게 골랐습니다.
        ///
        ///  - <b>칸 폭과 무관해야 합니다.</b> 1 분으로 보든 5 분으로 보든
        ///    같은 로그이므로 줄 차례가 바뀌면 안 됩니다. 표본 하나하나에서
        ///    나온 값이라 칸을 어떻게 묶든 최대는 최대입니다.
        ///  - <b>IO 끼리 견줄 수 있어야 합니다.</b> 퍼센트라 값 크기가 다른
        ///    채널끼리도 그대로 견줍니다.
        /// </summary>
        public double Severity { get { return PeakMax; } }

    }

    public sealed class HeatmapOptions
    {
        /// <summary>한 칸의 시간 폭. 0 이면 스스로 정합니다 (시각 로그면 1 분).</summary>
        public double BucketSpan;

        /// <summary>절대 허용 오차. 이 값 이하의 차이는 같은 것으로 봅니다.</summary>
        public double AbsoluteTolerance;

        /// <summary>
        /// 허용 오차 퍼센트. 설정에 적는 그 값 그대로입니다 (0.1 이 0.1%).
        /// 그 순간의 오차 = |이후 − 이전| / |이전| x 100 과 견줍니다.
        /// 자세한 규칙은 ToleranceRule 에 있습니다.
        /// </summary>
        public double RelativePercent = ToleranceRule.DefaultPercent;

        /// <summary>칸이 너무 많아지지 않게 하는 상한.</summary>
        public int MaxBuckets = 4000;

        /// <summary>차이가 없는 IO 도 줄로 넣을지.</summary>
        public bool IncludeUnchanged;

        /// <summary>이후 로그의 시간을 이만큼 밀어 맞춥니다.</summary>
        public double Shift;

        public HeatmapOptions Clone() { return (HeatmapOptions)MemberwiseClone(); }
    }

    public sealed class HeatmapResult
    {
        public List<HeatRow> Rows = new List<HeatRow>();

        public double Start;
        public double End;
        public double BucketSpan;
        public int BucketCount;

        /// <summary>칸 시각을 글자로 바꿀 때 쓸 기준 로그.</summary>
        public LogDataset TimeReference;

        /// <summary>1 분 단위로 잘랐는지, 아니면 다른 폭으로 잘랐는지.</summary>
        public string BucketLabel = string.Empty;

        public string Warning = string.Empty;

        public int ComparedChannels;
        public int ChangedChannels;

        public double BucketStart(int index) { return Start + index * BucketSpan; }
    }

    /// <summary>
    /// 시간을 일정한 폭(기본 1 분)으로 잘라, IO 마다 그 구간에서 이전/이후가
    /// 얼마나 벌어졌는지를 표로 만듭니다.
    /// </summary>
    public static class HeatmapBuilder
    {
        private const double OneMinuteMs = 60000.0;

        public static HeatmapResult Build(LogDataset before, LogDataset after,
                                          HeatmapOptions opt, LoadProgress prog)
        {
            if (opt == null) opt = new HeatmapOptions();
            var res = new HeatmapResult();
            res.TimeReference = before ?? after;

            if (before == null || after == null)
            {
                res.Warning = "히트맵을 그리려면 이전 로그와 이후 로그가 모두 있어야 합니다.";
                return res;
            }

            double shift = opt.Shift;
            double t0 = Math.Max(before.TimeStart, after.TimeStart + shift);
            double t1 = Math.Min(before.TimeEnd, after.TimeEnd + shift);
            if (!(t1 > t0))
            {
                res.Warning = "두 로그의 시간 구간이 겹치지 않습니다.";
                return res;
            }

            double span = ChooseBucketSpan(before, t0, t1, opt, res);
            int buckets = (int)Math.Ceiling((t1 - t0) / span);
            if (buckets < 1) buckets = 1;

            res.Start = t0;
            res.End = t1;
            res.BucketSpan = span;
            res.BucketCount = buckets;

            int compared = 0, changed = 0;

            for (int bi = 0; bi < before.ChannelCount; bi++)
            {
                if (prog != null && (bi & 0x0F) == 0)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("구간별 차이를 세는 중… " + bi + " / " + before.ChannelCount,
                                before.ChannelCount > 0 ? (double)bi / before.ChannelCount : -1);
                }

                Channel bc = before.Channels[bi];
                int ai = after.FindChannel(bc.Name);
                if (ai < 0) continue;   // 한쪽에만 있는 IO 는 여기서 다루지 않습니다.

                compared++;
                HeatRow row = BuildRow(before, bi, after, ai, t0, t1, span, buckets, shift, opt);
                if (row.HasDifference) changed++;
                if (row.HasDifference || opt.IncludeUnchanged) res.Rows.Add(row);
            }

            res.ComparedChannels = compared;
            res.ChangedChannels = changed;

            // 많이 벌어진 IO 가 위로 오게.
            //
            // 기준은 <b>칸 폭과 무관한 값</b>만 씁니다. 예전에는 PeakMean(칸들
            // 중 가장 큰 값)과 OverBuckets(차이 난 칸 수)로 줄을 세웠는데,
            // 둘 다 칸을 어떻게 잘랐는지에 딸린 값입니다. 그래서 같은 로그인데
            // 1 분으로 보다가 5 분으로 바꾸면 IO 차례가 뒤바뀌었습니다.
            // 5 분으로 묶으면 1 분짜리 봉우리가 이웃한 0 들과 평균되어 낮아지고,
            // 차이 난 칸 수도 통째로 줄어들기 때문입니다.
            res.Rows.Sort(delegate (HeatRow a, HeatRow b)
            {
                int c = b.Severity.CompareTo(a.Severity);
                if (c != 0) return c;
                c = b.TotalOverSamples.CompareTo(a.TotalOverSamples);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            if (res.Rows.Count == 0 && !opt.IncludeUnchanged)
                res.Warning = "허용 오차 안에서 차이가 난 IO 가 없습니다.";

            return res;
        }

        /// <summary>
        /// 칸 하나의 시간 폭을 정합니다.
        /// 시각으로 읽힌 로그는 1 분으로 자릅니다. 시각이 아니면(샘플 번호나
        /// 단위 없는 숫자) 1 분이라는 게 뜻이 없으므로 전체를 60 칸으로 나눕니다.
        /// 어느 쪽이든 칸 수가 상한을 넘으면 폭을 넓힙니다.
        /// </summary>
        private static double ChooseBucketSpan(LogDataset before, double t0, double t1,
                                               HeatmapOptions opt, HeatmapResult res)
        {
            double total = t1 - t0;

            if (opt.BucketSpan > 0)
            {
                res.BucketLabel = DescribeSpan(before.TimeKind, opt.BucketSpan);
                return GrowToFit(opt.BucketSpan, total, opt.MaxBuckets, before.TimeKind, res);
            }

            bool clock = before.TimeKind == TimeKind.ClockMs || before.TimeKind == TimeKind.DateMs;
            double span = clock ? OneMinuteMs : Math.Max(total / 60.0, 1e-9);
            res.BucketLabel = DescribeSpan(before.TimeKind, span);
            return GrowToFit(span, total, opt.MaxBuckets, before.TimeKind, res);
        }

        private static double GrowToFit(double span, double total, int maxBuckets,
                                        TimeKind kind, HeatmapResult res)
        {
            if (maxBuckets <= 0) maxBuckets = 4000;
            if (span <= 0) span = Math.Max(total / 60.0, 1e-9);

            while (total / span > maxBuckets)
            {
                span *= 2;
                res.BucketLabel = DescribeSpan(kind, span) + " (칸이 너무 많아 폭을 넓혔습니다)";
            }
            return span;
        }

        private static string DescribeSpan(TimeKind kind, double span)
        {
            if (kind == TimeKind.ClockMs || kind == TimeKind.DateMs)
            {
                double minutes = span / OneMinuteMs;
                if (minutes >= 1) return minutes.ToString("0.##") + "분";
                return (span / 1000.0).ToString("0.##") + "초";
            }
            if (kind == TimeKind.Index) return span.ToString("0.##") + "표본";
            return NumberText.Plain(span);
        }

        /// <summary>
        /// 이전 로그의 표본을 시간 순서대로 훑으면서, 이후 로그의 자리를
        /// 같이 앞으로 밀며 값을 읽습니다. 표본마다 이진 탐색을 하지 않으므로
        /// 채널 하나가 O(표본 수) 로 끝납니다.
        /// </summary>
        private static HeatRow BuildRow(LogDataset before, int bi, LogDataset after, int ai,
                                        double t0, double t1, double span, int buckets,
                                        double shift, HeatmapOptions opt)
        {
            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];

            var row = new HeatRow();
            row.Name = bc.Name;
            row.BeforeIndex = bi;
            row.AfterIndex = ai;
            row.Cells = new HeatCell[buckets];
            bool stepped = bc.IsStepped || ac.IsStepped;
            bool byName = bc.Kind == ChannelKind.State || ac.Kind == ChannelKind.State;

            row.ByName = byName;

            row.Unit = byName ? string.Empty : (bc.Unit.Length > 0 ? bc.Unit : ac.Unit);

            double[] bt = before.Times, at = after.Times;
            float[] bv = bc.Values, av = ac.Values;
            int an = after.SampleCount;

            var sum = new double[buckets];
            var sumSq = new double[buckets];

            int start = before.IndexAtOrBefore(t0);
            if (start < 0) start = 0;
            while (start < before.SampleCount && bt[start] < t0) start++;

            int j = 0;   // 이후 로그에서 지금 보고 있는 자리 (뒤로 가지 않습니다)

            for (int i = start; i < before.SampleCount && bt[i] <= t1; i++)
            {
                double t = bt[i];
                double ta = t - shift;

                while (j + 1 < an && at[j + 1] <= ta) j++;
                if (j >= an) break;
                if (at[j] > ta) continue;      // 아직 이후 로그의 기록 구간에 들어오지 않음

                float b = bv[i];
                double a;

                if (stepped) a = av[j];
                else
                {
                    float a0 = av[j];
                    if (j + 1 < an)
                    {
                        float a1 = av[j + 1];
                        double t0a = at[j], t1a = at[j + 1];
                        if (float.IsNaN(a0)) a = double.NaN;
                        else if (float.IsNaN(a1) || t1a <= t0a) a = a0;
                        else a = a0 + (a1 - a0) * (ta - t0a) / (t1a - t0a);
                    }
                    else a = a0;
                }

                // 오차는 그 순간의 값끼리 견줍니다 (ToleranceRule 참고).
                double diff, pct;
                bool over;

                if (byName)
                {
                    string bs = StateText(bc, b);
                    string as_ = StateText(ac, a);
                    if (bs == null && as_ == null) continue;

                    bool same = string.Equals(bs, as_, StringComparison.Ordinal);
                    diff = same ? 0.0 : 1.0;
                    pct = same ? 0.0 : ToleranceRule.NameMismatchPercent;
                    // 이름이 다르면 허용 오차와 상관없이 차이입니다.
                    over = !same && ToleranceRule.NameMismatchIsOver;
                }
                else
                {
                    if (float.IsNaN(b) || double.IsNaN(a)) continue;
                    diff = Math.Abs(b - a);
                    pct = ToleranceRule.ErrorPercent(b, a);
                    over = ToleranceRule.IsOver(b, a, opt.AbsoluteTolerance, opt.RelativePercent);
                }

                int k = (int)((t - t0) / span);
                if (k < 0) k = 0;
                if (k >= buckets) k = buckets - 1;

                HeatCell cell = row.Cells[k];
                cell.Samples++;
                sum[k] += diff;
                sumSq[k] += diff * diff;
                if (diff > cell.Max) cell.Max = (float)diff;

                if (over)
                {
                    cell.OverSamples++;
                    // 가장 크게 벌어진 순간을 붙잡습니다. 값과 퍼센트를
                    // <b>같은 표본에서</b> 가져와야 둘이 가리키는 순간이
                    // 어긋나지 않습니다.
                    if (!double.IsNaN(pct) && pct > cell.PeakPercent)
                    {
                        cell.PeakPercent = (float)pct;
                        cell.PeakDiff = (float)diff;
                    }
                }
                row.Cells[k] = cell;
            }

            for (int k = 0; k < buckets; k++)
            {
                HeatCell cell = row.Cells[k];
                if (cell.Samples == 0) continue;
                cell.Mean = (float)(sum[k] / cell.Samples);
                cell.Rms = (float)Math.Sqrt(sumSq[k] / cell.Samples);
                row.Cells[k] = cell;

                // "이 구간에서 차이가 났나" 는 한 순간이라도 허용 오차를
                // 넘었는지로 봅니다. 순간의 튐도 차이이고, 평균을 내면
                // 묻혀 버리니까요. 칸에 적는 숫자도 바로 그 "가장 크게 넘은
                // 순간" 이라, 숫자와 판정과 색이 모두 같은 값에서 나옵니다.
                if (cell.OverSamples > 0)
                {
                    row.OverBuckets++;
                    row.TotalOverSamples += cell.OverSamples;
                    if (cell.PeakPercent > row.PeakMean) row.PeakMean = cell.PeakPercent;
                    if (cell.PeakPercent > row.PeakMax) row.PeakMax = cell.PeakPercent;
                }
            }

            return row;
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

        /// <summary>칸이 "차이 남" 인지.</summary>
        public static bool IsOver(HeatRow row, int bucket)
        {
            if (row == null || row.Cells == null || bucket < 0 || bucket >= row.Cells.Length) return false;
            return row.Cells[bucket].OverSamples > 0;
        }
    }
}
