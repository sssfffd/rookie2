using System;
using System.Collections.Generic;
using System.IO;
using LogScope.Core.Io;

namespace LogScope.Core.Db
{
    /// <summary>
    /// 표 하나가 담긴 <c>.csv</c> 를 읽습니다. <b>파일 이름이 표 이름</b>이고
    /// 첫 줄이 열 이름입니다.
    ///
    /// 로그를 읽는 <see cref="CsvReader"/> 를 그대로 씁니다 — 구분자와 인코딩
    /// 판별, 따옴표 안의 줄바꿈 처리가 이미 거기 있습니다. 같은 일을 두 번
    /// 만들면 한쪽만 고치게 됩니다.
    ///
    /// <b>키가 없습니다.</b> CSV 에는 기본 키라는 것이 없으니, 행을 짝지을 때는
    /// "모든 열이 같은 행끼리" 가 됩니다 (DbTable.RowKey 참고). 키로 쓸 열을
    /// 정하고 싶으면 <c>.sql</c> 덤프를 쓰시는 편이 맞습니다.
    /// </summary>
    public static class CsvTableReader
    {
        public static DbTable Read(string path, int maxRows)
        {
            if (maxRows <= 0) maxRows = SqlDumpReader.MaxRowsPerTable;

            var t = new DbTable();
            t.Name = Path.GetFileNameWithoutExtension(path);
            t.Note = Path.GetFileName(path);

            using (var src = new CsvReader(path))
            {
                Cell[] cells; int count;

                if (!src.NextRow(out cells, out count))
                {
                    t.Note += " / 빈 파일입니다";
                    return t;
                }

                for (int i = 0; i < count; i++)
                {
                    var c = new DbColumn();
                    string name = Text(cells[i]);
                    c.Name = string.IsNullOrEmpty(name) ? "열" + (i + 1) : name;
                    c.Ordinal = i;
                    t.Columns.Add(c);
                }

                while (src.NextRow(out cells, out count))
                {
                    if (t.Rows.Count >= maxRows)
                    {
                        t.Note += " / 행 " + maxRows.ToString("N0") + " 개까지만 읽었습니다 (상한)";
                        break;
                    }

                    // 줄이 머리보다 길면 열을 늘립니다. 잘라 버리면 값이 조용히 사라집니다.
                    while (t.Columns.Count < count)
                    {
                        var c = new DbColumn();
                        c.Name = "열" + (t.Columns.Count + 1);
                        c.Ordinal = t.Columns.Count;
                        t.Columns.Add(c);
                    }

                    var row = new DbRow(t.Columns.Count);
                    for (int i = 0; i < count && i < row.Values.Length; i++)
                    {
                        // 빈 칸은 CSV 에서 "값 없음" 과 "빈 글자" 를 가릴 수 없습니다.
                        // 빈 글자로 둡니다 — NULL 로 바꾸면 없는 차이가 생깁니다.
                        row.Values[i] = Text(cells[i]);
                    }
                    t.Rows.Add(row);
                }
                t.HasRows = true;
            }
            return t;
        }

        private static string Text(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Empty: return string.Empty;
                case CellKind.Text: return c.Text ?? string.Empty;
                default: return c.Display();
            }
        }
    }
}
