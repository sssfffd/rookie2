# 144 — LLM 분석에 보낼 분석 고르기 · 메인 맨 아래 안내 빼기 (1.54)

> 그리고 지금 llm 분석 실행 누르면 1,2,3이 다 가잖아 이거 내가 분석 1체크 분석 2체크 분석3체크해서 원하는 분석들만 가능하게끔 해줘야되고 메인화면에서 체크까진 여기 깃에 있을텐데 데이터를 따로 분류해서 보내는건 내가 따로 만든 코드에서 수정해야되는거면 말해주셈
>
> 그리고 메인화면 맨 밑에 점수 색이랑 점수매기는 규칙 없다 이런 문구랑 안내들 다 없애주고

## 1. 체크해서 고르기

```
 종합 분석 결과                                          [LLM 분석 실행]
 ☑ ● 분석 1 · 로그 비교  완료   ☐ ● 분석 2 · DB  완료   ☐ ● 분석 3  안 함(꺼짐)
```

- 체크 상자는 결과가 있을 때만 누를 수 있음. 처음엔 모두 체크
- 결과가 없어 꺼져 있는 동안에도 고른 것은 기억 → 결과가 생기면 고른 대로
- 보낼 것(결과 있음 + 체크)이 하나도 없으면 [LLM 분석 실행] 꺼짐
- `LlmRequest.Use1~3` = 결과 있음 **그리고 체크함**
- 안 고른 분석 2 → `DbDiff = null`, 분석 3 → `Analysis3 = null` · 자리 빈 글자

## 2. 사용자 코드(LlmExport, 저장소 밖)에서 고칠 것 — **필요함**

프로그램 쪽에서 막을 수 없는 것:

| 분석 | 왜 못 막나 | 사용자 코드에서 |
|---|---|---|
| 1 | 로그 · 비교 결과가 `request.State` 안에 있고, State 에는 설정도 있어서 비울 수 없음 | `if (request.Use1)` 일 때만 분석 1 부분 만들기 |
| 2 | `request.DbDiff` 는 null 로 오지만, `State.HasDbResult` · `DbChangedIos` 로도 읽을 수 있음 | `if (request.Use2)` |
| 3 | `Analysis3Link.Result` 가 정적이라 어디서든 읽힘 (1.43 의 Analysis3Digest 가 이렇게 읽음) | `if (request.Use3)` |
| 1+2 묶음 (CrossLink) | 둘 다 있어야 의미 | `if (request.Use1 && request.Use2)` |

1.43 에 만든 모양 그대로면 `LlmDocument.Build` 가 넷을 다 넣음:

```csharp
// 지금 (1.43 모양)
doc.Parts.Add(Analysis1Digest.Build(state, doc.Options, prog));
doc.Parts.Add(Analysis2Digest.Build(state, dbDiff, doc.Options, prog));
doc.Parts.Add(Analysis3Digest.Build());
doc.Parts.Add(CrossLink.Build(state, dbDiff, doc.Options));

// 이렇게 — Build 에 request(또는 use1~3)를 넘겨서
if (request.Use1) doc.Parts.Add(Analysis1Digest.Build(request.State, doc.Options, prog));
if (request.Use2) doc.Parts.Add(Analysis2Digest.Build(request.State, request.DbDiff, doc.Options, prog));
if (request.Use3) doc.Parts.Add(Analysis3Digest.Build());
if (request.Use1 && request.Use2) doc.Parts.Add(CrossLink.Build(request.State, request.DbDiff, doc.Options));
```

## 3. 메인 맨 아래 안내 빼기
- "점수를 매기는 규칙은 아직 정해지지 않았습니다 …" (MainTexts.LogNote) — 뺌
- 점수 색 설명 (초록/노랑/빨강 범위, MainVm.Bands) — 뺌. 칸의 점수 색 자체는 그대로
- History 이름 아래 안내 글 (HistoryNote) — 뺌. 줄 풍선 도움말은 남음
- 겸사: 체크 풍선의 "안 함" 설명을 1.53 [분석] 흐름에 맞춤

## 확인
- Linux: 테스트 992 통과 (새 9), 실제 csc 7.3 오류 0, 처리기 · 이름 겹침 · 소스 검사 통과
- **Windows 에서 못 봄**: 체크 상자 모양 · 꺼진 상태 풍선, 맨 아래가 비어 보이는 모양
- 사용자 OverallLlmAnalyzer 가 Use1~3 을 이미 보는지는 모름 (저장소 밖)
