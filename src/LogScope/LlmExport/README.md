# LlmExport — 사내 LLM 에 보낼 "종합 분석 입력" 만들기 (1.43)

> 이 폴더는 **사내 AI 에 그대로 먹여도 되게** 썼습니다. 서버로 보내는 코드를 사내 AI 에 부탁할 때
> 이 README · `sample/` 두 파일 · 아래 "파일" 표의 .cs 를 같이 올리세요.

## 하는 일

```
 분석 1 (로그 수천만 칸) ──Analysis1Digest──┐
 분석 2 (DB 수백만 자)   ──Analysis2Digest──┤
 분석 3 (Analysis3Link)  ──Analysis3Digest──┼──▶ LlmDocument ──▶ llm_input_시각.md   (LLM 에 넣는 글)
 분석 1 ↔ 2 같은 IO      ──CrossLink────────┘          │        llm_input_시각.json (서버 코드용 구조)
                                                       └─ FitToBudget: 글자 수 한도면 L3 → L2 줄을 반씩
```

- 원본을 줄이는 게 아니라 **프로그램이 이미 계산한 차이**를 표로 줍니다 (화면과 같은 계산을 다시 불러 씀 →
  화면의 수와 LLM 이 받는 수가 같음).
- **이 폴더 밖은 건드리지 않았습니다** (1.44 에 되돌림 — 단추 · 창 연결 없음). 부르는 코드에서
  `LlmDocument.Build(state, dbDiff, options, progress)` 를 부르면 됩니다. `dbDiff` (분석 2 결과 전체) 는
  분석 2 화면의 `DbVm.Diff` 에 있습니다 — 지금 `DbView` 가 밖으로 내주지 않으므로, 쓰려면 `DbView` 에
  `public DbDiffResult Diff { get { return _vm.Diff; } }` 한 줄이 필요합니다. 없으면 null 을 넘겨도 됩니다
  (분석 2 · 연결이 "담지 못함" 으로 나감).
- 크기 예: 달라진 IO 1,000 개 · DB 값 변화 500 줄이면 L1 만 약 13 만 자. 한도 기본 30 만 자.

## 파일

| 파일 | 내용 |
|---|---|
| `LlmSection.cs` | 기본 단위. `LlmSection` (표 하나: Id · 층 · 설명 · Facts · Columns · Rows · TotalRows), `LlmPart` (분석 하나). Markdown(TSV) · JSON 으로 쓰기 |
| `LlmDocument.cs` | 한 벌 만들기 `Build` · 글자 수 맞추기 `FitToBudget` · `ToMarkdown` · `ToJson` · `Save(folder)` |
| `Analysis1Digest.cs` | 분석 1 → a1.head · a1.groups · a1.ios · a1.steps · a1.time · a1.events · a1.oneside · a1.shape |
| `Analysis2Digest.cs` | 분석 2 → a2.head · a2.values · a2.schema · a2.tables · a2.rows · a2.notes · a2.keys |
| `Analysis3Digest.cs` | 분석 3 → a3.head · a3.lines (분석 3 이 메인 화면에 올린 것) |
| `CrossLink.cs` | 분석 1 ↔ 2 같은 IO → links.io |
| `LlmPrompt.cs` | LLM 에 주는 요청문 (맨 앞 Intro · 맨 끝 Request) — **글만 고쳐 쓰세요** |
| `LlmExportOptions.cs` | 얼마나 담을지 (층 · 글자 수 · 표마다 줄 상한) — **숫자만 고쳐 쓰세요** |
| `LlmFormat.cs` | 숫자 · 시각 · 길이를 적는 규칙 (점 소수 · 쉼표 없음 · 길이는 초) |
| `sample/llm_input_sample.md` · `.json` | 가짜 로그 · DB 로 만든 **실제 출력 예** (모양을 보여 주는 것) |

## 섹션 (Id 는 서버 코드가 고르는 열쇠 — 바꾸지 마세요)

