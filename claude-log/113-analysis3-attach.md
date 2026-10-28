# 113 — 분석 3 화면의 Attach 를 창이 불러 주게

> 아이거 shell window쪽에 loadanalysis3 있잖아 그 안에 try a3host.content이렇게 되어 있는데
> framework view = (frameworkelement)activator.createinstant(t)
> var attach = t.getmethod("attach"
> 이런식으로 attach로 바꾸니까 잘 되는데 어떻게 할까

사용자의 분석 3 화면은 분석 2(DbView)처럼 `Attach(AppState)` 로 설정 · 로그를 받는 꼴이었는데,
창은 만들기만 하고 Attach 를 안 불러서 빈 상태로 돌다가 폴더를 열 때 오류가 났습니다(112).
사용자가 `ShellWindow.LoadAnalysis3` 를 직접 고쳐 해결했지만, **ShellWindow 는 Analysis3 밖이라
새 버전을 받으면 덮여 사라집니다.** 그래서 같은 일을 저장소 쪽에 넣었습니다 (1.33).

| | 바꾼 것 |
|---|---|
| `ShellWindow.CallAttach(view, state)` (새 — 1.34 에 Analysis3Link 에서 창 파일로 옮김) | `public void Attach(AppState)` 를 먼저, 없으면 `public void Attach()` 를 이름으로 찾아 부름. 없으면 false. 오버로드가 있어도 `AmbiguousMatchException` 안 남 (꼴을 정해서 찾음) |
| `ShellWindow.LoadAnalysis3` | 만들기 → 자리에 넣기 → `CallAttach(view, _state)`. 만들기 · Attach 중 오류는 자리 화면에 "Attach 하다 오류가 났습니다" 처럼 어느 단계인지와 함께 |
| 시험 | 두 꼴 · 둘 다 있을 때 AppState 쪽 · 없을 때 · 오류가 속에 싸여 올라옴 (900 → 905) |

자리에 먼저 넣고 Attach 를 부르는 까닭: Attach 안에서 `Window.GetWindow(this)` 를 쓰면 창 안에 있어야 함.

**사용자 쪽**: 직접 고친 ShellWindow 는 새 버전으로 덮어쓰면 됩니다 (같은 일을 함). Analysis3 쪽 코드는 그대로.

확인: 리눅스 컴파일 · 시험 905 통과, 실제 C# 7.3 컴파일러 오류 0. Windows 실행은 못 함.
