# 20 — 빌드 중간부터 한글이 사라지던 이유

> 요청: "build.bat하면 중간부터 한글 짤려서 나오는데?"

---

## 증상

시작은 멀쩡합니다.

```
  LogScope 빌드  [Release]
  ------------------------------------------------------------
  MSBuild : C:\Program Files (x86)\...\MSBuild.exe
  로그     : C:\work\rookie2\build.log
  버전     : 0.19
```

그런데 어느 지점부터 한글만 없어집니다.

```
  ============================================================
   .
   : C:\work\rookie2\out\LogScope.exe
   out     .
  ============================================================
```

한글이 **깨진 게 아니라 아예 빠졌습니다.** ASCII 인 `:` · `\` · 경로만
남았습니다. 그래서 줄이 "잘린" 것처럼 보입니다.

---

## 범인

`src/LogScope.Tests/Program.cs` 첫 줄이었습니다.

```csharp
private static int Main()
{
    Console.OutputEncoding = Encoding.UTF8;   // ← 이것
```

### 코드 페이지는 프로세스의 것이 아니라 창의 것입니다

이게 핵심입니다. `Console.OutputEncoding` 에 값을 넣으면 .NET 은 윈도우
API `SetConsoleOutputCP(65001)` 을 부릅니다. 그런데 코드 페이지는
**콘솔 창에 달린 성질**입니다. 프로세스에 달린 게 아닙니다.

그래서 **그 프로그램이 끝나도 창은 65001 인 채로 남습니다.**

```
build.bat 시작              창: 949   한글 잘 나옴
  MSBuild 실행              창: 949   잘 나옴
  "테스트" 찍기             창: 949   잘 나옴
  LogScope.Tests.exe 실행   창: 949 → 65001   ← 여기서 바뀜
  ...exe 끝남               창: 65001         ← 안 돌아옴
  "완료." 찍기              창: 65001  ✗ 한글 사라짐
  "실행 파일 :" 찍기        창: 65001  ✗
  "아무 키나 누르면..."     창: 65001  ✗
```

`build.bat` 은 CP949 로 저장돼 있으니 `완료` 는 `\xBF\xCF\xB7\xE1` 라는
바이트로 나갑니다. 창은 이제 그걸 UTF-8 로 읽습니다. `0xBF` 는 UTF-8 에서
올 수 없는 첫 바이트라 그냥 버려집니다. ASCII 부분만 살아남습니다.

**"중간부터" 라는 말이 정확히 이 지점을 가리켰습니다.** 테스트가 시작되는
자리입니다.

### 왜 그 줄이 거기 있었나

리눅스에서 Mono 로 테스트를 돌릴 때 `LANG` 이 비어 있으면 기본 인코딩이
ASCII 로 잡혀서 `통과 204 / 실패 0` 이 `?? 204 / ?? 0` 으로 나옵니다.
그걸 막으려고 넣은 줄인데, 윈도우에서는 얻는 것 없이 해만 끼쳤습니다.
한국어 윈도우 콘솔은 어차피 CP949 라 .NET 이 한글을 알아서 그 코드
페이지로 바꿔 내보냅니다. 건드릴 이유가 없었습니다.

---

## 고친 것 — 두 군데

### 1. 테스트가 남의 창을 건드리지 않습니다

```csharp
private static void UseConsoleEncoding()
{
    PlatformID id = Environment.OSVersion.Platform;
    bool windows = id == PlatformID.Win32NT || id == PlatformID.Win32Windows
                || id == PlatformID.Win32S  || id == PlatformID.WinCE;
    if (windows) return;                       // ← 손대지 않음

    try { Console.OutputEncoding = new UTF8Encoding(false); }
    catch (IOException) { }
    catch (NotSupportedException) { }
}
```

리눅스에는 "콘솔 코드 페이지" 라는 개념 자체가 없어서 남의 뒷정리를
망칠 일이 없습니다. 거기서만 UTF-8 로 잡습니다.

출력이 파일로 돌려져 있으면 런타임에 따라 예외가 나므로 감쌌습니다.
인코딩 하나 때문에 테스트가 아예 안 돌면 곤란합니다.

### 2. build.bat 이 매번 되돌립니다

앞으로 **어떤** 자식 프로그램이 코드 페이지를 바꿔 놓든 그 뒤가 멀쩡하도록,
자식을 부른 뒤마다 되돌립니다.

```bat
:cp
if not defined CP goto :eof
chcp %CP% >nul 2>nul
goto :eof
```

`%CP%` 는 **스크립트를 시작할 때 콘솔이 갖고 있던 값**입니다. 새 코드
페이지를 강요하는 게 아니라 원래대로 돌려놓기만 합니다. 애초에 949 가
아니었던 콘솔은 그대로 둡니다 — 앞 문서에서 하지 말라고 한
`chcp 65001` 과는 반대 방향입니다.

### errorlevel 을 먼저 챙깁니다

`call :cp` 가 `chcp` 를 돌리면서 `errorlevel` 을 0 으로 덮습니다. 그대로
두면 **빌드가 실패해도 성공한 걸로 보입니다.** 그래서 먼저 담아 둡니다.

```bat
"%MSBUILD%" ... 
set "RC=%ERRORLEVEL%"
call :cp

if not "%RC%"=="0" (
```

`if errorlevel 1` 대신 `if not "%RC%"=="0"` 을 쓴 건 덤입니다 —
`if errorlevel 1` 은 "1 이상" 이라서 음수 종료 코드(예기치 못한 죽음)를
성공으로 넘깁니다.

---

## 이번 일에서 얻은 것

자기 프로세스 안에서 끝나는 줄 알았던 설정이 사실은 **창 전체**를 바꾸고
있었습니다. `Console.OutputEncoding` 은 이름만 보면 "내 출력" 얘기 같지만,
실제로는 콘솔 창에 대고 거는 전역 설정입니다.

그래서 고친 방식도 두 겹입니다. 원인을 없앴고(테스트가 안 건드림),
그래도 누가 또 그러면 버티게 했습니다(build.bat 이 되돌림).
