# 133 — Analysis1 파일 합치기 (같은 기능끼리)

> 그 analysis1 폴더가 화면별로 나눈건 좋았는데 너무 파일들이 여러개로 나눠진 거 같음
> 동일 기능으로 파일들만 통합할 수 있는거 통합해줘

## 바꾼 것 (1.50) — 54 개 → 32 개

화면 폴더(Graph · Heatmap · …)와 화면 파일(…View.xaml · …Vm · …Texts)은 그대로 두고, **같은 기능의 계산 ·
조각 파일**만 합쳤습니다. 코드는 한 줄도 바꾸지 않았습니다 (합친 뒤 코드 줄을 전과 견줘 보니 GraphCanvas 의
`partial` 선언 다섯 줄만 빠짐). 합친 파일 안에는 덩어리마다 `// ==== 전파일.cs — 1.50 에 이 파일로 합침` 줄이
있어 Ctrl+F 로 찾으면 됩니다.

| 폴더 | 합친 파일 | ← 전 파일 |
|---|---|---|
| Graph (18 → 8) | `GraphCanvas.cs` (그래프 판) | GraphCanvas · .Draw · .Lane · .Trace · .Axis · .Input (partial 여섯 → 한 클래스) |
| | `ChannelListVm.cs` (왼쪽 IO 목록) | ChannelListVm · IoRowVm · GroupRowVm |
| | `GraphSeries.cs` (그릴 값 계산, 새 이름) | Decimator · DifferenceSeries · TraceColors |
| | `StepVm.cs` (단계) | StepVm · StepBands |
| Heatmap (9 → 6) | `HeatmapBuilder.cs` (히트맵 계산) | HeatmapBuilder · HeatTypes · HeatBuckets · HeatLayout |
| Compare (4 → 3) | `DiffEngine.cs` (두 로그 견주기) | DiffEngine · DiffTypes |
| LogFile (14 → 6) | `Channel.cs` (IO 채널) | Channel · ChannelKind · ChannelAccum · FloatBuffer |
| | `TimeAxis.cs` (시간축) | TimeAxis · TimeKind · TimeText |
| | `LogSteps.cs` (로그 안의 단계) | LogSteps · StepRead · StepText |
| | `LogReader.cs` (로그 열기) | LogReader · OpenOptions |

그대로 둔 것: 화면 파일 전부(…View.xaml(.cs) · …Vm · …Texts), Align · Dashboard 폴더, HeatmapCanvas
(히트맵 그리기 — WPF), StepPairs · AiBridge (기능이 다름), LogDataset · TableBuilder.

함께 고친 것: csproj 목록, 주석 · README 의 옛 파일 이름 (예 `StepText.cs` → `LogSteps.cs (StepText)`),
"파일 다섯으로 나눈 것" 같은 옛 설명. 클래스 · 이름 공간은 그대로라 다른 화면 · 시험은 손대지 않음.

확인: 리눅스 컴파일 · 시험 951 통과, 소스 · 처리기 · 이름 검사 통과, 실제 C# 7.3 컴파일러 오류 0.
