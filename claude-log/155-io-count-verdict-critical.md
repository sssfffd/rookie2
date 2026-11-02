# 155 — 분석 1 판정 = 오차 IO 수 (10 개 넘으면 CRITICAL) · FAIL → CRITICAL (1.64)

> 아니 위에 IO 개수 기준으로 넣으라고 한건 구간별로 개수를 넣으면 너무 크게 보여서 그럼
> IO개수 기준으로 넣고 10개 이하면 warning 10개 넘으면 Critical
> 그리고 이제 fail대신에 critical로 다 바꿔줘

## 분석 1 판정
| 허용 안 된 오차가 있는 IO | 판정 |
|---|---|
| 0 | PASS |
| 1 ~ 10 | WARNING |
| 11 이상 | CRITICAL |
| 단계가 없어짐 · 생김 · 차례가 바뀜 (IO 수와 상관없이) | CRITICAL |
| 단계 길이 차이만 | WARNING |

- 메인 칸 "차이 발생 N 개" = 그 IO 수 (1.63 과 같음)
- 1.63 까지는 "오차 줄 중 가장 나쁜 것" (한 단계에서 오차 시간 20 % 이상이면 FAIL) 이 판정이었음 → 이제 그 20 % 는
  **오차 줄 낱말(심한 정도 표시)** 로만 남음. 그래서 오차 줄이 CRITICAL 이어도 판정은 IO 수에 따라 WARNING 일 수 있음
- 기준 수 10 은 JudgeOptions.CriticalIoCount

## FAIL → CRITICAL
- 분석 1 (판정 · 오차 줄 · 그룹 색 · LLM 글) · **분석 2 (DB 판정 100 개부터 CRITICAL)** · 주석 · README · 가이드
- 분석 3 은 사용자 코드가 낱말을 정함 — 칸 색은 CRITICAL · FAIL 둘 다 빨강으로 받음
- 옛 이름 `DbVerdict.FailFrom` · `DbVerdict.Fail` · `JudgeGrade.Fail` 은 **남겨 둠** (Obsolete, 값은 새 것과 같음) —
  사용자 폴더(LlmExport)의 코드가 부르고 있을 수 있어서 (1.43 의 Analysis2Digest 가 FailFrom 을 부름). `DbVerdict.Fail` 의 값은 이제 "CRITICAL"

## 확인
- Linux: 테스트 1074 통과 (IO 11 개 → CRITICAL · 10 개 → WARNING · 0 → PASS 시험 더함), 실제 csc 7.3 오류 · 경고 0, 검사 통과
- Windows 에서 못 봄
