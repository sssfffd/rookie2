# 106 — 분석 3 (Python 프로그램) 을 사내 AI 로 C# 으로 옮길 때 올릴 파일 정리

> 내가 분석 3은 이미 개발한 프로그램이 있음 근데 python으로 개발하고 지금 우리가 개발한 플젝과는
> 구조나 UI, 사용기술들이 다르단 말임 / 그래서 사내 ai를 활용해서 프로그램을 다시 C#으로 개조해서
> 붙이고 싶음 / 사내 ai에 이미 개발한 프로젝트 코드를 넣긴 할껀데 지금 우리 git에 올라와있는 코드를
> 다 올리진 못해서 어떤 내용을 올려야될지 너가 정리해주셈 그 git 대화내역에도 올려주고

정리한 문서: **[`docs/ANALYSIS3-PORTING.md`](../docs/ANALYSIS3-PORTING.md)** — 사내 AI 에 이 문서도 같이 올립니다.

## 요약

```
Python 원본 + A 묶음(약 3,000 줄) + 이 문서  ──▶ 사내 AI ──▶ src/LogScope/Analysis3/*.cs, *.xaml
                                                              │
                                     Claude 가 붙임 (4 장) ◀──┘  ShellWindow · MainVm · config.txt · csproj
```

| 묶음 | 내용 | 줄 |
|---|---|---|
| **A 꼭** | 이 문서 · 화면 지도 README · PlaceholderView · **분석 2 화면(DbView/DbVm/DbTexts) = 본보기** · Common 바탕(ObservableObject · RelayCommand · Converters · BackgroundJob · LoadingWindow · CardTag) · Themes(Controls · Light · Dark) · ShellWindow.xaml | ≈ 3,000 |
| **B 필요할 때** | 메인 칸에 결과 → AnalysisCardVm · MainVm / 설정 저장 → AppSettings · SettingsStore / CSV·XLSX → Common 읽개 6 개 / 그래프 직접 그림 → HeatmapCanvas · Palette | 기능마다 |
| **C 안 올림** | Analysis1 전부 · 분석 2 계산 · Tests · scripts · claude-log · 루트 README · REQUIREMENTS | — |

## 문서에 담은 것

1. 올릴 파일 표 (A · B · C, 파일마다 줄 수와 이유)
2. 사내 AI 가 지킬 규칙 — VS2017 · C# 7.3 · .NET 4.6.1 · NuGet 없음, **Python → C# 대신하는 표**
   (pandas → CsvReader/XlsxReader, numpy → float[], matplotlib → OnRender, threading → BackgroundJob …),
   폴더 = 이름 공간, 화면 글자는 `Analysis3Texts.cs`, 계산 파일은 WPF 없이, 색은 `DynamicResource Brush.*`,
   SQL 실행 안 함 · 자격 증명 저장 안 함
3. **사내 AI 에 그대로 붙여 쓸 요청문**
4. 받아 온 코드를 붙이는 자리 — `ShellWindow.xaml` 218 줄, `ShellWindow.xaml.cs` 86 줄, `MainVm.cs` 35 줄,
   `config.txt`, csproj
5. 받은 코드 확인 목록 (C# 8 문법 · 바깥 dll · 한글이 XAML 에 박힘 등)

## 다음

사내 AI 결과 파일을 주면 `Analysis3/` 에 넣고 4 장대로 붙인 뒤 `check_compile.sh` ·
`check_sources.py` 로 검사합니다. XAML 컴파일은 여기서 못 하니 Windows `build.bat` 결과를 알려 주세요.
