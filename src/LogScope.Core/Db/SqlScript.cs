using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LogScope.Core.Db
{
    /// <summary>
    /// 차이를 <b>SQL 글자</b>로 적어 줍니다.
    ///
    /// <b>이 프로그램은 DB 에 접속하지 않고, 이 글을 실행하지도 않습니다.</b>
    /// 화면에 보여 주고 파일로 저장할 뿐입니다. 이유가 둘 있습니다.
    ///
    ///   1. 접속에는 바깥 라이브러리가 필요합니다 (안 쓰기로 한 것).
    ///   2. 무엇보다, 한 번 잘못 돌리면 되돌릴 수 없습니다. 지금까지 이
    ///      프로그램은 읽기만 했습니다. 그 선을 넘는 것은 따로 정할 일입니다.
    ///
    /// 그래서 <b>사람이 읽고 직접 돌리는 글</b>로 만듭니다. 위험한 줄은
    /// 주석으로 표시해 두고, 지울지 말지는 보는 사람이 정합니다.
    ///
    /// 방향은 "이전 → 이후" 가 아니라 <b>"고를 수 있게"</b> 둡니다.
    ///   ToAfter  : 이전 DB 를 이후와 같게 (보통 "새 것으로 맞추기")
    ///   ToBefore : 이후 DB 를 이전과 같게 (되돌리기)
    /// </summary>
    public static class SqlScript
    {
        public enum Direction
        {
            /// <summary>이전 DB 를 이후처럼 바꾸는 글.</summary>
            ToAfter = 0,
            /// <summary>이후 DB 를 이전처럼 바꾸는 글 (되돌리기).</summary>
            ToBefore = 1,
        }

        public sealed class Options
        {
            public Direction Way = Direction.ToAfter;

            /// <summary>표를 지우거나 만드는 문장도 넣을지. 기본은 주석으로만.</summary>
            public bool IncludeTableCreateDrop;

            /// <summary>한 표에서 적어 둘 행 문장 수 상한.</summary>
            public int MaxRowStatements = 2000;
        }

        public static string Build(DbDiffResult diff, Options opt)
        {
            if (opt == null) opt = new Options();
            var sb = new StringBuilder();

            bool toAfter = opt.Way == Direction.ToAfter;

            sb.AppendLine("-- " + (toAfter ? "이전 DB 를 이후와 같게 만드는 글"
                                           : "이후 DB 를 이전과 같게 되돌리는 글"));
            sb.AppendLine("--");
            sb.AppendLine("-- 이 글은 LogScope 가 \"두 DB 의 차이\" 를 보고 적은 것입니다.");
            sb.AppendLine("-- 프로그램이 직접 돌리지 않습니다. 읽어 보고 직접 돌리세요.");
            sb.AppendLine("-- 돌리기 전에 백업을 받으시고, 트랜잭션으로 묶어 확인하세요.");
            sb.AppendLine("--");
            sb.AppendLine("--   START TRANSACTION;  ... 확인 ...  COMMIT;  (아니면 ROLLBACK;)");
            sb.AppendLine();

            if (diff == null || diff.Tables.Count == 0)
            {
                sb.AppendLine("-- 견준 표가 없습니다.");
                return sb.ToString();
            }

            int wrote = 0;
            for (int i = 0; i < diff.Tables.Count; i++)
            {
                TableDiff td = diff.Tables[i];
                if (td.Change == DbChange.Same) continue;

                sb.AppendLine("-- ======================================================");
                sb.AppendLine("-- 표: " + td.Name + "   (" + td.Summary() + ")");
                if (td.RowNote.Length > 0) sb.AppendLine("-- " + td.RowNote);
                sb.AppendLine("-- ======================================================");

                wrote += Table(sb, td, opt, toAfter);
                sb.AppendLine();
            }

            if (wrote == 0) sb.AppendLine("-- 바꿀 것이 없습니다.");
            return sb.ToString();
        }

        private static int Table(StringBuilder sb, TableDiff td, Options opt, bool toAfter)
        {
            int n = 0;

            // ---- 표 자체가 한쪽에만 있는 경우 ----
            bool onlyHere = (toAfter && td.Change == DbChange.OnlyBefore)
                         || (!toAfter && td.Change == DbChange.OnlyAfter);
            bool onlyThere = (toAfter && td.Change == DbChange.OnlyAfter)
                          || (!toAfter && td.Change == DbChange.OnlyBefore);

            if (onlyHere)
            {
                // 지우는 문장은 <b>늘 주석</b>으로 둡니다. 표를 지우는 일을
                // 글만 보고 돌리게 하면 안 됩니다.
                sb.AppendLine("-- 이 표는 한쪽에만 있습니다. 지우려면 아래 주석을 풀어 주세요.");
                sb.AppendLine("-- DROP TABLE " + Quote(td.Name) + ";");
                return 0;
            }
            if (onlyThere)
            {
                sb.AppendLine("-- 이 표가 없습니다. 만들어야 합니다.");
                sb.AppendLine("-- " + CreateHint(td));
                if (opt.IncludeTableCreateDrop)
                {
                    DbTable src = toAfter ? td.After : td.Before;
                    if (src != null) { sb.AppendLine(Create(src)); n++; }
                }
                return n;
            }

            // ---- 열 ----
            for (int i = 0; i < td.Columns.Count; i++)
            {
                ColumnDiff cd = td.Columns[i];
                if (cd.Change == DbChange.Same) continue;
                n += Column(sb, td, cd, toAfter);
            }

            // ---- 행 ----
            n += Rows(sb, td, opt, toAfter);
            return n;
        }

        private static int Column(StringBuilder sb, TableDiff td, ColumnDiff cd, bool toAfter)
        {
            string table = Quote(td.Name);

            // 이름이 바뀐 것으로 보이는 짝은 <b>주석으로만</b> 내놓습니다.
            // CHANGE 로 돌려 버리면, 짐작이 틀렸을 때 다른 열의 값이 사라집니다.
            if (cd.RenameGuess != null)
            {
                DbColumn from = toAfter ? cd.Before : cd.After;
                string to = cd.RenameGuess;
                if (from == null) return 0;      // 짝의 다른 쪽에서 이미 적었습니다

                sb.AppendLine("-- 이름이 바뀐 것으로 보입니다 (단정할 수 없습니다). 맞다면:");
                sb.AppendLine("--   ALTER TABLE " + table + " CHANGE " + Quote(from.Name)
                              + " " + Quote(to) + " " + Spec(from) + ";");
                sb.AppendLine("-- 아니라면 아래 둘로 나누어 돌리세요:");
                sb.AppendLine("--   ALTER TABLE " + table + " DROP COLUMN " + Quote(from.Name) + ";");
                sb.AppendLine("--   ALTER TABLE " + table + " ADD COLUMN " + Quote(to) + " ...;");
                return 0;
            }

            bool missingHere = (toAfter && cd.Change == DbChange.OnlyAfter)
                            || (!toAfter && cd.Change == DbChange.OnlyBefore);
            bool extraHere = (toAfter && cd.Change == DbChange.OnlyBefore)
                          || (!toAfter && cd.Change == DbChange.OnlyAfter);

            if (missingHere)
            {
                DbColumn want = toAfter ? cd.After : cd.Before;
                sb.AppendLine("ALTER TABLE " + table + " ADD COLUMN " + Quote(want.Name)
                              + " " + Spec(want) + After(want) + ";");
                return 1;
            }

            if (extraHere)
            {
                // 열을 지우면 그 안의 값이 다 사라집니다. 주석으로 둡니다.
                sb.AppendLine("-- 이 열은 한쪽에만 있습니다. 지우면 값이 사라집니다:");
                sb.AppendLine("-- ALTER TABLE " + table + " DROP COLUMN " + Quote(cd.Name) + ";");
                return 0;
            }

            if (cd.Change == DbChange.Changed)
            {
                DbColumn want = toAfter ? cd.After : cd.Before;
                if (cd.MovedOnly && !cd.TypeChanged && !cd.NullChanged && !cd.DefaultChanged)
                {
                    // 자리만 다른 것은 고치지 않아도 값에는 영향이 없습니다.
                    sb.AppendLine("-- 열 자리만 다릅니다 (" + cd.What() + "). 값에는 영향이 없습니다:");
                    sb.AppendLine("-- ALTER TABLE " + table + " MODIFY COLUMN " + Quote(want.Name)
                                  + " " + Spec(want) + After(want) + ";");
                    return 0;
                }

                sb.AppendLine("ALTER TABLE " + table + " MODIFY COLUMN " + Quote(want.Name)
                              + " " + Spec(want) + ";");
                return 1;
            }
            return 0;
        }

        private static int Rows(StringBuilder sb, TableDiff td, Options opt, bool toAfter)
        {
            if (!td.RowsDiffer) return 0;

            DbTable here = toAfter ? td.Before : td.After;
            DbTable there = toAfter ? td.After : td.Before;
            if (here == null || there == null) return 0;

            if (!here.HasRows || !there.HasRows)
            {
                sb.AppendLine("-- 행을 읽지 못해 값 문장은 만들지 않았습니다.");
                return 0;
            }

            bool keyed = here.HasKey && there.HasKey;
            if (!keyed)
            {
                sb.AppendLine("-- 기본 키가 없어 UPDATE 를 만들 수 없습니다.");
                sb.AppendLine("-- (어느 행을 가리킬지 적을 방법이 없습니다. 아래는 지우고 다시 넣는 꼴입니다.)");
            }

            int n = 0;
            for (int i = 0; i < td.Rows.Count; i++)
            {
                if (n >= opt.MaxRowStatements)
                {
                    sb.AppendLine("-- 문장이 " + opt.MaxRowStatements.ToString("N0")
                                  + " 개를 넘어 여기까지만 적었습니다 (상한).");
                    break;
                }

                RowDiff rd = td.Rows[i];
                DbRow want = toAfter ? rd.After : rd.Before;
                DbRow have = toAfter ? rd.Before : rd.After;

                // 이 방향에서 "없는 행" 이면 넣고, "남는 행" 이면 지웁니다.
                if (want == null)
                {
                    sb.AppendLine("-- 한쪽에만 있는 행입니다. 지우려면 주석을 풀어 주세요:");
                    sb.AppendLine("-- DELETE FROM " + Quote(td.Name) + Where(there, have, keyed) + ";");
                    continue;
                }
                if (have == null)
                {
                    sb.AppendLine(Insert(there, td.Name, want));
                    n++;
                    continue;
                }

                if (!keyed)
                {
                    sb.AppendLine("-- 값이 바뀐 행 (키가 없어 UPDATE 를 만들지 못했습니다):");
                    sb.AppendLine("--   이전: " + Join(here, have));
                    sb.AppendLine("--   이후: " + Join(there, want));
                    continue;
                }

                string set = Set(there, want, rd.ChangedColumns, here);
                if (set.Length == 0) continue;
                sb.AppendLine("UPDATE " + Quote(td.Name) + " SET " + set
                              + Where(there, want, true) + ";");
                n++;
            }
            return n;
        }

        // ---------------- 글 만들기 ----------------

        private static string Spec(DbColumn c)
        {
            var sb = new StringBuilder();
            sb.Append(c.Type.Length > 0 ? c.Type : "text");
            sb.Append(c.Nullable ? " NULL" : " NOT NULL");
            if (c.Default != null) sb.Append(" DEFAULT ").Append(c.Default);
            return sb.ToString();
        }

        private static string After(DbColumn c)
        {
            return c.Ordinal == 0 ? " FIRST" : string.Empty;
        }

        private static string CreateHint(TableDiff td)
        {
            DbTable t = td.After ?? td.Before;
            if (t == null) return "CREATE TABLE ...;";
            return "CREATE TABLE " + Quote(t.Name) + " (열 " + t.Columns.Count + " 개) ...";
        }

        private static string Create(DbTable t)
        {
            var sb = new StringBuilder();
            sb.Append("CREATE TABLE ").Append(Quote(t.Name)).AppendLine(" (");
            var keys = new List<string>();
            for (int i = 0; i < t.Columns.Count; i++)
            {
                DbColumn c = t.Columns[i];
                sb.Append("  ").Append(Quote(c.Name)).Append(' ').Append(Spec(c));
                if (i < t.Columns.Count - 1 || t.HasKey) sb.Append(',');
                sb.AppendLine();
                if (c.IsKey) keys.Add(Quote(c.Name));
            }
            if (keys.Count > 0) sb.Append("  PRIMARY KEY (").Append(string.Join(", ", keys.ToArray())).AppendLine(")");
            sb.Append(");");
            return sb.ToString();
        }

        private static string Insert(DbTable shape, string table, DbRow row)
        {
            var cols = new List<string>();
            var vals = new List<string>();
            for (int i = 0; i < shape.Columns.Count; i++)
            {
                cols.Add(Quote(shape.Columns[i].Name));
                vals.Add(Literal(row.Get(i)));
            }
            return "INSERT INTO " + Quote(table) + " (" + string.Join(", ", cols.ToArray())
                 + ") VALUES (" + string.Join(", ", vals.ToArray()) + ");";
        }

        private static string Set(DbTable shape, DbRow want, List<int> changed, DbTable from)
        {
            var parts = new List<string>();
            for (int k = 0; k < changed.Count; k++)
            {
                int ci = changed[k];
                if (ci < 0 || ci >= from.Columns.Count) continue;
                string name = from.Columns[ci].Name;
                int at = shape.IndexOf(name);
                if (at < 0) continue;
                parts.Add(Quote(name) + " = " + Literal(want.Get(at)));
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string Where(DbTable shape, DbRow row, bool keyed)
        {
            if (row == null) return string.Empty;

            int[] keys = shape.KeyIndexes();
            var parts = new List<string>();

            if (keyed && keys.Length > 0)
            {
                for (int k = 0; k < keys.Length; k++)
                {
                    string v = row.Get(keys[k]);
                    parts.Add(Quote(shape.Columns[keys[k]].Name) + Eq(v));
                }
            }
            else
            {
                // 키가 없으면 모든 열로 가리킵니다. 같은 행이 여럿이면 다 바뀌므로
                // 이 꼴은 주석으로만 내놓습니다 (위 Rows 참고).
                for (int i = 0; i < shape.Columns.Count; i++)
                {
                    parts.Add(Quote(shape.Columns[i].Name) + Eq(row.Get(i)));
                }
            }

            if (parts.Count == 0) return string.Empty;
            return " WHERE " + string.Join(" AND ", parts.ToArray());
        }

        private static string Eq(string v)
        {
            return v == null ? " IS NULL" : " = " + Literal(v);
        }

        private static string Join(DbTable shape, DbRow row)
        {
            var parts = new List<string>();
            for (int i = 0; i < shape.Columns.Count && i < row.Values.Length; i++)
                parts.Add(shape.Columns[i].Name + "=" + DbRow.Show(row.Get(i)));
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// 값 하나를 SQL 글자로. <b>따옴표와 역슬래시를 반드시 막습니다</b> —
        /// 값에 <c>'</c> 가 들어 있으면 글이 깨지고, 깨진 글을 사람이 돌리면
        /// 엉뚱한 문장이 됩니다.
        /// </summary>
        public static string Literal(string v)
        {
            if (v == null) return "NULL";

            // 숫자는 따옴표 없이. 숫자처럼 보이는 글자도 숫자로 둡니다 —
            // 어차피 DB 가 열 타입대로 받습니다.
            if (IsNumber(v)) return v;

            var sb = new StringBuilder(v.Length + 8);
            sb.Append('\'');
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                switch (c)
                {
                    case '\'': sb.Append("\\'"); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\u001a': sb.Append("\\Z"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('\'');
            return sb.ToString();
        }

        private static bool IsNumber(string v)
        {
            if (v.Length == 0) return false;
            double d;
            return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        /// <summary>
        /// 이름을 역따옴표로 감쌉니다. 이름에 역따옴표가 들어 있으면 두 번
        /// 적습니다 (MySQL 규칙).
        /// </summary>
        public static string Quote(string name)
        {
            if (name == null) name = string.Empty;
            return "`" + name.Replace("`", "``") + "`";
        }
    }
}
