using System;
using System.Collections.Generic;
using System.Text;

namespace LogScope.Core.Db
{
    /// <summary>
    /// 읽은 표의 모양을 <c>CREATE TABLE</c> 글로 적습니다.
    ///
    /// <b>쓰임이 둘입니다.</b>
    ///
    /// 1. <b>확인용.</b> <c>.frm</c> 을 우리가 제대로 읽었는지 보는 가장 빠른
    ///    길입니다. 서버에서 <c>SHOW CREATE TABLE</c> 을 찍어 이 글과 나란히
    ///    놓고 보면, 열 이름·타입·순서가 맞는지 한눈에 드러납니다.
    ///
    /// 2. <b>받는 쪽 표를 만들 때의 출발점.</b> 값을 옮기려면 받는 서버에 같은
    ///    표가 먼저 있어야 합니다 (transportable tablespace).
    ///
    /// <b>그대로 쓰면 안 되는 글입니다.</b> 빠진 것이 있습니다 —
    /// 기본값(<c>.frm</c> 에서 안 읽습니다), <c>AUTO_INCREMENT</c>, 문자셋과
    /// <c>COLLATE</c>, 외래 키, <b>기본 키가 아닌 색인</b>, 엔진과 행 포맷.
    /// 머리에 그렇다고 적어 둡니다. 모르는 것을 아는 척 적는 쪽이 더
    /// 나쁩니다 — 그 글을 믿고 돌리면 엉뚱한 표가 생깁니다.
    /// </summary>
    public static class DbCreateSql
    {
        /// <summary>두 쪽을 한 글로. 한 파일로 저장해 나란히 보게 합니다.</summary>
        public static string Both(DbSnapshot before, DbSnapshot after)
        {
            var sb = new StringBuilder();
            Head(sb);
            sb.Append(Build(before, "이전"));
            sb.AppendLine();
            sb.Append(Build(after, "이후"));
            return sb.ToString();
        }

        private static void Head(StringBuilder sb)
        {
            sb.AppendLine("-- 읽은 표의 모양입니다. LogScope 가 적었습니다.");
            sb.AppendLine("--");
            sb.AppendLine("-- 그대로 돌리지 마세요. 빠진 것이 있습니다:");
            sb.AppendLine("--   기본값(DEFAULT) · AUTO_INCREMENT · 문자셋/COLLATE ·");
            sb.AppendLine("--   외래 키 · 기본 키가 아닌 색인 · 엔진/행 포맷");
            sb.AppendLine("--");
            sb.AppendLine("-- 쓸 곳");
            sb.AppendLine("--   1. 서버의 SHOW CREATE TABLE 과 나란히 놓고 견주기");
            sb.AppendLine("--      (.frm 을 제대로 읽었는지 보는 가장 빠른 길입니다)");
            sb.AppendLine("--   2. 값을 옮길 때 받는 쪽 표를 만드는 출발점");
            sb.AppendLine();
        }

        /// <summary>한 쪽.</summary>
        public static string Build(DbSnapshot snap, string title)
        {
            var sb = new StringBuilder();
            sb.Append("-- ===== ").Append(title).Append(" ");
            if (snap != null && snap.Source.Length > 0) sb.Append("· ").Append(snap.Source).Append(' ');
            sb.AppendLine("=====");
            sb.AppendLine();

            if (snap == null || snap.Tables.Count == 0)
            {
                sb.AppendLine("-- 읽은 표가 없습니다.");
                return sb.ToString();
            }

            for (int i = 0; i < snap.Tables.Count; i++)
            {
                sb.Append(One(snap.Tables[i]));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>표 하나.</summary>
        public static string One(DbTable t)
        {
            if (t == null) return string.Empty;

            var sb = new StringBuilder();
            if (t.Note.Length > 0) sb.Append("-- ").AppendLine(t.Note);
            if (!t.HasRows) sb.AppendLine("-- 값은 읽지 못했습니다 (모양만).");

            sb.Append("CREATE TABLE ").Append(Quote(t.Name)).AppendLine(" (");

            var lines = new List<string>();
            for (int i = 0; i < t.Columns.Count; i++) lines.Add("  " + Column(t.Columns[i]));

            string keys = Keys(t);
            if (keys.Length > 0) lines.Add("  " + keys);

            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                sb.AppendLine(i + 1 < lines.Count ? "," : string.Empty);
            }
            sb.AppendLine(");");
            return sb.ToString();
        }

        private static string Column(DbColumn c)
        {
            var sb = new StringBuilder();
            sb.Append(Quote(c.Name)).Append(' ');
            sb.Append(c.Type.Length > 0 ? c.Type : "/* 타입을 모릅니다 */");

            if (!c.Nullable) sb.Append(" NOT NULL");

            // 모르는 기본값은 적지 않습니다. "DEFAULT NULL" 로 적으면 모르는
            // 것을 아는 척하는 것이고, 그걸 믿고 돌리면 기본값이 바뀝니다.
            if (c.DefaultKnown && c.Default != null) sb.Append(" DEFAULT ").Append(c.Default);
            else if (!c.DefaultKnown) sb.Append(" /* 기본값 모름 */");

            return sb.ToString();
        }

        private static string Keys(DbTable t)
        {
            int[] keys = t.KeyIndexes();
            if (keys.Length == 0) return string.Empty;

            var sb = new StringBuilder("PRIMARY KEY (");
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Quote(t.Columns[keys[i]].Name));
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>이름을 홑따옴표가 아니라 역따옴표로 감쌉니다. 안의 역따옴표는 두 번.</summary>
        private static string Quote(string name)
        {
            return "`" + (name ?? string.Empty).Replace("`", "``") + "`";
        }
    }
}
