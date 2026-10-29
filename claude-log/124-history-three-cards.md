# 124 — 종합 분석 결과 코드 위치 + History (분석 셋 요약 저장 · 다시 펼치기)

> 맨 위에 종합분석결과를 바꾸고싶은데 관련 코드들이 어디어디 있을까
>
> 그리고 맨 아래 저장된 분석은 이름 History로 바꿔주고 각 분석 1 / 분석 2 / 분석3에 해당하는 이름으로
> 가로 3개 세로 1개로 저장 한번할 때 마다 3개의 결과가 저장되게 해주셈
>
> 저장되야 되는 것들은 메인화면에 요약 내용으로 아래 history에서 예전 내역을 클릭하면 그거에 맞게
> 요약칸 변경되며 보여지게 해줄 수 있어?

## 1. 종합 분석 결과 — 코드 지도 (1.42 기준)

| 무엇 | 파일 : 줄 |
|---|---|
| 화면 (테두리 칸 전체) | `src/LogScope/Main/MainView.xaml` : 20 ~ 78 |
| ├ 제목 "종합 분석 결과" | 같은 파일 34 → 글은 `src/LogScope/Main/MainTexts.cs` : 20 `Label` |
| ├ 점 색 (초록 · 노랑 · 빨강) | 같은 파일 40 ~ 58 (`OverallGrade` 트리거) |
| ├ 큰 한 줄 "달라진 데가 있습니다" | 같은 파일 60 (`OverallHeadline`) |
| ├ 기준 "허용 오차 …" | 같은 파일 66 (`OverallBasis`) |
| ├ "비교 실행 …" 시각 | 같은 파일 70 (`ComparedText`) |
| └ 안내 글 | 같은 파일 74 (`Hint`) |
| 값 | `src/LogScope/Main/MainVm.cs` : 581 머리 주석, 588 `OverallHeadline`, 606 `OverallGrade`, 623 `OverallBasis`, 571 `ComparedText`, 887 `Hint` |
| 다시 그리기 | 같은 파일 122 `Raise("OverallHeadline")` … (Refresh 안) |
| 기준 글의 틀 | `src/LogScope/Main/AnalysisRecord.cs` : 136 `BasisText` |

지금은 **분석 1(로그 비교)만** 보고 판정합니다 — 분석 2 · 3 을 같이 반영하려면 `OverallHeadline` · `OverallGrade` 를 고치면 됩니다.

## 2. History (1.42)

```
┌ History ───────────────────────────────────────────── [지금 결과 저장] ┐
│ 기동 전   2026-10-29 14:02:00                                   [삭제] │
│ ┌ 로그 비교 ──────┐ ┌ DB 비교 ────────┐ ┌ 분석 3 ─────────┐          │
│ │ 87  차이 발생 3개│ │ FAIL 차이 발생 2개│ │ -               │          │
│ │ 그룹 1개 중 1개… │ │ TEMP_MAX 1 → 2 …│ │ [오류] 압력 … 외 │          │
│ │ a.xlsx → b.xlsx │ │ a.sql → b.sql   │ │ dir1 → dir2     │          │
│ └─────────────────┘ └─────────────────┘ └─────────────────┘          │
```

| | 전 | 후 |
|---|---|---|
| 이름 | 저장된 분석 | **History** (`MainTexts.HistoryNoteLabel`) |
| 저장 한 번 | 분석 1 의 수와 점수 셋 | **칸 셋의 요약 그대로** (`CardSnapshot`: 점수/낱말 · 개수 · 요약 줄 · 그룹 줄 · 견준 이름) |
| 저장 조건 | 분석 1 비교가 있어야 | 분석 1 · 2 · 3 중 **하나라도** 결과가 있으면 |
| 줄 모양 | 이름 · 파일 · 점수 · 지금과의 차이 | 이름 · 시각 + **칸 셋 가로** (분석 1 / 2 / 3, 이름은 config.txt) |
| 줄 누름 | 따로 창(SavedResultWindow) | **위 칸 셋에 그때 요약을 펼침**, 줄에 표시. 다시 누르거나 [지금 결과로] → 돌아옴 |
| 펼쳐 보는 중 | — | "분석 결과" 옆 띠 "History — 이름 [지금 결과로]" |

- 펼쳐 보는 중에 [지금 결과 저장] 을 누르면 먼저 지금 결과로 돌리고 저장합니다 (옛 요약을 다시 저장하지 않게).
- 펼쳐 보던 기록을 지우면 지금 결과로.
- 1.41 까지 저장한 기록은 요약이 없어 칸에 점수만, 요약 자리에 "요약 없음 (1.42 전에 저장)".
- 이전 · 이후 칸과 [분석] 단추는 펼쳐 보는 중에도 지금 것 그대로입니다 (고르는 자리라서).
- 뺀 것: `SavedResultWindow.xaml(.cs)` · `SavedTexts.cs` (줄을 누르면 칸에 펼치므로 따로 창이 필요 없음).

| 파일 | 바꾼 것 |
|---|---|
| `Main/CardSnapshot.cs` (새) | 칸 하나의 요약 + JSON + 줄 칸에 적는 짧은 글 |
| `Main/AnalysisRecord.cs` | `Cards`, `CardAt`, JSON `cards` (FileVersion 2, 1 도 읽음) |
| `Main/AnalysisCardVm.cs` | `TakeSnapshot` · `ApplySnapshot`, `GroupSummaryVm.Detail` |
| `Main/HistoryRowVm.cs` | `Cells` (칸 셋) · `IsSelected` · `SavedAtText`, `HistoryCellVm` (새) |
| `Main/MainVm.cs` | `ShowSaved` · `ShowLive` · `IsViewingHistory` · `ViewingText`, `SaveCurrent` 가 칸 셋을 찍음, `CanSave` |
| `Main/MainView.xaml(.cs)` | History 줄 모양, 펼쳐 보기 띠, 줄 누름 → `ShowSaved` |

## 확인

- 리눅스 컴파일 · 시험 934 통과 (History 13 개 더 — 찍기 · JSON · 파일 · 펼치기 · 옛 기록), 실제 C# 7.3 컴파일러 오류 0.
- **못 한 것**: XAML 화면 — History 줄의 칸 셋 모양 · 띠 · 고른 줄 색은 Windows 에서 확인 필요.
