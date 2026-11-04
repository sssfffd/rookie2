using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LogScope.Common;

namespace LogScope.Analysis2
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

            // 칸 폭을 목록 폭에 맞춥니다 (1.14). 매개변수 없는 delegate 라
            // 이벤트 꼴(SizeChangedEventHandler)을 따로 적지 않아도 붙습니다.
            DiffList.SizeChanged += delegate { FitColumns(); };
        }

        // ---------------- 칸 폭 (1.14) ----------------
        //
        // 칸 폭이 고정(360 · 220 · 300)이라 넓은 화면에서는 오른쪽이 비고,
        // 좁은 화면에서는 이후 칸이 잘렸습니다. 이제 목록 폭을 나눠 줍니다.
        //
        // 손으로 칸 폭을 끌어 바꿔도 창 크기가 바뀌면 다시 이 비율로
        // 돌아갑니다 — 비율을 고치려면 아래 Share 만 고치면 됩니다.

        /// <summary>칸마다 몫 — IO명 · 이전 value · 이후 value. 합이 1 입니다.</summary>
        private static readonly double[] Share = { 0.40, 0.30, 0.30 };

        /// <summary>이 폭보다 좁아지면 더 줄이지 않고 가로 스크롤로 봅니다.</summary>
        private const double MinListWidth = 420;

        private void FitColumns()
        {
            var grid = DiffList.View as GridView;
            if (grid == null || grid.Columns.Count < Share.Length) return;

            // 세로 스크롤 막대 자리와 테두리·여백만큼 뺍니다. 안 빼면 마지막
            // 칸이 막대 밑으로 들어가 가로 스크롤 막대가 생깁니다.
            double w = DiffList.ActualWidth - SystemParameters.VerticalScrollBarWidth - 8;
            if (w < MinListWidth) w = MinListWidth;

            for (int i = 0; i < Share.Length; i++) grid.Columns[i].Width = Math.Floor(w * Share[i]);
        }

        /// <summary>설정을 붙입니다. 창이 만든 뒤에 불러 줍니다.</summary>
        public void Attach(AppState state)
        {
            if (state != null) _vm.UseSettings(state.Settings);

            // 칸에 직접 타 넣어 고친 것도 저장되어야 합니다. 단추와
            // 끌어다 놓기만 챙기면, 타 넣은 사람은 저장이 안 되는 것을
            // 다시 켤 때 압니다.
            _vm.PathsChanged += OnVmPathsChanged;
            _vm.ResultChanged += OnVmResultChanged;
        }

        /// <summary>읽고 견준 뒤. 창이 받아 메인 화면 칸에 넘깁니다.</summary>
        public event EventHandler ResultChanged;

        /// <summary>마지막으로 센 수. 아직 안 읽었으면 <c>HasTally</c> 가 거짓입니다.</summary>
        public bool HasTally { get { return _vm.HasTally; } }
        public DbDiffList.Tally Tally { get { return _vm.Tally; } }

        /// <summary>값이 달라진 IO 이름들 (앞 몇 개). 메인 화면 칸이 적습니다.</summary>
        public IList<string> ChangedIos { get { return _vm.ChangedIos; } }

        /// <summary>같은 IO 들의 이전 → 이후 값. 메인 화면 요약이 적습니다.</summary>
        public IList<DbChangedIo> ChangedValues { get { return _vm.ChangedValues; } }

        /// <summary>마지막으로 견준 결과 전체 — LLM 분석 재료(LlmRequest.DbDiff) 로 넘깁니다 (1.45). 안 읽었으면 null.</summary>
        public DbDiffResult Diff { get { return _vm.Diff; } }

        /// <summary>
        /// 그 IO 의 줄을 목록에서 찾아 골라 줍니다 (메인 화면 요약에서
        /// 눌러 넘어왔을 때).
        ///
        /// 못 찾으면 <b>조용히 넘깁니다</b> — 읽기 전이거나 걸러 보는 중일
        /// 수 있습니다. 창을 띄워 "없습니다" 라고 할 일은 아닙니다 (목록이
        /// 그 자리에 있고, 위의 보기 단추로 넓혀 보면 됩니다).
        /// </summary>
        public void FocusIo(string io)
        {
            if (string.IsNullOrEmpty(io)) return;

            // 검색으로 걸러 둔 중이면 그 IO 가 안 보일 수 있습니다 — 메인 화면
            // 에서 눌러 넘어왔는데 못 찾으면 안 되니 검색을 풉니다 (1.17).
            if (!NameSearch.Matches(io, _vm.Search)) _vm.Search = string.Empty;

            IList<DbDiffLine> lines = _vm.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine ln = lines[i];
                if (ln == null || ln.Io != io) continue;

                DiffList.SelectedItem = ln;
                DiffList.ScrollIntoView(ln);
                return;
            }
        }

        private void OnVmResultChanged(object sender, EventArgs e)
        {
            EventHandler h = ResultChanged;
            if (h != null) h(this, EventArgs.Empty);
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

        /// <summary>
        /// 파일 고르는 창을 띄웁니다. <b>메인 화면 칸을 눌렀을 때</b>도
        /// 이 길로 옵니다 — 고르는 규칙이 두 군데에 있으면 한쪽만 고치는
        /// 날이 옵니다.
        ///
        /// 1.90: 폴더는 읽지 않습니다 (파일 하나씩 — .sql / .csv).
        ///
        /// 1.53: 자리만 적고 <b>읽지 않습니다</b>. 메인 칸 머리의 [분석] 이 읽습니다
        /// (<see cref="ReadNow"/>). 고른 쪽 줄에는 "분석 대기" 가 붙습니다 — 1.03 처럼
        /// 이름은 보이는데 결과가 그대로인 까닭을 모르는 일이 없게. 골랐으면 true.
        /// </summary>
        public bool PickFileFor(bool before)
        {
            return PickFile(before);
        }

        /// <summary>
        /// 메인 칸의 [분석] (1.53). 분석 2 화면의 [읽기] 와 같습니다 — 자리가 비었으면
        /// 그렇다고 알리고, 둘 다 차 있으면 읽고 견줍니다.
        /// </summary>
        public void ReadNow()
        {
            OnRead(this, new RoutedEventArgs());
        }

        private void OnClearSearch(object sender, RoutedEventArgs e)
        {
            _vm.Search = string.Empty;
        }

        private void OnPickFile(object sender, RoutedEventArgs e)
        {
            PickFile(Before(sender));
        }

        /// <summary>파일 고르는 창. 골랐으면 true.</summary>
        private bool PickFile(bool before)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = (before ? "이전" : "이후") + " DB 파일";
            dlg.Filter = "DB 파일 (*.sql;*.csv)|*.sql;*.csv|모든 파일 (*.*)|*.*";
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

            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return false;
            _vm.SetPath(before, dlg.FileName);   // SetPath 가 PathsChanged 를 알립니다
            return true;
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

            string[] paths = Dropped(e);
            if (paths == null) return;

            // <b>한 칸 = 한 자리</b>입니다. 놓은 칸에만 들어갑니다 —
            // 메인 화면의 로그 칸과 같습니다. 놓은 칸이 아닌 쪽을
            // 함께 바꾸면 "이전에 놓았는데 이후도 바뀌는" 일이 됩니다.
            _vm.SetPath(Before(sender), paths[0]);
            Said(paths, 1);
        }

        /// <summary>
        /// <b>메인 화면 칸에 놓은 것</b>을 받습니다. 분석 2 화면을 열지
        /// 않고도 자리가 들어가야 해서 밖으로 열어 둔 길입니다.
        ///
        /// 1.52 까지는 자리가 둘 다 차면 바로 읽었습니다. 1.53 부터는 자리만 적고,
        /// 메인 칸 머리의 [분석] 이 읽습니다 (<see cref="ReadNow"/>) — 분석 1 · 3 과 같은
        /// 흐름입니다. 메인 칸 줄에 "분석 대기" 가 붙어서 왜 결과가 그대로인지 보입니다.
        /// </summary>
        public void TakePaths(IList<string> paths, bool before)
        {
            if (paths == null || paths.Count == 0) return;

            _vm.SetPath(before, paths[0]);   // SetPath 가 PathsChanged 를 알립니다
            Said(paths, 1);
        }

        /// <summary>끌어온 것이 파일이면 그 목록, 아니면 null (폴더를 놓으면 읽을 때 "폴더는 읽지 않습니다" 로 알림).</summary>
        private static string[] Dropped(DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            return paths == null || paths.Length == 0 ? null : paths;
        }

        /// <summary>
        /// 쓴 것보다 많이 놓았으면 그렇다고 알립니다. 한 칸은 자리
        /// <b>하나</b>라서 둘째부터는 쓸 데가 없습니다 — 조용히 버리면
        /// "넣었는데 왜 안 보이지" 가 됩니다.
        /// </summary>
        private void Said(IList<string> paths, int used)
        {
            if (paths.Count <= used) return;

            MessageBox.Show(Window.GetWindow(this),
                paths.Count + " 개를 놓으셨습니다. 한 칸에는 자리가 하나라서 "
                + "맨 앞의 것만 넣었습니다 (.sql 이나 .csv 파일 하나씩).",
                _vm.ScreenTitle, MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (!_vm.HasPaths)
            {
                MessageBox.Show(Window.GetWindow(this),
                    "견줄 두 자리를 먼저 정해 주세요.\n\n"
                    + "위 칸에 타 넣거나, [파일] 로 고르거나,\n"
                    + "칸에 .sql 이나 .csv 파일을 끌어다 놓으면 됩니다.",
                    _vm.ScreenTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Read();
        }

        /// <summary>
        /// 두 자리를 읽고 견줍니다. 자리가 둘 다 정해져 있어야 합니다 —
        /// 부르는 쪽이 보고 부릅니다.
        /// </summary>
        private void Read()
        {
            object result; Exception error;
            bool ok = BackgroundJob.Run(Window.GetWindow(this), "DB 를 읽는 중",
                delegate (LoadProgress p)
                {
                    // 화면이 들고 있는 자리를 읽습니다. 0.77 까지는 여기서
                    // config.txt 를 읽고 있었는데, 화면에서 자리를 고른
                    // 사람에게는 빈 글자가 넘어갔습니다 — 자리를 정해
                    // 두었는데 "읽은 표가 없습니다" 가 나왔습니다.
                    // 1.90: 파일 하나씩 (.sql / .csv). 바깥 프로그램 (mysqldump · ibd2sdi) 은 뺐습니다.
                    var pair = new DbSnapshot[2];
                    p.Report("이전 DB", 0.0);
                    pair[0] = DbFolderReader.Read(_vm.BeforePath, null);
                    p.Report("이후 DB", 0.5);
                    pair[1] = DbFolderReader.Read(_vm.AfterPath, null);
                    return pair;
                }, out result, out error);

            if (!ok || error != null)
            {
                if (error is OperationCanceledException) return;
                MessageBox.Show(Window.GetWindow(this),
                    "DB 를 읽지 못했습니다.\n\n" + (error != null ? error.Message : "알 수 없는 오류"),
                    _vm.ScreenTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var snaps = (DbSnapshot[])result;
            _vm.Take(snaps[0], snaps[1]);
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
    }
}
