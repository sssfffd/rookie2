using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 아주 작은 JSON 읽기/쓰기.
    ///
    /// 설정 파일 하나 때문에 바깥 라이브러리(Newtonsoft 등)를 들이지 않으려고
    /// 직접 만들었습니다. 200 줄 남짓이라 전부 읽어볼 수 있습니다.
    /// 우리가 쓰는 만큼만 지원합니다: 객체, 배열, 문자열, 숫자, 참거짓, null.
    ///
    /// 값은 다음 .NET 타입으로 옵니다.
    ///   객체 -> Dictionary&lt;string, object&gt;
    ///   배열 -> List&lt;object&gt;
    ///   문자열 -> string, 숫자 -> double, 참거짓 -> bool, null -> null
    /// </summary>
    public static class Json
    {
        // ---------------- 쓰기 ----------------

        public static string Write(object value)
        {
            var sb = new StringBuilder(1024);
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void Indent(StringBuilder sb, int depth)
        {
            for (int i = 0; i < depth; i++) sb.Append("  ");
        }

        private static void WriteValue(StringBuilder sb, object v, int depth)
        {
            if (v == null) { sb.Append("null"); return; }

            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    Indent(sb, depth + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, depth + 1);
                    if (++i < dict.Count) sb.Append(',');
                    sb.Append('\n');
                }
                Indent(sb, depth);
                sb.Append('}');
                return;
            }

            var list = v as IList<object>;
            if (list != null)
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    Indent(sb, depth + 1);
                    WriteValue(sb, list[i], depth + 1);
                    if (i + 1 < list.Count) sb.Append(',');
                    sb.Append('\n');
                }
                Indent(sb, depth);
                sb.Append(']');
                return;
            }

            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append(((bool)v) ? "true" : "false"); return; }

            if (v is double || v is float || v is int || v is long)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            WriteString(sb, v.ToString());
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- 읽기 ----------------

        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = 0;
            object v = ParseValue(text, ref i, 0);
            return v;
        }

        private const int MaxDepth = 64;

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static object ParseValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("JSON 이 너무 깊습니다.");
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 이 갑자기 끝났습니다.");

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') return ParseString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ParseNumber(s, ref i);
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i, int depth)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            i++;   // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON: 이름이 있어야 할 자리입니다.");
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON: ':' 이 없습니다.");
                i++;
                d[key] = ParseValue(s, ref i, depth + 1);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw new FormatException("JSON: ',' 나 '}' 가 없습니다.");
            }
        }

        private static List<object> ParseArray(string s, ref int i, int depth)
        {
            var list = new List<object>();
            i++;   // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException("JSON: ',' 나 ']' 가 없습니다.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;   // 여는 따옴표
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        {
                            if (i + 4 > s.Length) throw new FormatException("JSON: \\u 가 짧습니다.");
                            int code = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            i += 4;
                            sb.Append((char)code);
                            break;
                        }
                    default: throw new FormatException("JSON: 모르는 이스케이프 \\" + e);
                }
            }
            throw new FormatException("JSON: 따옴표가 닫히지 않았습니다.");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || s[i] == '+' || s[i] == '-')) i++;
            string t = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("JSON: 숫자를 읽을 수 없습니다: " + t);
            return d;
        }

        // ---------------- 꺼내 쓰기 ----------------

        public static Dictionary<string, object> AsObject(object v)
        {
            return v as Dictionary<string, object> ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public static List<object> AsArray(object v)
        {
            return v as List<object> ?? new List<object>();
        }

        public static string GetString(IDictionary<string, object> d, string key, string fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is string) return (string)v;
            return fallback;
        }

        public static double GetDouble(IDictionary<string, object> d, string key, double fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (double)v;
            return fallback;
        }

        public static int GetInt(IDictionary<string, object> d, string key, int fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (int)Math.Round((double)v);
            return fallback;
        }

        public static bool GetBool(IDictionary<string, object> d, string key, bool fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return fallback;
        }

        public static List<object> GetArray(IDictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsArray(v);
            return new List<object>();
        }

        public static Dictionary<string, object> GetObject(IDictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return AsObject(v);
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
    }
}
