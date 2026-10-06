using System;
using System.Collections.Generic;
using System.Text;

namespace LogScope.Core.Db
{
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
}
