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
