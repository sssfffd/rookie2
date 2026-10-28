# 분석 3 — Python 프로그램을 C# 으로 옮겨 붙이기 (사내 AI 에 넘기는 안내)

> 이 문서는 **사내 AI 에 같이 올리는 용도**입니다. 저장소 전체를 올릴 수 없으니, 무엇을 올리고
> 무엇을 부탁할지 정리했습니다. 맨 아래 "사내 AI 에 줄 요청문" 을 그대로 붙여 쓰면 됩니다.
> (작성 1.26 · 1.27 에 "폴더째 갈아 끼우기" 로 고침 · LogScope 저장소 `src/LogScope` 한 프로젝트 기준)

## 0. 한눈에

```
[이미 있는 Python 프로그램]  ──사내 AI──▶  src/LogScope/Analysis3/  (C# · WPF)
                                            Analysis3View.xaml      화면 모양
                                            Analysis3View.xaml.cs   단추 눌렀을 때
                                            Analysis3Vm.cs          화면이 보여 주는 값
                                            Analysis3Texts.cs       화면 글자 전부
                                            (계산 파일들).cs         Python 의 계산 부분 — WPF 안 씀
```

지금 `src/LogScope/Analysis3/` 에는 "아직 만들지 않았습니다" 자리(`Analysis3View`)만 있습니다.
**이 폴더는 통째로 갈아 끼우는 자리입니다** (1.27, 4 장). 사내 AI 는 이 폴더 안의 파일만 만들고,
`Analysis3View` 라는 이름의 화면을 하나 꼭 둡니다. csproj 나 다른 폴더는 건드리지 않습니다.

## 1. 사내 AI 에 올릴 것

### A. 꼭 올릴 것 (약 3,000 줄)

| 파일 | 줄 | 왜 |
|---|---|---|
| **이 문서** `docs/ANALYSIS3-PORTING.md` | — | 규칙 · 붙이는 자리 · 요청문 |
| `src/LogScope/README.md` | 71 | 폴더 = 화면 지도. 파일을 어떻게 나누는지 |
| `src/LogScope/Analysis3/Analysis3View.xaml` · `.xaml.cs` | 58 | 갈아 끼울 자리 (이 이름을 그대로 써야 함) |
| **본보기 화면** `src/LogScope/Analysis2/DbView.xaml` | 372 | 분석 하나짜리 완성 화면의 XAML — 파일 고르기 · 끌어다 놓기 · 목록 · 단추 줄 · 테마 색 쓰는 법 |
| `src/LogScope/Analysis2/DbView.xaml.cs` | 532 | 단추 처리 · 배경 작업으로 읽기 · 결과를 창에 알리기 (`ResultChanged`) |
| `src/LogScope/Analysis2/DbVm.cs` | 431 | 뷰모델 꼴 (`ObservableObject` · `Set` · `Raise`) |
| `src/LogScope/Analysis2/DbTexts.cs` | 70 | **화면 글자를 한 파일에** 모으는 꼴 (`x:Static t:…`) |
| `src/LogScope/Common/ObservableObject.cs` | 29 | 뷰모델 바탕 |
| `src/LogScope/Common/RelayCommand.cs` | 64 | 명령 |
| `src/LogScope/Common/Converters.cs` | 63 | `BoolToVisibility` · `TextToVisibility` |
| `src/LogScope/Common/BackgroundJob.cs` · `LoadingWindow.xaml` · `.xaml.cs` | 154 | 오래 걸리는 일은 진행 창 + 취소로 |
| `src/LogScope/Common/CardTag.cs` | 109 | 끌어다 놓는 칸의 색 바꾸기 (놓는 칸을 쓸 때) |
| `src/LogScope/Themes/Controls.xaml` | 749 | 쓸 수 있는 스타일 이름 (`PrimaryButton` · `ToolGroup` · `SegmentButton` · `H1` · `Muted` …) |
| `src/LogScope/Themes/Light.xaml` · `Dark.xaml` | 72 | 색 이름 (`Brush.Text` · `Brush.Bad` · `Brush.Good` …) — 색은 늘 이 이름으로 |
| `src/LogScope/Shell/ShellWindow.xaml` | 222 | 화면이 어떻게 갈아 끼워지나 (`a3:Analysis3View`) |
| **Python 원본** (분석 3) | — | 옮길 대상. 화면 캡처 몇 장도 같이 주면 좋습니다 |

