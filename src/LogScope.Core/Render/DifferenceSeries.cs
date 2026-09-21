using System;
using LogScope.Core.Model;

namespace LogScope.Core.Render
{
    /// <summary>
    /// <b>두 로그의 같은 IO 를 견준 차이 곡선.</b> 선 하나입니다.
    ///
    ///   차이(t) = 이후 로그의 값 − 이전 로그의 값
    ///
    /// 같으면 0, 이후가 1 크면 +1, 1 작으면 -1. 두 로그를 나란히 놓고
    /// 눈으로 견주는 대신 <b>벌어진 만큼만</b> 보는 눈금입니다. 6466.3 과
    /// 6466.6 은 값 눈금에서 겹쳐 보이지만 여기서는 +0.3 으로 또렷이 섭니다.
    ///
    /// <b>시간 격자가 서로 다릅니다.</b> 이전 로그가 0.1 초마다, 이후 로그가
    /// 0.25 초마다 찍혔을 수 있습니다. 그래서 한쪽 격자에 맞추지 않고
    /// <b>두 격자를 합쳐</b> 훑습니다 — 한쪽에만 있는 표본에서도 반대쪽 값을
    /// 읽어 견줍니다. 이전 로그 격자만 쓰면 이후 로그에서만 튄 자리를
    /// 통째로 놓칩니다.
    ///
    /// 두 로그의 시각은 "이후 시각 + 밀기 = 이전 시각" 으로 맞춥니다.
    /// 그리는 쪽 · 견주는 쪽과 같은 약속입니다.
    /// </summary>
    public static class DifferenceSeries
    {
        /// <summary>
        /// 상태(문자열) 채널은 값을 빼면 안 됩니다. 상태 번호는 로그마다
        /// 따로 매겨져서 "3 − 1 = 2" 같은 수에 뜻이 없습니다. 이름이 같으면
        /// 0, 다르면 1 로 봅니다 — 대시보드 · 히트맵과 같은 규칙입니다.
        /// </summary>
        public static bool ByName(Channel b, Channel a)
        {
            return (b != null && b.Kind == ChannelKind.State)
                || (a != null && a.Kind == ChannelKind.State);
        }

        /// <summary>
        /// [t0,t1] (이전 로그 시각 기준) 의 차이 곡선을 뽑습니다.
        ///
        /// 한 번 훑으면서 두 가지를 같이 채웁니다.
        ///  - <paramref name="outCols"/> : 픽셀 열마다 차이의 최소/최대.
        ///    표본이 픽셀보다 촘촘할 때 씁니다.
        ///  - <paramref name="outT"/> / <paramref name="outV"/> : 표본 그대로.
        ///    표본이 성길 때 씁니다. 접어서 그리면 표본이 없는 열에서 선이
        ///    끊겨 점만 찍힙니다.
        ///
        /// 돌려주는 값은 표본 개수입니다. <paramref name="maxPoints"/> 를 넘으면
        /// -1 을 돌려주고, 그때는 열(outCols)을 쓰면 됩니다.
        /// </summary>
        public static int Build(LogDataset before, int bi, LogDataset after, int ai,
                                double shift, double t0, double t1,
                                Decimator.Column[] outCols, int columns,
                                double[] outT, float[] outV, int maxPoints)
        {
            for (int i = 0; i < columns; i++) outCols[i] = new Decimator.Column();
            if (!Usable(before, bi, after, ai) || columns <= 0 || !(t1 > t0)) return 0;

            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];
            bool byName = ByName(bc, ac);

            double[] bt = before.Times, at = after.Times;
            float[] bv = bc.Values, av = ac.Values;
            int nb = before.SampleCount, na = after.SampleCount;

            double scale = columns / (t1 - t0);

            // 두 격자를 합쳐 훑습니다. 양쪽 다 시간 순서라 뒤로 가지 않으므로,
            // 값을 읽을 자리를 가리키는 두 손가락이 앞으로만 움직입니다
            // (표본마다 이진 탐색하지 않습니다).
            int i0 = before.IndexAtOrBefore(t0); if (i0 < 0) i0 = 0;
            int j0 = after.IndexAtOrBefore(t0 - shift); if (j0 < 0) j0 = 0;

            int ib = i0, ja = j0;     // 다음에 볼 표본
            int pb = i0, pa = j0;     // 값을 읽을 자리

            int count = 0;
            bool overflow = false;

            int cur = -1;
            float mn = 0f, mx = 0f;
            bool has = false;

