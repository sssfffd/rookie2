# 117 — 분석 3 → 메인 화면: 받고 띄우는 코드가 어디 있나

> 그 분석 3안에서 폴더 열고 요약메세지 보냈을 때 메인화면에서 받고 띄워주는 코드들 어디에 있음?

```
 분석 3 (사용자 코드)                 게시판                          창                         메인 화면
 SetPath(true, 폴더) ──────▶ Analysis3Link.SetPath ──PathsChanged──▶ OnA3PathsChanged ──▶ MainVm.Refresh
 Publish(결과)       ──────▶ Analysis3Link.Publish ──ResultChanged─▶ OnA3ResultChanged ─┘   ├ A3Before… (이전·이후 칸)
                                                                                           └ ShowA3Result (결과 · 요약)
                                                                                                 └ AnalysisCardVm → MainView.xaml
```

| 단계 | 파일 : 줄 (1.36 기준) | 하는 일 |
|---|---|---|
| ① 받는 곳 (게시판) | `src/LogScope/Common/Analysis3Link.cs` : 82 `SetPath` · 90 `SetPaths` · 112 `Publish` | 자리 · 결과를 담고 49 `PathsChanged` · 52 `ResultChanged` 를 울림 |
| | 같은 파일 : 162 `Analysis3Result` · 190 `Analysis3Line` | 결과 모양 |
| ② 창이 듣기 | `src/LogScope/Shell/ShellWindow.xaml.cs` : 103 · 104 | `PathsChanged += OnA3PathsChanged`, `ResultChanged += OnA3ResultChanged` |
| | 같은 파일 : 497 `OnA3PathsChanged` | 설정에 저장(`A3BeforePath`) + `_vm.Main.Refresh()` |
| | 같은 파일 : 512 `OnA3ResultChanged` | `_vm.Main.Refresh()` (배경 스레드면 화면 스레드로) |
| | 같은 파일 : 40 `Restore` · 440 `OnMainA3Pick` · 490 `OnMainA3Dropped` | 켤 때 지난 자리 · 메인 칸 클릭(폴더 고르기) · 끌어다 놓기 |
| ③ 메인 화면 값 | `src/LogScope/Main/MainVm.cs` : 51 `Refresh` → 95 `ShowA3Result()` · 112 `Raise("A3…")` | 다시 그리기 |
| | 같은 파일 : 176 `ShowA3Result` | 결과를 칸에 옮김 — **174 `A3ResultOnCard = false` 라 지금은 비움 (1.36)** |
| | 같은 파일 : 529~ `A3BeforeName` · `A3BeforeHint` · `A3BeforeKind` … | 이전·이후 칸 글 |
| ④ 칸 값 | `src/LogScope/Main/AnalysisCardVm.cs` : 318 `SetVerdict` · 377 `SetScore` · 520 `SetLines` · 528 `GroupNote` | 낱말 · 점수 · 요약 줄 · 빈 칸 글 |
| ⑤ 화면 | `src/LogScope/Main/MainView.xaml` : 159 (단추) · 196 `ScoreText` · 213 `VerdictCount` · 474~ (이전·이후 칸) · 624 `Lines` (요약 줄) | 그리기 |
| (저장) | `src/LogScope/Settings/AppSettings.cs` : 160 · 300 · 423 | `a3Before` · `a3After` |
