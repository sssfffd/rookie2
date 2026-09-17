using System;
using System.Globalization;
using LogScope.Core.Model;

namespace LogScope.Core.Io
{
    /// <summary>글자를 숫자 / 참거짓 / 시각으로 읽어 보는 함수 모음.</summary>
    public static class ValueParse
    {
        private static readonly char[] DateSeps = new[] { '-', '/', '.' };

        /// <summary>
        /// 숫자로 읽습니다. 소수점은 마침표로 봅니다. "1,234.5" 처럼 세 자리마다
        /// 쉼표가 찍힌 형태만 쉼표를 지우고 다시 읽습니다. "1,5" 같은 쉼표 소수점은
        /// "1,234" 와 구분할 수 없으므로 건드리지 않습니다.
        /// </summary>
        public static bool TryNumber(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            if (s.Length == 0) return false;

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            if (s.IndexOf(',') >= 0 && LooksGrouped(s))
            {
                string t = s.Replace(",", string.Empty);
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
            }

            // 뒤에 단위가 붙은 숫자 ("12.5 V", "3 ms") 도 값으로 봅니다.
            int i = 0;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            int digits = 0;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) { if (char.IsDigit(s[i])) digits++; i++; }
            if (digits > 0 && i < s.Length && (s[i] == ' ' || char.IsLetter(s[i])))
            {
                if (double.TryParse(s.Substring(0, i), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
            }
            value = 0;
            return false;
        }

        private static bool LooksGrouped(string s)
        {
            int start = (s.Length > 0 && (s[0] == '+' || s[0] == '-')) ? 1 : 0;
            int run = 0; bool seenComma = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ',')
                {
                    if (run < 1 || run > 3) return false;
                    if (seenComma && run != 3) return false;
                    seenComma = true; run = 0;
                }
                else if (char.IsDigit(c)) run++;
                else if (c == '.') { return seenComma && run == 3; }
                else return false;
            }
            return seenComma && run == 3;
        }

        public static bool TryBool(string s, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(s)) return false;
            switch (s.Trim().ToUpperInvariant())
            {
                case "TRUE": case "T": case "ON": case "HIGH": case "YES": case "Y":
                    value = true; return true;
                case "FALSE": case "F": case "OFF": case "LOW": case "NO": case "N":
                    value = false; return true;
                default: return false;
            }
        }

        /// <summary>"HH:MM:SS.fff" / "HH:MM:SS" / "MM:SS" 를 자정 기준 밀리초로.</summary>
        public static bool TryClock(string s, out double ms)
        {
            ms = 0;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            if (s.IndexOf(':') < 0) return false;

            string[] parts = s.Split(':');
            if (parts.Length < 2 || parts.Length > 3) return false;

            double[] v = new double[3];
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) return false;
                double d;
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
                if (d < 0) return false;
                v[i] = d;
            }

            if (parts.Length == 3) ms = ((v[0] * 60 + v[1]) * 60 + v[2]) * 1000.0;
            else ms = (v[0] * 60 + v[1]) * 1000.0;   // MM:SS
            return true;
        }

        /// <summary>"2024-01-02 12:34:56.7" 같은 날짜+시각을 1970 기준 밀리초로.</summary>
        public static bool TryDateTime(string s, out double unixMs)
        {
            unixMs = 0;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            if (s.Length < 8) return false;
            if (s.IndexOfAny(DateSeps) < 0) return false;

            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out dt))
            {
                unixMs = ToUnixMs(dt);
                return true;
            }
            // 현재 문화권도 한 번 봐 줍니다 (한국식 "2024. 1. 2" 등).
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture,
                    DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out dt))
            {
                unixMs = ToUnixMs(dt);
                return true;
            }
            return false;
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static double ToUnixMs(DateTime dt)
        {
            return (dt - Epoch).TotalMilliseconds;
        }

        /// <summary>엑셀 일련값(1899-12-30 기준 일수)을 1970 기준 밀리초로.</summary>
        public static bool TryExcelSerialToUnixMs(double serial, out double unixMs)
        {
            unixMs = 0;
            if (double.IsNaN(serial) || serial < 0 || serial > 2958465.0) return false;
            try
            {
                DateTime dt = DateTime.FromOADate(serial);
                unixMs = ToUnixMs(dt);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        /// <summary>
        /// 칸 하나를 시간축 값으로 읽어 봅니다. 읽히면 종류와 값을 돌려줍니다.
        /// 시간축 후보를 고를 때와 실제 시간축을 만들 때 같은 규칙을 쓰기 위해
        /// 한 곳에 모아 뒀습니다.
        /// </summary>
        public static bool TryTimeValue(Cell c, out TimeKind kind, out double value)
        {
            kind = TimeKind.Number;
            value = 0;

            switch (c.Kind)
            {
                case CellKind.DateSerial:
                    {
                        // 하루 미만이면 시각만 적힌 칸으로 봅니다.
                        if (c.Num > 0 && c.Num < 1.0)
                        {
                            kind = TimeKind.ClockMs;
                            value = c.Num * 86400000.0;
                            return true;
                        }
                        double ms;
                        if (TryExcelSerialToUnixMs(c.Num, out ms)) { kind = TimeKind.DateMs; value = ms; return true; }
                        kind = TimeKind.Number; value = c.Num; return true;
                    }
                case CellKind.Number:
                    kind = TimeKind.Number; value = c.Num; return true;
                case CellKind.Text:
                    {
                        double ms;
                        if (TryClock(c.Text, out ms)) { kind = TimeKind.ClockMs; value = ms; return true; }
                        if (TryDateTime(c.Text, out ms)) { kind = TimeKind.DateMs; value = ms; return true; }
                        double n;
                        if (TryNumber(c.Text, out n)) { kind = TimeKind.Number; value = n; return true; }
                        return false;
                    }
                default:
                    return false;
            }
        }

        /// <summary>엑셀 numFmt 코드가 날짜/시각 서식인지.</summary>
        public static bool IsDateFormat(int builtinId, string formatCode)
        {
            if (builtinId >= 14 && builtinId <= 22) return true;
            if (builtinId >= 45 && builtinId <= 47) return true;
            if (string.IsNullOrEmpty(formatCode)) return false;

            bool inQuote = false, inBracket = false;
            for (int i = 0; i < formatCode.Length; i++)
            {
                char c = formatCode[i];
                if (inQuote) { if (c == '"') inQuote = false; continue; }
                if (inBracket) { if (c == ']') inBracket = false; continue; }
                if (c == '"') { inQuote = true; continue; }
                if (c == '[') { inBracket = true; continue; }
                if (c == '\\') { i++; continue; }
                switch (c)
                {
                    case 'y': case 'Y': case 'd': case 'D':
                    case 'h': case 'H': case 's': case 'S':
                        return true;
                    case 'm': case 'M':
                        return true;   // 분/월 어느 쪽이든 날짜·시각 서식입니다.
                }
            }
            return false;
        }
    }
}
