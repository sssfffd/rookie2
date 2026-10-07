using System;
using System.Collections.Generic;
using System.Text;

// ==========================================================
//  DbCompare
//
// 견주기와 목록.
//
// 화면에 나오는 줄을 고칠 때 보는 파일입니다 (IO명 · v1 · v2).
//
//   DbDiff      두 벌을 견주는 세 단계 (표 → 열 → 행)
//   DbDiffList  그 차이를 화면 목록의 줄로 펼칩니다.
//               Io · V1 · V2 를 채우는 자리가 DbDiffList.OneRow 입니다.
//
//  칸에 "적히는 이름" 은 이 파일에 없습니다 — DbNames.cs 한 곳입니다
//  (config.txt 의 db.col.* 로도 바뀝니다). 여기 있는 것은 칸에 담기는
//  값입니다.
//
//  한 파일로 모았습니다 (0.73). 전에는 DbDiff · DbDiffList
//  로 나뉘어 있었습니다 — DB 화면을 고치려면 열 파일을 뒤져야 했습니다.
// ==========================================================

namespace LogScope.Core.Db
{
    // ======================================================
    //  DbDiff
    // ======================================================

    public enum DbChange
    {
        Same = 0,
        OnlyBefore = 1,
        OnlyAfter = 2,
        Changed = 3,
    }

    /// <summary>열 하나의 차이.</summary>
    public sealed class ColumnDiff
    {
        public string Name = string.Empty;
        public DbChange Change;
        public DbColumn Before;
        public DbColumn After;

        /// <summary>
        /// 이름이 바뀐 것으로 <b>보이는</b> 짝. 단정하지 않습니다 — 사라진 열과
        /// 새로 생긴 열이 모양과 자리가 같을 때의 짐작입니다.
        /// </summary>
        public string RenameGuess;

        public bool TypeChanged;
        public bool NullChanged;
        public bool DefaultChanged;

        /// <summary>
        /// 양쪽 기본값을 다 아는지. 거짓이면 기본값은 견주지 않았습니다 —
        /// <b>"같다" 가 아니라 "모른다"</b> 입니다.
        /// </summary>
        public bool DefaultKnown = true;
        public bool MovedOnly;          // 모양은 같고 자리만 바뀜

        public string What()
        {
            switch (Change)
            {
                case DbChange.OnlyBefore:
                    return RenameGuess != null
                        ? "없어짐 (이름이 \"" + RenameGuess + "\" 로 바뀐 듯)"
                        : "없어짐";
                case DbChange.OnlyAfter:
                    return RenameGuess != null
                        ? "새로 생김 (\"" + RenameGuess + "\" 의 이름이 바뀐 듯)"
                        : "새로 생김";
                case DbChange.Changed:
                    var parts = new List<string>();
                    if (TypeChanged) parts.Add("타입 " + Before.Type + " → " + After.Type);
                    if (NullChanged) parts.Add(Before.Nullable ? "NULL 허용 → NOT NULL" : "NOT NULL → NULL 허용");
                    if (DefaultChanged)
                        parts.Add("기본값 " + (Before.Default ?? "없음") + " → " + (After.Default ?? "없음"));
                    if (MovedOnly) parts.Add("자리 " + (Before.Ordinal + 1) + " → " + (After.Ordinal + 1));
                    return string.Join(", ", parts.ToArray());
                default:
                    return "같음";
            }
        }
    }

    /// <summary>행 하나의 차이.</summary>
    public sealed class RowDiff
    {
        public string Key = string.Empty;
        public DbChange Change;
        public DbRow Before;
        public DbRow After;

        /// <summary>바뀐 열의 자리들 (Changed 일 때만).</summary>
        public List<int> ChangedColumns = new List<int>();
    }

    /// <summary>표 하나의 차이.</summary>
    public sealed class TableDiff
    {
        public string Name = string.Empty;
        public DbChange Change;
        public DbTable Before;
        public DbTable After;

        public List<ColumnDiff> Columns = new List<ColumnDiff>();
        public List<RowDiff> Rows = new List<RowDiff>();

        /// <summary>행을 못 읽었거나 키가 없어 짐작으로 짝지을 수 없을 때의 설명.</summary>
        public string RowNote = string.Empty;

        public int ColumnsChanged;
        public int RowsAdded, RowsRemoved, RowsChanged;

        public bool SchemaChanged { get { return ColumnsChanged > 0; } }
        public bool RowsDiffer { get { return RowsAdded + RowsRemoved + RowsChanged > 0; } }

