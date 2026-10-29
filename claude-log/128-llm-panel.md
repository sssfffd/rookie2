# 128 — 메인 "종합 분석 결과" 를 LLM 분석 칸으로

> 메인화면 종합분석결과 내용을 없애야함 / LLM 분석결과로 대체할꺼임 / 일단 기존 내용 대신에 LLM 분석 실행 버튼과
> 진행도를 나타내는 진행바가 있어야하고 / 종합분석 결과를 받아서 보여줘야되지 / 그리고 동작을 분석 1,2,3중에 하나만
> 비교해도 동작 가능하게 할꺼임 / 그래서 분석 1,2,3에 대한 체크 표시도 있어여돼(어떤게 지금 되있고 안되어있나) /
> 저 분석이 되있는지 안되있는지를 혹시 너가 알 수 없으면 알려줘 / 그리고 llm분석기능은 내가 따로 개발해놨어 /
> 일단 기능 연결은 맞추면 되니까 저 종합분석 시작 버튼이랑 받는거는 인터페이스만 맞춰줘 내가 다른 코드에서 수정할게

## 화면 (1.45)

```
┌ 종합 분석 결과 ───────────────────────────────────── [LLM 분석 실행] [취소] ┐
│ ● 분석 1 · 로그 비교  완료   ● 분석 2 · DB 비교  안 함   ● 분석 3  완료     │  ← 체크 (마우스 → 판단 근거)
│ [■■■■■■□□□□□□] 서버에 보내는 중                                            │  ← 돌 때만
│ 종합 판정: WARNING …  (결과 글 — 고르고 복사 가능, 길면 칸 안에서 굴림)      │
│ 2026-10-29 15:02:10 · 분석 1 · 3 결과로 분석                                │
└────────────────────────────────────────────────────────────────────────────┘
```
- 뺀 것: 한 줄 판정 · 점 · 기준 · 비교 시각 · 안내 (`MainVm.OverallHeadline/Grade/Basis` · `ComparedText` · `Hint` 삭제).
- [LLM 분석 실행]: 체크가 **하나라도** 완료면 켜짐, 도는 중엔 꺼짐. [취소] 는 도는 중에만.
- 진행 창(모달)이 아니라 칸 안 진행바 — LLM 이 오래 걸려도 다른 화면을 볼 수 있게.
- 실패하면 앞 결과는 두고 빨간 오류 한 줄. 취소도 같은 자리.

## 체크 — 무엇을 보고 "완료" 로 보나

| | 완료 조건 | 알 수 있나 |
|---|---|---|
| 분석 1 | 이전 · 이후 로그를 비교한 결과(`AppState.Comparison`)가 있음 | 앎 |
| 분석 2 | 이전 · 이후 DB 를 읽어 견줌(`AppState.HasDbResult`) | 앎 |
| 분석 3 | 분석 3 이 결과를 올림(`Analysis3Link.Result`) | **분석 3 코드가 `Analysis3Link.Publish(r)` 를 불러야만 앎.** 안 부르면 늘 "안 함" — 폴더만 넣은 것(이전/이후 자리)은 "됨" 으로 안 봄 |

## 연결 (인터페이스만) — `Common/LlmLink.cs`

```csharp
public interface ILlmAnalyzer
{
    // 배경 스레드. progress.Report("단계", 0~1 / 모르면 -1), 취소는 progress.IsCancelled · ThrowIfCancelled().
    // 돌려준 글이 칸에 그대로, 예외는 빨간 오류로.
    string Analyze(LlmRequest request, LoadProgress progress);
}
// LlmRequest: Use1 · Use2 · Use3 (체크와 같음) · Titles · State(AppState) · DbDiff · Analysis3 · Analysis3Before/After
// 꽂기: src/LogScope/Main/LlmSetup.cs 의 Register 에  LlmLink.Analyzer = new 내분석기();
```
꽂지 않고 누르면 "연결된 LLM 분석 기능이 없습니다 … LlmSetup.cs" 알림.

| 파일 | |
|---|---|
| `Common/LlmLink.cs` (새) | `ILlmAnalyzer` · `LlmRequest` · `LlmLink.Analyzer` |
| `Main/LlmSetup.cs` (새) | **꽂는 한 줄 자리** (예시 코드 주석) |
| `Main/LlmPanelVm.cs` (새) | 칸 값: 체크 · 진행 · 결과 · 오류 |
| `Main/MainVm.cs` | `Llm`, `RefreshLlmChecks`, `BuildLlmRequest` · 옛 종합 판정 값 삭제 |
| `Main/MainView.xaml(.cs)` · `MainTexts.cs` | 칸 화면 · 단추 · 글 |
| `Shell/ShellWindow.xaml.cs` | `OnLlmRun` (배경 스레드 · 화면 스레드로 진행/결과) · `OnLlmCancel` · `LlmSetup.Register()` |
| `Analysis2/DbView.xaml.cs` | `Diff` (분석 2 결과 전체를 요청에 넣으려고) |

## 확인

- 리눅스 컴파일 · 시험 946 통과 (칸 12 개 더), 실제 C# 7.3 컴파일러 오류 0.
- **못 한 것**: 화면 모양 (진행바 · 결과 글 상자) — Windows 확인 필요.
