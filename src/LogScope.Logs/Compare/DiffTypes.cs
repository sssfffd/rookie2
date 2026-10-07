using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using LogScope.Core.Model;

namespace LogScope.Logs.Compare
{
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
        /// 허용 오차 퍼센트. 설정에 적는 그 값 그대로입니다 (0.1 이 0.1%).
        ///
        /// 그 순간의 오차 = |이후 − 이전| / |이전| x 100 과 견줍니다.
        /// 채널마다 기준을 따로 만들지 않습니다. 자세한 규칙은 ToleranceRule.
        /// </summary>
        public double RelativePercent = ToleranceRule.DefaultPercent;

        /// <summary>
        /// IO 별 허용 오차. <b>있으면 이것이 이깁니다</b> — 위의 두 값 대신
        /// 표의 기본값과 IO 별 값을 씁니다. 없으면(null) 위의 두 값 그대로.
        /// </summary>
        public ToleranceTable Tolerances;

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

        /// <summary>
        /// 가장 크게 벌어진 순간의 오차(%). 이전 값 대비입니다.
        /// "차이 났다" 는 판정도 이 값이 허용 오차를 넘었는지로 내리므로,
        /// 목록에 적히는 숫자와 판정이 어긋날 수 없습니다.
        /// </summary>
        public double MaxPercent;
        public double MeanAbs;
        public double Rms;
        public double Area;
        public double TimeRatio;
        public int DiffSamples;
        public int Segments;
        public int ComparedSamples;

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
                case DiffMetric.MaxPercent: return MaxPercent;
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
                case DiffMetric.MaxPercent: return "최대 오차 %";
                default: return "최대 차이";
            }
        }

        public string Format(DiffMetric m)
        {
            double v = Value(m);
            switch (m)
            {
                case DiffMetric.TimeRatio: return (v * 100.0).ToString("0.0") + " %";
                case DiffMetric.MaxPercent:
                    // 허용 오차와 같은 잣대라 소수점을 넉넉히 둡니다.
                    // 0.1% 기준과 0.09% 를 눈으로 가려야 하기 때문입니다.
                    return NumberText.Plain(v) + " %";
                case DiffMetric.SampleCount:
                case DiffMetric.SegmentCount: return ((long)v).ToString("N0");
                default:
                    // 지수 표기(1.2e+07)를 쓰지 않습니다 — NumberText 참고.
                    return NumberText.Plain(v);
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
