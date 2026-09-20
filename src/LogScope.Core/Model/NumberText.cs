using System;
using System.Globalization;

namespace LogScope.Core.Model
{
    /// <summary>
    /// 숫자를 사람이 읽을 글자로.
    ///
    /// <b>지수 표기(1.2e+07, 3.4E-05)를 쓰지 않습니다.</b> .NET 의 "G4" 같은
    /// 서식은 자리 수가 크거나 작아지면 멋대로 지수 표기로 바뀝니다. 로그를
    /// 들여다보는 사람 입장에서 "1.234e+06" 은 한 번 더 머리로 옮겨야 하는
    /// 숫자라, 그냥 "1,234,000" 이 낫습니다.
    ///
    /// 화면마다 따로 서식을 쓰면 같은 값이 화면마다 달라 보이므로
    /// 대시보드 · 그래프 눈금 · 히트맵이 모두 여기를 씁니다.
    /// </summary>
    public static class NumberText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 자리가 넉넉한 곳(대시보드 칸, 설명 줄)에서 쓰는 서식.
        ///
        /// 큰 수는 천 단위로 끊어 주고, 작은 수는 유효한 자리가 보일 때까지
        /// 소수점을 늘립니다. 어느 쪽도 지수로 바뀌지 않습니다.
        /// </summary>
        public static string Plain(double v)
        {
            if (double.IsNaN(v)) return "-";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";

            double a = Math.Abs(v);
            if (a == 0) return "0";

            if (a >= 1e6) return v.ToString("#,##0", Inv);
            if (a >= 0.001) return v.ToString("0.####", Inv);

            // 0.001 보다 작은 값. 0 으로 뭉개지지 않게 소수점을 넉넉히 둡니다.
            // 그래도 0 으로 보이면(1e-13 같은 값) 0 이 아니라는 것만 알립니다.
            string s = v.ToString("0.############", Inv);
            return IsAllZero(s) ? (v > 0 ? "0 보다 조금 큼" : "0 보다 조금 작음") : s;
        }

        /// <summary>
        /// 칸이 좁은 곳(히트맵 칸)에서 쓰는 서식. 자리 수를 줄이되
        /// 역시 지수 표기는 쓰지 않습니다.
        /// </summary>
        public static string Short(double v)
        {
            if (double.IsNaN(v)) return "-";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";

            double a = Math.Abs(v);
            if (a == 0) return "0";

            if (a >= 1e5) return v.ToString("#,##0", Inv);
            if (a >= 100) return v.ToString("0", Inv);
            if (a >= 10) return v.ToString("0.#", Inv);
            if (a >= 1) return v.ToString("0.##", Inv);
            if (a >= 0.001) return v.ToString("0.###", Inv);

            string s = v.ToString("0.#########", Inv);
            return IsAllZero(s) ? "≈0" : s;
        }

        /// <summary>"0", "0.000" 처럼 0 만 남았는지.</summary>
        private static bool IsAllZero(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '1' && c <= '9') return false;
            }
            return true;
        }
    }
}
