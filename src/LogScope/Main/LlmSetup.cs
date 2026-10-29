using System;
using System.Reflection;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// <b>LLM 분석 기능을 꽂는 곳</b> (1.45). 창이 켤 때 한 번 <see cref="Register"/> 를 부릅니다.
    ///
    /// 내 분석기(<see cref="ILlmAnalyzer"/> 를 맞춘, 매개변수 없는 생성자가 있는 클래스)를 프로젝트 어디에든
    /// (보통 LlmExport 폴더) 만들면 <see cref="Register"/> 가 찾아 꽂습니다 (1.47) — 메인 화면
    /// [LLM 분석 실행] 이 그것을 부릅니다. 예:
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
            // 1.47: 주석으로 두었던 한 줄을 풀었습니다. 다만 클래스 이름을 여기 적으면 그 이름이 다를 때
            // 빌드가 깨지고(LlmExport 는 사용자 폴더라 이 파일이 그 안의 이름을 모름), 폴더가 없을 때도
            // 깨집니다. 그래서 이름 대신 <b>ILlmAnalyzer 를 맞춘 클래스를 프로그램 안에서 찾아</b> 꽂습니다.
            // 이름이 정해져 있으면 아래를 지우고 LlmLink.Analyzer = new 그이름(); 으로 바꿔도 됩니다.
            if (LlmLink.Analyzer != null) return;   // 다른 곳에서 이미 꽂았으면 그대로
            LlmLink.Analyzer = Find(typeof(LlmSetup).Assembly);
        }

        /// <summary>
        /// 어셈블리에서 <see cref="ILlmAnalyzer"/> 를 맞춘 클래스를 찾아 하나 만듭니다. 없으면 null.
        /// 여럿이면 LogScope.LlmExport 이름 공간의 것을 먼저, 그다음 이름 순 — 늘 같은 것이 고르게.
        /// 만들다 오류가 나면 null (버튼이 "연결된 기능이 없습니다" 를 알림 — 프로그램은 켜짐).
        /// </summary>
        public static ILlmAnalyzer Find(Assembly asm)
        {
            if (asm == null) return null;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }

            Type best = null;
            foreach (Type t in types)
            {
                if (t == null || !t.IsClass || t.IsAbstract || t.ContainsGenericParameters) continue;
                if (!typeof(ILlmAnalyzer).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                if (best == null || Better(t, best)) best = t;
            }
            if (best == null) return null;

            try { return (ILlmAnalyzer)Activator.CreateInstance(best); }
            catch (Exception) { return null; }
        }

        private static bool Better(Type a, Type b)
        {
            bool la = (a.Namespace ?? string.Empty).StartsWith("LogScope.LlmExport", StringComparison.Ordinal);
            bool lb = (b.Namespace ?? string.Empty).StartsWith("LogScope.LlmExport", StringComparison.Ordinal);
            if (la != lb) return la;
            return string.CompareOrdinal(a.FullName, b.FullName) < 0;
        }
    }
}
