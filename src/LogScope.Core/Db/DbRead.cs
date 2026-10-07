using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LogScope.Core.Io;

// ==========================================================
//  DbRead
//
// 읽기 — 어느 파일을 누가 읽을지, 그리고 .sql / .csv 읽기.
//
// DB 화면을 고칠 때 제일 먼저 볼 파일입니다.
//
//   DbFolderReader  폴더(또는 파일 하나)를 훑어 어느 파일을 누가 읽을지 정합니다.
//                   새 확장자를 더하려면 TakeFile 에 한 줄입니다.
//   SqlDumpReader   .sql 덤프 (CREATE TABLE / INSERT 를 글자로 훑습니다)
//   CsvTableReader  .csv (파일 이름 = 표 이름, 첫 줄 = 열 이름)
//
//  한 파일로 모았습니다 (0.73). 전에는 DbFolderReader · SqlDumpReader · CsvTableReader
//  로 나뉘어 있었습니다 — DB 화면을 고치려면 열 파일을 뒤져야 했습니다.
// ==========================================================

namespace LogScope.Core.Db
{
    // ======================================================
    //  DbFolderReader
    // ======================================================

    /// <summary>
    /// 폴더 하나를 DB 한 벌로 읽습니다.
    ///
    ///   *.sql   덤프. 한 파일에 표가 여러 개 있을 수 있습니다
    ///   *.csv   표 하나씩 (파일 이름 = 표 이름)
    ///   *.frm   <b>못 읽습니다.</b> 공개 규격이 없는 바이너리이고, MySQL 8 부터는
    ///           아예 쓰지 않습니다. 있으면 그렇다고 적어 둡니다
    ///   *.ibd   값은 못 읽습니다. 표 이름만 세어 "여기 표가 있다" 까지 알립니다
    ///
    /// <b>못 읽은 것을 조용히 넘기지 않습니다.</b> 폴더에 .ibd 가 40 개 있는데
    /// 화면에 표가 하나도 없으면 "차이 없음" 으로 읽힙니다. 그래서 Notes 에
    /// 적어 화면에 띄웁니다.
    /// </summary>
    public static class DbFolderReader
    {
        public sealed class Options
        {
            public int MaxRowsPerTable = SqlDumpReader.MaxRowsPerTable;

            /// <summary>표 수 상한. 너무 큰 폴더에서 화면이 멈추지 않게.</summary>
            public int MaxTables = 2000;

            /// <summary>
            /// 하위 폴더를 몇 겹까지 들어갈지.
            ///
            /// MySQL 데이터 폴더는 <c>Data\&lt;DB 이름&gt;\&lt;표&gt;.ibd</c> 로 한 겹이지만,
            /// 날짜별 덤프를 또 나눠 두는 일이 있어 넉넉히 둡니다. 상한을 두는
            /// 이유는 <b>바로 가기(심볼릭 링크)가 자기 위를 가리키면 끝없이
            /// 돌기</b> 때문입니다 — 그런 자리를 따로 걸러도, 겹 수 상한이
            /// 마지막 방패가 됩니다.
            /// </summary>
            public int MaxDepth = 8;

            /// <summary>훑을 파일 수 상한. 데이터 폴더에는 파일이 수만 개 있을 수 있습니다.</summary>
            public int MaxFiles = 50000;
        }

        /// <summary>
        /// 폴더 아래의 파일을 <b>하위 폴더까지</b> 모읍니다.
        ///
        /// Directory.GetFiles 의 AllDirectories 를 쓰지 않습니다. 그걸 쓰면
        /// 가는 길에 못 읽는 폴더 하나가 있으면 <b>전체가 예외로 끝나고</b>,
        /// 바로 가기 고리를 걸러낼 자리도 없습니다. 직접 훑으면 못 읽는 폴더는
        /// 건너뛰고 나머지를 읽을 수 있습니다.
        /// </summary>
        public static List<string> Files(string root, Options opt, List<string> notes)
        {
            if (opt == null) opt = new Options();
            var found = new List<string>();
            int skippedDirs = 0, deepest = 0;

            var stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(root, 0));

            while (stack.Count > 0)
            {
                KeyValuePair<string, int> cur = stack.Pop();
                string dir = cur.Key;
                int depth = cur.Value;
                if (depth > deepest) deepest = depth;

                try
                {
                    string[] files = Directory.GetFiles(dir);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (found.Count >= opt.MaxFiles)
                        {
                            if (notes != null)
                                notes.Add("파일이 " + opt.MaxFiles.ToString("N0")
                                        + " 개를 넘어 거기까지만 훑었습니다 (상한).");
                            return found;
                        }
                        found.Add(files[i]);
                    }

                    if (depth >= opt.MaxDepth) continue;

                    string[] dirs = Directory.GetDirectories(dir);
                    Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                    // 역순으로 넣어 꺼낼 때 이름 순이 되게 합니다.
                    for (int i = dirs.Length - 1; i >= 0; i--)
                    {
                        if (IsLink(dirs[i])) { skippedDirs++; continue; }
                        stack.Push(new KeyValuePair<string, int>(dirs[i], depth + 1));
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    skippedDirs++;
                }
            }

