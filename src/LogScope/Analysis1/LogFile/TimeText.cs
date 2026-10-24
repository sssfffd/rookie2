using System;
using System.Globalization;
using LogScope.Common;

namespace LogScope.Analysis1.LogFile
{
    /// <summary>
    /// 시간을 글자로 바꾸는 규칙. <see cref="LogDataset.FormatTime"/> 이
    /// 쓰고, <b>경과 시간</b>을 적어야 하는 쪽(단계 시작을 0 으로 놓은
    /// 그래프 가로축)도 씁니다.
    ///
    /// 따로 뺀 까닭: 경과 시간을 그 로그의 시간 모양으로 그대로 적으면
    /// 날짜 로그에서 <b>1970 년</b>이 찍힙니다. 5 초 지났다는 말이
    /// "1970-01-01 00:00:05" 로 나오는데, 그게 날짜로 읽히면 로그를
    /// 잘못 읽은 줄 알게 됩니다.
    /// </summary>
    public static class TimeText
    {
        /// <summary>밀리초를 시:분:초.밀리 로. 음수면 앞에 "-" 가 붙습니다.</summary>
        public static string Clock(double ms)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms)) return "-";

            bool neg = ms < 0;
            if (neg) ms = -ms;

            long total = (long)Math.Floor(ms);
            int msec = (int)(total % 1000);
            long sec = total / 1000;
            int s = (int)(sec % 60);
            long min = sec / 60;
            int m = (int)(min % 60);
            long h = min / 60;

            string text = msec != 0
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}.{3:000}", h, m, s, msec)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", h, m, s);
            return neg ? "-" + text : text;
        }

        /// <summary>
        /// 어떤 시각에서부터 센 <b>경과 시간</b>. 날짜 로그도 날짜가 아니라
        /// 걸린 시간으로 적습니다.
        ///
        /// 부호를 적습니다. 0 자리보다 앞은 "-" 로 나오는데, 그게 안 보이면
        /// 단계가 시작하기 전의 구간을 단계 안으로 읽습니다.
        /// </summary>
        public static string Elapsed(LogDataset ds, double delta)
        {
            if (double.IsNaN(delta)) return "-";

            TimeKind kind = ds != null ? ds.TimeKind : TimeKind.Number;
            switch (kind)
            {
                case TimeKind.ClockMs:
                case TimeKind.DateMs:
                    // 둘 다 밀리초입니다. 날짜 로그여도 경과 시간은 시계로.
                    return (delta > 0 ? "+" : string.Empty) + Clock(delta);

                case TimeKind.Index:
                    return Sign(delta) + Math.Abs(Math.Round(delta)).ToString("0", CultureInfo.InvariantCulture)
                         + "표본";

                default:
                    {
                        string u = ds != null && ds.TimeUnit.Length > 0 ? " " + ds.TimeUnit : string.Empty;
                        return Sign(delta) + NumberText.Plain(Math.Abs(delta)) + u;
                    }
            }
        }

        private static string Sign(double v)
        {
            if (v > 0) return "+";
            return v < 0 ? "-" : string.Empty;
        }
    }
}
