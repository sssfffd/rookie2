using System;
using LogScope.Core.Model;

namespace LogScope.Core.Render
{
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
        /// <summary>한 픽셀 열의 요약.</summary>
        public struct Column
        {
            public float Min;
            public float Max;
            public float First;
            public float Last;
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

            for (int i = start; i < n; i++)
            {
                double t = times[i];
                if (t > t1)
                {
                    // 마지막 한 점은 화면 오른쪽 끝까지 선을 잇기 위해 포함합니다.
                    int edge = columns - 1;
                    Put(outCols, edge, v[i]);
                    break;
                }
                int c = (int)((t - t0) * scale);
                if (c < 0) c = 0;
                if (c >= columns) c = columns - 1;
                Put(outCols, c, v[i]);
            }
        }

        private static void Put(Column[] cols, int c, float value)
        {
            if (float.IsNaN(value)) return;
            Column col = cols[c];
            if (!col.HasValue)
            {
                col.HasValue = true;
                col.Min = value; col.Max = value;
                col.First = value; col.Last = value;
            }
            else
            {
                if (value < col.Min) col.Min = value;
                if (value > col.Max) col.Max = value;
                col.Last = value;
            }
            cols[c] = col;
        }

        /// <summary>
        /// 픽셀 열마다 "그 시각의 값" 하나씩을 뽑습니다.
        ///
        /// 접기(Build)와 다릅니다. 접기는 한 열에 든 표본들의 최소/최대를 뽑아
        /// 파형의 위아래 끝을 살리는 것이고, 이쪽은 열마다 값 하나만 봅니다.
        /// 두 로그 사이를 칠할 때(차이 음영) 쓰는데, 칠하려면 양쪽 값이 같은
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
}
