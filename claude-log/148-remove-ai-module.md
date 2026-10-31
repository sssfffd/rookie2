# 148 — 설정의 AI 모듈과 관련 코드 빼기 (1.57)

> 아 그리고 설정쪽에 ai모듈이랑 관련 코드 빼도 될듯 지금 llm종합분석으로 대채됬어

## 뺀 것
| 무엇 | 자리 |
|---|---|
| 설정 창 "AI 모듈 (선택)" 탭 (사용 여부 · python.exe 경로 · 스크립트 경로) | Settings/SettingsWindow.xaml(.cs) · SettingsVm · SettingsTexts |
| 설정 값 AiEnabled · PythonPath · AiScriptPath | Settings/AppSettings.cs — 옛 settings.json 의 값은 안 읽고 다음 저장 때 사라짐 |
| 파이썬을 띄우던 코드 | Analysis1/Compare/AiBridge.cs (어디서도 안 불리고 있었음) · csproj |
| 예제 스크립트 | ai/analyze.py (폴더째) |
| 문서 | docs/SECURITY.md 5 장 · README 폴더 목록 |

LLM 서버 탭 · LLM 종합 분석 · DB 도구 탭은 그대로.

## 확인
- Linux: 테스트 1046 통과, 실제 csc 7.3 오류 · 경고 0, 처리기 · 이름 겹침 · 소스 검사 통과
- Windows 에서 못 봄: 설정 창 탭 모양
- REQUIREMENTS.md 10 줄 "python(ai 활용 모듈, 옵션 기능으로 존재…)" 은 사용자 문서라 안 고침
