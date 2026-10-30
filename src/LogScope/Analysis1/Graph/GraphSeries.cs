using System;
using LogScope.Analysis1.LogFile;

namespace LogScope.Analysis1.Graph
{
    // 1.50: 그래프에 그릴 값 계산 (WPF 없음) — Decimator (픽셀 열로 줄이기) · DifferenceSeries (차이 곡선) · TraceColors (선 색 규칙).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>
    /// 그리기 전에 표본을 픽셀 열 수만큼 접습니다.
    ///
    /// 표본이 픽셀보다 촘촘하면 한 픽셀 폭에 수천 번 선을 긋게 됩니다.
    /// 화면에 보이는 것은 그 구간의 위아래 끝뿐이므로, 열마다 최소/최대만
    /// 뽑아 세로 막대 하나로 그립니다. 표본 5 만 개짜리를 1000 픽셀에
    /// 그릴 때 선 긋기가 5 만 번에서 1000 번으로 줄어듭니다.
    ///
    /// UI 에 기대지 않는 순수 계산이라 Core 에 둡니다.
    /// </summary>
    public static class Decimator
    {
        /// <summary>
        /// 한 픽셀 열의 요약.
        ///
        /// 최소와 최대만 담습니다. 예전에는 첫값과 끝값도 담았는데, 한 열은
        /// 가로로 1 픽셀이라 그 값들이 모두 <b>같은 세로 선 위</b>에 찍힙니다.
        /// 화면에 보이는 것은 최소~최대 사이의 세로 선 하나뿐이라, 나머지
        /// 두 값은 그리는 데 쓰이지 않으면서 표본마다 읽고 쓰는 비용만
        /// 냈습니다. 구조체가 작아져서 열 배열을 훑는 것도 빨라집니다.
        /// </summary>
        public struct Column
        {
            public float Min;
            public float Max;
            public bool HasValue;
        }

        /// <summary>
        /// [t0,t1] 을 columns 개의 열로 접습니다. out 배열은 호출자가
        /// columns 개 잡아 넘깁니다 (매 프레임 새로 잡지 않도록).
        /// </summary>
        public static void Build(LogDataset ds, int channel, double t0, double t1,
                                 Column[] outCols, int columns)
        {
            for (int i = 0; i < columns; i++) outCols[i] = new Column();
            if (ds == null || columns <= 0) return;
            if (channel < 0 || channel >= ds.ChannelCount) return;

            int n = ds.SampleCount;
            if (n == 0 || !(t1 > t0)) return;

            float[] v = ds.Channels[channel].Values;
            double[] times = ds.Times;

            int start = ds.IndexAtOrBefore(t0);
            if (start < 0) start = 0;
            double scale = columns / (t1 - t0);

            // 표본은 시간 순서라 열 번호가 뒤로 가지 않습니다. 그래서 지금 열의
            // 최소/최대를 지역 변수에 모았다가 열이 바뀔 때 한 번만
            // 배열에 씁니다.
            //
            // 예전에는 표본마다 배열에서 구조체를 통째로 읽어 고치고 다시
            // 써 넣었습니다. 0.1 초마다 값이 바뀌는 채널은 한 화면에 표본이
            // 수십만 개라, 이 읽고-쓰기가 프레임마다 수십만 번 돌았습니다.
            int cur = -1;
            float mn = 0f, mx = 0f;
            bool has = false;

            for (int i = start; i < n; i++)
            {
                double t = times[i];
                if (t > t1)
                {
                    // 마지막 한 점은 화면 오른쪽 끝까지 선을 잇기 위해 포함합니다.
                    float edgeValue = v[i];
                    if (!float.IsNaN(edgeValue))
                    {
                        int edge = columns - 1;
                        if (has && cur == edge)
                        {
                            if (edgeValue < mn) mn = edgeValue;
                            else if (edgeValue > mx) mx = edgeValue;
                        }
                        else
                        {
                            if (has) Write(outCols, cur, mn, mx);
                            cur = edge; mn = edgeValue; mx = edgeValue; has = true;
                        }
                    }
                    break;
                }

                float value = v[i];
                if (float.IsNaN(value)) continue;

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
        }

        private static void Write(Column[] cols, int c, float mn, float mx)
        {
            cols[c].Min = mn;
            cols[c].Max = mx;
            cols[c].HasValue = true;
        }

        /// <summary>
        /// 픽셀 열마다 "그 시각의 값" 하나씩을 뽑습니다.
        ///
        /// 접기(Build)와 다릅니다. 접기는 한 열에 든 표본들의 최소/최대를 뽑아
        /// 파형의 위아래 끝을 살리는 것이고, 이쪽은 열마다 값 하나만 봅니다.
        /// 두 로그 사이를 칠할 때(차이 영역 표시) 쓰는데, 칠하려면 양쪽 값이 같은
        /// 자리에서 하나씩 있어야 하기 때문입니다.
        ///
        /// 표본이 픽셀보다 성길 때도 열이 비지 않습니다. 계단 채널은 직전 값을
        /// 유지하고, 아날로그는 앞뒤를 선형 보간합니다. 기록 구간 밖은 NaN 입니다.
        ///
        /// 표본을 앞에서부터 한 번만 훑습니다(열마다 이진 탐색하지 않음).
        /// </summary>
        public static void SampleColumns(LogDataset ds, int channel, double t0, double t1,
                                         float[] outValues, int columns)
        {
            for (int i = 0; i < columns; i++) outValues[i] = float.NaN;
            if (ds == null || columns <= 0) return;
            if (channel < 0 || channel >= ds.ChannelCount) return;

            int n = ds.SampleCount;
            if (n == 0 || !(t1 > t0)) return;

            Channel c = ds.Channels[channel];
            float[] v = c.Values;
            double[] times = ds.Times;
            bool stepped = c.IsStepped;

            double first = times[0], last = times[n - 1];
            double step = (t1 - t0) / columns;

            int i0 = ds.IndexAtOrBefore(t0);
            if (i0 < 0) i0 = 0;
            int j = i0;

            for (int x = 0; x < columns; x++)
            {
                // 열의 한가운데 시각을 봅니다. 열의 왼쪽 끝을 쓰면 값이 반 칸씩
                // 왼쪽으로 밀려 보입니다.
                double t = t0 + (x + 0.5) * step;
                if (t < first || t > last) continue;

                while (j + 1 < n && times[j + 1] <= t) j++;
                if (times[j] > t) continue;

                float a = v[j];
                if (float.IsNaN(a)) continue;

                if (stepped || j + 1 >= n) { outValues[x] = a; continue; }

                float b = v[j + 1];
                if (float.IsNaN(b)) { outValues[x] = a; continue; }

                double ta = times[j], tb = times[j + 1];
                outValues[x] = tb > ta ? (float)(a + (b - a) * (t - ta) / (tb - ta)) : a;
            }
        }

        /// <summary>
        /// [t0,t1] 에 걸치는 표본 번호 범위. 양쪽 끝 바깥의 표본 하나씩을 함께
        /// 돌려주므로, 화면 가장자리에서 선이 잘리지 않습니다.
        /// 걸치는 표본이 없으면 false.
        /// </summary>
        public static bool VisibleRange(LogDataset ds, double t0, double t1, out int first, out int last)
        {
            first = 0; last = -1;
            if (ds == null) return false;
            int n = ds.SampleCount;
            if (n == 0 || !(t1 >= t0)) return false;
            if (ds.Times[n - 1] < t0 || ds.Times[0] > t1) return false;

            int i0 = ds.IndexAtOrBefore(t0);
            if (i0 < 0) i0 = 0;

            int i1 = ds.IndexAtOrBefore(t1);
            if (i1 < i0) i1 = i0;
            if (i1 + 1 < n) i1++;

            first = i0;
            last = i1;
            return true;
        }

        /// <summary>[t0,t1] 안의 값 범위. "보이는 구간에 맞춤" 에 씁니다.</summary>
        public static bool RangeIn(LogDataset ds, int channel, double t0, double t1,
                                   out double min, out double max)
        {
            min = 0; max = 0;
            if (ds == null || channel < 0 || channel >= ds.ChannelCount) return false;

            float[] v = ds.Channels[channel].Values;
            double[] times = ds.Times;
            int n = ds.SampleCount;

            int start = ds.IndexAtOrBefore(t0);
            if (start < 0) start = 0;

            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            for (int i = start; i < n && times[i] <= t1; i++)
            {
                float x = v[i];
                if (float.IsNaN(x)) continue;
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }
            if (lo > hi) return false;
            min = lo; max = hi;
            return true;
        }
    }

