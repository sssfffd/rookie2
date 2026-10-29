# 131 — LLM 분석기 꽂는 주석을 그대로 풀기 (1.47 되돌림)

> 아니 그거 맞춰서 작성한거여가지고 주석만 풀면 될듯?
>
> 아 그거 LlmLink.Analyzer = new LogScope.LlmExport.OverallLlmAnalyzer(); 이걸로 작성하면 됨

사용자 분석기가 이미 `ILlmAnalyzer` 에 맞춰 `LogScope.LlmExport.OverallLlmAnalyzer` 로 있으므로, 1.47 의
"찾아서 꽂기" 를 되돌리고 (1.48) `Main/LlmSetup.cs` 를 한 줄로:

```csharp
public static void Register()
{
    LlmLink.Analyzer = new LogScope.LlmExport.OverallLlmAnalyzer();
}
```

| 파일 | |
|---|---|
| `Main/LlmSetup.cs` | 1.46 판 + 위 한 줄 (Find · 반사 없음) |
| `Main/MainTexts.cs` · 시험 | 1.46 판 (1.47 의 안내 글 · 찾기 시험 뺌) |
| `scripts/stubs/OverallLlmAnalyzer.cs` (새) · `check_compile.sh` | 리눅스 검사 전용 가짜 — LlmExport 에 진짜가 없을 때만 같이 컴파일. **build.bat 에는 안 들어감** |

**주의**: LlmExport 폴더(저장소에 없음)가 없거나 클래스 이름 · 이름 공간이 바뀌면 이 줄에서 빌드가 깨집니다
(CS0234 `OverallLlmAnalyzer` 없음). 새 PC 에서 받으면 LlmExport 폴더를 먼저 넣으세요.

확인: 가짜와 함께 리눅스 컴파일 · 시험 946 통과, 실제 C# 7.3 컴파일러 오류 0. 가짜 없이 컴파일하면 이 줄 하나만
CS0234 (예상한 그대로).