        public string Summary()
        {
            switch (Change)
            {
                case DbChange.OnlyBefore: return "이전에만 있음";
                case DbChange.OnlyAfter: return "이후에만 있음";
            }

            var parts = new List<string>();
            if (ColumnsChanged > 0) parts.Add("열 " + ColumnsChanged + " 곳");
            if (RowsChanged > 0) parts.Add("값 바뀐 행 " + RowsChanged.ToString("N0"));
            if (RowsAdded > 0) parts.Add("생긴 행 " + RowsAdded.ToString("N0"));
            if (RowsRemoved > 0) parts.Add("없어진 행 " + RowsRemoved.ToString("N0"));
            if (parts.Count == 0) return "같음";
            return string.Join(" · ", parts.ToArray());
        }
    }

    public sealed class DbDiffResult
    {
        public List<TableDiff> Tables = new List<TableDiff>();
        public List<string> Notes = new List<string>();

        public int TablesOnlyBefore, TablesOnlyAfter, TablesChanged, TablesSame;

        public bool AnyChange
        {
            get { return TablesOnlyBefore + TablesOnlyAfter + TablesChanged > 0; }
        }
    }

    /// <summary>
    /// 두 DB 한 벌을 견줍니다. 세 단계입니다.
    ///
    ///   1. 표    한쪽에만 있는 표
    ///   2. 열    추가·삭제·타입/NULL/기본값/자리 변경, 그리고 <b>이름 변경 짐작</b>
    ///   3. 행    키 기준으로 추가·삭제·값 변경
    ///
    /// <b>이름 변경은 짐작으로만 적습니다.</b> 사라진 열과 새로 생긴 열이 모양
    /// (타입·NULL·기본값)과 자리가 같으면 "이름이 바뀐 듯" 이라고 덧붙이되,
    /// 그렇다고 단정하지 않습니다. 단정하면 그걸 믿고 ALTER 를 돌리게 되는데,
    /// 실제로는 한 열을 지우고 다른 열을 더한 것일 수도 있습니다.
    ///
    /// <b>키가 없는 표</b>는 행을 짝지을 수가 없습니다. 그때는 "모든 열이 같은
    /// 행끼리" 묶으므로, 값이 하나 바뀐 행은 "없어진 행 1 + 생긴 행 1" 로
    /// 보입니다. 그게 아는 것의 전부이고, RowNote 에 그렇다고 적습니다.
    /// </summary>
    public static class DbDiff
    {
        /// <summary>표 하나에서 적어 둘 행 차이의 상한. 넘으면 세기만 합니다.</summary>
        public const int MaxRowDiffsPerTable = 5000;

        public static DbDiffResult Compare(DbSnapshot before, DbSnapshot after)
        {
            var res = new DbDiffResult();
            if (before == null) before = new DbSnapshot();
            if (after == null) after = new DbSnapshot();

            for (int i = 0; i < before.Notes.Count; i++) res.Notes.Add("이전: " + before.Notes[i]);
            for (int i = 0; i < after.Notes.Count; i++) res.Notes.Add("이후: " + after.Notes[i]);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < before.Tables.Count; i++)
            {
                DbTable b = before.Tables[i];
                seen.Add(b.Name);
                DbTable a = after.Find(b.Name);

                if (a == null)
                {
                    var d = new TableDiff();
                    d.Name = b.Name; d.Change = DbChange.OnlyBefore; d.Before = b;
                    res.Tables.Add(d);
                    res.TablesOnlyBefore++;
                    continue;
                }

                TableDiff td = CompareTable(b, a);
                res.Tables.Add(td);
                if (td.SchemaChanged || td.RowsDiffer) { td.Change = DbChange.Changed; res.TablesChanged++; }
                else { td.Change = DbChange.Same; res.TablesSame++; }
            }

            for (int i = 0; i < after.Tables.Count; i++)
            {
                DbTable a = after.Tables[i];
                if (seen.Contains(a.Name)) continue;
                var d = new TableDiff();
                d.Name = a.Name; d.Change = DbChange.OnlyAfter; d.After = a;
                res.Tables.Add(d);
                res.TablesOnlyAfter++;
            }

