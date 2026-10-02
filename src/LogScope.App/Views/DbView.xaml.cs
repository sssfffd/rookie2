using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.Services;
using LogScope.App.ViewModels;
using LogScope.Core.Db;
using LogScope.Core.Io;
using LogScope.Core.Settings;

namespace LogScope.App.Views
{
    /// <summary>
    /// 분석 2 — DB 분석.
    ///
    /// 읽는 일은 여기서 합니다 (진행 창을 띄워야 하므로). 견주고 글을 만드는
    /// 일은 Core 에 있고, 이 파일은 그 사이를 잇습니다.
    /// </summary>
    public partial class DbView : UserControl
    {
        private readonly DbVm _vm = new DbVm();

        public DbView()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        private void OnRead(object sender, RoutedEventArgs e)
        {
            AppConfig cfg = AppConfig.Current;
            if (!_vm.HasPaths)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "config.txt 에 견줄 두 자리를 적어 주세요.\n\n"
                    + "    db.before = D:\\db\\이전\n"
                    + "    db.after  = D:\\db\\이후",
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            object result; Exception error;
            bool ok = BackgroundJob.Run(Window.GetWindow(this), "DB 를 읽는 중",
                delegate (LoadProgress p)
                {
                    var pair = new DbSnapshot[2];
                    p.Report("이전 DB", 0.0);
                    pair[0] = Load(cfg.DbBefore, cfg.DbDumpBefore, cfg, "before");
                    p.Report("이후 DB", 0.5);
                    pair[1] = Load(cfg.DbAfter, cfg.DbDumpAfter, cfg, "after");
                    return pair;
                }, out result, out error);

            if (!ok || error != null)
            {
                if (error is OperationCanceledException) return;
                MessageBox.Show(Window.GetWindow(this),
                    "DB 를 읽지 못했습니다.\n\n" + (error != null ? error.Message : "알 수 없는 오류"),
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var snaps = (DbSnapshot[])result;
            _vm.Take(snaps[0], snaps[1]);
        }

        /// <summary>
        /// 한쪽을 읽습니다.
        ///
        /// 순서가 있습니다.
        ///   1. 덤프 명령 줄이 적혀 있으면 먼저 돌려 .sql 을 만듭니다.
        ///   2. 폴더(또는 파일)의 .sql / .csv 를 읽습니다.
        ///   3. .ibd 가 있고 ibd2sdi 경로가 적혀 있으면 <b>이름과 타입만</b> 더합니다.
        /// </summary>
        private static DbSnapshot Load(string path, string dumpCommand, AppConfig cfg, string tag)
        {
            var snap = new DbSnapshot();

            if (dumpCommand.Length > 0)
            {
                string tmp = Path.Combine(Path.GetTempPath(),
                    "logscope_" + tag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".sql");
                DbTools.Run r = DbTools.DumpTo(dumpCommand, tmp, DbTools.DefaultTimeoutMs);
                if (!r.Ok)
                {
                    snap.Notes.Add("덤프를 받지 못했습니다: " + r.Error);
                }
                else
                {
                    try
                    {
                        SqlDumpReader.Read(tmp, snap, SqlDumpReader.MaxRowsPerTable);
                        snap.Notes.Add("덤프를 받아 읽었습니다 (" + Path.GetFileName(tmp) + ").");
                    }
                    catch (Exception e) when (e is IOException || e is FormatException)
                    {
                        snap.Notes.Add("받은 덤프를 읽지 못했습니다: " + e.Message);
                    }
                }
            }

            DbSnapshot files = DbFolderReader.Read(path, null);
            snap.Source = files.Source;
            for (int i = 0; i < files.Tables.Count; i++)
            {
                DbTable old = snap.Find(files.Tables[i].Name);
                if (old != null) snap.Tables.Remove(old);
                snap.Tables.Add(files.Tables[i]);
            }
            for (int i = 0; i < files.Notes.Count; i++) snap.Notes.Add(files.Notes[i]);

            TakeIbd(path, cfg, snap);
            snap.Sort();
            return snap;
        }

        /// <summary>
        /// .ibd 에서 <b>모양만</b> 더합니다. 이미 .sql 로 읽은 표는 건드리지
        /// 않습니다 — 값까지 있는 쪽이 더 많이 아는 것입니다.
        /// </summary>
        private static void TakeIbd(string path, AppConfig cfg, DbSnapshot snap)
        {
            if (cfg.Ibd2SdiPath.Length == 0) return;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            string[] ibds;
            try { ibds = Directory.GetFiles(path, "*.ibd"); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return; }
            if (ibds.Length == 0) return;

            Array.Sort(ibds, StringComparer.OrdinalIgnoreCase);
            int added = 0, failed = 0;

            foreach (string ibd in ibds)
            {
                var side = new DbSnapshot();
                DbTools.Run r = DbTools.ReadIbd(cfg.Ibd2SdiPath, ibd, side, DbTools.DefaultTimeoutMs);
                if (!r.Ok) { failed++; continue; }

                for (int i = 0; i < side.Tables.Count; i++)
                {
                    DbTable t = side.Tables[i];
                    DbTable have = snap.Find(t.Name);
                    if (have != null && have.HasRows) continue;   // .sql 쪽이 더 많이 압니다
                    if (have != null) snap.Tables.Remove(have);
                    snap.Tables.Add(t);
                    added++;
                }
            }

            if (added > 0)
                snap.Notes.Add("ibd2sdi 로 표 " + added + " 개의 모양을 더 읽었습니다 (값은 못 읽습니다).");
            if (failed > 0)
                snap.Notes.Add(".ibd " + failed + " 개는 ibd2sdi 가 읽지 못했습니다.");
        }

        private void OnSaveScript(object sender, RoutedEventArgs e)
        {
            if (_vm.ScriptText.Length == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "먼저 [DB 읽기] 를 눌러 주세요.",
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = "맞추는 SQL 저장";
            dlg.Filter = "SQL (*.sql)|*.sql|모든 파일 (*.*)|*.*";
            dlg.FileName = "db_diff_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".sql";
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                // BOM 을 붙입니다. mysql 클라이언트가 한글을 UTF-8 로 읽게 하는
                // 가장 확실한 방법입니다.
                File.WriteAllText(dlg.FileName, _vm.ScriptText, new UTF8Encoding(true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(Window.GetWindow(this), "저장하지 못했습니다.\n\n" + ex.Message,
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
