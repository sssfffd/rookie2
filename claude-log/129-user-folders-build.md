# 129 — Analysis3 폴더를 넣어도 build.bat 되나 · LlmExport 구조를 바꿔도 되나

> 지금 소스코드에 analysis3폴더가 없잖아 내가 추가해도 build.bat돌아가나? 그리고 llmexport쪽 폴더에
> 파일구조들 내가 바꿨는데도 되나?

## Analysis3 — 됩니다 (1.27 · 1.30 부터)

`build.bat` 은 `LogScope.sln` 을 MSBuild 로 빌드하고, `LogScope.csproj` 맨 끝이 `tools/Analysis3.targets` 를
가져와 `src\LogScope\Analysis3\` 안의 `.cs` · `.xaml` · 그림을 **폴더에서 찾아** 넣습니다. 폴더가 없으면
자리 화면(Shell/Analysis3Placeholder)이 뜨고, 있으면 `LogScope.Analysis3.Analysis3View` 를 이름으로 찾아 띄웁니다.

주의: 이름 공간 `LogScope.Analysis3` · 맨 바깥 `Analysis3View` · `.resx` 와 바깥 dll(NuGet 등) 참조는 자동으로
안 들어감 (필요하면 Claude 에게) · `App.xaml` 은 가져오지 않음.

## LlmExport — 1.45 까지는 안 됐고, 1.46 부터 됩니다

1.45 까지 csproj 에 LlmExport `.cs` 9 개가 **이름으로** 적혀 있었습니다. 이름을 바꾸거나 지우면
"파일 없음"(CS2001), 더한 파일은 빌드에 안 들어갔습니다. 또 새 버전을 덮어 풀면 Claude 의 옛 파일이 되살아나
사용자 파일과 같은 클래스가 두 번 생깁니다.

바꾼 것 (1.46) — Analysis3 와 같은 방식:

| | 전 | 후 |
|---|---|---|
| csproj | LlmExport `.cs` 9 줄 | 지움 → `tools/LlmExport.targets` 를 가져옴 (`LlmExport\**\*.cs` · `*.xaml`, `sample\` · bin · obj 빼고) |
| 저장소 | LlmExport 파일 12 개 | **뺌** (`.gitignore`) — 사용자 폴더 |
| 폴더가 없을 때 | — | 빌드 됨 (LlmLink · LlmSetup 은 주석으로만 언급) |
| 검사 | — | `check_sources.py`: LlmExport.targets 를 ItemGroup 뒤에서 가져오는지 |

**이번 한 번만**: git 으로 받는다면 pull 전에 `src\LogScope\LlmExport` 를 다른 곳에 복사해 두고 pull 뒤 다시
넣기 (저장소에서 지운 파일을 git 이 지우려 함). zip 이면 덮어 풀면 끝 (zip 에 LlmExport 가 없음).
Claude 가 만든 원래 판은 git 기록(1.43 ~ 1.45)에 남아 있습니다.

확인: LlmExport 폴더가 있을 때 · 없을 때 둘 다 리눅스 컴파일 · 시험 946 통과, 실제 C# 7.3 컴파일러 오류 0.
MSBuild 의 와일드카드 · Remove 동작은 Windows `build.bat` 확인 필요 (Analysis3 와 같은 방식).
