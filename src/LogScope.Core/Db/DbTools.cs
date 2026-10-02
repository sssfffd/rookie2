using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using LogScope.Core.Settings;

namespace LogScope.Core.Db
{
    /// <summary>
    /// <b>선택 기능</b>: 바깥 프로그램을 불러 DB 파일을 글로 바꿔 옵니다.
    /// 기본은 꺼져 있고, config.txt 에 경로나 명령 줄을 적어야만 돕니다.
    ///
    /// 라이브러리를 더하지 않습니다 — 이미 깔려 있는 MySQL 도구를 자식
    /// 프로세스로 띄우는 것뿐입니다. 파이썬 모듈(AiBridge)과 같은 방식입니다.
    ///
    /// <b>알아 둘 것</b>
    ///  - 적어 둔 실행 파일을 그대로 띄웁니다. 남이 바꿔 쓸 수 있는 폴더를
    ///    가리키지 마세요.
    ///  - <b>mysqldump 는 돌고 있는 서버에서 뽑습니다.</b> 멈춘 데이터
    ///    폴더(.frm/.ibd)만으로는 못 뽑습니다. 그게 MySQL 쪽 사정입니다.
    ///  - ibd2sdi 는 멈춘 .ibd 에서 <b>이름과 타입만</b> 꺼냅니다. 값은 못 꺼냅니다.
    ///  - 접속 정보는 우리가 들고 있지 않습니다. 명령 줄을 그대로 적게 둡니다.
    /// </summary>
    public static class DbTools
    {
        public const int DefaultTimeoutMs = 120000;

        public sealed class Run
        {
            public bool Ok;
            public string Output = string.Empty;
            public string Error = string.Empty;
        }

        /// <summary>
        /// 실행 파일과 인수를 정합니다.
        ///
        ///   <paramref name="exePath"/> 가 적혀 있으면 → 그것이 실행 파일,
        ///     <paramref name="dumpLine"/> 은 <b>인수</b>입니다.
        ///   비어 있으면 → <paramref name="dumpLine"/> 이 <b>명령 줄 전체</b>입니다.
        ///
        /// 둘을 섞어 짐작하지 않습니다. "경로를 적어 두었는가" 하나로 갈립니다 —
        /// 글만 보고 어느 쪽인지 알 수 있어야 합니다.
        /// </summary>
        public static bool Resolve(string exePath, string dumpLine, out string exe, out string args)
        {
            exe = string.Empty; args = string.Empty;

            string path = (exePath ?? string.Empty).Trim();
            string line = (dumpLine ?? string.Empty).Trim();

            // 적어 둔 경로에 따옴표가 붙어 있으면 떼어 줍니다.
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2).Trim();

            if (path.Length > 0)
            {
                exe = path;
                args = line;
                return exe.Length > 0;
            }

            if (line.Length == 0) return false;
            return SplitCommand(line, out exe, out args);
        }