### B. 분석 3 이 그 기능을 쓸 때만 더 올릴 것

| 이럴 때 | 올릴 파일 | 줄 |
|---|---|---|
| 결과를 **메인 화면 분석 3 칸**에 보이고 싶다 (점수 · PASS/FAIL · 요약 줄) | `src/LogScope/Main/AnalysisCardVm.cs` | 515 |
| 〃 (칸을 채우는 자리) | `src/LogScope/Main/MainVm.cs` | 678 |
| 설정을 **저장**해서 다음에 켤 때도 남겨야 한다 (경로 · 기준값 등) | `src/LogScope/Settings/AppSettings.cs` · `SettingsStore.cs` | 569 |
| 화면 이름을 config.txt 에서 바꾸게 | `src/LogScope/Settings/AppConfig.cs` · `config.txt` | 369 |
| 다른 분석 결과(로그 · DB)를 같이 써야 한다 | `src/LogScope/Common/AppState.cs` | 370 |
| 창 쪽 연결(파일을 메인 칸에 놓기 · 화면 이동) | `src/LogScope/Shell/ShellWindow.xaml.cs` · `ShellVm.cs` | 917 |
| **CSV / XLSX** 를 읽는다 | `src/LogScope/Common/CsvReader.cs` · `XlsxReader.cs` · `RowSource.cs` · `Cell.cs` · `ValueParse.cs` · `TextSniff.cs` | 1,170 |
| JSON 을 읽고 쓴다 | `src/LogScope/Common/Json.cs` | 302 |
| 숫자 · 시각을 글자로 적는다 | `src/LogScope/Common/NumberText.cs` | 77 |
| **그래프를 직접 그린다** (matplotlib 대신) | `src/LogScope/Analysis1/Heatmap/HeatmapCanvas.cs` (`OnRender` 로 그리는 본보기) · `src/LogScope/Themes/Palette.cs` · `ThemeManager.cs` | 934 |

### C. 올리지 않아도 되는 것

- `src/LogScope/Analysis1/**` — 로그 비교 · 그래프 · 히트맵 (분석 3 과 상관없음; 그래프를 직접 그릴 때만 위 B 의 HeatmapCanvas 하나)
- `src/LogScope/Analysis2/Db*.cs` 중 **DbView · DbVm · DbTexts 말고 나머지** — DB 읽기 · 비교 계산
- `src/LogScope.Tests/` · `scripts/` · `claude-log/` · 루트 `README.md`(2,000 줄 넘음) · `REQUIREMENTS.md`
  → 필요한 규칙은 이 문서에 다 옮겨 두었습니다.

## 2. 사내 AI 가 지킬 규칙

**만드는 환경**
- Visual Studio **2017**, **C# 7.3**, **.NET Framework 4.6.1**, **WPF**. Windows 7 · 10 · 11 에서 돕니다.
  - C# 8 이상 문법 금지: `??=`, `switch` 식, `using var`, 범위(`..`), nullable 참조형(`string?`),
    `record`, `init`, 파일 범위 namespace 등.
- **NuGet · 바깥 라이브러리 쓰지 않기.** 꼭 써야 하면 무엇을 왜 쓰는지, 위험해 보이는 점을 적어서
  먼저 묻기. → Python 의 `pandas` · `numpy` · `matplotlib` · `openpyxl` 은 아래 표처럼 대신합니다.
- 설치 프로그램 없음 — `out` 폴더째 복사해서 씁니다.

**Python → C# 대신하는 것**

