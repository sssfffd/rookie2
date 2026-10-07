# 58 — DB 설정 빼내기 + 모듈 다시 묶기 (0.77)

## 주신 말

> 일단 지금 db관련 설정들을 config.txt에서 맞추는 건 진짜 아닌 거 같아
> 해당 내용들 빼줘 그리고 파일들좀 모듈화 잘 시켜줘 너무 잘게 쪼개지도 말고
> 너무 하나에 몰아 넣지도 말고 지금 app이랑 core로만 나눈건 너무 몰아놓았고
> 해당 폴더안에 뭐 하나 고치는데 수정 파일만 4개 이상되는 건 심하잖아
> 그리고 수정하고나면 v1,v2 이름 어디서 수정해야되는 지 표시해주고

---

## 1) DB 설정을 config.txt 에서 빼냈습니다

지웠습니다: `db.before` · `db.after` · `db.mysqldump` · `db.dump.before` ·
`db.dump.after` · `db.ibd2sdi` · `db.col.*` (0.75 에서 제가 넣은 것).

| 값 | 전 | 지금 |
|---|---|---|
| 견줄 두 자리 | config.txt **또는** 화면 | **화면만** (프로그램이 settings.json 에 저장) |
| mysqldump · ibd2sdi 경로, 덤프 인수 | config.txt | **설정 창 → [DB 도구 (선택)]** |
| 칸 이름 (`v1`, `v2` …) | config.txt `db.col.*` | **`DbNames.cs` 한 줄** |

`config.txt` 에 남은 것은 프로그램 이름 · 긴 이름 · 분석 화면 이름 셋입니다.

### 왜 옳은 지적이었나 — 버그가 하나 있었습니다

자리가 두 군데에 있으면 "지금 읽는 자리가 어느 쪽인지" 를 매번 따져야
합니다. 실제로 따지다가 틀렸습니다:

```
DbVm.BeforePath     → 설정 우선, 없으면 config.txt   (화면이 보여 주는 값)
DbView.OnRead       → cfg.DbBefore                   (실제로 읽는 값)
```

화면에서 자리를 **고른 사람**에게는 `cfg.DbBefore` 가 빈 글자였습니다.
자리를 정해 놓고 [DB 읽기] 를 눌렀는데 "읽은 표가 없습니다" 가 나오는
상태였습니다. 자리를 한 곳(설정)으로 모으면서 같이 고쳤습니다.

### 도구 경로는 왜 지우지 않고 옮겼나

`mysqldump` 경로는 **이 프로그램이 자식 프로세스로 띄울 실행 파일의
자리**입니다. 그걸 손으로 고치는 텍스트 파일에 둔 것이 특히 좋지
않았습니다 — 그 파일을 고칠 수 있는 사람이 띄울 프로그램을 고를 수 있게
됩니다. 기능 자체는 쓰시던 것이라 지우지 않고, 프로그램이 들고 있는
설정으로 옮겼습니다. 아예 빼기를 원하시면 말씀해 주세요.

---

## 2) 모듈 다시 묶기

### 전 — 레이어로 나뉘어 있었습니다

```
src/LogScope.Core/   (31 파일)   Io Model Compare Render Settings Align History Ai Db
src/LogScope.App/    (54 파일)   Views/ ViewModels/ Controls/ Infrastructure/ Services/ Themes/
```

DB 화면 하나를 고치려면 `Views/DbView.xaml` → `ViewModels/DbVm.cs` →
`Core/Db/*` 로 폴더를 왕복해야 했습니다. "한 가지" 가 세 폴더에
흩어져 있었습니다.

### 지금 — 기능으로 묶었습니다

```
src/LogScope.Core/   25 파일  4460 줄   바탕: Model Io Settings Text
src/LogScope.Logs/   11 파일  2478 줄   분석 1 알맹이: Compare Align Render History Ai
src/LogScope.Db/      7 파일  3939 줄   분석 2 알맹이
src/LogScope.App/    54 파일 12830 줄   Shell/ Logs/ Db/ Settings/ Common/ Themes/
```

```
      LogScope.App
        /     |     \
   Logs     Db      |      Logs ↔ Db 사이에는 선이 없습니다
       \    /       |
       LogScope.Core
```

**경계를 사람이 아니라 컴파일러가 지킵니다.** `check_compile.sh` 가 Logs 와
Db 를 따로 컴파일하므로, 한쪽이 다른 쪽을 쓰기 시작하면 바로 "그런 타입
없다" 가 납니다.

### 하나 고치려면 몇 파일

| 고치려는 것 | 여는 곳 | 파일 |
|---|---|---|
| DB 화면 | `App/Db/` | **3** |
| DB 읽기·견주기·SQL | `LogScope.Db/` | 6 |
| 그래프·히트맵 화면 | `App/Logs/` | 17 |
| 설정 창 | `App/Settings/` | 4 |
| 색·컨트롤 모양 | `App/Themes/` | 5 |

### 자르려고 먼저 고친 것 — 의존 방향

`Settings → Compare` 가 거꾸로였습니다. 설정이 견주기를 보고 있어서
"바탕" 과 "분석 1" 을 가를 수가 없었습니다. 걸린 것은 셋뿐이었습니다.

