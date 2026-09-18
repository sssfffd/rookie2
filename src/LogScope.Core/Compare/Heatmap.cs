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
    ///  - <b>절대 차이의 평균</b>: 구간 길이에 휘둘리지 않고, 진동해도 0 이
    ///    되지 않습니다. 그래서 칸에 적는 숫자는 이 값입니다.
    /// 최대값과 RMS 도 함께 담아 두어 필요하면 바꿔 볼 수 있게 했습니다.
    /// </summary>
    public struct HeatCell
    {
        /// <summary>
        /// 구간 안 모든 표본의 절대 차이 평균.
        ///
        /// 이 값은 <b>칸에 적지 않습니다.</b> 60 표본 중 하나만 크게 튀면
        /// 나머지 59 개의 0 에 묻혀 기준보다 작아지는데, 칸은 빨갛게 되어
        /// "기준 0.1% 인데 0.008% 라고 적힌 빨간 칸" 이 나옵니다.
        /// 칸에 적는 값은 아래 OverMean 입니다. 이 값은 설명 줄에만 씁니다.
        /// </summary>
        public float Mean;

        /// <summary>
        /// <b>기준을 넘은 표본만</b>의 절대 차이 평균. 칸에 적히는 값입니다.
        ///
        /// "벌어졌을 때 이만큼 벌어졌다" 는 뜻이라, 칸이 빨갛다는 것과
        /// "이 숫자가 기준보다 크다" 가 <b>항상 같은 말</b>이 됩니다.
        /// 넘은 표본이 없으면 0 이고, 그 칸은 정상입니다.
        ///
        /// 합계가 아니라 평균이라 구간이 길다고 커지지 않고, 절대값이라
        /// +1 / -1 로 진동해도 0 이 되지 않습니다. 고르던 조건은 그대로입니다.
        /// </summary>
        public float OverMean;
        /// <summary>이 구간에서 가장 크게 벌어진 순간.</summary>
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

        /// <summary>이 채널에서 "차이"로 볼 기준값. ToleranceRule 이 정합니다.</summary>
        public double Threshold;

        /// <summary>차이가 난 칸들 중 가장 큰 평균값. 색 진하기의 기준입니다.</summary>
        public double PeakMean;

        /// <summary>차이가 난 칸의 개수.</summary>
        public int OverBuckets;

        /// <summary>줄 전체에서 가장 크게 벌어진 순간.</summary>
        public double PeakMax;

        /// <summary>
        /// 차이를 퍼센트로 바꿀 때 나눌 밑값. 그 채널의 값 범위입니다
        /// (ToleranceRule.BaseRange 와 같은 값이라 허용 오차 0.1% 와 눈금이 맞습니다).
        /// 0 이면 나눌 수 없어서 퍼센트를 적지 않습니다.
        /// </summary>
        public double Range;

        /// <summary>
        /// 값이 아니라 이름(상태 문자열)으로 견준 줄인지.
        /// 이런 줄은 차이가 0 또는 1 이라 평균이 곧 "다른 표본의 비율" 입니다.
        /// 그래서 퍼센트가 바로 뜻이 통하고, 단위 붙은 값은 뜻이 없습니다.
        /// </summary>
        public bool ByName;

        /// <summary>값의 단위. 칸 설명에 같이 적습니다. 없으면 빈 문자열.</summary>
        public string Unit = string.Empty;

        public bool HasDifference { get { return OverBuckets > 0; } }

        /// <summary>
        /// 칸에 적을 값. <b>색을 정하는 것과 같은 값</b>이어야 하므로
        /// 여기 한 곳에서만 정합니다. 화면이 따로 계산하면 또 어긋납니다.
        ///
        ///  - 값으로 견준 줄: 기준을 넘은 표본만의 평균. 기준과 바로 견줄 수 있습니다.
        ///  - 이름으로 견준 줄: 다른 표본의 비율. 차이 하나하나는 늘 "1" 이라
        ///    평균을 내 봐야 항상 1 이고, 알고 싶은 것은 "얼마나 자주 달랐나" 입니다.
        /// </summary>
        public double ValueOf(HeatCell c)
        {
            if (!c.HasData || c.OverSamples == 0) return 0;
            return ByName ? c.Mean : c.OverMean;
        }

        /// <summary>
        /// 기준값을 퍼센트로. 칸에 적히는 퍼센트와 같은 밑값(Range)을 씁니다.
        /// 나눌 수 없으면 NaN.
        /// </summary>
        public double ThresholdPercent
        {
            get { return Range > 0 ? Threshold / Range * 100.0 : double.NaN; }
        }
    }

    public sealed class HeatmapOptions
    {
        /// <summary>한 칸의 시간 폭. 0 이면 스스로 정합니다 (시각 로그면 1 분).</summary>
        public double BucketSpan;

        /// <summary>절대 허용 오차. 이 값 이하의 차이는 같은 것으로 봅니다.</summary>
        public double AbsoluteTolerance;

        /// <summary>
        /// 채널 값 범위에 대한 비율 허용 오차. 기본 0.001 = 0.1%.
        /// IO 범위가 몇천인데 1 정도 흔들리는 것을 오류로 세지 않기 위한 것입니다.
        /// 기준값을 실제로 정하는 규칙은 ToleranceRule 에 있습니다.
        /// </summary>
        public double RelativeTolerance = ToleranceRule.FromPercent(ToleranceRule.DefaultPercent);

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
            res.Rows.Sort(delegate (HeatRow a, HeatRow b)
            {
                int c = b.PeakMean.CompareTo(a.PeakMean);
                if (c != 0) return c;
                c = b.OverBuckets.CompareTo(a.OverBuckets);
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
            return span.ToString("G4");
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

            // 이름으로 견주는 줄에는 허용 오차를 매기지 않습니다.
            //
            // 이런 줄의 차이는 "같다(0) / 다르다(1)" 뿐입니다. 여기에 값 범위의
            // 0.1% 같은 기준을 매기면 그 범위가 상태 번호의 범위(예: 0~2)라서
            // 기준이 아무 뜻 없는 숫자가 되고, 설명 줄에도 그 숫자가 나와
            // 위쪽에 적어 둔 허용 오차와 달라 보입니다.
            //
            // 더 나쁜 것은 절대 오차를 1 이상으로 적어 둔 경우입니다. 그러면
            // 1 > 기준 이 거짓이 되어 상태 이름이 통째로 바뀌어도 차이로 세지
            // 않았습니다. 기준을 0 으로 두어 "이름이 다르면 차이" 로 못박습니다.
            row.Threshold = byName
                ? 0.0
                : ToleranceRule.For(bc, ac, opt.AbsoluteTolerance, opt.RelativeTolerance);
            // 이름으로 견준 줄은 차이가 0/1 이므로 밑값이 1 입니다. 그래야
            // 평균 0.25 가 "표본의 25% 가 달랐다" 로 그대로 읽힙니다.
            row.Range = byName ? 1.0 : ToleranceRule.BaseRange(bc, ac);
            row.Unit = byName ? string.Empty : (bc.Unit.Length > 0 ? bc.Unit : ac.Unit);

            double[] bt = before.Times, at = after.Times;
            float[] bv = bc.Values, av = ac.Values;
            int an = after.SampleCount;

            var sum = new double[buckets];
            var sumSq = new double[buckets];
            var overSum = new double[buckets];

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

                double diff;
                if (byName)
                {
                    string bs = StateText(bc, b);
                    string as_ = StateText(ac, a);
                    if (bs == null && as_ == null) continue;
                    diff = string.Equals(bs, as_, StringComparison.Ordinal) ? 0.0 : 1.0;
                }
                else
                {
                    if (float.IsNaN(b) || double.IsNaN(a)) continue;
                    diff = Math.Abs(b - a);
                }

                int k = (int)((t - t0) / span);
                if (k < 0) k = 0;
                if (k >= buckets) k = buckets - 1;

                HeatCell cell = row.Cells[k];
                cell.Samples++;
                sum[k] += diff;
                sumSq[k] += diff * diff;
                if (diff > cell.Max) cell.Max = (float)diff;
                if (diff > row.Threshold) { cell.OverSamples++; overSum[k] += diff; }
                row.Cells[k] = cell;
            }

            for (int k = 0; k < buckets; k++)
            {
                HeatCell cell = row.Cells[k];
                if (cell.Samples == 0) continue;
                cell.Mean = (float)(sum[k] / cell.Samples);
                cell.Rms = (float)Math.Sqrt(sumSq[k] / cell.Samples);
                cell.OverMean = cell.OverSamples > 0 ? (float)(overSum[k] / cell.OverSamples) : 0f;
                row.Cells[k] = cell;

                if (cell.Max > row.PeakMax) row.PeakMax = cell.Max;

                // "이 구간에서 차이가 났나" 는 한 순간이라도 기준을 넘었는지로
                // 봅니다. 순간의 튐도 차이이고, 평균을 내면 묻혀 버리니까요.
                //
                // 그래서 칸에 적는 숫자도 "넘은 표본만의 평균"(ValueOf)입니다.
                // 색 진하기도 같은 값을 기준으로 해야 숫자와 색이 따로 놀지
                // 않습니다.
                if (cell.OverSamples > 0)
                {
                    row.OverBuckets++;
                    double shown = row.ValueOf(cell);
                    if (shown > row.PeakMean) row.PeakMean = shown;
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