    // ======================================================================
    //  DifferenceSeries.cs — 1.50 에 이 파일로 합침
    // ======================================================================

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

    // ======================================================================
    //  TraceColors.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 선 색이 <b>무엇을 가리키는지</b> 정하는 규칙 한 줄.
    ///
    /// 두 가지 뜻 중 하나입니다.
    ///
    ///   거짓 — 색이 <b>이전/이후</b>를 가리킵니다. 이전은 파랑, 이후는 빨강.
    ///   참   — 색이 <b>IO</b> 를 가리킵니다. 같은 IO 의 이전/이후는 같은 색의
    ///          옅고 짙음으로 나뉩니다.
    ///
    /// 겹쳐 보기에서 IO 가 여럿이면 뒤쪽입니다. 모두 파랑·빨강으로 그리면
    /// 어느 선이 어느 IO 인지 알 수 없습니다. 레인으로 나눠 보면 IO 마다 칸이
    /// 따로 있으니 색으로 IO 를 가릴 필요가 없고, 겹쳐 보기라도 IO 가 하나면
    /// 가릴 것이 없습니다.
    ///
    /// <b>이 한 줄을 Core 에 둔 이유.</b> 선을 그리는 쪽(GraphCanvas)과
    /// 범례를 적는 쪽(GraphVm)이 각자 판단하고 있었습니다. 그러면 조건을 한쪽만
    /// 고친 날부터 <b>범례가 "파란 선은 이전" 이라고 하면서 선은 다른 색</b>인
    /// 일이 생깁니다. 거짓말하는 범례는 범례가 없는 것보다 나쁩니다 — 없으면
    /// 물어보고, 있으면 믿습니다.
    ///
    /// 화면이 없어도 돌아가는 규칙이라 여기서 시험할 수 있습니다.
    /// </summary>
    public static class TraceColors
    {
        /// <param name="laneMode">IO 마다 칸을 따로 두고 그리는 중인지.</param>
        /// <param name="channelCount">지금 그리는 IO 수.</param>
        public static bool ByIo(bool laneMode, int channelCount)
        {
            return !laneMode && channelCount > 1;
        }

        /// <summary>파랑/빨강 범례를 보여도 되는지. <see cref="ByIo"/> 의 반대입니다.</summary>
        public static bool ShowSideLegend(bool laneMode, int channelCount)
        {
            return !ByIo(laneMode, channelCount);
        }
    }
}
