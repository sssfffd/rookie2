# scripts/

**빌드에는 필요 없습니다.** `build.bat` 은 이 폴더를 쓰지 않고, 파이썬이나
mono 가 없어도 프로그램은 그대로 빌드되고 돌아갑니다.

여기 있는 것은 **올리기 전에 미리 훑어보는 검사**입니다. 개발을 맡은 쪽
(Claude)이 도는 환경은 리눅스라 윈도우 빌드를 돌릴 수 없어서, 컴파일러가
잡아 줄 만한 실수를 대신 잡으려고 만들었습니다.

```
sh scripts/check_compile.sh      진짜 컴파일 + 테스트 실행   ← 제일 중요
python scripts/check_sources.py  XAML · 리소스 · csproj 훑기
python scripts/check_shadowing.py 이름 겹침(CS0136)
python scripts/check_handlers.py XAML 이벤트 처리기 서명
```

## check_compile.sh — 제일 중요합니다

눈으로 훑는 게 아니라 **C# 컴파일러에게 물어봅니다.**

1. `LogScope.Core` 컴파일
2. `LogScope.Tests` 컴파일 **+ 실제로 실행** (통과/실패가 숫자로 나옵니다)
3. `LogScope.App` 컴파일 — 리눅스에 WPF 가 없으므로 `wpfstub/` 의 껍데기로 대신

필요한 것:

```
apt-get install -y mono-mcs mono-runtime \
  libmono-system-io-compression4.0-cil \
  libmono-system-io-compression-filesystem4.0-cil
```

mcs 가 없으면 조용히 건너뜁니다.

### 왜 만들었나

이 검사가 없던 동안 **컴파일 오류를 세 번 올렸습니다** — `RelayCommand`
생성자 모호성, `CS0136` 이름 겹침, 그리고 지우다 만 테스트 호출
(`NumberTextHasNoExponent`). 셋 다 사람이 눈으로 볼 게 아니라 컴파일러가
볼 일이었습니다 ([claude-log/18](../claude-log/18-real-compile-check.md)).

### 대신하지 못하는 것

- **XAML 컴파일.** 태그와 속성이 진짜 WPF 타입에 맞는지는 여기서 모릅니다.
- **진짜 WPF 타입.** `wpfstub/` 은 시그니처만 흉내 낸 껍데기라, 스텁에는
  있는데 진짜 WPF 에는 없는 멤버를 쓰면 여기서는 통과하고 윈도우에서 깨집니다.
- 그래서 **`build.bat` 이 여전히 정답입니다.** 이건 그 전에 거르는 체입니다.

## wpfstub/

WPF 를 흉내 낸 껍데기 소스입니다. `System.Windows`, `.Media`, `.Input`,
`.Controls`, `.Data`, `.Threading`, `Microsoft.Win32`, 그리고 폴더 고르기에
쓰는 `System.Windows.Forms` 조각이 들어 있습니다.

**동작은 하나도 흉내 내지 않습니다.** 메서드 몸통은 전부 비어 있고, 오로지
이름과 시그니처만 맞춰 두었습니다. 프로그램에는 들어가지 않습니다.

새 WPF API 를 쓰기 시작하면 `check_compile.sh` 가 "그런 타입 없다" 고
알려 줍니다. 그때 여기에 한 줄 보태면 됩니다.

## 나머지 검사

| 파일 | 보는 것 |
|---|---|
| `check_sources.py` | XAML 이 XML 로 읽히는지 · 밝은/어두운 테마 열쇠가 같은지 · 리소스 열쇠가 다 있는지 · 이벤트 처리기가 있는지 · 괄호 균형 · `.csproj` 파일 목록 |
| `check_shadowing.py` | 한 메서드에서 바깥과 안쪽이 같은 이름을 선언했는지 (`CS0136`) |
| `check_handlers.py` | XAML 이벤트 처리기의 **두 번째 인자 타입**이 맞는지. 이름만 맞고 타입이 틀리면 윈도우에서만 깨집니다 |

넷 다 지적이 없으면 0 으로, 있으면 1 로 끝납니다.
