using System;
using System.Collections.Generic;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Heatmap
{
    /// <summary>
    /// 히트맵 왼쪽 줄 하나. 그룹 이름 줄이거나 IO 줄입니다.
    /// </summary>
    public sealed class HeatLine
    {
        /// <summary>참이면 그룹 이름 줄, 거짓이면 IO 줄.</summary>
        public bool IsGroup { get; private set; }

        /// <summary>그룹 이름 (IO 줄이면 그 IO 가 든 그룹 이름).</summary>
        public string Group { get; private set; }

        /// <summary>그룹 줄: 그 그룹에서 남은 IO 수 (검색 뒤). IO 줄: 0.</summary>
        public int Count { get; private set; }

        /// <summary>그룹 줄: 접혀 있는지.</summary>
        public bool Collapsed { get; private set; }

        /// <summary>IO 줄: 결과의 줄 (HeatmapResult.Rows 의 것). 그룹 줄이면 null.</summary>
        public HeatRow Row { get; private set; }

        public static HeatLine Head(string group, int count, bool collapsed)
        {
            var l = new HeatLine();
            l.IsGroup = true; l.Group = group; l.Count = count; l.Collapsed = collapsed;
            return l;
        }

        public static HeatLine Io(string group, HeatRow row)
        {
            var l = new HeatLine();
            l.Group = group; l.Row = row;
            return l;
        }
    }

    /// <summary>
    /// 히트맵의 줄을 <b>그래프 화면과 같은 그룹</b>으로 나누고, 검색으로
    /// 거릅니다 (1.07).
    ///
    /// 나누는 규칙은 그래프 화면의 IO 목록(ChannelListVm.RebuildRows)과
    /// 같습니다 — 다르면 같은 IO 가 두 화면에서 다른 그룹에 보입니다.
    ///
    ///   - 그룹은 설정에 적힌 차례대로
    ///   - IO 가 그룹 둘에 들어 있으면 <b>앞 그룹</b>에만
    ///   - 어느 그룹에도 없는 IO 는 맨 아래 <see cref="Ungrouped"/>
    ///   - 검색은 이름에 그 글자가 들어 있으면 (대소문자 안 가림)
    ///
    /// 다른 점 하나: <b>남은 IO 가 없는 그룹은 이름 줄도 감춥니다</b> (검색
    /// 안 해도). 히트맵에는 차이 난 IO 만 오므로, 아무 일 없던 그룹의 빈
    /// 이름 줄이 수십 개 깔리면 정작 볼 줄이 아래로 밀립니다.
    ///
    /// 그룹 안의 차례는 <b>히트맵 차례 그대로</b>(많이 벌어진 것이 위)입니다.
    /// 그룹 목록의 차례로 바꾸면 그룹 안에서 무엇이 제일 나쁜지가 안 보입니다.
    ///
    /// 계산은 다시 하지 않습니다 — 이미 센 줄을 늘어놓는 것뿐이라, 검색을
    /// 고칠 때마다 바로 바뀝니다.
    /// </summary>
    public static class HeatLayout
    {
        public const string Ungrouped = "그룹에 없는 IO";

        public static List<HeatLine> Build(IList<HeatRow> rows, IList<GroupDef> groups,
                                           string search, ICollection<string> collapsed)
        {
            var lines = new List<HeatLine>();
            if (rows == null || rows.Count == 0) return lines;


            // IO 이름 → 처음 든 그룹 번호. 뒤 그룹에 또 있어도 앞 그룹이 이깁니다.
            var groupOf = new Dictionary<string, int>(StringComparer.Ordinal);
            int groupCount = groups == null ? 0 : groups.Count;
            for (int g = 0; g < groupCount; g++)
            {
                GroupDef def = groups[g];
                if (def == null || def.Members == null) continue;
                foreach (string name in def.Members)
                    if (name != null && !groupOf.ContainsKey(name)) groupOf.Add(name, g);
            }

            // 그룹마다 남은 줄 (히트맵 차례 그대로). 마지막 칸이 그룹에 없는 IO.
            var buckets = new List<HeatRow>[groupCount + 1];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<HeatRow>();

            for (int i = 0; i < rows.Count; i++)
            {
                HeatRow row = rows[i];
                if (row == null) continue;
                if (!NameSearch.Matches(row.Name, search)) continue;

                int g;
                buckets[groupOf.TryGetValue(row.Name, out g) ? g : groupCount].Add(row);
            }

            for (int g = 0; g <= groupCount; g++)
            {
                List<HeatRow> members = buckets[g];
                if (members.Count == 0) continue;

                string name = g < groupCount ? groups[g].Name : Ungrouped;
                bool shut = collapsed != null && collapsed.Contains(name);
                lines.Add(HeatLine.Head(name, members.Count, shut));
                if (shut) continue;
                for (int i = 0; i < members.Count; i++) lines.Add(HeatLine.Io(name, members[i]));
            }
            return lines;
        }

        /// <summary>남은 IO 줄 수 (그룹 줄은 빼고, 접힌 그룹의 IO 는 셉니다).</summary>
        public static int IoCount(List<HeatLine> lines)
        {
            int n = 0;
            if (lines == null) return 0;
            for (int i = 0; i < lines.Count; i++) if (lines[i].IsGroup) n += lines[i].Count;
            return n;
        }
    }
}
