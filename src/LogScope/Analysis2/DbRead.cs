using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LogScope.Common;

// ==========================================================
//  DbRead
//
// 읽기 — 어느 파일을 누가 읽을지, 그리고 .sql / .csv 읽기.
//
// DB 화면을 고칠 때 제일 먼저 볼 파일입니다.
//
//   DbFolderReader  파일 하나 (.sql / .csv) 를 DB 한 벌로 — 확장자로 누가 읽을지 정합니다.
//                   새 확장자를 더하려면 TakeFile 에 한 줄입니다.
//                   (1.90: 폴더 통째 읽기 · .frm · .ibd 는 뺐습니다 — 사용자 결정)
//   SqlDumpReader   .sql 덤프 (CREATE TABLE / INSERT 를 글자로 훑습니다). 사용자 읽기 (SqlUserReader) 가 먼저
//   CsvTableReader  .csv (파일 이름 = 표 이름, 첫 줄 = 열 이름)
//
//  한 파일로 모았습니다 (0.73). 전에는 DbFolderReader · SqlDumpReader · CsvTableReader
//  로 나뉘어 있었습니다 — DB 화면을 고치려면 열 파일을 뒤져야 했습니다.
// ==========================================================

namespace LogScope.Analysis2
{
    // ======================================================
    //  DbFolderReader
    // ======================================================

    /// <summary>
    /// <b>파일 하나</b>를 DB 한 벌로 읽습니다 (이름은 예전 그대로 — 부르는 쪽이 많아서).
    ///
    ///   *.sql   덤프. 한 파일에 표가 여러 개 있을 수 있습니다 (SqlUserReader → 없으면 SqlDumpReader)
    ///   그 밖   .csv 로 읽습니다 (표 하나 — 파일 이름 = 표 이름)
    ///
    /// 1.90: 폴더 통째 읽기, MySQL 내부 파일 (.frm 표 모양 · .ibd 안내), 바깥 프로그램 (mysqldump · ibd2sdi) 을
    /// 뺐습니다. 폴더 · .frm · .ibd 를 주면 <b>조용히 넘기지 않고</b> 못 읽는다고 적습니다 —
    /// 표가 하나도 없으면 "차이 없음" 으로 읽히기 때문입니다.
    /// </summary>
    public static class DbFolderReader
    {
        public sealed class Options
        {
            public int MaxRowsPerTable = SqlDumpReader.MaxRowsPerTable;
        }

        public static DbSnapshot Read(string path, Options opt)
        {
            if (opt == null) opt = new Options();

            var snap = new DbSnapshot();
            snap.Source = path ?? string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                snap.Notes.Add("경로가 비어 있습니다.");
                return snap;
            }

            if (Directory.Exists(path))
            {
                snap.Notes.Add("폴더는 읽지 않습니다 — .sql 이나 .csv 파일 하나를 넣어 주세요: " + path);
                return snap;
            }

            if (!File.Exists(path))
            {
                snap.Notes.Add("파일이 없습니다: " + path);
                return snap;
            }

            TakeFile(path, snap, opt);
            snap.Sort();
            return snap;
        }

        /// <summary>
        /// `.csv` 를 읽으며 생긴 말을 <b>화면까지</b> 올립니다.
        ///
        /// "6 개 열 중 3 번과 4 번만 봤습니다" 같은 말이 표 안에만 있으면
        /// 아무도 못 봅니다 — <b>안 본 열이 있다는 것을 모르고</b> "차이가
        /// 없네" 로 읽게 됩니다. 조용히 덜 보여 주는 것이 이 프로그램에서
        /// 가장 나쁜 고장입니다.
        /// </summary>
        private static void CsvNote(DbSnapshot snap, DbTable t)
        {
            if (t == null || string.IsNullOrEmpty(t.Note)) return;

            int bar = t.Note.IndexOf(" / ", StringComparison.Ordinal);
            if (bar < 0) return;                       // 파일 이름뿐이면 적을 것이 없습니다

            string what = t.Note.Substring(bar + 3);
            foreach (string had in snap.Notes) if (had.EndsWith(what, StringComparison.Ordinal)) return;

            snap.Notes.Add(t.Note.Substring(0, bar) + " — " + what);
        }

