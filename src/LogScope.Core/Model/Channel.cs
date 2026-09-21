using System;

namespace LogScope.Core.Model
{
    /// <summary>
    /// IO 채널 하나. 값은 float 로 담습니다. 채널 200 x 표본 5만 이면
    /// double 이면 80 MB, float 이면 40 MB 입니다. 로그 값은 소수점 몇 자리
    /// 수준이라 float 의 유효자리(약 7 자리)로 충분합니다.
    /// 비어 있는 칸은 NaN 이며 "여기서 선이 끊긴다"는 뜻입니다.
    /// </summary>
    public sealed class Channel
    {
        public string Name;
        public ChannelKind Kind;
        public float[] Values;

        /// <summary>Kind == State 일 때만 채워집니다. Values 는 이 배열의 인덱스.</summary>
        public string[] States;

        /// <summary>
        /// 단위 ("bar", "℃", "mm" 등). 못 찾으면 빈 글자.
        /// 엑셀 머리 행 아래의 단위 줄에서 가져오고, 없으면 이름 끝의
        /// 괄호에서 찾습니다 ("압력 PT-01 [bar]").
        /// 세로 눈금 옆에 적는 데 씁니다.
        /// </summary>
        public string Unit = string.Empty;

        /// <summary>NaN 을 뺀 최소/최대. 값이 하나도 없으면 둘 다 NaN.</summary>
        public double Min;
        public double Max;

        public Channel(string name, int sampleCount)
        {
            Name = name ?? string.Empty;
            Kind = ChannelKind.Analog;
            Unit = string.Empty;
            Values = new float[sampleCount];
            States = null;
            Min = double.NaN;
            Max = double.NaN;
        }

        public int SampleCount { get { return Values.Length; } }

        // ---- 변화량 (차분) ------------------------------------------------
        //
        // 이웃한 두 표본의 <b>차이</b>입니다. 안 바뀌었으면 0, 1 올랐으면 +1,
        // 1 내렸으면 -1. 값이 얼마인지가 아니라 <b>언제 얼마나 움직였는지</b>를
        // 보는 눈금입니다.
        //
        // 값 배열과 나란한 배열을 하나 더 만듭니다. 화면에 그릴 때마다 새로
        // 계산하면 표본이 수백만 개인 채널에서 프레임마다 그만큼 돌아서 못 씁니다.
        // 그렇다고 읽을 때 미리 다 만들면 채널마다 메모리가 두 배가 되므로,
        // <b>그 눈금으로 실제 그리는 채널만</b> 처음 필요할 때 만듭니다.
        // 값 배열은 읽은 뒤 바뀌지 않으므로 한 번 만들면 계속 맞습니다.
        private float[] _diff;
        private double _diffMin = double.NaN, _diffMax = double.NaN;

        public float[] Diff
        {
            get { EnsureDiff(); return _diff; }
        }

        /// <summary>NaN 을 뺀 변화량의 최소/최대. 값이 하나도 없으면 NaN.</summary>
        public double DiffMin { get { EnsureDiff(); return _diffMin; } }
        public double DiffMax { get { EnsureDiff(); return _diffMax; } }

        private void EnsureDiff()
        {
            if (_diff != null) return;

            float[] v = Values;
            var d = new float[v.Length];
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;

            bool havePrev = false;
            float prev = 0f;

            for (int i = 0; i < v.Length; i++)
            {
                float x = v[i];
                if (float.IsNaN(x))
                {
                    // 값이 없는 자리는 변화량도 없습니다. 여기서 선이 끊깁니다.
                    d[i] = float.NaN;
                    havePrev = false;
                    continue;
                }

                // 첫 표본과, 빈 구간 뒤의 첫 표본은 0 입니다. 직전 값을 모르는
                // 자리라, 없는 변화를 지어내는 것보다 "여기서는 모른다" 를
                // 0 으로 두는 편이 낫습니다.
                float delta = havePrev ? x - prev : 0f;
                d[i] = delta;
                prev = x; havePrev = true;

                if (delta < lo) lo = delta;
                if (delta > hi) hi = delta;
            }

            _diff = d;
            if (lo > hi) { _diffMin = double.NaN; _diffMax = double.NaN; }
            else { _diffMin = lo; _diffMax = hi; }
        }

        /// <summary>Min/Max 를 값 배열에서 다시 계산합니다.</summary>
        public void RecomputeRange()
        {
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            float[] v = Values;
            for (int i = 0; i < v.Length; i++)
            {
                float x = v[i];
                if (float.IsNaN(x)) continue;
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }
            if (lo > hi) { Min = double.NaN; Max = double.NaN; }
            else { Min = lo; Max = hi; }
        }

        /// <summary>계단으로 그려야 하는 채널인지 (디지털/상태).</summary>
        public bool IsStepped { get { return Kind != ChannelKind.Analog; } }

        /// <summary>커서 자리에 보여줄 글자. 상태 채널이면 상태 이름을 돌려줍니다.</summary>
        public string FormatValue(double v)
        {
            if (double.IsNaN(v)) return "-";
            if (Kind == ChannelKind.State && States != null)
            {
                int i = (int)Math.Round(v);
                if (i >= 0 && i < States.Length) return States[i];
                return "?";
            }
            if (Kind == ChannelKind.Digital) return v >= 0.5 ? "1" : "0";
            double a = Math.Abs(v);
            if (a != 0 && (a < 0.001 || a >= 1e7)) return v.ToString("G6");
            return v.ToString("0.######");
        }
    }
}
