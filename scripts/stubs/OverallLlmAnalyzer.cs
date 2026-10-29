using LogScope.Common;

namespace LogScope.LlmExport
{
    // 검사 전용 가짜 (1.48). 진짜 OverallLlmAnalyzer 는 사용자 폴더 src/LogScope/LlmExport (저장소에 없음)에
    // 있습니다. Main/LlmSetup.cs 가 그 이름을 부르므로, 폴더가 없는 곳(리눅스 검사)에서는 컴파일이 깨집니다.
    // scripts/check_compile.sh 는 진짜가 없을 때만 이 파일을 같이 컴파일합니다. 빌드(build.bat)에는 안 들어갑니다.
    public sealed class OverallLlmAnalyzer : ILlmAnalyzer
    {
        public string Analyze(LlmRequest request, LoadProgress progress) { return "검사용 가짜"; }
    }
}
