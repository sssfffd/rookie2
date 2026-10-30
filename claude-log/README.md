# 작업 기록

폰으로 프롬프트를 넣고 PC 에서 git 으로 본다고 하셔서, 주고받은 내용을
여기에 파일로 남깁니다. 번호 순서대로 읽으면 무엇을 왜 그렇게 정했는지
따라올 수 있습니다.

| 파일 | 내용 |
|---|---|
| `00-user-messages.md` | **보내신 요청 원문** (고치지 않고 그대로) |
| `01-language-and-structure.md` | 언어와 구조를 무엇으로 정했는지, 후보와 위험 |
| `02-what-was-built.md` | 요구사항 항목별로 어디에 구현했는지 |
| `03-wpf-rewrite.md` | WinForms 에서 WPF 로 바꾸면서 달라진 것 |
| `04-heatmap.md` | 히트맵 화면 — 지표를 왜 "절대 차이의 평균" 으로 골랐는지 |
| `05-tolerance.md` | 허용 오차 — 왜 절대값이 아니라 값 범위의 % 인지 |
| `06-graph-and-sorting.md` | 확대하면 선이 사라지던 버그, 화질, 제목 클릭 정렬 |
| `07-units-and-percent.md` | 세로축 단위와 상태 이름 눈금, 히트맵의 값/% 고르기 |
| `08-heatmap-tolerance-mismatch.md` | 허용 오차와 칸의 숫자가 왜 어긋났는지, 어떻게 맞췄는지 |
| `09-axis-fit-and-toolbar.md` | 세로축 자동 맞춤, 지수 표기 없애기, 도구 줄 갈래 나누기 |
| `10-heatmap-row-order.md` | 칸 폭을 바꾸면 IO 차례가 바뀌던 버그 |
| `11-build-bat-encoding.md` | chcp 65001 이 왜 틀린 방법이었는지, CP949 로 바꾼 이야기 |
| `12-tolerance-base.md` | 1% 의 밑값이 잘못돼 있던 것, 세로 눈금 세 모드가 같아 보이던 이유 |
| `13-shadowing-check.md` | 제가 낸 컴파일 오류와, 그걸 잡으려고 만든 검사 |
| `14-toolbar-reflow.md` | 안내 글 때문에 도구 줄이 접혔다 펴졌다 하던 것 |
| `15-oscillating-channel-perf.md` | 값이 자주 바뀌는 IO 에서 그리기가 굼뜨던 세 가지 원인 |
| `16-stale-checkout.md` | 이미 고친 오류가 계속 보이던 이유와, 판본을 가리는 방법 |
| `17-relative-to-before.md` | 오차를 "이전 값 대비 %" 로 통일 — 설정한 %와 화면의 %를 같게 |
| `18-real-compile-check.md` | 매번 다른 오류가 나던 진짜 이유 — 이제 정말로 컴파일합니다 |
| `19-trigger-align-and-axis.md` | 특정 IO 의 변화로 시간축 맞추기, 다른 IO 를 가로축으로 |
| `20-console-codepage.md` | 빌드 중간부터 한글이 사라지던 이유 — 코드 페이지는 창의 것입니다 |
| `21-toolbar-and-align-rework.md` | 도구 줄 3 줄 상한, 값 단위 시간 맞추기, 가로축 재점검 |
| `22-delta-is-difference.md` | 0–1 정규화 제거 (변화량 부분은 23 에서 바로잡음) |
| `23-difference-of-two-logs.md` | 변화량 = 두 로그의 차이. 격자가 다른 두 로그를 합쳐 훑기 |
| `24-toolbar-labels.md` | 도구 줄 이름 정리, 차이 영역 표시와 파형 분리 보기는 하나만 |
| `25-lane-center-clamp.md` | 값이 0 으로 붙박이던 것, Lane/Overlay 이름, 두 맞춤의 차이 |
| `26-four-screens.md` | 화면을 넷으로. 메인 화면과 분석 1·2·3 |
| `27-main-screen-layout.md` | 메인 화면 — 가로로 긴 칸 셋, ??/100, 로그 요약 |
| `28-analysis-history.md` | 분석 결과 저장. 지난번과 견주기, 기준이 다르면 빼지 않기 |
| `29-card-summary-and-groups.md` | 칸마다 그룹별 요약, 이동 화살표, 그룹 → 히트맵 추리기 |
| `30-wording-and-drag-drop.md` | 말 다듬기, Log 파일 끌어다 놓기, 프로그램 이름은 appname.txt |
| `31-drop-zones.md` | 끌어다 놓을 때 화면을 반으로 갈라 이전/이후를 고르게 |
| `32-two-log-cards.md` | 열어 둔 로그를 좌우 두 칸으로. 31 의 큰 안내 판은 없앰 |
| `33-screen-names.md` | 분석 1·2·3 의 이름을 screens.txt 에서 바꾸기 |
| `34-no-auto-reopen.md` | 켤 때 지난번 로그를 저절로 열지 않기 |
| `35-clearvalue-erased-the-card.md` | 파일을 놓으면 칸이 사라지던 것. ClearValue 가 XAML 값까지 지움 |
| `36-per-io-tolerance.md` | IO 별 허용 오차. 퍼센트와 값 둘 다, 빈 칸은 기본값 |
| `37-dashboard-columns.md` | 대시보드에서 RMS · 구간 수 칸 빼기 |
| `38-top-bar-rework.md` | 위 줄 두 칸으로. 화면 단추 넷, 로그 세트와 다시 읽기 제거 |
| `39-graph-legend-and-band-zoom.md` | 그래프 이전/이후 범례, 오른쪽 끌기로 네모 확대 |
| `40-show-one-side.md` | 이전 선 / 이후 선 하나씩 끄고 보기 |
| `41-one-config-file.md` | appname.txt + screens.txt 를 config.txt 한 파일로 |
| `42-two-row-toolbar.md` | 도구 줄 두 줄, [그릴 로그] 를 왼쪽 IO 칸으로 |
| `43-db-analysis.md` | 분석 2 = DB 분석. .sql/.csv 읽기, 세 단계 비교, 맞추는 SQL 생성 |
| `44-dbview-covered-the-screen.md` | DB 화면이 메인 위에 덮여 있던 것. 바인딩 실패 = Visible |
| `45-diff-list.md` | DB 차이를 칸으로 쪼갠 목록. 한 줄에 값 하나, CSV 로 내보내기 |
| `46-where-mysql-keeps-data.md` | MySQL 이 데이터를 담고 있는 곳. .frm/.ibd 를 BCL 로 읽는 이야기 |
| `47-frm-reader.md` | 5.6 의 .frm 을 직접 읽기. "없음" 과 "모름" 을 가르는 이유 |
| `48-reading-values.md` | 값은 서버를 한 번 거쳐야 하는 이유, 네 가지 길, 표 정의 저장 |
| `49-overall-result-panel.md` | 메인 맨 위를 종합 분석 결과로. 로그 칸은 분석 1 안으로 |
| `50-blue-before-red-after.md` | 이전 파랑 · 이후 빨강, 늘 같은 자리의 범례, config 의 긴 이름 |
| `51-groups-were-not-saved.md` | 그룹이 바로 저장되지 않던 것. 아무도 듣지 않는 이벤트 검사 |
| `52-db-paths-and-list.md` | DB 자리를 화면에서 고르기, 목록 칸 바꾸기, 파싱 파일 위치 |
| `53-groups-txt.md` | 그룹을 빌드 전에 정해 두는 groups.txt. 긴 이름 폭 가두기 |
| `54-topbar-right-align.md` | 그 화면 메뉴가 창 끝으로 밀려 있던 것 (0.70 에서 제가 낸 것) |
| `55-hard-to-read.md` | "사람이 보기 힘들다" — 재 본 숫자와 고친 것, 남은 선택 |
| `56-one-place-for-names.md` | 표시명 하나에 네 파일 — 이름을 한 곳(DbNames)으로, config.txt 로 바뀜 |
| `57-glue-strings.md` | 같은 눈으로 나머지를 훑음 — 글자 열쇠 다섯, 트리거 18, 검사기 규칙 둘 |
| `58-modules.md` | DB 설정을 config.txt 에서 빼냄 · 다섯 프로젝트로 모듈화 · v1·v2 자리 |
| `59-vm-prefix.md` | `vm:` 빌드 에러 — 접두어 선언을 보지 않던 검사기를 고침 |
| `60-garbage-values.md` | 목록 칸 둘 지움 · CP949 덤프와 16진수 값이 쓰레기로 보이던 까닭 |
| `61-sql-pane-out-dragdrop.md` | 맞추는 SQL 창 없앰 · TextBox 가 파일 드롭을 먹던 것 · 분석 2 코드 자리 |
| `62-drop-cards.md` | 끌어다 놓기를 메인 화면 로그 칸과 같은 모양으로 · 없는 자리 알려 주기 |
| `63-headline-colors-filter.md` | 세 숫자로 바꿈 · 줄 색(빨강·노랑) · 차이 난 IO만 보기 · SQL 문 출력 창 |
| `64-vs-vs-batch.md` | VS 는 성공 배치는 실패하는 까닭 · v1 채우기를 막던 시험 · 테스트가 왜 있나 |
| `65-tests-dont-block.md` | 테스트 실패가 build.bat 을 멈추지 않게 (알리고 계속) |
| `66-fail-summary.md` | 실패한 시험만 맨 밑에 모아 찍기 (묶음 이름과 함께) |
| `67-pair-different-names.md` | .csv 파일 이름이 달라도 열 구성이 같으면 짝지음 · CSV 에 열쇠가 없는 문제 |
| `68-csv-3-and-4.md` | v1·v2 와 표 걷어냄 · .csv 는 3 번 열쇠 4 번 값 · ON 이 TRUE 로 바뀌던 버그 |
| `69-db-slim.md` | DB 쪽에서 안 쓰는 길 걷어냄 (되돌리는 방향 · DbCreateSql) · 거짓이 된 안내 글 |
| `70-db-pass-fail.md` | 메인 화면 DB 칸을 점수에서 PASS·WARNING·FAIL 로 · 안 읽어 본 것과 0 개 |
| `71-main-db-drop.md` | 메인 화면 분석 2 칸에도 견줄 자리 · 놓으면 바로 읽기 · 경로 끊기 |
| `72-steps.md` | 단계(Step) — 히트맵 가로 · 그래프 단계 고르기 · 분석 1 파일 나누기 |
| `73-axis-out-group-to-graph.md` | 가로축 바꾸기 걷어냄 · 그룹은 그래프로 · DB 판정 글귀 |
| `74-step-split.md` | 그래프를 단계 칸으로 나눠 그리기 (칸마다 제 밀기 값) |
| `75-db-summary-on-main.md` | 분석 2 요약을 메인 화면 요약 칸에 · 단계 나눠 보기 확인 |
| `76-step-bands-gap-zoom.md` | 칸 띄우기 · 칸마다 확대 · 단추 이름과 설명 줄 |
| `77-heatmap-per-step-shift.md` | 히트맵이 칸마다 그 단계의 밀기 값으로 견주게 (오차 못 잡던 것) |
| `78-db-show-all-ios.md` | "모든 IO 보기" 가 되게 (같은 행도 담기) · 요약 칸은 달라진 IO 목록 |
| `79-step-now-and-vertical-cards.md` | 단계 IO 바로 적용 · 로그 칸 위아래로 · 칸 밖 휠 · 커서에 단계 경과 |
| `80-card-cleanup-and-click.md` | 분석 2 칸의 군글자 빼기 · 칸을 누르면 파일 고르기 |
| `81-one-band-only.md` | 나눠 보다가 칸 하나만 크게 (제가 잠가 놨던 자리) |
| `82-card-footer-and-big-count.md` | 파일은 카드 바닥글로 · 분석 2 차이 개수를 크게 · 테두리가 남는 고장 막기 |
| `83-count-beside-and-two-look-slots.md` | 차이 개수는 판정 오른쪽 · 파일 칸은 제목 아래, 열기 전 상자 / 연 뒤 한 줄 |
| `84-score-first-read-on-pick-db-values.md` | 점수가 파일 칸 위 · 분석 2 칸을 눌러 골라도 바로 읽기 · 요약에 이전 → 이후 값 |
| `85-db-screen-quiet.md` | 분석 2 화면의 판정·개수·줄 수 글 걷어 내기 (알림은 ⚠ 하나) · 맨 아래 이전/이후 줄은 분석 1 에서만 |
| `86-main-align-and-db-labels.md` | 점수 아래 "이상 없음" 빼기 · 점수 24pt + 자리 높이 고정으로 분석 1·2 파일 줄 맞춤 · [데이터 읽기] · [.CSV로 저장] |
| `87-heatmap-groups-and-search.md` | 히트맵에 검색 칸 · 그래프와 같은 그룹으로 줄 나누기 (그룹 줄 눌러 접기) |
| `88-db-delta-red.md` | 분석 2 목록에 빨간 차이 "(+3)" · "(a → b)" · 메인 요약 값은 빨간 색만 |
| `89-score-30-files-up.md` | 분석 1 점수 · 분석 2 낱말 · 개수 모두 30pt · 점수 아래 빈 줄 없애 이전/이후 위로 |
| `90-heatmap-search-above-list.md` | 히트맵 검색 칸을 IO 이름 칸 바로 위로 · 색 설명 옆 쓰는 법 글 빼기 |
| `91-db-view-two-buttons.md` | 분석 2 보기 단추를 [차이 발생] · [전체] 둘로 ([값이 달라진 것만] 뺌) |
| `92-db-same-rows-green.md` | 분석 2 [전체] 에서 두 쪽이 같은 줄은 옅은 초록 |
| `93-step-label-no-number.md` | 단계 이름 앞 순번 "1." 빼기 (같은 이름 둘째부터 "(2)") |
| `94-db-list-card.md` | 분석 2 목록을 카드로 띄우고 칸 폭을 창에 맞춰 40 · 30 · 30 으로 |
| `95-graph-cleanup-step-buttons.md` | Delete 로 커서 지우기 · 그래프 아래 설명 · 맨 아래 파일 줄 빼기 · [정렬][단계마다][전체 구간] · StepText.cs |
| `96-step-pick-and-head.md` | 단계 목록 [전체 구간 ▼] 되살림 · 칸 머리 눌러 그 단계만 · 단계 이름을 그림 위로 · 칸마다 맞추는 법 |
| `97-db-search-heatmap-instant.md` | 분석 2 IO 검색 (검색 규칙 한 곳 NameSearch) · 히트맵 [차이 없는 IO도 보기] 바로 반영 |
| `98-step-length-and-screen-texts.md` | 단계 길이가 다르면 짧은 쪽 선을 제 끝에서 끊기 · 화면마다 글자 파일 하나 (`App/Texts/`) |
| `99-step-tail-readout.md` | 단계 길이가 다를 때 짧은 쪽은 끝에서 없음 — 그래프(1.18) 와 커서 값(1.19) |
| `100-graph-labels-two-rows.md` | 그래프: 빈 화면 문구 · 타입 빼기 · 전체 IO / 차이 발생 IO · Value / Difference · 도구 줄 두 줄 · 구간 맞춤 설명 |
| `101-card1-count-and-history-plan.md` | 분석 1 카드 개수를 점수 오른쪽 · 한쪽에만 있는 IO 빼기 · 분석 이력 제안 |
| `102-screen-folders-designer.md` | 화면 기준 폴더 (App/Main · Analysis1/… · Analysis2) · 프로젝트 이름 Analysis1 · Analysis2 · VS 디자이너 견본 · 그룹 칸 낮게 |
| `103-one-project.md` | 프로젝트 넷을 `src/LogScope` 하나로 — 화면 폴더에 그 화면의 모양 · 코드 · 글자 · 계산 전부 |
| `104-build-fix-buildinfo.md` | 1.23 빌드 오류: BuildInfo 가 XAML 임시 빌드에서 빠짐 → 빌드 맨 처음에 만들고 csproj 에 늘 적어 둠 · C# 7.3 컴파일러로 재확인 |
| `105-buildinfo-plain-class.md` | "BuildInfo 가 없다" → 만들어 끼우던 BuildInfo.g.cs 대신 평범한 BuildInfo.cs + 리소스(version.txt) |
| `106-analysis3-porting-guide.md` | 분석 3 Python 프로그램을 사내 AI 로 C# 으로 — 올릴 파일 · 규칙 · 요청문 · 붙이는 자리 (`docs/ANALYSIS3-PORTING.md`) |
| `107-analysis3-folder-swap.md` | 분석 3 폴더를 통째로 갈아 끼워 빌드 — 파일 목록은 `tools/Analysis3.targets` 가 폴더에서 · 진입 화면 이름 `Analysis3View` |
| `108-analysis3-connect-view.md` | GraphCanvas.Trace.cs 는 지우면 안 됨 (선 그리는 코드) · 내 화면을 `Analysis3View` 에 연결하는 법 (이름 바꾸기 / 감싸기 / Window → UserControl) |
| `109-analysis3-main-card.md` | 메인 화면 분석 3 칸에 이전 · 이후 자리 + 분석 3 결과 요약 — 폴더 밖 게시판 `Common/Analysis3Link` 로 주고받음 |
| `110-analysis3-not-in-repo.md` | Analysis3 폴더를 저장소에서 빼서(.gitignore) 새 버전을 덮어 풀어도 사용자 파일이 남게 · 창은 Analysis3View 를 이름으로 찾음 |
| `111-analysis3-where-publish.md` | 요약 칸에 넣는 코드는 내 [분석] 단추 처리에서 결과를 화면에 보인 바로 아래 — 넣는 모양과 옮기는 표 |
| `112-analysis3-folder-error.md` | 분석 3 폴더 열기 오류 — 오류 창에 종류 · 속 오류 · 진짜 자리를 적게 하고, 흔한 까닭 표 |
| `113-analysis3-attach.md` | 창이 분석 3 화면을 만든 뒤 Attach(AppState) / Attach() 를 불러 줌 — 사용자가 ShellWindow 를 직접 고친 것을 저장소에 넣음 |
| `114-attach-in-shell-only.md` | Attach 부르기를 ShellWindow.xaml.cs 하나에만 — 1.32 에서 그 파일 하나만 바꿔 넣으면 됨 |
| `115-a3-folder-pick-buttons.md` | 분석 3 칸: 누르면 폴더 고르기 · 칸 머리에 [분석] [리포트] (Analysis3Link 이벤트로 연결) |
| `116-a3-card-result-blank.md` | 메인 화면 분석 3 칸의 결과 자리 당분간 비움 (`MainVm.A3ResultOnCard`) |
| `117-a3-main-code-map.md` | 분석 3 → 메인 화면: 자리 · 요약을 받고 띄우는 코드의 파일 · 줄 지도 |
| `118-a3-card-result-on.md` | 분석 3 결과를 메인 칸에 다시 띄움 (`A3ResultOnCard = true`), 결과 전에는 빈 칸 |
| `119-a3-headline-off-paths.md` | 분석 3 칸 머리 줄(낱말 · 차이 발생 N 개 · 작은 글) 지우고 요약 줄만 · 이전/이후 폴더 칸 연결하는 두 줄 |
| `120-a3-analyze-vs-report.md` | 분석 3 칸 [분석] · [리포트] 차이 — 둘 다 신호만, 하는 일은 분석 3 코드가 연결 |
| `121-a3-report-button-removed.md` | 분석 3 칸의 [리포트] 단추 뺌 ([분석] 만) |
| `122-a3-line-wrap-tag-color.md` | 분석 3 요약 줄: 길면 줄 바꿈 · [오류]/[경고]/[정상] 꼬리표만 점과 같은 색 |
| `123-a3-lines-no-height-limit.md` | 분석 3 요약 줄 높이 한도(230) 없앰 — 줄 바꿈 뒤 두어 줄 넘으면 가려지던 것 |
| `124-history-three-cards.md` | 종합 분석 결과 코드 지도 · "저장된 분석" → History: 저장 한 번에 칸 셋 요약, 줄 누르면 칸에 펼침 |
| `125-llm-summary-input.md` | 사내 LLM 종합 분석 입력 제안 — 분석 1 · 2 에서 무엇을 층(L1~L3)으로, TSV, 분석 사이 IO 연결 표 |
| `126-llm-export.md` | `src/LogScope/LlmExport/` — 분석 1 · 2 · 3 을 사내 LLM 입력(.md · .json)으로, 메인 [LLM 입력 저장] |
| `127-llm-export-revert-outside.md` | LlmExport 밖(단추 · 창 · AppState · DbView · 시험)을 1.42 로 되돌림, csproj 등록만 남김 |
| `128-llm-panel.md` | 메인 종합 분석 결과 → LLM 칸: 분석 셋 체크 · [LLM 분석 실행] · 진행바 · 결과, 꽂는 자리 `ILlmAnalyzer` · `LlmSetup.cs` |
| `129-user-folders-build.md` | Analysis3 폴더는 build.bat 에 저절로 들어감 · LlmExport 도 사용자 폴더로 (tools/LlmExport.targets, 저장소에서 뺌) |
| `130-llm-auto-register.md` | LLM 분석기 꽂기 — 주석 한 줄 대신 ILlmAnalyzer 를 맞춘 클래스를 찾아 꽂음 |
| `131-llm-register-uncomment.md` | 1.47 되돌림 — LlmSetup 에 `LlmLink.Analyzer = new LogScope.LlmExport.OverallLlmAnalyzer();` 한 줄 |
| `132-db-many-rows.md` | 분석 2 상한 (같은 줄 2 만 · 행 차이 5 천 · 목록 5 천/5 만 · 읽기 20 만) → 몇십만 행까지 |
| `133-analysis1-merge-files.md` | Analysis1 같은 기능 파일 합치기 54 → 32 (GraphCanvas partial 여섯 → 하나 등, 코드 그대로) |
| `134-llm-server-check.md` | 설정에 LLM 서버 주소 · [연결 확인], 켤 때 자동 확인, 안 되면 분석 3 · 종합 분석 결과를 회색 덮개 + 안내 |
| `135-build-bat-rebuild.md` | build.bat 이 늘 Rebuild (zip 파일 시각 때문에 건너뛰던 것) · out 의 exe 복사 실패를 알림 |
| `136-sensor-compare-methods.md` | 센서 로그 비교 기법 (DTW · 시간 허용 띠 · 단계 특징값 · 사건 짝짓기 …) 와 추천 순서 — 질문, 코드 안 바꿈 |
| `137-normal-or-not.md` | 목적이 "정상인가" 일 때의 판정 구성 — 정상 로그 여러 개로 범위 배우기 · 사건 순서 · 단계 특징값 · 곡선 띠 · DTW 는 보조 |
| `138-judgement-rules.md` | 정상 판정 규칙 제안 — 시간 허용 Δt · 단계 안정화 · 차이 시간 비율로 등급 · 치명 IO 는 짧아도 FAIL |
| `139-less-customizing.md` | 판정 설정 줄이기 — 사람이 정하는 건 "중요 표시" · "엄격도" 둘, 나머지는 단계 길이 등에서 저절로 |
