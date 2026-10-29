# 119 — 분석 3 칸 머리 줄 지우기 + 이전/이후 폴더 칸 연결

> 그 메인화면에 리포트 차이발생 시간이랑 oo개 이렇게 뜨는거 지워달라니까
>
> 그리고 요약칸에 내용은 들어가는데 이전 이후 폴더칸은 연결이 안됨

## 1. 머리 줄 지움 (1.38)

116 의 "비워 달라" 는 **머리 줄**(크게 적는 낱말 · "차이 발생 N 개" · 옆의 작은 글)이었는데, 1.36 에서
결과 전체를 껐다가 1.37 에 전체를 다시 켜서 머리 줄도 같이 돌아왔습니다. 이제 둘을 나눴습니다.

| 스위치 (`Main/MainVm.cs`) | 값 | 뜻 |
|---|---|---|
| `A3ResultOnCard` | true | 분석 3 결과를 칸에 적음 |
| `A3HeadlineOnCard` (새) | **false** | 머리 줄(`Verdict` · `Score` · `Count` · `Note`)은 안 적음 — **요약 줄(`Lines`)만** |

머리 줄 자리의 높이는 남겨 둡니다 — 칸 셋의 이전 · 이후 줄이 나란히 놓이게.

## 2. 이전/이후 폴더 칸

메인 칸의 이전 · 이후는 `Analysis3Link.BeforePath` · `AfterPath` 를 보여 줍니다. 분석 3 화면이 **자기
[열기] 로 폴더를 고르면 이 값이 안 바뀌므로** 메인 칸이 비어 있습니다 — 사용자 코드에 한 줄씩 필요.

```csharp
// 분석 3 → 메인 칸: 내 화면에서 폴더를 고른 줄 바로 아래
Analysis3Link.SetPath(true, beforeFolder);    // 이전
Analysis3Link.SetPath(false, afterFolder);    // 이후

// 메인 칸 → 분석 3: 메인 칸에서 폴더를 고르면 내 화면에도 (생성자나 Attach 안에서 한 번)
Analysis3Link.PathsChanged += (s, e) =>
{
    _beforeFolder = Analysis3Link.BeforePath;   // 내 화면의 경로 변수 / 글 상자
    _afterFolder = Analysis3Link.AfterPath;
};
```

사용자 코드를 볼 수 없어 변수 이름은 예시. 폴더 고르는 부분 코드를 주면 정확한 자리에 맞춰 줄 수 있음.

확인: 리눅스 컴파일 · 시험 912 통과, 실제 C# 7.3 컴파일러 오류 0.
