using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 손으로 고치는 설정 <b>한 파일</b>. 실행 파일 옆의 config.txt 입니다.
    ///
    /// 전에는 appname.txt(프로그램 이름)와 screens.txt(분석 이름)로 나뉘어
    /// 있었습니다. 고칠 것이 늘어날 때마다 파일이 하나씩 생기면, 무엇을 어디서
    /// 바꾸는지 기억해야 합니다. 한 파일로 모았습니다.
    ///
    /// <code>
    ///   # 이렇게 시작하는 줄과 빈 줄은 건너뜁니다.
    ///   name = LogScope
    ///
    ///   analysis1 = 로그 비교 | 이전 Log 파일과 이후 Log 파일을 비교합니다.
    ///   analysis2 = 분석 2   | 아직 정해지지 않았습니다.
    ///   analysis3 = 분석 3
    /// </code>
    ///
    /// 규칙은 셋입니다.
    ///   1. <c>열쇠 = 값</c>. 열쇠는 대소문자를 가리지 않습니다.
    ///   2. 분석 이름의 세로줄(|) 뒤는 메인 화면 칸에 적히는 설명입니다 (없어도 됩니다).
    ///   3. <b>없는 줄은 기본값</b>입니다. 파일을 지워도 프로그램은 그대로 돕니다.
    ///
    /// 프로그램 안에서 바꾸는 화면은 없습니다 — 자주 바뀌는 값이 아니고,
    /// 설정 창을 뒤지는 것보다 파일 한 줄을 고치는 편이 빠릅니다.
    ///
    /// <b>실행 파일 이름</b>도 이 <c>name</c> 을 따라갑니다. 그건 build.bat 이
    /// 복사할 때 붙여 줍니다 — 빌드 산출물 이름이라 프로그램이 스스로 바꿀 수
    /// 없습니다.
    /// </summary>
    public sealed class AppConfig
    {
        public const string FileName = "config.txt";

        /// <summary>전에 쓰던 파일들. config.txt 가 없으면 이것들도 봅니다.</summary>
        public const string LegacyNameFile = "appname.txt";
        public const string LegacyScreenFile = "screens.txt";

        /// <summary>분석의 수. 화면이 셋이라 셋입니다.</summary>
        public const int Count = 3;

        public const string DefaultName = "LogScope";

        private static readonly string[] DefaultTitles =
        {
            "로그 비교", "DB 분석", "분석 3",
        };

        private static readonly string[] DefaultSummaries =
        {
            "이전 Log 파일과 이후 Log 파일을 비교합니다. 대시보드 · 그래프 · 히트맵.",
            "두 DB 의 표·열·값 차이를 보고 맞추는 SQL 을 만듭니다.",
            "아직 정해지지 않았습니다.",
        };

        private string _name = DefaultName;
        private string _fullName = string.Empty;
        private string _dbBefore = string.Empty;
        private string _dbAfter = string.Empty;
        private string _mysqlDump = string.Empty;
        private string _dbDumpBefore = string.Empty;
        private string _dbDumpAfter = string.Empty;
        private string _ibd2sdi = string.Empty;
        private readonly string[] _titles = (string[])DefaultTitles.Clone();
        private readonly string[] _summaries = (string[])DefaultSummaries.Clone();

        /// <summary>바꾸지 않은 상태. 파일이 없을 때 이것을 씁니다.</summary>
        public static AppConfig Default { get { return new AppConfig(); } }

        /// <summary>
        /// 프로그램이 쓰는 값. 시작할 때 <see cref="Load"/> 로 채웁니다.
        /// 채우기 전에 읽어도 기본값이 나오므로 터지지 않습니다.
        /// </summary>
        public static AppConfig Current = new AppConfig();

        /// <summary>창 제목과 왼쪽 위에 적히는 프로그램 이름.</summary>
        public string Name { get { return _name; } }

        /// <summary>
        /// <b>창 제목</b>에 들어가는 긴 이름. 비워 두면 전에 쓰던 글이
        /// 붙습니다 (<see cref="DefaultSubtitle"/>).
        ///
        /// <see cref="Name"/> 과 따로 둔 이유가 있습니다. Name 은 창 제목과
        /// 실행 파일 이름까지 따라가므로 짧아야 합니다. 긴 이름은 보여 주기만
        /// 하는 것이라 길어도 되고, 띄어쓰기나 괄호가 들어가도 됩니다.
        /// </summary>
        public string FullName { get { return _fullName; } }

        /// <summary>긴 이름을 안 적었을 때 창 제목에 붙는 글.</summary>
        public const string DefaultSubtitle = "IO 로그 그래프 뷰어";

        /// <summary>
        /// 창 제목. <b>긴 이름이 여기 들어갑니다.</b>
        ///
        /// 위 줄(화면 단추가 있는 줄)에 적으면 폭을 먹습니다. 창 제목은 운영
        /// 체제가 이미 그려 주는 자리라 <b>화면을 하나도 더 쓰지 않습니다</b> —
        /// 작업 표시줄과 Alt+Tab 에도 그대로 나오니 "이 프로그램이 무엇인지"
        /// 를 적어 두기에는 거기가 제자리입니다. 그래서 길이도 안 자릅니다.
        ///
        /// <b>Core 에 둔 이유.</b> 화면에서 만들면 시험이 같은 규칙을 한 번
        /// 더 적게 되고, 그러면 한쪽만 고쳐지는 날이 옵니다.
        /// </summary>
        public string WindowTitle
        {
            get
            {
                string full = _fullName.Length == 0 ? DefaultSubtitle : _fullName;
                return _name + " — " + full;
            }
        }

        // ---------------- 분석 2 (DB) ----------------
        //
        // 견줄 DB 두 벌이 있는 자리입니다. 폴더면 그 안의 .sql / .csv 를 읽고,
        // 파일 하나면 그 파일을 읽습니다.

        public string DbBefore { get { return _dbBefore; } }
        public string DbAfter { get { return _dbAfter; } }

        /// <summary>
        /// <b>선택</b>: mysqldump 실행 파일의 자리. 적어 두면 아래 두 줄은
        /// <b>인수만</b> 적으면 됩니다.
        ///
        /// <b>알아 둘 것</b>: 적어 둔 프로그램을 자식 프로세스로 띄웁니다.
        /// 남이 바꿔 쓸 수 있는 폴더의 실행 파일을 가리키지 마세요.
        /// 그리고 mysqldump 는 <b>돌고 있는 서버</b>에 접속해서 뽑습니다 —
        /// 멈춘 데이터 폴더(.frm/.ibd)만으로는 뽑을 수 없습니다.
        /// </summary>
        public string MysqlDumpPath { get { return _mysqlDump; } }

        /// <summary>
        /// <b>선택</b>: 덤프를 받을 때 쓸 인수(또는 명령 줄 전체).
        ///
        ///   db.mysqldump 를 적어 두었으면 → <b>인수만</b>
        ///   안 적어 두었으면           → <b>명령 줄 전체</b> (실행 파일 포함)
        ///
        /// 접속 정보를 우리가 받아 들고 있지 않습니다. 사용자 이름과 암호를
        /// 저장하기 시작하면 "그걸 어디에 어떻게 두느냐" 가 새 문제가 되고,
        /// 명령 줄에 암호를 박으면 작업 관리자에 그대로 보입니다. 접속 방법은
        /// MySQL 쪽 방식(--login-path, --defaults-extra-file)에 맡깁니다.
        /// </summary>
        public string DbDumpBefore { get { return _dbDumpBefore; } }
        public string DbDumpAfter { get { return _dbDumpAfter; } }

        /// <summary>
        /// <b>선택</b>: ibd2sdi 경로. MySQL 8 의 .ibd 에서 <b>표와 열 이름만</b>
        /// 꺼냅니다 (값은 못 꺼냅니다). 서버가 멈춰 있어도 됩니다.
        /// </summary>
        public string Ibd2SdiPath { get { return _ibd2sdi; } }

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

        /// <summary>실행 파일 옆의 config.txt. 자리를 못 찾으면 이름만 돌려줍니다.</summary>
        public static string ResolvePath()
        {
            return Beside(FileName);
        }

        private static string Beside(string name)
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, name);
            }
            catch (Exception e) when (e is IOException || e is NotSupportedException)
            {
            }
            return name;
        }

        /// <summary>
        /// 파일에서 읽습니다. 없거나 읽을 수 없으면 기본값입니다 —
        /// <b>이름 하나 때문에 프로그램이 안 켜지면 안 됩니다.</b>
        ///
        /// config.txt 가 없으면 전에 쓰던 appname.txt / screens.txt 를 봅니다.
        /// 거기 적어 둔 것을 파일 이름이 바뀌었다는 이유로 잃으면 안 됩니다.
        /// </summary>
        public static AppConfig Load(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return Parse(ReadLines(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                return Default;
            }

            return LoadLegacy(path);
        }

        /// <summary>
        /// 전에 쓰던 두 파일. config.txt 와 같은 폴더에서 찾습니다.
        /// appname.txt 는 한 줄에 이름, screens.txt 는 한 줄에 분석 하나입니다.
        /// </summary>
        private static AppConfig LoadLegacy(string configPath)
        {
            var it = new AppConfig();
            string dir = null;
            try { dir = string.IsNullOrEmpty(configPath) ? null : Path.GetDirectoryName(configPath); }
            catch (ArgumentException) { }

            string nameFile = Join(dir, LegacyNameFile);
            string screenFile = Join(dir, LegacyScreenFile);

            try
            {
                if (File.Exists(nameFile))
                {
                    foreach (string line in ReadLines(nameFile))
                    {
                        string t = line.Trim();
                        if (t.Length == 0 || t[0] == '#') continue;
                        it._name = t;
                        break;
                    }
                }

                if (File.Exists(screenFile))
                {
                    int n = 0;
                    foreach (string line in ReadLines(screenFile))
                    {
                        if (n >= Count) break;
                        string t = line.Trim();
                        if (t.Length == 0 || t[0] == '#') continue;
                        it.TakeAnalysis(n++, t);
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                return Default;
            }
            return it;
        }

        private static string Join(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return name;
            try { return Path.Combine(dir, name); }
            catch (ArgumentException) { return name; }
        }

        /// <summary>
        /// 글자 인코딩을 알아서 맞춥니다.
        ///
        /// 메모장으로 고칠 파일입니다. 요즘 메모장은 UTF-8 로 저장하지만
        /// 예전 것은 <b>CP949(ANSI)</b> 로 저장합니다. UTF-8 로만 읽으면 그 경우
        /// 한글이 깨집니다. 그래서 BOM → UTF-8 → CP949 순으로 봅니다.
        ///
        /// <b>손으로 고치는 다른 설정 파일도 이 함수를 씁니다</b>
        /// (<see cref="GroupFile"/>). 인코딩 읽기를 두 번 짜면 한쪽만 고쳐져
        /// "config.txt 는 한글이 되는데 groups.txt 는 깨지는" 일이 생깁니다.
        /// </summary>
        public static string[] ReadLines(string path)
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
        /// <c>열쇠 = 값</c> 줄들을 읽습니다. 빈 줄과 '#' 로 시작하는 줄은
        /// 건너뜁니다. 모르는 열쇠도 건너뜁니다 — 뒤 버전에서 생긴 열쇠를
        /// 적어 둔 파일로 옛 프로그램을 켜도 터지지 않아야 합니다.
        /// </summary>
        public static AppConfig Parse(IEnumerable<string> lines)
        {
            var it = new AppConfig();
            if (lines == null) return it;

            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (value.Length == 0) continue;   // 비워 둔 줄은 기본값입니다.

                if (key == "name") { it._name = value; continue; }
                if (key == "fullname") { it._fullName = value; continue; }
                if (key == "db.before") { it._dbBefore = value; continue; }
                if (key == "db.after") { it._dbAfter = value; continue; }
                if (key == "db.mysqldump") { it._mysqlDump = value; continue; }
                if (key == "db.dump.before") { it._dbDumpBefore = value; continue; }
                if (key == "db.dump.after") { it._dbDumpAfter = value; continue; }
                if (key == "db.ibd2sdi") { it._ibd2sdi = value; continue; }

                for (int n = 0; n < Count; n++)
                {
                    if (key != "analysis" + (n + 1)) continue;
                    it.TakeAnalysis(n, value);
                    break;
                }
            }
            return it;
        }

        /// <summary>"이름 | 설명" 한 줄. 이름을 비워 두면 그 자리의 기본값입니다.</summary>
        private void TakeAnalysis(int index, string value)
        {
            if (index < 0 || index >= Count) return;

            string title = value;
            string summary = null;

            int bar = value.IndexOf('|');
            if (bar >= 0)
            {
                title = value.Substring(0, bar).Trim();
                summary = value.Substring(bar + 1).Trim();
            }

            if (title.Length > 0) _titles[index] = title;
            if (!string.IsNullOrEmpty(summary)) _summaries[index] = summary;
        }

        /// <summary>저장소에 두는 본보기. build.bat 이 out 폴더에 넣어 줍니다.</summary>
        public static string Sample()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# " + DefaultName + " 설정. 이 파일 하나만 고치면 됩니다.");
            sb.AppendLine("# 고치고 프로그램을 다시 켜면 바뀝니다. 지우면 기본값으로 돕니다.");
            sb.AppendLine("#");
            sb.AppendLine("# name      : 창 제목과 왼쪽 위에 적히는 이름. 실행 파일 이름도 이걸 따릅니다");
            sb.AppendLine("#             (실행 파일 이름은 build.bat 이 붙이므로 다시 빌드해야 바뀝니다).");
            sb.AppendLine("# fullname  : 창 제목에 들어가는 긴 이름. 길이는 마음대로 (화면을 안 먹습니다).");
            sb.AppendLine("# analysis1 : 분석 화면의 이름.  \"이름 | 한 줄 설명\" 으로 적습니다.");
            sb.AppendLine("# db.before : 분석 2 에서 견줄 DB 두 벌의 자리 (폴더 또는 .sql 파일).");
            sb.AppendLine();
            sb.AppendLine("name = " + DefaultName);
            sb.AppendLine("fullname = Log 비교 · 분석 도구");
            sb.AppendLine();
            for (int i = 0; i < Count; i++)
            {
                sb.AppendLine("analysis" + (i + 1) + " = " + DefaultTitles[i] + " | " + DefaultSummaries[i]);
            }
            sb.AppendLine();
            sb.AppendLine("# db.before = D:\\db\\이전");
            sb.AppendLine("# db.after  = D:\\db\\이후");
            return sb.ToString();
        }
    }
}