            if (skippedDirs > 0 && notes != null)
                notes.Add("폴더 " + skippedDirs + " 개는 건너뛰었습니다 (못 읽거나 바로 가기).");
            return found;
        }

        /// <summary>
        /// 바로 가기(심볼릭 링크 · 접합점)인지. 자기 위를 가리키면 끝없이
        /// 돌기 때문에 들어가지 않습니다.
        /// </summary>
        private static bool IsLink(string dir)
        {
            try { return (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return true; }
        }

        /// <summary>
        /// 표 이름 앞에 붙일 폴더 이름. <c>db1\recipe.csv</c> → <c>"db1."</c>
        ///
        /// 붙이는 이유가 있습니다. DB 마다 폴더가 나뉘는데 표 이름은 DB 안에서만
        /// 다릅니다 — <c>db1\users</c> 와 <c>db2\users</c> 를 그냥 "users" 로
        /// 두면 <b>한쪽이 다른 쪽을 덮어 조용히 사라집니다.</b>
        ///
        /// 뿌리에 바로 있는 파일은 아무것도 붙이지 않습니다.
        /// </summary>
        public static string Prefix(string root, string file)
        {
            try
            {
                string dir = Path.GetDirectoryName(file) ?? string.Empty;
                string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                string here = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);

                if (string.Equals(full, here, StringComparison.OrdinalIgnoreCase)) return string.Empty;
                if (!here.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                string rel = here.Substring(full.Length + 1);
                return rel.Replace(Path.DirectorySeparatorChar, '.')
                          .Replace(Path.AltDirectorySeparatorChar, '.') + ".";
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException
                                   || e is PathTooLongException)
            {
                return string.Empty;
            }
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

            // 파일 하나를 바로 줄 수도 있습니다 (.sql 덤프 하나).
            if (File.Exists(path))
            {
                TakeFile(path, string.Empty, snap, opt);
                snap.Sort();
                return snap;
            }

            if (!Directory.Exists(path))
            {
                snap.Notes.Add("폴더가 없습니다: " + path);
                return snap;
            }

            List<string> files = Files(path, opt, snap.Notes);

            int ibd = 0;
            var frmFiles = new List<string>();
            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string f in files)
            {
                string ext = (Path.GetExtension(f) ?? string.Empty).ToLowerInvariant();
                if (ext == ".frm") { frmFiles.Add(f); continue; }
                if (ext == ".ibd") { ibd++; continue; }
                if (ext != ".sql" && ext != ".csv" && ext != ".tsv") continue;

                if (snap.Tables.Count >= opt.MaxTables)
                {
                    snap.Notes.Add("표가 " + opt.MaxTables + " 개를 넘어 거기까지만 읽었습니다.");
                    break;
                }

                string prefix = Prefix(path, f);
                if (prefix.Length > 0) seenFolders.Add(prefix);
                TakeFile(f, prefix, snap, opt);
            }

            // .frm 은 <b>나중에</b> 읽습니다. 값까지 있는 .sql 쪽이 더 많이 아는
            // 것이라, 같은 이름이면 그쪽이 이겨야 합니다. 파일 차례에 맡기면
            // 폴더를 훑는 순서에 따라 결과가 달라집니다.
            int frmOk = 0, frmFail = 0, frmSkip = 0;
            var frmWhy = new List<string>();
            var frmNotes = new List<string>();
            var versions = new List<string>();

            foreach (string f in frmFiles)
            {
                if (snap.Tables.Count >= opt.MaxTables)
                {
                    snap.Notes.Add("표가 " + opt.MaxTables + " 개를 넘어 거기까지만 읽었습니다.");
                    break;
                }

                string prefix = Prefix(path, f);
                FrmReader.Result res;
                switch (TakeFrm(f, prefix, snap, out res))
                {
                    case FrmTake.Added:
                        frmOk++;
                        if (prefix.Length > 0) seenFolders.Add(prefix);
                        string v = VersionText(res.VersionId);
                        if (!versions.Contains(v)) versions.Add(v);
                        if (res.Note.Length > 0 && frmNotes.Count < 3)
                            frmNotes.Add(Path.GetFileName(f) + " — " + res.Note);
                        break;
                    case FrmTake.Skipped:
                        frmSkip++;
                        break;
                    default:
                        frmFail++;
                        if (frmWhy.Count < 3) frmWhy.Add(Path.GetFileName(f) + " — " + res.Why);
                        break;
                }
            }

            int folders = seenFolders.Count;
            if (folders > 0)
                snap.Notes.Add("하위 폴더 " + folders + " 개까지 읽었습니다. "
                             + "표 이름 앞에 폴더 이름을 붙입니다 (db1.recipe 꼴).");

            if (frmOk > 0) snap.Notes.Add(FrmNote(frmOk, versions));
            if (frmSkip > 0)
                snap.Notes.Add(".frm " + frmSkip + " 개는 같은 이름의 덤프가 이미 있어 쓰지 "
                             + "않았습니다 (값까지 있는 쪽이 더 많이 압니다).");
            if (frmFail > 0)
                snap.Notes.Add(".frm " + frmFail + " 개는 읽지 못했습니다. "
                             + string.Join(" / ", frmWhy.ToArray()));
            for (int i = 0; i < frmNotes.Count; i++) snap.Notes.Add(frmNotes[i]);

            if (ibd > 0) snap.Notes.Add(IbdNote(ibd, frmFiles.Count > 0));
            if (snap.Tables.Count == 0 && frmFiles.Count == 0 && ibd == 0)
                snap.Notes.Add("읽을 수 있는 파일(.sql / .csv / .frm)이 없습니다.");

            snap.Sort();
            return snap;
        }

