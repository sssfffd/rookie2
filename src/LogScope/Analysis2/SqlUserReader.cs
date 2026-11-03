using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ==========================================================
//  SqlUserReader — 분석 2 의 .sql 읽기를 <b>사용자가 직접</b> 만드는 자리 (1.82)
//
//  .sql 파일 하나를 읽을 때마다 여기 Read 가 <b>먼저</b> 불립니다 (DbRead.cs 의 TakeFile).
//    true  → 여기서 읽은 것을 씀 (내장 SqlDumpReader 는 안 씀)
//    false → 내장 SqlDumpReader (mysqldump 꼴: CREATE TABLE / INSERT INTO … VALUES) 로 읽음
//  지금은 false 만 돌려줘서 예전과 똑같이 돕니다.
//
//  이 파일은 사용자 몫입니다 — 이후 버전에서 Claude 는 이 파일을 고치지 않습니다.
//  (부르는 자리 · 넘기는 값이 바뀌면 그때 알립니다.)
//
//  규칙 (프로그램 전체와 같음)
//   - SQL 을 <b>실행하지 않습니다</b> — 글자를 읽어 표로 만들 뿐. DB 에 접속하지 않습니다.
//   - 바깥 라이브러리 없이 (.NET 4.6.1 · C# 7.3 기본만).
//   - 예외를 던져도 프로그램은 안 죽습니다 — 그 파일만 "읽지 못했습니다: …" 로 적히고 넘어갑니다.
// ==========================================================

namespace LogScope.Analysis2
{
    public static class SqlUserReader
    {
        /// <summary>
        /// .sql 파일 하나를 읽어 <paramref name="into"/>.Tables 에 표를 더합니다.
        /// </summary>
        /// <param name="path">.sql 파일 경로 (덤프 명령으로 만든 것 포함).</param>
        /// <param name="into">
        /// 담을 곳 — 비어 있는 새 DbSnapshot. 표 이름 앞의 폴더 이름 (db1. 등) 은 부르는 쪽이 붙입니다.
        /// 화면 위쪽에 띄울 말은 <c>into.Notes.Add("…")</c>.
        /// </param>
        /// <param name="maxRows">표 하나에서 읽을 행 수 상한 (지금 200 만). 넘으면 거기까지만 읽고 table.Note 에 적어 주세요.</param>
        /// <returns>여기서 읽었으면 true. false 면 내장 읽기 (SqlDumpReader) 로 넘어갑니다.</returns>
        public static bool Read(string path, DbSnapshot into, int maxRows)
        {
            // TODO (사용자): 여기에 .sql 파싱을 만드세요. 다 되면 아래 줄을 지우고 true 를 돌려주면 됩니다.
            return false;

            // ---- 표 하나를 만드는 모양 (예시) ----------------------------------------
            //
            // var t = new DbTable();
            // t.Name = "recipe";                       // 표 이름 — 이전 · 이후에서 같은 이름끼리 견줍니다
            // t.Note = Path.GetFileName(path);         // 어디서 읽었는지 (화면에 보임)
            //
            // // 열: 차례대로. IsKey 인 열 (기본 키) 로 행끼리 짝을 짓고, 없으면 열 전체로 견줍니다.
            // t.Columns.Add(new DbColumn { Name = "id",    Type = "int",         IsKey = true, Ordinal = 0 });
            // t.Columns.Add(new DbColumn { Name = "name",  Type = "varchar(64)",               Ordinal = 1 });
            // t.Columns.Add(new DbColumn { Name = "value", Type = "double",                    Ordinal = 2 });
            //
            // // 행: 값은 <b>적힌 글자 그대로</b> (숫자로 바꾸지 않음 — "1.10" 과 "1.1" 은 다른 값). NULL 은 null.
            // var row = new DbRow(t.Columns.Count);
            // row.Values[0] = "1";
            // row.Values[1] = "TEMP_SET";
            // row.Values[2] = null;                    // NULL
            // t.Rows.Add(row);
            //
            // t.HasRows = true;                        // 행까지 읽었음 (false 면 "모양만 아는 표")
            // into.Tables.Add(t);
            // return true;
            //
            // ---- 파일 읽기 참고 ----------------------------------------------------------
            // 덤프는 기가바이트일 수 있어 한 번에 다 올리지 말고 흘려 읽으세요 (StreamReader.ReadLine 등).
            // 글자 꼴은 UTF-8 이 보통이지만 CP949 일 수도 있습니다 — 내장 읽기는 BOM 이 없으면 둘 다 해 봅니다
            // (DbRead.cs 의 SqlDumpReader.Read 참고).
        }
    }
}
