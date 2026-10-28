# 112 — 분석 3 에서 폴더 열기 할 때 오류

> 이거 anal3 따로 만든거 프로젝트 포함으로 추가해서 실행시키니까 폴더 열기 할 때 에러남 왜이래

오류 글과 사용자 코드를 볼 수 없어서 까닭을 하나로 짚지는 못했습니다. 대신 두 가지를 했습니다.

## 1. 오류 창이 진짜 까닭을 보이게 (1.32)

`App.xaml.cs` 의 오류 창이 겉 오류의 글 한 줄만 적었습니다. 이름으로 찾아 만든 분석 3 화면이나
XAML 에서 난 오류는 겉이 `TargetInvocationException` · `XamlParseException` 이라 진짜 까닭이 속에
있습니다. 이제 **종류 + 속 오류까지 + 맨 속 오류의 호출 순서**를 적고, Ctrl+C 로 복사된다고 알려 줍니다.

```
예상하지 못한 오류가 났습니다. (이 창에서 Ctrl+C 를 누르면 글 전체가 복사됩니다)

TargetInvocationException: 호출 대상이 예외를 Throw했습니다.
└ UnauthorizedAccessException: 'D:\data' 경로에 대한 액세스가 거부되었습니다.

버전 1.32 (…)
   위치: System.IO.File.ReadAllText(…)
   위치: LogScope.Analysis3.MyMainView.OnOpenFolder(…)
```

## 2. 폴더 열기에서 흔한 까닭 (Python → C# 옮긴 코드)

| 종류 | 까닭 | 고침 |
|---|---|---|
| ThreadStateException (STA) | 폴더 고르는 창을 배경 작업 안에서 띄움 | 화면 스레드에서 고르고 경로만 넘기기 |
| FileNotFoundException (dll) | WindowsAPICodePack 같은 바깥 라이브러리 — `out` 에 dll 없음 | `System.Windows.Forms.FolderBrowserDialog` |
| UnauthorizedAccessException | 폴더를 파일처럼 읽음 / 권한 없는 하위 폴더까지 훑음 | `Directory.Exists` → `Directory.GetFiles` |
| DirectoryNotFoundException | Python 상대 경로 그대로 | `AppDomain.CurrentDomain.BaseDirectory` 기준 |
| MissingManifestResourceException | 옛 프로젝트 `Resources.resx` — Analysis3 의 resx 는 빌드에 안 들어감 | 글은 상수, 그림은 png |

전체 표: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4-4 장.

다음: 오류 창 글(Ctrl+C)과 폴더를 여는 부분 코드를 받으면 정확히 고칩니다.