        /// <summary>
        /// .frm 에서 모양을 읽었다고 적습니다.
        ///
        /// <b>만든 판 번호를 함께 적습니다.</b> 이게 자체 점검입니다 —
        /// .frm 의 자리값을 우리가 잘못 잡았으면 판 번호부터 엉뚱하게 나옵니다.
        /// "5.6.19" 로 찍히면 머리 부분은 제대로 읽은 것입니다.
        /// </summary>
        private static string FrmNote(int count, List<string> versions)
        {
            string ver = versions.Count > 0
                ? " (만든 판 " + string.Join(", ", versions.ToArray()) + ")"
                : string.Empty;
            return ".frm " + count + " 개에서 표 모양을 읽었습니다" + ver + ". "
                 + "열 이름 · 타입 · NULL 허용 · 순서 · 기본 키입니다 — "
                 + "값과 기본값(DEFAULT)은 .frm 에 없어 견주지 않습니다. "
                 + "값까지 보려면 mysqldump 로 .sql 을 만들어 이 폴더에 두세요.";
        }

        /// <summary>5.6.19 꼴로. 0 이면 5.0 미만입니다.</summary>
        public static string VersionText(int id)
        {
            if (id <= 0) return "5.0 미만";
            return (id / 10000) + "." + (id % 1000 / 100) + "." + (id % 100);
        }

        /// <summary>
        /// .ibd 를 왜 안 읽는지 적습니다. <b>안내가 두 갈래입니다.</b>
        ///
        /// <c>.frm</c> 이 함께 있으면 MySQL 5.x 이고, 그 <c>.ibd</c> 안에는
        /// ibd2sdi 가 꺼낼 표 정의(SDI)가 <b>없습니다</b> — SDI 는 8.0 부터
        /// 들어간 것입니다. 그런 폴더에 "ibd2sdi 경로를 적으세요" 라고 안내하면,
        /// 적어 보고 또 안 되는 길로 보내는 셈입니다.
        /// </summary>
        private static string IbdNote(int ibd, bool fiveX)
        {
            if (fiveX)
            {
                return ".ibd " + ibd + " 개는 읽지 않습니다 (값이 그 안에 있지만 "
                     + "직접 읽지 않습니다). .frm 이 함께 있으니 MySQL 5.x "
                     + "(또는 MariaDB) 이고, 이 .ibd 안에는 ibd2sdi 가 꺼낼 표 "
                     + "정의(SDI)가 없습니다 (SDI 는 8.0 부터 들어갑니다). "
                     + "값은 mysqldump 로 .sql 을 만들어 이 폴더에 두거나 "
                     + "config.txt 의 db.dump.* 에 덤프 명령을 적어 주세요. "
                     + "이 폴더를 다른 MySQL 에 붙여 뽑으려면 ibdata1 과 "
                     + "ib_logfile* 도 함께 있어야 합니다.";
            }

            return ".ibd " + ibd + " 개는 직접 읽지 않습니다 (공개 규격이 없는 "
                 + "InnoDB 내부 파일). config.txt 의 db.ibd2sdi 에 ibd2sdi 경로를 "
                 + "적으면 표와 열의 이름·타입은 읽습니다 (MySQL 8.0 의 .ibd 만, "
                 + "값은 못 읽습니다). 값까지 보려면 mysqldump 로 .sql 을 만들어 "
                 + "이 폴더에 두세요.";
        }

        /// <summary>.frm 하나를 읽어 담은 결과.</summary>
        private enum FrmTake { Added, Failed, Skipped }

