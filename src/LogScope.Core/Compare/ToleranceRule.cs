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
    ///   비율 오차   그 채널 값 크기의 몇 %. 기본 0.1%.
    ///               "무엇의 몇 %" 인지는 BaseRange 가 정합니다.
    ///
    /// 비율을 쓰는 이유는 채널마다 값의 크기가 다르기 때문입니다.
    /// 0 에서 4000 까지 오르내리는 아날로그 채널에서 1 쯤 흔들리는 것은 잡음이고,
    /// 0 과 1 만 오가는 디지털 채널에서 1 은 완전히 다른 상태입니다.
    /// 절대값 하나로는 이 둘을 같이 다룰 수 없습니다.
    /// </summary>
    public static class ToleranceRule
    {
        /// <summary>기본 비율 오차 (퍼센트). 값 크기의 0.1%.</summary>
        public const double DefaultPercent = 0.1;

        /// <summary>
        /// 비율을 곱할 밑값.
        ///
        /// 두 가지 중 <b>큰 쪽</b>입니다.
        ///
        ///   폭     로그 안에서 값이 오르내린 너비 (최대 − 최소)
        ///   크기   값 자체의 크기 (절대값 중 가장 큰 것)
        ///
        /// 예전에는 폭만 보고, 폭이 <b>정확히 0</b> 일 때만 크기로 물러섰습니다.
        /// 그런데 실제로 걸린 것은 폭이 0 인 채널이 아니라 <b>폭이 아주 좁은</b>
        /// 채널이었습니다.
        ///
        ///   6466 과 6467 사이만 오가는 채널 → 폭 1
        ///   허용 오차를 1% 로 잡아도 기준값은 1 x 1% = 0.01
        ///   6467 과 6466 의 차이 1 은 0.01 보다 크므로 "차이" 로 잡힘
        ///
        /// 6467 짜리 값에서 1 이 흔들린 것을 1% 오차로 봐 달라는 말은 누가 봐도
        /// "6467 의 1%(=64)" 라는 뜻입니다. 폭이 1 인 것은 그 로그가 신호의 좁은
        /// 한 토막만 담고 있다는 뜻이지, 그 채널이 원래 1 만큼만 움직이는
        /// 채널이라는 뜻이 아닙니다.
        ///
        /// 그래서 둘 중 큰 쪽을 씁니다. 계기 사양에서 정확도를 "풀 스케일의 몇 %"
        /// 또는 "읽은 값의 몇 %" 로 적는 것과 같은 얘기이고, 여기서는 <b>둘 중
        /// 느슨한 쪽</b>을 택한 것입니다.
        ///
        /// 0/1 만 오가는 디지털 채널은 폭도 1, 크기도 1 이라 예전과 같습니다.
        /// 0 과 1 의 차이는 그대로 잡힙니다.
        ///
        /// 좁은 폭에서 벌어진 작은 차이까지 봐야 한다면 비율을 낮추거나
        /// (예: 0.01%) 절대 허용 오차를 쓰면 됩니다. 실제로 쓰인 기준값은
        /// 대시보드의 "기준값" 칸과 그래프·히트맵 설명 줄에 그대로 보입니다.
        /// </summary>
        public static double BaseRange(Channel a, Channel b)
        {
            double span = 0, magnitude = 0;

            if (a != null && !double.IsNaN(a.Min))
            {
                span = Math.Max(span, a.Max - a.Min);
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(a.Min), Math.Abs(a.Max)));
            }
            if (b != null && !double.IsNaN(b.Min))
            {
                span = Math.Max(span, b.Max - b.Min);
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(b.Min), Math.Abs(b.Max)));
            }

            return Math.Max(span, magnitude);
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
