using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Logs.Align;

namespace LogScope.Logs.Compare
{
    /// <summary>
    /// 두 로그를 칸마다 견주어 히트맵 표를 만듭니다.
    ///
    /// 칸을 어떻게 자를지는 <see cref="HeatBuckets"/> 가 압니다 — 시간
    /// 폭으로 자르거나(기본 1 분), <b>단계 하나를 칸 하나로</b> 자릅니다.
    /// 여기서는 "어느 칸이냐" 를 칸 목록에게 묻기만 합니다.
    ///
    /// 칸과 줄의 뜻(무엇을 차이로 보는지, 줄 차례를 무엇으로 매기는지)은
    /// 옆 파일 HeatTypes.cs 에 적혀 있습니다.
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

            // 칸을 어떻게 자를지 먼저 정합니다 — 단계로, 아니면 시간 폭으로.
            HeatBuckets cuts = null;
            if (opt.ByStep)
            {
                string why;
                cuts = HeatBuckets.Steps(opt.Steps, before, t0, t1, out why);
                if (cuts != null)
                {
                    res.BucketLabel = "단계";
                    if (why.Length > 0) res.Warning = why;
                }
                else
                {
                    // 단계로 못 자르면 시간으로 돌아가되 <b>말해 줍니다</b>.
                    // 조용히 돌아가면 가로가 단계인 줄 알고 읽습니다.
                    res.Warning = why.Length > 0 ? why
                        : "단계가 없어 가로를 시간으로 둡니다.";
                }
            }

            double span;
            int buckets;
            if (cuts != null)
            {
                span = double.NaN;
                buckets = cuts.Count;
            }
            else
            {
                span = ChooseBucketSpan(before, t0, t1, opt, res);
                cuts = HeatBuckets.Uniform(t0, t1, span);
                buckets = cuts.Count;
            }

            res.Start = t0;
            res.End = t1;
            res.BucketSpan = span;
            res.BucketCount = buckets;
            res.Buckets = cuts;

            int compared = 0, changed = 0;

            // 칸마다의 합계를 담을 자리. <b>채널마다 새로 만들지 않고</b>
            // 한 번 만들어 돌려 씁니다. IO 200 개 x 칸 4000 개면 채널마다
            // 64 KB 씩 두 개를 새로 만들어 버리게 됩니다 — 25 MB 를
            // 만들고 바로 버리는 일이라, 세는 동안 GC 가 계속 돕니다.
            //
            // 돌려 쓰므로 줄마다 지워야 합니다 (BuildRow 가 지웁니다).
            // 안 지우면 앞 IO 의 합계가 다음 IO 에 더해지는데, 그건
            // 숫자가 조금 크게 나오는 모양이라 눈으로는 못 찾습니다.
            var sum = new double[buckets];
            var sumSq = new double[buckets];

            for (int bi = 0; bi < before.ChannelCount; bi++)
            {
                if (prog != null && (bi & 0x0F) == 0)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("구간별 차이를 세는 중… " + bi + " / " + before.ChannelCount,
                                before.ChannelCount > 0 ? (double)bi / before.ChannelCount : -1);
                }

                Channel bc = before.Channels[bi];
                if (opt.HasFilter && !opt.OnlyNames.Contains(LogDataset.LooseKey(bc.Name))) continue;

                int ai = after.FindChannel(bc.Name);
                if (ai < 0) continue;   // 한쪽에만 있는 IO 는 여기서 다루지 않습니다.

                compared++;
                HeatRow row = BuildRow(before, bi, after, ai, t0, t1, cuts, shift, opt, sum, sumSq);
                if (row.HasDifference) changed++;
                if (row.HasDifference || opt.IncludeUnchanged) res.Rows.Add(row);
            }

            res.ComparedChannels = compared;
            res.ChangedChannels = changed;

            // 많이 벌어진 IO 가 위로 오게.
            //
            // 기준은 칸 폭과 무관한 값만 씁니다. 예전에는 PeakMean(칸들
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
            {
                // 덮어쓰지 않고 뒤에 붙입니다. 칸을 자를 때의 말(짝 없는
                // 단계를 뺐다 …)이 지워지면, 왜 칸이 그것뿐인지 알 수가
                // 없습니다.
                string none = "허용 오차 안에서 차이가 난 IO 가 없습니다.";
                res.Warning = res.Warning.Length > 0 ? res.Warning + " " + none : none;
            }

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
                                        double t0, double t1, HeatBuckets cuts,
                                        double shift, HeatmapOptions opt,
                                        double[] sum, double[] sumSq)
        {
            int buckets = cuts.Count;
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

            // 허용 오차는 IO 마다 다를 수 있습니다. 표본마다 표를 뒤지지 않게
            // 채널 하나당 한 번만 풀어 둡니다.
            double limitAbs = opt.AbsoluteTolerance, limitPct = opt.RelativePercent;
            if (opt.Tolerances != null)
            {
                limitAbs = opt.Tolerances.AbsoluteFor(bc.Name);
                limitPct = opt.Tolerances.PercentFor(bc.Name);
            }

            double[] bt = before.Times, at = after.Times;
            float[] bv = bc.Values, av = ac.Values;
            int an = after.SampleCount;

            // 돌려 쓰는 자리라 먼저 지웁니다 (위 Build 의 주석 참고).
            Array.Clear(sum, 0, buckets);
            Array.Clear(sumSq, 0, buckets);

            int start = before.IndexAtOrBefore(t0);
            if (start < 0) start = 0;
            while (start < before.SampleCount && bt[start] < t0) start++;

            int j = 0;   // 이후 로그에서 지금 보고 있는 자리 (뒤로 가지 않습니다)
            int walk = 0;   // 지금 보고 있는 칸 (앞으로만 밉니다)

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
                    over = ToleranceRule.IsOver(b, a, limitAbs, limitPct);
                }

                // 어느 칸인지는 칸 목록이 압니다. 단계로 자르면 칸 사이에
                // 빈틈(공정이 멈춘 구간)이 있어서, 그 자리의 표본은 어느
                // 칸에도 넣지 않습니다 — 옆 칸으로 밀어 넣으면 그 단계에서
                // 나지 않은 차이가 그 단계에 적힙니다.
                int k = cuts.Walk(t, ref walk);
                if (k < 0) continue;

                HeatCell cell = row.Cells[k];
                cell.Samples++;
                sum[k] += diff;
                sumSq[k] += diff * diff;
                if (diff > cell.Max) cell.Max = (float)diff;

                if (over)
                {
                    cell.OverSamples++;
                    // 가장 크게 벌어진 순간을 붙잡습니다. 값과 퍼센트를
                    // 같은 표본에서 가져와야 둘이 가리키는 순간이
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