        /// <summary>
        /// .frm 하나를 읽어 담습니다. 같은 이름의 표가 이미 <b>값까지</b>
        /// 있으면 담지 않습니다 — 모양만 아는 것으로 덮으면 아는 것이 줄어듭니다.
        /// </summary>
        private static FrmTake TakeFrm(string file, string prefix, DbSnapshot snap,
                                       out FrmReader.Result res)
        {
            res = FrmReader.Read(file);
            if (!res.Ok) return FrmTake.Failed;

            res.Table.Name = prefix + res.Table.Name;
            DbTable had = snap.Find(res.Table.Name);
            if (had != null && had.HasRows) return FrmTake.Skipped;
            if (had != null) snap.Tables.Remove(had);
            snap.Tables.Add(res.Table);
            return FrmTake.Added;
        }

        /// <param name="prefix">
        /// 표 이름 앞에 붙일 폴더 이름 ("db1." 또는 빈 글자).
        /// </param>
        private static void TakeFile(string file, string prefix, DbSnapshot snap, Options opt)
        {
            string ext = (Path.GetExtension(file) ?? string.Empty).ToLowerInvariant();
            try
            {
                if (ext == ".sql")
                {
                    // 덤프 안의 표들에도 같은 폴더 이름을 붙입니다. 따로 읽어
                    // 옮겨 담아야 해서 한 번 거칩니다.
                    var side = new DbSnapshot();
                    SqlDumpReader.Read(file, side, opt.MaxRowsPerTable);
                    for (int i = 0; i < side.Tables.Count; i++)
                    {
                        DbTable one = side.Tables[i];
                        one.Name = prefix + one.Name;
                        DbTable had = snap.Find(one.Name);
                        if (had != null) snap.Tables.Remove(had);
                        snap.Tables.Add(one);
                    }
                    for (int i = 0; i < side.Notes.Count; i++) snap.Notes.Add(side.Notes[i]);
                    return;
                }

                if (ext == ".frm")
                {
                    // 파일 하나를 바로 준 경우입니다. 폴더를 훑을 때는 차례를
                    // 맞추려고 따로 돌지만(.sql 이 이겨야 합니다), 여기서는
                    // 이 파일 하나뿐이라 바로 담습니다.
                    FrmReader.Result res;
                    FrmTake take = TakeFrm(file, prefix, snap, out res);
                    if (take == FrmTake.Added)
                    {
                        snap.Notes.Add(FrmNote(1, new List<string> { VersionText(res.VersionId) }));
                        if (res.Note.Length > 0) snap.Notes.Add(res.Note);
                    }
                    else if (take == FrmTake.Failed)
                    {
                        snap.Notes.Add(Path.GetFileName(file) + " 를 읽지 못했습니다: " + res.Why);
                    }
                    return;
                }

                DbTable t = CsvTableReader.Read(file, opt.MaxRowsPerTable);
                t.Name = prefix + t.Name;
                DbTable old = snap.Find(t.Name);
                if (old != null) snap.Tables.Remove(old);
                snap.Tables.Add(t);
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
    /// <b>키가 없습니다.</b> CSV 에는 기본 키라는 것이 없으니, 행을 짝지을 때는
    /// "모든 열이 같은 행끼리" 가 됩니다 (DbTable.RowKey 참고). 키로 쓸 열을
    /// 정하고 싶으면 <c>.sql</c> 덤프를 쓰시는 편이 맞습니다.
    /// </summary>
    public static class CsvTableReader
    {
        public static DbTable Read(string path, int maxRows)
        {
            if (maxRows <= 0) maxRows = SqlDumpReader.MaxRowsPerTable;

            var t = new DbTable();
            t.Name = Path.GetFileNameWithoutExtension(path);
            t.Note = Path.GetFileName(path);

            using (var src = new CsvReader(path))
            {
                Cell[] cells; int count;

                if (!src.NextRow(out cells, out count))
                {
                    t.Note += " / 빈 파일입니다";
                    return t;
                }

                for (int i = 0; i < count; i++)
                {
                    var c = new DbColumn();
                    string name = Text(cells[i]);
                    c.Name = string.IsNullOrEmpty(name) ? "열" + (i + 1) : name;
                    c.Ordinal = i;
                    t.Columns.Add(c);
                }

                while (src.NextRow(out cells, out count))
                {
                    if (t.Rows.Count >= maxRows)
                    {
                        t.Note += " / 행 " + maxRows.ToString("N0") + " 개까지만 읽었습니다 (상한)";
                        break;
                    }

                    // 줄이 머리보다 길면 열을 늘립니다. 잘라 버리면 값이 조용히 사라집니다.
                    while (t.Columns.Count < count)
                    {
                        var c = new DbColumn();
                        c.Name = "열" + (t.Columns.Count + 1);
                        c.Ordinal = t.Columns.Count;
                        t.Columns.Add(c);
                    }

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