| Python | 여기서 |
|---|---|
| tkinter · PyQt 화면 | WPF XAML (`Analysis3View.xaml`) + 뷰모델(`Analysis3Vm`) |
| pandas `read_csv` / `read_excel` | `Common/CsvReader` · `Common/XlsxReader` (직접 만든 것, 바깥 라이브러리 아님) |
| numpy 배열 · 계산 | `float[]` / `double[]` + 반복문 (10M 칸까지 — 값은 `float` 로 아껴 담기) |
| matplotlib 그래프 | `FrameworkElement` 를 상속해 `OnRender` 에서 직접 그리기 (`HeatmapCanvas` 꼴) |
| threading / 오래 걸리는 계산 | `BackgroundJob.Run(창, "제목", p => { … p.Report(…) … })` — 진행 창과 취소가 따라옴 |
| json | `Common/Json` |
| print / 로그 | 화면 글 또는 `MessageBox` (콘솔 없음) |
| 설정 파일 (ini · yaml) | `AppSettings` 에 필드를 더하고 `SettingsStore` 가 저장 |

**파일 · 폴더**
- 폴더 = 이름 공간. `src/LogScope/Analysis3/` 의 파일은 `namespace LogScope.Analysis3`,
  하위 폴더를 만들면 `LogScope.Analysis3.하위폴더`.
- 한 화면 = `XxxView.xaml` + `XxxView.xaml.cs` + `XxxVm.cs` + **`Analysis3Texts.cs`**.
- **계산 파일(Python 계산 부분을 옮긴 것)은 WPF 를 쓰지 않습니다** (`System.Windows` using 금지).
  화면 없이 시험할 수 있어야 합니다. 파싱/데이터와 UI 를 나눈다는 원래 요구사항입니다.
- **화면에 보이는 글자(단추 이름 · 설명 · 칸 이름)는 전부 `Analysis3Texts.cs`** 에 `public const string`
  으로 모으고, XAML 은 `Content="{x:Static t:Analysis3Texts.이름}"` 으로 가리킵니다
  (`xmlns:t="clr-namespace:LogScope.Analysis3"`). 글자만 고칠 때 그 파일 하나만 열면 되게.
  코드가 상황 따라 짓는 문장(요약 · 알림 창)은 그 코드에 둬도 됩니다.
- **맨 바깥 화면 이름은 `Analysis3View`** (`LogScope.Analysis3.Analysis3View`, `UserControl`,
  매개변수 없는 생성자). 창이 이 이름을 띄우고 아무 속성도 넘기지 않습니다.
- **csproj 에 파일을 적지 않습니다.** `tools/Analysis3.targets` 가 `Analysis3` 폴더 안의
  `.cs` · `.xaml` · 그림(png · jpg · gif · bmp · ico)을 빌드할 때 저절로 찾습니다.
  그 밖의 파일(txt · json 등)은 빌드에 안 들어갑니다 — 필요하면 Claude 에게 말하기.
- 주석은 한국어로, 왜 그렇게 했는지를 적기 (이 저장소의 다른 파일처럼).

**화면**
- 색은 **`{DynamicResource Brush.…}`** 로만 (밝은/어두운 테마가 바뀜). `#FF0000` 처럼 박지 않기.
- 단추 · 묶음은 `Themes/Controls.xaml` 의 스타일 키를 씁니다: `PrimaryButton` · `ToolGroup` ·
  `ToolGroupCaption` · `SegmentButton` · `ToolToggle` · `H1` · `H2` · `Muted` · `FieldLabel` · `SectionBox` …
- 마우스를 올리면 뜨는 설명(ToolTip)을 단추마다 — 글자는 Texts 파일에.
- 끌어다 놓는 칸을 만들면 그 안의 `TextBox` 에 `AllowDrop="False"` (안 그러면 파일 드롭을 먹음).

**안전**
- 프로그램이 **SQL 을 직접 실행하지 않기** (만들어 보여 주기만). DROP · DELETE 는 늘 주석으로.
- **자격 증명(비밀번호 · 토큰)을 저장하지 않기.**
- 프로그램이 띄울 **실행 파일 경로를 사람이 고치는 설정 파일에 두지 않기**.
- 큰 파일 (~10M 칸): 한꺼번에 복사하지 말고 흘려 읽기, 값은 `float`.

## 3. 사내 AI 에 줄 요청문 (그대로 붙여 쓰기)