            while (true)
            {
                double tb = ib < nb ? bt[ib] : double.PositiveInfinity;
                double ta = ja < na ? at[ja] + shift : double.PositiveInfinity;
                double t = tb < ta ? tb : ta;
                if (double.IsPositiveInfinity(t) || t > t1) break;

                // 같은 시각이면 양쪽 다 넘깁니다. 안 그러면 같은 자리를 두 번 봅니다.
                if (tb <= t) ib++;
                if (ta <= t) ja++;

                while (pb + 1 < nb && bt[pb + 1] <= t) pb++;
                while (pa + 1 < na && at[pa + 1] + shift <= t) pa++;

                double b = ValueAt(bt, bv, nb, pb, t, bc.IsStepped);
                double a = ValueAt(at, av, na, pa, t - shift, ac.IsStepped);

                double d;
                if (byName)
                {
                    string bs = StateText(bc, b), as_ = StateText(ac, a);
                    if (bs == null || as_ == null) continue;
                    d = string.Equals(bs, as_, StringComparison.Ordinal) ? 0.0 : 1.0;
                }
                else
                {
                    if (double.IsNaN(b) || double.IsNaN(a)) continue;
                    d = a - b;
                }

                var value = (float)d;

                if (!overflow)
                {
                    if (count >= maxPoints) { overflow = true; }
                    else { outT[count] = t; outV[count] = value; count++; }
                }

                int c = (int)((t - t0) * scale);
                if (c < 0) c = 0;
                else if (c >= columns) c = columns - 1;

                if (c != cur)
                {
                    if (has) Write(outCols, cur, mn, mx);
                    cur = c; mn = value; mx = value; has = true;
                }
                else
                {
                    if (value < mn) mn = value;
                    else if (value > mx) mx = value;
                }
            }

            if (has) Write(outCols, cur, mn, mx);
            return overflow ? -1 : count;
        }

        private static void Write(Decimator.Column[] cols, int c, float mn, float mx)
        {
            cols[c].Min = mn;
            cols[c].Max = mx;
            cols[c].HasValue = true;
        }

        /// <summary>[t0,t1] 안에서 차이가 오르내린 폭. 세로 눈금을 잡는 데 씁니다.</summary>
        public static bool RangeIn(LogDataset before, int bi, LogDataset after, int ai,
                                   double shift, double t0, double t1,
                                   out double min, out double max)
        {
            min = 0; max = 0;
            if (!Usable(before, bi, after, ai) || !(t1 > t0)) return false;

            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];
            bool byName = ByName(bc, ac);

            double[] bt = before.Times, at = after.Times;
            float[] bv = bc.Values, av = ac.Values;
            int nb = before.SampleCount, na = after.SampleCount;

            int i0 = before.IndexAtOrBefore(t0); if (i0 < 0) i0 = 0;
            int j0 = after.IndexAtOrBefore(t0 - shift); if (j0 < 0) j0 = 0;
            int ib = i0, ja = j0, pb = i0, pa = j0;

            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;

            while (true)
            {
                double tb = ib < nb ? bt[ib] : double.PositiveInfinity;
                double ta = ja < na ? at[ja] + shift : double.PositiveInfinity;
                double t = tb < ta ? tb : ta;
                if (double.IsPositiveInfinity(t) || t > t1) break;
                if (tb <= t) ib++;
                if (ta <= t) ja++;

                while (pb + 1 < nb && bt[pb + 1] <= t) pb++;
                while (pa + 1 < na && at[pa + 1] + shift <= t) pa++;

                double b = ValueAt(bt, bv, nb, pb, t, bc.IsStepped);
                double a = ValueAt(at, av, na, pa, t - shift, ac.IsStepped);

                double d;
                if (byName)
                {
                    string bs = StateText(bc, b), as_ = StateText(ac, a);
                    if (bs == null || as_ == null) continue;
                    d = string.Equals(bs, as_, StringComparison.Ordinal) ? 0.0 : 1.0;
                }
                else
                {
                    if (double.IsNaN(b) || double.IsNaN(a)) continue;
                    d = a - b;
                }

                if (d < lo) lo = d;
                if (d > hi) hi = d;
            }

            if (lo > hi) return false;
            min = lo; max = hi;
            return true;
        }

        /// <summary>한 시각의 차이. 커서 옆에 적는 데 씁니다. 못 읽으면 NaN.</summary>
        public static double At(LogDataset before, int bi, LogDataset after, int ai,
                                double shift, double t)
        {
            if (!Usable(before, bi, after, ai)) return double.NaN;

            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];

            double b = before.SampleAt(bi, t);
            double a = after.SampleAt(ai, t - shift);

            if (ByName(bc, ac))
            {
                string bs = StateText(bc, b), as_ = StateText(ac, a);
                if (bs == null || as_ == null) return double.NaN;
                return string.Equals(bs, as_, StringComparison.Ordinal) ? 0.0 : 1.0;
            }
            if (double.IsNaN(b) || double.IsNaN(a)) return double.NaN;
            return a - b;
        }

        private static bool Usable(LogDataset before, int bi, LogDataset after, int ai)
        {
            return before != null && after != null
                && bi >= 0 && bi < before.ChannelCount
                && ai >= 0 && ai < after.ChannelCount
                && before.SampleCount > 0 && after.SampleCount > 0;
        }

        /// <summary>
        /// 손가락 p 가 가리키는 자리에서 t 의 값. 기록 구간 밖이면 NaN —
        /// 없는 값을 지어내면 로그가 짧은 쪽 끝에서 가짜 차이가 생깁니다.
        /// </summary>
        private static double ValueAt(double[] times, float[] v, int n, int p, double t, bool stepped)
        {
            if (n == 0 || t < times[0] || t > times[n - 1]) return double.NaN;
            if (p < 0) p = 0;
            if (p >= n - 1) return v[n - 1];

            float a = v[p];
            if (stepped) return a;

            float b = v[p + 1];
            if (float.IsNaN(a)) return double.NaN;
            if (float.IsNaN(b)) return a;

            double t0 = times[p], t1 = times[p + 1];
            if (t1 <= t0) return a;
            return a + (b - a) * (t - t0) / (t1 - t0);
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
    }
}
