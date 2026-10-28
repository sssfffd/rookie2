# 109 — 메인 화면 분석 3 칸: 이전 · 이후 자리 + 분석 3 결과 요약

> 메인화면 분석 3에도 이전 이후 들어가야되고
>
> 내가 직접 넣은 프로그램 결과를(분석 3 결과)를 메인화면 요약칸에 넣고싶은데 어떻게 수정하면 될까?

## 어떻게 이었나 (1.29)

분석 3 폴더는 통째로 갈아 끼우는 자리라(107), 창이나 메인 화면이 그 안의 클래스를 직접 부르면
폴더를 바꿀 때 빌드가 깨집니다. 그래서 폴더 **밖**에 게시판 하나를 두고 양쪽이 거기만 봅니다.

```
 메인 화면 분석 3 칸                 Common/Analysis3Link            Analysis3 폴더 (사용자 코드)
 ● 이전 / ● 이후  ──────────────▶ BeforePath · AfterPath ─ PathsChanged ─▶ 둘 다 차면 분석 실행
 [FAIL] 차이 발생 [2] 개 + 요약 ◀── ResultChanged ───── Publish(결과) ◀── 분석이 끝나면
 요약 줄 누름 ──────────────────▶ 분석 3 화면으로 + OpenRequested(Target)
```

| 파일 | 바꾼 것 |
|---|---|
| `Common/Analysis3Link.cs` (새) | 자리 · 결과 · 알림. `Analysis3Result` · `Analysis3Line` · `Analysis3Grade`. WPF 없음 |
| `Main/MainView.xaml(.cs)` | 분석 3 칸에 이전 · 이후 자리 (분석 2 칸과 같은 두 모양, 누르면 파일 고르기, 끌어다 놓기) |
| `Main/MainVm.cs` | 칸 3 을 "만들었음" 으로, `ShowA3Result` — 낱말이면 분석 2 처럼, 점수면 분석 1 처럼, 없으면 비움 |
| `Main/AnalysisCardVm.cs` | `HasA3Box`, 분석 3 은 결과 전에는 "?? / 100" 을 안 띄움, 요약 줄이 어느 칸 것인지(`Card`) |
| `Shell/ShellWindow.xaml.cs` | 켤 때 지난 자리 되살리기(알림 없이), 고르기 · 놓기 → 게시판, 결과 오면 메인 칸 다시 그리기(배경 스레드에서 와도 화면 스레드로), 요약 줄 → 분석 3 화면 |
| `Settings/AppSettings.cs` | `a3Before` · `a3After` 저장 |
| 시험 | 게시판 알림이 바뀔 때만 한 번 · 켤 때 안 울림 · 칸 표시 규칙 · 설정 저장 (877 → 900) |

## 사용자 코드에서 할 일

```csharp
Analysis3Link.PathsChanged += (s, e) => { if (Analysis3Link.HasBoth) Run(); };

var r = new Analysis3Result();
r.Verdict = "FAIL";        // 또는 r.Score = 87;
r.Count = "1";
r.Lines.Add(new Analysis3Line("항목 A", "1 → 2", Analysis3Grade.Bad) { Target = "항목 A", ValueIsChange = true });
Analysis3Link.Publish(r);
```

전체 예시와 표: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4-2 장.

## 확인

- 리눅스: 컴파일 · 시험 900 통과, 소스 검사 · 처리기 검사 통과.
- 실제 C# 7.3 컴파일러(csc 2.10) + .NET 4.6.1 WPF 참조로 LogScope 와 문서의 예시 코드 컴파일 — 오류 0.
- **못 한 것**: XAML 컴파일과 화면 모양 (Windows `build.bat` 필요). 분석 3 칸의 이전 · 이후 줄 높이가
  분석 1 · 2 칸과 맞는지 봐 주세요.
- 아직 안 한 것: "분석 결과 저장" 기록에 분석 3 의 낱말 · 요약 줄 (점수는 들어감).
