# 114 — Attach 부르기를 ShellWindow 파일 하나에만

> ㄴㄴ 너가 shell window만 고쳐주면 될 것 같음

1.33 에는 Attach 를 찾는 코드를 `Common/Analysis3Link.cs` 에 두고 창이 불렀습니다. 그러면 사용자가
**ShellWindow 파일 하나만** 바꿔 끼울 때 Analysis3Link 도 같이 바꿔야 합니다. 그래서 (1.34):

| 파일 | 1.32 와 비교 |
|---|---|
| `Shell/ShellWindow.xaml.cs` | `CallAttach(view, state)` 를 이 파일 안에 두고 `LoadAnalysis3` 가 부름 |
| `Common/Analysis3Link.cs` | **1.32 그대로** (1.33 에 더했던 CallAttach 뺌) |
| 그 밖의 `src/LogScope` | 바뀐 것 없음 |

→ 1.32 를 쓰고 있다면 `src/LogScope/Shell/ShellWindow.xaml.cs` 하나만 바꿔 넣으면 됩니다.

시험(905)은 `ShellWindow.CallAttach` 를 부르도록 고쳤습니다 (창을 띄우지 않는 static 함수라 시험이 닿음).
확인: 리눅스 컴파일 · 시험 905 통과, 실제 C# 7.3 컴파일러 오류 0. Windows 실행은 못 함.
