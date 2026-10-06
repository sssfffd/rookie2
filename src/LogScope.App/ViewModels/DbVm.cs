using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using LogScope.App.Infrastructure;
using LogScope.Core.Db;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 분석 2 — DB 분석.
    ///
    /// 두 DB 한 벌을 읽어 견주고, 차이를 <b>SQL 글</b>로 적어 줍니다.
    /// 어디를 읽을지는 <b>config.txt</b> 의 db.before / db.after 입니다 —
    /// 로그와 달리 DB 경로는 자주 바뀌지 않아서 파일에 적어 두는 편이 낫습니다.
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
        // config.txt 의 db.before / db.after 가 기본값이고, 화면에서 고르면
        // 설정 파일에 적혀 그쪽이 이깁니다. 비우면 config.txt 로 돌아갑니다.

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
            Raise("HasPaths"); Raise("PathText");
        }

        public string BeforePath
        {
            get { return Pick(_settings != null ? _settings.DbBeforePath : null, AppConfig.Current.DbBefore); }
        }

        public string AfterPath
        {
            get { return Pick(_settings != null ? _settings.DbAfterPath : null, AppConfig.Current.DbAfter); }
        }

        private static string Pick(string chosen, string fallback)
        {
            return string.IsNullOrEmpty(chosen) ? (fallback ?? string.Empty) : chosen;
        }

        /// <summary>어느 쪽 자리가 화면에서 고른 것인지 (지우기 단추를 보일지).</summary>
        public bool BeforeChosen
        {
            get { return _settings != null && _settings.DbBeforePath.Length > 0; }
        }

        public bool AfterChosen
        {
            get { return _settings != null && _settings.DbAfterPath.Length > 0; }
        }

        /// <summary>
        /// 화면에서 고른 자리를 적습니다. 빈 글자를 주면 config.txt 로
        /// 돌아갑니다. 저장은 부르는 쪽(창)이 합니다.
        /// </summary>
        public void SetPath(bool before, string path)
        {
            if (_settings == null) return;
            string v = path ?? string.Empty;
            if (before) _settings.DbBeforePath = v;
            else _settings.DbAfterPath = v;

            Raise("BeforePath"); Raise("AfterPath");
            Raise("BeforeChosen"); Raise("AfterChosen");
            Raise("HasPaths"); Raise("PathText");
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
                    return "견줄 두 자리를 정해 주세요. [찾기] 로 고르거나, "
                         + "칸에 폴더나 파일을 끌어다 놓으면 됩니다.\n"
                         + "config.txt 에 db.before / db.after 로 적어 두면 켤 때마다 그 자리입니다.\n"
                         + "폴더면 그 안의 .sql / .csv / .frm 을 (하위 폴더까지) 읽고, "
                         + "파일 하나면 그 파일을 읽습니다.";
                }
                return string.Empty;
            }
        }

        private DbDiffResult _diff;
        public DbDiffResult Diff { get { return _diff; } }

        // 견준 뒤에도 두 벌을 들고 있습니다. 표 정의 글을 만들 때 씁니다 —
        // 차이만 들고 있으면 "같은 표" 의 모양이 없어집니다.
        private DbSnapshot _before, _after;

        public bool HasSnapshots { get { return _before != null || _after != null; } }

        /// <summary>
        /// 읽은 표의 모양을 CREATE TABLE 글로. <b>그대로 돌릴 글이 아닙니다</b> —
        /// 서버의 SHOW CREATE TABLE 과 견주거나, 값을 옮길 때 받는 쪽 표를
        /// 만드는 출발점입니다. 빠진 것은 글 머리에 적혀 있습니다.
        /// </summary>
        public string CreateSqlText()
        {
            return DbCreateSql.Both(_before, _after);
        }

        private string _summary = "아직 읽지 않았습니다. [DB 읽기] 를 눌러 주세요.";
        public string Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        private string _notes = string.Empty;
        /// <summary>못 읽은 파일 같은 것. 비어 있으면 줄 자체가 사라집니다.</summary>
        public string Notes
        {
            get { return _notes; }
            private set { Set(ref _notes, value); }
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

        /// <summary>목록을 CSV 글로. 저장은 창이 맡습니다.</summary>
        public string LinesCsv()
        {
            return DbDiffList.ToCsv(_lines);
        }

        private void Rebuild()
        {
            // 늘 전체입니다. 표를 고르는 왼쪽 목록을 없앴으니 범위도 하나입니다.
            if (_diff == null)
            {
                _lines = new List<DbDiffLine>();
                EmptyText = "아직 읽지 않았습니다. [DB 읽기] 를 눌러 주세요.";
            }
            else
            {
                _lines = DbDiffList.Build(_diff, null);
                EmptyText = _diff.Tables.Count == 0
                    ? "읽은 표가 없습니다. 경로와 config.txt 를 봐 주세요."
                    : "두 DB 가 같습니다 — 다른 데가 없습니다.";
            }

            LineText = _lines.Count == 0 ? string.Empty : _lines.Count.ToString("N0") + " 줄";
            Raise("Lines");
            Raise("LinesIsEmpty");
        }

        private string _script = string.Empty;
        public string ScriptText
        {
            get { return _script; }
            private set { Set(ref _script, value); }
        }

        private bool _toAfter = true;
        /// <summary>참이면 "이전 → 이후", 거짓이면 되돌리는 글.</summary>
        public bool ToAfter
        {
            get { return _toAfter; }
            set { if (Set(ref _toAfter, value)) { Raise("ToBefore"); BuildScript(); } }
        }

        public bool ToBefore
        {
            get { return !_toAfter; }
            set { if (value) ToAfter = false; }
        }

        /// <summary>읽은 결과를 받습니다. 읽는 일은 창(DbView)이 맡습니다 — 진행 창을 띄워야 합니다.</summary>
        public void Take(DbSnapshot before, DbSnapshot after)
        {
            _before = before;
            _after = after;
            _diff = DbDiff.Compare(before, after);

            Summary = Overview(before, after, _diff);
            Notes = string.Join("\n", _diff.Notes.ToArray());

            Rebuild();
            BuildScript();
        }

        private static string Overview(DbSnapshot b, DbSnapshot a, DbDiffResult d)
        {
            var sb = new StringBuilder();
            sb.Append("표 ").Append(d.Tables.Count);
            sb.Append(" 개 중  달라짐 ").Append(d.TablesChanged);
            sb.Append(" · 이전에만 ").Append(d.TablesOnlyBefore);
            sb.Append(" · 이후에만 ").Append(d.TablesOnlyAfter);
            sb.Append(" · 같음 ").Append(d.TablesSame);
            sb.Append("      (읽은 행 ").Append(b.RowCount.ToString("N0"));
            sb.Append(" / ").Append(a.RowCount.ToString("N0")).Append(")");
            return sb.ToString();
        }

        public void BuildScript()
        {
            if (_diff == null) { ScriptText = string.Empty; return; }

            var opt = new SqlScript.Options();
            opt.Way = _toAfter ? SqlScript.Direction.ToAfter : SqlScript.Direction.ToBefore;
            ScriptText = SqlScript.Build(_diff, opt);
        }
    }
}
