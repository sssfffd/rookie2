using System.Collections.Generic;
using System;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Heatmap
{
    // 1.50: 히트맵 계산 (WPF 없음) — HeatmapBuilder · HeatTypes (칸 · 줄 · 옵션 · 결과) · HeatBuckets (칸 자르기) · HeatLayout (그룹 · 검색 배치).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>
    /// 두 로그를 칸마다 견주어 히트맵 표를 만듭니다.
    ///
    /// 칸을 어떻게 자를지는 <see cref="HeatBuckets"/> 가 압니다 — 시간
    /// 폭으로 자르거나(기본 1 분), <b>단계 하나를 칸 하나로</b> 자릅니다.
    /// 여기서는 "어느 칸이냐" 를 칸 목록에게 묻기만 합니다.
    ///
    /// 칸과 줄의 뜻(무엇을 차이로 보는지, 줄 차례를 무엇으로 매기는지)은
    /// 같은 파일 아래 HeatTypes 덩어리에 적혀 있습니다.
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

            // 칸을 어떻게 자를지 먼저 정합니다 — 단계로, 아니면 시간 폭으로.
            //
            // 단계로 자를 때는 칸을 <b>이전 로그의 단계 구간</b>으로 잡고,
            // 겹치는 구간을 밀기 값 하나로 미리 자르지 않습니다. 칸마다
            // 밀기 값이 따로라서, 하나로 자르면 그 하나에 안 맞는 단계가
            // 통째로 잘려 나갑니다.
            HeatBuckets cuts = null;
            if (opt.ByStep)
            {
                string why;
                cuts = HeatBuckets.Steps(opt.Steps, before, before.TimeStart, before.TimeEnd, out why);
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

            double t0, t1, span;
            int buckets;

            if (cuts != null)
            {
                // 칸들이 덮는 구간이 곧 보는 구간입니다.
                t0 = cuts.From(0);
                t1 = cuts.To(cuts.Count - 1);
                span = double.NaN;
                buckets = cuts.Count;
            }
            else
            {
                t0 = Math.Max(before.TimeStart, after.TimeStart + shift);
                t1 = Math.Min(before.TimeEnd, after.TimeEnd + shift);
                if (!(t1 > t0))
                {
                    res.Warning = "두 로그의 시간 구간이 겹치지 않습니다.";
                    return res;
                }

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

            // ---- 칸 하나씩 ----
            //
            // 전에는 표본을 쭉 훑으면서 "이 표본은 몇 번 칸" 을 물었습니다.
            // 그러면 밀기 값이 하나일 때만 맞습니다. <b>칸마다 밀기 값이
            // 다르면</b> (단계로 자른 경우) 이후 로그를 읽는 자리가 칸이
            // 바뀔 때 뒤로 갈 수 있어서, 앞으로만 미는 자리표로는 못 찾습니다.
            //
            // 그래서 칸 단위로 돕니다. 표본은 여전히 한 번씩만 봅니다
            // (칸이 차례대로이고 겹치지 않으므로 i 는 앞으로만 갑니다).
            // 이후 로그의 자리는 <b>밀기 값이 바뀔 때만</b> 다시 찾습니다 —
            // 시간 폭으로 자르면 한 번뿐입니다.
            int bn = before.SampleCount;
            int i = before.IndexAtOrBefore(t0);
            if (i < 0) i = 0;

            int j = 0;              // 이후 로그에서 보고 있는 자리
            double curShift = double.NaN;

            for (int k = 0; k < buckets; k++)
            {
                double from = cuts.From(k), to = cuts.To(k);
                double shiftK = cuts.ShiftOf(k, shift);

                while (i < bn && bt[i] < from) i++;   // 칸 사이의 빈틈은 건너뜁니다
                if (i >= bn) break;
                if (bt[i] >= to) continue;            // 이 칸에는 표본이 없습니다

                if (!shiftK.Equals(curShift))
                {
                    int seek = after.IndexAtOrBefore(bt[i] - shiftK);
                    j = seek < 0 ? 0 : seek;
                    curShift = shiftK;
                }

                for (; i < bn && bt[i] < to; i++)
                {
                    double t = bt[i];
                    double ta = t - shiftK;

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

    // ======================================================================
    //  HeatTypes.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    // 히트맵이 다루는 것들 — 칸 하나(HeatCell), 가로 한 줄(HeatRow),
    // 무엇을 어떻게 셀지(HeatmapOptions), 센 결과(HeatmapResult).
    //
    // 세는 코드는 이 파일 위쪽 HeatmapBuilder 입니다 (1.50 에 한 파일로 합침).
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

        /// <summary>
        /// IO 별 허용 오차. 있으면 이것이 이깁니다. 대시보드·그래프와 같은
        /// 표를 씁니다 — 화면마다 "차이 난 IO" 가 달라지면 안 됩니다.
        /// </summary>
        public ToleranceTable Tolerances;

        /// <summary>칸이 너무 많아지지 않게 하는 상한.</summary>
        public int MaxBuckets = 4000;

        /// <summary>차이가 없는 IO 도 줄로 넣을지.</summary>
        public bool IncludeUnchanged;

        /// <summary>이후 로그의 시간을 이만큼 밀어 맞춥니다.</summary>
        public double Shift;

        /// <summary>
        /// <b>가로를 단계로 자를지.</b> 참이면 단계 하나가 칸 하나입니다
        /// (시간 폭 대신). 단계가 없으면 시간으로 자르고 그렇다고 말해
        /// 줍니다 — 조용히 시간으로 돌아가면 "단계로 봤다" 고 착각합니다.
        /// </summary>
        public bool ByStep;

        /// <summary>
        /// 짝지어 둔 단계. <see cref="ByStep"/> 일 때만 씁니다.
        /// 여기서 짓지 않고 받는 이유: 그래프 화면도 같은 목록을 쓰는데,
        /// 각자 지으면 두 화면의 단계 번호가 어긋날 수 있습니다.
        /// </summary>
        public StepPairList Steps;

        /// <summary>
        /// 이 이름들만 봅니다. 비어 있으면 모두 봅니다.
        ///
        /// 메인 화면에서 그룹을 눌러 넘어왔을 때, 그 그룹의 IO 만 남깁니다.
        /// 넘어와 놓고 수백 줄 가운데서 다시 찾아야 하면 넘어온 뜻이 없습니다.
        ///
        /// 이름은 <b>느슨하게</b> 맞춥니다 (공백·대소문자·_-. 무시). 그룹은
        /// 사람이 손으로 담는 것이라 이전 로그와 이후 로그의 이름이 살짝
        /// 다를 수 있는데, 그 규칙은 채널을 찾을 때 이미 쓰고 있습니다.
        /// </summary>
        public HashSet<string> OnlyNames;

        public bool HasFilter { get { return OnlyNames != null && OnlyNames.Count > 0; } }

        /// <summary>이름 목록을 느슨한 열쇠로 바꿔 담습니다.</summary>
        public static HashSet<string> NameFilter(IEnumerable<string> names)
        {
            if (names == null) return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                set.Add(LogDataset.LooseKey(n));
            }
            return set.Count > 0 ? set : null;
        }

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

        /// <summary>
        /// 칸의 경계. 가로축 글자도 여기서 나옵니다.
        /// <b>칸 번호를 셈하는 곳은 여기 하나입니다</b> — 단계로 자르면
        /// 칸 폭이 다 다르고 칸 사이에 빈틈이 있어서, 셈하는 식을 여러
        /// 곳에 두면 한 곳만 고쳐도 조용히 어긋납니다.
        /// </summary>
        public HeatBuckets Buckets;

        /// <summary>단계로 잘렸는지. 화면이 가로축 이름을 바꿔 적습니다.</summary>
        public bool ByStep { get { return Buckets != null && Buckets.ByStep; } }

        public string Warning = string.Empty;

        public int ComparedChannels;
        public int ChangedChannels;

        /// <summary>
        /// 칸이 시작하는 시각. <b>칸 목록에게 묻습니다</b> — 단계로 자르면
        /// 폭이 다 달라서 "시작 + 번호 x 폭" 이 맞지 않습니다.
        /// </summary>
        public double BucketStart(int index)
        {
            if (Buckets != null && index >= 0 && index < Buckets.Count) return Buckets.From(index);
            return Start + index * BucketSpan;
        }

        /// <summary>
        /// 칸이 끝나는 시각. <b>"시작 + 폭" 으로 셈하면 안 됩니다</b> —
        /// 단계로 자르면 폭이 다 다르고 BucketSpan 은 NaN 입니다.
        /// </summary>
        public double BucketEnd(int index)
        {
            if (Buckets != null && index >= 0 && index < Buckets.Count) return Buckets.To(index);
            return Start + (index + 1) * BucketSpan;
        }

        /// <summary>칸 하나의 가로축 글자. 단계면 단계 이름입니다.</summary>
        public string BucketText(int index)
        {
            if (Buckets == null) return string.Empty;
            return Buckets.Label(index, TimeReference);
        }
    }

    // ======================================================================
    //  HeatBuckets.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 히트맵의 <b>가로 칸</b>들. 칸 하나가 세로줄 하나입니다.
    ///
    /// 자르는 길이 둘입니다.
    ///   <see cref="Uniform"/> : 일정한 시간 폭 (기본 1 분)
    ///   <see cref="Steps"/>   : <b>단계 하나가 칸 하나</b>
    ///
    /// 왜 따로 뺐나. 전에는 칸 번호를 <c>(t - t0) / 폭</c> 으로 바로
    /// 셈했습니다. 그 셈은 "칸은 다 같은 폭이고 빈틈 없이 붙어 있다" 를
    /// 전제하는데, 단계는 폭이 다 다르고 <b>단계 사이에 빈틈이 있습니다</b>
    /// (공정이 멈춘 구간). 그 전제를 코드 여러 곳에 흩어 두면 단계로
    /// 바꾸는 날 한 곳만 고쳐도 조용히 틀립니다 — 칸이 밀려도 그림은
    /// 멀쩡해 보입니다.
    ///
    /// 그래서 칸의 경계는 여기 한 곳에만 있고, 밖에서는 <see cref="Find"/>
    /// 로 묻습니다.
    /// </summary>
    public sealed class HeatBuckets
    {
        private readonly double[] _from;
        private readonly double[] _to;
        private readonly string[] _labels;   // null 이면 시각으로 적습니다

        /// <summary>
        /// 칸마다의 밀기 값. null 이면 칸이 다 같은 값을 씁니다.
        ///
        /// <b>단계로 자르면 칸마다 다릅니다.</b> 단계마다 두 로그가 어긋난
        /// 만큼이 다르기 때문입니다 — 공정이 중간에 조금 늘어지면 뒤 단계는
        /// 앞 단계보다 더 밀립니다. 칸을 단계로 잘라 놓고 밀기 값은 하나만
        /// 쓰면, <b>그 하나로 맞은 단계만 제대로 견주고 나머지 칸은 어긋난
        /// 채 견줍니다</b> — 차이가 없는데 있다고 나오거나, 있는데 묻힙니다.
        /// </summary>
        private readonly double[] _shifts;

        /// <summary>단계로 자른 것인지. 화면의 가로축 글자가 달라집니다.</summary>
        public readonly bool ByStep;

        /// <summary>일정 폭으로 잘랐을 때의 그 폭. 단계로 자르면 NaN.</summary>
        public readonly double Span;

        private HeatBuckets(double[] from, double[] to, string[] labels, double[] shifts,
                            bool byStep, double span)
        {
            _from = from;
            _to = to;
            _labels = labels;
            _shifts = shifts;
            ByStep = byStep;
            Span = span;
        }

        /// <summary>칸마다 밀기 값이 따로인지.</summary>
        public bool HasShifts { get { return _shifts != null; } }

        /// <summary>
        /// 그 칸에서 이후 로그를 미는 값. 칸마다의 값이 없으면
        /// <paramref name="fallback"/> 을 돌려줍니다.
        /// </summary>
        public double ShiftOf(int i, double fallback)
        {
            if (_shifts == null || i < 0 || i >= _shifts.Length) return fallback;
            return _shifts[i];
        }

        public int Count { get { return _from.Length; } }

        /// <summary>칸이 시작하는 시각 (이전 로그 기준).</summary>
        public double From(int i) { return _from[i]; }

        /// <summary>칸이 끝나는 시각. 이 시각은 칸에 <b>안</b> 들어갑니다.</summary>
        public double To(int i) { return _to[i]; }

        /// <summary>
        /// 그 시각이 든 칸. 어느 칸에도 안 들면 -1 입니다.
        ///
        /// -1 이 나오는 것은 <b>단계 사이의 빈틈</b>뿐입니다. 일정 폭으로
        /// 자르면 칸이 빈틈 없이 붙어 있어서 -1 이 없습니다.
        /// </summary>
        public int Find(double t)
        {
            int lo = 0, hi = _from.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (t < _from[mid]) hi = mid - 1;
                else if (t >= _to[mid]) lo = mid + 1;
                else return mid;
            }
            return -1;
        }

        /// <summary>
        /// 시각이 커지는 순서로 훑을 때 쓰는 빠른 길. <paramref name="k"/> 를
        /// 앞으로만 밀면서 찾습니다 (표본마다 이진 탐색을 하지 않게).
        /// 빈틈에 든 시각은 -1 을 돌려주고 k 는 그대로 둡니다.
        /// </summary>
        public int Walk(double t, ref int k)
        {
            int n = _from.Length;
            if (k < 0) k = 0;
            while (k < n && t >= _to[k]) k++;
            if (k >= n) return -1;
            return t >= _from[k] ? k : -1;
        }

        /// <summary>
        /// 가로축에 적을 글자. 단계로 자르면 단계 이름이고, 그게 아니면
        /// 칸이 시작하는 시각입니다.
        /// </summary>
        public string Label(int i, LogDataset timeRef)
        {
            if (i < 0 || i >= _from.Length) return string.Empty;
            if (_labels != null) return _labels[i];
            return timeRef != null ? timeRef.FormatTime(_from[i]) : NumberText.Plain(_from[i]);
        }

        // ================= 자르는 길 =================

        /// <summary>일정한 시간 폭으로 자릅니다. 칸이 빈틈 없이 붙습니다.</summary>
        public static HeatBuckets Uniform(double t0, double t1, double span)
        {
            if (!(span > 0)) span = Math.Max((t1 - t0) / 60.0, 1e-9);
            int n = (int)Math.Ceiling((t1 - t0) / span);
            if (n < 1) n = 1;

            var from = new double[n];
            var to = new double[n];
            for (int i = 0; i < n; i++)
            {
                from[i] = t0 + i * span;
                // 마지막 칸은 끝 시각까지 덮습니다. 나눗셈이 딱 맞지 않으면
                // 마지막 표본 몇 개가 어느 칸에도 안 들어갑니다.
                to[i] = i == n - 1 ? Math.Max(t1 + Math.Abs(t1) * 1e-12 + 1e-9, from[i] + span)
                                   : t0 + (i + 1) * span;
            }
            return new HeatBuckets(from, to, null, null, false, span);
        }

        /// <summary>
        /// <b>단계 하나를 칸 하나로</b> 자릅니다. 가로가 시간 대신 공정
        /// 차례가 됩니다.
        ///
        /// 칸의 경계는 <b>이전 로그</b>의 단계 구간입니다. 히트맵은 이전
        /// 로그의 시간을 바탕으로 그려지고 이후 로그를 밀어서 견주기
        /// 때문입니다 — 이후 로그의 단계 경계로 자르면 밀기 값을 두 번
        /// 거친 자리가 되어 칸이 어긋납니다.
        ///
        /// 양쪽에 다 있는 단계만 칸이 됩니다. 한쪽에만 있는 단계는 견줄
        /// 것이 없으니 칸을 만들어도 비어 있고, 빈 칸이 줄줄이 있으면
        /// "차이가 없다" 로 읽힙니다. 몇 개를 뺐는지는 말해 줍니다.
        /// </summary>
        public static HeatBuckets Steps(StepPairList pairs, LogDataset before,
                                        double t0, double t1, out string warning)
        {
            warning = string.Empty;
            if (pairs == null || pairs.IsEmpty || before == null) return null;

            var from = new List<double>();
            var to = new List<double>();
            var labels = new List<string>();
            var shifts = new List<double>();

            int outside = 0, oneSided = 0;

            for (int i = 0; i < pairs.Count; i++)
            {
                StepPair p = pairs[i];
                if (!p.HasBefore) { oneSided++; continue; }
                if (!p.HasBoth) { oneSided++; continue; }

                double a = p.BeforeStart, b = p.BeforeEnd;
                if (double.IsNaN(a) || double.IsNaN(b)) continue;

                // 견주는 구간 밖은 잘라 들입니다. 통째로 밖이면 뺍니다.
                if (b < t0 || a > t1) { outside++; continue; }
                if (a < t0) a = t0;
                if (b > t1) b = t1;

                // 끝 시각은 칸에 안 들어가므로 마지막 표본이 빠지지 않게
                // 아주 조금 넘겨 둡니다.
                double end = b + Math.Max(Math.Abs(b) * 1e-12, 1e-9);
                if (end <= a) end = a + 1e-9;

                from.Add(a);
                to.Add(end);
                labels.Add(p.Label);

                // 그 단계의 밀기 값. 칸마다 다릅니다 — 이것이 단계로
                // 자르는 뜻의 절반입니다 (나머지 절반은 칸 자리).
                shifts.Add(p.Shift);
            }

            if (from.Count == 0)
            {
                warning = "견주는 구간 안에 양쪽에 다 있는 단계가 없습니다. 가로를 시간으로 둡니다.";
                return null;
            }

            if (oneSided > 0)
                warning = "짝이 없는 단계 " + oneSided + " 개는 칸으로 만들지 않았습니다.";
            if (outside > 0)
                warning = (warning.Length > 0 ? warning + " " : string.Empty)
                        + "견주는 구간 밖의 단계 " + outside + " 개도 뺐습니다.";

            return new HeatBuckets(from.ToArray(), to.ToArray(), labels.ToArray(),
                                   shifts.ToArray(), true, double.NaN);
        }
    }

    // ======================================================================
    //  HeatLayout.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 히트맵 왼쪽 줄 하나. 그룹 이름 줄이거나 IO 줄입니다.
    /// </summary>
    public sealed class HeatLine
    {
        /// <summary>참이면 그룹 이름 줄, 거짓이면 IO 줄.</summary>
        public bool IsGroup { get; private set; }

        /// <summary>그룹 이름 (IO 줄이면 그 IO 가 든 그룹 이름).</summary>
        public string Group { get; private set; }

        /// <summary>그룹 줄: 그 그룹에서 남은 IO 수 (검색 뒤). IO 줄: 0.</summary>
        public int Count { get; private set; }

        /// <summary>그룹 줄: 접혀 있는지.</summary>
        public bool Collapsed { get; private set; }

        /// <summary>IO 줄: 결과의 줄 (HeatmapResult.Rows 의 것). 그룹 줄이면 null.</summary>
        public HeatRow Row { get; private set; }

        public static HeatLine Head(string group, int count, bool collapsed)
        {
            var l = new HeatLine();
            l.IsGroup = true; l.Group = group; l.Count = count; l.Collapsed = collapsed;
            return l;
        }

        public static HeatLine Io(string group, HeatRow row)
        {
            var l = new HeatLine();
            l.Group = group; l.Row = row;
            return l;
        }
    }

    /// <summary>
    /// 히트맵의 줄을 <b>그래프 화면과 같은 그룹</b>으로 나누고, 검색으로
    /// 거릅니다 (1.07).
    ///
    /// 나누는 규칙은 그래프 화면의 IO 목록(ChannelListVm.RebuildRows)과
    /// 같습니다 — 다르면 같은 IO 가 두 화면에서 다른 그룹에 보입니다.
    ///
    ///   - 그룹은 설정에 적힌 차례대로
    ///   - IO 가 그룹 둘에 들어 있으면 <b>앞 그룹</b>에만
    ///   - 어느 그룹에도 없는 IO 는 맨 아래 <see cref="Ungrouped"/>
    ///   - 검색은 이름에 그 글자가 들어 있으면 (대소문자 안 가림)
    ///
    /// 다른 점 하나: <b>남은 IO 가 없는 그룹은 이름 줄도 감춥니다</b> (검색
    /// 안 해도). 히트맵에는 차이 난 IO 만 오므로, 아무 일 없던 그룹의 빈
    /// 이름 줄이 수십 개 깔리면 정작 볼 줄이 아래로 밀립니다.
    ///
    /// 그룹 안의 차례는 <b>히트맵 차례 그대로</b>(많이 벌어진 것이 위)입니다.
    /// 그룹 목록의 차례로 바꾸면 그룹 안에서 무엇이 제일 나쁜지가 안 보입니다.
    ///
    /// 계산은 다시 하지 않습니다 — 이미 센 줄을 늘어놓는 것뿐이라, 검색을
    /// 고칠 때마다 바로 바뀝니다.
    /// </summary>
    public static class HeatLayout
    {
        public const string Ungrouped = "그룹에 없는 IO";

        public static List<HeatLine> Build(IList<HeatRow> rows, IList<GroupDef> groups,
                                           string search, ICollection<string> collapsed)
        {
            var lines = new List<HeatLine>();
            if (rows == null || rows.Count == 0) return lines;


            // IO 이름 → 처음 든 그룹 번호. 뒤 그룹에 또 있어도 앞 그룹이 이깁니다.
            var groupOf = new Dictionary<string, int>(StringComparer.Ordinal);
            int groupCount = groups == null ? 0 : groups.Count;
            for (int g = 0; g < groupCount; g++)
            {
                GroupDef def = groups[g];
                if (def == null || def.Members == null) continue;
                foreach (string name in def.Members)
                    if (name != null && !groupOf.ContainsKey(name)) groupOf.Add(name, g);
            }

            // 그룹마다 남은 줄 (히트맵 차례 그대로). 마지막 칸이 그룹에 없는 IO.
            var buckets = new List<HeatRow>[groupCount + 1];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<HeatRow>();

            for (int i = 0; i < rows.Count; i++)
            {
                HeatRow row = rows[i];
                if (row == null) continue;
                if (!NameSearch.Matches(row.Name, search)) continue;

                int g;
                buckets[groupOf.TryGetValue(row.Name, out g) ? g : groupCount].Add(row);
            }

            for (int g = 0; g <= groupCount; g++)
            {
                List<HeatRow> members = buckets[g];
                if (members.Count == 0) continue;

                string name = g < groupCount ? groups[g].Name : Ungrouped;
                bool shut = collapsed != null && collapsed.Contains(name);
                lines.Add(HeatLine.Head(name, members.Count, shut));
                if (shut) continue;
                for (int i = 0; i < members.Count; i++) lines.Add(HeatLine.Io(name, members[i]));
            }
            return lines;
        }

        /// <summary>남은 IO 줄 수 (그룹 줄은 빼고, 접힌 그룹의 IO 는 셉니다).</summary>
        public static int IoCount(List<HeatLine> lines)
        {
            int n = 0;
            if (lines == null) return 0;
            for (int i = 0; i < lines.Count; i++) if (lines[i].IsGroup) n += lines[i].Count;
            return n;
        }
    }
}
