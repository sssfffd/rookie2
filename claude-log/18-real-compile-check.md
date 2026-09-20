# 18 — 매번 다른 오류가 나던 이유, 그리고 이제 진짜로 컴파일합니다

> 요청: "build.bat으로 빌드할때 매다 다른 오류들이 계속 발생하는데 전체
> 다시 점검해봐"

**진짜 원인은 제가 컴파일을 한 번도 안 해봤다는 것입니다.**

---

## 1. 먼저 컴파일할 방법부터 찾았습니다

이제까지 "이 컨테이너에는 .NET 이 없어서 컴파일을 못 해봤습니다" 라고
매번 적어 놓고는, **정말 없는지 제대로 찾아본 적이 없었습니다.**

이번에 찾아봤습니다.

| 시도 | 결과 |
|---|---|
| `dotnet` / `msbuild` | 없음 |
| .NET SDK 내려받기 | 프록시가 막음 |
| **`apt-get install mono-mcs`** | **됨** |

Mono 의 C# 컴파일러가 apt 에 있었습니다. 명령 한 줄이면 됐던 것을,
"없을 것" 이라고 넘겨짚고 몇 주를 눈으로 검사했습니다.

---

## 2. 컴파일해 보니 바로 나왔습니다

```
src/LogScope.Tests/Program.cs(51,17): error CS0103:
  The name `NumberTextHasNoExponent' does not exist in the current context
```

지난 커밋(`cf7cd19`)에서 묵은 테스트 셋을 지울 때, 그 사이에 끼어 있던
`NumberTextHasNoExponent` 까지 같이 지워 놓고 **부르는 줄은 남겨 뒀습니다.**

올린 판본에 그대로 들어가 있었습니다. 확인해 보면

```
$ git show cf7cd19:src/LogScope.Tests/Program.cs | grep -n NumberTextHasNoExponent
51:                NumberTextHasNoExponent();     ← 부르는 곳만 있고
                                                    정의는 없음
```

고치는 과정에서 하나 더 나왔습니다. 지워진 함수를 git 에서 되살릴 때
범위를 넓게 잡아 `HeatmapOrderIsSameForEveryBucketWidth` 가 두 벌이 됐고,
그건 `CS0111` 로 바로 걸렸습니다. **컴파일러가 있으니 5 초 만에 잡힙니다.**

---

## 3. 이제 어디까지 검사하나

### Core 와 Tests — 진짜로 컴파일하고 진짜로 돌립니다

```
[1/4] LogScope.Core          컴파일 OK
[2/4] LogScope.Tests         컴파일 OK
[3/4] 테스트 실행            통과 168 / 실패 0
```

지금까지 "손으로 따라가 봤습니다" 라고 적었던 것들 — 허용 오차 계산,
히트맵 칸 값, 접기 경계, 시간축 처리, 단위 읽기 — 을 **이제 기계가
확인합니다.** 168 개 전부 통과합니다.

### App — WPF 껍데기를 만들어 컴파일합니다

리눅스에는 WPF 가 없습니다. 그래서 `System.Windows`, `.Media`, `.Input`,
`.Controls` … 를 **시그니처만 흉내 낸 껍데기**(`scripts/wpfstub/`)를 만들고,
XAML 이 만들어 주는 부분(`InitializeComponent`, `x:Name` 필드)도
`make_xaml_stubs.py` 로 흉내 냈습니다.

```
[4/4] LogScope.App           컴파일 OK
```

이걸로 잡히는 것: 없는 이름 부르기, 중복 정의, 인자 개수/타입 안 맞음,
`CS0136` 이름 겹힘, `using` 빠짐, 템플릿 안의 `x:Name` 을 코드에서 쓰기.

**잡지 못하는 것:** XAML 컴파일 자체, 그리고 껍데기에는 있는데 진짜 WPF
에는 없는 멤버. 그래서 `build.bat` 이 여전히 정답이고, 이건 그 앞의 체입니다.

### XAML 이벤트 처리기 서명

새로 넣었습니다. 예전 검사는 **이름만** 봤습니다.

```xml
<Button Click="OnSave" />
```

이라면 `void OnSave(object, RoutedEventArgs)` 여야 합니다. 이름만 맞고
두 번째 인자가 다르면 윈도우에서만 깨집니다. 이제 서명까지 봅니다
(지금 전부 맞습니다).

---

## 4. 덤 — 소스에 UTF-8 BOM 을 붙였습니다

한글이 든 소스 65 개에 **BOM 이 하나도 없었습니다.**

C# 컴파일러는 BOM 이 없으면 파일을 **시스템 코드페이지**(한국어 윈도우면
CP949)로 읽으려 할 수 있습니다. 그러면 주석과 문자열의 한글이 이렇게 됩니다.

```
원문      /// "이만큼 벌어진 건 오차지 차이가 아니다" 를 정하는 한 곳.
CP949 로  /// "?씠留뚰겮 踰뚯뼱吏? 嫄? ?삤李⑥?? 李⑥씠媛? ?븘?땲?떎" 瑜? ?젙?븯?뒗 ?븳 怨?.
```

지금 쓰시는 VS2017 의 컴파일러(Roslyn)는 BOM 없는 UTF-8 도 알아서 읽으므로
아마 문제가 없었을 겁니다. 다만 `build.bat` 에는 **마지막 대비책**으로
.NET Framework 에 딸린 옛 컴파일러로 넘어가는 길이 있고, 그쪽은 코드페이지로
읽습니다. 위험을 없애는 비용이 0 이라 그냥 붙였습니다. 화면에 뜨는 한글이
깨질 일도 함께 사라집니다.

(`build.bat` 은 CP949 파일이라 그대로 뒀습니다 —
[11번](11-build-bat-encoding.md) 참고.)

---

## 5. 앞으로

올리기 전에 이것부터 돌립니다.

```
sh scripts/check_compile.sh      ← 진짜 컴파일 + 테스트 실행
python scripts/check_sources.py
python scripts/check_shadowing.py
python scripts/check_handlers.py
```

"컴파일을 못 해봤습니다" 는 말은 이제 **App 의 XAML 부분에만** 해당합니다.
C# 은 세 프로젝트 모두 컴파일되고, Core 의 동작은 테스트로 확인됩니다.

여전히 남는 말씀: 이 체를 통과해도 윈도우에서 처음 F5 에 오류가 날 수
있습니다. 나면 그대로 붙여 주세요 — 이제는 원인을 훨씬 좁게 짚을 수 있습니다.
