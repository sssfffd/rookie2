using System.Collections.Generic;
using System.Globalization;
using System;
using LogScope.Common;

namespace LogScope.Analysis1.LogFile
{
    // 1.50: 시간축 — TimeAxis (만들기) · TimeKind · Orientation · TimeText (시각 글자).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>
    /// 시간 칸들을 실제로 쓸 수 있는 시간축으로 다듬습니다.
    /// 로그마다 시간 칸을 적는 방식이 달라서, 여기서 다음 네 가지를 처리합니다.
    ///
    ///  1) 시간이 바뀔 때만 적혀 있고 중간이 비어 있는 경우
    ///     -> 앞뒤 값 사이를 고르게 채웁니다.
    ///  2) 같은 시각이 여러 줄에 반복되는 경우 (분 단위 표기에 초 단위 기록)
    ///     -> 다음 시각까지 고르게 펴서 배치합니다. 그대로 두면 몇 분치가
    ///        화면의 한 점에 겹쳐 보이지 않게 됩니다.
    ///  3) 자정을 넘겨 시각이 되감기는 경우 -> 하루씩 더해 이어 붙입니다.
    ///  4) 시각이 전부 같거나 하나도 못 읽은 경우 -> 샘플 번호를 시간축으로 씁니다.
    ///
    /// 값 자체는 손대지 않습니다. 0 이 몇 분 동안 이어지는 구간도 줄 수 그대로
    /// 남고, 시간축만 그 구간에 걸쳐 펴집니다.
    /// </summary>
    public static class TimeAxis
    {
        public sealed class Result
        {
            public double[] Times;
            public TimeKind Kind;
            public string Unit = string.Empty;
            public List<string> Notes = new List<string>();
        }

        public static Result Build(double[] raw, bool[] has, int n, TimeKind kind, string unit)
        {
            var res = new Result();
            res.Kind = kind;
            res.Unit = unit ?? string.Empty;

            if (n <= 0) { res.Times = new double[0]; return res; }

            int known = 0, firstKnown = -1, lastKnown = -1;
            for (int i = 0; i < n; i++)
            {
                if (!has[i]) continue;
                known++;
                if (firstKnown < 0) firstKnown = i;
                lastKnown = i;
            }

            if (kind == TimeKind.Index || known == 0)
            {
                res.Times = Index(n);
                res.Kind = TimeKind.Index;
                res.Unit = string.Empty;
                if (known == 0) res.Notes.Add("시간으로 읽을 수 있는 칸이 없어 샘플 번호를 시간축으로 씁니다.");
                return res;
            }

            double[] t = new double[n];
            Array.Copy(raw, t, n);

            if (kind == TimeKind.ClockMs && UnwrapMidnight(t, has, n))
                res.Notes.Add("자정을 넘어가는 지점이 있어 시각을 이어 붙였습니다.");

            int filled = FillGaps(t, has, n, firstKnown, lastKnown);
            if (filled > 0)
                res.Notes.Add("시간이 비어 있는 칸 " + filled + " 개를 앞뒤 시각 사이로 채웠습니다.");

            // 펴기 전에 먼저 봅니다. 시각이 전부 같은 로그를 펴 버리면
            // 없는 시간 간격을 지어내는 셈이 됩니다. 그런 로그는 샘플 번호를
            // 시간축으로 써야 합니다.
            double lo = t[0], hi = t[0];
            for (int i = 1; i < n; i++) { if (t[i] < lo) lo = t[i]; if (t[i] > hi) hi = t[i]; }
            if (!(hi > lo))
            {
                res.Times = Index(n);
                res.Kind = TimeKind.Index;
                res.Unit = string.Empty;
                res.Notes.Add("시각이 모두 같아 샘플 번호를 시간축으로 씁니다.");
                return res;
            }

            int spread = SpreadRepeats(t, n);
            if (spread > 0)
                res.Notes.Add("같은 시각이 반복되는 표본 " + spread + " 개를 다음 시각까지 고르게 폈습니다.");

            lo = t[0]; hi = t[0];
            for (int i = 1; i < n; i++) { if (t[i] < lo) lo = t[i]; if (t[i] > hi) hi = t[i]; }

            if (MakeIncreasing(t, n, hi - lo))
                res.Notes.Add("시각이 거꾸로 가는 지점이 있어 순서대로 바로잡았습니다.");

            res.Times = t;
            return res;
        }

