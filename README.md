# LogScope — IO 로그 그래프 뷰어

엑셀(.xlsx) / CSV 로 뽑은 IO 로그를 열어 그래프로 보고, 개조 전/후 로그 두 개를
견주는 윈도우 데스크톱 프로그램입니다.

화면은 셋입니다. 위 메뉴에서 오갑니다 (**F1 / F2 / F3**).

- **대시보드** — 켜면 먼저 나옵니다. 이전/이후를 견준 요약 네모 칸과
  차이가 난 IO 목록. 목록에서 IO 를 누르면 그 IO 만 켜진 채로 그래프로 넘어갑니다.
- **그래프** — 시간에 따른 파형. 왼쪽 목록/그룹에서 직접 골라 볼 수도 있습니다.
- **히트맵** — 시간을 1 분씩 잘라 "어느 IO 가 어느 시간대에 벌어졌는지" 를
  한눈에. 칸을 누르면 그 IO 와 그 시간대로 그래프가 열립니다.

---

## 1. 빠르게 쓰기

```
build.bat
out\LogScope.exe
```

`out` 폴더를 통째로 복사하면 다른 PC 에서도 그대로 실행됩니다.
설치 프로그램이 없고, 레지스트리도 건드리지 않습니다.

---

## 2. 무엇으로 만들었나 (그리고 왜)

| 항목 | 고른 것 | 이유 |
|---|---|---|
| 언어 | **C# 7.3** | VS2017 이 그대로 지원. 요구사항에 있던 선택 |
| 실행 기반 | **.NET Framework 4.6.1** | 윈도우 10/11 에 이미 들어 있고, 윈도우 7 SP1 에도 설치 가능. .NET 5 이상은 윈도우 7 을 지원하지 않고 VS2017 로 빌드할 수 없습니다 |
| 화면 | **WPF** | 요청하신 대로. 배율(DPI)이 저절로 되고, 테마를 다시 켜지 않고 바꿀 수 있고, 목록 가상화가 공짜 |
| 구조 | **LogScope.Core.dll + LogScope.exe** | 파일 파싱/데이터와 화면을 나눔 (요구사항 0번) |
| 빌드 | **MSBuild (build.bat) + CMake(선택)** | 확장 없이 빌드되는 진입점. CMake 로도 VS 솔루션을 만들 수 있음 |
| 바깥 라이브러리 | **없음** | 아래 참고 |

### 바깥 라이브러리를 하나도 쓰지 않습니다

| 필요했던 것 | 쓴 것 | 어디 것인가 |
|---|---|---|
| .xlsx 압축 풀기 | `System.IO.Compression.ZipArchive` | .NET 기본 |
| .xlsx 안의 XML 읽기 | `System.Xml.XmlReader` | .NET 기본 |
| CSV 읽기 | 직접 구현 (`Io/CsvReader.cs`) | 저장소 안 |
| 설정 파일 JSON | 직접 구현 (`Settings/Json.cs`, 약 200줄) | 저장소 안 |
| 폴더 고르는 창 | `System.Windows.Forms.FolderBrowserDialog` | .NET 기본 (WPF 에 폴더 창이 없어서 이 한 곳만) |
| 그래프 그리기 | WPF (`OnRender` + `StreamGeometry`) | .NET 기본 |
| 테스트 | 직접 만든 콘솔 테스트 | 저장소 안 |

NuGet 복원 단계가 아예 없습니다. 네트워크가 막힌 PC 에서도 빌드됩니다.
자세한 보안 메모는 [docs/SECURITY.md](docs/SECURITY.md) 를 보세요.

### 미리 알아야 할 런타임

- **윈도우 10 / 11** — .NET Framework 4.8 이 운영체제에 들어 있습니다. 따로 깔 것 없음.
- **윈도우 7 SP1** — .NET Framework 4.6.1 이상이 필요합니다. 윈도우 업데이트를 받은
  PC 라면 대개 이미 있습니다. 없으면 .NET Framework 4.8 오프라인 설치 파일을
  **한 번만** 깔면 됩니다. (프로그램 자체는 그대로 복사해서 씁니다.)
- 윈도우 7 **SP1 이전**, 그리고 XP/Vista 는 지원하지 않습니다.

---

## 3. 폴더