        /// <summary>파일 하나를 확장자대로 읽어 snap 에 담습니다. <b>새 확장자를 더하는 자리</b>입니다.</summary>
        private static void TakeFile(string file, DbSnapshot snap, Options opt)
        {
            string ext = (Path.GetExtension(file) ?? string.Empty).ToLowerInvariant();
            try
            {
                if (ext == ".sql")
                {
                    // 1.82: 사용자가 만든 읽기 (SqlUserReader.cs) 가 먼저 — false 면 내장 읽기.
                    // 거기서 어떤 예외가 나도 프로그램은 안 멈추고 그 파일만 "오류" 로 적고 넘어갑니다.
                    var side = new DbSnapshot();
                    bool mine;
                    try { mine = SqlUserReader.Read(file, side, opt.MaxRowsPerTable); }
                    catch (Exception ue)
                    {
                        snap.Notes.Add(Path.GetFileName(file) + " — SqlUserReader 오류 (" + ue.GetType().Name + "): " + ue.Message);
                        return;
                    }
                    if (!mine) SqlDumpReader.Read(file, side, opt.MaxRowsPerTable);
                    for (int i = 0; i < side.Tables.Count; i++)
                    {
                        DbTable one = side.Tables[i];
                        DbTable had = snap.Find(one.Name);
                        if (had != null) snap.Tables.Remove(had);
                        snap.Tables.Add(one);
                    }
                    for (int i = 0; i < side.Notes.Count; i++) snap.Notes.Add(side.Notes[i]);
                    // 1.90: 표가 하나도 없고 아무 말도 없으면 "차이 없음" 으로 읽히므로 그렇다고 적음.
                    if (side.Tables.Count == 0 && side.Notes.Count == 0)
                        snap.Notes.Add(Path.GetFileName(file) + " 에서 표를 찾지 못했습니다 (CREATE TABLE · INSERT 가 없음).");
                    return;
                }

                if (ext == ".frm" || ext == ".ibd")
                {
                    snap.Notes.Add(Path.GetFileName(file) + " — MySQL 내부 파일은 읽지 않습니다 (1.90). "
                                 + "mysqldump 로 만든 .sql 이나 .csv 를 넣어 주세요.");
                    return;
                }

                DbTable t = CsvTableReader.Read(file, opt.MaxRowsPerTable);
                snap.Tables.Add(t);
                CsvNote(snap, t);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is FormatException || e is NotSupportedException)
            {
                snap.Notes.Add(Path.GetFileName(file) + " 를 읽지 못했습니다: " + e.Message);
            }
        }
    }

    // ======================================================
    //  SqlDumpReader
    // ======================================================

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
        /// <summary>
        /// 표 하나에서 읽을 행 수 상한. 넘으면 거기까지만 읽고 밝힙니다.
        /// 1.49: 20 만 → 200 만 — IO 표가 몇만 ~ 몇십만 행일 수 있습니다 (64 비트로 돌아 메모리는 넉넉).
        /// </summary>
        public const int MaxRowsPerTable = 2000000;

        public static void Read(string path, DbSnapshot into, int maxRows)
        {
            if (into == null) throw new ArgumentNullException("into");
            if (maxRows <= 0) maxRows = MaxRowsPerTable;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 1 << 16))
            {
                // 덤프가 CP949(ANSI)일 수 있습니다. 전에는 BOM 이 없으면
                // 무조건 UTF-8 로 읽어서 한글 값이 통째로 깨졌습니다.
                Encoding enc = TextSniff.Of(fs);
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
        ///
        /// <b>16진수로 적힌 값</b>도 되돌립니다. mysqldump 는 글자가 아닌
        /// 바이트가 섞인 열을 <c>0x4D7953</c> 처럼 적고, <c>--hex-blob</c> 을
        /// 주면 늘 그렇게 적습니다. <c>X'4D79'</c> 와 <c>_binary'…'</c> 꼴도
        /// 있습니다. 그대로 두면 화면에 16진수가 줄줄이 나와서 <b>값이
        /// 쓰레기처럼 보입니다.</b>
        ///
        /// 두 가지를 같이 고칩니다.
        ///   1. 바이트가 <b>읽을 수 있는 글자</b>면 그 글자로 보여 줍니다.
        ///   2. 한쪽 덤프가 16진수로, 다른 쪽이 따옴표로 같은 값을 적어
        ///      두었을 때 <b>없는 차이를 만들지 않습니다.</b>
        ///
        /// 글자가 아닌 진짜 바이너리는 <c>0x…</c> 소문자로 맞춰 둡니다 —
        /// 글자로 바꾸는 척하면 안 되고, 길이만 적으면 서로 다른 두
        /// 바이너리가 같아 보입니다.
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

                // _binary'…' · X'…' 꼴은 접두어를 떼고 따옴표부터 읽습니다.
                int pre = Prefix(s, i, end);
                if (pre > 0)
                {
                    bool hex = s[i] == 'X' || s[i] == 'x';
                    i += pre;
                    string body = Quoted(s, ref i, end);
                    if (hex)
                    {
                        string got = Bytes(body);
                        list.Add(got ?? ("X'" + body + "'"));
                    }
                    else list.Add(body);
                    while (i < end && s[i] != ',') i++;
                    if (i < end) i++;
                    continue;
                }

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
                list.Add(string.Equals(raw, "NULL", StringComparison.OrdinalIgnoreCase)
                         ? null : Literal(raw));
                if (i < end) i++;
                any = false;
            }

            if (any) list.Add(sb.ToString());
            return list;
        }

        /// <summary>
        /// <c>_binary'</c> 나 <c>X'</c> 처럼 따옴표 앞에 붙는 접두어의 길이.
        /// 없으면 0 입니다.
        /// </summary>
        private static int Prefix(string s, int i, int end)
        {
            if (i < end && (s[i] == 'X' || s[i] == 'x')
                && i + 1 < end && s[i + 1] == '\'') return 1;

            const string bin = "_binary";
            if (i + bin.Length <= end
                && string.Compare(s, i, bin, 0, bin.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                int j = i + bin.Length;
                while (j < end && char.IsWhiteSpace(s[j])) j++;
                if (j < end && (s[j] == '\'' || s[j] == '"')) return j - i;
            }
            return 0;
        }

        /// <summary>따옴표 한 벌을 읽어 원래 글자로 돌려줍니다. 끝 따옴표 뒤에 섭니다.</summary>
        private static string Quoted(string s, ref int i, int end)
        {
            if (i >= end) return string.Empty;
            char q = s[i]; i++;

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
            return v.ToString();
        }

        /// <summary>
        /// 따옴표가 없는 값. <c>0x4D7953</c> 이면 되돌리고, 나머지는 적힌
        /// 그대로입니다 (숫자 · 함수 호출 등).
        /// </summary>
        private static string Literal(string raw)
        {
            if (raw.Length >= 3 && raw[0] == '0' && (raw[1] == 'x' || raw[1] == 'X'))
            {
                // 16진수가 아니면 null 입니다. 그때는 적힌 그대로 둡니다 —
                // 떼어 낸 "0x" 를 잃으면 값이 조용히 달라집니다.
                string got = Bytes(raw.Substring(2));
                return got ?? raw;
            }
            return raw;
        }

        /// <summary>
        /// 16진수 글자를 바이트로 되돌리고, <b>읽을 수 있는 글자면</b> 그
        /// 글자로 돌려줍니다. 아니면 <c>0x…</c> 소문자로 맞춰 돌려줍니다.
        ///
        /// 16진수가 아닌 글자가 섞여 있으면 손대지 않습니다 — 모르는 것을
        /// 짐작해서 바꾸면 값이 조용히 달라집니다.
        /// </summary>
        private static string Bytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0) return null;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = Nibble(hex[i * 2]);
                int lo = Nibble(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0) return null;      // 16진수가 아닙니다
                bytes[i] = (byte)((hi << 4) | lo);
            }

            string text = AsText(bytes);
            if (text != null) return text;

            var sb = new StringBuilder(2 + bytes.Length * 2);
            sb.Append("0x");
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>
        /// 바이트가 읽을 수 있는 글자인가. 올바른 UTF-8 이고 제어 문자가
        /// 없어야 합니다. 아니면 null (바이너리로 봅니다).
        /// </summary>
        private static string AsText(byte[] bytes)
        {
            if (bytes.Length == 0) return string.Empty;

            // throwOnInvalidBytes 로 <b>끝까지</b> 봅니다. TextSniff 의 검사는
            // 흘려 읽는 앞부분용이라 마지막 글자를 잘라 보므로 여기선 느슨합니다.
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { return null; }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\t' || c == '\n' || c == '\r') continue;
                if (char.IsControl(c)) return null;
            }
            return text;
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

    // ======================================================
    //  CsvTableReader
    // ======================================================

    /// <summary>
    /// 표 하나가 담긴 <c>.csv</c> 를 읽습니다. <b>파일 이름이 표 이름</b>이고
    /// 첫 줄이 열 이름입니다.
    ///
    /// 로그를 읽는 <see cref="CsvReader"/> 를 그대로 씁니다 — 구분자와 인코딩
    /// 판별, 따옴표 안의 줄바꿈 처리가 이미 거기 있습니다. 같은 일을 두 번
    /// 만들면 한쪽만 고치게 됩니다.
    ///
    /// <b>3 번 열을 열쇠로, 4 번 열을 값으로 읽습니다</b> (0.87).
    /// 쓰는 `.csv` 가 이렇게 생겼습니다.
    ///
    /// <code>
    ///   1,2,3,4,5,6         ← 첫 줄은 열 이름
    ///   …,…,IO 이름,값,…,…   ← 둘째 줄부터 자료
    /// </code>
    ///
    /// 그래서 "3 번이 같은 행끼리 짝지어 4 번 값을 견준다" 가 됩니다. 전에는
    /// 열쇠가 없어서 <b>값이 하나 바뀐 행이 "없어진 행 + 생긴 행" 으로</b>
    /// 보였습니다.
    ///
    /// 나머지 열(1 · 2 · 5 · 6)은 <b>읽지 않습니다.</b> 읽어 두면 "안 본
    /// 열인데 왜 차이에 안 나오지" 가 되고, 견주면 묻지 않은 차이가
    /// 쏟아집니다. <b>안 봤다는 것은 화면에 적습니다.</b>
    ///
    /// 자리는 <see cref="DbNames.CsvKeyColumn"/> 한 곳입니다.
    /// </summary>
    public static class CsvTableReader
    {
        public static DbTable Read(string path, int maxRows)
        {
            if (maxRows <= 0) maxRows = SqlDumpReader.MaxRowsPerTable;

            var t = new DbTable();
            t.Name = Path.GetFileNameWithoutExtension(path);
            t.Note = Path.GetFileName(path);

            // rawText: 적힌 글자 그대로 읽습니다. 가려 읽으면 ON 과 1 이
            // 둘 다 참이 되어 <b>서로 다른 값이 같아 보입니다.</b>
            using (var src = new CsvReader(path, true))
            {
                Cell[] cells; int count;

                if (!src.NextRow(out cells, out count))
                {
                    t.Note += " / 빈 파일입니다";
                    return t;
                }

                // 첫 줄로 열을 세웁니다. 어느 자리를 쓸지는 Head 가 정합니다.
                int[] take = Head(t, cells, count);

                while (src.NextRow(out cells, out count))
                {
                    if (t.Rows.Count >= maxRows)
                    {
                        t.Note += " / 행 " + maxRows.ToString("N0") + " 개까지만 읽었습니다 (상한)";
                        break;
                    }

                    if (take != null)
                    {
                        var two = new DbRow(2);
                        for (int i = 0; i < take.Length; i++)
                            two.Values[i] = take[i] < count ? Text(cells[take[i]]) : string.Empty;
                        t.Rows.Add(two);
                        continue;
                    }

                    // 줄이 머리보다 길면 열을 늘립니다. 잘라 버리면 값이 조용히 사라집니다.
                    while (t.Columns.Count < count)
                        t.Columns.Add(NewColumn("열" + (t.Columns.Count + 1), t.Columns.Count, false));

                    var row = new DbRow(t.Columns.Count);
                    for (int i = 0; i < count && i < row.Values.Length; i++)
                    {
                        // 빈 칸은 CSV 에서 "값 없음" 과 "빈 글자" 를 가릴 수 없습니다.
                        // 빈 글자로 둡니다 — NULL 로 바꾸면 없는 차이가 생깁니다.
                        row.Values[i] = Text(cells[i]);
                    }
                    t.Rows.Add(row);
                }
                t.HasRows = true;
            }
            return t;
        }

        /// <summary>
        /// 첫 줄(열 이름)로 표의 열을 세웁니다.
        ///
        /// 돌려주는 것은 <b>각 행에서 읽을 자리</b>입니다.
        ///   자리 둘  : 3 번(IO 이름) · 4 번(값) 만 읽습니다
        ///   <c>null</c> : 열이 모자라 통째로 읽습니다
        ///
        /// 통째로 읽을 때는 <b>열쇠를 두지 않습니다.</b> 자리를 짐작해
        /// 엉뚱한 열을 열쇠로 쓰면 엉뚱한 행끼리 짝지어 <b>없는 차이가
        /// 생깁니다.</b> 왜 그랬는지는 표의 알림에 적습니다.
        /// </summary>
        private static int[] Head(DbTable t, Cell[] cells, int count)
        {
            int key = DbNames.CsvKeyColumn - 1;        // 사람은 1 부터 셉니다
            int val = DbNames.CsvValueColumn - 1;
            bool two = count > key && count > val;

            for (int i = 0; i < count; i++)
            {
                if (two && i != key && i != val) continue;
                t.Columns.Add(NewColumn(Name(cells[i], i), t.Columns.Count, two && i == key));
            }

            if (!two)
            {
                t.Note += " / 열이 " + count + " 개뿐이라 " + DbNames.CsvKeyColumn
                        + " 번과 " + DbNames.CsvValueColumn + " 번을 쓸 수 없습니다. "
                        + "모든 열을 읽고 열쇠 없이 견줍니다";
                return null;
            }

            if (count > 2) t.Note += " / " + count + " 개 열 중 " + DbNames.CsvKeyColumn
                        + " 번(" + t.Columns[0].Name + ")으로 행을 짝짓고 "
                        + DbNames.CsvValueColumn + " 번(" + t.Columns[1].Name
                        + ") 값을 견줍니다. 안 본 열: " + Skipped(cells, count, key, val);

            return new[] { key, val };
        }

        /// <summary>안 읽은 열 이름들. 조용히 버리지 않으려고 적습니다.</summary>
        private static string Skipped(Cell[] cells, int count, int key, int val)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i == key || i == val) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Name(cells[i], i));
            }
            return sb.ToString();
        }

        /// <summary>적힌 열 이름. 비어 있으면 "열3" 처럼 자리로 부릅니다.</summary>
        private static string Name(Cell c, int i)
        {
            string name = Text(c);
            return string.IsNullOrEmpty(name) ? "열" + (i + 1) : name;
        }

        private static DbColumn NewColumn(string name, int ordinal, bool isKey)
        {
            var c = new DbColumn();
            c.Name = name;
            c.Ordinal = ordinal;
            c.IsKey = isKey;
            return c;
        }

        private static string Text(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Empty: return string.Empty;
                case CellKind.Text: return c.Text ?? string.Empty;
                default: return c.Display();
            }
        }
    }
}
