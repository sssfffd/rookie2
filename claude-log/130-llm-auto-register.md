# 130 — LLM 분석기 꽂는 한 줄 주석 풀기

> 아 그리고 log 종합 분석 실행에 주석처리한거 있잖아? 버튼누르면 동작하는거에서 한줄 주석처리한거 그거 풀어줘

`Main/LlmSetup.cs` 의 `// LlmLink.Analyzer = new MyLlmAnalyzer();` 를 **그대로 풀면 빌드가 깨집니다** —
`MyLlmAnalyzer` 는 예시 이름이고, 사용자 분석기는 LlmExport(사용자 폴더, 저장소에 없음) 안에 다른 이름으로
있을 수 있어서 이 파일이 그 이름을 알 수 없습니다. 폴더가 없을 때도 깨집니다.

그래서 (1.47) 그 한 줄 대신 **`ILlmAnalyzer` 를 맞춘 클래스를 프로그램 안에서 찾아 꽂습니다**:

```csharp
public static void Register()
{
    if (LlmLink.Analyzer != null) return;              // 다른 곳에서 이미 꽂았으면 그대로
    LlmLink.Analyzer = Find(typeof(LlmSetup).Assembly); // ILlmAnalyzer 를 맞춘 클래스 하나
}
```

| 찾는 조건 | |
|---|---|
| `ILlmAnalyzer` 를 맞춤 · 추상 아님 · **매개변수 없는 생성자** | 그 클래스 |
| 여럿이면 | `LogScope.LlmExport` 이름 공간 것 먼저, 그다음 이름 순 |
| 없거나 만들다 오류 | null → 버튼이 "연결된 LLM 분석 기능이 없습니다" 알림 (프로그램은 켜짐) |

이름이 정해져 있으면 `Register` 를 `LlmLink.Analyzer = new 그이름();` 한 줄로 바꿔도 됩니다.

확인: 리눅스 컴파일 · 시험 950 통과 (찾기 3 개 더 — 가짜 분석기를 찾아 돌림), 실제 C# 7.3 컴파일러 오류 0.
