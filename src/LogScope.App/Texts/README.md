# 화면 글자 — 화면마다 파일 하나

단추 이름 · 칸 이름 · **마우스를 올리면 뜨는 설명**은 화면마다 이 폴더의 파일 하나에
모여 있습니다 (1.18). 바꿀 글을 그 파일에서 Ctrl+F 로 찾아 **따옴표 안만** 고치고
`build.bat` 으로 다시 빌드하면 됩니다.

| 화면 | 파일 | 글 수 |
|---|---|---|
| 메인 화면 | `MainTexts.cs` | 17 |
| 맨 위 메뉴 줄 (모든 화면) | `ShellTexts.cs` | 13 |
| 분석 1 — 대시보드 | `DashboardTexts.cs` | 9 |
| 분석 1 — 그래프 (단계 단추 [정렬] [단계마다] [전체 구간] 포함) | `GraphTexts.cs` | 69 |
| 분석 1 — 히트맵 | `HeatmapTexts.cs` | 25 |
| 분석 1 — 시간 맞추기 판 (그래프 · 히트맵 공용) | `AlignTexts.cs` | 12 |
| 분석 2 — DB 비교 | `DbTexts.cs` | 25 |
| 설정 창 | `SettingsTexts.cs` | 60 |
| 저장된 분석 보기 창 | `SavedTexts.cs` | 11 |
| 작은 창들 (읽는 중 · 이름 넣기 · 글 보기 · 준비 중 화면) | `CommonTexts.cs` | 11 |

## 고칠 때 지킬 것

- **왼쪽 이름은 바꾸지 마세요.** XAML 이 그 이름으로 찾습니다. 바꾸면
  `python3 scripts/check_sources.py` 가 `x:Static 대상 없음 …` 으로 알려 줍니다.
- 줄바꿈은 `\n`, 따옴표는 `\"` 입니다.
- 글이 길어지면 단추가 넓어집니다 (도구 줄이 두 줄로 접힐 수 있음).

## 여기 없는 글 — 상황에 따라 바뀌는 글

아래는 그때그때 **코드가 지어서** 띄우는 글이라 이 폴더에 없습니다. 고치려면
그 파일을 엽니다.

| 무엇 | 파일 |
|---|---|
| 단계 이름 앞뒤에 붙는 글 ("1단계" 의 "단계") | `src/LogScope.Core/Model/StepText.cs` |
| 분석 2 판정 낱말 (PASS · WARNING · FAIL) · 판정 문장 | `src/LogScope.Db/DbVerdict.cs` |
| 분석 2 목록 머리 (IO명 · 이전 value · 이후 value) | `src/LogScope.Db/DbNames.cs` |
| 메인 화면 카드 안 글 (점수 옆 안내 · 요약 줄) | `src/LogScope.App/Shell/AnalysisCardVm.cs` · `MainVm.cs` |
| 히트맵 요약 · 색 설명 · 안내 줄 | `src/LogScope.App/Logs/HeatmapVm.cs` |
| 그래프 커서 값 줄 · 단계 안내 | `src/LogScope.App/Logs/GraphVm.cs` · `StepVm.cs` |
| 분석 2 빈 목록 안내 | `src/LogScope.App/Db/DbVm.cs` |
| 알림 창 (MessageBox) | 그 화면의 `*.xaml.cs` |
| 프로그램 이름 · 분석 화면 이름 | `config.txt` (빌드 없이) |
