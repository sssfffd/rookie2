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
| `22-delta-is-difference.md` | 0–1 정규화 제거, 변화량을 진짜 차분으로 |