        private static double[] Index(int n)
        {
            double[] t = new double[n];
            for (int i = 0; i < n; i++) t[i] = i;
            return t;
        }

        /// <summary>자정을 넘어 00:00 으로 되감긴 지점마다 하루(ms)를 더합니다.</summary>
        private static bool UnwrapMidnight(double[] t, bool[] has, int n)
        {
            const double Day = 86400000.0;
            double offset = 0, prev = double.NaN;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                if (!has[i]) continue;
                double v = t[i];
                if (!double.IsNaN(prev) && v + offset < prev - 1000.0)
                {
                    offset += Day;
                    any = true;
                }
                t[i] = v + offset;
                prev = t[i];
            }
            return any;
        }

        /// <summary>
        /// 빈 시간 칸을 앞뒤 값 사이로 고르게 채웁니다.
        /// 앞쪽/뒤쪽 끝은 가장 가까운 간격을 그대로 이어서 늘립니다.
        /// </summary>
        private static int FillGaps(double[] t, bool[] has, int n, int firstKnown, int lastKnown)
        {
            int filled = 0;

            int a = firstKnown;
            while (a < lastKnown)
            {
                int b = a + 1;
                while (b <= lastKnown && !has[b]) b++;
                if (b > lastKnown) break;
                int gap = b - a;
                if (gap > 1)
                {
                    double ta = t[a], tb = t[b];
                    for (int i = a + 1; i < b; i++)
                    {
                        t[i] = ta + (tb - ta) * (i - a) / gap;
                        filled++;
                    }
                }
                a = b;
            }

            double step = EstimateStep(t, has, n, firstKnown, lastKnown);

            for (int i = firstKnown - 1; i >= 0; i--) { t[i] = t[i + 1] - step; filled++; }
            for (int i = lastKnown + 1; i < n; i++) { t[i] = t[i - 1] + step; filled++; }
            return filled;
        }

        private static double EstimateStep(double[] t, bool[] has, int n, int firstKnown, int lastKnown)
        {
            if (lastKnown > firstKnown)
            {
                double span = t[lastKnown] - t[firstKnown];
                int count = lastKnown - firstKnown;
                if (count > 0 && span > 0) return span / count;
            }
            return 1.0;
        }

        /// <summary>
        /// 같은 시각이 이어지는 구간을 다음 시각까지 고르게 폅니다.
        /// 마지막 구간은 바로 앞 구간의 간격을 씁니다.
        /// </summary>
        private static int SpreadRepeats(double[] t, int n)
        {
            int moved = 0;
            double lastStep = 0;

            int i = 0;
            while (i < n)
            {
                int j = i;
                while (j + 1 < n && t[j + 1] == t[i]) j++;
                int len = j - i + 1;

                if (len > 1)
                {
                    double start = t[i];
                    double next;
                    if (j + 1 < n) next = t[j + 1];
                    else if (lastStep > 0) next = start + lastStep * len;
                    else next = start + len;

                    double span = next - start;
                    if (span > 0)
                    {
                        for (int m = 1; m < len; m++)
                        {
                            t[i + m] = start + span * m / len;
                            moved++;
                        }
                    }
                }

                if (j + 1 < n)
                {
                    double d = t[j + 1] - t[j];
                    if (d > 0) lastStep = d;
                }
                i = j + 1;
            }
            return moved;
        }

