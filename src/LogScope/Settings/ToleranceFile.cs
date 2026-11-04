using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using LogScope.Analysis1.LogFile;

namespace LogScope.Settings
{
    /// <summary>
    /// 손으로 고치는 <b>IO 별 허용 오차 파일</b> (<c>tolerances.txt</c>, 1.86).
    ///
    /// groups.txt 와 같은 방식입니다. 설정 창에서 건 IO 별 허용 오차는
    /// <c>LogScope.settings.json</c> (프로그램이 쓰는 파일) 에만 있어서 <b>빌드 전에 미리 넣어 둘 수가
    /// 없었습니다</b>. 이 파일은 저장소에 두고 <c>build.bat</c> 이 <c>out</c> 으로 넣어 줍니다.
    ///
    /// <b>누가 이기나.</b> 이 파일이 규칙을 <b>하나라도</b> 정하면 이 파일이 주인입니다 — 켤 때 그 규칙으로
    /// 맞춥니다. 설정 창에서 고친 것은 그 자리에서는 먹지만 다시 켜면 이 파일 쪽으로 돌아갑니다.
    /// <b>전부 주석이면 아무것도 안 합니다</b> (설명만 적힌 빈 틀이 그때까지 건 규칙을 지우면 안 되니까).
    ///
    /// 적는 법 — 한 줄에 IO 하나, 설정 창의 두 칸 (퍼센트, 절대값) 순서 그대로:
    /// <code>
    /// TT-01  = 20          (퍼센트만)
    /// VALVE  = , 0         (절대값만)
    /// PT-01  = 5, 0.1      (둘 다)
    /// </code>
    /// 빈 칸은 기본값입니다 (설정 창에서 칸을 비워 둔 것과 같음).
    /// </summary>
    public static class ToleranceFile
    {
        public const string FileName = "tolerances.txt";

        /// <summary>실행 파일 옆의 tolerances.txt.</summary>
        public static string ResolvePath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, FileName);
            }
            catch (Exception e) when (e is IOException || e is NotSupportedException)
            {
            }
            return FileName;
        }

        /// <summary>파일을 읽습니다. 없거나 정한 규칙이 없으면 <b>빈 목록</b> — 그때는 설정 파일의 규칙을 그대로 씁니다.</summary>
        public static List<ToleranceOverride> Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new List<ToleranceOverride>();
                return Parse(AppConfig.ReadLines(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                // 파일 하나 때문에 프로그램이 안 켜지면 안 됩니다.
                return new List<ToleranceOverride>();
            }
        }

        /// <summary>
        /// <c>IO = 퍼센트, 절대값</c> 꼴로 읽습니다.
        ///
        /// <c>#</c> 줄 · 빈 줄 · 등호 없는 줄 · 이름이 빈 줄은 건너뜁니다. 숫자가 아니거나 범위 밖
        /// (퍼센트 0~100, 절대값 0 이상) 인 칸은 빈 칸 (기본값) 으로 봅니다. 두 칸 다 비면 그 줄은 건너뜁니다.
        /// 등호는 <b>마지막 것</b>으로 가릅니다 — 숫자에는 등호가 없으니, 이름에 등호가 있어도 읽힙니다.
        /// 같은 IO (띄어쓰기 · _ - . 차이는 같은 것) 가 또 나오면 <b>뒤의 줄</b>이 이깁니다 (설정 창과 같음).
        /// </summary>
        public static List<ToleranceOverride> Parse(IEnumerable<string> lines)
        {
            var list = new List<ToleranceOverride>();
            if (lines == null) return list;

            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.LastIndexOf('=');
                if (eq <= 0) continue;

                string io = line.Substring(0, eq).Trim();
                if (io.Length == 0) continue;

                string[] parts = line.Substring(eq + 1).Split(new char[] { ',', ';', '\t' });
                var r = new ToleranceOverride();
                r.Io = io;
                r.Percent = Number(parts.Length > 0 ? parts[0] : null, 100);
                r.Absolute = Number(parts.Length > 1 ? parts[1] : null, double.MaxValue);
                if (r.IsEmpty) continue;

                string key = LogDataset.LooseKey(io);
                if (key.Length == 0) continue;

                int at;
                if (byKey.TryGetValue(key, out at)) { list[at] = r; continue; }
                if (list.Count >= AppSettings.MaxToleranceRules) continue;
                byKey[key] = list.Count;
                list.Add(r);
            }
            return list;
        }

        /// <summary>빈 칸 · 숫자 아님 · 범위 밖은 NaN (= 기본값). 끝의 % 는 떼고 읽습니다.</summary>
        private static double Number(string text, double max)
        {
            if (text == null) return double.NaN;
            string t = text.Trim();
            if (t.EndsWith("%", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1).Trim();
            if (t.Length == 0) return double.NaN;

            double v;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return double.NaN;
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v > max) return double.NaN;
            return v;
        }

        /// <summary>지금 규칙을 이 파일 꼴로 적습니다. 설정 창에서 건 것을 저장소로 옮길 때 씁니다.</summary>
        public static string Write(IEnumerable<ToleranceOverride> rules)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# IO 별 허용 오차. 한 줄에 IO 하나입니다.");
            sb.AppendLine("#");
            sb.AppendLine("#   IO이름 = 퍼센트, 절대값      (빈 칸은 기본값)");
            sb.AppendLine("#");
            sb.AppendLine("# 이 파일이 규칙을 하나라도 정하면 이 파일이 주인입니다.");
            sb.AppendLine("# 설정 창에서 고친 것은 다시 켜면 이 파일 쪽으로 돌아갑니다.");
            sb.AppendLine("# 전부 주석이면 아무것도 안 합니다 (그때는 설정 파일의 규칙을 씁니다).");
            sb.AppendLine();

            if (rules != null)
            {
                foreach (ToleranceOverride r in rules)
                {
                    if (r == null || r.IsEmpty || string.IsNullOrEmpty((r.Io ?? string.Empty).Trim())) continue;
                    sb.Append(r.Io.Trim()).Append(" = ");
                    if (r.HasPercent) sb.Append(r.Percent.ToString("0.####", CultureInfo.InvariantCulture));
                    if (r.HasAbsolute)
                    {
                        sb.Append(", ");
                        sb.Append(r.Absolute.ToString("0.######", CultureInfo.InvariantCulture));
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
