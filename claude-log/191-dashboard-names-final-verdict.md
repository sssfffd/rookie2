# 191 — 1.95 대시보드 이름 · 최종 판정 1 · 2 · 3 · "결과 없음" · History 폭

## 요청 (요청 218)

이름 바꾸기 · 경계 95 / 80 · 차이 IO 이름은 config · 평균 오차율 설명이 칸에서 안 잘리게 · 분석 2 · 3 칸 아래 줄 지우기 ·
최종 판정은 1 · 2 · 3 다 (안 넣은 것은 무시) · "아직" → "결과 없음" · "—" 도 "결과 없음" · 분석 칸의 "아직" 판정 칸 없애기 ·
History 분석 2 줄바꿈 (오른쪽 공간 활용) · 저장되는 "아직 읽지 않았습니다" → "결과 없음" · 비어 있는 요약 → "결과 없음".

## 한 것

| 요청 | 이렇게 | 파일 |
|---|---|---|
| 최종 판정에 "분석 1 기준" 대신 이름 | 칸 이름 "최종 판정", 근거 "로그 비교 — 공정 일치율 85 % · 차이 IO 5 / 120" (config.txt 이름), 아래 "로그 비교 주의 · DB 분석 정상" | `OverallVm.cs`, `MainTexts.cs` |
| 최종 판정 = 1 · 2 · 3 | **결과가 있는 분석 중 가장 나쁜 판정** — 결과 없음 · 못 씀 (분석 3 LLM 서버) 은 무시 | `OverallVm.cs` |
| 점수 → 공정 일치율 | 대시보드 칸 · 게이지 "78.9%" · 근거 줄 · 분석 1 칸 "73.1 %" (전 "/ 100") · LLM 글 | `MainTexts.cs`, `DashVisuals.cs`, `AnalysisCardVm.cs`, `JudgeText.cs` |
| 95 정상 · 80 주의 | 기본값 · config.txt (score.good · score.warn) | `AppConfig.cs`, `config.txt` |
| 차이 IO 이름 config | `label.diff = 차이 IO` (대시보드 칸 이름 · 근거 줄) | `AppConfig.cs`, `MainDashVm.cs` |
| 평균 오차율 | 이름 "IO별 평균 오차율", 기준 설명은 줄바꿈 (안 잘림) | `MainTexts.cs`, `MainView.xaml` |
| 로그 길이 → 공정 시간 일치율 | 칸 이름 · 설명 · 분석 1 칸 · 오차 목록 줄 "공정 시간" · 히트맵 알림 · LLM 글 | 여러 곳 |
| 분석 2 · 3 칸 아래 줄 | 지움 (이름 + 판정만) | `MainView.xaml` |
| "아직" · "—" → "결과 없음" | 판정 낱말 · 근거 줄 · 대시보드 숫자 (작은 회색 글로 — 큰 글자면 칸에서 잘려서) · 게이지 | `AnalysisCardVm.cs`, `MainDashVm.cs`, `DashVisuals.cs` |
| 분석 칸의 판정 칸 | 칸 머리 배지 (1.92) 뺌 | `MainView.xaml` |
| History 줄바꿈 · 공간 | 칸 셋 폭 1.4 : 1.4 : 1 (전 같은 폭), 글은 한 줄 (줄바꿈 → " · ") | `MainView.xaml`, `HistoryRowVm.cs`, `CardSnapshot.cs` |
| 저장되는 글 | 분석 2 빈 요약 "결과 없음" (전 "아직 읽지 않았습니다 …"), History 에서 예전 "아직 …" 글 · 빈 요약 · "-" 도 "결과 없음" | `MainVm.cs`, `CardSnapshot.cs` |
| 빈 요약칸 | "결과 없음" (분석 1 은 견줬는데 그룹이 없을 때만 "그룹이 없습니다 — …") | `AnalysisCardVm.cs` |

## 확인

- 시험 1118 통과 (최종 판정 시험을 새 규칙으로 다시 씀: 결과 없음 무시 · 가장 나쁜 것 · 이름 근거 · 못 씀 무시, 경계 95 · 80,
  label.diff, "결과 없음", %).
- check 스크립트 · 실제 csc 빌드 (사용자 `LlmExport` 포함) · XAML 형식 통과.
- **Windows 에서 못 본 것**: 대시보드 칸 글 길이 (특히 "IO별 평균 오차율" 설명 줄바꿈 높이), "결과 없음" 크기, History 칸 폭.