            res.Tables.Sort(delegate (TableDiff x, TableDiff y)
            {
                int rx = Rank(x), ry = Rank(y);
                if (rx != ry) return rx - ry;
                return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            return res;
        }

        /// <summary>달라진 표를 위로. "같음" 만 아래로 모읍니다.</summary>
        private static int Rank(TableDiff d)
        {
            switch (d.Change)
            {
                case DbChange.Changed: return 0;
                case DbChange.OnlyBefore: return 1;
                case DbChange.OnlyAfter: return 2;
                default: return 3;
            }
        }

        public static TableDiff CompareTable(DbTable b, DbTable a)
        {
            var td = new TableDiff();
            td.Name = b.Name;
            td.Before = b;
            td.After = a;

            CompareColumns(b, a, td);
            CompareRows(b, a, td);
            return td;
        }

        // ---------------- 열 ----------------

        private static void CompareColumns(DbTable b, DbTable a, TableDiff td)
        {
            var gone = new List<ColumnDiff>();
            var born = new List<ColumnDiff>();

            for (int i = 0; i < b.Columns.Count; i++)
            {
                DbColumn bc = b.Columns[i];
                DbColumn ac = a.Find(bc.Name);
                if (ac == null)
                {
                    var d = new ColumnDiff();
                    d.Name = bc.Name; d.Change = DbChange.OnlyBefore; d.Before = bc;
                    td.Columns.Add(d);
                    gone.Add(d);
                    continue;
                }

                var same = new ColumnDiff();
                same.Name = bc.Name;
                same.Before = bc; same.After = ac;
                same.TypeChanged = DbColumn.Norm(bc.Type) != DbColumn.Norm(ac.Type);
                same.NullChanged = bc.Nullable != ac.Nullable;
                // 한쪽이라도 기본값을 모르면 견주지 않습니다. 모르는 것을
                // "없음" 으로 치면 없는 차이가 생깁니다 (.frm 에서 모양만 읽은
                // 열이 그렇습니다).
                same.DefaultKnown = bc.DefaultKnown && ac.DefaultKnown;
                same.DefaultChanged = same.DefaultKnown
                    && (bc.Default ?? string.Empty) != (ac.Default ?? string.Empty);

                bool moved = bc.Ordinal != ac.Ordinal;
                if (same.TypeChanged || same.NullChanged || same.DefaultChanged)
                {
                    same.Change = DbChange.Changed;
                }
                else if (moved)
                {
                    // 자리만 바뀐 것도 차이입니다. SELECT * 의 순서가 달라집니다.
                    same.Change = DbChange.Changed;
                    same.MovedOnly = true;
                }
                else same.Change = DbChange.Same;

                td.Columns.Add(same);
            }

            for (int i = 0; i < a.Columns.Count; i++)
            {
                DbColumn ac = a.Columns[i];
                if (b.Find(ac.Name) != null) continue;
                var d = new ColumnDiff();
                d.Name = ac.Name; d.Change = DbChange.OnlyAfter; d.After = ac;
                td.Columns.Add(d);
                born.Add(d);
            }

            GuessRenames(gone, born);

            for (int i = 0; i < td.Columns.Count; i++)
                if (td.Columns[i].Change != DbChange.Same) td.ColumnsChanged++;
        }

        /// <summary>
        /// 사라진 열과 새로 생긴 열을 짝지어 봅니다. <b>모양과 자리가 같을 때만</b>
        /// 이고, 그래도 "그런 듯" 이라고만 적습니다.
        ///
        /// 한쪽이 둘 이상이면 짝짓지 않습니다 — 어느 것이 어느 것인지 알 수
        /// 없는데 하나를 고르면 그게 틀렸을 때 ALTER 가 엉뚱한 열을 건드립니다.
        /// </summary>
        private static void GuessRenames(List<ColumnDiff> gone, List<ColumnDiff> born)
        {
            for (int i = 0; i < gone.Count; i++)
            {
                ColumnDiff g = gone[i];
                ColumnDiff match = null;
                int hits = 0;

                for (int j = 0; j < born.Count; j++)
                {
                    ColumnDiff n = born[j];
                    if (n.RenameGuess != null) continue;
                    if (g.Before.Shape() != n.After.Shape()) continue;
                    if (g.Before.Ordinal != n.After.Ordinal) continue;
                    hits++;
                    match = n;
                }

                if (hits != 1) continue;
                g.RenameGuess = match.Name;
                match.RenameGuess = g.Name;
            }
        }

        // ---------------- 행 ----------------

        private static void CompareRows(DbTable b, DbTable a, TableDiff td)
        {
            if (!b.HasRows || !a.HasRows)
            {
                td.RowNote = "행을 읽지 못했습니다 (모양만 견줬습니다).";
                return;
            }

            int[] bk = b.KeyIndexes();
            int[] ak = a.KeyIndexes();

            // 양쪽 키가 같아야 행을 짝지을 수 있습니다. 다르면 키 없는 것으로 봅니다.
            bool keyed = bk.Length > 0 && bk.Length == ak.Length;
            if (keyed)
            {
                for (int i = 0; i < bk.Length; i++)
                {
                    if (!DbTable.Same(b.Columns[bk[i]].Name, a.Columns[ak[i]].Name)) { keyed = false; break; }
                }
            }

            if (!keyed)
            {
                td.RowNote = "기본 키가 없어 모든 열이 같은 행끼리만 짝지었습니다. "
                           + "값이 하나 바뀐 행은 \"없어진 행 1 + 생긴 행 1\" 로 보입니다.";
            }

            // 이후 쪽을 열쇠로 색인합니다. 같은 열쇠가 여럿이면 차례대로 씁니다.
            var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < a.Rows.Count; i++)
            {
                string key = a.RowKey(a.Rows[i], keyed ? ak : null);
                List<int> slot;
                if (!index.TryGetValue(key, out slot)) { slot = new List<int>(); index[key] = slot; }
                slot.Add(i);
            }

            var usedAfter = new bool[a.Rows.Count];

            for (int i = 0; i < b.Rows.Count; i++)
            {
                DbRow br = b.Rows[i];
                string key = b.RowKey(br, keyed ? bk : null);

                List<int> slot;
                int at = -1;
                if (index.TryGetValue(key, out slot))
                {
                    for (int k = 0; k < slot.Count; k++)
                    {
                        if (usedAfter[slot[k]]) continue;
                        at = slot[k];
                        break;
                    }
                }

                if (at < 0)
                {
                    td.RowsRemoved++;
                    Add(td, new RowDiff { Key = key, Change = DbChange.OnlyBefore, Before = br });
                    continue;
                }

                usedAfter[at] = true;
                DbRow ar = a.Rows[at];

                var changed = new List<int>();
                for (int c = 0; c < b.Columns.Count; c++)
                {
                    int ai = a.IndexOf(b.Columns[c].Name);
                    if (ai < 0) continue;              // 없어진 열은 열 차이로 이미 적혔습니다
                    string bv = br.Get(c), av = ar.Get(ai);
                    if (bv == null && av == null) continue;
                    if (bv != null && av != null && string.Equals(bv, av, StringComparison.Ordinal)) continue;
                    changed.Add(c);
                }

                if (changed.Count == 0) continue;

                td.RowsChanged++;
                var d = new RowDiff { Key = key, Change = DbChange.Changed, Before = br, After = ar };
                d.ChangedColumns = changed;
                Add(td, d);
            }

            for (int i = 0; i < a.Rows.Count; i++)
            {
                if (usedAfter[i]) continue;
                td.RowsAdded++;
                Add(td, new RowDiff
                {
                    Key = a.RowKey(a.Rows[i], keyed ? ak : null),
                    Change = DbChange.OnlyAfter,
                    After = a.Rows[i],
                });
            }
        }

