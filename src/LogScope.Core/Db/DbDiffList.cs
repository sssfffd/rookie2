using System;
using System.Collections.Generic;
using System.Text;

namespace LogScope.Core.Db
{
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

        /// <summary>표 · 열 · 행 · 알림.</summary>
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
                if (Io.Length > 0) sb.Append("\nIO   ").Append(Io);
                if (Where.Length > 0) sb.Append("\n행   ").Append(Where);
                if (Column.Length > 0) sb.Append("\n열   ").Append(Column);
                if (Before.Length > 0 || After.Length > 0)
                    sb.Append("\n이전  ").Append(Before).Append("\n이후  ").Append(After);
                if (V1.Length > 0 || V2.Length > 0)
                    sb.Append("\nv1   ").Append(V1).Append("\nv2   ").Append(V2);
                if (Note.Length > 0) sb.Append('\n').Append(Note);
                return sb.ToString();
            }
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
                Lines.Add(new DbDiffLine { Table = table, Kind = "알림", Note = note });
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
                Table = td.Name, Kind = "알림", Note = td.RowNote,
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
            string ch = before ? "없어짐" : "생김";

            int cols = only == null ? 0 : only.Columns.Count;
            int rows = only == null ? 0 : only.Rows.Count;

            var head = new DbDiffLine { Table = td.Name, Kind = "표", Change = ch, Io = td.Name };
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
                    Table = td.Name, Kind = "열", Change = ch, Column = c.Name, Io = c.Name,
                    Note = c.IsKey ? "기본 키" : string.Empty,
                };
                if (before) ln.Before = c.Shape(); else ln.After = c.Shape();
                if (!s.Add(ln)) { Cut(td, s); return; }
            }
        }

        private static DbDiffLine ColumnLine(string table, ColumnDiff cd)
        {
            var ln = new DbDiffLine { Table = table, Kind = "열", Column = cd.Name, Io = cd.Name };
            ln.Change = cd.Change == DbChange.OnlyBefore ? "없어짐"
                      : cd.Change == DbChange.OnlyAfter ? "생김"
                      : cd.MovedOnly ? "자리" : "달라짐";
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
                        Table = td.Name, Kind = "행", Change = "달라짐",
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
            string ch = before ? "없어짐" : "생김";

            if (row == null || shape == null || !s.Opt.ExpandWholeRows)
            {
                var one = new DbDiffLine
                {
                    Table = td.Name, Kind = "행", Change = ch, Where = key, Io = key,
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
                    Table = td.Name, Kind = "행", Change = ch, Where = key, Io = key,
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
            sb.Append("표,IO명,갈래,구분,행,열,이전 value,이후 value,v1,v2,설명\r\n");
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
