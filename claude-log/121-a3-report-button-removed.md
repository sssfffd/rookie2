# 121 — 분석 3 칸의 [리포트] 단추 뺌

> 리포트는 빼줘

| 파일 | 뺀 것 |
|---|---|
| `Main/MainView.xaml` | 칸 머리의 [리포트] 단추 — [분석] 하나만 남음 |
| `Main/MainView.xaml.cs` | `A3ReportRequested` · `OnA3Report` |
| `Main/MainTexts.cs` | `A3Report` · `A3ReportTip` |
| `Shell/ShellWindow.xaml.cs` | `OnMainA3Report` |
| `Common/Analysis3Link.cs` | `RequestReport` |

`Analysis3Link.ReportRequested` 는 **이름만 남겼습니다** (울리지 않음) — 분석 3 쪽에 이미
`ReportRequested += …` 를 적어 두었다면 빌드가 깨지지 않게.

```
┌ 분석 3 ─ 이름 ──────────────── [분석] ↗ ┐
```

확인: 리눅스 컴파일 · 시험 910 통과, 실제 C# 7.3 컴파일러 오류 0.
