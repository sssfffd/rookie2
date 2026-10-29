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

`src/LogScope/Analysis3/` 는 저장소에 없는 사용자 폴더입니다 (1.30, 4 장). 없으면 "아직 만들지 않았습니다" 자리 화면이 뜹니다.
**이 폴더는 통째로 갈아 끼우는 자리입니다** (1.27, 4 장). 사내 AI 는 이 폴더 안의 파일만 만들고,
`Analysis3View` 라는 이름의 화면을 하나 꼭 둡니다. csproj 나 다른 폴더는 건드리지 않습니다.

## 1. 사내 AI 에 올릴 것

### A. 꼭 올릴 것 (약 3,000 줄)

| 파일 | 줄 | 왜 |
|---|---|---|
| **이 문서** `docs/ANALYSIS3-PORTING.md` | — | 규칙 · 붙이는 자리 · 요청문 |
| `src/LogScope/README.md` | 71 | 폴더 = 화면 지도. 파일을 어떻게 나누는지 |
| `src/LogScope/Shell/Analysis3Placeholder.xaml` · `.xaml.cs` | 71 | Analysis3View 가 없을 때 뜨는 자리 (만들 화면 이름은 `Analysis3View`) |
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
| `src/LogScope/Shell/ShellWindow.xaml` | 222 | 분석 3 화면이 들어가는 자리 (`A3Host`) |
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
  매개변수 없는 생성자). 창이 이 이름을 띄웁니다. 설정 · 로그가 필요하면
  `public void Attach(AppState state)` 를 두면 창이 만든 직후 불러 줍니다 (1.33, DbView 와 같은 꼴).
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

## 4. 새 버전을 받아도 Analysis3 가 남게 (1.30)

분석 3 은 사용자가 VS 로 `src\LogScope\Analysis3\` 에 직접 만들고, 나머지는 이 저장소에서 고칩니다.
1.30 부터 **`Analysis3` 폴더는 저장소에 아예 없습니다** (`.gitignore`). 새 버전에는 그 폴더가 없으니
받아서 덮어도 내 파일이 지워지거나 덮이지 않습니다.

```
 새 버전 (zip / git)                 내 작업 폴더
 ┌──────────────────────┐           ┌─────────────────────────────┐
 │ src\LogScope\Main\   │ ──덮음──▶ │ src\LogScope\Main\    (새것)  │
 │ src\LogScope\Shell\  │ ──덮음──▶ │ src\LogScope\Shell\   (새것)  │
 │ (Analysis3 없음)     │           │ src\LogScope\Analysis3\ (그대로) │ ← 내 파일
 └──────────────────────┘           └─────────────────────────────┘