```
첨부한 Python 프로그램(분석 3)을 C# 으로 옮겨, 첨부한 LogScope 프로젝트의 "분석 3" 화면으로
붙이려고 합니다. 첨부한 docs/ANALYSIS3-PORTING.md 의 규칙을 지켜 주세요.

만들 것 — 모두 src/LogScope/Analysis3/ 안, namespace LogScope.Analysis3:
1. Analysis3View.xaml / Analysis3View.xaml.cs — 화면. Analysis2/DbView 의 꼴을 따라
   (UserControl, 이름은 꼭 Analysis3View, 매개변수 없는 생성자 — 창이 이 이름으로 띄움).
2. Analysis3Vm.cs — ObservableObject 를 상속한 뷰모델.
3. Analysis3Texts.cs — 화면 글자 전부 (public const string). XAML 은 {x:Static t:…}.
4. Python 의 계산 부분을 옮긴 C# 파일들 — WPF 를 쓰지 않는 순수 C#.
(csproj 는 고치지 않습니다 — Analysis3 폴더 안의 파일은 빌드할 때 저절로 들어갑니다.)

지킬 것: VS2017 · C# 7.3 · .NET Framework 4.6.1 · WPF, NuGet/바깥 라이브러리 없이
(pandas/numpy/matplotlib 는 문서 2 장 표대로 대신), 색은 DynamicResource Brush.*,
스타일은 Themes/Controls.xaml 의 키, 오래 걸리는 일은 BackgroundJob.Run.

먼저 Python 프로그램의 기능 목록 · 화면 구성 · 입력/출력 파일 형식을 표로 정리해서 보여 주고,
그 다음 C# 파일을 하나씩 전부 (생략 없이) 주세요. 확실하지 않은 부분은 짐작하지 말고 질문으로
남겨 주세요.
```

## 4. 폴더째 갈아 끼우기 (1.27)

분석 3 은 사내에서 VS 로 만들고, 나머지는 이 저장소에서 고칩니다. 그래서 **`Analysis3` 폴더만
따로 들고 다니다가, 저장소를 새로 받을 때마다 그 폴더를 덮어 넣고 빌드**합니다.

```
 사내 PC                                 이 저장소 (Claude 가 고침)
 ┌──────────────────┐   새로 받기        ┌────────────────────────────┐
 │ 백업\Analysis3\  │ ─────────────────▶ │ src\LogScope\Analysis3\   │ ← 이 폴더만 지우고
 │  Analysis3View…  │   폴더째 덮어 넣기 │   (자리 화면뿐)            │   백업 폴더를 넣음
 │  그 밖의 .cs/.xaml│                   │ 나머지 전부 — 새것 그대로  │
 └──────────────────┘                    └────────────────────────────┘
                                                  └▶ build.bat
```

