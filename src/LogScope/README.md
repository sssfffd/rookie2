# 화면 폴더 지도 — 화면마다 폴더 하나

프로그램은 **프로젝트 하나**(`src/LogScope/LogScope.csproj`)이고, 폴더는 **화면 기준**입니다
(1.23). 고칠 화면의 폴더를 열면 그 화면의 모양(XAML) · 단추를 눌렀을 때(`.xaml.cs`) ·
보여 주는 값(`…Vm.cs`) · **글자(`…Texts.cs`)** · **계산**이 다 들어 있습니다.

| 화면 | 폴더 | 모양 (XAML) | 글자 | 계산 (같은 폴더) |
|---|---|---|---|---|
| 메인 | `Main/` | `MainView.xaml` | `MainTexts.cs` | History — 저장 한 번에 칸 셋 요약 `CardSnapshot` · 기록 `AnalysisRecord` · 파일 `HistoryStore` · 줄 `HistoryRowVm` (펼쳐 보기는 `MainVm.ShowSaved`) |
| 맨 위 메뉴 줄 · 창 틀 | `Shell/` | `ShellWindow.xaml` | `ShellTexts.cs` | |
| 분석 1 — 대시보드 | `Analysis1/Dashboard/` | `DashboardView.xaml` | `DashboardTexts.cs` | (`Analysis1/Compare/` 의 견주기 결과를 씀) |
| 분석 1 — 그래프 | `Analysis1/Graph/` | `GraphView.xaml` | `GraphTexts.cs` | 그리기 `GraphCanvas.*` · `Decimator` · `DifferenceSeries` · 선 색 `TraceColors` · 단계 칸 `StepBands` |
| 분석 1 — 히트맵 | `Analysis1/Heatmap/` | `HeatmapView.xaml` | `HeatmapTexts.cs` | 칸 세기 `HeatmapBuilder` · `HeatBuckets` · `HeatTypes` · 그룹/검색 `HeatLayout` |
| 분석 1 — 시간 맞추기 판 | `Analysis1/Align/` | `AlignPanel.xaml` | `AlignTexts.cs` | `TriggerAlign` |
| 분석 1 — 여러 화면이 같이 씀 | `Analysis1/LogFile/` · `Analysis1/Compare/` | — | — | 로그 읽기 · 자료 · 단계 이름(`StepText`) / 두 로그 견주기 `DiffEngine` · 단계 짝 `StepPairs` |
| 분석 2 — DB 비교 | `Analysis2/` | `DbView.xaml` | `DbTexts.cs` | DB 읽기 · 견주기 · 판정 · SQL (`Db…`) |
| 분석 3 — **사용자 폴더, 저장소에 없음** ([안내](../../docs/ANALYSIS3-PORTING.md) 4 장) | `Analysis3/` | `Analysis3View.xaml` (이 이름 꼭 · 없으면 `Shell/Analysis3Placeholder`) | — | 파일 목록은 csproj 가 아니라 `tools/Analysis3.targets` 가 폴더에서 찾음 · 메인 칸과는 `Common/Analysis3Link.cs` 로만 주고받음 |
| 메인 맨 위 종합 분석 결과 (LLM) | `Main/` · `Common/` | `MainView.xaml` | `MainTexts.cs` | 칸 `LlmPanelVm` · 꽂는 자리 **`Main/LlmSetup.cs`** · 모양 `Common/LlmLink.cs` (`ILlmAnalyzer` · `LlmRequest`) · 돌리기 `ShellWindow.OnLlmRun` |
| 설정 창 | `Settings/` | `SettingsWindow.xaml` | `SettingsTexts.cs` | 설정 저장 · 허용 오차 표 |
| 작은 창들 (읽는 중 · 이름 넣기 · 글 보기) | `Common/` | `LoadingWindow.xaml` 외 | `CommonTexts.cs` | CSV · XLSX 읽기 · 숫자 글자 · 검색 규칙 |
| 색 · 단추 모양 (모든 화면) | `Themes/` | `Light.xaml` · `Dark.xaml` · `Controls.xaml` | — | |