        /// <summary>순서가 뒤집힌 곳을 아주 작은 간격으로 밀어 올립니다.</summary>
        private static bool MakeIncreasing(double[] t, int n, double span)
        {
            double eps = span > 0 ? span / (n * 1024.0 + 1.0) : 1e-9;
            if (eps <= 0) eps = 1e-9;
            bool changed = false;
            for (int i = 1; i < n; i++)
            {
                if (t[i] <= t[i - 1])
                {
                    t[i] = t[i - 1] + eps;
                    changed = true;
                }
            }
            return changed;
        }
    }

    // ======================================================================
    //  TimeKind.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>시간축 값이 무엇을 뜻하는지. 눈금 글자를 어떻게 쓸지 정한다.</summary>
    public enum TimeKind
    {
        /// <summary>시간을 읽어내지 못해 샘플 번호를 쓴다.</summary>
        Index = 0,
        /// <summary>단위 없는 숫자. TimeUnit 에 단위 글자가 있을 수 있다.</summary>
        Number = 1,
        /// <summary>자정 기준 경과 밀리초 (HH:MM:SS.mmm).</summary>
        ClockMs = 2,
        /// <summary>1970-01-01 기준 밀리초.</summary>
        DateMs = 3,
    }

    /// <summary>시트에서 IO 가 행으로 놓였는지 열로 놓였는지.</summary>
    public enum Orientation
    {
        /// <summary>배치를 자동으로 찾는다.</summary>
        Auto = 0,
        /// <summary>각 행이 IO 채널, 한 행이 시간축.</summary>
        Rows = 1,
        /// <summary>각 열이 IO 채널, 한 열이 시간축.</summary>
        Cols = 2,
    }

    // ======================================================================
    //  TimeText.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 시간을 글자로 바꾸는 규칙. <see cref="LogDataset.FormatTime"/> 이
    /// 쓰고, <b>경과 시간</b>을 적어야 하는 쪽(단계 시작을 0 으로 놓은
    /// 그래프 가로축)도 씁니다.
    ///
    /// 따로 뺀 까닭: 경과 시간을 그 로그의 시간 모양으로 그대로 적으면
    /// 날짜 로그에서 <b>1970 년</b>이 찍힙니다. 5 초 지났다는 말이
    /// "1970-01-01 00:00:05" 로 나오는데, 그게 날짜로 읽히면 로그를
    /// 잘못 읽은 줄 알게 됩니다.
    /// </summary>
    public static class TimeText
    {
        /// <summary>밀리초를 시:분:초.밀리 로. 음수면 앞에 "-" 가 붙습니다.</summary>
        public static string Clock(double ms)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms)) return "-";

            bool neg = ms < 0;
            if (neg) ms = -ms;

            long total = (long)Math.Floor(ms);
            int msec = (int)(total % 1000);
            long sec = total / 1000;
            int s = (int)(sec % 60);
            long min = sec / 60;
            int m = (int)(min % 60);
            long h = min / 60;

            string text = msec != 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}.{3:000}", h, m, s, msec)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", h, m, s);
            return neg ? "-" + text : text;
        }

        /// <summary>
        /// 어떤 시각에서부터 센 <b>경과 시간</b>. 날짜 로그도 날짜가 아니라
        /// 걸린 시간으로 적습니다.
        ///
        /// 부호를 적습니다. 0 자리보다 앞은 "-" 로 나오는데, 그게 안 보이면
        /// 단계가 시작하기 전의 구간을 단계 안으로 읽습니다.
        /// </summary>
        public static string Elapsed(LogDataset ds, double delta)
        {
            if (double.IsNaN(delta)) return "-";

            TimeKind kind = ds != null ? ds.TimeKind : TimeKind.Number;
            switch (kind)
            {
                case TimeKind.ClockMs:
                case TimeKind.DateMs:
                    // 둘 다 밀리초입니다. 날짜 로그여도 경과 시간은 시계로.
                    return (delta > 0 ? "+" : string.Empty) + Clock(delta);

                case TimeKind.Index:
                    return Sign(delta) + Math.Abs(Math.Round(delta)).ToString("0", CultureInfo.InvariantCulture)
                         + "표본";

                default:
                    {
                        string u = ds != null && ds.TimeUnit.Length > 0 ? " " + ds.TimeUnit : string.Empty;
                        return Sign(delta) + NumberText.Plain(Math.Abs(delta)) + u;
                    }
            }
        }

        private static string Sign(double v)
        {
            if (v > 0) return "+";
            return v < 0 ? "-" : string.Empty;
        }
    }
}
