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
                TakeFile(path, snap, opt);
                snap.Sort();
                return snap;
            }

            if (!Directory.Exists(path))
            {
                snap.Notes.Add("폴더가 없습니다: " + path);
                return snap;
            }

            string[] files;
            try { files = Directory.GetFiles(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                snap.Notes.Add("폴더를 읽지 못했습니다: " + e.Message);
                return snap;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int frm = 0, ibd = 0;
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
                TakeFile(f, snap, opt);
            }

            if (frm > 0 || ibd > 0)
            {
                snap.Notes.Add(Untouched(frm, ibd));
            }
            if (snap.Tables.Count == 0 && frm == 0 && ibd == 0)
            {
                snap.Notes.Add("읽을 수 있는 파일(.sql / .csv)이 없습니다.");
            }

            snap.Sort();
            return snap;
        }

        /// <summary>
        /// .frm / .ibd 를 그냥 넘기지 않고 왜 못 읽는지 적습니다.
        /// 이 글이 화면에 그대로 나갑니다.
        /// </summary>
        private static string Untouched(int frm, int ibd)
        {
            var parts = new List<string>();
            if (ibd > 0) parts.Add(".ibd " + ibd + " 개");
            if (frm > 0) parts.Add(".frm " + frm + " 개");

            return string.Join(", ", parts.ToArray())
                 + " 는 이 프로그램이 직접 읽지 않습니다 (공개 규격이 없는 InnoDB 내부 파일). "
                 + "[설정] → [DB] 에서 MySQL 도구 경로를 넣으면 거기서 뽑아 읽습니다. "
                 + "또는 mysqldump 로 .sql 을 만들어 그 폴더에 두세요.";
        }

        private static void TakeFile(string file, DbSnapshot snap, Options opt)
        {
            string ext = (Path.GetExtension(file) ?? string.Empty).ToLowerInvariant();
            try
            {
                if (ext == ".sql")
                {
                    SqlDumpReader.Read(file, snap, opt.MaxRowsPerTable);
                    return;
                }

                DbTable t = CsvTableReader.Read(file, opt.MaxRowsPerTable);
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
