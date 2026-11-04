# 178 — 1.86 Esc 로 커서 지우기 · 같은 커서 다시 누르면 지움 · tolerances.txt

## 요청 (요청 205)

> 분석1에 Esc로 커서 제거 기능 추가 / 같은 커서 다시 클릭하면 커서 사라지게 / 그룹설정처럼 IO별 허용오차 설정한 것도 빼 줄 수 있음?

"그룹설정처럼 빼 주기" 는 groups.txt (손으로 고치는 파일, build.bat 이 out 으로 넣음, [글 복사] 단추) 와 같은 것을
IO 별 허용 오차에도 — 로 읽었습니다.

## 한 것

| 무엇 | 어떻게 | 파일 |
|---|---|---|
| Esc = 커서 지우기 | 창이 Esc 를 먼저 받으므로 (메인으로 가기) 그 앞에서: 분석 1 그래프 화면이고 커서가 있으면 A · B 를 지우고 끝. 커서가 없으면 예전처럼 메인으로 | `ShellWindow.xaml.cs`, `GraphView.xaml.cs` (`ClearCursors`), `GraphCanvas.cs` (`ClearCursors`, Delete 도 이것을 씀) |
| 같은 커서 다시 누르기 | 누른 자리 ± 5 px 가 그 줄의 시간으로 어디인지 재고 (`NearCursor`), 커서 A 가 그 안이면 A 를 지움. Shift + 누르기는 B 에 같은 규칙 | `GraphCanvas.cs` |
| tolerances.txt | `IO = 퍼센트, 절대값` 한 줄에 하나. 하나라도 정하면 그 파일이 주인 (켤 때 덮음), 전부 주석이면 아무것도 안 함. 틀린 칸은 빈 칸, 같은 IO 는 뒤의 줄, 등호는 마지막 것으로 가름 | `Settings/ToleranceFile.cs` (새), `App.xaml.cs` |
| 설정 창 | [IO 추가] 옆에 [tolerances.txt 글 복사], 파일이 주인일 때만 "다시 켜면 파일 쪽으로 돌아감" 글 | `SettingsWindow.xaml(.cs)`, `SettingsVm.cs`, `SettingsTexts.cs` |
| 빌드 | build.bat 이 out 에 tolerances.txt 가 없을 때만 넣음 (CP949 · CRLF 그대로, 영어 주석만 더함). 저장소 뿌리에 전부 주석인 빈 틀 | `build.bat`, `tolerances.txt` |

```
설정 창 · IO 별 허용 오차
 ┌ IO 이름 ──────┬ 퍼센트 ┬ 절대값 ┬──────┐
 │ TT-01         │ 20     │        │ 지우기│
 └───────────────┴────────┴────────┴──────┘
 [IO 추가] [tolerances.txt 글 복사]
 (파일이 주인일 때) IO 별 허용 오차는 tolerances.txt 가 정합니다 …
```

## 확인

- 시험 1149 통과 (새 10: 파일 읽기 — 퍼센트만 / 절대값만 / 틀린 줄 / 범위 밖 / 같은 IO / 이름에 등호 / 전부 주석,
  글 복사 → 다시 읽기 그대로, 같은 커서 가까움 · 멂 · 없음 · 칸 끝).
- check 스크립트 · 실제 csc 빌드 · XAML 형식 통과.
- **Windows 에서 못 본 것**: Esc · 다시 누르기 손맛, 설정 창 단추 · 글 배치, build.bat 실제 복사, 클립보드.
