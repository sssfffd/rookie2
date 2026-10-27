# 108 — 내가 만든 화면을 분석 3 자리에 연결하기 (+ GraphCanvas.Trace.cs 질문)

## 요청 134

> analysis1 폴더에 graphCanva.Trace.cs 파일이 있는데 지워도 됨?

**안 됩니다.** `Analysis1/Graph/GraphCanvas.Trace.cs` (390 줄) 는 그래프 **선을 실제로 그리는** 코드입니다
(`DrawChannel` · `DrawDifference` · `DrawSamples` · `ShadeGap` · `Clamp`). `GraphCanvas` 는 한 클래스를
파일 6 개로 나눈 것(partial)이라, 지우면 csproj 의 "파일 없음" 오류, 줄까지 지우면 Lane.cs ·
Draw.cs · Input.cs 가 `DrawChannel` · `Clamp` 를 못 찾는 컴파일 오류가 납니다.

## 요청 135

> 내가 개발한거 anal3화면이랑 어떻게 연결하지??

창은 `LogScope.Analysis3.Analysis3View` 하나만 띄웁니다. 내 화면을 그 이름에 걸면 됩니다.

```
ShellWindow ──띄움──▶ Analysis3View (껍데기, 이름 고정)
                           └──안에 넣음──▶ 내 화면 (이름 자유)
```

| 방법 | 언제 | 할 일 |
|---|---|---|
| A. 이름 바꾸기 | 내 맨 바깥 화면이 UserControl 이고 이름을 바꿔도 될 때 | `x:Class` · 클래스 · 파일 이름을 `Analysis3View` 로, 자리 화면은 지움 |
| **B. 감싸기 (권함)** | 그 밖 | `Analysis3View.xaml` 안에 `<my:MyMainView />` 한 줄 |
| 내 화면이 Window | — | UserControl 로 (Title · Width · WindowStartupLocation 지우기, Closing → Unloaded, `Owner = Window.GetWindow(this)`) |
| 내 App.xaml 의 리소스 | — | 내 화면의 `UserControl.Resources` 로 |

코드 예시와 표 전체: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4-1 장.

여기서는 사용자 코드를 볼 수 없어 연결을 직접 해 보지는 못했습니다. 빌드 오류가 나면 오류 줄을 주면 됩니다.