        /// <summary>
        /// 덤프를 받아 파일로 저장합니다. 끝나면 그 파일을
        /// <see cref="SqlDumpReader"/> 로 읽으면 됩니다.
        /// </summary>
        public static Run DumpTo(string exePath, string dumpLine, string outFile, int timeoutMs)
        {
            var r = new Run();

            string exe, args;
            if (!Resolve(exePath, dumpLine, out exe, out args))
            {
                r.Error = "mysqldump 경로나 명령 줄이 비어 있습니다.";
                return r;
            }
            if (!File.Exists(exe))
            {
                r.Error = "실행 파일이 없습니다: " + exe;
                return r;
            }

            try
            {
                using (var p = Start(exe, args))
                using (var w = new StreamWriter(outFile, false, new UTF8Encoding(false)))
                {
                    var err = new StringBuilder();
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null && err.Length < 8000) err.AppendLine(e.Data);
                    };
                    p.BeginErrorReadLine();

                    char[] buf = new char[1 << 16];
                    int n;
                    while ((n = p.StandardOutput.Read(buf, 0, buf.Length)) > 0) w.Write(buf, 0, n);

                    if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs))
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        r.Error = "시간이 너무 걸려 멈췄습니다.";
                        return r;
                    }

                    if (p.ExitCode != 0)
                    {
                        r.Error = "도구가 오류로 끝났습니다 (코드 " + p.ExitCode + ")\n" + err;
                        return r;
                    }
                    r.Ok = true;
                    r.Output = outFile;
                    return r;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is System.ComponentModel.Win32Exception
                                   || e is InvalidOperationException)
            {
                r.Error = e.Message;
                return r;
            }
        }

        /// <summary>
        /// ibd2sdi 로 .ibd 에서 표 정의를 꺼냅니다. <b>이름과 타입만</b>입니다 —
        /// 행은 들어 있지 않으므로 <see cref="DbTable.HasRows"/> 는 거짓입니다.
        /// </summary>
        public static Run ReadIbd(string ibd2sdi, string ibdFile, DbSnapshot into, int timeoutMs)
        {
            var r = new Run();
            if (string.IsNullOrEmpty(ibd2sdi) || !File.Exists(ibd2sdi))
            {
                r.Error = "ibd2sdi 경로가 없습니다.";
                return r;
            }

            string json;
            try
            {
                using (var p = Start(ibd2sdi, "\"" + ibdFile + "\""))
                {
                    json = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs))
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        r.Error = "시간이 너무 걸려 멈췄습니다.";
                        return r;
                    }
                    if (p.ExitCode != 0)
                    {
                        r.Error = "ibd2sdi 가 오류로 끝났습니다 (코드 " + p.ExitCode + ")\n" + err;
                        return r;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is System.ComponentModel.Win32Exception
                                   || e is InvalidOperationException)
            {
                r.Error = e.Message;
                return r;
            }

            int added = TakeSdi(json, into, Path.GetFileName(ibdFile));
            r.Ok = added > 0;
            if (!r.Ok) r.Error = "ibd2sdi 가 낸 글에서 표 정의를 찾지 못했습니다.";
            return r;
        }

        /// <summary>
        /// ibd2sdi 가 낸 JSON 에서 표와 열 이름을 꺼냅니다.
        ///
        /// 이미 있는 Json 읽기를 그대로 씁니다 (설정 파일에 쓰는 것과 같은
        /// 것입니다). 모양이 조금 달라도 "이름" 과 "columns" 만 찾으므로,
        /// MySQL 판이 올라가도 쉽게 깨지지 않습니다.
        /// </summary>
        public static int TakeSdi(string json, DbSnapshot into, string note)
        {
            if (string.IsNullOrEmpty(json) || into == null) return 0;

            object root;
            try { root = Json.Parse(json); }
            catch (FormatException) { return 0; }

            int added = 0;
            foreach (Dictionary<string, object> obj in Objects(root))
            {
                object ddo;
                if (!obj.TryGetValue("dd_object", out ddo)) continue;
                Dictionary<string, object> dd = Json.AsObject(ddo);
                if (dd == null) continue;

                string name = Json.GetString(dd, "name", string.Empty);
                if (name.Length == 0) continue;

                List<object> cols = Json.GetArray(dd, "columns");
                if (cols.Count == 0) continue;

                var t = new DbTable();
                t.Name = name;
                t.HasRows = false;
                t.Note = note + " (ibd2sdi — 값은 읽지 못합니다)";

                foreach (object co in cols)
                {
                    Dictionary<string, object> c = Json.AsObject(co);
                    if (c == null) continue;
                    string cn = Json.GetString(c, "name", string.Empty);
                    if (cn.Length == 0) continue;
                    // InnoDB 가 스스로 넣는 숨은 열은 사용자 열이 아닙니다.
                    if (cn.StartsWith("DB_", StringComparison.Ordinal)) continue;

                    var col = new DbColumn();
                    col.Name = cn;
                    col.Type = Json.GetString(c, "column_type_utf8", string.Empty);
                    col.Nullable = Json.GetBool(c, "is_nullable", true);
                    col.Ordinal = t.Columns.Count;
                    t.Columns.Add(col);
                }

                if (t.Columns.Count == 0) continue;

                DbTable old = into.Find(t.Name);
                if (old != null) into.Tables.Remove(old);
                into.Tables.Add(t);
                added++;
            }
            return added;
        }

        /// <summary>JSON 안의 객체들을 훑습니다 (배열이 겹쳐 있어도).</summary>
        private static IEnumerable<Dictionary<string, object>> Objects(object node)
        {
            var obj = Json.AsObject(node);
            if (obj != null) { yield return obj; }

            var list = node as List<object>;
            if (list == null) yield break;
            for (int i = 0; i < list.Count; i++)
            {
                foreach (Dictionary<string, object> o in Objects(list[i])) yield return o;
            }
        }

        private static Process Start(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            try
            {
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
            }
            catch (ArgumentException) { }
            return Process.Start(psi);
        }

        /// <summary>
        /// 명령 줄을 실행 파일과 인수로 나눕니다. 따옴표로 감싼 경로를 먼저 봅니다 —
        /// 윈도우 경로에는 공백이 흔합니다.
        /// </summary>
        public static bool SplitCommand(string line, out string exe, out string args)
        {
            exe = string.Empty; args = string.Empty;
            if (line == null) return false;

            string s = line.Trim();
            if (s.Length == 0) return false;

            if (s[0] == '"')
            {
                int close = s.IndexOf('"', 1);
                if (close < 0) return false;
                exe = s.Substring(1, close - 1);
                args = s.Substring(close + 1).Trim();
                return exe.Length > 0;
            }

            int sp = s.IndexOf(' ');
            if (sp < 0) { exe = s; return true; }
            exe = s.Substring(0, sp);
            args = s.Substring(sp + 1).Trim();
            return exe.Length > 0;
        }
    }
}
