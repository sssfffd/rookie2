# 120 — 분석 3 칸의 [분석] 과 [리포트] 차이

> 분석3에 리포트랑 분석 버튼 차이

프로그램 쪽에서는 **둘 다 신호만 보내고 하는 일은 없습니다** — 무엇을 할지는 분석 3 코드가 연결한 함수가 정합니다.

| | [분석] | [리포트] |
|---|---|---|
| 누르면 울리는 것 | `Analysis3Link.AnalyzeRequested` | `Analysis3Link.ReportRequested` |
| 모양 | 파란 강조 단추 (`PrimaryButton`) | 보통 단추 |
| 연결 없을 때 | "연결된 기능이 아직 없습니다" 알림 | 같음 |
| 만들 때 생각한 쓰임 | 이전 · 이후 폴더로 분석을 돌리고 결과를 `Publish` → 메인 요약 칸 | 마지막 분석 결과로 리포트 파일(엑셀 · HTML 등)을 만들어 저장 |
| 코드 | `src/LogScope/Shell/ShellWindow.xaml.cs` `OnMainA3Analyze` | 같은 파일 `OnMainA3Report` |

이름 · 설명 글: `src/LogScope/Main/MainTexts.cs` 의 `A3Analyze` · `A3Report` (· `…Tip`).
