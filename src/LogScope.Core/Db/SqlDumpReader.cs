using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogScope.Core.Db
{
    /// <summary>
    /// <c>.sql</c> 덤프(mysqldump 등)를 읽습니다. <b>라이브러리 없이</b>, 글자를
    /// 직접 훑습니다.
    ///
    /// 보는 것은 둘뿐입니다.
    ///   CREATE TABLE `이름` ( ... );   → 표의 모양
    ///   INSERT INTO `이름` VALUES (…),(…);  → 행
    ///
    /// <b>SQL 을 실행하지 않습니다.</b> 글자를 읽어 표로 만들 뿐이고, 모르는
    /// 문장은 건너뜁니다. 그래서 덤프에 무엇이 적혀 있어도 이 프로그램이
    /// 그걸 수행하는 일은 없습니다.
    ///
    /// 파일이 기가바이트가 될 수 있어 <b>흘려 읽습니다</b> — 문장 하나씩
    /// 끊어서 처리하고, 파일 전체를 글자로 올리지 않습니다.
    ///
    /// 못 읽는 것:
    ///  - <c>INSERT ... SET a=1</c> 꼴 (mysqldump 는 안 씁니다)
    ///  - 프로시저·트리거·뷰 본문 (표가 아니라 건너뜁니다)
    ///  - 열 목록이 붙은 <c>INSERT INTO t (a,b) VALUES</c> 는 그 목록을 그대로 씁니다
    /// </summary>
    public static class SqlDumpReader
    {
        /// <summary>표 하나에서 읽을 행 수 상한. 넘으면 거기까지만 읽고 밝힙니다.</summary>
        public const int MaxRowsPerTable = 200000;

        public static void Read(string path, DbSnapshot into, int maxRows)
        {
            if (into == null) throw new ArgumentNullException("into");
            if (maxRows <= 0) maxRows = MaxRowsPerTable;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 1 << 16))
            {
                Encoding enc = Sniff(fs);
                fs.Position = 0;
                using (var sr = new StreamReader(fs, enc, true, 1 << 16))
                {
                    foreach (string stmt in Statements(sr))
                    {
                        TakeStatement(stmt, into, maxRows, path);
                    }
                }
            }
        }

        /// <summary>BOM 이 있으면 따르고, 없으면 UTF-8 로 봅니다 (덤프 기본).</summary>
        private static Encoding Sniff(FileStream fs)
        {
            byte[] head = new byte[3];
            int n = fs.Read(head, 0, 3);
            if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new UTF8Encoding(true);
            return new UTF8Encoding(false);
        }

        /// <summary>
        /// 세미콜론으로 문장을 끊습니다. <b>따옴표와 주석 안의 세미콜론은
        /// 끊지 않습니다</b> — 값에 세미콜론이 들어 있는 것은 흔합니다.
        /// </summary>
        private static IEnumerable<string> Statements(TextReader sr)
        {
            var sb = new StringBuilder(4096);
            char quote = '\0';
            bool escape = false, lineComment = false, blockComment = false;
            int c;

            while ((c = sr.Read()) >= 0)
            {
                char ch = (char)c;

                if (lineComment)
                {
                    if (ch == '\n') lineComment = false;
                    continue;
                }
                if (blockComment)
                {
                    if (ch == '*' && sr.Peek() == '/') { sr.Read(); blockComment = false; }
                    continue;
                }

                if (quote != '\0')
                {
                    sb.Append(ch);
                    if (escape) { escape = false; continue; }
                    if (ch == '\\' && quote != '`') { escape = true; continue; }
                    if (ch == quote)
                    {
                        // 따옴표를 두 번 쓴 것은 글자 하나입니다 ('' 또는 ``).
                        if (sr.Peek() == quote) { sb.Append((char)sr.Read()); continue; }
                        quote = '\0';
                    }
                    continue;
                }

                if (ch == '-' && sr.Peek() == '-') { sr.Read(); lineComment = true; continue; }
                if (ch == '#') { lineComment = true; continue; }
                if (ch == '/' && sr.Peek() == '*') { sr.Read(); blockComment = true; continue; }

                if (ch == '\'' || ch == '"' || ch == '`') { quote = ch; sb.Append(ch); continue; }

                if (ch == ';')
                {
                    string s = sb.ToString().Trim();
                    sb.Length = 0;
                    if (s.Length > 0) yield return s;
                    continue;
                }

                sb.Append(ch);
            }

            string last = sb.ToString().Trim();
            if (last.Length > 0) yield return last;
        }

        private static void TakeStatement(string stmt, DbSnapshot into, int maxRows, string path)
        {
            if (StartsWith(stmt, "CREATE TABLE"))
            {
                DbTable t = ParseCreate(stmt);
                if (t == null) return;

                DbTable old = into.Find(t.Name);
                if (old != null) into.Tables.Remove(old);   // 덤프에 두 번 있으면 뒤의 것
                t.Note = Path.GetFileName(path);
                into.Tables.Add(t);
                return;
            }

            if (StartsWith(stmt, "INSERT INTO") || StartsWith(stmt, "REPLACE INTO"))
            {
                ParseInsert(stmt, into, maxRows);
            }
        }

        private static bool StartsWith(string s, string head)
        {
            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (s.Length - i < head.Length) return false;

            // 가운데 공백 수가 달라도 같은 문장입니다.
            int j = 0;
            while (j < head.Length)
            {
                if (head[j] == ' ')
                {
                    if (i >= s.Length || !char.IsWhiteSpace(s[i])) return false;
                    while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                    j++;
                    continue;
                }
                if (i >= s.Length || char.ToUpperInvariant(s[i]) != head[j]) return false;
                i++; j++;
            }
            return true;
        }

        // ---------------- CREATE TABLE ----------------

        private static DbTable ParseCreate(string stmt)
        {
            int open = stmt.IndexOf('(');
            if (open < 0) return null;

            var t = new DbTable();
            t.Name = LastName(stmt.Substring(0, open));
            if (t.Name.Length == 0) return null;

            int close = MatchParen(stmt, open);
            if (close < 0) close = stmt.Length;
            string body = stmt.Substring(open + 1, Math.Max(0, close - open - 1));

            var keys = new List<string>();
            foreach (string part in SplitTop(body))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;

                if (StartsWith(p, "PRIMARY KEY"))
                {
                    foreach (string k in NamesInParens(p)) keys.Add(k);
                    continue;
                }
                // 다른 키·제약은 모양 비교에 넣지 않습니다. 열 차이를 보는 것이
                // 목적이고, 인덱스까지 보면 거짓 차이가 많아집니다.
                if (StartsWith(p, "KEY") || StartsWith(p, "UNIQUE") || StartsWith(p, "INDEX")
                 || StartsWith(p, "CONSTRAINT") || StartsWith(p, "FOREIGN KEY")
                 || StartsWith(p, "FULLTEXT") || StartsWith(p, "SPATIAL")
                 || StartsWith(p, "CHECK")) continue;

                DbColumn col = ParseColumn(p, t.Columns.Count);
                if (col != null) t.Columns.Add(col);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                DbColumn c = t.Find(keys[i]);
                if (c != null) c.IsKey = true;
            }
            return t;
        }

        /// <summary>
        /// 열 한 줄: <c>`이름` 타입 NOT NULL DEFAULT '0' ...</c>
        ///
        /// 타입은 <b>적힌 그대로</b> 둡니다. 우리가 고쳐 쓰면 "같은 타입인데
        /// 다르다" 는 거짓 차이가 생깁니다.
        /// </summary>
        private static DbColumn ParseColumn(string part, int ordinal)
        {
            int i = 0;
            string name = ReadName(part, ref i);
            if (name.Length == 0) return null;

            var col = new DbColumn();
            col.Name = name;
            col.Ordinal = ordinal;

            // 타입: 괄호를 세며 다음 공백까지. decimal(6,2) 가 한 덩어리입니다.
            while (i < part.Length && char.IsWhiteSpace(part[i])) i++;
            var type = new StringBuilder();
            int depth = 0;
            while (i < part.Length)
            {
                char c = part[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (char.IsWhiteSpace(c) && depth == 0) break;
                type.Append(c);
                i++;
            }
            // unsigned 처럼 타입에 붙는 말은 같이 둡니다.
            string rest = part.Substring(Math.Min(i, part.Length));
            string restUp = rest.ToUpperInvariant();
            if (restUp.Contains(" UNSIGNED")) type.Append(" unsigned");
            if (restUp.Contains(" ZEROFILL")) type.Append(" zerofill");
            col.Type = type.ToString();

            col.Nullable = !restUp.Contains("NOT NULL");
            col.Default = ReadDefault(rest);
            return col;
        }

        private static string ReadDefault(string rest)
        {
            int at = IndexOfWord(rest, "DEFAULT");
            if (at < 0) return null;

            int i = at + 7;
            while (i < rest.Length && char.IsWhiteSpace(rest[i])) i++;
            if (i >= rest.Length) return null;

            if (rest[i] == '\'' || rest[i] == '"')
            {
                char q = rest[i++];
                var sb = new StringBuilder();
                while (i < rest.Length)
                {
                    char c = rest[i];
                    if (c == '\\' && i + 1 < rest.Length) { sb.Append(c).Append(rest[i + 1]); i += 2; continue; }
                    if (c == q) { i++; break; }
                    sb.Append(c); i++;
                }
                return "'" + sb + "'";
            }

            var w = new StringBuilder();
            int depth = 0;
            while (i < rest.Length)
            {
                char c = rest[i];
                if (c == '(') depth++;
                else if (c == ')') { if (depth == 0) break; depth--; }
                else if (char.IsWhiteSpace(c) && depth == 0) break;
                w.Append(c); i++;
            }
            string v = w.ToString();
            return v.Length == 0 ? null : v;
        }

        /// <summary>낱말 단위로 찾습니다. ON UPDATE 의 "UPDATE" 를 DEFAULT 로 보지 않게.</summary>
        private static int IndexOfWord(string s, string word)
        {
            int from = 0;
            while (true)
            {
                int at = s.IndexOf(word, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return -1;
                bool left = at == 0 || !char.IsLetterOrDigit(s[at - 1]);
                int end = at + word.Length;
                bool right = end >= s.Length || !char.IsLetterOrDigit(s[end]);
                if (left && right) return at;
                from = at + 1;
            }
        }

        // ---------------- INSERT ----------------

        private static void ParseInsert(string stmt, DbSnapshot into, int maxRows)
        {
            int at = IndexOfWord(stmt, "INTO");
            if (at < 0) return;
            int i = at + 4;

            while (i < stmt.Length && char.IsWhiteSpace(stmt[i])) i++;
            string table = ReadName(stmt, ref i);
            if (table.Length == 0) return;

            DbTable t = into.Find(table);
            if (t == null)
            {
                // CREATE 없이 INSERT 만 있는 덤프도 있습니다. 그때는 열 이름을
                // 모르니 "열1, 열2 …" 로 두고, 그렇다고 밝힙니다.
                t = new DbTable();
                t.Name = table;
                t.Note = "CREATE TABLE 이 없어 열 이름을 모릅니다";
                into.Tables.Add(t);
            }

            // 열 목록이 붙어 있으면 그 순서대로 넣습니다.
            List<string> named = null;
            while (i < stmt.Length && char.IsWhiteSpace(stmt[i])) i++;
            if (i < stmt.Length && stmt[i] == '(')
            {
                int close = MatchParen(stmt, i);
                if (close > i)
                {
                    named = new List<string>();
                    foreach (string n in NamesInParens(stmt.Substring(i, close - i + 1))) named.Add(n);
                    i = close + 1;
                }
            }

            int valuesAt = IndexOfWord(stmt, "VALUES");
            if (valuesAt < i) valuesAt = stmt.IndexOf("VALUES", i, StringComparison.OrdinalIgnoreCase);
            if (valuesAt < 0) return;
            i = valuesAt + 6;

            while (i < stmt.Length)
            {
                while (i < stmt.Length && (char.IsWhiteSpace(stmt[i]) || stmt[i] == ',')) i++;
                if (i >= stmt.Length || stmt[i] != '(') break;

                int close = MatchParen(stmt, i);
                if (close < 0) break;

                List<string> vals = ReadValues(stmt, i + 1, close);
                i = close + 1;

                if (t.Rows.Count >= maxRows)
                {
                    if (!t.Note.Contains("상한"))
                        t.Note = (t.Note.Length > 0 ? t.Note + " / " : "")
                               + "행 " + maxRows.ToString("N0") + " 개까지만 읽었습니다 (상한)";
                    continue;
                }

                EnsureColumns(t, named, vals.Count);
                var row = new DbRow(t.Columns.Count);
                if (named != null)
                {
                    for (int k = 0; k < named.Count && k < vals.Count; k++)
                    {
                        int at2 = t.IndexOf(named[k]);
                        if (at2 >= 0) row.Values[at2] = vals[k];
                    }
                }
                else
                {
                    for (int k = 0; k < vals.Count && k < row.Values.Length; k++) row.Values[k] = vals[k];
                }
                t.Rows.Add(row);
                t.HasRows = true;
            }
        }

        /// <summary>열 수를 모르던 표에 자리를 만들어 줍니다.</summary>
        private static void EnsureColumns(DbTable t, List<string> named, int count)
        {
            if (named != null)
            {
                for (int k = 0; k < named.Count; k++)
                {
                    if (t.IndexOf(named[k]) >= 0) continue;
                    var c = new DbColumn();
                    c.Name = named[k];
                    c.Ordinal = t.Columns.Count;
                    t.Columns.Add(c);
                }
                return;
            }

            while (t.Columns.Count < count)
            {
                var c = new DbColumn();
                c.Name = "열" + (t.Columns.Count + 1);
                c.Ordinal = t.Columns.Count;
                t.Columns.Add(c);
            }
        }

        /// <summary>
        /// 괄호 한 벌의 값들. 따옴표 안의 쉼표는 자르지 않습니다.
        /// <c>NULL</c> 은 null 로, 따옴표 글자는 <b>원래 글자</b>로 되돌립니다
        /// (<c>\'</c> → <c>'</c>). 그렇게 해야 두 덤프가 같은 값을 다르게
        /// 적어 둔 것(이스케이프 차이)을 차이로 세지 않습니다.
        /// </summary>
        private static List<string> ReadValues(string s, int from, int end)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            int i = from;
            bool any = false;

            while (i < end)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c) && sb.Length == 0) { i++; continue; }

                if (c == '\'' || c == '"')
                {
                    char q = c; i++;
                    var v = new StringBuilder();
                    while (i < end)
                    {
                        char d = s[i];
                        if (d == '\\' && i + 1 < end) { v.Append(Unescape(s[i + 1])); i += 2; continue; }
                        if (d == q)
                        {
                            if (i + 1 < end && s[i + 1] == q) { v.Append(q); i += 2; continue; }
                            i++; break;
                        }
                        v.Append(d); i++;
                    }
                    sb.Append(v);
                    any = true;
                    // 값 끝까지 (쉼표까지) 건너뜁니다.
                    while (i < end && s[i] != ',') i++;
                    list.Add(sb.ToString());
                    sb.Length = 0; any = false;
                    if (i < end) i++;
                    continue;
                }

                // 따옴표 없는 값: 숫자, NULL, 함수 호출 등
                var w = new StringBuilder();
                int depth = 0;
                while (i < end)
                {
                    char d = s[i];
                    if (d == '(') depth++;
                    else if (d == ')') depth--;
                    else if (d == ',' && depth == 0) break;
                    w.Append(d); i++;
                }
                string raw = w.ToString().Trim();
                list.Add(string.Equals(raw, "NULL", StringComparison.OrdinalIgnoreCase) ? null : raw);
                if (i < end) i++;
                any = false;
            }

            if (any) list.Add(sb.ToString());
            return list;
        }

        private static char Unescape(char c)
        {
            switch (c)
            {
                case '0': return '\0';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'b': return '\b';
                case 'Z': return '\u001a';
                default: return c;      // \' \" \\ \% \_ 등은 그 글자 그대로
            }
        }

        // ---------------- 이름과 괄호 ----------------

        /// <summary>`이름` 또는 "이름" 또는 맨 이름. db.table 이면 뒤쪽만.</summary>
        private static string ReadName(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return string.Empty;

            var sb = new StringBuilder();
            if (s[i] == '`' || s[i] == '"' || s[i] == '[')
            {
                char open = s[i];
                char close = open == '[' ? ']' : open;
                i++;
                while (i < s.Length)
                {
                    if (s[i] == close)
                    {
                        if (i + 1 < s.Length && s[i + 1] == close) { sb.Append(close); i += 2; continue; }
                        i++; break;
                    }
                    sb.Append(s[i]); i++;
                }
            }
            else
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '$'))
                { sb.Append(s[i]); i++; }
            }

            // db.table 꼴이면 뒤쪽 이름을 씁니다.
            if (i < s.Length && s[i] == '.')
            {
                i++;
                return ReadName(s, ref i);
            }
            return sb.ToString();
        }

        /// <summary>CREATE TABLE 머리에서 표 이름만.</summary>
        private static string LastName(string head)
        {
            int i = IndexOfWord(head, "TABLE");
            if (i < 0) return string.Empty;
            i += 5;

            // IF NOT EXISTS 는 건너뜁니다.
            int ine = IndexOfWord(head.Substring(i), "EXISTS");
            if (ine >= 0 && ine < 20) i += ine + 6;

            return ReadName(head, ref i);
        }

        private static int MatchParen(string s, int open)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == '\\' && quote != '`') { i++; continue; }
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`') { quote = c; continue; }
                if (c == '(') depth++;
                else if (c == ')') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        /// <summary>괄호 안의 쉼표로 나눕니다. 안쪽 괄호와 따옴표는 지킵니다.</summary>
        private static List<string> SplitTop(string body)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            char quote = '\0';

            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (quote != '\0')
                {
                    sb.Append(c);
                    if (c == '\\' && quote != '`' && i + 1 < body.Length) { sb.Append(body[++i]); continue; }
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`') { quote = c; sb.Append(c); continue; }
                if (c == '(') { depth++; sb.Append(c); continue; }
                if (c == ')') { depth--; sb.Append(c); continue; }
                if (c == ',' && depth == 0) { list.Add(sb.ToString()); sb.Length = 0; continue; }
                sb.Append(c);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        /// <summary>(`a`,`b`) → a, b</summary>
        private static List<string> NamesInParens(string part)
        {
            var list = new List<string>();
            int open = part.IndexOf('(');
            if (open < 0) return list;
            int close = MatchParen(part, open);
            if (close < 0) close = part.Length - 1;

            foreach (string one in SplitTop(part.Substring(open + 1, Math.Max(0, close - open - 1))))
            {
                int i = 0;
                string n = ReadName(one, ref i);
                if (n.Length > 0) list.Add(n);
            }
            return list;
        }
    }
}
