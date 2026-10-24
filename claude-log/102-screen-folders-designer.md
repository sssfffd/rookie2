# 102 — 화면 기준 폴더 · VS 디자이너로 보기 · 그룹 칸 낮게

> 그리고
> 분석 1 그래프 화면 좌측 아래에 그룹 수정 칸 세로로 조금 줄여줘
> 전체 프로젝트가 xaml 파일들 추가해서 visual studio에서 화면 보고 수정할 수 있게 되었으면 좋겠음
> 그리고 지금 core .app이렇게 되어있는 거 말고 화면 기준으로 파일 구조들 바뀌면 좋겠음 개발자가 알 기 쉽게

## 1. 화면 기준 폴더

### 화면 쪽 — `src/LogScope.App/`

```
전 (기능 · 층)                          후 (화면)
App/Shell/   창 틀 + 메인 + 저장 창       App/Shell/                 창 틀 · 맨 위 메뉴 줄
App/Logs/    분석 1 화면 전부 (25 파일)   App/Main/                  메인 화면 · 저장된 분석 창
App/Db/      분석 2 화면                  App/Analysis1/Dashboard/   대시보드
App/Texts/   화면 글자 10 개              App/Analysis1/Graph/       그래프 (IO 목록 · 그룹 · 단계)
App/Settings/                             App/Analysis1/Heatmap/     히트맵
App/Common/                               App/Analysis1/Align/       시간 맞추기 판
App/Themes/                               App/Analysis2/             분석 2 (DB)
                                          App/Analysis3/             분석 3 (준비 중)
                                          App/Settings/ · Common/ · Themes/
```

- 화면 폴더 하나에 그 화면의 **XAML · 코드 · 뷰모델 · 글자(`…Texts.cs`)** 가 다 있습니다.
  1.18 에 만든 `Texts/` 의 글자 파일 10 개도 각 화면 폴더로 들어갔습니다.
- 지도: `src/LogScope.App/README.md` (화면 → 폴더 → XAML → 글자 파일).

### 계산 쪽 — 프로젝트 이름도 화면 이름으로

| 1.21 까지 | 1.22 | 무엇 |
|---|---|---|
| `LogScope.Logs` | **`LogScope.Analysis1`** | 분석 1 계산 (견주기 · 히트맵 · 시간 맞추기 · 단계 · 기록) |
| `LogScope.Db` | **`LogScope.Analysis2`** | 분석 2 계산 (DB 읽기 · 견주기 · 판정 · SQL) |
| `LogScope.Core` | (그대로) | 공통 (로그 읽기 · 자료 · 설정) |
| `LogScope.App` | (그대로) | 화면 — 위 폴더들 |

프로젝트(DLL)를 하나로 합치지 않은 까닭: 계산은 WPF 없이 돌아서 시험을 어디서나 돌릴 수
있고, 분석 1 계산과 분석 2 계산이 서로를 모르게(따로 컴파일) 해 둔 것을 지키려고입니다.
대신 **이름이 화면을 가리키게** 했습니다.

바뀐 이름이 들어가는 곳을 다 고쳤습니다: `.sln` · `.csproj` (경로 · AssemblyName ·
RootNamespace · 참조) · `CMakeLists.txt` · `build.bat` (DLL 복사 두 줄, CP949 · CRLF 그대로) ·
`scripts/check_compile.sh` · 이름 공간 전부 · XAML 의 `clr-namespace` / `assembly=` · 문서.
`out` 폴더에 복사되는 DLL 이름이 `LogScope.Analysis1.dll` · `LogScope.Analysis2.dll` 로
바뀝니다 (예전 `out` 폴더의 `LogScope.Logs.dll` · `LogScope.Db.dll` 은 지워도 됩니다).

## 2. Visual Studio 디자이너로 화면 보기

XAML 은 원래 다 프로젝트에 들어 있었고(.csproj 의 Page), 이번에 **디자이너에서 볼 때만**
쓰는 것을 넣었습니다.

- 화면마다 `mc:Ignorable="d"` · `d:DesignWidth="1200" d:DesignHeight="760"` — 디자이너가
  쓸 크기.
- `d:DataContext="{x:Static common:DesignData.Graph}"` — 견본 뷰모델(`Common/DesignData.cs`).
  바인딩된 단추 · 목록이 디자이너에서도 제자리에 그려지고, XAML 을 쓸 때 바인딩 이름이
  자동 완성됩니다. 만들다 터지면 null 이라 디자이너가 빈 화면으로 보일 뿐입니다.
- `d:` 는 디자이너만 읽습니다 — 실행에는 영향 없음.

쓰는 법: `LogScope.sln` 열기 → **한 번 빌드** → 화면 폴더의 `.xaml` 두 번 누르기.
그래프 · 히트맵 그림은 코드가 그리는 판이라 디자이너에서는 빈 판입니다.
글자는 디자이너 속성 창이 아니라 `…Texts.cs` 에서 고쳐 주세요 (속성 창에서 고치면
`{x:Static …}` 이 지워지고 글자가 XAML 에 박힙니다).

## 3. 그래프 왼쪽 아래 그룹 칸 — 낮게

```
전                                       후
고른 그룹: 온도                            고른 그룹: 온도
[새 그룹 만들기][이름 바꾸기][위로 옮기기]     [새 그룹 만들기][이름 바꾸기][▲][▼]
[아래로 옮기기][고른 IO를 이 그룹에 담기]      [고른 IO 담기][그룹 삭제][groups.txt 글 복사]
[그룹 삭제][groups.txt 글 복사]
```

- 그 칸 안의 단추만 작게: 글자 11 · 높이 22 · 여백 좁게.
- [위로 옮기기] [아래로 옮기기] → ▲ ▼ (이름은 풍선말), [고른 IO를 이 그룹에 담기] → [고른 IO 담기].
  글자는 `Analysis1/Graph/GraphTexts.cs`.

## 검사기에 하나 더

`check_sources.py`: XAML 이 **없는 이름 공간**을 가리키면 (안 쓰는 접두어라도) 알립니다 —
옮기다 `DbView.xaml` 에 옛 `LogScope.App.Db` 가 하나 남아 있었습니다. 일부러 틀려 보고
잡는 것을 확인했습니다.

## Windows 에서 확인 못 한 것

- **파일 45 개를 옮기고 프로젝트 이름 둘을 바꿨습니다.** 여기서는 C# 컴파일 · 시험 872 ·
  XAML 이름 공간 · x:Static · csproj 목록 · 폴더=이름 공간 검사가 다 통과했지만, XAML 컴파일과
  VS 솔루션 열기는 Windows 에서만 됩니다. **첫 빌드에서 오류가 나면 그 줄을 알려 주세요.**
- VS 가 예전 `obj` 폴더를 들고 있으면 이상한 오류가 날 수 있습니다 — 그때는
  "솔루션 정리" 뒤 다시 빌드.
- 디자이너 화면 모양 (견본 뷰모델이 디자이너 안에서 잘 만들어지는지).
