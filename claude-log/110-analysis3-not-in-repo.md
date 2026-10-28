# 110 — 새 버전을 받아도 내가 만든 Analysis3 파일이 안 사라지게

> 아 그리고 내가 직접 visual studio로 anlaysis3 쪽에 파일들 추가해서 구현했거든? 근데 이제 git에서
> 업데이트 버전 다운받을 때 마다 사라질꺼 아니야 이거 어떻게 매번 넣을까( 자동화 프로그램까진
> 필요없고 비교적 간단하게 넣을 수 있는 방법)

## 무엇이 문제였나

1.27~1.29 에는 저장소의 `Analysis3` 폴더에 자리 화면 `Analysis3View.xaml(.cs)` 가 들어 있었습니다.
그래서 새 버전을 기존 폴더 위에 풀면 **사용자의 Analysis3View 를 자리 화면이 덮어썼고**, 새 폴더에
풀면 폴더째 없었습니다.

## 바꾼 것 (1.30)

| | 전 | 후 |
|---|---|---|
| `src/LogScope/Analysis3/` | 저장소에 자리 화면 2 개 | **저장소에 없음** (`.gitignore`) — 사용자 폴더 |
| 자리 화면 | `Analysis3/Analysis3View` | `Shell/Analysis3Placeholder` (Analysis3View 가 없을 때만) |
| 창이 분석 3 을 띄우는 법 | XAML 에 `a3:Analysis3View` 를 적음 (없으면 빌드 깨짐) | 빈 자리(`A3Host`)에 **이름으로 찾아** 넣음, 없으면 자리 화면, 만들다 오류면 그 까닭을 빨갛게 |
| 검사 | Analysis3View 가 있어야 함 | 폴더가 비어도 됨 |

## 쓰는 법

```
 새 버전 (zip / git)          내 작업 폴더
 Main · Shell · …  ──덮음──▶  Main · Shell · … (새것)
 (Analysis3 없음)             Analysis3 (그대로 — 내 파일)
```

- **zip**: 새 zip 을 지금 쓰는 폴더 위에 풀고("모두 예") 빌드. 새 폴더에 풀었으면 옛 `src\LogScope\Analysis3`
  폴더만 복사.
- **git**: `git pull`. 단 **이번 한 번만** pull 전에 `Analysis3` 를 다른 곳에 복사해 두고 pull 뒤 다시 넣기
  (저장소에서 자리 화면 두 파일을 지웠기 때문).
- 저장소에 없는 폴더라 그 PC 에만 있습니다 — 가끔 백업.

덮어 풀어도 되는 까닭: 저장소에서 지운 파일이 남아도 `Analysis3` 밖은 csproj 에 적힌 파일만 빌드합니다.

## 확인

- 리눅스 컴파일 · 시험 900 통과, 소스 · 처리기 · 이름 검사 통과.
- 실제 C# 7.3 컴파일러 + .NET 4.6.1 WPF 참조로 컴파일 — 오류 0.
- 검사용 WPF 흉내에 `ContentControl` 을 알려 줌 (`scripts/make_xaml_stubs.py`).
- **못 한 것**: Windows 에서 사용자 `Analysis3View` 가 실제로 뜨는지 (`build.bat` 후 분석 3 탭).

자세한 순서: [`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md) 4 장.
