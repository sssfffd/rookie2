# 02 — 요구사항별 구현 위치

요구사항 번호 순서대로, 어디에 어떻게 넣었는지 적습니다.

## 1. 입력 데이터

| 요구 | 어디에 | 메모 |
|---|---|---|
| .xlsx 와 CSV 둘 다 | `Io/XlsxReader.cs`, `Io/CsvReader.cs` | 둘 다 `IRowSource` 로 맞춰 같은 코드가 처리 |
| 행이 IO / 열이 IO 자동 판별 | `Io/TableBuilder.Decide()` | 머리 행과 머리 열 중 **이름처럼 보이는 칸**이 많은 쪽을 고름. 시각이 "12:34:56" 처럼 글자로 들어와도 시간으로 읽히면 이름으로 세지 않음 |
| 수동 지정 | `OpenOptions.Orientation` | `Auto` / `Rows` / `Cols` |
| 표가 시트 중간에서 시작 | `Io/TableBuilder.Locate()` | 머리말 줄은 칸이 두어 개, 표 줄은 훨씬 넓다는 차이로 찾음 |
| IO 이름을 그대로 | `TableBuilder.BuildCols/BuildRows` | 머리 칸의 글자를 그대로 씀. 겹치면 뒤엣것에 `#2` |
| 파일 크기 상한 없음 | `OpenOptions` | 상한값 전부 기본 0 = 무제한 |
| 한 번 연 파일 다시 열기 | 위 메뉴 [다시 읽기] | 세트에 기억된 경로를 다시 읽음 |
| 시간이 바뀔 때만 적힌 경우 | `Io/TimeAxis.FillGaps()` | 앞뒤 시각 사이를 고르게 채움 |
| 같은 시각 반복 | `Io/TimeAxis.SpreadRepeats()` | 다음 시각까지 고르게 폄. 마지막 구간은 직전 간격을 씀 |
| 시각이 전부 같음 | `Io/TimeAxis.Build()` | **펴기 전에** 먼저 확인하고 샘플 번호로 바꿈 |
| 0 이 이어지는 구간 | 값에 손대지 않음 | 시간축만 펴고 값은 줄 수 그대로. 테스트 `ZeroRunSurvives` |
| 자정 넘김 | `TimeAxis.UnwrapMidnight()` | 되감기면 하루씩 더해 이어 붙임 |

## 2. 그래프

| 요구 | 어디에 |
|---|---|
| 레인 / 겹쳐보기 | `Controls/GraphCanvas.DrawLane()`, 위 도구줄의 [보기 방식] |
| 휠 확대가 과하지 않게 | `GraphCanvas.OnMouseWheel()` — 한 칸에 1.14 배 |
| 시간축을 옮겨도 세로 배율 유지 | `GraphCanvas.BaseRange()` — 기본은 채널 전체 범위 |
| "보이는 구간에 맞춤" | 도구줄 토글 → `FitVisible` |
| **값 축 확대 (Shift+휠)** | `GraphCanvas.ZoomValueAt()` — 커서가 가리키던 값이 제자리에 남도록 Center 를 다시 계산 |
| 세로 눈금 모드 3가지 | `ValueScaleMode.Raw / Normalized / Delta`. "변화만" 은 채널마다 첫 유효값을 뺌 |
| 눈금 숫자와 그래프 높이 일치 | 눈금과 선이 **같은 `ValueToY()`** 를 거침 |
| 커서 A/B + 값 표시 | 누르기 = A, Shift+누르기 = B. 값은 레인 머리줄과 아래 띠에 |
| 픽셀 열마다 최소/최대 | `Render/Decimator.cs` |
| 여는 시간 짧게 | 표를 통째로 복사/전치하지 않음 (`docs/ARCHITECTURE.md` 참고) |

## 3. IO 선택

| 요구 | 어디에 |
|---|---|
| 처음엔 아무것도 선택 안 됨 | `ChannelView` 의 선택 집합이 비어서 시작 |
| 전체 선택 / 전체 해제 | 왼쪽 위 버튼 |
| Shift 범위가 **보이는 순서**를 따름 | `ChannelView.SelectRangeByRow()` — 줄 번호로 훑음 |
| 다른 IO 를 눌러도 이전 선택이 안 꺼짐 | `ChannelView.Toggle()` — 같은 것을 다시 눌러야 꺼짐 |
| 검색창이 깜빡이지 않음 | 검색창이 진짜 `TextBox` 라 그래프를 다시 그려도 영향 없음 |
| 타입 필터 | 왼쪽 위 콤보 (전체/디지털/아날로그/상태) |
| 고른 IO만 보기 | 왼쪽 위 콤보 (모든 IO / 고른 IO만 / 달라진 IO만) |
| 그리는 순서 = 목록 순서 | `ChannelView.DrawOrder()` 가 보이는 줄을 그대로 훑음 |

## 4. 그룹