순서
1. 저장소를 새로 받습니다 (zip 이든 git 이든).
2. `src\LogScope\Analysis3\` 폴더를 **지우고**, 백업해 둔 `Analysis3` 폴더를 그 자리에 넣습니다.
   (덮어쓰기만 하면 자리 화면 `Analysis3View` 가 내 것으로 바뀌므로 그것도 됩니다. 지우는 쪽이
   지난번에 지운 파일이 남지 않아 깔끔합니다.)
3. `build.bat` (또는 VS 에서 빌드). csproj 에 파일을 더할 일이 없습니다.

명령 한 줄로 하려면 (cmd, 경로는 바꿔서):
```
rmdir /s /q src\LogScope\Analysis3 & xcopy /e /i /y D:\백업\Analysis3 src\LogScope\Analysis3
```

**왜 되나** — `src/LogScope/LogScope.csproj` 맨 끝이 `tools/Analysis3.targets` 를 가져오고, 거기서
`Analysis3\**\*.cs` (코드) · `Analysis3\**\*.xaml` (화면) · 그림 (Resource) 을 폴더에서 찾습니다.
VS 에서 이 폴더에 파일을 추가하면 VS 가 csproj 에 줄을 적어 넣는데, 같은 파일이 두 번 들어가지
않게 targets 가 먼저 그 줄을 지우고 다시 넣습니다. 그 csproj 는 다음에 새로 받으면 어차피 바뀝니다.

**지킬 것**

| | |
|---|---|
| 폴더 안에 `LogScope.Analysis3.Analysis3View` (UserControl) 가 있어야 함 | 창이 이 이름을 띄웁니다. 내 화면 이름이 다르면 이름을 바꾸거나, `Analysis3View.xaml` 을 하나 만들어 그 안에 내 화면을 넣기 |
| `Analysis3` 폴더 **밖**은 사내에서 고치지 않기 | 새로 받으면 덮어써져 사라집니다. 밖을 고쳐야 하면 Claude 에게 |
| 폴더 안에 `bin` · `obj` 를 넣지 않기 | 넣어도 빌드에서는 빠지지만 폴더만 커집니다 |
| 이름 공간은 `LogScope.Analysis3` (하위 폴더면 `.하위폴더`) | `check_sources.py` 가 봅니다 |

**메인 화면 분석 3 칸** — 이전 · 이후 자리와 결과 요약은 4-2 장 (1.29).
`config.txt` 의 `analysis3 = 이름 | 설명` 줄을 고치면 탭 · 칸 이름이 바뀝니다.

확인 (Claude 가 돌림): `sh scripts/check_compile.sh` (컴파일 · 시험),
`python3 scripts/check_sources.py` (XAML 이 가리키는 이름 · `Analysis3View` 있는지 · 폴더=이름 공간),
`check_handlers.py` · `check_shadowing.py`. XAML 컴파일은 Windows 의 `build.bat` 으로.

## 4-1. 내가 만든 화면을 분석 3 자리에 연결하기 (1.28)

창(`Shell/ShellWindow.xaml`)은 **`LogScope.Analysis3.Analysis3View`** 라는 이름 하나만 띄웁니다.
내 화면을 그 이름에 걸어 주면 끝입니다. 방법은 둘 — **B (감싸기) 를 권합니다.** 내 파일은 그대로 두고
작은 파일 두 개만 바꾸면 됩니다.

```
ShellWindow ──띄움──▶ Analysis3View (이름 고정, 껍데기)
                           └──안에 넣음──▶ 내 화면 (MyMainView 등, 이름 자유)
```

**A. 내 화면 이름을 `Analysis3View` 로 바꾸기** — 내 맨 바깥 화면이 이미 UserControl 이고 이름을
바꿔도 될 때. `x:Class="LogScope.Analysis3.Analysis3View"`, `.xaml.cs` 의 클래스 이름, 파일 이름을
모두 바꾸고, 저장소의 자리 화면 `Analysis3View.xaml(.cs)` 은 지웁니다.

**B. `Analysis3View` 를 껍데기로 두고 그 안에 내 화면 넣기** — 저장소의 자리 화면 두 파일을 아래로
바꿉니다 (`MyMainView` 를 내 화면 이름으로).

`Analysis3/Analysis3View.xaml`
```xml
<UserControl x:Class="LogScope.Analysis3.Analysis3View"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:my="clr-namespace:LogScope.Analysis3">
    <my:MyMainView />
</UserControl>
```

`Analysis3/Analysis3View.xaml.cs`
```csharp
using System.Windows.Controls;