```

**zip 으로 받을 때 — 기존 폴더 위에 풀기**
1. 새 zip 을 **지금 쓰는 폴더 위에** 풉니다. 덮어쓰겠냐고 물으면 "모두 예".
2. `build.bat` (또는 VS 에서 빌드). 끝.

새 폴더에 풀었다면 옛 폴더의 `src\LogScope\Analysis3` 를 새 폴더의 같은 자리로 복사만 하면 됩니다.
```
xcopy /e /i /y D:\옛폴더\src\LogScope\Analysis3 D:\새폴더\src\LogScope\Analysis3
```

**git 으로 받을 때 — `git pull`**
`Analysis3` 는 git 이 보지 않는 폴더라 pull 해도 손대지 않습니다.
- **이번(1.30) 한 번만**: 1.27~1.29 에는 저장소에 `Analysis3\Analysis3View.xaml(.cs)` 자리 화면이 있었습니다.
  pull 하면 git 이 그 두 파일을 지우려 하므로(고쳤다면 pull 이 멈춤), **pull 전에 `Analysis3` 폴더를
  다른 곳에 복사해 두고, pull 뒤 다시 넣으세요.** 그다음부터는 그냥 pull.

**그래도 백업은** — 저장소에 없는 폴더라 그 PC 에만 있습니다. 가끔 다른 곳에 복사해 두세요.

**`src\LogScope\LlmExport\` 도 같습니다 (1.46)** — 사용자가 직접 고치는 폴더라 저장소에서 뺐고
(`.gitignore`), 파일 목록은 `tools/LlmExport.targets` 가 폴더에서 찾습니다. 파일 이름을 바꾸거나 더하거나
지워도 csproj 를 고칠 일이 없습니다. 폴더가 없어도 빌드는 됩니다.

**왜 되나**
- 파일 목록: `LogScope.csproj` 맨 끝이 `tools/Analysis3.targets` 를 가져오고, 거기서
  `Analysis3\**\*.cs` · `*.xaml` · 그림을 폴더에서 찾습니다. VS 가 csproj 에 줄을 적어 넣어도 targets 가
  먼저 지우고 다시 넣어 두 번 들어가지 않습니다 (그 csproj 는 다음에 새로 받으면 어차피 바뀜).
- 화면: 창은 `LogScope.Analysis3.Analysis3View` 를 **이름으로 찾아** 띄웁니다 (`ShellWindow.LoadAnalysis3`).
  없으면 "아직 만들지 않았습니다" 자리 화면(`Shell/Analysis3Placeholder`), 만들다 오류가 나면 그 까닭을
  자리 화면에 빨갛게 적습니다. 그래서 `Analysis3` 가 비어 있어도 빌드는 됩니다.
- 덮어 풀어도 되는 까닭: 저장소에서 지운 파일이 내 폴더에 남아도, `Analysis3` 밖은 csproj 에
  적힌 파일만 빌드하므로 남은 파일은 무시됩니다.

**지킬 것**

| | |
|---|---|
| 맨 바깥 화면은 `LogScope.Analysis3.Analysis3View` (UserControl, 매개변수 없는 생성자) | 창이 이 이름을 찾습니다. 이름이 다르면 4-1 장처럼 감싸기 |
| (선택) `public void Attach(AppState state)` 또는 `public void Attach()` | 있으면 창이 화면을 만들어 자리에 넣은 직후 불러 줍니다 (1.33). 그 안에서 난 오류는 자리 화면에 빨갛게 |
| `Analysis3` 폴더 **밖**은 사내에서 고치지 않기 | 새로 받으면 덮여 사라집니다. 밖을 고쳐야 하면 Claude 에게 |
| 폴더 안에 `bin` · `obj` 를 넣지 않기 | 넣어도 빌드에서는 빠지지만 폴더만 커집니다 |
| 이름 공간은 `LogScope.Analysis3` (하위 폴더면 `.하위폴더`) | `check_sources.py` 가 봅니다 |

**메인 화면 분석 3 칸** — 이전 · 이후 자리와 결과 요약은 4-2 장 (1.29).
`config.txt` 의 `analysis3 = 이름 | 설명` 줄을 고치면 탭 · 칸 이름이 바뀝니다.

확인 (Claude 가 돌림): `sh scripts/check_compile.sh` (컴파일 · 시험),
`python3 scripts/check_sources.py` (XAML 이 가리키는 이름 · 폴더=이름 공간),
`check_handlers.py` · `check_shadowing.py`. XAML 컴파일은 Windows 의 `build.bat` 으로.

## 4-1. 내가 만든 화면을 분석 3 자리에 연결하기 (1.28)

창은 **`LogScope.Analysis3.Analysis3View`** 라는 이름 하나만 찾아 띄웁니다 (없으면 자리 화면).
내 화면을 그 이름에 걸어 주면 끝입니다. 방법은 둘 — **B (감싸기) 를 권합니다.** 내 파일은 그대로 두고
작은 파일 두 개만 바꾸면 됩니다.

```
ShellWindow ──띄움──▶ Analysis3View (이름 고정, 껍데기)
                           └──안에 넣음──▶ 내 화면 (MyMainView 등, 이름 자유)
