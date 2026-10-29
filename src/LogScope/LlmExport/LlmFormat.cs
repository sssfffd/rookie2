using System;
using System.Globalization;
using LogScope.Analysis1.LogFile;

namespace LogScope.LlmExport
{
    /// <summary>
    /// LLM 입력에 숫자 · 시간을 적는 규칙 한 곳 (1.43).
    ///
    /// 화면용 글(NumberText · FormatTime)은 사람 눈에 맞춰 "1.2k" 처럼 줄이기도 하는데, LLM 에는
    /// <b>계산할 수 있는 숫자</b>를 줘야 합니다. 그래서 여기서는 늘 점(.) 소수, 천 단위 쉼표 없음,
    /// 유효 숫자 6 자리 안쪽으로 적습니다.
    /// </summary>
    public static class LlmFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>숫자 하나. NaN · 무한대는 빈 글자 (표에서 "값 없음").</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return string.Empty;
            double a = Math.Abs(v);
            if (a != 0 && (a < 1e-4 || a >= 1e9)) return v.ToString("0.###E+0", Inv);
            if (a >= 1000) return v.ToString("0.#", Inv);
            if (a >= 1) return v.ToString("0.####", Inv);
            return v.ToString("0.######", Inv);
        }

        /// <summary>정수.</summary>
        public static string Int(long v) { return v.ToString(Inv); }

        /// <summary>퍼센트 (0.123 → "12.3").</summary>
        public static string Pct(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio)) return string.Empty;
            return (ratio * 100.0).ToString("0.##", Inv);
        }

        /// <summary>
        /// 로그의 <b>시각</b> 하나 — 그 로그가 쓰는 모양 그대로 (시계면 "HH:MM:SS.mmm").
        /// LLM 이 사람에게 "12:03:15 쯤" 이라고 말할 수 있게.
        /// </summary>
        public static string Time(LogDataset ds, double t)
        {
            if (double.IsNaN(t) || double.IsInfinity(t)) return string.Empty;
            return ds != null ? ds.FormatTime(t) : Num(t);
        }

        /// <summary>
        /// 시간 <b>길이</b> 하나를 초로 (시계 · 날짜 로그는 밀리초를 초로 바꿈). 단위를 알 수 없는
        /// 숫자 시간축은 그 숫자 그대로 — 단위는 머리(a1.head) 의 "시간 단위" 에 적습니다.
        /// </summary>
        public static string Span(LogDataset ds, double delta)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta)) return string.Empty;
            if (ds != null && (ds.TimeKind == TimeKind.ClockMs || ds.TimeKind == TimeKind.DateMs))
                return Num(delta / 1000.0);
            return Num(delta);
        }

        /// <summary>길이 숫자의 단위 설명 (머리에 적음).</summary>
        public static string SpanUnit(LogDataset ds)
        {
            if (ds == null) return string.Empty;
            switch (ds.TimeKind)
            {
                case TimeKind.ClockMs:
                case TimeKind.DateMs: return "초 (s)";
                case TimeKind.Number: return ds.TimeUnit.Length > 0 ? ds.TimeUnit : "로그의 시간 숫자 그대로 (단위 모름)";
                default: return "표본 번호 (시간 열을 못 읽음)";
            }
        }

        /// <summary>IO 종류를 사람 말로.</summary>
        public static string Kind(ChannelKind k)
        {
            switch (k)
            {
                case ChannelKind.Digital: return "디지털";
                case ChannelKind.State: return "상태";
                default: return "아날로그";
            }
        }
    }
}
