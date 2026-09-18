using System;
using LogScope.Core.Model;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// "이만큼 벌어진 건 오차지 차이가 아니다" 를 정하는 한 곳.
    ///
    /// 대시보드(비교 목록), 그래프(차이 음영), 히트맵이 <b>모두 이 함수</b>를
    /// 씁니다. 각자 따로 계산하면 같은 로그인데 화면마다 "차이 난 IO" 가
    /// 달라져서 어느 쪽을 믿어야 할지 알 수 없게 됩니다.
    ///
    /// 기준값은 두 가지 중 <b>큰 쪽</b>입니다.
    ///
    ///   절대 오차   설정에 적은 값 그대로. 기본 0 (끔).
    ///   비율 오차   그 채널 값 범위의 몇 %. 기본 0.1%.
    ///
    /// 비율을 쓰는 이유는 채널마다 값의 크기가 다르기 때문입니다.
    /// 0 에서 4000 까지 오르내리는 아날로그 채널에서 1 쯤 흔들리는 것은 잡음이고,
    /// 0 과 1 만 오가는 디지털 채널에서 1 은 완전히 다른 상태입니다.
    /// 절대값 하나로는 이 둘을 같이 다룰 수 없습니다.
    /// </summary>
    public static class ToleranceRule
    {
        /// <summary>기본 비율 오차 (퍼센트). 값 범위의 0.1%.</summary>
        public const double DefaultPercent = 0.1;

        /// <summary>
        /// 비율을 곱할 밑값.
        ///
        /// 먼저 값이 오르내린 폭(최대 − 최소)을 봅니다. 그게 "그 채널의 범위" 라는
        /// 말에 가장 가깝기 때문입니다.
        ///
        /// 로그 내내 값이 한 자리에 머문 채널은 폭이 0 이라 비율 오차가 통째로
        /// 사라집니다. 3000 에 가만히 있던 값이 3001 이 된 것까지 차이로 세게
        /// 되는데, 그건 잡음에 가깝습니다. 그래서 폭이 0 이면 값의 크기를
        /// 대신 씁니다.
        /// </summary>
        public static double BaseRange(Channel a, Channel b)
        {
            double span = 0;
            if (a != null && !double.IsNaN(a.Min)) span = Math.Max(span, a.Max - a.Min);
            if (b != null && !double.IsNaN(b.Min)) span = Math.Max(span, b.Max - b.Min);
            if (span > 0) return span;

            double magnitude = 0;
            if (a != null && !double.IsNaN(a.Min))
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(a.Min), Math.Abs(a.Max)));
            if (b != null && !double.IsNaN(b.Min))
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(b.Min), Math.Abs(b.Max)));
            return magnitude;
        }

        /// <summary>
        /// 이 채널 짝에서 "차이" 로 볼 기준값.
        /// </summary>
        /// <param name="a">이전 로그 쪽 채널. null 이면 무시합니다.</param>
        /// <param name="b">이후 로그 쪽 채널. null 이면 무시합니다.</param>
        /// <param name="absolute">절대 오차. 음수는 0 으로 봅니다.</param>
        /// <param name="relative">비율 오차. 0.001 이 0.1% 입니다.</param>
        public static double For(Channel a, Channel b, double absolute, double relative)
        {
            double abs = absolute > 0 ? absolute : 0;
            double rel = relative > 0 ? BaseRange(a, b) * relative : 0;
            return Math.Max(abs, rel);
        }

        /// <summary>퍼센트(0.1)를 비율(0.001)로.</summary>
        public static double FromPercent(double percent)
        {
            if (double.IsNaN(percent) || percent < 0) return 0;
            if (percent > 100) percent = 100;
            return percent / 100.0;
        }

        /// <summary>비율(0.001)을 퍼센트(0.1)로.</summary>
        public static double ToPercent(double relative)
        {
            if (double.IsNaN(relative) || relative < 0) return 0;
            return relative * 100.0;
        }
    }
}
