using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using LogScope.App.Infrastructure;
using LogScope.Core.Db;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>차이 목록의 표 한 줄.</summary>
    public sealed class DbTableRowVm
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public TableDiff Diff { get; set; }

        /// <summary>달라진 줄만 굵게. 색은 쓰지 않습니다 — 글자로 적힙니다.</summary>
        public bool Changed { get; set; }
    }

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
            Tables = new ObservableCollection<DbTableRowVm>();
        }

        public ObservableCollection<DbTableRowVm> Tables { get; private set; }

        public string BeforePath { get { return AppConfig.Current.DbBefore; } }
        public string AfterPath { get { return AppConfig.Current.DbAfter; } }

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
                    return "config.txt 에 견줄 두 자리를 적어 주세요.\n\n"
                         + "    db.before = D:\\db\\이전\n"
                         + "    db.after  = D:\\db\\이후\n\n"
                         + "폴더면 그 안의 .sql / .csv 를 읽고, 파일 하나면 그 파일을 읽습니다.";
                }
                return "이전  " + BeforePath + "\n이후  " + AfterPath;
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

        private string _notes = string.Empty;
        /// <summary>못 읽은 파일 같은 것. 비어 있으면 줄 자체가 사라집니다.</summary>
        public string Notes
        {
            get { return _notes; }
            private set { Set(ref _notes, value); }
        }

        private DbTableRowVm _selected;
        public DbTableRowVm Selected
        {
            get { return _selected; }
            set
            {
                // 목록을 새로 만들 때 null 이 한 번 들어옵니다. 그때 고른 것을
                // 잃지 않게 무시합니다 (IO 목록에서 겪은 것과 같은 자리입니다).
                if (value == null) return;
                if (!Set(ref _selected, value)) return;
                Raise("DetailText");
            }
        }

        public string DetailText
        {
            get { return _selected == null ? "왼쪽에서 표를 골라 주세요." : Describe(_selected.Diff); }
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
            _diff = DbDiff.Compare(before, after);

            Tables.Clear();
            for (int i = 0; i < _diff.Tables.Count; i++)
            {
                TableDiff td = _diff.Tables[i];
                Tables.Add(new DbTableRowVm
                {
                    Name = td.Name,
                    Status = Status(td),
                    Detail = td.Summary(),
                    Diff = td,
                    Changed = td.Change != DbChange.Same,
                });
            }

            Summary = Overview(before, after, _diff);
            Notes = string.Join("\n", _diff.Notes.ToArray());

            _selected = null;
            Raise("Tables");
            Raise("DetailText");
            BuildScript();
        }

        private static string Status(TableDiff td)
        {
            switch (td.Change)
            {
                case DbChange.OnlyBefore: return "이전에만";
                case DbChange.OnlyAfter: return "이후에만";
                case DbChange.Changed: return "달라짐";
                default: return "같음";
            }
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

        /// <summary>표 하나의 차이를 글로. 화면에 그대로 나갑니다.</summary>
        private static string Describe(TableDiff td)
        {
            if (td == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("표  ").AppendLine(td.Name);
            sb.AppendLine(td.Summary());
            if (td.RowNote.Length > 0) sb.AppendLine(td.RowNote);
            sb.AppendLine();

            if (td.Change == DbChange.OnlyBefore || td.Change == DbChange.OnlyAfter)
            {
                DbTable only = td.Before ?? td.After;
                sb.AppendLine("열 " + (only == null ? 0 : only.Columns.Count) + " 개:");
                if (only != null)
                    for (int i = 0; i < only.Columns.Count; i++)
                        sb.AppendLine("    " + only.Columns[i].Name + "  " + only.Columns[i].Type);
                return sb.ToString();
            }

            sb.AppendLine("── 열 ──────────────────────────────");
            int shown = 0;
            for (int i = 0; i < td.Columns.Count; i++)
            {
                ColumnDiff cd = td.Columns[i];
                if (cd.Change == DbChange.Same) continue;
                sb.Append("  ").Append(cd.Name).Append("   ").AppendLine(cd.What());
                shown++;
            }
            if (shown == 0) sb.AppendLine("  (같음)");

            sb.AppendLine();
            sb.AppendLine("── 행 ──────────────────────────────");
            if (!td.RowsDiffer)
            {
                sb.AppendLine("  (같음)");
                return sb.ToString();
            }

            int lines = 0;
            for (int i = 0; i < td.Rows.Count && lines < 300; i++)
            {
                RowDiff rd = td.Rows[i];
                switch (rd.Change)
                {
                    case DbChange.OnlyBefore:
                        sb.Append("  − 없어진 행  ").AppendLine(Key(td, rd));
                        break;
                    case DbChange.OnlyAfter:
                        sb.Append("  + 생긴 행    ").AppendLine(Key(td, rd));
                        break;
                    default:
                        sb.Append("  ~ ").Append(Key(td, rd)).Append("   ");
                        for (int k = 0; k < rd.ChangedColumns.Count; k++)
                        {
                            int ci = rd.ChangedColumns[k];
                            string cname = ci < td.Before.Columns.Count ? td.Before.Columns[ci].Name : "?";
                            int ai = td.After.IndexOf(cname);
                            if (k > 0) sb.Append(", ");
                            sb.Append(cname).Append(": ")
                              .Append(DbRow.Show(rd.Before.Get(ci)))
                              .Append(" → ")
                              .Append(DbRow.Show(ai < 0 ? null : rd.After.Get(ai)));
                        }
                        sb.AppendLine();
                        break;
                }
                lines++;
            }
            if (td.Rows.Count > lines)
                sb.AppendLine("  … " + (td.Rows.Count - lines).ToString("N0") + " 줄 더 (화면에는 300 줄까지)");
            return sb.ToString();
        }

        private static string Key(TableDiff td, RowDiff rd)
        {
            DbTable shape = td.Before ?? td.After;
            if (shape == null || !shape.HasKey) return "(" + rd.Key.Replace('\u001f', ',') + ")";

            int[] keys = shape.KeyIndexes();
            DbRow row = rd.Before ?? rd.After;
            var sb = new StringBuilder();
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(shape.Columns[keys[i]].Name).Append('=')
                  .Append(DbRow.Show(row.Get(keys[i])));
            }
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
