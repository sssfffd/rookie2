namespace LogScope.Core.Settings
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
        /// <summary>
        /// 가장 크게 벌어진 순간의 오차(%). 이전 값 대비입니다.
        /// 설정에 적는 허용 오차와 <b>같은 잣대</b>라 바로 견줄 수 있습니다.
        /// </summary>
        MaxPercent = 7,
    }
}