```
version.txt              버전. 이 파일 한 곳에만 있습니다.
build.bat                확장 없이 빌드하는 진입점
CMakeLists.txt           CMake 로 VS 솔루션을 만들고 싶을 때
LogScope.sln             VS2017 로 바로 열 때

src/LogScope.Core/       파일 파싱 + 데이터 모델 (화면 참조 없음)
  Io/                    CSV / XLSX 읽기, 표 배치 판별, 시간축 만들기
  Model/                 채널, 데이터셋
  Compare/               두 로그 견주기, 차이량 계산, 히트맵 집계
  Render/                픽셀 열 접기 (그리기 전 단계 계산)
  Settings/              설정 저장, 작은 JSON
  Ai/                    선택 기능: 파이썬 모듈 호출

src/LogScope.App/        화면 (WPF)
  Themes/                색 사전(Light/Dark) + 컨트롤 모양 + 직접 그릴 때 쓰는 색
  Views/                 셸 창, 대시보드, 그래프, 히트맵, 설정 창, 진행 창
  ViewModels/            화면이 바인딩하는 값들
  Controls/              그래프 판, 히트맵 판 (직접 그리기)
  Infrastructure/        INotifyPropertyChanged, 명령, 값 바꾸미
  Services/              상태, 배경 작업

src/LogScope.Tests/      콘솔 자체 테스트
ai/                      선택 기능인 파이썬 분석 모듈 (없어도 됩니다)
claude-log/              작업 대화 기록
docs/                    보안 메모, 구조 설명
```

---

## 4. 빌드하는 세 가지 방법

**(1) build.bat** — 가장 간단합니다. MSBuild 를 스스로 찾아 빌드하고 테스트까지 돌립니다.

```
build.bat            Release + 테스트
build.bat Debug      Debug
build.bat Release nt 테스트 건너뛰기
```

**(2) Visual Studio 2017** — `LogScope.sln` 을 열고 F5.

**(3) VS Code (확장 없이)** — `Ctrl+Shift+B`. `.vscode/tasks.json` 이 `build.bat` 을 부릅니다.
   C# 확장이나 OmniSharp 이 없어도 빌드는 됩니다 (자동완성만 없습니다).

**(4) CMake (선택)**

```
cmake -S . -B build -G "Visual Studio 15 2017" -A x64
cmake --build build --config Release
```

---

## 5. 버전

버전은 `version.txt` **한 곳**에만 있습니다.
빌드할 때 `tools/BuildInfo.targets` 가 그 값과 `git rev-parse` 결과를 읽어
`BuildInfo.g.cs` 를 만들어 냅니다. 설정 창 → [정보] 에서 버전과 커밋 해시를
함께 볼 수 있습니다.

파일을 고칠 때마다 `version.txt` 를 0.01 씩 올립니다.

---

## 6. 조작 방법

| 하고 싶은 것 | 방법 |
|---|---|
| 시간 확대/축소 | 그래프 위에서 **휠** (한 칸에 약 14% 씩만 바뀝니다) |
| **값(세로) 확대/축소** | **Shift + 휠**. 커서가 가리키던 값이 제자리에 남습니다 |
| 이동 | 그래프를 **끌기** (가로 = 시간, 세로 = 값) |
| 커서 A | 그래프를 **누르기** |
| 커서 B | **Shift + 누르기** |
| 전체 구간 | **Home** 또는 [전체 구간 보기] |
| 화면 전환 | **F1** 대시보드 / **F2** 그래프 / **F3** 히트맵 |
| 로그 열기 | **Ctrl+O** 이전 / **Ctrl+Shift+O** 이후 |
| 비교 다시 하기 | **F5** |
| IO 여러 개 고르기 | **Shift + 누르기** 로 범위 (화면에 보이는 순서대로) |
| IO 끄기 | 켠 IO 를 **다시 누르기**. 다른 IO 를 눌러도 꺼지지 않습니다 |
| 그룹에 IO 넣기 | IO 를 그룹 이름 줄로 **끌어다 놓기**, 또는 [고른 IO를 이 그룹에 담기] |
| 히트맵 칸 너비 | **Ctrl + 휠** |
| 히트맵 옆으로 | **Shift + 휠** |
| 히트맵에서 그래프로 | 칸을 **누르기** (그 IO, 그 시간대로 열립니다) |

---

## 7. 알아 둘 점 / 아직 못 한 것

- 모니터별 DPI 는 `App.config` 의 스위치로 켭니다. 그 PC 에 .NET Framework
  4.6.2 이상이 깔려 있어야 동작합니다 (윈도우 10/11 은 4.8 이 기본).
  윈도우 7 에 4.6.1 만 있으면 조용히 시스템 배율로 돕니다.
- `.xls` (예전 엑셀 이진 형식) 는 읽지 못합니다. 엑셀에서 `.xlsx` 로 저장해 주세요.
- 히트맵은 이전 로그와 이후 로그가 **둘 다** 있어야 나옵니다.
- 이 저장소의 코드는 리눅스 컨테이너에서 작성했습니다. **윈도우에서 컴파일해 본
  적이 없습니다.** 처음 `build.bat` 을 돌릴 때 오류가 나오면 그 내용을 알려 주세요.
