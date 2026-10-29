using System;
using LogScope.Analysis2;

namespace LogScope.Common
{
    // =====================================================================
    //  메인 화면 "종합 분석 결과" ↔ 사용자가 만든 LLM 분석 기능 (1.45)
    //
    //   [LLM 분석 실행] ──▶ 창(ShellWindow) 이 LlmRequest 를 만들어
    //                       배경 스레드에서 LlmLink.Analyzer.Analyze(request, progress) 를 부름
    //   progress.Report("단계", 0~1) ──▶ 메인 화면 진행바 · 글 (화면 스레드로 넘겨 줌)
    //   return "결과 글"             ──▶ 메인 화면 종합 분석 결과 칸
    //   throw                        ──▶ 칸에 빨갛게 (예외 메시지)
    //   [취소] ──▶ progress.IsCancelled 가 참 → progress.ThrowIfCancelled() 로 빠져나오기
    //
    //  연결은 한 줄: 켤 때 한 번  LlmLink.Analyzer = new 내분석기();
    //  (넣는 자리: src/LogScope/Main/LlmSetup.cs 의 Register)
    //
    //  WPF 를 쓰지 않습니다 — 사용자 코드는 화면을 몰라도 됩니다.
    // =====================================================================

    /// <summary>
    /// 사용자가 만든 LLM 분석 기능이 맞출 모양 하나 (1.45).
    /// </summary>
    public interface ILlmAnalyzer
    {
        /// <summary>
        /// 종합 분석을 돌리고 <b>화면에 보일 결과 글</b>을 돌려줍니다.
        ///
        /// - <b>배경 스레드에서 불립니다.</b> 화면(WPF) 을 직접 만지지 마세요 — 진행은 progress 로만.
        /// - 진행: <c>progress.Report("서버로 보내는 중", 0.4)</c>. 0~1, 모르면 음수(-1 → 진행바가 왔다갔다).
        /// - 취소: [취소] 를 누르면 <c>progress.IsCancelled</c> 가 참. 틈틈이 <c>progress.ThrowIfCancelled()</c>.
        /// - 실패: 예외를 던지면 그 메시지가 칸에 빨갛게 나옵니다 (프로그램은 계속 돕니다).
        /// - 무엇을 넣을지는 <paramref name="request"/> 의 Use1~3 (체크 표시와 같음) 을 보고 고르세요.
        ///   예: <c>LlmExport.LlmDocument.Build(request.State, request.DbDiff, 옵션, progress).ToMarkdown()</c>
        /// </summary>
        string Analyze(LlmRequest request, LoadProgress progress);
    }

    /// <summary>[LLM 분석 실행] 을 누른 그 순간의 재료 (1.45).</summary>
    public sealed class LlmRequest
    {
        /// <summary>분석 1 (로그 비교) 결과가 있는지 — 이전 · 이후 로그를 둘 다 열고 비교했으면 참.</summary>
        public bool Use1;

        /// <summary>분석 2 (DB 비교) 결과가 있는지 — 이전 · 이후 DB 를 읽어 견줬으면 참.</summary>
        public bool Use2;

        /// <summary>
        /// 분석 3 결과가 있는지 — 분석 3 화면이 <see cref="Analysis3Link.Publish"/> 로 결과를 올렸으면 참.
        /// <b>Publish 를 안 부르면 이 프로그램은 분석 3 이 됐는지 알 수 없습니다.</b>
        /// </summary>
        public bool Use3;

        /// <summary>분석 이름 (config.txt). [0] = 분석 1.</summary>
        public string[] Titles = new string[3];

        /// <summary>
        /// 프로그램 상태 전체 — 분석 1 은 Before · After · Comparison, 분석 2 는 HasDbResult · DbChanged · DbChangedIos,
        /// 설정은 Settings. <b>읽기만 하세요</b> (배경 스레드라 바꾸면 화면과 엇갈립니다).
        /// </summary>
        public AppState State;

        /// <summary>분석 2 결과 전체 (표 · 행 · 열 차이). 없으면 null.</summary>
        public DbDiffResult DbDiff;

        /// <summary>분석 3 이 올린 결과. 없으면 null.</summary>
        public Analysis3Result Analysis3;

        /// <summary>분석 3 의 이전 · 이후 자리.</summary>
        public string Analysis3Before = string.Empty;
        public string Analysis3After = string.Empty;

        /// <summary>누른 시각.</summary>
        public DateTime RequestedAt = DateTime.Now;

        /// <summary>하나라도 결과가 있는지 (없으면 버튼이 꺼져 있음).</summary>
        public bool Any { get { return Use1 || Use2 || Use3; } }
    }

    /// <summary>LLM 분석 기능을 꽂는 자리 (1.45).</summary>
    public static class LlmLink
    {
        /// <summary>
        /// 사용자가 만든 LLM 분석 기능. 비어 있으면 [LLM 분석 실행] 이 "연결된 기능이 없습니다" 를 알립니다.
        /// 켤 때 한 번 넣으세요 — <c>src/LogScope/Main/LlmSetup.cs</c> 의 Register.
        /// </summary>
        public static ILlmAnalyzer Analyzer;
    }
}