계산 파일(…View · …Vm · …Texts · …Canvas · 창이 아닌 것)은 WPF 를 쓰지 않습니다 — 그래서
`src/LogScope.Tests` 가 창 없이 그 계산을 시험합니다.

## Visual Studio 디자이너로 화면 보기 (1.22)

1. `LogScope.sln` 을 VS2017 로 엽니다.
2. **한 번 빌드**합니다 (빌드 → 솔루션 빌드). 디자이너는 빌드된 결과로 그립니다 —
   안 하면 "x:Static … 을 찾을 수 없음" 이 뜹니다.
3. 화면 폴더의 `.xaml` 을 두 번 누르면 위가 그림(디자인), 아래가 XAML 입니다.
   그림에서 단추를 누르면 XAML 의 그 줄로 갑니다.

- 화면마다 `d:DesignWidth="1200" d:DesignHeight="760"` 과 견본 뷰모델
  (`d:DataContext="{x:Static common:DesignData.Graph}"`, `Common/DesignData.cs`)을 넣어
  두었습니다. `d:` 는 **디자이너만** 읽는 자리라 실제 실행에는 영향이 없습니다.
- 견본에는 로그가 없습니다 — 단추 · 칸 · 글자의 자리를 보는 용도입니다. 그래프와
  히트맵 그림(GraphCanvas · HeatmapCanvas)은 코드가 그리는 판이라 디자이너에서는 빈
  판으로 보입니다.
- **글자는 XAML 이 아니라 `…Texts.cs` 에서** 고칩니다. 디자이너의 속성 창에서
  Content 를 고치면 `{x:Static …}` 이 지워지고 글자가 XAML 에 박힙니다 — 그러면 글자가
  두 곳에 나뉘니 되도록 `…Texts.cs` 를 고쳐 주세요.

## 글자 고치는 법

바꿀 글을 그 화면의 `…Texts.cs` 에서 Ctrl+F 로 찾아 **따옴표 안만** 고치고
`build.bat` 으로 다시 빌드하면 됩니다.

## 고칠 때 지킬 것

- **왼쪽 이름은 바꾸지 마세요.** XAML 이 그 이름으로 찾습니다. 바꾸면
  `python3 scripts/check_sources.py` 가 `x:Static 대상 없음 …` 으로 알려 줍니다.
- 줄바꿈은 `\n`, 따옴표는 `\"` 입니다.
- 글이 길어지면 단추가 넓어집니다 (도구 줄이 두 줄로 접힐 수 있음).

## 여기 없는 글 — 상황에 따라 바뀌는 글

아래는 그때그때 **코드가 지어서** 띄우는 글이라 `…Texts.cs` 에 없습니다. 고치려면
그 파일을 엽니다.

| 무엇 | 파일 |
|---|---|
| 단계 이름 앞뒤에 붙는 글 ("1단계" 의 "단계") | `src/LogScope/Analysis1/LogFile/StepText.cs` |
| 분석 2 판정 낱말 (PASS · WARNING · FAIL) · 판정 문장 | `src/LogScope/Analysis2/DbVerdict.cs` |
| 분석 2 목록 머리 (IO명 · 이전 value · 이후 value) | `src/LogScope/Analysis2/DbNames.cs` |
| 메인 화면 카드 안 글 (점수 옆 안내 · 요약 줄) | `src/LogScope/Main/AnalysisCardVm.cs` · `MainVm.cs` |
| 히트맵 요약 · 색 설명 · 안내 줄 | `src/LogScope/Analysis1/Heatmap/HeatmapVm.cs` |
| 그래프 커서 값 줄 · 단계 안내 | `src/LogScope/Analysis1/Graph/GraphVm.cs` · `StepVm.cs` |
| 분석 2 빈 목록 안내 | `src/LogScope/Analysis2/DbVm.cs` |
| 알림 창 (MessageBox) | 그 화면의 `*.xaml.cs` |
| 프로그램 이름 · 분석 화면 이름 | `config.txt` (빌드 없이) |
