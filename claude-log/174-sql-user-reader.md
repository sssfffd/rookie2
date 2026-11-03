# 174 — 분석 2 .sql 읽기를 직접 만들 자리 (1.82)

> 아 그리고 분석 2에 지금 csv파일로 집어넣는데 .sql도 집어 넣게 만들꺼임
> 근데 sql 파싱은 내가 직접 구현할테니까 파일 혹은 함수정도만 준비해주고 위치알려주셈

## 자리

**`src/LogScope/Analysis2/SqlUserReader.cs`** — `SqlUserReader.Read(string path, DbSnapshot into, int maxRows)`

```
 .sql 하나 읽을 때 (DbRead.cs · DbFolderReader.TakeFile)
   ├─ SqlUserReader.Read(...)  ← 여기를 만드세요
   │     true  → 여기서 into.Tables 에 넣은 표를 씀
   │     false → 내장 SqlDumpReader (mysqldump 꼴) — 지금은 늘 false
   └─ 예외가 나면 그 파일만 "SqlUserReader 오류 (…)" 로 화면 위에 적고 넘어감
```

- 넘어오는 것: 파일 경로, 비어 있는 `DbSnapshot` (`into.Tables.Add(표)`, 화면에 띄울 말은 `into.Notes.Add`), 표 하나 행 상한 (200 만)
- 표 이름 앞의 폴더 이름 (`db1.` 등) 은 부르는 쪽이 붙임
- 표 모양: `DbTable { Name, Columns (DbColumn: Name · Type · IsKey · Ordinal), Rows (DbRow.Values = 적힌 글자 그대로 · NULL 은 null), HasRows = true, Note }` — 파일 안에 예시 주석
- 고르는 창 · 폴더 읽기 · 메인 칸에 놓기는 원래 .sql 을 받음 (바꿀 것 없음)
- 이 파일은 사용자 몫 — 이후 Claude 가 안 고침
- 규칙 그대로: SQL 실행 · DB 접속 없음, 바깥 라이브러리 없음

## 확인
- 테스트 1134 통과 (내장 .sql 덤프 테스트가 그대로 — 지금은 false 라 내장으로), 실제 csc 7.3 오류 · 경고 0, 검사 스크립트 통과
