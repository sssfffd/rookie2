# 97 — 분석 2 IO 검색 · 히트맵 [차이 없는 IO도 보기] 바로 반영

> 분석 2에도 io검색 기능 넣어주셈
>
> 그리고 분석 1에 히트맵에서 차이없는 IO도 보는거 선택하면 바로 반영되게 하고

## 1. 분석 2 IO 검색

```
╭─ 차이 목록  IO 검색 [temp______][✕]  ⚠  보기 [차이 발생][전체]  … [.CSV로 저장] ─╮
│ IO명              이전 value   이후 value                                       │
│ TEMP_MAX          1            4  (+3)                                          │
│ TEMP_MIN          0            0                                                │
```

- 목록 머리 `차이 목록` 바로 옆입니다 (히트맵처럼 목록 바로 위).
- IO 이름에 그 글자가 들면 남깁니다 — 대소문자 안 가림, 치는 대로 바로 (다시 견주지
  않고 이미 지은 줄을 거르기만 함).
- [차이 발생] / [전체] 를 바꿔도 검색은 그대로 걸립니다.
- 맞는 IO 가 없으면 목록 가운데에 `"xxx" 이(가) 이름에 든 IO 가 없습니다.`
- **안 바뀌는 것**: 메인 화면 개수 · 판정, `[.CSV로 저장]` (전체 그대로).
- 메인 화면 요약에서 IO 를 눌러 넘어왔는데 검색에 걸려 안 보이면, 검색을 풀고
  그 IO 를 찾아 줍니다.

### 검색 규칙을 한 곳으로

그래프 IO 목록 · 히트맵 · 분석 2 가 각자 규칙을 들고 있었습니다(그래프만 앞뒤
빈칸을 안 지움). 이제 셋 다 `src/LogScope.Core/Text/NameSearch.cs` 하나를 씁니다 —
같은 글을 쳤는데 한 화면에서만 나오는 IO 가 생기지 않게.

## 2. 히트맵 [차이 없는 IO도 보기] — 누르면 바로

전에는 켜 두고 [히트맵 다시 계산] 을 눌러야 반영돼서, 눌러도 아무 일 없는 단추로
보였습니다. 이제 누르는 즉시 다시 셉니다 (진행 창이 잠깐 뜰 수 있음). 로그가 안
열려 있으면 조용히 넘깁니다.

## 바뀐 파일

| 파일 | 내용 |
|---|---|
| `Core/Text/NameSearch.cs` (새) · `LogScope.Core.csproj` | 검색 규칙 한 곳 |
| `App/Db/DbVm.cs` | `Search` · `_built`(거르기 전) · `ApplySearch` |
| `App/Db/DbView.xaml(.cs)` | 검색 칸 · ✕, 메인에서 넘어올 때 검색 풀기 |
| `Logs/Compare/HeatLayout.cs` · `App/Logs/ChannelListVm.cs` | `NameSearch` 를 씀 |
| `App/Logs/HeatmapView.xaml(.cs)` | 토글을 누르면 `Recalculate(false)` |
| `Tests/Program.cs` | `NameSearchRule` 7 개 (869 통과) |
| `README.md` | 설명 |

## Windows 에서 확인 못 한 것

- XAML 은 여기서 컴파일되지 않습니다 (스텁 + 검사기만 통과).
- 히트맵 토글을 누를 때 진행 창이 떴다 사라지는지 (큰 로그면 몇 초).
- 검사: 컴파일 · 테스트 869 통과 · `check_sources` / `check_handlers` /
  `check_shadowing` 지적 없음.
