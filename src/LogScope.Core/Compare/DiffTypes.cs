using System;
using System.Collections.Generic;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// 차이량을 재는 방법. 하나만 쓰면 오해하기 쉬워서 여러 개를 함께 냅니다.
    ///
    /// 예를 들어 +1 / -1 이 계속 흔들리는 채널은 "면적" 이 아주 크게 나오지만
    /// "최대 차이" 는 1 입니다. 반대로 딱 한 순간만 크게 튄 채널은 "평균" 이
    /// 작게 나옵니다. 어느 쪽이 중요한지는 보는 사람이 정해야 합니다.
    /// </summary>
    public enum DiffMetric
    {
        /// <summary>가장 크게 벌어진 순간의 차이. 한 번이라도 크게 틀어졌는지.</summary>
        MaxAbs = 0,
        /// <summary>차이의 평균. 전반적으로 얼마나 다른지.</summary>
        MeanAbs = 1,
        /// <summary>제곱평균제곱근. 큰 차이에 더 무게를 둡니다.</summary>
        Rms = 2,
        /// <summary>차이 x 시간의 합(면적). 오래 벌어져 있을수록 커집니다.</summary>
        Area = 3,
        /// <summary>허용 오차를 넘은 시간의 비율(0~1).</summary>
        TimeRatio = 4,
        /// <summary>허용 오차를 넘은 표본 수.</summary>
        SampleCount = 5,
        /// <summary>허용 오차를 넘은 구간의 개수. 흔들림 횟수를 봅니다.</summary>
        SegmentCount = 6,
    }

    public enum Presence
    {
        Both = 0,
        OnlyBefore = 1,
        OnlyAfter = 2,
    }

    public sealed class DiffOptions
    {
        /// <summary>
        /// 절대 허용 오차. 이 값 이하의 차이는 같은 것으로 봅니다.
        /// 기본 0 (끔). 비율 오차와 함께 쓰이며 둘 중 큰 쪽이 기준이 됩니다.
        /// </summary>
        public double AbsoluteTolerance = 0.0;

        /// <summary>
        /// 비율 허용 오차. 채널 값 범위에 대한 비율이고, 0.001 이 0.1% 입니다.
        /// 채널마다 값의 크기가 달라서 절대값 하나로는 다룰 수 없습니다.
        /// 자세한 규칙은 ToleranceRule 을 보세요.
        /// </summary>
        public double RelativeTolerance = ToleranceRule.FromPercent(ToleranceRule.DefaultPercent);

        /// <summary>이후 로그의 시간을 이만큼 밉니다 (시간축 단위).</summary>
        public double Shift = 0.0;

        /// <summary>두 로그의 시작 시각을 자동으로 맞춥니다.</summary>
        public bool AutoAlign = true;

        /// <summary>비교에 쓸 표본 수 상한. 크면 정확하지만 느려집니다.</summary>
        public int MaxComparePoints = 20000;

        /// <summary>목록을 정렬할 기준.</summary>
        public DiffMetric SortBy = DiffMetric.MaxAbs;

        public DiffOptions Clone() { return (DiffOptions)MemberwiseClone(); }
    }

    /// <summary>채널 하나의 비교 결과.</summary>
    public sealed class ChannelDiff
    {
        public string Name;
        public Presence Presence;
        public int BeforeIndex = -1;
        public int AfterIndex = -1;

        public double MaxAbs;
        public double MeanAbs;
        public double Rms;
        public double Area;
        public double TimeRatio;
        public int DiffSamples;
        public int Segments;
        public int ComparedSamples;

        /// <summary>이 채널에서 "차이" 로 본 기준값. 화면에 그대로 보여 줍니다.</summary>
        public double Threshold;

        /// <summary>상태/문자열 채널이라 "같다/다르다" 로만 비교한 경우.</summary>
        public bool ByName;

        public bool Changed;

        public double Value(DiffMetric m)
        {
            switch (m)
            {
                case DiffMetric.MaxAbs: return MaxAbs;
                case DiffMetric.MeanAbs: return MeanAbs;
                case DiffMetric.Rms: return Rms;
                case DiffMetric.Area: return Area;
                case DiffMetric.TimeRatio: return TimeRatio;
                case DiffMetric.SampleCount: return DiffSamples;
                case DiffMetric.SegmentCount: return Segments;
                default: return MaxAbs;
            }
        }

        public static string MetricLabel(DiffMetric m)
        {
            switch (m)
            {
                case DiffMetric.MaxAbs: return "최대 차이";
                case DiffMetric.MeanAbs: return "평균 차이";
                case DiffMetric.Rms: return "RMS";
                case DiffMetric.Area: return "차이 면적";
                case DiffMetric.TimeRatio: return "차이 시간 비율";
                case DiffMetric.SampleCount: return "차이 표본 수";
                case DiffMetric.SegmentCount: return "차이 구간 수";
                default: return "최대 차이";
            }
        }

        public string Format(DiffMetric m)
        {
            double v = Value(m);
            switch (m)
            {
                case DiffMetric.TimeRatio: return (v * 100.0).ToString("0.0") + " %";
                case DiffMetric.SampleCount:
                case DiffMetric.SegmentCount: return ((long)v).ToString("N0");
                default:
                    {
                        double a = Math.Abs(v);
                        if (a != 0 && (a < 0.001 || a >= 1e6)) return v.ToString("G4");
                        return v.ToString("0.####");
                    }
            }
        }
    }

    public sealed class CompareResult
    {
        public List<ChannelDiff> Items = new List<ChannelDiff>();
        public List<ChannelDiff> OnlyBefore = new List<ChannelDiff>();
        public List<ChannelDiff> OnlyAfter = new List<ChannelDiff>();

        public double AppliedShift;
        public double OverlapStart;
        public double OverlapEnd;
        public int ComparePoints;

        public int BeforeChannelCount;
        public int AfterChannelCount;
        public int CommonCount;
        public int ChangedCount;

        /// <summary>겹치는 구간이 하나도 없으면 여기에 이유가 들어갑니다.</summary>
        public string Warning = string.Empty;

        public List<ChannelDiff> Changed()
        {
            var r = new List<ChannelDiff>();
            for (int i = 0; i < Items.Count; i++) if (Items[i].Changed) r.Add(Items[i]);
            return r;
        }
    }
}
