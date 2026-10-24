# 104 — 1.23 빌드 오류 다시 보기: BuildInfo (XAML 임시 빌드)

> 너 만든거 다시확인해봐 빌드 에러나는 거 같은데?

## 찾은 것 — 1.23 에서 생긴 빌드 오류

**버전 글(`BuildInfo.g.cs`)이 XAML 임시 빌드에서 빠집니다.**

- 1.22 까지는 화면이 없는 `LogScope.Core` 프로젝트가 빌드 직전(`BeforeCompile`)에 이 파일을 만들고
  그때 컴파일 목록에 더했습니다.
- 1.23 에 프로젝트를 하나(`LogScope`, WPF)로 합치면서 그 일이 WPF 프로젝트로 왔습니다.
- WPF 는 XAML 을 컴파일하는 중에 **임시 프로젝트(`LogScope_xxxx_wpftmp`)를 한 번 더 빌드**합니다.
  그 빌드는 `BeforeCompile` 을 거치지 않고, 프로젝트 이름도 달라서 조건(`MSBuildProjectName ==
  'LogScope'`)에도 안 걸립니다. 그래서 임시 빌드에는 `BuildInfo` 가 없어
  `App.xaml.cs` · `SettingsVm.cs` · `AppSettings.cs` 에서 **"BuildInfo 이름이 없다"** 로 실패합니다.

### 고친 것

| 파일 | |
|---|---|
| `tools/BuildInfo.targets` | 빌드 **맨 처음**(`BeforeTargets="BeforeBuild"`)에 파일을 씀. 조건은 프로젝트 파일 이름이 아니라 `AssemblyName == 'LogScope'` (임시 프로젝트도 같음). 타깃 안에서 Compile 을 더하던 것은 지움 |
| `src/LogScope/LogScope.csproj` | `<Compile Include="BuildInfo.g.cs" />` 를 늘 적어 둠 → 임시 빌드도 이 파일을 봄 |

`BuildInfo.g.cs` 는 여전히 저장소에 없습니다 (`.gitignore`). 처음 빌드할 때 만들어집니다.

## 다시 확인한 것

이번에는 여기서 쓰던 검사(mono `mcs` + WPF 흉내)보다 엄격하게 한 번 더 봤습니다.

- **Microsoft 의 C# 컴파일러**(Microsoft.Net.Compilers 2.10.0 = VS2017 · C# 7.3)와
  **.NET Framework 4.6.1 참조 어셈블리**(진짜 WPF · WinForms 타입)를 NuGet 에서 **검사용으로만**
  임시 폴더에 받아 `src/LogScope` 전부와 `src/LogScope.Tests` 를 컴파일 → **오류 0**.
  (App.xaml 이 만들어 주는 `Main` 이 없어 라이브러리로 컴파일. 저장소 · out 폴더 · 프로그램에는
  아무것도 안 들어감.)
- 예전 프로젝트 셋(Core · Analysis1 · Analysis2)의 csproj 에 있던 것 중 합치며 빠진 것: 위
  BuildInfo 하나뿐 (EmbeddedResource · None · 따로 둔 컴파일 옵션 없음).
- csproj 목록 = 디스크 (빠진 파일 · 두 번 적힌 파일 없음).
- 기존 검사: mcs 컴파일 · 시험 872 통과 · `check_sources` / `check_handlers` / `check_shadowing`.

## 여기서 못 보는 것

- **XAML 컴파일(마크업 컴파일러)** 은 Windows 에서만 됩니다. XAML 이 가리키는 타입 · 상수 ·
  이름 공간은 `check_sources.py` 가 보지만, 그래도 오류가 나면 **오류 줄(파일 · 줄 번호 · MC/CS
  번호)을 그대로 붙여 주세요.**
- 예전 폴더(`src/LogScope.App` · `src/LogScope.Core` · `src/LogScope.Analysis1` ·
  `src/LogScope.Analysis2`)가 디스크에 남아 있으면 지우고 "솔루션 정리" 뒤 다시 빌드.
