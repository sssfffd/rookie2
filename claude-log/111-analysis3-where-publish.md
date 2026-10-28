# 111 — 요약 칸에 넣는 코드는 어디에?

> 저거 요약칸에 내용 넣는거 사용자 코드에 넣으라고 했는데 어디에 어떻게 넣어야하는거?

**내 분석이 끝나고 결과를 내 화면에 보여 주는 바로 그 자리 아래** — 보통 내 [분석] 단추의 클릭 처리
함수 안, 계산 직후입니다. 파일은 `src\LogScope\Analysis3\` 의 내 화면 `.xaml.cs`.

```
 Analysis3View.xaml.cs (또는 내 화면 .xaml.cs)
   ├ using 줄들           ← ① using LogScope.Common;
   ├ 생성자               ← ③ (선택) 메인 칸에 이전·이후가 차면 저절로 돌리기
   ├ [열기] 단추          ← ④ (선택) Analysis3Link.SetPath(true/false, 경로)
   └ [분석] 단추
       ├ 기존: 계산
       ├ 기존: 내 화면에 표시   (ItemsSource = / .Text = 같은 줄)
       └ ② 여기: Analysis3Result 를 채워 Analysis3Link.Publish(r)
```

| 내 결과 | 넣을 곳 |
|---|---|
| OK/NG | `r.Verdict = "PASS"` / `"FAIL"` |
| 점수 | `r.Score = 87;` |
| 문제 개수 | `r.Count = "3";` |
| 항목들 | `r.Lines.Add(new Analysis3Line(이름, 값, Analysis3Grade.Bad))` |

예시 코드 전체: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4-3 장
(실제 C# 7.3 컴파일러로 예시 컴파일 확인 — 오류 0).

사용자 코드를 볼 수 없어 이름(`OnRunClick` · `diffs` 등)은 예시입니다. 내 화면 `.xaml.cs` 를 주면
그 자리에 맞춰 정확한 줄을 써 줄 수 있습니다.
