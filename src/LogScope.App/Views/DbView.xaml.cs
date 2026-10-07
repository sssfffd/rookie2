using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LogScope.App.Services;
using LogScope.App.Infrastructure;
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

        /// <summary>설정을 붙입니다. 창이 만든 뒤에 불러 줍니다.</summary>
        public void Attach(AppState state)
        {
            if (state != null) _vm.UseSettings(state.Settings);

            // 칸에 직접 타 넣어 고친 것도 저장되어야 합니다. 단추와
            // 끌어다 놓기만 챙기면, 타 넣은 사람은 저장이 안 되는 것을
            // 다시 켤 때 압니다.
            _vm.PathsChanged += OnVmPathsChanged;
        }

        private void OnVmPathsChanged(object sender, EventArgs e)
        {
            RaisePaths();
        }

        /// <summary>
        /// 고른 자리를 저장해야 할 때. 창이 듣습니다 — 설정 파일을 쓰는 일은
        /// 창이 맡고 있어서, 여기서 직접 쓰면 저장하는 곳이 둘이 됩니다.
        /// </summary>
        public event EventHandler PathsChanged;

        private void RaisePaths()
        {
            EventHandler h = PathsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---------------- 견줄 자리 고르기 ----------------

        private static bool Before(object sender)
        {
            return CardTag.IsBefore(sender);
        }

        /// <summary>폴더 고르기. 폴더 고르는 창이 따로 없어서 저장 창을 씁니다.</summary>
        private void OnPickPath(object sender, RoutedEventArgs e)
        {
            bool before = Before(sender);

            // .NET Framework 의 WPF 에는 폴더 고르는 창이 없습니다. 바깥
            // 라이브러리를 더하지 않기로 했으니, 저장 창을 폴더 고르기로
            // 씁니다 — 파일 이름 칸은 안 쓰고 열린 폴더만 가져옵니다.
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = (before ? "이전" : "이후") + " DB 폴더 — 그 폴더로 들어가서 [저장]";
            dlg.FileName = "이 폴더를 고릅니다";
            dlg.Filter = "폴더 고르기|*.이폴더";
            dlg.CheckPathExists = true;
            dlg.OverwritePrompt = false;

            string start = before ? _vm.BeforePath : _vm.AfterPath;
            try { if (start.Length > 0 && Directory.Exists(start)) dlg.InitialDirectory = start; }
            catch (ArgumentException) { }

            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            string dir;
            try { dir = Path.GetDirectoryName(dlg.FileName); }
            catch (ArgumentException) { return; }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            _vm.SetPath(before, dir);   // SetPath 가 PathsChanged 를 알립니다
        }

        private void OnPickFile(object sender, RoutedEventArgs e)
        {
            bool before = Before(sender);

            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = (before ? "이전" : "이후") + " DB 파일";
            dlg.Filter = "DB 파일 (*.sql;*.csv;*.frm)|*.sql;*.csv;*.frm|모든 파일 (*.*)|*.*";
            dlg.CheckFileExists = true;

            string start = before ? _vm.BeforePath : _vm.AfterPath;
            try
            {
                if (start.Length > 0)
                {
                    string dir = Directory.Exists(start) ? start : Path.GetDirectoryName(start);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.InitialDirectory = dir;
                }
            }
            catch (ArgumentException) { }

            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            _vm.SetPath(before, dlg.FileName);   // SetPath 가 PathsChanged 를 알립니다
        }

        // ---------------- 끌어다 놓기 ----------------

        private void OnPathDragOver(object sender, DragEventArgs e)
        {
            bool ok = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            if (ok) Lit(sender as Border, true);
            e.Handled = true;
        }

        private void OnPathDragLeave(object sender, DragEventArgs e)
        {
            Lit(sender as Border, false);
        }

        private void OnPathDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Lit(sender as Border, false);

            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            _vm.SetPath(Before(sender), paths[0]);   // SetPath 가 PathsChanged 를 알립니다
        }

        /// <summary>
        /// 칸을 밝히고 되돌립니다. 되돌릴 배경은 <b>XAML 의 Tag</b> 에 적혀
        /// 있습니다 — 코드에 박아 두면 XAML 배경을 바꾼 날부터 한 번 끌어다
        /// 놓은 칸만 색이 달라진 채 남습니다 (0.59 에서 겪었습니다).
        /// </summary>
        private static void Lit(Border card, bool on)
        {
            CardTag.Lit(card, on);
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
                DbTools.Run r = DbTools.DumpTo(cfg.MysqlDumpPath, dumpCommand, tmp,
                                               DbTools.DefaultTimeoutMs);
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

            // 파일 모으기는 폴더 읽기와 같은 걸음을 씁니다 — 하위 폴더까지,
            // 바로 가기 고리는 건너뛰고, 겹 수 상한도 같습니다. 따로 훑으면
            // 한쪽만 하위 폴더를 보게 되어 엇갈립니다.
            var notes = new List<string>();
            List<string> all = DbFolderReader.Files(path, null, notes);

            int added = 0, failed = 0;
            foreach (string ibd in all)
            {
                string ext = (Path.GetExtension(ibd) ?? string.Empty).ToLowerInvariant();
                if (ext != ".ibd") continue;

                var side = new DbSnapshot();
                DbTools.Run r = DbTools.ReadIbd(cfg.Ibd2SdiPath, ibd, side, DbTools.DefaultTimeoutMs);
                if (!r.Ok) { failed++; continue; }

                string prefix = DbFolderReader.Prefix(path, ibd);
                for (int i = 0; i < side.Tables.Count; i++)
                {
                    DbTable t = side.Tables[i];
                    t.Name = prefix + t.Name;
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

        /// <summary>
        /// 보이는 목록을 CSV 로 저장합니다.
        ///
        /// 칸 그대로 나갑니다. 거르고 정렬하는 일은 엑셀이 이 화면보다 훨씬
        /// 잘합니다 — 목록을 그 쪽으로 넘기는 통로입니다.
        /// </summary>
        private void OnSaveList(object sender, RoutedEventArgs e)
        {
            if (_vm.Lines.Count == 0)
            {
                MessageBox.Show(Window.GetWindow(this), "저장할 목록이 없습니다.",
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = "차이 목록 저장";
            dlg.Filter = "CSV (*.csv)|*.csv|모든 파일 (*.*)|*.*";
            dlg.FileName = "db_diff_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                // BOM 을 붙입니다. 안 붙이면 한국어 윈도우의 엑셀이 CP949 로
                // 읽어 한글이 깨집니다 — 그게 가장 흔한 사고입니다.
                File.WriteAllText(dlg.FileName, _vm.LinesCsv(), new UTF8Encoding(true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(Window.GetWindow(this), "저장하지 못했습니다.\n\n" + ex.Message,
                    "DB 분석", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
