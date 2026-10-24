# 105 — "BuildInfo 가 없대" → BuildInfo 를 평범한 코드 파일로

> build info가 없대

1.24 의 고침(빌드 맨 처음에 BuildInfo.g.cs 를 만들고 csproj 에 적어 둠)으로도 Windows 에서
"BuildInfo 가 없다" 가 났습니다. **빌드 도중에 코드를 만들어 끼워 넣는 방식 자체**가 VS 에서
믿을 수 없었습니다 — XAML 임시 빌드, VS 의 IntelliSense(디자인 타임) 빌드처럼 그 단계를
안 거치는 빌드가 여럿 있습니다.

## 바꾼 방식 (1.25)

```
1.24 까지                                  1.25
빌드 중 BuildInfo.g.cs (코드) 를 만듦        BuildInfo.cs = 저장소에 있는 평범한 코드 (늘 있음)
 → 그 단계를 안 거치는 빌드에선 없음           값은 프로그램 안의 글 파일(리소스)에서 실행할 때 읽음
                                              - version.txt   ← 저장소 루트 파일을 그대로 넣음
                                              - BuildStamp.txt ← 빌드할 때 obj 에 씀 (커밋|수정됨|시각)
```

- `BuildInfo` 클래스가 **코드 파일로 늘 있으므로** 어떤 빌드에서도 "없다" 가 날 수 없습니다.
- 버전은 여전히 `version.txt` **한 곳**입니다 (csproj 가 그 파일을 그대로 넣음).
- 커밋 글을 만드는 타깃(`tools/BuildInfo.targets`)은 이제 **코드를 만들지 않고** 글 파일만 씁니다.
  안 돌거나 git 이 없어도 컴파일 오류가 없고, 커밋이 "unknown" 으로 보일 뿐입니다. 모든 작업에
  `ContinueOnError` 를 걸었습니다.
- 확인: 리소스가 있을 때 `1.24 / abc123def456 / True / 2026-10-24 12:00`, 없을 때
  `0.00 / unknown / False /` — 둘 다 오류 없이 읽힘 (mono 로 따로 돌려 봄).

## 바뀐 파일

| 파일 | |
|---|---|
| `src/LogScope/BuildInfo.cs` (새) | Version · Commit · Modified · BuiltAt 를 리소스에서 읽음 |
| `src/LogScope/LogScope.csproj` | `BuildInfo.g.cs` → `BuildInfo.cs`, `version.txt` 를 EmbeddedResource 로 |
| `tools/BuildInfo.targets` | 코드 대신 `obj/…/BuildStamp.txt` 를 쓰고 리소스로 넣음 |
| `scripts/check_compile.sh` · `check_sources.py` | 가짜 BuildInfo 흉내 지움 (이제 진짜 파일을 컴파일) |
| `README.md` 6 장 | 설명 |

디스크에 1.24 가 만든 `src/LogScope/BuildInfo.g.cs` 가 남아 있으면 지워도 됩니다 (프로젝트에 안
들어감).

## Windows 에서 확인 못 한 것

- XAML 컴파일 · VS 빌드는 여기서 못 합니다. 또 오류가 나면 **오류 줄 전체(파일 · 줄 · CS/MC 번호)**
  를 붙여 주세요.
- 검사: mcs 컴파일 · 시험 877 통과 · `check_sources` 지적 없음.