| 분석 | Id | 층 | 내용 |
|---|---|---|---|
| 1 | `a1.head` | L1 | 파일 · 길이 · 시간 맞추기 · 허용 오차 · 비교 구간 · 달라진/안 바뀐/한쪽 IO 수 |
| 1 | `a1.groups` | L1 | 그룹마다 IO 수 · 달라진 수 · 등급 |
| 1 | `a1.ios` | L1 | 달라진 IO 전부 (최대차% 큰 순): 종류 · 그룹 · 최대차 · 최대차% · 평균차 · RMS · 차이시간% · 구간수 · 처음달라짐 · 값 범위 |
| 1 | `a1.steps` | L2 | 단계마다 이전/이후 길이 · 길이차 · 넘은 IO 수 · 상위 IO (단계가 있을 때만) |
| 1 | `a1.time` | L2 | 비교 구간 N 칸마다 넘은 IO 수 · 상위 IO |
| 1 | `a1.events` | L2 | 디지털 · 상태 IO 가 바뀐 시각 이전 vs 이후 짝 · 차(초) |
| 1 | `a1.oneside` | L2 | 한쪽 로그에만 있는 IO |
| 1 | `a1.shape` | L3 | 상위 아날로그 IO 의 구간별 최소 · 평균 · 최대 |
| 2 | `a2.head` | L1 | 자리 · 판정 · 달라진/없어진/생긴 IO 수 · 표 수 |
| 2 | `a2.values` | L1 | IO 값 변화: 표 · IO · 열 · 이전 · 이후 · 다른 열 |
| 2 | `a2.schema` | L1 | 열 정의 변화 |
| 2 | `a2.tables` | L2 | 표마다 생긴 · 없어진 · 바뀐 · 같은 행 수 |
| 2 | `a2.rows` | L2 | 바뀐 행의 바뀐 칸만 |
| 2 | `a2.notes` | L2 | 읽기 · 짝짓기 알림 |
| 2 | `a2.keys` | L3 | 생긴 · 없어진 행 키 |
| 3 | `a3.head` · `a3.lines` | L1 | 분석 3 의 판정 · 요약 줄 |
| 연결 | `links.io` | L1 | 분석 2 에서 바뀐 IO 가 분석 1 에서도 달라졌는지 |

"담지 못함"(`LlmPart.Missing`) — 그 분석 결과가 없으면 까닭을 적습니다 (LLM 이 "없음" 을 "괜찮음" 으로 읽지 않게).

## JSON 모양 (서버 코드용)

```json
{ "format": "logscope-llm-input", "formatVersion": 1, "createdAt": "...", "programVersion": "1.43",
  "maxLevel": 3, "trimmed": false, "intro": "...", "request": "...",
  "parts": [
    { "id": "analysis1", "title": "로그 비교", "missing": "",
      "sections": [
        { "id": "a1.ios", "title": "분석 1 — 달라진 IO", "level": 1, "note": "...",
          "facts": { "이름": "값" }, "factList": [["이름", "값"]],
          "columns": ["IO", "종류", "..."], "rows": [["PRES_MAIN", "아날로그", "..."]],
          "totalRows": 152, "omittedRows": 0 } ] } ] }
```

## 서버로 보내는 코드를 사내 AI 에 부탁할 때 (예시 요청문)

```
첨부한 LogScope 의 src/LogScope/LlmExport (README · .cs · sample) 를 읽고,
1. LlmDocument.Build(...) 로 만든 문서를 사내 LLM 서버로 보내고 답을 받는 C# 코드를 짜 주세요.
   - VS2017 · C# 7.3 · .NET Framework 4.6.1 · NuGet 없이 (HttpWebRequest 또는 WebClient)
   - 보낼 글은 doc.ToMarkdown(), 또는 doc.ToJson() 의 parts 중 필요한 것만
   - 분석 3 결과(분석 3 쪽의 자세한 결과)도 LlmSection 모양으로 더해 같이 보내기
     (LlmPart "analysis3" 에 섹션을 Add — Id 는 "a3.xxx")
   - 서버 주소는 설정 파일에서 읽되, 인증 토큰 · 비밀번호는 파일에 저장하지 말고 실행할 때 입력받기
   - 시간 초과 · 실패 시 사용자에게 알리고 프로그램은 계속 돌게
2. 받은 답(종합 분석 결과 글)을 화면에 보여 줄 자리는 Claude 가 붙일 테니 함수 하나로 돌려주세요:
   string SendAndReceive(LlmDocument doc, out string error)
```

## 지킬 것

- **이 폴더는 파일만 만듭니다.** 네트워크 코드는 없습니다. 보내는 코드는 따로 · 사내 규정에 맞게.
- 인증 정보(토큰 · 비밀번호)는 이 프로그램의 설정 · 코드 · 파일에 적지 않습니다.
- 담기는 것: IO 이름 · 표 이름 · 숫자 · **DB 칸 값**(a2.values · a2.rows). 민감한 값이 있으면 사내 규정 확인.
  파일 이름은 이름만, 폴더 경로는 안 담습니다.
- WPF 를 쓰지 않습니다 (화면 없이 시험 · 서버 쪽 재사용 가능).
