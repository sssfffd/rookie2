using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 분석 1·2·3 의 이름과 한 줄 설명.
    ///
    /// <b>실행 파일 옆의 screens.txt 한 곳</b>에서 옵니다. 프로그램 안에서
    /// 바꾸는 화면은 없습니다 — 이름은 자주 바뀌는 것이 아니고, 바꿀 때마다
    /// 설정 화면을 뒤지는 것보다 파일 한 줄을 고치는 편이 빠릅니다.
    ///
    /// 파일이 없거나 줄이 모자라면 <b>기본값</b>을 씁니다. 지워도 프로그램은
    /// 그대로 돕니다.
    ///
    /// 파일 모양 (한 줄에 하나, 위에서부터 분석 1 · 2 · 3):
    /// <code>
    ///   # 이렇게 시작하는 줄과 빈 줄은 건너뜁니다.
    ///   로그 비교 | 이전 Log 파일과 이후 Log 파일을 비교합니다.
    ///   분석 2    | 아직 정해지지 않았습니다.
    ///   분석 3
    /// </code>
    /// 세로줄(|) 뒤는 설명입니다. 없어도 됩니다.
    /// </summary>
    public sealed class ScreenNames
    {
        public const string FileName = "screens.txt";

        /// <summary>분석의 수. 화면이 셋이라 셋입니다.</summary>
        public const int Count = 3;

        private static readonly string[] DefaultTitles =
        {
            "로그 비교", "분석 2", "분석 3",
        };

        private static readonly string[] DefaultSummaries =
        {
            "이전 Log 파일과 이후 Log 파일을 비교합니다. 대시보드 · 그래프 · 히트맵.",
            "아직 정해지지 않았습니다.",
            "아직 정해지지 않았습니다.",
        };

        private readonly string[] _titles = (string[])DefaultTitles.Clone();
        private readonly string[] _summaries = (string[])DefaultSummaries.Clone();

        /// <summary>바꾸지 않은 상태. 파일이 없을 때 이것을 씁니다.</summary>
        public static ScreenNames Default { get { return new ScreenNames(); } }

        /// <summary>
        /// 프로그램이 쓰는 값. 시작할 때 <see cref="Load"/> 로 채웁니다.
        /// 채우기 전에 읽어도 기본값이 나오므로 터지지 않습니다.
        /// </summary>
        public static ScreenNames Current = new ScreenNames();

        /// <summary>1 부터 셉니다 (분석 1 · 2 · 3). 범위를 벗어나면 빈 글자.</summary>
        public string Title(int number)
        {
            return In(number) ? _titles[number - 1] : string.Empty;
        }

        public string Summary(int number)
        {
            return In(number) ? _summaries[number - 1] : string.Empty;
        }

        private static bool In(int number)
        {
            return number >= 1 && number <= Count;
        }

        /// <summary>실행 파일 옆의 screens.txt. 자리를 못 찾으면 이름만 돌려줍니다.</summary>
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

        /// <summary>
        /// 파일에서 읽습니다. 없거나 읽을 수 없으면 기본값입니다 —
        /// <b>이름 하나 때문에 프로그램이 안 켜지면 안 됩니다.</b>
        /// </summary>
        public static ScreenNames Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Default;
                return Parse(ReadLines(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                return Default;
            }
        }

        /// <summary>
        /// 글자 인코딩을 알아서 맞춥니다.
        ///
        /// 메모장으로 고칠 파일입니다. 요즘 메모장은 UTF-8 로 저장하지만
        /// 예전 것은 <b>CP949(ANSI)</b> 로 저장합니다. UTF-8 로만 읽으면 그 경우
        /// 한글이 깨집니다. 그래서 BOM → UTF-8 → CP949 순으로 봅니다.
        /// </summary>
        private static string[] ReadLines(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            string text;

            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                text = new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
            }
            else if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                text = Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
            }
            else
            {
                try
                {
                    // throwOnInvalidBytes: CP949 로 저장된 파일이면 여기서 걸립니다.
                    text = new UTF8Encoding(false, true).GetString(raw);
                }
                catch (DecoderFallbackException)
                {
                    text = Legacy().GetString(raw);
                }
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>CP949. 없는 환경(리눅스의 mono 등)에서는 기본 인코딩으로 물러섭니다.</summary>
        private static Encoding Legacy()
        {
            try { return Encoding.GetEncoding(949); }
            catch (ArgumentException) { return Encoding.Default; }
            catch (NotSupportedException) { return Encoding.Default; }
        }

        /// <summary>
        /// 줄을 읽어 이름과 설명으로 나눕니다. 빈 줄과 '#' 로 시작하는 줄은
        /// 건너뜁니다. 세 줄을 채우면 나머지는 보지 않습니다.
        /// </summary>
        public static ScreenNames Parse(IEnumerable<string> lines)
        {
            var it = new ScreenNames();
            if (lines == null) return it;

            int n = 0;
            foreach (string raw in lines)
            {
                if (n >= Count) break;
                if (raw == null) continue;

                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string title = line;
                string summary = null;

                int bar = line.IndexOf('|');
                if (bar >= 0)
                {
                    title = line.Substring(0, bar).Trim();
                    summary = line.Substring(bar + 1).Trim();
                }

                // 이름을 비워 둔 줄은 <b>그 자리의 기본값</b>으로 둡니다.
                // 자리를 건너뛰는 뜻으로 쓸 수 있어야 해서입니다.
                if (title.Length > 0) it._titles[n] = title;
                if (!string.IsNullOrEmpty(summary)) it._summaries[n] = summary;
                n++;
            }
            return it;
        }

        /// <summary>파일이 없을 때 만들어 둘 본보기. build.bat 이 out 폴더에 넣어 줍니다.</summary>
        public static string Sample()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 분석 1·2·3 의 이름입니다. 위에서부터 차례로 한 줄씩.");
            sb.AppendLine("# 세로줄(|) 뒤는 메인 화면 칸에 적히는 한 줄 설명입니다 (없어도 됩니다).");
            sb.AppendLine("# 이 파일을 고치고 프로그램을 다시 켜면 바뀝니다. 지우면 아래 기본값으로 돕니다.");
            sb.AppendLine();
            for (int i = 0; i < Count; i++)
            {
                sb.AppendLine(DefaultTitles[i] + " | " + DefaultSummaries[i]);
            }
            return sb.ToString();
        }
    }
}