| 옮긴 것 | 어디로 | 왜 |
|---|---|---|
| `ToleranceRule` · `ToleranceTable` | Compare → **Settings** | 허용 오차는 **설정**입니다. 견주기가 그걸 읽는 쪽입니다 |
| `DiffMetric` (차이량 잣대) | Compare → **Settings** | 저장되는 설정 값입니다 |
| `Json` | Settings → **Text** | DB 가 JSON 을 쓰는데 그게 "설정을 본다" 로 보였습니다 |

이 셋을 옮기자 방향이 한쪽으로만 흐릅니다.

### 너무 잘게 쪼개지 않으려고 안 한 것

- **Io 를 따로 떼지 않았습니다** (2200 줄). 떼면 Core 가 1900 줄로 줄지만
  프로젝트 하나가 늘고, 두 분석이 전부 Io 를 봅니다 — 떼서 얻는 것이
  없습니다.
- **그래프·히트맵·대시보드를 가르지 않았습니다.** 한 화면의 세 탭이고
  IO 목록·그룹을 함께 씁니다. 가르면 "그래프 고치려면 셋을 열어야"
  됩니다 — 지금 고치려는 것과 같은 문제입니다.
- **파일을 더 쪼개지 않았습니다.** `DbRead.cs` 는 1100 줄로 큰 편이지만
  0.74 에서 함수를 갈라 둔 뒤라 읽을 수 있습니다.

---

## 3) v1 · v2 이름을 바꾸는 곳

### **`src/LogScope.Db/DbNames.cs` — 이 파일, 이 두 줄**

```csharp
public const string V1 = "v1";      // ← 여기
public const string V2 = "v2";      // ← 여기
```

고치고 **다시 빌드**하면 끝입니다. 같이 바뀌는 곳:

- DB 화면 목록의 칸 머리글
- `[목록 CSV 저장]` 로 뽑은 CSV 의 머리글
- 줄에 마우스를 올리면 뜨는 설명 글

**다른 파일은 손대지 않습니다.** 시험에도 이름 글자가 하나도 없습니다 —
글자를 시험에 적어 두면 이름 바꿀 때 고칠 곳이 하나 더 생깁니다.

같은 파일에 나머지 아홉 칸 이름도 있습니다 (표 · IO명 · 이전 value ·
이후 value · 열 · 갈래 · 구분 · 행 · 설명).

---

## 4) 옮기다가 제가 낸 버그 셋 — 그리고 검사기 규칙 하나

파일을 다른 폴더로 옮기면 XAML 의 `clr-namespace` 가 어긋납니다. XAML 은
여기서 컴파일되지 않아서, 어긋나도 **Windows 에서 창을 띄울 때**서야
터집니다. 실제로 셋이 어긋났습니다.

| 어긋난 것 | 증상이 될 것이었던 것 |
|---|---|
| `Themes/Controls.xaml` 의 변환기 셋 | 테마 사전을 못 만들어 **창이 안 뜸** |
| `ShellWindow.xaml` 의 그래프·히트맵·대시보드 | 화면 셋이 **안 뜸** |
| `HeatmapView.xaml` 의 시간 맞추기 칸 | 그 칸이 **안 뜸** |

그래서 규칙을 더했습니다 (`check_sources.py`):

> **XAML 이 `<접두어:타입>` 으로 쓰는 우리 타입이 그 이름 공간에 있는가.**
> 어디에 무엇이 있는지는 소스에서 찾습니다 — 표로 적어 두면 그 표가 또
> 어긋납니다.

되돌려서 실제로 잡는 것을 봤습니다:

```
XAML 이 쓰는 inf:BoolToVisibility 가 LogScope.App.Themes 에 없음
XAML 이 쓰는 v:GraphView 가 LogScope.App.Shell 에 없음
```

그리고 `make_xaml_stubs.py` 의 **타입 → 이름 공간 표**(열 줄)도 같은
병이었습니다. 파일을 옮기자 바로 어긋났습니다. 소스에서 찾게 바꿨습니다.

---

## 확인한 것 · 못 한 것

- `통과 638 / 실패 0`, 검사기 넷 지적 없음
- 모듈 셋이 **따로** 컴파일됩니다 (Logs ↔ Db 사이 참조 없음이 컴파일로 증명)
- 새 검사 규칙은 되돌려 깨뜨려 잡는 것을 확인했습니다
- 시험 수가 643 → 638 로 줄었습니다. 없어진 다섯은 `db.col.*` 을
  시험하던 것들입니다 (그 기능이 없어졌습니다).
- **Windows 에서 확인 못 했습니다**:
  - 폴더를 옮긴 뒤 `.csproj` 로 **진짜 XAML 컴파일**이 되는지
    (`x:Class` · `DependentUpon` 은 맞춰 뒀고 검사기도 통과합니다)
  - 설정 창의 새 **[DB 도구]** 칸 모양
  - VS2017 에서 솔루션이 다섯 프로젝트로 열리는지
  - 처음 켤 때 `out` 폴더에 `LogScope.Logs.dll` · `LogScope.Db.dll` 이
    같이 복사되는지 (`build.bat` 에 넣었습니다)
