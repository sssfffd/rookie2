# 보안 메모

"외부 라이브러리는 보안 때문에 자제해야 함" 이라는 요구에 맞춰, 무엇을 썼고
어디가 위험할 수 있는지 적어 둡니다.

## 1. 바깥에서 가져온 코드: 없음

NuGet 패키지가 하나도 없습니다. `packages.config` 도, `PackageReference` 도
없습니다. 빌드할 때 네트워크에 나가지 않습니다.

쓰는 것은 .NET Framework 에 원래 들어 있는 어셈블리뿐입니다.

| 어셈블리 | 쓰는 곳 |
|---|---|
| `System`, `System.Core` | 기본 |
| `System.IO.Compression` | .xlsx 의 zip 풀기 |
| `System.Xml` | .xlsx 안의 XML 읽기 |
| `WindowsBase`, `PresentationCore`, `PresentationFramework`, `System.Xaml` | WPF (창, 컨트롤, 그리기) |
| `System.Windows.Forms` | 폴더 고르는 창 하나에만 (아래 5번) |

## 2. 신뢰할 수 없는 파일을 다루는 자리

로그 파일은 남이 준 것일 수 있습니다. 파일을 여는 코드에서 조심한 것들:

### XML 외부 실체 참조 (XXE)

`XlsxReader.SafeSettings()` 에서 모든 XmlReader 에 다음을 걸어 둡니다.

```csharp
s.DtdProcessing = DtdProcessing.Prohibit;   // DTD 자체를 거부
s.XmlResolver = null;                       // 외부 문서를 불러오지 않음
```

이걸 끄면 `<!ENTITY ... SYSTEM "file:///...">` 같은 것으로 로컬 파일을
읽히거나, 실체 폭탄("billion laughs")으로 메모리를 터뜨릴 수 있습니다.
**이 두 줄을 지우지 마세요.**

### 압축 폭탄

`OpenOptions.MaxUncompressedBytes` 로 "압축을 푼 총량" 상한을 걸 수 있습니다.
**기본값은 0(무제한)** 입니다. "파일 크기 상한은 없게 해 달라" 는 요구를
따른 것입니다.

남이 준 파일을 자주 연다면 이 값을 켜는 편이 낫습니다. 예를 들어 2 GB:

```csharp
var options = new OpenOptions();
options.MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;
```

`MaxChannels`, `MaxSamples`, `MaxCells` 도 같은 식으로 0 = 무제한입니다.

### 경로

zip 안의 항목 이름을 **디스크 경로로 쓰지 않습니다.** 필요한 항목
(`xl/workbook.xml` 등)을 이름으로 찾아 스트림으로만 읽습니다. 그래서
`../../windows/system32/...` 같은 이름이 들어 있어도 아무 파일도 쓰지 않습니다.
zip 을 폴더에 푸는 코드가 이 저장소에 없습니다.

### 메모리

시트 XML 은 `XmlReader` 로 앞에서부터 흘려 읽습니다. 파일이 몇 백 MB 라도
한 번에 올라가는 것은 지금 읽는 한 줄과 공유 문자열 표뿐입니다.
값은 채널마다 `float[]` 하나로 모읍니다 (채널 200 x 표본 5만 = 약 40 MB).

## 3. 설정 파일

`LogScope.settings.json` 은 사람이 읽을 수 있는 JSON 이고, 읽는 코드는
`src/LogScope.Core/Settings/Json.cs` 한 파일(약 200줄)입니다. 직접 읽어보고
확인할 수 있는 크기로 일부러 작게 유지했습니다.

- 중첩 깊이를 64 로 제한합니다 (스택 넘침 방지).
- 파일이 깨져 있으면 예외를 삼키고 기본값으로 시작합니다. 설정 하나 때문에
  프로그램이 안 열리는 일은 없습니다.
- 저장은 임시 파일에 먼저 쓰고 바꿔치기합니다. 저장 중에 꺼져도 기존 설정이
  반쪽으로 남지 않습니다.
- 저장 위치: 실행 파일 옆에 쓸 수 있으면 거기, 아니면 `%APPDATA%\LogScope\`.

## 4. 폴더 고르는 창 때문에 참조하는 System.Windows.Forms

WPF 에는 폴더를 고르는 창이 없습니다. 설정 창의 "폴더 고르기" 버튼 두 개에서만
`System.Windows.Forms.FolderBrowserDialog` 를 씁니다
(`src/LogScope.App/Views/SettingsWindow.xaml.cs`).

- **바깥에서 받아 온 라이브러리가 아닙니다.** .NET Framework 에 원래 들어 있는
  어셈블리이고, 윈도우 7 / 10 / 11 어디에나 이미 있습니다.
- 하는 일은 셸의 폴더 선택 대화상자를 띄우는 것뿐입니다. 그 창이 돌려주는
  경로 문자열만 받아 설정에 넣습니다.
- 다른 곳에서는 쓰지 않습니다. 파일을 고르는 창은 WPF 에 있는
  `Microsoft.Win32.OpenFileDialog` 를 씁니다.

## 5. 선택 기능: 파이썬 AI 모듈

`src/LogScope.Core/Ai/AiBridge.cs` + `ai/analyze.py`.

**기본은 꺼져 있습니다.** 설정 창에서 켜고, `python.exe` 경로와 스크립트
경로를 사람이 직접 지정해야만 동작합니다.

켰을 때 일어나는 일과 위험한 부분:

- 지정한 실행 파일을 **자식 프로세스로 띄웁니다.** 이게 이 기능의 가장
  위험한 부분입니다. 남이 고쳐 쓸 수 있는 폴더(공용 다운로드 폴더 등)에 있는
  python.exe 를 지정하면 그 파일을 바꿔치기한 사람이 코드를 실행할 수 있습니다.
- 넘기는 것은 **채널 이름과 숫자 요약뿐**입니다. 로그 파일 내용이나 파일 경로는
  넘기지 않습니다.
- `AiBridge` 에는 네트워크로 나가는 코드가 없습니다. 다만 파이썬 스크립트는
  무엇이든 할 수 있으므로, 그 스크립트를 직접 읽어 확인하세요.
- 응답을 기다리는 시간에 상한이 있고, 넘으면 자식 프로세스를 끝냅니다.
- 이 기능이 필요 없으면 `ai/` 폴더를 지워도 나머지는 그대로 돕니다.

## 6. 히트맵

`Compare/Heatmap.cs` 는 이미 메모리에 올라온 두 데이터셋만 읽습니다.
파일을 다시 열지 않고, 디스크에 쓰지 않고, 바깥으로 아무것도 보내지 않습니다.
새로 생긴 위험한 자리는 없습니다.

## 7. 관리자 권한

필요 없습니다. `app.manifest` 에 `asInvoker` 로 적어 두었습니다.
설치 프로그램이 없고, 레지스트리를 건드리지 않고, `Program Files` 에
쓰지 않습니다.