        /// <summary>
        /// 차이를 적어 둡니다. 상한을 넘으면 <b>세는 것만</b> 계속합니다 —
        /// 10 만 줄을 화면에 띄워도 읽을 수 없고, 그 전에 메모리가 먼저 찹니다.
        /// </summary>
        private static void Add(TableDiff td, RowDiff d)
        {
            if (td.Rows.Count >= MaxRowDiffsPerTable)
            {
                if (!td.RowNote.Contains("상한"))
                {
                    td.RowNote = (td.RowNote.Length > 0 ? td.RowNote + " " : "")
                               + "차이가 " + MaxRowDiffsPerTable.ToString("N0")
                               + " 줄을 넘어 그 뒤는 세기만 했습니다 (상한).";
                }
                return;
            }
            td.Rows.Add(d);
        }
    }

    // ======================================================
    //  DbDiffList
    // ======================================================

    /// <summary>
    /// 차이 목록의 한 줄. <b>칸마다 값 하나</b>입니다.
    ///
    /// 글 한 덩이로 적으면 "달라진 것이 많다" 는 느낌만 남고, 어느 표의 어느
    /// 열이 몇 번 걸렸는지는 눈으로 세야 합니다. 칸으로 쪼개 두면 그대로
    /// 목록이 되고, CSV 로 내보내 엑셀에서 걸러 볼 수도 있습니다.
    ///
    /// 바인딩이 읽어야 하므로 <b>필드가 아니라 속성</b>입니다. WPF 는 필드를
    /// 바인딩하지 못합니다 — 조용히 빈 칸으로 나옵니다.
    /// </summary>
    public sealed class DbDiffLine
    {
        /// <summary>표 이름. 하위 폴더까지 읽으면 "mydb.users" 처럼 앞이 붙습니다.</summary>
        public string Table { get; set; }

        /// <summary>
        /// <see cref="Kind"/> 와 <see cref="Change"/> 에 들어갈 수 있는 글자.
        /// <b>여기 적힌 것이 전부입니다.</b>
        ///
        /// 글자를 직접 적지 마세요. 이 값은 화면에 보이기도 하지만
        /// <b>비교되기도 합니다</b> — DbVm 은 알림 줄을 목록에서 빼내려고
        /// <c>Kind</c> 를 견줍니다. 쓰는 쪽과 견주는 쪽에 같은 글자가 따로
        /// 적혀 있으면, 한 군데만 고쳤을 때 컴파일도 되고 시험도 통과하는데
        /// <b>알림이 목록에 섞여 나옵니다.</b>
        ///
        /// 그래서 이 넷은 config.txt 로 바꾸지 않습니다 (칸 <b>이름</b>은
        /// <see cref="DbNames"/> 로 바꿉니다). 견주는 값을 설정으로 바꾸게
        /// 하면 그 순간 견주기가 깨집니다.
        /// </summary>
        public const string KindTable = "표";
        public const string KindColumn = "열";
        public const string KindRow = "행";
        public const string KindNote = "알림";

