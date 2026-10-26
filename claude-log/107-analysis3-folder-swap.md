# 107 — 분석 3 폴더를 통째로 갈아 끼워 빌드하기

> 내가 코드 따로 작성해서 analysis3 폴더안에 visual studio 활용해서 넣었는데
> 문제는 여기서 수정할때마다 새로 추가를 해야되잖아
>
> 그냥 폴더를 복사하고 백업해놔서 여기서 코드 수정하면 다운받아서 해당 폴더만 다시 넣어가지고
> 빌드되게 하고싶단 말임? 어떻게 하는게 좋을까?

## 왜 매번 다시 추가해야 했나

옛 방식 csproj 는 빌드할 파일을 **한 줄씩 적어 둡니다**. VS 에서 "기존 항목 추가" 를 하면 사내 PC 의
csproj 에만 줄이 생기고, 저장소를 새로 받으면 csproj 가 새것으로 바뀌어 그 줄이 사라졌습니다.

## 바꾼 것 (1.27)

| | 전 | 후 |
|---|---|---|
| Analysis3 파일 목록 | csproj 에 한 줄씩 | **`tools/Analysis3.targets` 가 폴더에서 찾음** (`Analysis3\**\*.cs` · `*.xaml` · 그림) |
| 창이 띄우는 이름 | `a3:PlaceholderView Heading=…` | **`a3:Analysis3View`** — 속성 없이 |
| 자리 화면 | `PlaceholderView` | `Analysis3View` (이름만 바뀜, 제목은 config.txt 에서 직접 읽음) |
| 보이기 여부 | 창 뷰모델을 그냥 `{Binding}` | DbView 처럼 창에서 찾음 — 갈아 끼운 화면이 DataContext 를 바꿔도 메인 위에 덮이지 않게 |
| 검사 | — | `check_sources.py`: targets 를 csproj 맨 끝에서 가져오나 · `Analysis3View` 가 있나 |

```
새로 받은 저장소                     백업
src\LogScope\Analysis3\  ◀── 지우고 ── D:\백업\Analysis3\   (Analysis3View 꼭 포함)
            │
            └─ build.bat — csproj 를 고칠 일 없음
```

VS 에서 Analysis3 에 파일을 추가하면 VS 가 csproj 에 줄을 적지만, targets 가 그 줄을 먼저 지우고
폴더에서 다시 넣으므로 두 번 들어가지 않습니다 (`Remove` — VS2017 의 MSBuild 15 부터).

## 지킬 것

- 폴더 안에 `LogScope.Analysis3.Analysis3View` (UserControl) — 이름이 다르면 바꾸거나 감싸는 화면 하나.
- Analysis3 **밖**은 사내에서 고치지 않기 (새로 받으면 사라짐) — 밖이 필요하면 요청.
- 메인 화면 분석 3 칸에 결과 보이기는 아직 안 함 — 필요하면 폴더 밖에 약속(인터페이스)을 두고 연결.

## 여기서 확인 못 한 것

- Windows 의 진짜 MSBuild / VS2017 에서의 동작 (와일드카드 · `Remove`) — 리눅스에는 MSBuild 15 가 없어
  돌려 보지 못했습니다. `build.bat` 결과와, VS 솔루션 탐색기에 Analysis3 파일이 보이는지 알려 주세요.
- 솔루션 탐색기에서 `X.xaml.cs` 가 `X.xaml` 아래로 접혀 보이지 않고 나란히 보일 수 있습니다 (보기만 다름).

자세한 순서: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4 장.
