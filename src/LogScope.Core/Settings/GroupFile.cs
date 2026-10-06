using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 손으로 고치는 <b>그룹 설정 파일</b> (<c>groups.txt</c>).
    ///
    /// <b>왜 따로 있나.</b> 그룹은 지금까지 <c>LogScope.settings.json</c> 에만
    /// 있었습니다. 그건 프로그램이 쓰는 파일이라 <b>빌드 전에 미리 넣어 둘 수가
    /// 없었습니다</b> — 설비마다 IO 를 수십 개씩 묶어 두는데, 그걸 PC 마다
    /// 손으로 다시 하는 것은 할 일이 아닙니다. 이 파일은 저장소에 두고
    /// <c>build.bat</c> 이 <c>out</c> 으로 넣어 줍니다 (config.txt 와 같습니다).
    ///
    /// <b>누가 이기나.</b> 이 파일이 그룹을 <b>하나라도</b> 정하면 그 파일이
    /// 주인입니다 — 켤 때 그 그룹으로 맞춥니다. 화면에서 고친 것은 그 자리에서는
    /// 바뀌지만 <b>다시 켜면 이 파일 쪽으로 돌아갑니다</b>. 그래서 화면에 그렇게
    /// 적어 둡니다.
    ///
    /// <b>전부 주석이면 아무것도 안 합니다.</b> 이게 중요합니다. "파일이 있으면
    /// 주인" 으로 했다면, 설명만 적힌 빈 틀이 들어 있는 것만으로 그때까지 묶어 둔
    /// 그룹이 전부 지워집니다. 그래서 <b>정한 그룹이 하나 이상일 때만</b>
    /// 주인이 됩니다.
    ///
    /// 적는 법 — 한 줄에 그룹 하나:
    /// <code>
    /// 가열부 = TEMP_1, TEMP_2, HEATER_ON
    /// 압력   = PRS_MAIN, PRS_SUB
    /// </code>
    /// </summary>
    public static class GroupFile
    {
        public const string FileName = "groups.txt";

        /// <summary>그룹 수 상한. 설정 파일이 감당할 만큼입니다.</summary>
        public const int MaxGroups = 200;

        /// <summary>그룹 하나에 담을 IO 수 상한.</summary>
        public const int MaxMembersPerGroup = 2000;

        /// <summary>실행 파일 옆의 groups.txt.</summary>
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
        /// 파일을 읽습니다. 없거나 정한 그룹이 없으면 <b>빈 목록</b>입니다 —
        /// 그때는 부르는 쪽이 설정 파일의 그룹을 그대로 씁니다.
        /// </summary>
        public static List<GroupDef> Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new List<GroupDef>();
                return Parse(AppConfig.ReadLines(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                // 그룹 파일 하나 때문에 프로그램이 안 켜지면 안 됩니다.
                return new List<GroupDef>();
            }
        }

        /// <summary>
        /// <c>이름 = IO, IO, IO</c> 꼴로 읽습니다.
        ///
        /// <c>#</c> 로 시작하는 줄과 빈 줄은 건너뜁니다. 등호가 없는 줄,
        /// 이름이 빈 줄, IO 가 하나도 없는 줄도 건너뜁니다 — 틀린 줄 하나
        /// 때문에 나머지를 못 읽으면 안 됩니다.
        ///
        /// 같은 이름이 또 나오면 <b>앞의 그룹에 더합니다.</b> 긴 목록을 여러
        /// 줄에 나눠 적을 수 있고, 같은 이름의 그룹이 둘로 보이는 일도
        /// 없습니다.
        /// </summary>
        public static List<GroupDef> Parse(IEnumerable<string> lines)
        {
            var list = new List<GroupDef>();
            if (lines == null) return list;

            var byName = new Dictionary<string, GroupDef>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string name = line.Substring(0, eq).Trim();
                string rest = line.Substring(eq + 1);
                if (name.Length == 0) continue;

                GroupDef g;
                if (!byName.TryGetValue(name, out g))
                {
                    if (list.Count >= MaxGroups) continue;
                    g = new GroupDef(name);
                    byName[name] = g;
                    list.Add(g);
                }

                Members(rest, g);
            }

            // IO 가 하나도 없는 그룹은 빼지 않습니다. "이름만 만들어 두고
            // 화면에서 끌어다 넣기" 가 쓰는 길이라, 지우면 그 길이 막힙니다.
            return list;
        }

        private static void Members(string rest, GroupDef g)
        {
            // 쉼표 · 세미콜론 · 탭으로 가릅니다. 엑셀에서 붙여넣으면 탭이
            // 들어오고, 손으로 적으면 쉼표를 씁니다.
            string[] parts = rest.Split(new char[] { ',', ';', '\t' });
            for (int i = 0; i < parts.Length; i++)
            {
                string io = parts[i].Trim();
                if (io.Length == 0) continue;
                if (g.Members.Count >= MaxMembersPerGroup) return;
                if (g.Members.Contains(io)) continue;
                g.Members.Add(io);
            }
        }

        /// <summary>
        /// 지금 그룹을 이 파일 꼴로 적습니다. 화면에서 묶어 둔 것을 저장소로
        /// 옮길 때 씁니다 — 손으로 다시 적지 않게.
        /// </summary>
        public static string Write(IEnumerable<GroupDef> groups)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 그룹 설정. 한 줄에 그룹 하나입니다.");
            sb.AppendLine("#");
            sb.AppendLine("#   그룹이름 = IO1, IO2, IO3");
            sb.AppendLine("#");
            sb.AppendLine("# 이 파일이 그룹을 하나라도 정하면 이 파일이 주인입니다.");
            sb.AppendLine("# 화면에서 고친 것은 다시 켜면 이 파일 쪽으로 돌아갑니다.");
            sb.AppendLine("# 전부 주석이면 아무것도 안 합니다 (그때는 설정 파일의 그룹을 씁니다).");
            sb.AppendLine();

            if (groups != null)
            {
                foreach (GroupDef g in groups)
                {
                    if (g == null) continue;
                    sb.Append(g.Name).Append(" = ");
                    sb.AppendLine(string.Join(", ", g.Members.ToArray()));
                }
            }
            return sb.ToString();
        }
    }
}
