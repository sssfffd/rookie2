# 분석 3 — Python 프로그램을 C# 으로 옮겨 붙이기 (사내 AI 에 넘기는 안내)

> 이 문서는 **사내 AI 에 같이 올리는 용도**입니다. 저장소 전체를 올릴 수 없으니, 무엇을 올리고
> 무엇을 부탁할지 정리했습니다. 맨 아래 "사내 AI 에 줄 요청문" 을 그대로 붙여 쓰면 됩니다.
> (작성 1.26 · LogScope 저장소 `src/LogScope` 한 프로젝트 기준)

## 0. 한눈에

```
[이미 있는 Python 프로그램]  ──사내 AI──▶  src/LogScope/Analysis3/  (C# · WPF)
                                            Analysis3View.xaml      화면 모양
                                            Analysis3View.xaml.cs   단추 눌렀을 때
                                            Analysis3Vm.cs          화면이 보여 주는 값
                                            Analysis3Texts.cs       화면 글자 전부
                                            (계산 파일들).cs         Python 의 계산 부분 — WPF 안 씀
```

지금 `src/LogScope/Analysis3/` 에는 "아직 만들지 않았습니다" 자리(`PlaceholderView`)만 있습니다.
사내 AI 는 **이 폴더 안의 파일만** 만들고, 붙이는 몇 줄(4 장)은 따로 고칩니다.

## 1. 사내 AI 에 올릴 것

### A. 꼭 올릴 것 (약 3,000 줄)

| 파일 | 줄 | 왜 |
|---|---|---|
| **이 문서** `docs/ANALYSIS3-PORTING.md` | — | 규칙 · 붙이는 자리 · 요청문 |
| `src/LogScope/README.md` | 71 | 폴더 = 화면 지도. 파일을 어떻게 나누는지 |
| `src/LogScope/Analysis3/PlaceholderView.xaml` · `.xaml.cs` | 59 | 바꿔 끼울 자리 |
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
| `src/LogScope/Shell/ShellWindow.xaml` | 222 | 화면이 어떻게 갈아 끼워지나 (218 줄 `a3:PlaceholderView`) |
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
- 새 파일은 **`src/LogScope/LogScope.csproj` 에 등록** — XAML 은 `<Page Include="Analysis3\X.xaml">`,
  코드는 `<Compile Include="Analysis3\X.cs" />`, 코드 비하인드는 `<DependentUpon>X.xaml</DependentUpon>`.
  (DbView 가 등록된 모양을 따라 하면 됩니다.)
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
   (UserControl, Attach(AppState state) 메서드, 결과가 바뀌면 ResultChanged 이벤트).
2. Analysis3Vm.cs — ObservableObject 를 상속한 뷰모델.
3. Analysis3Texts.cs — 화면 글자 전부 (public const string). XAML 은 {x:Static t:…}.
4. Python 의 계산 부분을 옮긴 C# 파일들 — WPF 를 쓰지 않는 순수 C#.
5. LogScope.csproj 에 넣을 <Page>/<Compile> 줄 목록.

지킬 것: VS2017 · C# 7.3 · .NET Framework 4.6.1 · WPF, NuGet/바깥 라이브러리 없이
(pandas/numpy/matplotlib 는 문서 2 장 표대로 대신), 색은 DynamicResource Brush.*,
스타일은 Themes/Controls.xaml 의 키, 오래 걸리는 일은 BackgroundJob.Run.

먼저 Python 프로그램의 기능 목록 · 화면 구성 · 입력/출력 파일 형식을 표로 정리해서 보여 주고,
그 다음 C# 파일을 하나씩 전부 (생략 없이) 주세요. 확실하지 않은 부분은 짐작하지 말고 질문으로
남겨 주세요.
```

## 4. 받아 온 코드를 붙이는 자리 (이 저장소에서 할 일)

사내 AI 가 준 파일을 `src/LogScope/Analysis3/` 에 넣은 뒤, 아래 몇 줄만 고치면 붙습니다
(Claude 에게 "사내 AI 결과 붙여 줘" 하고 파일을 주면 이 작업과 검사를 합니다).

| 자리 | 지금 | 바꿀 것 |
|---|---|---|
| `src/LogScope/Shell/ShellWindow.xaml` 218 줄 | `<a3:PlaceholderView Heading="{Binding Analysis3Name}" …/>` | `<a3:Analysis3View x:Name="A3" Visibility="{Binding ShowAnalysis3, Converter={StaticResource BoolToVisibility}}" />` |
| `src/LogScope/Shell/ShellWindow.xaml.cs` 86 줄 근처 | `Db.Attach(_state);` | 그 아래 `A3.Attach(_state); A3.ResultChanged += …;` (메인 칸을 채울 때) |
| `src/LogScope/Main/MainVm.cs` 35 줄 | `new AnalysisCardVm(3, …, false)` | `true` (만들었음) + 결과를 `Cards[2].SetScore(…)` / `SetVerdict(…)` / `SetLines(…)` 로 |
| `config.txt` | `analysis3 = 분석 3 \| 아직 정해지지 않았습니다.` | 진짜 이름과 한 줄 설명 |
| `src/LogScope/LogScope.csproj` | — | 새 파일 등록 (사내 AI 가 준 목록) |
| `PlaceholderView.xaml(.cs)` | — | 다 붙인 뒤 지우거나 그대로 둠 |

붙인 뒤 확인 (Claude 가 돌림): `sh scripts/check_compile.sh` (컴파일 · 시험),
`python3 scripts/check_sources.py` (XAML 이 가리키는 이름 · csproj 목록 · 폴더=이름 공간),
`check_handlers.py` · `check_shadowing.py`. XAML 컴파일은 Windows 의 `build.bat` 으로.

## 5. 사내 AI 결과를 받을 때 확인할 것

- [ ] 파일이 전부 `src/LogScope/Analysis3/` 안이고 `namespace LogScope.Analysis3` 인가
- [ ] 계산 파일에 `using System.Windows` 가 없는가
- [ ] NuGet · 바깥 dll 이 없는가 (`<PackageReference>` · `packages.config` · 낯선 `<Reference>`)
- [ ] C# 8 이상 문법이 없는가 (`??=` · `switch` 식 · `using var` · `string?` · `record`)
- [ ] 화면 글자가 Texts 파일에 모여 있는가 (XAML 에 한글이 박혀 있지 않은가)
- [ ] 색이 `DynamicResource Brush.*` 인가
- [ ] Python 원본의 기능이 빠짐없이 옮겨졌는가 (1 단계에서 받은 기능 표와 맞대 보기)
