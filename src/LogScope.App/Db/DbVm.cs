using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using System.Text;
using LogScope.Db;
using LogScope.Core.Settings;

using LogScope.App.Common;
using LogScope.App.Logs;
namespace LogScope.App.Db
{
    /// <summary>
    /// 분석 2 — DB 분석.
    ///
    /// 두 DB 한 벌을 읽어 견주고, 차이를 <b>SQL 글</b>로 적어 줍니다.
    ///
    /// 어디를 읽을지는 <b>이 화면</b>에서 정합니다 (칸에 타 넣거나 [폴더] ·
    /// [파일] 로 고르거나 끌어다 놓기). 정한 자리는 프로그램이 설정에
    /// 저장하므로 다음에 켤 때도 그대로입니다. 0.77 까지는 config.txt 에도
    /// 적을 수 있었는데 빼냈습니다 — 두 군데에 있으면 "지금 읽는 자리가
    /// 어느 쪽인지" 를 매번 따져야 합니다.
    ///
    /// <b>이 화면은 DB 에 접속하지 않습니다.</b> 파일을 읽고, 글을 만들어
    /// 보여 줄 뿐입니다. 만든 글을 돌리는 것은 사람이 합니다.
    /// </summary>
    public sealed class DbVm : ObservableObject
    {
        public DbVm()
        {
        }

        /// <summary>
        /// 화면 제목. config.txt 의 analysis2 이름을 그대로 씁니다 — 위 줄의
        /// 단추와 메인 화면 칸과 <b>같은 이름</b>이어야 합니다. 여기만 "DB 분석"
        /// 로 박아 두면, 이름을 바꾼 사람이 이 화면만 안 바뀐 것을 봅니다.
        /// </summary>
        public string ScreenTitle { get { return AppConfig.Current.Title(2); } }

        // ---- 견줄 자리 ----
        //
        // 자리는 설정 파일(settings.json) 한 곳에만 있습니다.

        private AppSettings _settings;

        /// <summary>
        /// 설정을 받습니다. 창이 만들어진 뒤에 붙입니다 — 이 화면은 XAML 이
        /// 직접 만들어서 생성자로 넘길 수가 없습니다 (그래프 화면의
        /// <c>Attach</c> 와 같은 방식입니다).
        /// </summary>
        public void UseSettings(AppSettings settings)
        {
            _settings = settings;
            Raise("BeforePath"); Raise("AfterPath");
            Raise("BeforeChosen"); Raise("AfterChosen");
            Raise("BeforeNote"); Raise("AfterNote");
            Raise("HasPaths"); Raise("PathText");
        }

        /// <summary>
        /// 견줄 자리. <b>화면에서 고칠 수 있습니다</b> — 타 넣거나 붙여넣어도
        /// 되고, 끌어다 놓아도 됩니다. 폴더든 파일이든 됩니다.
        /// </summary>
        public string BeforePath
        {
            get { return _settings == null ? string.Empty : _settings.DbBeforePath ?? string.Empty; }
            set { SetPath(true, value); }
        }

        public string AfterPath
        {
            get { return _settings == null ? string.Empty : _settings.DbAfterPath ?? string.Empty; }
            set { SetPath(false, value); }
        }

        // ---- 바깥 도구 (선택). 설정 창의 [DB 도구] 칸입니다 ----
        //
        // 화면이 AppSettings 를 직접 뒤지지 않게 여기로 통해 줍니다.

        public string MysqlDumpPath
        {
            get { return _settings == null ? string.Empty : _settings.MysqlDumpPath ?? string.Empty; }
        }

        public string DumpBefore
        {
            get { return _settings == null ? string.Empty : _settings.DbDumpBefore ?? string.Empty; }
        }

        public string DumpAfter
        {
            get { return _settings == null ? string.Empty : _settings.DbDumpAfter ?? string.Empty; }
        }

        public string Ibd2SdiPath
        {
            get { return _settings == null ? string.Empty : _settings.Ibd2SdiPath ?? string.Empty; }
        }

