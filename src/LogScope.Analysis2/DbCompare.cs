using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// ==========================================================
//  DbCompare
//
// 견주기와 목록.
//
// 화면에 나오는 줄을 고칠 때 보는 파일입니다.
//
//   DbDiff      두 벌을 견주는 세 단계 (파일 → 열 → 행)
//   DbDiffList  그 차이를 화면 목록의 줄로 펼칩니다.
//               줄에 값을 담는 자리가 DbDiffList.OneRow 입니다.
//
//  칸에 "적히는 이름" 은 이 파일에 없습니다 — DbNames.cs 한 곳입니다
//  — 거기 한 줄만 고치면 됩니다. 여기 있는 것은 칸에 담기는
//  값입니다.
//
//  한 파일로 모았습니다 (0.73). 전에는 DbDiff · DbDiffList
//  로 나뉘어 있었습니다 — DB 화면을 고치려면 열 파일을 뒤져야 했습니다.
// ==========================================================

namespace LogScope.Analysis2
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

        /// <summary>
        /// <b>두 쪽이 같은 줄들.</b> 차이 목록과 따로 담습니다.
        ///
        /// 0.97 까지는 아예 안 담았습니다 (차이만 담으면 되니까요). 그러면
        /// 화면에서 "모든 IO 보기" 를 할 수가 없습니다 — 같은 IO 는 어디에도
        /// 없으니 무엇을 어떻게 걸러도 차이 난 IO 만 나옵니다. 견준 IO 가
        /// 목록에 없으면 "이 IO 는 봤는데 괜찮았다" 를 확인할 길이 없습니다.
        ///
        /// 차이 목록과 <b>따로</b> 담는 까닭: 같은 줄이 훨씬 많아서, 한
        /// 통에 담으면 상한을 같은 줄이 다 먹고 차이가 밀려 나갑니다.
        /// </summary>
        public List<RowDiff> SameRows = new List<RowDiff>();

        /// <summary>행을 못 읽었거나 키가 없어 짐작으로 짝지을 수 없을 때의 설명.</summary>
        public string RowNote = string.Empty;

        /// <summary>
        /// <b>이름이 달랐는데 짝지은 경우</b> 이후 쪽 표 이름. 같으면 빈 글자입니다.
        ///
        /// <see cref="Name"/> 을 "a ↔ b" 처럼 바꾸지 않은 이유: 그 이름이
        /// 그대로 <c>ALTER TABLE</c> 의 식별자로 나갑니다 (DbSql). 거기에
        /// 화살표가 섞이면 돌아가지 않는 SQL 이 나옵니다.
        /// </summary>
        public string PairedName = string.Empty;

        public int ColumnsChanged;
        public int RowsAdded, RowsRemoved, RowsChanged;

        /// <summary>두 쪽이 같았던 행 수. 담은 것(SameRows)보다 많을 수 있습니다 (상한).</summary>
        public int RowsSame;

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

        /// <summary>
        /// 표 하나에서 <b>같은 줄</b>을 몇 개까지 담을지. "모든 IO 보기" 를
        /// 위한 것이라 차이보다 넉넉히 둡니다 — IO 표는 보통 수백~수천
        /// 줄입니다. 넘으면 담기를 그치고 <b>그렇다고 적습니다</b>.
        /// </summary>
        public const int MaxSameRowsPerTable = 20000;

        public static DbDiffResult Compare(DbSnapshot before, DbSnapshot after)
        {
            var res = new DbDiffResult();
            if (before == null) before = new DbSnapshot();
            if (after == null) after = new DbSnapshot();

            for (int i = 0; i < before.Notes.Count; i++) res.Notes.Add("이전: " + before.Notes[i]);
            for (int i = 0; i < after.Notes.Count; i++) res.Notes.Add("이후: " + after.Notes[i]);

            // 1) 이름이 같은 표끼리 짝짓습니다.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leftBefore = new List<DbTable>();

            for (int i = 0; i < before.Tables.Count; i++)
            {
                DbTable b = before.Tables[i];
                seen.Add(b.Name);
                DbTable a = after.Find(b.Name);
                if (a == null) { leftBefore.Add(b); continue; }

                Took(res, CompareTable(b, a));
            }

            var leftAfter = new List<DbTable>();
            for (int i = 0; i < after.Tables.Count; i++)
            {
                DbTable a = after.Tables[i];
                if (!seen.Contains(a.Name)) leftAfter.Add(a);
            }

            // 2) 이름으로 못 지은 것끼리 짝짓습니다 (.csv 파일 이름이 다른 경우).
            PairLeftovers(res, leftBefore, leftAfter);

            // 3) 그래도 남은 것은 한쪽에만 있는 표입니다.
            for (int i = 0; i < leftBefore.Count; i++)
            {
                if (leftBefore[i] == null) continue;
                var d = new TableDiff();
                d.Name = leftBefore[i].Name; d.Change = DbChange.OnlyBefore; d.Before = leftBefore[i];
                res.Tables.Add(d);
                res.TablesOnlyBefore++;
            }
            for (int i = 0; i < leftAfter.Count; i++)
            {
                if (leftAfter[i] == null) continue;
                var d = new TableDiff();
                d.Name = leftAfter[i].Name; d.Change = DbChange.OnlyAfter; d.After = leftAfter[i];
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

        /// <summary>짝지은 표 하나를 결과에 담습니다.</summary>
        private static void Took(DbDiffResult res, TableDiff td)
        {
            res.Tables.Add(td);
            if (td.SchemaChanged || td.RowsDiffer) { td.Change = DbChange.Changed; res.TablesChanged++; }
            else { td.Change = DbChange.Same; res.TablesSame++; }
        }

        /// <summary>
        /// <b>이름이 달라도 짝지어 줍니다.</b>
        ///
        /// `.csv` 는 <b>파일 이름이 표 이름</b>입니다. 그래서 같은 설비의
        /// 같은 자료인데도 <c>2026-03-14.csv</c> / <c>2026-03-21.csv</c> 처럼
        /// 이름이 다르면, 이름으로만 짝지을 때 "없어짐 + 생김" 이 되어
        /// <b>차이를 하나도 못 보게</b> 됩니다.
        ///
        /// 규칙은 둘뿐입니다. 짐작을 늘리면 엉뚱한 표를 견주게 됩니다.
        ///
        /// 규칙은 하나입니다. <b>열 구성(열 이름)이 똑같은 표가 양쪽에
        /// 하나씩</b>일 때만 짝짓습니다.
        ///
        /// "양쪽에 하나씩 남았으면 그 둘" 로도 해 봤는데 위험했습니다 —
        /// 서로 상관없는 표 둘이 남아 있을 때 그것들을 견주게 됩니다.
        /// 열 이름이 똑같다는 것은 <b>같은 자료를 두 번 뽑은 것</b>이라는
        /// 꽤 센 근거이고, 이름이 다를 때 우리가 가진 유일한 근거입니다.
        ///
        /// 아니면 짝짓지 않고, <b>왜 못 지었는지 적습니다.</b>
        /// 그리고 짝지을 때마다 그렇다고 적습니다 — 이름이 다른 둘을
        /// 말없이 견주면, 보는 사람은 같은 표를 본 줄로 압니다.
        /// </summary>
        private static void PairLeftovers(DbDiffResult res, List<DbTable> b, List<DbTable> a)
        {
            if (b.Count == 0 || a.Count == 0) return;

            for (int i = 0; i < b.Count; i++)
            {
                if (b[i] == null) continue;

                string sig = Signature(b[i]);
                if (sig.Length == 0) continue;     // 열을 못 읽은 표끼리는 짝짓지 않습니다

                int mine = 0;
                for (int k = 0; k < b.Count; k++)
                    if (b[k] != null && Signature(b[k]) == sig) mine++;
                if (mine != 1) continue;          // 이전 쪽에 같은 모양이 여럿

                int found = -1, hits = 0;
                for (int j = 0; j < a.Count; j++)
                {
                    if (a[j] == null || Signature(a[j]) != sig) continue;
                    found = j; hits++;
                }

                if (hits == 1) Pair(res, b, i, a, found, "열 구성이 같아서");
                else if (hits > 1)
                {
                    res.Notes.Add("'" + b[i].Name + "' 과 열 구성이 같은 것이 이후 쪽에 "
                        + hits + " 개 있어 어느 것과 견줄지 정할 수 없었습니다. "
                        + "파일 이름을 맞춰 주시면 그대로 짝지읍니다.");
                }
            }

            // 짝을 못 지은 채 양쪽에 남았으면 그렇다고 적습니다.
            int bl = Alive(b), al = Alive(a);
            if (bl > 0 && al > 0)
            {
                res.Notes.Add("이름이 다른 것이 이전 쪽에 " + bl + " 개, 이후 쪽에 " + al
                    + " 개 남았습니다. 열 구성이 같은 짝이 하나씩일 때만 이름이 달라도 "
                    + "견줍니다 — 파일 이름을 맞춰 주시면 그대로 짝지읍니다.");
            }
        }

        private static void Pair(DbDiffResult res, List<DbTable> b, int i,
                                 List<DbTable> a, int j, string why)
        {
            TableDiff td = CompareTable(b[i], a[j]);
            td.PairedName = a[j].Name;
            res.Notes.Add("이름이 다른 파일을 짝지었습니다: '" + b[i].Name + "' ↔ '"
                + a[j].Name + "' (" + why + ").");
            Took(res, td);
            b[i] = null; a[j] = null;
        }

        private static int Alive(List<DbTable> list)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] != null) n++;
            return n;
        }

        /// <summary>
        /// 표의 <b>열 구성</b>. 열 이름만 보고, 순서는 안 가립니다 —
        /// `.csv` 에는 타입이 없고, 열 순서가 바뀐 것도 같은 표입니다.
        /// </summary>
        private static string Signature(DbTable t)
        {
            if (t == null) return string.Empty;

            var names = new List<string>(t.Columns.Count);
            for (int i = 0; i < t.Columns.Count; i++)
                names.Add((t.Columns[i].Name ?? string.Empty).ToLowerInvariant());
            names.Sort(StringComparer.Ordinal);
            return string.Join(",", names.ToArray());
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

                if (changed.Count == 0)
                {
                    // 같은 줄도 담습니다 ("모든 IO 보기" 가 쓸 자리).
                    td.RowsSame++;
                    AddSame(td, new RowDiff { Key = key, Change = DbChange.Same, Before = br, After = ar });
                    continue;
                }

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

        /// <summary>
        /// 같은 줄을 담습니다. 상한을 넘으면 세기만 하고 <b>적어 둡니다</b> —
        /// 조용히 그치면 "모든 IO" 가 전부인 줄 알게 됩니다.
        /// </summary>
        private static void AddSame(TableDiff td, RowDiff d)
        {
            if (td.SameRows.Count >= MaxSameRowsPerTable)
            {
                if (!td.RowNote.Contains("같은 줄"))
                {
                    td.RowNote = (td.RowNote.Length > 0 ? td.RowNote + " " : "")
                               + "같은 줄은 " + MaxSameRowsPerTable.ToString("N0")
                               + " 개까지만 목록에 담았습니다 (상한). 그 뒤는 세기만 했습니다.";
                }
                return;
            }
            td.SameRows.Add(d);
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
        /// 칸 <b>이름</b>을 바꾸는 곳은 <see cref="DbNames"/> 입니다. 이 넷은
        /// 거기 두지 않았습니다 — 이름은 보여 주기만 하는 것이고, 이것은
        /// 견주는 값입니다.
        /// </summary>
        public const string KindColumn = "열";
        public const string KindRow = "행";
        public const string KindNote = "알림";

        public const string ChangeAdded = "생김";
        public const string ChangeGone = "없어짐";
        public const string ChangeDiff = "달라짐";
        public const string ChangeMoved = "자리";   // 값은 같고 열 자리만 바뀜

        /// <summary>
        /// 두 쪽이 <b>같은</b> 줄. "모든 IO 보기" 에서만 목록에 들어옵니다.
        /// 칠하지 않습니다 (Mark 가 빈 글자).
        /// </summary>
        public const string ChangeSame = "같음";

        /// <summary>
        /// <b>줄 색을 정하는 값.</b> 화면이 이 글자를 보고 줄을 칠합니다.
        ///
        ///   MarkValue  값이 달라짐  → 빨강
        ///   빈 글자    칠하지 않음 (열 줄, 생기거나 없어진 줄, 알림)
        ///
        /// 0.87 에서 노랑(v1 · v2 가 달라짐)을 없앴습니다 — 그 자리 자체를
        /// 뺐습니다. 색이 하나면 "칠했나 안 칠했나" 만 보면 됩니다.
        ///
        /// 생기거나 없어진 줄을 칠하지 않는 이유: 그건 "값이 달라진" 것이
        /// 아니고, 위 요약에서 <b>따로 세어 보여 줍니다</b>. 같은 것을 두
        /// 가지 방법으로 알려 주면 수가 안 맞아 보일 때 어느 쪽이 맞는지
        /// 알 수 없습니다.
        ///
        /// </summary>
        public const string MarkValue = "값";

        public string Mark { get; set; }

        /// <summary>열 · 행 · 알림. 위 Kind... 상수 중 하나입니다.</summary>
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

        /// <summary>열 이름.</summary>
        public string Column { get; set; }

        /// <summary>이전 값(행) 또는 이전 모양(열). 그 쪽에 없으면 빈 칸입니다.</summary>
        public string Before { get; set; }

        public string After { get; set; }

        /// <summary>짐작·상한·키 없음 같은 덧말.</summary>
        public string Note { get; set; }

        /// <summary>
        /// <b>얼마나 달라졌나</b> — 값이 달라진 줄(<see cref="MarkValue"/>)에서만
        /// 채워지고, 나머지는 빈 글자입니다 (1.08). 분석 2 목록이 이후 값
        /// 옆에 빨간 글씨로 붙입니다.
        ///
        ///   둘 다 숫자    "(+3)"  "(-0.25)"   이후 − 이전
        ///   그 밖         "(OPEN → CLOSE)"
        ///
        /// 숫자는 <c>decimal</c> 로 뺍니다. <c>double</c> 로 빼면 0.3 − 0.1 이
        /// 0.19999999999999998 로 적힙니다. 숫자로는 같은데 글자가 다른 경우
        /// ("1.0" 과 "1")는 "(+0)" 이 아니라 글자 꼴로 적습니다 — 0 이라고 적으면
        /// 왜 칠해졌는지 알 수 없습니다.
        ///
        /// 저장해 두지 않고 읽을 때 셉니다. 목록은 보이는 줄만 만들어서
        /// (가상화) 수만 줄이어도 실제로 세는 것은 화면에 보이는 몇십 줄입니다.
        /// </summary>
        public string Delta
        {
            get
            {
                if (Mark != MarkValue) return string.Empty;

                decimal b, a;
                if (Number(Before, out b) && Number(After, out a) && a != b)
                {
                    decimal d = a - b;
                    string n = (d < 0 ? -d : d).ToString("0.############################",
                                                        CultureInfo.InvariantCulture);
                    return "(" + (d < 0 ? "-" : "+") + n + ")";
                }
                return "(" + DbChangedIo.Pair(Before, After) + ")";
            }
        }

        private static bool Number(string s, out decimal v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return decimal.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        public DbDiffLine()
        {
            Table = string.Empty; Kind = string.Empty; Change = string.Empty;
            Where = string.Empty; Column = string.Empty; Io = string.Empty;
            Before = string.Empty; After = string.Empty; Note = string.Empty;
            Mark = string.Empty;
        }

        /// <summary>
        /// 줄 하나를 한 줄 글로. 칸이 좁아 잘린 값을 마우스로 짚어 보게 하는
        /// 용도입니다 — 값이 긴 열에서는 칸만으로는 읽을 수가 없습니다.
        /// </summary>
        public string Tip
        {
            get
            {
                // 표 이름은 안 적습니다 (0.87). 파일 하나를 넣어 견주는
                // 쓰임이라 표를 가를 일이 없습니다.
                var sb = new StringBuilder();
                sb.Append(Io.Length > 0 ? Io : Table);
                if (Kind.Length > 0) sb.Append("  [").Append(Kind).Append(']');
                if (Change.Length > 0) sb.Append(' ').Append(Change);
                // 앞에 붙는 이름은 목록 칸 이름을 그대로 씁니다 — 칸 이름을
                // 바꿨는데 설명 글만 옛 이름으로 남으면 안 됩니다.
                Label(sb, DbNames.Where, Where);
                Label(sb, DbNames.Column, Column);
                if (Before.Length > 0 || After.Length > 0)
                {
                    Label(sb, DbNames.Before, Before);
                    Label(sb, DbNames.After, After);
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
    /// 값이 달라진 IO 하나 — 대표 열의 <b>이전 → 이후</b> 와 더 달라진 열들.
    /// <see cref="DbDiffList.ChangedValues"/> 가 만듭니다.
    /// </summary>
    public sealed class DbChangedIo
    {
        public const string Arrow = " → ";

        /// <summary>값이 빈 글자일 때 적는 말. 안 적으면 "1 → " 가 되어 잘린 줄로 보입니다.</summary>
        public const string EmptyValue = "(빈 값)";

        public string Table { get; private set; }
        public string Io { get; private set; }

        /// <summary>대표 열. 목록에 먼저 나온 열입니다.</summary>
        public string Column { get; private set; }
        public string Before { get; private set; }
        public string After { get; private set; }

        /// <summary>같은 IO 에서 더 달라진 열들 (대표 열은 뺀 것).</summary>
        public List<DbDiffLine> Others { get; private set; }

        public DbChangedIo(DbDiffLine first)
        {
            Table = first.Table; Io = first.Io; Column = first.Column;
            Before = first.Before; After = first.After;
            Others = new List<DbDiffLine>();
        }

        /// <summary>"1 → 2". 대표 열만.</summary>
        public string Change { get { return Pair(Before, After); } }

        /// <summary>
        /// 더 달라진 열들을 "MAX: 5 → 6 · MIN: 0 → 1" 로. <paramref name="max"/>
        /// 개를 넘으면 "외 N칸" 을 붙입니다. 없으면 빈 글자.
        /// </summary>
        public string OthersText(int max)
        {
            if (Others.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(Column).Append(": ").Append(Change);
            int shown = Math.Min(Others.Count, Math.Max(0, max));
            for (int i = 0; i < shown; i++)
            {
                DbDiffLine o = Others[i];
                sb.Append("  ·  ").Append(o.Column).Append(": ").Append(Pair(o.Before, o.After));
            }
            int more = Others.Count - shown;
            if (more > 0) sb.Append("  외 ").Append(more.ToString("N0")).Append("칸");
            return sb.ToString();
        }

        public static string Pair(string before, string after)
        {
            return Show(before) + Arrow + Show(after);
        }

        private static string Show(string v)
        {
            return string.IsNullOrEmpty(v) ? EmptyValue : v;
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

            /// <summary>
            /// <b>두 쪽이 같은 줄도 목록에 넣을지</b> ("모든 IO 보기").
            ///
            /// 기본은 거짓입니다 — 차이를 보려고 여는 화면이고, 같은 줄이
            /// 훨씬 많아서 켜 두면 차이가 그 안에 묻힙니다. 화면에서 고르면
            /// 참이 됩니다.
            /// </summary>
            public bool IncludeSame;
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
                        + "CSV 로 내보내도 같은 상한이 걸립니다 — 견줄 자리를 "
                        + "좁혀 주세요 (폴더 대신 파일 하나).");
            }
        }

        // ---------------- 표 하나 ----------------

        private static void OneTable(TableDiff td, Sink s)
        {
            if (td == null) return;

            // 같은 표는 넘깁니다 — 단, "모든 IO 보기" 면 그 표의 IO 들도
            // 적어야 합니다 (차이가 없는 표의 IO 도 견준 IO 입니다).
            if (td.Change == DbChange.Same && !s.Opt.IncludeSame) return;
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

            // 같은 줄은 <b>차이 뒤에</b> 적습니다. 섞어 놓으면 차이를 눈으로
            // 찾아야 합니다 (목록을 열쇠 순으로 정렬하지 않는 까닭입니다).
            if (!s.Opt.IncludeSame) return;
            for (int i = 0; i < td.SameRows.Count; i++)
            {
                if (!SameRowLine(td, td.SameRows[i], s)) { Cut(td, s); return; }
            }
        }

        /// <summary>
        /// 두 쪽이 같은 줄 하나. <b>행 하나에 한 줄</b>입니다 (열마다 펼치지
        /// 않습니다) — 같은 값을 열 수만큼 적으면 목록이 그것만으로 찹니다.
        ///
        /// 값 열이 하나뿐인 표(IO 표가 그렇습니다)는 그 값을 적습니다.
        /// 여러 열이면 행 전체를 한 칸에 적습니다.
        /// </summary>
        private static bool SameRowLine(TableDiff td, RowDiff rd, Sink s)
        {
            string key = KeyText(td, rd);
            string io = IoText(td, rd);

            var ln = new DbDiffLine
            {
                Table = td.Name, Kind = DbDiffLine.KindRow, Change = DbDiffLine.ChangeSame,
                Where = key, Io = io,
            };

            int one = SingleValueColumn(td.Before);
            if (one >= 0 && rd.Before != null)
            {
                ln.Column = td.Before.Columns[one].Name;
                ln.Before = DbRow.Show(rd.Before.Get(one));
                ln.After = ln.Before;      // 같은 줄이라 두 쪽이 같은 값입니다
            }
            else
            {
                ln.Column = "(행 전체)";
                ln.Before = RowText(td.Before, rd.Before);
                ln.After = ln.Before;
            }
            return s.Add(ln);
        }

        /// <summary>
        /// 값 열이 하나뿐이면 그 자리, 아니면 -1. (열쇠 하나 + 값 하나로 된
        /// IO 표를 가립니다 — .csv 로 읽은 표가 그 모양입니다.)
        /// </summary>
        private static int SingleValueColumn(DbTable t)
        {
            if (t == null) return -1;

            int found = -1;
            for (int i = 0; i < t.Columns.Count; i++)
            {
                if (t.Columns[i].IsKey) continue;
                if (found >= 0) return -1;      // 값 열이 둘 이상
                found = i;
            }
            return found;
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

            // 0.87: 표 단위 줄을 <b>알림</b>으로 바꿨습니다. 목록에서 "표"
            // 갈래를 없앴지만 이 사실까지 없애면 안 됩니다 — 파일 한쪽이
            // 통째로 없는 것은 가장 큰 차이입니다. 알림은 목록 위에 적힙니다.
            var head = new DbDiffLine { Table = td.Name, Kind = DbDiffLine.KindNote };
            head.Note = "'" + td.Name + "' 은 " + (before ? "이전 쪽에만" : "이후 쪽에만")
                      + " 있습니다 — 열 " + cols + " 개 · 읽은 행 "
                      + rows.ToString("N0") + " 개."
                      + " 행은 하나씩 적지 않습니다 (전부 한쪽에만 있는 것이라 같은 말입니다).";
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
        /// <b>줄에 값을 담는 자리가 여기입니다.</b> 열을 더 보여 주고 싶으면
        /// <c>td.Before</c> / <c>td.After</c> 의 <c>IndexOf("열이름")</c> 으로
        /// 자리를 찾고 <c>rd.Before.Get(i)</c> 로 읽어 넣으면 됩니다.
        ///
        /// <c>ln.Io</c> 에는 행 열쇠가 들어갑니다. <c>.csv</c> 는 3 번 열이
        /// 열쇠라서(<see cref="DbNames.CsvKeyColumn"/>) 거기 적힌 IO 이름이
        /// 그대로 들어옵니다.
        /// </summary>
        private static bool OneRow(TableDiff td, RowDiff rd, Sink s)
        {
            string key = KeyText(td, rd);
            string io = IoText(td, rd);

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
                        Where = key, Column = name, Io = io,
                        Before = DbRow.Show(rd.Before.Get(ci)),
                        After = DbRow.Show(ai < 0 || rd.After == null ? null : rd.After.Get(ai)),
                        Mark = DbDiffLine.MarkValue,
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
                    Table = td.Name, Kind = DbDiffLine.KindRow, Change = ch, Where = key, Io = io,
                    Column = "(행 전체)",
                };
                string all = RowText(shape, row);
                if (before) one.Before = all; else one.After = all;
                return s.Add(one);
            }

            for (int c = 0; c < shape.Columns.Count; c++)
            {
                // 열쇠 열은 줄로 적지 않습니다 (0.87). 그 값은 이미 IO명 칸에
                // 있습니다 — 적으면 "TEMP_MAX → TEMP_MAX" 같은 줄이 하나 더
                // 생기고, 없어진 행 하나가 줄 둘로 보입니다.
                if (shape.Columns[c].IsKey && shape.KeyIndexes().Length == 1) continue;

                var ln = new DbDiffLine
                {
                    Table = td.Name, Kind = DbDiffLine.KindRow, Change = ch, Where = key, Io = io,
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

        /// <summary>
        /// 그 행의 <b>IO 이름</b>. 목록 첫 칸에 적힙니다.
        ///
        /// 열쇠가 <b>한 열</b>이면 그 값만 적습니다 — `.csv` 는 3 번 열이
        /// 열쇠고 거기 IO 이름이 들어 있어서, <c>io=TEMP_MAX</c> 가 아니라
        /// <c>TEMP_MAX</c> 가 나와야 합니다.
        ///
        /// 열쇠가 여러 열이면 <see cref="KeyText"/> 와 같습니다 — 어느 값이
        /// 어느 열인지 적어 주지 않으면 읽을 수 없습니다.
        /// </summary>
        public static string IoText(TableDiff td, RowDiff rd)
        {
            if (td == null || rd == null) return string.Empty;

            DbTable shape = td.Before ?? td.After;
            DbRow row = rd.Before ?? rd.After;
            if (shape == null || row == null || !shape.HasKey) return KeyText(td, rd);

            int[] keys = shape.KeyIndexes();
            if (keys.Length != 1) return KeyText(td, rd);

            return DbRow.Show(row.Get(keys[0]));
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

        // ---------------- 개수 세기 ----------------

        /// <summary>
        /// 화면 위에 적는 세 숫자. <b>IO 를 셉니다 (줄이 아닙니다).</b>
        ///
        /// 한 IO 가 여러 열에서 달라지면 줄은 여러 개지만 IO 는 하나입니다.
        /// 줄을 세면 "차이 발생 12 개" 인데 IO 는 3 개인 일이 생겨서, 보는
        /// 사람이 수를 믿을 수 없게 됩니다.
        ///
        /// <b>Core 에 둔 이유</b>: 화면이 같은 셈을 한 번 더 짜면 한쪽만
        /// 고쳐지는 날이 옵니다. 여기 있으면 시험이 지켜 줍니다.
        /// </summary>
        public struct Tally
        {
            /// <summary>값이 달라진 IO.</summary>
            public int Changed;

            /// <summary>이전에만 있던 IO.</summary>
            public int Gone;

            /// <summary>이후에 새로 생긴 IO.</summary>
            public int Added;
        }

        public static Tally Count(List<DbDiffLine> lines)
        {
            var changed = new HashSet<string>(StringComparer.Ordinal);
            var gone = new HashSet<string>(StringComparer.Ordinal);
            var added = new HashSet<string>(StringComparer.Ordinal);

            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    DbDiffLine ln = lines[i];
                    if (ln == null || ln.Kind == DbDiffLine.KindNote) continue;

                    // 같은 이름이 표마다 있을 수 있어 표 이름을 함께 셉니다.
                    string who = ln.Table + "\u0001" + ln.Io;

                    if (ln.Mark.Length > 0) { changed.Add(who); continue; }
                    if (ln.Change == DbDiffLine.ChangeGone) { gone.Add(who); continue; }
                    if (ln.Change == DbDiffLine.ChangeAdded) { added.Add(who); continue; }
                }
            }

            var t = new Tally();
            t.Changed = changed.Count;
            t.Gone = gone.Count;
            t.Added = added.Count;
            return t;
        }

        /// <summary>
        /// 화면 위에 적는 한 줄. <b>글자도 Core 에 둡니다</b> — 화면에서
        /// 만들면 CSV 나 다른 화면이 같은 글을 또 짜게 됩니다.
        /// </summary>
        /// <summary>
        /// <b>값이 달라진 IO 이름들</b>. 차례는 목록에 나온 차례입니다.
        ///
        /// WARNING 일 때 "어떤 항목이 달라졌나" 를 바로 적어 주려고
        /// 냅니다 — 개수만 보면 결국 분석 2 화면을 열어 목록을 뒤져야
        /// 합니다.
        ///
        /// <paramref name="max"/> 개까지만 모읍니다. 수백 개를 한 줄에
        /// 늘어놓으면 칸을 넘기고, 넘긴 글은 아무도 못 읽습니다. 몇 개를
        /// 더 안 적었는지는 <b>부르는 쪽이 개수(Tally)로 알 수 있습니다</b>.
        ///
        /// 이름만 뽑는 길을 따로 두지 않고 <see cref="ChangedValues"/> 에서
        /// 꺼냅니다 — 두 군데서 고르면 메인 칸의 이름과 값이 어긋납니다.
        /// </summary>
        public static List<string> ChangedIos(List<DbDiffLine> lines, int max)
        {
            var names = new List<string>();
            List<DbChangedIo> ios = ChangedValues(lines, max);
            for (int i = 0; i < ios.Count; i++) names.Add(ios[i].Io);
            return names;
        }

        /// <summary>
        /// <b>값이 달라진 IO 와 그 값</b> (1.04). 메인 화면 분석 2 칸의
        /// 요약이 "TEMP_MAX   1 → 2" 처럼 적는 데 씁니다.
        ///
        /// IO 하나에서 열이 여럿 달라졌을 수 있습니다. 목록에 <b>먼저
        /// 나온 열</b>을 대표로 두고 나머지는 <see cref="DbChangedIo.Others"/>
        /// 에 모읍니다. 대표만 적고 나머지를 버리면 "1 → 2 만 바뀌었다" 로
        /// 읽힙니다.
        ///
        /// 고르는 잣대는 개수를 세는 <see cref="Count"/> 와 같습니다 (표 +
        /// IO 이름, 칠한 줄만). 다르면 "차이 발생 3개" 옆에 넷이 적힙니다.
        /// </summary>
        public static List<DbChangedIo> ChangedValues(List<DbDiffLine> lines, int max)
        {
            var ios = new List<DbChangedIo>();
            if (lines == null || max <= 0) return ios;

            var at = new Dictionary<string, DbChangedIo>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine ln = lines[i];
                if (ln == null || ln.Kind == DbDiffLine.KindNote) continue;
                if (ln.Mark.Length == 0) continue;

                string who = ln.Table + "\u0001" + ln.Io;
                DbChangedIo io;
                if (at.TryGetValue(who, out io))
                {
                    // 이미 적은 IO 의 다른 열. 다 찼어도 이것은 모읍니다 —
                    // 적은 IO 의 값은 빠짐없이 있어야 합니다.
                    io.Others.Add(ln);
                    continue;
                }
                if (ios.Count >= max) continue;

                io = new DbChangedIo(ln);
                at.Add(who, io);
                ios.Add(io);
            }
            return ios;
        }

        public static string Headline(List<DbDiffLine> lines)
        {
            Tally t = Count(lines);
            return "차이 발생 : " + t.Changed.ToString("N0") + "개"
                 + "      없어진 IO : " + t.Gone.ToString("N0") + "개"
                 + "      새로 생긴 IO : " + t.Added.ToString("N0") + "개";
        }

        /// <summary>
        /// 값이 달라진 줄만 (빨강 · 노랑으로 칠하는 줄). 화면의
        /// "차이가 발생한 IO만 보기" 가 이것을 씁니다.
        ///
        /// 걸러 내는 것은 <b>생기거나 없어진 줄과 표·열 줄</b>입니다. 그것들도
        /// 차이는 차이라서, 걸러 두면 "이게 전부" 로 읽힐 수 있습니다 —
        /// 그래서 화면은 걸러 둔 동안에도 위의 세 숫자를 그대로 보여 줍니다.
        /// </summary>
        public static List<DbDiffLine> OnlyChanged(List<DbDiffLine> lines)
        {
            var kept = new List<DbDiffLine>();
            if (lines == null) return kept;

            for (int i = 0; i < lines.Count; i++)
                if (lines[i] != null && lines[i].Mark.Length > 0) kept.Add(lines[i]);
            return kept;
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
            // 머리글 이름은 DbNames 한 곳에 있습니다.
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
                Cell(sb, ln.Io); sb.Append(',');
                Cell(sb, ln.Kind); sb.Append(',');
                Cell(sb, ln.Change); sb.Append(',');
                Cell(sb, ln.Where); sb.Append(',');
                Cell(sb, ln.Column); sb.Append(',');
                Cell(sb, ln.Before); sb.Append(',');
                Cell(sb, ln.After); sb.Append(',');
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
