# 03 — WinForms 에서 WPF 로

> 요청: "winforms 말고 wpf로해줘"

화면 쪽(`LogScope.App`)을 통째로 다시 썼습니다. **`LogScope.Core` 는 한 줄도
고치지 않았습니다.** 파싱·데이터·비교·설정이 화면에 기대지 않게 나눠 둔
덕분입니다. 이게 요구사항 0번의 "파싱/데이터와 UI 를 나눠 달라" 가 실제로
값을 하는 순간이었습니다.

## 1. 바뀐 것

| | WinForms 때 | WPF |
|---|---|---|
| 창 | `Form` + 코드로 자리 계산 | `Window` + XAML 레이아웃 |
| 색 | `Theme.cs` 의 `Color` 상수 | `Themes/Light.xaml` / `Dark.xaml` 의 브러시 자원 |
| 테마 바꾸기 | **프로그램 다시 켜야 했음** | **바로 바뀜** (자원 사전만 갈아 끼움) |
| 배율(DPI) | `Dpi.S(px)` 를 손으로 다 걸어야 했음 | **그럴 필요 없음** (단위가 픽셀이 아니라 DIP) |
| 줄바꿈 도구 줄 | `FlowLayoutPanel` + `AutoSize` | `WrapPanel` |
| IO 목록 | 직접 그리는 컨트롤 + 스크롤바 직접 관리 | `ListBox` + `DataTemplate` (가상화 기본 제공) |
| 그래프 | `System.Drawing` (GDI+) `OnPaint` | `FrameworkElement.OnRender` + `StreamGeometry` |
| 값 ↔ 화면 | 이벤트로 일일이 밀어 넣기 | 바인딩 (`INotifyPropertyChanged`) |

### 없어진 것

- **`Dpi` 클래스 전체.** WPF 는 좌표 단위가 DIP(1/96 인치)라서 배율이
  올라가면 알아서 커집니다. `Dpi.S(22)` 같은 호출이 코드 전체에서 사라졌습니다.
- **`FlowBar`, `Buttons`(버튼 만드는 도우미), `ChannelPanel`(직접 그리는 목록).**
  각각 `WrapPanel`, XAML `Style`, `ListBox` + `DataTemplate` 로 대체됐습니다.
- **"테마는 다시 켜야 적용됩니다" 안내.** 이제 바로 바뀝니다.

### 새로 생긴 것

```
Themes/Light.xaml, Dark.xaml   색만 들어 있는 자원 사전 (키 이름이 서로 똑같아야 함)
Themes/Controls.xaml           버튼·입력칸·목록·탭 모양
Themes/Palette.cs              직접 그리는 그래프/히트맵이 쓸 색 (얼린 Brush/Pen)
Themes/ThemeManager.cs         색 사전을 0번 자리에 갈아 끼움
Infrastructure/                INotifyPropertyChanged, 명령, 값 바꾸미
ViewModels/                    화면이 바인딩할 값들
Views/*.xaml(.cs)              창과 화면
Controls/GraphCanvas.cs        그래프 (OnRender)
Controls/HeatmapCanvas.cs      히트맵 (OnRender)
```

## 2. 왜 그래프는 여전히 직접 그리나

WPF 라고 채널 200 개 x 표본 5 만 개를 `Polyline` 같은 요소로 올릴 수는
없습니다. 요소 하나하나가 시각 트리에 남아 메모리와 레이아웃 시간을
잡아먹습니다. 그래서 `FrameworkElement` 하나에 `OnRender` 로 직접 그립니다.

다만 WinForms 때보다 나아진 점이 있습니다.

- **선 하나 = `StreamGeometry` 하나.** 점마다 `DrawLine` 을 부르지 않고
  한 덩어리로 넘깁니다. WinForms 때는 `DrawLines` 에 넘길 배열을 매번
  정확한 길이로 새로 잡아야 해서 쓰레기가 쌓였는데, 그 문제도 없어졌습니다.
- **`Freeze()` 한 브러시와 펜.** 얼린 것은 다시 만들 필요가 없습니다.
- `RenderOptions.SetEdgeMode(EdgeMode.Aliased)` 로 1 픽셀 선이 흐려지지
  않게 했습니다. 파형은 또렷해야 읽힙니다.

접는 계산(`Decimator`)은 `LogScope.Core` 에 그대로 있습니다. UI 기술과
상관없는 순수 계산이라 옮길 필요가 없었습니다.

## 3. 모니터별 DPI

WPF 는 원래 배율에 맞춰 커지지만, **창을 다른 배율의 모니터로 옮겼을 때
다시 그리는 것**은 .NET Framework 4.6.2 에서 들어온 기능입니다. 4.6.1 을
대상으로 빌드한 앱에서는 기본으로 꺼져 있어서 `App.config` 에서 명시로 켭니다.

```xml
<AppContextSwitchOverrides value="Switch.System.Windows.DoNotScaleForDpiChanges=false" />
```

이 설정은 **빌드 대상이 아니라 그 PC 에 깔린 런타임**이 읽습니다. 윈도우
10/11 은 4.8 을 들고 있으므로 그대로 동작하고, 4.6.1 까지만 있는 윈도우 7
에서는 조용히 무시되면서 시스템 배율로 돕니다. 어느 쪽이든 깨지지 않습니다.

## 4. 폴더 고르는 창 하나만 WinForms

WPF 에는 폴더 고르는 창이 없습니다. 설정 창의 "폴더 고르기" 버튼 두 개에서만
`System.Windows.Forms.FolderBrowserDialog` 를 씁니다
(`Views/SettingsWindow.xaml.cs`). 이것은 **바깥에서 받아 온 라이브러리가
아니라 .NET Framework 에 원래 들어 있는 어셈블리**입니다. NuGet 패키지는
여전히 0 개입니다.

## 5. 제대로 됐는지 확인한 것

> 요청: "wpf로 해주는거 제대로 했는지 다시 보고"

윈도우가 없어 컴파일은 못 하지만, 기계로 확인할 수 있는 것은 전부
확인했습니다.

| 검사 | 결과 |
|---|---|
| XAML 11 개가 올바른 XML 인가 | 전부 통과 |
| `Light.xaml` 과 `Dark.xaml` 의 키 이름이 같은가 | 한쪽에만 있는 키 없음 |
| `{DynamicResource}` / `{StaticResource}` 키가 전부 정의돼 있는가 | 없는 키 없음 |
| XAML 의 이벤트 처리기 이름이 코드비하인드에 있는가 | 전부 있음 |
| XAML 의 `{Binding 경로}` 가 뷰모델에 실제로 있는가 | 전부 있음 |
| C# 52 개의 괄호 짝 | 전부 맞음 |
| `using` 빠진 곳 | 없음 (전부 완전한 이름으로 쓴 경우였음) |
| `.csproj` 에 적힌 파일 = 디스크의 파일 | 세 프로젝트 모두 일치 |

이 중 마지막 항목에서 **빠져 있던 파일 세 개**(대시보드 코드비하인드,
히트맵 코드비하인드, 셸 창)를 찾아 채웠습니다. 앞선 작업이 중간에 끊기면서
생긴 구멍이었습니다.

여전히 **윈도우에서 컴파일해 본 적은 없습니다.** `build.bat` 에서 오류가
나오면 그 메시지를 알려 주세요.