        public const string ChangeAdded = "생김";
        public const string ChangeGone = "없어짐";
        public const string ChangeDiff = "달라짐";
        public const string ChangeMoved = "자리";   // 값은 같고 열 자리만 바뀜

        /// <summary>표 · 열 · 행 · 알림. 위 Kind... 상수 중 하나입니다.</summary>
        public string Kind { get; set; }

        /// <summary>생김 · 없어짐 · 달라짐 · 자리. 알림 줄은 빕니다.</summary>
        public string Change { get; set; }

        /// <summary>행 줄에서 그 행을 가리키는 열쇠 ("id=7").</summary>
        public string Where { get; set; }

        /// <summary>
        /// 이 줄이 가리키는 <b>IO 이름</b>. 목록의 첫 칸입니다.
        ///
        /// 기본으로 채우는 값은 "그 줄을 가리키는 가장 좁은 이름" 입니다 —
        /// 행이면 행 열쇠(<c>id=7</c>), 열이면 열 이름, 표 단위 줄이면 표
        /// 이름입니다. DB 에 IO 이름이 담긴 열이 따로 있으면
        /// <see cref="DbDiffList"/> 를 고쳐 그 값을 넣으면 됩니다.
        /// </summary>
        public string Io { get; set; }

        /// <summary>
        /// 쓰는 쪽에서 채우는 빈 칸 둘. 설비 DB 마다 함께 봐야 하는 열이
        /// 달라서(설정값 · 단위 · 판본 같은 것) 여기 이름을 박지 않았습니다.
        ///
        /// <b>기본은 빈 글자입니다.</b> 비어 있으면 칸은 그냥 빕니다 —
        /// 아무 값이나 채워 두면 그게 DB 에서 읽은 값인 줄 알고 읽게 됩니다.
        /// 채우는 자리는 <c>DbDiffList.OneRow</c> 입니다.
        /// </summary>
        public string V1 { get; set; }

        public string V2 { get; set; }

        /// <summary>열 이름.</summary>
        public string Column { get; set; }

        /// <summary>이전 값(행) 또는 이전 모양(열). 그 쪽에 없으면 빈 칸입니다.</summary>
        public string Before { get; set; }

        public string After { get; set; }

        /// <summary>짐작·상한·키 없음 같은 덧말.</summary>
        public string Note { get; set; }

        public DbDiffLine()
        {
            Table = string.Empty; Kind = string.Empty; Change = string.Empty;
            Where = string.Empty; Column = string.Empty; Io = string.Empty;
            Before = string.Empty; After = string.Empty; Note = string.Empty;
            V1 = string.Empty; V2 = string.Empty;
        }

