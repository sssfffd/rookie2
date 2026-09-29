# 35 — 파일을 놓으면 네모 칸이 사라지던 것 (ClearValue)

> 요청: "넣으면 네모 칸 사라지는거 불편"

**제가 0.39 에서 넣은 버그입니다.**

## 무슨 일이 있었나

메인 화면의 이전/이후 칸은 커서가 올라오면 밝아졌다가, 놓으면 원래대로
돌아가야 합니다. 그 "원래대로" 를 이렇게 했습니다.

```csharp
card.ClearValue(Border.BackgroundProperty);
card.ClearValue(Border.BorderBrushProperty);
card.ClearValue(Border.BorderThicknessProperty);
```

그때 주석에 이렇게 적었습니다 — *"지우면 XAML 의 DynamicResource 가 다시
살아납니다."* **틀렸습니다.**

XAML 에 적은

```xml
<Border Background="{DynamicResource Brush.Panel}"
        BorderBrush="{DynamicResource Brush.Border}" BorderThickness="1">
```

도 WPF 가 보기에는 **"직접 쓴 값"(local value)** 입니다. `ClearValue` 는 그
자리를 통째로 비웁니다. 그러면 남는 것은 `Border` 의 기본값 — 배경 없음,
테두리 없음. **네모 칸이 사라집니다.**

그래서 파일을 놓는 순간(`OnLogRowDrop`)이나 칸 밖으로 끌고 나가는
순간(`OnLogRowDragLeave`) 칸이 없어졌습니다.

## 어떻게 고쳤나

되돌릴 때도 **값을 다시 걸어 줍니다.**

```csharp
card.SetResourceReference(Border.BackgroundProperty,
    on ? "Brush.DropTarget" : "Brush.Panel");
card.SetResourceReference(Border.BorderBrushProperty,
    on ? (before ? "Brush.Before" : "Brush.After") : "Brush.Border");
card.BorderThickness = new Thickness(on ? 2 : 1);
```

`SetResourceReference` 는 **DynamicResource 를 코드로 거는 것**입니다. 값을
박아 넣는 `FindResource` 와 달리, 테마를 바꾸면 이 칸도 같이 따라갑니다 —
원래 걱정했던 "그 칸만 옛 색으로 남는" 문제는 이걸로 제대로 막힙니다.

밝힐 때와 되돌릴 때가 같은 모양이 된 것도 덤입니다. 한쪽만 고치는 실수가
줄어듭니다.

## 왜 검사에서 안 잡혔나

WPF 흉내 파일의 `ClearValue` 는 **빈 메서드**입니다. 타입이 맞는지만 봅니다.
`ClearValue` 가 하는 <b>일</b>은 진짜 WPF 에만 있습니다.

XAML 은 여기서 컴파일되지 않고 화면도 뜨지 않으므로, 이런 것은 **윈도우에서
써 봐야만** 나옵니다. 매번 적어 두는 "아직 확인 못 한 것" 이 실제로 터진
경우입니다.

`SetResourceReference` 를 흉내 파일에 더했습니다 — 진짜 WPF 에 있는 API 인데
아직 안 써서 빠져 있었습니다.

## 자체 테스트

테스트 352 통과(그대로). 화면 쪽이라 Core 에 붙일 것이 없습니다.

### 봐 주셔야 할 것

- 파일을 놓은 뒤 칸이 제자리로 돌아오는지
- 칸 위로 끌었다가 밖으로 나갔을 때도 그런지
- 설정에서 테마를 바꿨을 때 두 칸이 같이 따라오는지