| 요구 | 어디에 |
|---|---|
| 10개 단위 기본 그룹 | `ChannelView.MakeDefaultGroups(10)` — 저장된 그룹이 없을 때만 |
| 접기/펴기 | 그룹 이름 줄을 누르면 토글 |
| 이름 바꾸기 | 아래 [이름 바꾸기] 버튼, 오른쪽 단추 메뉴 |
| 끌어다 넣기 | IO 를 그룹 이름 줄로 드래그 |
| 검색으로 골라 넣기 | 검색 → 전체 선택 → [고른 IO를 이 그룹에 담기] |
| 그룹 순서 바꾸기 | [위로 옮기기] / [아래로 옮기기] |
| 껐다 켜도 유지 | `AppSettings.Groups` → 설정 파일. **구성원을 IO 이름으로 저장** |
| 그룹 전체 선택 체크박스 | 그룹 이름 줄 왼쪽 |
| 버튼에 글자 | 기호만 있는 버튼을 안 만들었습니다 (`Controls/Buttons.cs` 주석 참고) |

## 5. 두 로그 비교

| 요구 | 어디에 |
|---|---|
| 이전/이후 색 구분 | `Theme.Before` = 파랑, `Theme.After` = 주황 |
| 차이 구간 음영 | `GraphCanvas.ShadeGap()`. **점선은 쓰지 않았습니다** |
| 벌려 그리기 | 도구줄 [이전/이후 벌려 그리기] |
| align 자동 + 수동 | `DiffOptions.AutoAlign` / `Shift`. 설정 창 [비교] 탭 |
| IO 존재 차이 | 대시보드 아래 두 칸: 이전에만 있는 IO / 이후에만 있는 IO. 그래프에서는 이름 옆에 "(이전에만)" |
| 차이량 7가지 | 최대 / 평균 / RMS / 면적 / 시간비율 / 표본수 / 구간수 (`Compare/DiffTypes.cs`) |
| 허용 오차 | 도구줄과 설정 창 양쪽에서 조절 |
| 달라진 IO만 보기 | 왼쪽 콤보의 "달라진 IO만" |

**+1/-1 흔들림 문제**: 테스트 `CompareMetrics` 가 정확히 이 경우를 확인합니다.
한 번 크게 벌어진 채널과 계속 흔들리는 채널이 **최대 차이는 똑같이 1** 인데,
구간 수는 1 대 50, 시간 비율은 0.2 대 1.0 으로 갈립니다.

## 6. 설정 창

| 요구 | 어디에 |
|---|---|
| 오른쪽 위 설정 버튼 | 위 메뉴 줄 맨 오른쪽 [설정] |
| 버전 확인 | [정보] 탭 — 버전 + **커밋 해시** + 빌드 시각 + 32/64비트 |
| 로그 세트 6개 | [로그 세트] 탭. 위 메뉴의 콤보로 전환 |
| 세트마다 기본 폴더 (이전/이후 따로) | [로그 세트] 탭의 두 칸 |
| 설정 저장 | `Settings/SettingsStore.cs` |

## 7. UI 일반

| 요구 | 어디에 |
|---|---|
| 좁아지면 줄바꿈, 버튼이 안 튀어나옴 | `Controls/FlowBar.cs` (`WrapContents` + `AutoSize`) |
| 버튼에 글자 | 전부 글자 |
| 진행 표시 + 취소 + 배경 스레드 | `Dialogs/LoadingDialog.cs` |
| 다크/라이트 테마 | `Theme.cs`. 설정 창에서 전환 (다시 켜야 전체 적용) |
| 모니터별 DPI | `app.manifest` 의 PerMonitorV2 + `MainForm.WndProc` 의 `WM_DPICHANGED`. 직접 그리는 컨트롤은 전부 `Dpi.S()` 를 거침 |

## 8. 작업 방식

- 대화 기록: 이 폴더 (`claude-log/`)
- 버전: `version.txt` **한 곳**. `tools/BuildInfo.targets` 가 빌드할 때
  `BuildInfo.g.cs` 를 만들면서 git 해시를 함께 넣습니다.

## 9. 화면 (이번 추가분)

| 요구 | 어디에 |
|---|---|
| 흰 바탕 + 진한 파랑 메뉴 | `Theme.Light()` 의 `Window` = 흰색, `TopBar` = `#103562` |
| 대시보드가 먼저 | `MainForm` 이 `DashboardView` 로 시작 |
| 네모칸 요약 | `DashboardView.Tile` 4개 — 이전 IO 개수(이름이 이후에 없는 것 n개), 이후 IO 개수(이름이 이전에 없는 것 n개), 차이 발생 IO 개수, 표본 수 |
| 큰 네모칸에 차이 난 IO 목록 | `DashboardView._changed` |
| IO 누르면 그래프로 | `DashboardView.IoActivated` → `GraphView.FocusOnIo(name, exclusive: true)` |
| 메뉴로도 이동 | 위 메뉴 [대시보드] / [그래프] (F1 / F2) |
| 그래프에서 직접 고르기 | 왼쪽 `ChannelPanel` (목록 + 그룹 + 검색 + 필터) |

## 아직 확인 못 한 것

리눅스 컨테이너에서 작성했기 때문에 **윈도우에서 컴파일해 본 적이 없습니다.**
`build.bat` 에서 오류가 나면 메시지를 알려 주세요.
