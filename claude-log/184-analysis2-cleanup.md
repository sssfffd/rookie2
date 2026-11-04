# 184 — 1.90 분석 2 에서 쓰지 않는 기능 · 파일 빼기

## 요청 (요청 211)

> 분석 2 안에 지금 필요 없는 내용들이나 파일들 삭제해 주셈. 잘 모르겠으면 나한테 기능 설명하고 확인받으면 됨

## 한 것

먼저 코드 전체 (사용자 폴더 `LlmExport` · `Analysis3` · 시험 포함) 에서 아무도 안 부르는 것을 찾았습니다 — **셋뿐**
(`DbVm.BeforeChosen` · `AfterChosen`, `DbDiffResult.AnyChange`). 나머지는 다 쓰이는 기능이라 기능을 설명하고 물었고,
사용자가 **넷 다** 빼기로 골랐습니다.

| 뺀 기능 | 지운 것 |
|---|---|
| 바깥 프로그램 실행 (mysqldump · ibd2sdi) | `DbReadMysql.cs` 의 `DbTools`, 설정 창 [DB 도구] 탭, `AppSettings` 의 `MysqlDumpPath` · `DbDumpBefore` · `DbDumpAfter` · `Ibd2SdiPath` (+ 저장 · 읽기), `SettingsVm` · 창 코드 · 글, `DbVm` 의 통로, `DbView.Load` · `TakeIbd` |
| `.frm` / `.ibd` 읽기 | `DbReadMysql.cs` 의 `FrmReader` (파일째 지움), `DbFolderReader` 의 `.frm` 차례 · 판 번호 · `.ibd` 안내 |
| [SQL 문 출력] | `DbSql.cs` (파일째), 단추 · 처리기 · 글, `DbVm.ScriptText` · `BuildScript`, 그 창만 쓰던 `Common/TextViewWindow.xaml(.cs)` + `CommonTexts` 글 |
| 폴더 통째 읽기 | `DbFolderReader` 의 `Files` · `IsLink` · `Prefix` · `ReadTables` · `Seen` · `Notes` · 상한 옵션, [폴더] 단추 · 처리기 · 글 |
| (안 쓰던 것) | `BeforeChosen` · `AfterChosen` · `AnyChange` |

남은 읽기 (`DbFolderReader.Read` — 이름은 부르는 쪽이 많아 그대로):

```
파일 하나 → .sql  → SqlUserReader.Read (사용자) → false 면 SqlDumpReader
          → .frm/.ibd → "MySQL 내부 파일은 읽지 않습니다"
          → 그 밖 → CsvTableReader (.csv)
폴더 → "폴더는 읽지 않습니다 — 파일 하나를 넣어 주세요"
```

새로 더한 것 하나: `.sql` 에서 표가 하나도 안 나오고 아무 말도 없으면 "표를 찾지 못했습니다" 를 적음
(조용히 "차이 없음" 으로 보이지 않게 — 시험이 잡아낸 빈자리).

그대로 둔 것: `TableDiff.PairedName` (사용자 `LlmExport/Analysis2Digest.cs` 가 씀 — 주석만 고침), `DbModel` 의
"기본값을 모름" 구분 (.sql 끼리도 쓰임), 판정 (`DbVerdict`) · 목록 · CSV 저장.

줄 수: 분석 2 폴더 6287 → 4281 줄, 전체 3193 줄 지움 (시험 527 줄 포함). 검사 항목은 1162 → 1072 (frm · 폴더 ·
SQL 글 · 바깥 프로그램 시험을 빼고, 파일 하나 · 경로 글 시험은 새 동작으로 바꿈).

## 확인

- 시험 1072 통과 (1162 − 뺀 시험 + 바꾼 시험), check 스크립트 (csproj 목록 · XAML · 처리기 · 이름) 통과.
- 실제 csc 빌드 통과 — **사용자 `LlmExport` 폴더를 넣고도** 통과 (없는 `OverallLlmAnalyzer` 는 늘 쓰던 stub).
- **Windows 에서 못 본 것**: 분석 2 화면 단추 줄 · 설정 창 탭 (DB 도구 탭이 빠진 모양), 폴더를 끌어다 놓았을 때 글.
