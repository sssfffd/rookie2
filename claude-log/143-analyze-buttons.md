# 143 — 분석 1 · 2 도 메인 칸 [분석] 을 눌러야 돌게 (1.53)

> 분석 1이랑 2도 넣으면 자동 실행이 아니라 분석 3처럼 메인화면에 분석 버튼 달아줘서 누르면 실행되게 가능?

## 바뀐 동작

```
 ┌ 분석 1 ───────────[분석][→]┐ ┌ 분석 2 ───────────[분석][→]┐ ┌ 분석 3 ───────────[분석][→]┐
 │ ● 이전  A.csv     분석 대기 │ │ ● 이전  before.sql 분석 대기│ │ ● 이전  폴더A                │
 │ ● 이후  B.csv     IO 120 …  │ │ ● 이후  after.sql  파일     │ │ ● 이후  폴더B                │
```

| 어디서 | 1.52 | 1.53 |
|---|---|---|
| 메인 분석 1 칸 누르기 · 놓기 · 창 빈 곳에 놓기(메인 화면일 때) | 바로 읽고 견줌 | 이름만 들어가고 "분석 대기" |
| 메인 분석 1 [분석] | 없음 | 기다리던 쪽만 읽고 견줌. 기다리는 게 없으면 다시 견주기만. 빈 쪽 있으면 알림 |
| 메인 분석 2 칸 누르기 · 놓기 | 둘 다 차면 바로 읽음 | 자리만 적고 "분석 대기" |
| 메인 분석 2 [분석] | 없음 | 분석 2 화면 [데이터 읽기] 와 같음 (읽으면 "분석 대기" 지움) |
| 분석 1 · 2 화면 안의 열기 · 읽기 · 놓기 | 바로 | 그대로 바로 |
| 분석 3 | [분석] → Analysis3Link | 그대로 |

- 못 읽은 쪽은 "분석 대기" 를 남김 → 파일 고친 뒤 다시 누르면 됨
- 메인 칸에 놓을 때 읽을 수 없는 파일은 그 자리에서 거름 (이제 놓을 때는 안 읽으므로)
- 분석 1 화면에서 다른 파일을 바로 열면 그 쪽 "분석 대기" 도 지움 (나중에 [분석] 이 옛 자리로 덮지 않게)

## 바뀐 파일
- `Main/MainView.xaml(.cs)` — 칸 머리 [분석] 을 칸 셋 모두에, `OnCardAnalyze` (Tag = 칸 번호) → `LogAnalyzeRequested` · `DbAnalyzeRequested` · `A3AnalyzeRequested`
- `Main/AnalysisCardVm.cs` — `AnalyzeTip`
- `Main/MainVm.cs` — 기다리는 자리 (`SetWaitingLog` · `WaitingLog` · `ForgetWaitingLog` · `HasLogFor` · `SetDbWaiting` · `ClearDbWaiting`), 줄 이름 · 덧글 · 풍선이 기다리는 파일을 보여 줌
- `Main/MainTexts.cs` — 단추 설명 · "분석 대기" · 알림
- `Shell/ShellWindow.xaml.cs` — `ChooseLog` 분리, `TakeFiles(…, wait)` · `Put` · `Filled` · `Readable`, `OnMainLogAnalyze` · `OnMainDbAnalyze`
- `Analysis2/DbView.xaml.cs` — `PickFileFor` · `TakePaths` 는 자리만, `ReadNow` 추가
- 테스트 `MainAnalyzeWaitRules` (14 개), README

## 확인
- Linux: mono 컴파일 + 테스트 983 통과, 실제 csc 7.3 오류 0, 처리기 · 이름 겹침 · 소스 검사 통과
- **Windows 에서 못 봄**: 칸 1 · 2 머리에 단추가 들어간 모양 (제목이 좁아짐), 실제로 눌러서 읽기