```

**A. 내 화면 이름을 `Analysis3View` 로 바꾸기** — 내 맨 바깥 화면이 이미 UserControl 이고 이름을
바꿔도 될 때. `x:Class="LogScope.Analysis3.Analysis3View"`, `.xaml.cs` 의 클래스 이름, 파일 이름을
모두 바꿉니다.

**B. `Analysis3View` 를 껍데기로 두고 그 안에 내 화면 넣기** — `Analysis3` 폴더에 아래 두 파일을
만듭니다 (`MyMainView` 를 내 화면 이름으로).

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

> **1.38 — 메인 칸에는 요약 줄(`Lines`)만 적습니다.** `Verdict` · `Score` · `Count` · `Note` 는 칸에 안 보입니다
> (`src/LogScope/Main/MainVm.cs` 의 `A3HeadlineOnCard = false` — true 로 켜면 보임). 결과가 오기 전에는
> 빈 칸. 결과 전체를 끄는 스위치는 `A3ResultOnCard` (지금 true).

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

**메인 칸의 이전 · 이후** — 누르면 **폴더** 고르는 창 (1.35), 파일 · 폴더 모두 끌어다 놓기도 됨.
넣은 자리는 설정에 저장되고 다음에 켤 때 되살아납니다 (켤 때는 알림 없이 — 저절로 돌지 않게).

**내 코드에서 쓰는 것** (`using LogScope.Common;`)

| 쓰는 것 | 뜻 |
|---|---|
| `Analysis3Link.BeforePath` · `AfterPath` · `HasBoth` | 지금 넣어 둔 자리 (파일이나 폴더, 안 넣었으면 빈 글자) |
| `Analysis3Link.PathsChanged` | 사람이 자리를 바꿨을 때 (바뀔 때만 한 번) |
| `Analysis3Link.SetPath(true/false, 경로)` | 내 화면 안의 [열기] 단추로 골랐을 때 — 메인 칸에도 보이고 저장됨 |
| `Analysis3Link.Publish(결과)` · `ClearResult()` | 메인 칸에 결과 올리기 · 지우기. 배경 작업 안에서 불러도 됨 |
| `Analysis3Link.OpenRequested` | 메인 칸 요약 줄을 눌렀을 때 (그 줄의 `Target`) |
| `Analysis3Link.AnalyzeRequested` | 메인 칸 머리의 **[분석]** 을 눌렀을 때 (1.35). 연결이 없으면 "연결된 기능이 없습니다" 알림 |

**결과 (`Analysis3Result`)**

| 칸 | 넣는 것 | 비우면 |
|---|---|---|
| `Verdict` | 크게 적는 낱말 `"PASS"` · `"WARNING"` · `"FAIL"` (다른 낱말도 됨) | 점수를 씀 |
| `Score` | 0~100 점수 (분석 1 처럼, 색도 같은 기준) | (NaN) 안 적음 |
| `Grade` | 낱말 색 `Analysis3Grade.Good/Warn/Bad` | 낱말로 고름 (PASS·OK 초록, WARNING·WARN 노랑, FAIL·NG 빨강) |
| `Count` | 오른쪽 "차이 발생 **N** 개" 의 N | 숨김 |
| `Note` | 낱말 오른쪽 작은 글 | 없음 |
| `Lines` | 요약 칸 줄 (`Analysis3Line`: `Label` · `Value` · `Grade` · `Detail` · `Target` · `ValueIsChange`). 길면 줄을 바꿔 다 보임 (1.40). `Label` 이 `"[오류] …"` 처럼 꼬리표로 시작하면 꼬리표만 점과 같은 색 — `Grade` 를 안 정하면 꼬리표로 고름 (오류·에러·ERROR·FAIL·NG 빨강, 경고·주의·WARN 노랑, 정상·OK·PASS 초록) | `EmptyText` (없으면 "적을 것이 없습니다.") |

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

## 4-3. 요약 칸에 넣는 코드는 어디에? (1.31)

**한 줄 답: 내 분석이 끝나고 결과를 내 화면에 보여 주는 바로 그 자리 아래에** 넣습니다.
보통 내가 만든 [분석] / [실행] 단추의 클릭 처리 함수 안, 계산이 끝난 직후입니다.

```
 src\LogScope\Analysis3\
   Analysis3View.xaml.cs   (또는 내 화면 MyMainView.xaml.cs)
     ├ 맨 위 using 줄들          ← ① using LogScope.Common; 한 줄 추가
     ├ 생성자                    ← ③ (선택) 메인 칸에서 이전·이후가 차면 저절로 돌리기
     ├ [열기] 단추 처리          ← ④ (선택) 고른 경로를 메인 칸에도 보이기
     └ [분석] 단추 처리
         ├ 기존: 파일 읽고 계산
         ├ 기존: 결과를 내 화면에 표시
         └ ② 여기! 결과를 Analysis3Result 로 옮겨 Publish
