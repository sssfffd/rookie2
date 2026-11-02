# 163 — 안 쓰는 코드 · 내용 지우기 (1.72)

> 아 그리고 지금 안쓰는 파일들이나 내용 삭제좀

## 찾은 방법
- 선언마다 이름이 코드 · XAML · 테스트 어디에도 다시 안 나오는 것 (주석 빼고) 을 모두 훑음
- 실제 csc 를 경고 다 켜고 돌려 안 쓰는 필드 · 변수 · 이벤트
- XAML 스타일 · 브러시 키가 어디서도 안 쓰이는 것
- 저장소 파일 목록 (csproj · build.bat · 문서가 가리키는지)

## 지운 것 (8 파일, 224 줄)

| 어디 | 무엇 | 까닭 |
|---|---|---|
| `Main/AnalysisCardVm.cs` | `ScoreBands.Legend` · `BandVm` | 메인 화면 맨 아래 색 설명 (1.54 에 화면에서 뺌) 의 남은 코드 |
| `Main/HistoryRowVm.cs` | `LogsText` · `PathsText` · `CountsText` | History 줄에서 안 씀 |
| `Main/MainTexts.cs` | `ScoreTextTip` | 안 씀 |
| `Shell/ShellVm.cs` | `Analysis2Name` · `Analysis3Name` | 옛 "아직 만들지 않은 화면" 자리 |
| `Analysis1/Graph/ChannelListVm.cs` | `KindFilter` (디지털 · 아날로그 · 상태 거르기) · `SetSelected` | 화면에 단추 없음 |
| `Analysis1/Heatmap/HeatmapBuilder.cs` | `HeatBuckets.HasShifts` · `Walk` | 안 씀 |
| `Analysis1/Compare/StepPairs.cs` | `IndexOfLabel` | 안 씀 |
| `Themes/Controls.xaml` | `CardButton` · `BrandButton` · `MenuItemButton` 스타일 | 옛 위 줄 · 칸 단추 모양 — 쓰는 곳 없음 |

## 일부러 남긴 것
- **데이터 · 연결 쪽 공개 API** (`LogDataset.NotesText` · `EdgeCount`, `Channel.AddEmpty` · `SetAt` · `IsState`, `Cell` · `CsvReader` · `Json.GetObject`,
  `LlmLink.RequestedAt`, `LlmServer.CheckedAt`, `LogSteps.RangesFromStarts`, `DbCompare.AnyChange`) — 사용자 폴더 (LlmExport · 분석 3 ·
  단계 `Mine()`) 코드가 쓸 수 있어서. 저장소에 없는 그 코드는 여기서 볼 수 없음
- `Analysis3Link.ReportRequested`, `DbVerdict.FailFrom` · `JudgeGrade.Fail` (Obsolete) — 사용자 코드가 깨지지 않게 이름만 남긴 것
- `RelayCommand` 의 CanExecute · Execute — WPF 가 부르는 것 (ICommand)
- 파일: CMakeLists.txt · .vscode (처음 요구사항), REQUIREMENTS.md · claude-log (요청 기록), DesignData.cs (XAML 디자이너), Analysis3Placeholder (분석 3 폴더가 없을 때)

## 확인
- 테스트 1107 통과, 실제 csc 7.3 오류 0 (경고 다 켜도 남은 것은 일부러 남긴 ReportRequested 하나), 검사 스크립트 통과, XAML 모양 확인
- **Windows 에서 못 봄** (지운 것은 화면에서 안 쓰던 것이라 보이는 변화 없음)
