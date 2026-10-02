# 44 — DB 화면이 메인과 분석 1 위에 덮여 있던 것

> 신고: "근데 지금 메인화면이랑 로그 분석 화면 이상한대?"

**제가 0.51 에서 넣은 버그입니다.**

## 무슨 일이었나

본문은 화면 넷을 **다 만들어 두고 보이기만 바꿉니다** (화면을 오갈 때 고른
IO 나 확대 상태가 사라지지 않게). 그래서 안 보여야 할 화면이 보이면
**그대로 위에 덮입니다.**

분석 2 (DB) 를 넣을 때 보이기 여부를 이렇게 적었습니다.

```xml
<v:DbView x:Name="Db"
          Visibility="{Binding ShowAnalysis2, Converter={StaticResource BoolToVisibility}}" />
```

그런데 `DbView` 는 생성자에서 **자기 DataContext 를 바꿉니다.**

```csharp
public DbView()
{
    InitializeComponent();
    DataContext = _vm;        // DbVm
}
```

그래서 `ShowAnalysis2` 를 창의 뷰모델(`ShellVm`)이 아니라 **`DbVm` 에서**
찾습니다. 없으니 바인딩이 조용히 실패하고, **실패한 Visibility 는 기본값
`Visible` 로 남습니다.** DB 화면이 늘 켜진 채 메인과 분석 1 위에 덮였습니다.

다른 화면들이 모두 이렇게 적혀 있던 이유가 이것입니다.

```xml
<v:GraphView DataContext="{Binding Graph}"
             Visibility="{Binding DataContext.ShowGraph,
                          RelativeSource={RelativeSource AncestorType=Window}, ...}" />
```

`DbView` 한 줄만 그 규칙에서 빠졌습니다.

## 고친 것

```xml
<v:DbView x:Name="Db"
          Visibility="{Binding DataContext.ShowAnalysis2,
                       RelativeSource={RelativeSource AncestorType=Window},
                       Converter={StaticResource BoolToVisibility}}" />
```

왜 이렇게 적어야 하는지를 그 자리 주석에 적어 뒀습니다. 다음에 화면을 더할
때 같은 실수를 하지 않게.

## 왜 검사에서 안 걸렸나 — 그래서 검사를 더했습니다

- 컴파일됩니다 (XAML 바인딩은 글자입니다).
- XAML 파싱도 됩니다.
- 처리기·리소스·테마 검사와도 상관이 없습니다.
- **바인딩 실패는 실행할 때만** 드러납니다 (WPF 는 출력 창에 적고 그냥 넘어갑니다).

이 자리를 잡는 검사를 `check_sources.py` 에 넣었습니다.

```
1. 코드 비하인드에서 DataContext 를 바꾸는 뷰를 모읍니다 (DataContext = ...).
2. 그 뷰를 <b>쓰는 자리</b>에서 속성이 맨 경로로 바인딩돼 있으면 지적합니다.
   (RelativeSource / ElementName / Source / DataContext. 로 시작하면 넘어갑니다.)
```

고치기 **전에** 돌려서 정말 잡는지 먼저 봤습니다.

```
DbView 의 Visibility 는 맨 경로 바인딩 (DbView 는 자기 DataContext 를 바꿉니다)
  <- src/LogScope.App/Views/ShellWindow.xaml
```

규칙을 쓰다 한 번 헛돌았습니다 — 속성값 안에
`Converter={StaticResource ...}` 처럼 중괄호가 또 들어 있어서, 닫는 중괄호까지
찾던 정규식이 아무것도 못 잡았습니다. **닫는 따옴표까지** 보도록 바꿨습니다
(XAML 속성값에는 따옴표가 들어갈 수 없습니다).

## 자체 테스트

테스트 474 통과(그대로). 한 줄 고친 것이고, 그 한 줄을 잡는 것은 테스트가
아니라 위의 검사입니다.

### 아직 확인 못 한 것

메인 화면과 분석 1 이 **제대로 보이는지**는 윈도우에서 봐 주셔야 합니다.
덮여 있던 것이 치워지면 원래 모습이 나올 텐데, 그 아래에 다른 문제가 같이
숨어 있었을 수도 있습니다. 여전히 이상하면 어느 쪽이 어떻게 이상한지 알려
주세요 (화면이 비었는지, 글자가 겹치는지, 칸이 깨졌는지).
