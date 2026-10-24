# 103 — 프로젝트 하나로 합치기 (화면 폴더에 그 화면 것 전부)

> 왜 app에도 코드가 있고 analysis는 또 왜존재하고 core는 왜있는거야 이따구로 이상하게 쪼개지말라고 찾아보기도 힘들고 찾아야되는 것도 많아지잖아 ㅡㅡ

1.22 에 "화면 기준" 이라면서 화면 쪽(App)과 계산 쪽(Core · Analysis1 · Analysis2)을 **다른
프로젝트**로 둔 채 이름만 바꿨습니다. 한 화면을 고치려면 여전히 두세 프로젝트를 오가야 했습니다.
잘못 짚었습니다.

## 바뀐 것

```
1.22 (프로젝트 다섯)                         1.23 (프로젝트 둘)
src/LogScope.App/        화면                src/LogScope/                 프로그램 전부 (LogScope.exe)
src/LogScope.Core/       공통 계산              Main/  Shell/  Analysis1/…  Analysis2/  Analysis3/
src/LogScope.Analysis1/  분석 1 계산            Settings/  Common/  Themes/
src/LogScope.Analysis2/  분석 2 계산          src/LogScope.Tests/           시험
src/LogScope.Tests/      시험
```

**화면 폴더 하나에 그 화면의 모양 · 코드 · 글자 · 계산이 다 있습니다.**

| 화면 | 폴더 | 그 폴더로 들어온 계산 |
|---|---|---|
| 메인 | `Main/` | 분석 기록 저장 (AnalysisRecord · HistoryStore) |
| 분석 1 그래프 | `Analysis1/Graph/` | 그리기 계산 (Decimator · DifferenceSeries · TraceColors · StepBands) |
| 분석 1 히트맵 | `Analysis1/Heatmap/` | 칸 세기 (HeatmapBuilder · HeatBuckets · HeatTypes · HeatLayout) |
| 분석 1 시간 맞추기 | `Analysis1/Align/` | TriggerAlign |
| 분석 1 여러 화면이 같이 씀 | `Analysis1/LogFile/` · `Analysis1/Compare/` | 로그 읽기 · 자료 · 단계 / 견주기 · 단계 짝 |
| 분석 2 | `Analysis2/` | DB 읽기 · 견주기 · 판정 · SQL 전부 |
| 설정 | `Settings/` | 설정 저장 · 허용 오차 표 · groups.txt |
| 공통 | `Common/` | CSV · XLSX 읽기 · 숫자 글자 · JSON · 검색 규칙 |

- 이름 공간도 폴더대로입니다: `LogScope.Main`, `LogScope.Analysis1.Graph`, `LogScope.Analysis2`, …
  (`LogScope.Core.*` · `LogScope.App.*` 는 이제 없습니다).
- `out` 폴더에는 **exe 하나**만 들어갑니다 (DLL 셋이 없어짐). 예전 `out` 의
  `LogScope.Core.dll` · `LogScope.Analysis1.dll` · `LogScope.Analysis2.dll` 은 지워도 됩니다.
- 시험(`LogScope.Tests`)은 `LogScope` 하나를 참조합니다. 계산 파일은 지금도 WPF 를 쓰지 않아
  창 없이 돌아갑니다 (요구사항 "파싱/데이터와 UI 가 나뉘어" 는 프로젝트가 아니라 **파일**로 지킴).
- 지도: `src/LogScope/README.md` (화면 → 폴더 → XAML → 글자 → 계산), 루트 README 3 · 4 장.

## 같이 고친 것

| 파일 | |
|---|---|
| `src/LogScope/LogScope.csproj` (옛 App) | 계산 파일 61 개 추가, 프로젝트 참조 지움, System.IO.Compression 참조, 버전 글(BuildInfo) 만들기를 여기로 |
| `src/LogScope.Tests/LogScope.Tests.csproj` | 참조 넷 → `LogScope` 하나 (+ WPF 참조) |
| `LogScope.sln` · `CMakeLists.txt` | 프로젝트 둘 |
| `build.bat` | 경로 `src\LogScope\`, DLL 복사 세 줄 지움 (CP949 · CRLF 그대로) |
| `tools/BuildInfo.targets` | `LogScope` 프로젝트에서 `namespace LogScope` 로 |
| `scripts/check_compile.sh` · `make_xaml_stubs.py` · `check_sources.py` · `check_handlers.py` | 새 경로 · 새 순서 (흉내 → LogScope → 시험) |
| XAML 17 개 | `x:Class` · `clr-namespace` (`assembly=` 지움 — 같은 어셈블리) |
| `.cs` 전부 | 이름 공간 = 폴더, `using` 다시 짬 |
| README · docs · 주석 | 옛 경로 |

## Windows 에서 확인 못 한 것

- **파일 130 개를 옮기고 프로젝트를 합쳤습니다.** 여기서는 C# 컴파일 · 시험 872 · XAML
  이름 공간 · x:Static · csproj 목록 · 폴더=이름 공간 검사가 다 통과했지만, XAML 컴파일 · VS
  솔루션 열기 · build.bat 은 Windows 에서만 됩니다. **첫 빌드에서 오류가 나면 그 줄을 알려
  주세요.**
- VS 가 예전 프로젝트의 `obj` · `bin` 을 들고 있으면 이상한 오류가 날 수 있습니다 — 그때는
  `src/LogScope.App` · `src/LogScope.Core` · `src/LogScope.Analysis1` · `src/LogScope.Analysis2`
  폴더가 디스크에 남아 있으면 지우고 (git 에는 이미 없음) "솔루션 정리" 뒤 다시 빌드.
