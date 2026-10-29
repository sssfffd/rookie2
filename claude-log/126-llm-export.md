# 126 — 사내 LLM 입력 만들기 (분석 1 · 2 파싱 → .md · .json)

> ㅇㅋ 그럼 따로 파일이랑 폴더 만들어서 분석 1, 2 내용 파싱해서 받는 거 만들어줘
> 주석으로도 내용좀 자세하게 써주고
>
> 왜냐하면 그 파일들 사내 ai한테 먹여서 분석 3이랑 같이 서버에 보내는 코드 작성해달라도 할꺼여서 그래

## 만든 것 (1.43) — `src/LogScope/LlmExport/` (namespace `LogScope.LlmExport`)

125 의 제안 그대로. 사내 AI 에 먹일 것을 생각해 **파일마다 머리 주석에 모양 · 섹션 · 쓰는 법**을 적었고,
폴더에 `README.md` (섹션 표 · JSON 모양 · 사내 AI 에 줄 요청문 예) 와 `sample/` (실제 출력 예) 를 넣었습니다.

```
 분석 1 ──Analysis1Digest──┐
 분석 2 ──Analysis2Digest──┤
 분석 3 ──Analysis3Digest──┼──▶ LlmDocument ──▶ llm_input_시각.md  (LLM 에 넣는 글, 표는 TSV)
 1 ↔ 2  ──CrossLink────────┘          │        llm_input_시각.json (서버 코드용 구조)
                                      └─ FitToBudget (글자 수 한도면 L3 → L2 줄을 반씩, L1 은 그대로)
```

| 파일 | 내용 |
|---|---|
| `LlmSection.cs` | 표 하나(`LlmSection`: Id · 층 · 설명 · Facts · Columns · Rows · TotalRows) · 분석 하나(`LlmPart`) · Markdown/JSON |
| `LlmDocument.cs` | `Build` · `FitToBudget` · `ToMarkdown` · `ToJson` · `Save(folder)` (덮어쓰지 않음) |
| `Analysis1Digest.cs` | a1.head · groups · ios · steps · time · events · oneside · shape — **히트맵 · 비교와 같은 계산을 다시 불러** 화면과 같은 수 |
| `Analysis2Digest.cs` | a2.head · values · schema · tables · rows(바뀐 칸만) · notes · keys — 분석 2 목록과 같은 계산 |
| `Analysis3Digest.cs` | a3.head · lines — 분석 3 이 메인 화면에 올린 것 |
| `CrossLink.cs` | links.io — 분석 2 에서 바뀐 IO 가 분석 1 에서도 달라졌는지 (이름 느슨하게 맞춤) |
| `LlmPrompt.cs` · `LlmExportOptions.cs` · `LlmFormat.cs` | 요청문 · 얼마나 담을지 · 숫자/시각 적는 규칙 |

화면: 메인 "종합 분석 결과" 칸 오른쪽 위 **[LLM 입력 저장]** → 폴더 → 진행 창 → 두 파일. **보내지는 않음.**
연결: `AppState.DbDiff` (분석 2 결과 전체, 창이 옮김) · `DbView.Diff` 더함.

## 안전

- 네트워크 코드 없음 — 파일만. 인증 정보는 프로그램에 저장하지 않는 원칙을 README · 주석 · 예시 요청문에 적음.
- 파일 이름만 담고 폴더 경로는 안 담음. DB 칸 값은 담기므로 사내 규정 확인을 README 에 적음.

## 확인

- 리눅스 컴파일 · 시험 952 통과 (LLM 입력 18 개 더 — 가짜 로그 · DB 로 각 섹션 값 · 사건 짝 · 연결 · 한도 · 파일).
- 실제 C# 7.3 컴파일러 + .NET 4.6.1 WPF 참조 — 오류 0.
- 가짜 데이터로 만든 실제 출력: `src/LogScope/LlmExport/sample/llm_input_sample.md` · `.json`.
- **못 한 것**: Windows 에서 실제 로그(수천만 칸)로 걸리는 시간 · 글자 수. 단추 모양.
