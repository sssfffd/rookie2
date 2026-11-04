# 182 — (질문) 분석 2 의 .csv 읽기 · 파싱은 어디

## 요청 (요청 209)

> 이게 분석 2 에 .csv 읽고 파싱하는 거 어디 있음?

## 답 (코드는 안 바꿈 — 버전 그대로)

```
DbView (파일 · 폴더 넣기)
  → DbFolderReader.Read            Analysis2/DbRead.cs:178
      → TakeFile (확장자로 가름)     Analysis2/DbRead.cs:438
          .sql → SqlUserReader.Read (사용자 것) → 없으면 SqlDumpReader.Read
          .csv → CsvTableReader.Read   Analysis2/DbRead.cs:489 → :1287
                   → Head (첫 줄 = 열 이름, 열쇠 · 값 열 고르기)  :1356
                   → CsvReader (줄 나누기 · 구분자 · 따옴표)       Common/CsvReader.cs
```

| 무엇 | 자리 |
|---|---|
| 확장자 가르기 | `DbRead.cs:438` `DbFolderReader.TakeFile` (.csv 는 `:489`) |
| 표 하나로 읽기 (파일 이름 = 표 이름, 첫 줄 = 열 이름, 행 수 상한) | `DbRead.cs:1285` `CsvTableReader.Read` |
| 열쇠 · 값 열 고르기 | `DbRead.cs:1356` `CsvTableReader.Head`, 열 번호는 `Analysis2/DbNames.cs:52` `CsvKeyColumn = 3` · `:53` `CsvValueColumn = 4` |
| 글자 → 칸 (구분자 알아내기 · 따옴표 · 줄) | `Common/CsvReader.cs` — 생성자 `:40` (`rawText: true` 로 적힌 글자 그대로), `NextRow` `:96`, `SplitLine` `:141`, `SniffDelimiter` `:54` |

`Common/CsvReader.cs` 는 분석 1 의 로그 읽기도 같이 씁니다 (분석 1 은 `rawText: false`). 그래서 분석 2 만 바꾸려면
`CsvTableReader` 쪽을 고치는 것이 안전합니다.