        /// <summary>
        /// 칸 아래에 작게 적는 글. <b>그 자리가 무엇인지</b> 말해 줍니다 —
        /// 폴더인지 파일인지, 그리고 <b>있기는 한지</b>.
        ///
        /// 없는 자리를 말해 주는 것이 중요합니다. 경로를 손으로 타 넣다가
        /// 한 글자 틀리면 [DB 읽기] 를 눌러야 알게 되는데, 그때 나오는 말은
        /// "읽은 표가 없습니다" 라서 왜인지 알 수가 없습니다.
        /// </summary>
        public string BeforeNote { get { return Note(BeforePath); } }

        public string AfterNote { get { return Note(AfterPath); } }

        // 그 한 줄을 적는 곳은 DbPathText 한 곳입니다 — 메인 화면 칸도
        // 같은 글을 보여 주므로, 각자 적으면 한쪽만 고치는 날이 옵니다.
        private static string Note(string path)
        {
            return DbPathText.Note(path);
        }

        /// <summary>자리가 바뀌었을 때. 저장은 창이 맡습니다.</summary>
        public event EventHandler PathsChanged;

        // ---- 센 결과를 밖으로 ----
        //
        // 메인 화면 칸이 PASS · WARNING · FAIL 을 보여 주려면 이 수가
        // 거기까지 가야 합니다. 뷰모델이 AppState 를 직접 만지지 않고
        // <b>알리기만</b> 합니다 — 누가 받아 어디에 둘지는 창이 정합니다
        // (자리가 바뀌었을 때와 같은 방식입니다).

        private bool _hasTally;
        private DbDiffList.Tally _tally;

        /// <summary>한 번이라도 읽어서 센 적이 있는지.</summary>
        public bool HasTally { get { return _hasTally; } }

        /// <summary>마지막으로 센 수 (차이 발생 · 없어진 IO · 새로 생긴 IO).</summary>
        public DbDiffList.Tally Tally { get { return _tally; } }

        /// <summary>읽고 견준 뒤. 메인 화면 칸이 이걸 듣습니다.</summary>
        public event EventHandler ResultChanged;

        /// <summary>자리가 정해졌는지 (지우기 단추를 보일지).</summary>
        public bool BeforeChosen
        {
            get { return BeforePath.Length > 0; }
        }

        public bool AfterChosen
        {
            get { return AfterPath.Length > 0; }
        }