        /// <summary>
        /// 줄 하나를 한 줄 글로. 칸이 좁아 잘린 값을 마우스로 짚어 보게 하는
        /// 용도입니다 — 값이 긴 열에서는 칸만으로는 읽을 수가 없습니다.
        /// </summary>
        public string Tip
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Table);
                if (Kind.Length > 0) sb.Append("  [").Append(Kind).Append(']');
                if (Change.Length > 0) sb.Append(' ').Append(Change);
                // 앞에 붙는 이름은 목록 칸 이름을 그대로 씁니다 — 칸 이름을
                // 바꿨는데 설명 글만 옛 이름으로 남으면 안 됩니다.
                Label(sb, DbNames.Io, Io);
                Label(sb, DbNames.Where, Where);
                Label(sb, DbNames.Column, Column);
                if (Before.Length > 0 || After.Length > 0)
                {
                    Label(sb, DbNames.Before, Before);
                    Label(sb, DbNames.After, After);
                }
                if (V1.Length > 0 || V2.Length > 0)
                {
                    Label(sb, DbNames.V1, V1);
                    Label(sb, DbNames.V2, V2);
                }
                if (Note.Length > 0) sb.Append('\n').Append(Note);
                return sb.ToString();
            }
        }

        /// <summary>"이름  값" 한 줄. 값이 비면 줄을 안 만듭니다.</summary>
        private static void Label(StringBuilder sb, string name, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            sb.Append('\n').Append(name).Append("  ").Append(value);
        }
    }

    /// <summary>
    /// 표 단위 차이(<see cref="DbDiffResult"/>)를 <b>한 줄에 한 칸씩</b> 펼칩니다.
    ///
    /// 값이 바뀐 행은 <b>바뀐 열마다 한 줄</b>입니다. 한 행을 한 줄로 묶으면
    /// "이전" 칸에 여러 값이 들어가 다시 글이 되고, 그러면 같은 열이 몇 군데서
    /// 바뀌었는지 셀 수 없습니다.
    ///
    /// 생기거나 없어진 행도 열마다 한 줄로 펼칩니다. 그래야 NULL 과 빈 글자가
    /// 구분되고, 엑셀에서 열 기준으로 걸러집니다.
    ///
    /// <b>표 자체가 한쪽에만 있을 때는 행을 펼치지 않습니다.</b> 행 1 만 개가
    /// 전부 "생김" 으로 깔려도 아는 것은 "이 표가 새것" 하나뿐입니다. 열 모양만
    /// 적고, 행 수는 알림에 적습니다.
    /// </summary>
    public static class DbDiffList
    {
        /// <summary>목록 전체 줄 수 상한.</summary>
        public const int MaxLines = 50000;

        /// <summary>표 하나가 차지할 수 있는 줄 수 상한. 한 표가 목록을 다 먹지 않게.</summary>
        public const int MaxLinesPerTable = 5000;

        public sealed class Options
        {
            public int MaxLines = DbDiffList.MaxLines;
            public int MaxLinesPerTable = DbDiffList.MaxLinesPerTable;

            /// <summary>생기거나 없어진 행을 열마다 펼칠지. 거짓이면 행 한 줄로 묶습니다.</summary>
            public bool ExpandWholeRows = true;
        }

        /// <summary>DB 한 벌 전체. "같음" 인 표는 줄을 만들지 않습니다.</summary>
        public static List<DbDiffLine> Build(DbDiffResult diff, Options opt)
        {
            var sink = new Sink(opt);
            if (diff != null)
            {
                for (int i = 0; i < diff.Tables.Count; i++)
                {
                    if (sink.Full) break;
                    OneTable(diff.Tables[i], sink);
                }
            }
            sink.Close();
            return sink.Lines;
        }

        /// <summary>표 하나만.</summary>
        public static List<DbDiffLine> Build(TableDiff table, Options opt)
        {
            var sink = new Sink(opt);
            if (table != null) OneTable(table, sink);
            sink.Close();
            return sink.Lines;
        }

        // ---------------- 줄 담는 통 ----------------

        /// <summary>
        /// 상한을 지키면서 줄을 담습니다. 상한에 닿으면 <b>그렇다고 적습니다</b> —
        /// 조용히 끊으면 "차이가 이것뿐" 으로 읽힙니다.
        /// </summary>
        private sealed class Sink
        {
            public readonly List<DbDiffLine> Lines = new List<DbDiffLine>();
            public readonly Options Opt;

            private int _inTable;
            private bool _capped;

            public Sink(Options opt) { Opt = opt ?? new Options(); }

            public bool Full { get { return Lines.Count >= Opt.MaxLines; } }

            public void Start() { _inTable = 0; }

            /// <summary>담았으면 참. 거짓이면 이 표는 여기서 그칩니다.</summary>
            public bool Add(DbDiffLine ln)
            {
                if (Full) { _capped = true; return false; }
                if (_inTable >= Opt.MaxLinesPerTable) return false;
                Lines.Add(ln);
                _inTable++;
                return true;
            }

            /// <summary>상한을 넘겼다고 알리는 줄. 이 줄은 상한에 걸리지 않습니다.</summary>
            public void Say(string table, string note)
            {
                Lines.Add(new DbDiffLine { Table = table, Kind = DbDiffLine.KindNote, Note = note });
            }

            public bool TableCapped { get { return _inTable >= Opt.MaxLinesPerTable; } }

            public void Close()
            {
                if (_capped)
                    Say(string.Empty, "목록이 " + Opt.MaxLines.ToString("N0")
                        + " 줄을 넘어 그 뒤는 적지 않았습니다 (상한). "
                        + "왼쪽에서 표를 골라 [고른 표만] 으로 보거나, CSV 로 내보내 보세요.");
            }
        }

        // ---------------- 표 하나 ----------------

        private static void OneTable(TableDiff td, Sink s)
        {
            if (td == null || td.Change == DbChange.Same) return;
            s.Start();

            if (td.Change == DbChange.OnlyBefore || td.Change == DbChange.OnlyAfter)
            {
                OnlyOneSide(td, s);
                return;
            }

            // 알림을 맨 위에 둡니다. 아래 줄들을 어떻게 읽어야 하는지가
            // 거기 적혀 있습니다 (키가 없다, 모양만 읽었다, 상한에 걸렸다).
            if (td.RowNote.Length > 0) s.Add(new DbDiffLine
            {
                Table = td.Name, Kind = DbDiffLine.KindNote, Note = td.RowNote,
            });

            for (int i = 0; i < td.Columns.Count; i++)
            {
                ColumnDiff cd = td.Columns[i];
                if (cd.Change == DbChange.Same) continue;
                if (!s.Add(ColumnLine(td.Name, cd))) { Cut(td, s); return; }
            }

            for (int i = 0; i < td.Rows.Count; i++)
            {
                if (!OneRow(td, td.Rows[i], s)) { Cut(td, s); return; }
            }
        }

        private static void Cut(TableDiff td, Sink s)
        {
            if (!s.TableCapped) return;      // 전체 상한이면 Close 에서 한 번만 적습니다
            s.Say(td.Name, "이 표의 차이가 " + s.Opt.MaxLinesPerTable.ToString("N0")
                + " 줄을 넘어 그 뒤는 적지 않았습니다 (상한). " + td.Summary() + " 입니다.");
        }

        private static void OnlyOneSide(TableDiff td, Sink s)
        {
            bool before = td.Change == DbChange.OnlyBefore;
            DbTable only = before ? td.Before : td.After;
            string ch = before ? DbDiffLine.ChangeGone : DbDiffLine.ChangeAdded;

            int cols = only == null ? 0 : only.Columns.Count;
            int rows = only == null ? 0 : only.Rows.Count;

            var head = new DbDiffLine { Table = td.Name, Kind = DbDiffLine.KindTable, Change = ch, Io = td.Name };
            head.Note = "표 자체가 " + (before ? "이전에만" : "이후에만") + " 있습니다"
                      + " — 열 " + cols + " 개 · 읽은 행 " + rows.ToString("N0") + " 개."
                      + " 행은 칸마다 적지 않습니다 (전부 한쪽에만 있는 것이라 적어도 같은 말입니다).";
            s.Add(head);

            if (only == null) return;
            for (int i = 0; i < only.Columns.Count; i++)
            {
                DbColumn c = only.Columns[i];
                var ln = new DbDiffLine
                {
                    Table = td.Name, Kind = DbDiffLine.KindColumn, Change = ch, Column = c.Name, Io = c.Name,
                    Note = c.IsKey ? "기본 키" : string.Empty,
                };
                if (before) ln.Before = c.Shape(); else ln.After = c.Shape();
                if (!s.Add(ln)) { Cut(td, s); return; }
            }
        }

        private static DbDiffLine ColumnLine(string table, ColumnDiff cd)
        {
            var ln = new DbDiffLine { Table = table, Kind = DbDiffLine.KindColumn, Column = cd.Name, Io = cd.Name };
            ln.Change = cd.Change == DbChange.OnlyBefore ? DbDiffLine.ChangeGone
                      : cd.Change == DbChange.OnlyAfter ? DbDiffLine.ChangeAdded
                      : cd.MovedOnly ? DbDiffLine.ChangeMoved : DbDiffLine.ChangeDiff;
            if (cd.Before != null) ln.Before = cd.Before.Shape();
            if (cd.After != null) ln.After = cd.After.Shape();
            ln.Note = cd.What();
            return ln;
        }

        // ---------------- 행 하나 ----------------

        /// <summary>
        /// 행 하나를 줄로 펼칩니다.
        ///
        /// <b>v1 · v2 를 채우는 자리가 여기입니다.</b> 설비 DB 마다 함께 봐야
        /// 하는 열이 달라서 이름을 박아 두지 않았습니다. 그 표에서 값을 꺼내
        /// <c>ln.V1</c> · <c>ln.V2</c> 에 넣으면 목록의 v1 · v2 칸에 그대로
        /// 나옵니다. 꺼낼 때는 <c>td.Before</c> / <c>td.After</c> 의
        /// <c>IndexOf("열이름")</c> 으로 자리를 찾고 <c>rd.Before.Get(i)</c> 로
        /// 읽습니다 — 위의 "달라진 열" 을 꺼내는 것과 같은 방법입니다.
        ///
        /// IO 이름도 같습니다. 지금은 행 열쇠를 넣는데, 이름이 담긴 열이
        /// 따로 있으면 <c>ln.Io</c> 에 그 값을 넣으면 됩니다.
        /// </summary>
        private static bool OneRow(TableDiff td, RowDiff rd, Sink s)
        {
            string key = KeyText(td, rd);

            if (rd.Change == DbChange.Changed)
            {
                for (int k = 0; k < rd.ChangedColumns.Count; k++)
                {
                    int ci = rd.ChangedColumns[k];
                    string name = td.Before != null && ci < td.Before.Columns.Count
                                ? td.Before.Columns[ci].Name : "?";
                    int ai = td.After == null ? -1 : td.After.IndexOf(name);

                    var ln = new DbDiffLine
                    {
                        Table = td.Name, Kind = DbDiffLine.KindRow, Change = DbDiffLine.ChangeDiff,
                        Where = key, Column = name, Io = key,
                        Before = DbRow.Show(rd.Before.Get(ci)),
                        After = DbRow.Show(ai < 0 || rd.After == null ? null : rd.After.Get(ai)),
                    };
                    if (!s.Add(ln)) return false;
                }
                return true;
            }

            bool before = rd.Change == DbChange.OnlyBefore;
            DbRow row = before ? rd.Before : rd.After;
            DbTable shape = before ? td.Before : td.After;
            string ch = before ? DbDiffLine.ChangeGone : DbDiffLine.ChangeAdded;

            if (row == null || shape == null || !s.Opt.ExpandWholeRows)
            {
                var one = new DbDiffLine
                {
                    Table = td.Name, Kind = DbDiffLine.KindRow, Change = ch, Where = key, Io = key,
                    Column = "(행 전체)",
                };
                string all = RowText(shape, row);
                if (before) one.Before = all; else one.After = all;
                return s.Add(one);
            }

            for (int c = 0; c < shape.Columns.Count; c++)
            {
                var ln = new DbDiffLine
                {
                    Table = td.Name, Kind = DbDiffLine.KindRow, Change = ch, Where = key, Io = key,
                    Column = shape.Columns[c].Name,
                };
                // 한쪽 칸은 비워 둡니다. "없음" 과 NULL 은 다른 것입니다 —
                // NULL 은 값이 NULL 인 것이고, 빈 칸은 그 행이 없는 것입니다.
                if (before) ln.Before = DbRow.Show(row.Get(c));
                else ln.After = DbRow.Show(row.Get(c));
                if (!s.Add(ln)) return false;
            }
            return true;
        }

        /// <summary>행을 가리키는 글. 기본 키가 있으면 "id=7", 없으면 값들을 늘어놓습니다.</summary>
        public static string KeyText(TableDiff td, RowDiff rd)
        {
            if (td == null || rd == null) return string.Empty;

            DbTable shape = td.Before ?? td.After;
            DbRow row = rd.Before ?? rd.After;
            if (shape == null || row == null || !shape.HasKey)
                return "(" + (rd.Key ?? string.Empty).Replace('\u001f', ',') + ")";

            int[] keys = shape.KeyIndexes();
            var sb = new StringBuilder();
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(shape.Columns[keys[i]].Name).Append('=')
                  .Append(DbRow.Show(row.Get(keys[i])));
            }
            return sb.ToString();
        }

        private static string RowText(DbTable shape, DbRow row)
        {
            if (row == null) return string.Empty;
            var sb = new StringBuilder();
            int n = shape == null ? row.Values.Length : shape.Columns.Count;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(", ");
                if (shape != null) sb.Append(shape.Columns[i].Name).Append('=');
                sb.Append(DbRow.Show(row.Get(i)));
            }
            return sb.ToString();
        }

        // ---------------- CSV ----------------

        /// <summary>
        /// 목록을 CSV 로. 엑셀에서 열어 거르고 정렬하는 쪽이 이 화면보다 낫습니다.
        ///
        /// <b>값은 손대지 않고 그대로 내보냅니다.</b> 그래서 "=" 로 시작하는 값은
        /// 엑셀이 수식으로 읽을 수 있습니다. 앞에 따옴표를 붙여 막을 수도 있지만
        /// 그러면 "-5" 같은 흔한 값까지 글자로 바뀌어, 차이를 보려고 뽑은 목록의
        /// 값이 원래와 달라집니다. 바꾸는 쪽이 더 나쁩니다.
        /// </summary>
        public static string ToCsv(List<DbDiffLine> lines)
        {
            var sb = new StringBuilder();
            // 머리글 이름은 DbNames 한 곳에 있습니다 (config.txt 로도 바뀝니다).
            // 값과 같은 Cell 로 찍으므로 이름에 쉼표가 들어가도 깨지지 않습니다.
            string[] head = DbNames.CsvNames();
            for (int h = 0; h < head.Length; h++)
            {
                if (h > 0) sb.Append(',');
                Cell(sb, head[h]);
            }
            sb.Append("\r\n");
            if (lines == null) return sb.ToString();

            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine ln = lines[i];
                Cell(sb, ln.Table); sb.Append(',');
                Cell(sb, ln.Io); sb.Append(',');
                Cell(sb, ln.Kind); sb.Append(',');
                Cell(sb, ln.Change); sb.Append(',');
                Cell(sb, ln.Where); sb.Append(',');
                Cell(sb, ln.Column); sb.Append(',');
                Cell(sb, ln.Before); sb.Append(',');
                Cell(sb, ln.After); sb.Append(',');
                Cell(sb, ln.V1); sb.Append(',');
                Cell(sb, ln.V2); sb.Append(',');
                Cell(sb, ln.Note);
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private static void Cell(StringBuilder sb, string v)
        {
            if (string.IsNullOrEmpty(v)) return;
            bool quote = v.IndexOf(',') >= 0 || v.IndexOf('"') >= 0
                      || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0;
            if (!quote) { sb.Append(v); return; }

            sb.Append('"');
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                if (c == '"') sb.Append('"');
                sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
