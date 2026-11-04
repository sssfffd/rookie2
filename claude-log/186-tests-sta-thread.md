# 186 — 1.91 빌드 시험 "The calling thread must be STA"

## 요청 (요청 213)

> the calling thread must be sta 라는 빌드 에러 뜨는데 왜 이럼? 분석 1 쪽인 듯, 아마 test?

## 원인

WPF 화면 부품 (`FrameworkElement` — 그래프 그림판 등) 은 **STA 스레드**에서만 만들 수 있습니다. 1.87 에 넣은 시험
`GalleryOneZoomRules` (`src/LogScope.Tests/Program.cs`) 가 `new GraphCanvas()` 로 그림판을 직접 만드는데, 시험 프로그램의
`Main` 에 `[STAThread]` 가 없었습니다. `build.bat` 이 빌드 뒤 시험을 돌리다 그 자리에서 멈춘 것입니다.

여기 (리눅스) 의 시험은 **흉내 WPF** (`scripts/wpfstub`) 로 돌아 스레드를 따지지 않아서 통과했습니다 — 그래서 못 봤습니다.

## 고친 것

| 무엇 | 파일 |
|---|---|
| 시험 `Main` 에 `[STAThread]` | `src/LogScope.Tests/Program.cs` |
| 점검: 시험이 WPF 부품 (`…View` · `…Window` · `…Canvas`) 을 만드는데 `[STAThread]` 가 없으면 지적 — 빼 보고 잡히는 것 확인 | `scripts/check_sources.py` |

프로그램 자체 (LogScope.exe) 는 원래 STA 입니다 (WPF `App` 이 그렇게 만듦) — 바뀐 것 없음.

## 확인

- 시험 1072 통과, check_sources (새 점검) 통과 · 빼면 지적.
- **Windows 에서 못 본 것**: 실제 `build.bat` 시험 단계 — 다시 돌려 보시고, 다른 시험이 또 멈추면 그 줄을 알려 주세요.