        /// <summary>
        /// 화면에서 고른 자리를 적습니다. 저장은 부르는 쪽(창)이 합니다.
        /// </summary>
        public void SetPath(bool before, string path)
        {
            if (_settings == null) return;
            string v = path ?? string.Empty;
            if (before) _settings.DbBeforePath = v;
            else _settings.DbAfterPath = v;

            Raise("BeforePath"); Raise("AfterPath");
            Raise("BeforeChosen"); Raise("AfterChosen");
            Raise("BeforeNote"); Raise("AfterNote");
            Raise("HasPaths"); Raise("PathText");

            EventHandler h = PathsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        public bool HasPaths
        {
            get { return BeforePath.Length > 0 && AfterPath.Length > 0; }
        }

        public string PathText
        {
            get
            {
                if (!HasPaths)
                {
                    return "폴더를 넣으면 그 안의 .sql / .csv / .frm 을 하위 폴더까지 읽고, "
                         + "파일 하나를 넣으면 그 파일만 읽습니다. "
                         + "한 번 정하면 다음에 켤 때도 그 자리입니다.";
                }
                return string.Empty;
            }
        }

        private DbDiffResult _diff;
        public DbDiffResult Diff { get { return _diff; } }

        private string _summary = "아직 읽지 않았습니다. [DB 읽기] 를 눌러 주세요.";
        public string Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        private string _fileNotes = string.Empty;   // 읽으면서 생긴 일 (못 읽은 파일 등)
        private string _listNotes = string.Empty;   // 목록에서 올라온 알림

        /// <summary>
        /// 읽으면서 생긴 일과 <b>목록의 알림</b>을 함께 적습니다.
        ///
        /// 알림을 여기 올리는 이유가 있습니다. 목록에서 "설명" 칸을 뺐는데,
        /// "기본 키가 없어 모든 열로 짝지었다" 나 "상한에 걸려 그 뒤는 세기만
        /// 했다" 같은 글이 그 칸에만 있었습니다. 그대로 두면 <b>목록이 전부인
        /// 줄 알고 읽게 됩니다</b> — 조용히 덜 보여 주는 것이 이 프로그램에서
        /// 가장 나쁜 고장입니다.
        /// </summary>
        public string Notes
        {
            get
            {
                if (_fileNotes.Length == 0) return _listNotes;
                if (_listNotes.Length == 0) return _fileNotes;
                return _fileNotes + "\n" + _listNotes;
            }
        }

        // ---------------- 차이 목록 ----------------

        private List<DbDiffLine> _lines = new List<DbDiffLine>();

        /// <summary>
        /// 칸으로 쪼갠 차이 목록.
        ///
        /// <b>ObservableCollection 이 아닙니다.</b> 줄이 몇 만 개까지 가는데,
        /// 하나씩 Add 하면 그만큼 바뀜 알림이 날아가 목록이 한참 멎습니다.
        /// 통째로 바꾸고 한 번만 알립니다.
        /// </summary>
        public IList<DbDiffLine> Lines { get { return _lines; } }

        private string _lineText = string.Empty;
        /// <summary>"1,234 줄" 처럼. 목록 위에 적힙니다.</summary>
        public string LineText
        {
            get { return _lineText; }
            private set { Set(ref _lineText, value); }
        }

        public bool LinesIsEmpty { get { return _lines.Count == 0; } }

        private string _emptyText = "아직 읽지 않았습니다. [DB 읽기] 를 눌러 주세요.";
        /// <summary>목록이 비었을 때 가운데 적는 글. 왜 비었는지가 매번 다릅니다.</summary>
        public string EmptyText
        {
            get { return _emptyText; }
            private set { Set(ref _emptyText, value); }
        }

        /// <summary>
        /// 알림 줄(갈래가 "알림")을 목록에서 빼내 위의 안내로 올립니다.
        /// 같은 글이 겹치면 한 번만 적고, 스무 줄까지만 적습니다.
        /// </summary>
        private List<DbDiffLine> TakeNotes(List<DbDiffLine> lines)
        {
            var kept = new List<DbDiffLine>(lines.Count);
            var notes = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine ln = lines[i];
                if (ln.Kind != DbDiffLine.KindNote) { kept.Add(ln); continue; }

                string text = ln.Table.Length > 0 ? ln.Table + " — " + ln.Note : ln.Note;
                if (text.Length > 0 && seen.Add(text) && notes.Count < 20) notes.Add(text);
            }

            _listNotes = string.Join("\n", notes.ToArray());
            return kept;
        }

        /// <summary>목록을 CSV 글로. 저장은 창이 맡습니다.</summary>
        public string LinesCsv()
        {
            // 화면에서 걸러 낸 알림 줄까지 다시 넣습니다. 내보낸 파일에서
            // "이 표는 상한에 걸렸다" 가 빠지면, 그 파일을 나중에 보는 사람은
            // 그게 차이의 전부인 줄 압니다.
            return DbDiffList.ToCsv(_diff == null ? _lines : DbDiffList.Build(_diff, null));
        }

        private bool _onlyChanged;

        /// <summary>
        /// <b>차이가 발생한 IO만 보기.</b> 값이 달라진 줄(빨강 · 노랑)만
        /// 남기고, 생기거나 없어진 줄과 표·열 줄을 감춥니다.
        ///
        /// 감춘 것이 "없는 것" 으로 읽히면 안 되므로, 켜 둔 동안에도
        /// <b>위의 세 숫자는 전체 기준 그대로</b>이고 줄 수 옆에 "걸러 보는
        /// 중" 이라고 적습니다.
        /// </summary>
        public bool OnlyChanged
        {
            get { return _onlyChanged; }
            set
            {
                if (_onlyChanged == value) return;
                _onlyChanged = value;
                Raise("OnlyChanged");
                Rebuild();
            }
        }