```

**찾는 법** — VS 에서 내 Analysis3 화면의 `.xaml.cs` 를 열고, 결과를 화면에 보이는 줄을 찾습니다.
`ItemsSource =` · `.Text =` · `DataGrid` · `MessageBox.Show(` 같은 줄이 보통 그 자리입니다.

**② 넣는 모양** (아래 `MyAnalyzer` · `diffs` · `d.Name` 같은 이름은 **내 코드의 이름으로** 바꾸세요)

```csharp
using LogScope.Common;                 // ① 파일 맨 위 using 들 사이에

private void OnRunClick(object sender, RoutedEventArgs e)     // 내 [분석] 단추 (이름은 내 것)
{
    // ---- 기존 코드 (그대로) ----
    List<MyDiff> diffs = MyAnalyzer.Compare(_beforePath, _afterPath);
    ResultGrid.ItemsSource = diffs;

    // ---- ② 여기부터 추가: 메인 화면 분석 3 칸으로 ----
    var r = new Analysis3Result();
    r.Verdict = diffs.Count == 0 ? "PASS" : "FAIL";           // 크게 적는 낱말 (점수면 r.Score = 87;)
    r.Count = diffs.Count.ToString();                          // "차이 발생 N 개"
    foreach (MyDiff d in diffs)                                // 요약 칸 한 줄씩
    {
        var line = new Analysis3Line(d.Name, d.Before + " → " + d.After, Analysis3Grade.Bad);
        line.Target = d.Name;            // 누르면 분석 3 화면으로 + OpenRequested 로 이 글자
        line.ValueIsChange = true;       // 값을 빨간 글씨로
        r.Lines.Add(line);
    }
    r.EmptyText = "차이가 없습니다.";   // 줄이 하나도 없을 때
    Analysis3Link.Publish(r);           // → 메인 화면 분석 3 칸이 바로 바뀜
}
```

**내 결과 → 요약 칸** 옮기는 표

| 내 결과에 있는 것 | 넣을 곳 | 메인 칸에 |
|---|---|---|
| 합격/불합격, OK/NG | `r.Verdict = "PASS"` / `"FAIL"` / `"WARNING"` | 크게, 색은 저절로 |
| 점수 (0~100) | `r.Score = 87;` (`Verdict` 는 비움) | `87 / 100` |
| 문제 개수 | `r.Count = "3";` | 오른쪽 "차이 발생 3 개" |
| 항목 하나하나 | `r.Lines.Add(new Analysis3Line(이름, 값, 색))` | 요약 칸 한 줄 |
| 항목의 덧글 | `line.Detail = "…";` | 줄 아래 작은 글 |

**③ (선택) 메인 칸에 이전 · 이후를 넣으면 저절로 돌리기** — 생성자의 `InitializeComponent();` 아래:

```csharp
Analysis3Link.PathsChanged += (s, e) =>
{
    if (!Analysis3Link.HasBoth) return;
    _beforePath = Analysis3Link.BeforePath;    // 내 코드가 경로를 담아 두는 변수
    _afterPath = Analysis3Link.AfterPath;
    OnRunClick(null, null);                    // 내 [분석] 단추 처리를 그대로 부름
};
```

**⑤ 메인 칸의 [분석] 단추 연결** (1.35, [리포트] 는 1.39 에 뺌) — 생성자나 `Attach` 안에서 한 번:

```csharp
Analysis3Link.AnalyzeRequested += (s, e) => OnRunClick(null, null);   // [분석] → 내 분석
```
단추 이름 · 설명 글은 `src/LogScope/Main/MainTexts.cs` 의 `A3Analyze` · `A3AnalyzeTip`.
단추 자리는 메인 화면 밖이라 사용자가 고치지 않습니다 — 연결은 위 한 줄로 Analysis3 쪽에서.
(`ReportRequested` 는 이름만 남아 있고 울리지 않습니다 — 연결해 둔 코드가 깨지지 않게.)

**④ (선택) 내 화면의 [열기] 로 고른 경로를 메인 칸에도** — 파일을 고른 줄 아래:
`Analysis3Link.SetPath(true, 고른경로);` (이전) · `Analysis3Link.SetPath(false, 고른경로);` (이후).
③ 을 넣었다면 둘 다 찼을 때 저절로 돕니다.

**오래 걸리는 계산을 배경 작업에서 돌린다면** — `Publish` 는 배경 작업 안에서 불러도 됩니다
(메인 칸 바꾸는 일은 창이 화면 스레드로 넘겨서 함).

## 4-4. 분석 3 에서 오류가 날 때 (1.32)

오류 창에서 **Ctrl+C** 를 누르면 글 전체가 복사됩니다. 1.32 부터 그 창에 오류의 **종류**와 **속 오류**,
진짜 난 자리(맨 속 오류의 호출 순서)가 적힙니다 — 그 글을 그대로 Claude 에게 주세요.

**폴더를 열 때** 자주 나는 것:

| 오류 창에 적힌 종류 | 흔한 까닭 | 고치는 법 |
|---|---|---|
| `ThreadStateException` (STA) | 폴더 고르는 창(`FolderBrowserDialog`)을 **배경 작업 안에서** 띄움 | 고르는 창은 [열기] 단추 처리(화면 스레드)에서 띄우고, 고른 경로만 배경 작업에 넘기기 |
| `FileNotFoundException` — `Microsoft.WindowsAPICodePack…` 등 dll 이름 | 폴더 고르기에 **바깥 라이브러리**(WindowsAPICodePack · Ookii 등)를 씀. 사내 PC 에서는 빌드됐어도 `out` 폴더에 그 dll 이 없음 | 바깥 라이브러리 대신 `System.Windows.Forms.FolderBrowserDialog` (LogScope 가 이미 참조) |
| `UnauthorizedAccessException` — "경로에 대한 액세스가 거부" | **폴더 경로를 파일처럼 읽음** (`File.ReadAllText(폴더)`), 또는 하위 폴더 중 권한 없는 곳까지 `AllDirectories` 로 훑음 | `Directory.Exists(경로)` 로 폴더인지 먼저 보고 `Directory.GetFiles(경로, "*.csv")` 로 파일을 꺼내기. 메인 칸에는 폴더를 끌어다 놓을 수 있으니 둘 다 받게 |
| `DirectoryNotFoundException` · `FileNotFoundException` (내 파일) | Python 의 상대 경로(`./data`, `os.getcwd()`)를 그대로 옮김 — 실행 위치가 달라짐 | 프로그램 옆 파일은 `AppDomain.CurrentDomain.BaseDirectory` 기준으로 |
| `MissingManifestResourceException` | 옛 프로젝트의 `Properties\Resources.resx` 를 씀 — `Analysis3` 폴더의 `.resx` 는 빌드에 안 들어감 | 글은 `Analysis3Texts.cs` 상수로, 그림은 `Analysis3` 폴더의 png (자동으로 들어감) |
| `ArgumentException` — "경로에 잘못된 문자" | `"*.csv;*.xlsx"` 처럼 여러 패턴을 한 번에, 또는 경로에 따옴표가 붙음 | 패턴마다 따로 `GetFiles`, 경로는 `Trim('"')` |

## 5. 사내 AI 결과를 받을 때 확인할 것

- [ ] 파일이 전부 `src/LogScope/Analysis3/` 안이고 `namespace LogScope.Analysis3` 인가
- [ ] 계산 파일에 `using System.Windows` 가 없는가
- [ ] NuGet · 바깥 dll 이 없는가 (`<PackageReference>` · `packages.config` · 낯선 `<Reference>`)
- [ ] C# 8 이상 문법이 없는가 (`??=` · `switch` 식 · `using var` · `string?` · `record`)
- [ ] 화면 글자가 Texts 파일에 모여 있는가 (XAML 에 한글이 박혀 있지 않은가)
- [ ] 색이 `DynamicResource Brush.*` 인가
- [ ] Python 원본의 기능이 빠짐없이 옮겨졌는가 (1 단계에서 받은 기능 표와 맞대 보기)
