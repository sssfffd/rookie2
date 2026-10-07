using System;
using System.Collections.Generic;
using System.Text;

namespace LogScope.Db
{
    /// <summary>
    /// 열 하나의 정의. <b>값이 아니라 모양</b>입니다.
    ///
    /// <see cref="Type"/> 은 적힌 그대로 둡니다 (<c>decimal(6,2)</c>,
    /// <c>varchar(64)</c>). 우리가 타입 체계를 따로 만들면 "같은 타입인데 다르다"
    /// 는 거짓 차이가 생깁니다. 견줄 때만 대소문자와 공백을 고릅니다.
    /// </summary>
    public sealed class DbColumn
    {
        public string Name = string.Empty;
        public string Type = string.Empty;
        public bool Nullable = true;
        public string Default;          // null = 적혀 있지 않음
        public bool IsKey;              // 기본 키의 일부
        public int Ordinal;             // 0 부터. 열 순서가 바뀐 것도 차이입니다.

        /// <summary>
        /// 기본값을 <b>아는지</b>. 거짓이면 "기본값이 없다" 가 아니라
        /// <b>"모른다"</b> 입니다 — .frm 에서 모양만 읽은 열이 그렇습니다.
        ///
        /// 이 둘을 섞으면 안 됩니다. 모르는 것을 "없음" 으로 적으면, 덤프에서
        /// 읽은 쪽(기본값 '0')과 견줄 때 <b>없는 차이가 생깁니다</b> —
        /// "기본값 없음 → '0'" 이라고 적고 그걸 맞추는 ALTER 까지 만들어 줍니다.
        /// </summary>
        public bool DefaultKnown = true;

        /// <summary>견주기 위한 모양. 대소문자와 군더더기 공백만 고릅니다.</summary>
        public string Shape()
        {
            var sb = new StringBuilder();
            sb.Append(Norm(Type));
            sb.Append(Nullable ? " NULL" : " NOT NULL");
            // 모르는 기본값은 아예 적지 않습니다. 적으면 "모름" 이 하나의
            // 값처럼 되어, 아는 쪽과 견줄 때 차이로 보입니다.
            if (DefaultKnown && Default != null) sb.Append(" DEFAULT ").Append(Default);
            return sb.ToString();
        }

        public static string Norm(string type)
        {
            if (string.IsNullOrEmpty(type)) return string.Empty;
            var sb = new StringBuilder(type.Length);
            bool space = false;
            for (int i = 0; i < type.Length; i++)
            {
                char c = type[i];
                if (c == ' ' || c == '\t') { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 행 하나. 값은 <b>글자 그대로</b> 들고 있습니다.
    ///
    /// 숫자로 바꿔 두면 <c>1.10</c> 과 <c>1.1</c> 이 같아져 "값이 안 바뀌었다" 가
    /// 되는데, DB 에서 그 둘은 다르게 적힌 것입니다. 적힌 대로 보여 주는 쪽이
    /// "무엇이 달라졌나" 에 맞습니다. NULL 은 null 로 둡니다 — 빈 글자와 다릅니다.
    /// </summary>
    public sealed class DbRow
    {
        public string[] Values;

        public DbRow(int n) { Values = new string[n]; }

        public string Get(int i)
        {
            return i >= 0 && i < Values.Length ? Values[i] : null;
        }

        /// <summary>화면과 SQL 에 쓰는 글자. NULL 은 그대로 "NULL".</summary>
        public static string Show(string v)
        {
            return v == null ? "NULL" : v;
        }
    }

    public sealed class DbTable
    {
        public string Name = string.Empty;
        public List<DbColumn> Columns = new List<DbColumn>();
        public List<DbRow> Rows = new List<DbRow>();

        /// <summary>
        /// 행을 읽었는지. 거짓이면 <b>모양만</b> 아는 표입니다 (.ibd 에서 정의만
        /// 꺼내 온 경우). "행 차이 없음" 과 "행을 못 읽음" 은 다릅니다.
        /// </summary>
        public bool HasRows;

        /// <summary>어디서 읽었는지, 못 읽은 것이 있으면 그 이유.</summary>
        public string Note = string.Empty;

        public int IndexOf(string column)
        {
            for (int i = 0; i < Columns.Count; i++)
                if (Same(Columns[i].Name, column)) return i;
            return -1;
        }

        public DbColumn Find(string column)
        {
            int i = IndexOf(column);
            return i < 0 ? null : Columns[i];
        }

        /// <summary>기본 키 열의 자리들. 없으면 빈 배열.</summary>
        public int[] KeyIndexes()
        {
            var list = new List<int>();
            for (int i = 0; i < Columns.Count; i++) if (Columns[i].IsKey) list.Add(i);
            return list.ToArray();
        }

        public bool HasKey { get { return KeyIndexes().Length > 0; } }

        /// <summary>
        /// 행을 짝지을 열쇠. 기본 키가 있으면 그 값들, 없으면 <b>모든 열</b>입니다.
        ///
        /// 키가 없는 표는 "어느 행이 어느 행이 되었나" 를 알 방법이 없습니다.
        /// 그때는 모든 열이 같은 행끼리만 같은 행으로 봅니다 — 값이 하나라도
        /// 바뀌면 "지워지고 새로 생겼다" 로 보이지만, 그게 아는 것의 전부입니다.
        /// 짐작으로 짝지어 "이 행의 이 값이 바뀌었다" 고 적으면 거짓말이 됩니다.
        /// </summary>
        public string RowKey(DbRow row, int[] keys)
        {
            var sb = new StringBuilder();
            if (keys != null && keys.Length > 0)
            {
                for (int k = 0; k < keys.Length; k++)
                {
                    if (k > 0) sb.Append('\u001f');
                    sb.Append(row.Get(keys[k]) ?? "\u0000NULL");
                }
                return sb.ToString();
            }

            for (int i = 0; i < Columns.Count; i++)
            {
                if (i > 0) sb.Append('\u001f');
                sb.Append(row.Get(i) ?? "\u0000NULL");
            }
            return sb.ToString();
        }

        /// <summary>DB 의 이름 견주기는 대소문자를 가리지 않습니다 (윈도우 기본).</summary>
        public static bool Same(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty,
                                 StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// DB 한 벌. 폴더 하나(또는 덤프 파일 하나)에서 읽은 표들입니다.
    /// </summary>
    public sealed class DbSnapshot
    {
        public string Source = string.Empty;         // 폴더나 파일 경로
        public List<DbTable> Tables = new List<DbTable>();

        /// <summary>읽다가 생긴 일들. 못 읽은 파일, 자른 행 수 같은 것.</summary>
        public List<string> Notes = new List<string>();

        public DbTable Find(string name)
        {
            for (int i = 0; i < Tables.Count; i++)
                if (DbTable.Same(Tables[i].Name, name)) return Tables[i];
            return null;
        }

        public int RowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Tables.Count; i++) n += Tables[i].Rows.Count;
                return n;
            }
        }

        public void Sort()
        {
            Tables.Sort(delegate (DbTable a, DbTable b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