namespace LogScope.Analysis3
{
    /// <summary>창이 띄우는 분석 3 자리. 실제 화면은 안에 넣은 MyMainView.</summary>
    public partial class Analysis3View : UserControl
    {
        public Analysis3View()
        {
            InitializeComponent();
        }
    }
}
```

내 화면의 이름 공간이 `LogScope.Analysis3` 가 아니면 `xmlns:my="clr-namespace:내이름공간"` 으로
바꿔도 빌드는 됩니다 (같은 프로그램 안). 다만 `check_sources.py` 가 "폴더 = 이름 공간" 으로
지적하니, 되도록 `LogScope.Analysis3` 로 맞추세요.

**내 맨 바깥 화면이 `Window` 라면** — 창 안에 창은 못 넣으므로 `UserControl` 로 바꿉니다.

| 바꿀 것 | 어떻게 |
|---|---|
| XAML 첫 줄 `<Window …>` · 끝 `</Window>` | `<UserControl …>` · `</UserControl>` |
| `.xaml.cs` 의 `: Window` | `: UserControl` (`using System.Windows.Controls;`) |
| `Title` · `Width`/`Height` · `WindowStartupLocation` · `ResizeMode` · `SizeToContent` · `Icon` · `Topmost` · `WindowStyle` 속성 | 지우기 (UserControl 에 없음 — 크기는 창이 정함) |
| `Window.Resources` | `UserControl.Resources` |
| `Closing` / `Closed` 이벤트 | `Unloaded` |
| `this.Close()` | 지우기 (분석 3 화면은 닫지 않고 탭을 옮김) |
| 대화 상자의 `Owner = this` | `Owner = Window.GetWindow(this)` |
| `MessageBox.Show(this, …)` | `MessageBox.Show(Window.GetWindow(this), …)` |

**따로 프로젝트일 때 있던 것**

| 내 프로젝트에 있던 것 | 여기서는 |
|---|---|
| `App.xaml` · `App.xaml.cs` | 가져오지 않음 (LogScope 의 것이 하나 있음). 켤 때 하던 일은 내 화면 생성자나 `Loaded` 로 |
| `App.xaml` 의 `Application.Resources` (스타일 · 색) | 내 화면의 `UserControl.Resources` 로 옮기기. 이름이 LogScope 스타일(`H1` · `PrimaryButton` …)과 겹치면 내 화면 안에서는 내 것이 이김 |
| `Properties\Settings` · `app.config` 설정 | 쓰지 않음 — 필요하면 Claude 에게 (LogScope 설정 파일에 칸을 더함) |
| NuGet 패키지 · 바깥 dll | 원칙상 안 씀 — 꼭 필요하면 무엇을 왜 쓰는지 먼저 |
| `Main` 메서드 · `StartupUri` | 필요 없음 |

연결이 됐는지: 빌드 후 분석 3 탭을 누르면 내 화면이 뜹니다. 빌드 오류가 나면 오류 줄 전체를
Claude 에게 주세요.

## 4-2. 메인 화면 분석 3 칸과 주고받기 (1.29)

메인 화면 분석 3 칸에 **이전 · 이후 자리**가 생겼고, 분석 3 이 올린 **결과가 요약 칸에** 뜹니다.
분석 3 폴더는 갈아 끼우는 자리라서 창 · 메인 화면과 직접 잇지 않고, 폴더 밖의 게시판
**`Common/Analysis3Link.cs`** 하나로만 주고받습니다. (폴더를 갈아 끼워도 이 파일은 그대로.)

```
 메인 화면 분석 3 칸                 Common/Analysis3Link            Analysis3 폴더 (내 코드)
 ┌──────────────────────────┐
 │ [FAIL]  차이 발생 [2] 개 │ ◀── ResultChanged ── Publish(결과) ◀── 내 분석이 끝나면
 │ ● 이전  before.csv  파일 │ ──▶ BeforePath/AfterPath ── PathsChanged ──▶ 둘 다 차면 내 분석 실행
 │ ● 이후  after.csv   파일 │
 │ ┌ 요약 ────────────────┐ │
 │ │ ● 항목 A     1 → 2   │ │ ──▶ (누르면) 분석 3 화면으로 + OpenRequested("항목 A")
 │ │ ● 항목 B     OK      │ │
 │ └──────────────────────┘ │
 └──────────────────────────┘
