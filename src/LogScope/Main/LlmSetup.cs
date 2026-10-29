using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// <b>LLM 분석 기능을 꽂는 곳</b> (1.45). 창이 켤 때 한 번 <see cref="Register"/> 를 부릅니다.
    ///
    /// 내 분석기(<see cref="ILlmAnalyzer"/> 를 맞춘 클래스)를 만든 뒤 아래 주석 한 줄을 풀어 이름만 바꾸면
    /// 메인 화면 [LLM 분석 실행] 이 그것을 부릅니다. 예:
    /// <code>
    ///   public sealed class MyLlmAnalyzer : ILlmAnalyzer
    ///   {
    ///       public string Analyze(LlmRequest request, LoadProgress progress)
    ///       {
    ///           progress.Report("입력 만드는 중", 0.1);
    ///           var doc = LogScope.LlmExport.LlmDocument.Build(request.State, request.DbDiff,
    ///                                                          new LogScope.LlmExport.LlmExportOptions(), null);
    ///           progress.ThrowIfCancelled();
    ///           progress.Report("서버에 보내는 중", 0.4);
    ///           string answer = ...;   // 사내 LLM 서버로 보내고 받기
    ///           progress.Report("받음", 1.0);
    ///           return answer;          // 메인 화면 종합 분석 결과 칸에 그대로 보임
    ///       }
    ///   }
    /// </code>
    /// </summary>
    public static class LlmSetup
    {
        public static void Register()
        {
            // 1.48: 주석을 풀었습니다. OverallLlmAnalyzer 는 사용자가 LlmExport 폴더(저장소에 없음)에 만든
            // 분석기입니다 — 그 폴더가 없거나 이름이 바뀌면 여기서 빌드가 깨집니다.
            LlmLink.Analyzer = new LogScope.LlmExport.OverallLlmAnalyzer();
        }
    }
}
