using System;
using System.Collections.Generic;
using System.IO;

namespace LogScope.Core.Db
{
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

            int frm = 0, ibd = 0, folders = 0;
            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string f in files)
            {
                string ext = (Path.GetExtension(f) ?? string.Empty).ToLowerInvariant();
                if (ext == ".frm") { frm++; continue; }
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
            folders = seenFolders.Count;

            if (folders > 0)
                snap.Notes.Add("하위 폴더 " + folders + " 개까지 읽었습니다. "
                             + "표 이름 앞에 폴더 이름을 붙입니다 (db1.recipe 꼴).");

            if (frm > 0 || ibd > 0) snap.Notes.Add(Untouched(frm, ibd));
            if (snap.Tables.Count == 0 && frm == 0 && ibd == 0)
                snap.Notes.Add("읽을 수 있는 파일(.sql / .csv)이 없습니다.");

            snap.Sort();
            return snap;
        }

        /// <summary>
        /// .frm / .ibd 를 그냥 넘기지 않고 왜 못 읽는지 적습니다.
        /// 이 글이 화면에 그대로 나갑니다.
        ///
        /// <b>안내가 두 갈래입니다.</b> <c>.frm</c> 이 함께 있으면 MySQL 5.x
        /// (또는 MariaDB) 이고, 그 <c>.ibd</c> 안에는 ibd2sdi 가 꺼낼 표
        /// 정의(SDI)가 <b>없습니다</b> — SDI 는 8.0 부터 들어간 것입니다.
        /// 그런 폴더에 "ibd2sdi 경로를 적으세요" 라고 안내하면, 적어 보고
        /// 또 안 되는 길로 보내는 셈입니다.
        /// </summary>
        private static string Untouched(int frm, int ibd)
        {
            var parts = new List<string>();
            if (ibd > 0) parts.Add(".ibd " + ibd + " 개");
            if (frm > 0) parts.Add(".frm " + frm + " 개");
            string what = string.Join(", ", parts.ToArray());

            if (frm > 0)
            {
                return what + " 는 직접 읽지 않습니다. "
                     + ".frm 이 함께 있으니 MySQL 5.x (또는 MariaDB) 입니다 — "
                     + "이 .ibd 안에는 ibd2sdi 가 꺼낼 표 정의(SDI)가 없습니다 "
                     + "(SDI 는 8.0 부터 들어갑니다). "
                     + "mysqldump 로 .sql 을 만들어 이 폴더에 두거나, config.txt 의 "
                     + "db.dump.* 에 덤프 명령을 적어 주세요. 이 폴더를 다른 MySQL 에 "
                     + "붙여 뽑으려면 ibdata1 과 ib_logfile* 도 함께 있어야 합니다.";
            }

            return what + " 는 직접 읽지 않습니다 (공개 규격이 없는 InnoDB 내부 파일). "
                 + "config.txt 의 db.ibd2sdi 에 ibd2sdi 경로를 적으면 표와 열의 "
                 + "이름·타입은 읽습니다 (MySQL 8.0 의 .ibd 만, 값은 못 읽습니다). "
                 + "값까지 보려면 mysqldump 로 .sql 을 만들어 이 폴더에 두세요.";
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
}