        private void Rebuild()
        {
            // 늘 전체입니다. 표를 고르는 왼쪽 목록을 없앴으니 범위도 하나입니다.
            int all = 0;
            if (_diff == null)
            {
                _lines = new List<DbDiffLine>();
                EmptyText = "아직 읽지 않았습니다. [DB 읽기] 를 눌러 주세요.";
            }
            else
            {
                _lines = TakeNotes(DbDiffList.Build(_diff, null));
                all = _lines.Count;
                if (_onlyChanged) _lines = DbDiffList.OnlyChanged(_lines);

                EmptyText = _diff.Tables.Count == 0
                    ? "읽은 표가 없습니다. 위 두 자리를 봐 주세요."
                    : _onlyChanged && all > 0
                        ? "값이 달라진 줄이 없습니다. [차이가 발생한 IO만 보기] 를 끄면 "
                          + "생기거나 없어진 줄도 보입니다."
                        : "두 DB 가 같습니다 — 다른 데가 없습니다.";
            }

            LineText = _lines.Count == 0 ? string.Empty
                     : _onlyChanged
                         ? _lines.Count.ToString("N0") + " 줄  (걸러 보는 중 · 전체 "
                           + all.ToString("N0") + " 줄)"
                         : _lines.Count.ToString("N0") + " 줄";
            Raise("Lines");
            Raise("LinesIsEmpty");
            Raise("Notes");
        }

        private string _script = string.Empty;
        public string ScriptText
        {
            get { return _script; }
            private set { Set(ref _script, value); }
        }

        /// <summary>읽은 결과를 받습니다. 읽는 일은 창(DbView)이 맡습니다 — 진행 창을 띄워야 합니다.</summary>
        public void Take(DbSnapshot before, DbSnapshot after)
        {
            _diff = DbDiff.Compare(before, after);
            _fileNotes = string.Join("\n", _diff.Notes.ToArray());

            // 세는 것은 늘 <b>걸러내지 않은 전체</b>입니다. 걸러 보기를
            // 켰다고 "없어진 IO" 가 0 개로 보이면 안 됩니다.
            Summary = Overview(before, after, TakeNotes(DbDiffList.Build(_diff, null)));

            Rebuild();
            BuildScript();

            // 센 수는 걸러내지 않은 전체 기준입니다 (위 Summary 와 같습니다).
            _tally = DbDiffList.Count(TakeNotes(DbDiffList.Build(_diff, null)));
            _hasTally = true;

            EventHandler h = ResultChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>
        /// 화면 위 한 줄. <b>IO 를 셉니다</b> — 표를 세던 것("표 12 개 중 …")을
        /// 0.82 에서 뺐습니다. 보는 사람이 찾는 것은 "어느 IO 가 달라졌나"
        /// 라서, 표 수는 그 답으로 가는 길에 없습니다.
        ///
        /// 세는 일과 글자는 <see cref="DbDiffList.Headline"/> 에 있습니다.
        /// 여기서 또 세면 걸러 보기를 켰을 때 수가 달라집니다.
        /// </summary>
        private static string Overview(DbSnapshot b, DbSnapshot a, List<DbDiffLine> all)
        {
            return DbDiffList.Headline(all)
                 + "      (읽은 행 " + b.RowCount.ToString("N0")
                 + " / " + a.RowCount.ToString("N0") + ")";
        }

        public void BuildScript()
        {
            if (_diff == null) { ScriptText = string.Empty; return; }

            // 늘 "이전 → 이후" 입니다 (SqlScript 가 한 방향만 만듭니다).
            ScriptText = SqlScript.Build(_diff, null);
        }
    }
}
