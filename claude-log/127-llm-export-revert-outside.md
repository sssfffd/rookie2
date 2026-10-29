# 127 — LlmExport 밖은 1.42 로 되돌림

> 지금 llmexport말고 다른 파일은 안거드린거지? 건드렸으면 원복시켜놔 파일 안에 내용들은 바꿀꺼야

1.43 에서 LlmExport 밖도 건드렸습니다 (단추 · 창 연결 · 시험). **1.42 그대로** 되돌렸습니다 (1.44).

| 파일 | 1.43 에 한 것 | 지금 |
|---|---|---|
| `Shell/ShellWindow.xaml.cs` | [LLM 입력 저장] 처리 · `_state.DbDiff` 옮김 | 1.42 그대로 |
| `Main/MainView.xaml` · `.xaml.cs` · `MainTexts.cs` | [LLM 입력 저장] 단추 | 1.42 그대로 |
| `Common/AppState.cs` | `DbDiff` 칸 | 1.42 그대로 |
| `Analysis2/DbView.xaml.cs` | `Diff` 내주기 | 1.42 그대로 |
| `src/LogScope/README.md` | 화면 지도 한 줄 | 1.42 그대로 |
| `src/LogScope.Tests/Program.cs` | LLM 입력 시험 18 개 | 1.42 그대로 (그 시험도 뺌) |
| **`LogScope.csproj`** | LlmExport `.cs` 9 줄 등록 | **남김** — 빼면 VS 빌드에서 LlmExport 가 아예 컴파일되지 않음 |

LlmExport 안: `README.md` 의 단추 · 시험 이야기를 고침 — 이제 단추가 없으니 부르는 코드에서
`LlmDocument.Build(state, dbDiff, options, progress)`. `dbDiff` 는 `DbVm.Diff` 에 있고 `DbView` 가 내주지 않으므로
쓰려면 `DbView` 에 한 줄 필요 (없으면 null — 분석 2 · 연결이 "담지 못함").

확인: 컴파일 · 시험 934 (1.42 와 같음) 통과, 실제 C# 7.3 컴파일러 오류 0.