```

**메인 칸의 이전 · 이후** — 누르면 파일 고르는 창, 폴더는 끌어다 놓기 (분석 2 칸과 같음).
넣은 자리는 설정에 저장되고 다음에 켤 때 되살아납니다 (켤 때는 알림 없이 — 저절로 돌지 않게).

**내 코드에서 쓰는 것** (`using LogScope.Common;`)

| 쓰는 것 | 뜻 |
|---|---|
| `Analysis3Link.BeforePath` · `AfterPath` · `HasBoth` | 지금 넣어 둔 자리 (파일이나 폴더, 안 넣었으면 빈 글자) |
| `Analysis3Link.PathsChanged` | 사람이 자리를 바꿨을 때 (바뀔 때만 한 번) |
| `Analysis3Link.SetPath(true/false, 경로)` | 내 화면 안의 [열기] 단추로 골랐을 때 — 메인 칸에도 보이고 저장됨 |
| `Analysis3Link.Publish(결과)` · `ClearResult()` | 메인 칸에 결과 올리기 · 지우기. 배경 작업 안에서 불러도 됨 |
| `Analysis3Link.OpenRequested` | 메인 칸 요약 줄을 눌렀을 때 (그 줄의 `Target`) |

**결과 (`Analysis3Result`)**

| 칸 | 넣는 것 | 비우면 |
|---|---|---|
| `Verdict` | 크게 적는 낱말 `"PASS"` · `"WARNING"` · `"FAIL"` (다른 낱말도 됨) | 점수를 씀 |
| `Score` | 0~100 점수 (분석 1 처럼, 색도 같은 기준) | (NaN) 안 적음 |
| `Grade` | 낱말 색 `Analysis3Grade.Good/Warn/Bad` | 낱말로 고름 (PASS·OK 초록, WARNING·WARN 노랑, FAIL·NG 빨강) |
| `Count` | 오른쪽 "차이 발생 **N** 개" 의 N | 숨김 |
| `Note` | 낱말 오른쪽 작은 글 | 없음 |
| `Lines` | 요약 칸 줄 (`Analysis3Line`: `Label` · `Value` · `Grade` · `Detail` · `Target` · `ValueIsChange`) | `EmptyText` (없으면 "적을 것이 없습니다.") |

**예시** — 내 화면(`MyMainView`)의 코드 비하인드:

```csharp
using System;
using System.Windows.Controls;
using LogScope.Common;

namespace LogScope.Analysis3
{
    public partial class MyMainView : UserControl
    {
        public MyMainView()
        {
            InitializeComponent();
            Analysis3Link.PathsChanged += OnPathsChanged;      // 메인 칸에서 이전·이후를 바꾸면
            Analysis3Link.OpenRequested += OnOpenRequested;    // 메인 칸 요약 줄을 누르면
            // 켤 때 지난 자리: Analysis3Link.BeforePath / AfterPath 를 그냥 읽으면 됩니다.
        }

        private void OnPathsChanged(object sender, EventArgs e)
        {
            if (Analysis3Link.HasBoth) Run();      // 둘 다 차면 바로 돌리기
        }

        private void Run()
        {
            string before = Analysis3Link.BeforePath;
            string after = Analysis3Link.AfterPath;

            // ... 내 분석 ... (오래 걸리면 BackgroundJob.Run 안에서)

            var r = new Analysis3Result();
            r.Verdict = "FAIL";                    // 점수로 하려면 r.Score = 87;
            r.Count = "1";                         // "차이 발생 1 개"
            r.Lines.Add(new Analysis3Line("항목 A", "1 → 2", Analysis3Grade.Bad)
                        { Target = "항목 A", ValueIsChange = true });
            r.Lines.Add(new Analysis3Line("항목 B", "OK", Analysis3Grade.Good));
            Analysis3Link.Publish(r);              // → 메인 화면 분석 3 칸
        }

        private void OnOpenRequested(object sender, Analysis3OpenEventArgs e)
        {
            // e.Target 을 내 화면에서 찾아 보여 주기
        }
    }
}
```

결과가 없을 때 메인 칸은 크게 적는 자리를 **비워 둡니다** ("?? / 100" 이나 PASS 를 적으면 이미 본 것으로
읽히기 때문 — 분석 2 와 같은 규칙). 요약 칸에는 "아직 결과가 없습니다" 가 적힙니다.

아직 안 한 것: 메인 화면 "분석 결과 저장" 기록에 분석 3 의 낱말 · 요약 줄 넣기 (점수는 들어감).

## 5. 사내 AI 결과를 받을 때 확인할 것

- [ ] 파일이 전부 `src/LogScope/Analysis3/` 안이고 `namespace LogScope.Analysis3` 인가
- [ ] 계산 파일에 `using System.Windows` 가 없는가
- [ ] NuGet · 바깥 dll 이 없는가 (`<PackageReference>` · `packages.config` · 낯선 `<Reference>`)
- [ ] C# 8 이상 문법이 없는가 (`??=` · `switch` 식 · `using var` · `string?` · `record`)
- [ ] 화면 글자가 Texts 파일에 모여 있는가 (XAML 에 한글이 박혀 있지 않은가)
- [ ] 색이 `DynamicResource Brush.*` 인가
- [ ] Python 원본의 기능이 빠짐없이 옮겨졌는가 (1 단계에서 받은 기능 표와 맞대 보기)
