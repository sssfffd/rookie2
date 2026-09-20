using System;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// "이만큼 벌어진 건 오차지 차이가 아니다" 를 정하는 한 곳.
    ///
    /// 대시보드(비교 목록), 그래프(차이 음영), 히트맵이 <b>모두 여기</b>를
    /// 씁니다. 각자 따로 계산하면 같은 로그인데 화면마다 "차이 난 IO" 가
    /// 달라져서 어느 쪽을 믿어야 할지 알 수 없게 됩니다.
    ///
    /// <b>오차는 그 순간의 값끼리 견줍니다.</b>
    ///
    ///   오차(%) = |이후 − 이전| / |이전| x 100
    ///
    /// 설정에 적는 허용 오차 퍼센트가 <b>바로 이 값</b>입니다. 화면에 적히는
    /// 퍼센트도 같은 값입니다. 그래서 "허용 오차 1%" 와 "이 칸은 3%" 를
    /// 눈으로 바로 견줄 수 있습니다.
    ///
    /// <b>예전에는 이렇지 않았습니다.</b> 채널마다 값 범위를 재어 두고 거기에
    /// 퍼센트를 곱해 <b>절대값 기준</b>을 하나 만들었습니다. 그러다 보니
    ///
    ///   - 같은 1% 라도 채널마다 다른 숫자를 뜻했고,
    ///   - 화면에 적히는 퍼센트의 밑값이 설정한 퍼센트와 달랐으며,
    ///   - 로그를 언제 떴느냐(값이 얼마나 오르내렸느냐)에 따라 기준이 달라졌습니다.
    ///
    /// 지금은 어느 채널이든 "이전 값 대비 몇 %" 하나로 봅니다.
    ///
    /// <b>이전 값이 0 이면</b> 나눌 수가 없습니다. 0 에서 벗어난 것은 값이
    /// 통째로 달라진 것이므로 <b>100%</b> 로 봅니다 (0 → 0 은 0%).
    /// 0 언저리에서 잘게 흔들리는 것까지 100% 로 잡히는 게 싫으면 절대 오차를
    /// 함께 걸어 두세요 — 그 값 이하로 벌어진 것은 퍼센트와 상관없이
    /// 같은 것으로 봅니다.
    /// </summary>
    public static class ToleranceRule
    {
        /// <summary>기본 허용 오차 (퍼센트).</summary>
        public const double DefaultPercent = 0.1;

        /// <summary>
        /// 이름(상태 문자열)으로 견주는 채널에서 이름이 다를 때의 오차.
        /// 값이 아니라 이름이 바뀐 것이므로 "완전히 다름" 으로 봅니다.
        /// </summary>
        public const double NameMismatchPercent = 100.0;

        /// <summary>
        /// 그 순간의 오차(%). 이전 값 대비 이후 값이 얼마나 벌어졌는지.
        /// 한쪽이라도 값이 없으면 NaN 입니다.
        /// </summary>
        public static double ErrorPercent(double before, double after)
        {
            if (double.IsNaN(before) || double.IsNaN(after)) return double.NaN;

            double diff = Math.Abs(after - before);
            if (diff == 0) return 0;

            double b = Math.Abs(before);
            // 0 에서 벗어난 것은 나눌 수가 없습니다. 값이 통째로 달라진
            // 것이므로 100% 로 봅니다.
            if (b == 0) return NameMismatchPercent;

            return diff / b * 100.0;
        }

        /// <summary>
        /// 그 순간이 허용 오차를 넘었는지.
        /// </summary>
        /// <param name="before">이전 로그의 값.</param>
        /// <param name="after">이후 로그의 값.</param>
        /// <param name="absolute">
        /// 절대 오차. 이 값 이하로 벌어진 것은 퍼센트와 상관없이 같은 것으로
        /// 봅니다. 0 이면 끕니다. 0 언저리에서 퍼센트가 튀는 것을 막는 데 씁니다.
        /// </param>
        /// <param name="percent">허용 오차 퍼센트. 설정에 적는 그 값입니다.</param>
        public static bool IsOver(double before, double after, double absolute, double percent)
        {
            if (double.IsNaN(before) || double.IsNaN(after)) return false;

            double diff = Math.Abs(after - before);
            if (diff == 0) return false;
            if (absolute > 0 && diff <= absolute) return false;

            if (double.IsNaN(percent) || percent <= 0) return true;
            return ErrorPercent(before, after) > percent;
        }

        /// <summary>
        /// 이름으로 견주는 채널에서 이름이 다를 때, 그게 차이인지.
        /// <b>늘 차이입니다.</b> 이름에는 "몇 % 다르다" 가 성립하지 않고,
        /// 절대 오차를 크게 잡아 두었다고 해서 상태가 바뀐 것을 놓치면
        /// 안 되기 때문입니다.
        /// </summary>
        public static bool NameMismatchIsOver { get { return true; } }
    }
}
