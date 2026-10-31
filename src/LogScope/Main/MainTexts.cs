using System;

namespace LogScope.Main
{
    /// <summary>
    /// <b>메인 화면</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Main/MainView.xaml
    /// </summary>
    public static class MainTexts
    {
        // ---- 종합 분석 결과 ----

        /// <summary>글</summary>
        public const string Label = "종합 분석 결과";
        /// <summary>단추 이름 — 종합 분석 결과 칸 (1.45)</summary>
        public const string LlmRun = "LLM 분석 실행";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string LlmRunTip = "결과가 있는 분석(체크 표시)으로 LLM 종합 분석을 돌립니다. 하나만 있어도 됩니다.";
        /// <summary>단추 이름</summary>
        public const string LlmCancel = "취소";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string LlmCancelTip = "LLM 분석을 멈춥니다 (분석 기능이 취소를 받아 줄 때).";
        /// <summary>글 — 아직 결과가 없을 때</summary>
        public const string LlmEmpty = "분석 1 · 2 · 3 중 하나 이상을 마친 뒤 [LLM 분석 실행] 을 누르면 여기에 종합 분석 결과가 나옵니다.";
        /// <summary>알림 — LLM 분석 기능이 꽂혀 있지 않을 때</summary>
        public const string LlmNoAnalyzer = "연결된 LLM 분석 기능이 없습니다.\n\nsrc/LogScope/Main/LlmSetup.cs 의 Register 에서\nLlmLink.Analyzer = new 내분석기(); 를 넣어 주세요.";
        /// <summary>글</summary>
        public const string Heading = "분석 결과";
        /// <summary>단추 이름</summary>
        public const string CardClick = "↗";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string CardClickTip = "이 분석 화면으로 넘어갑니다.";
        /// <summary>글</summary>
        public const string Text = "차이 발생";
        /// <summary>글</summary>
        public const string Text2 = "개";

        // ---- 이 분석이 쓰는 로그 (분석 1) — 점수 바로 아래 (1.04) ----

        /// <summary>글</summary>
        public const string Text3 = "이전";
        /// <summary>글</summary>
        public const string Text4 = "이후";

        // ---- 분석 3 칸 (1.35) ----

        /// <summary>단추 이름 — 분석 3 칸 머리 (기능은 분석 3 쪽이 Analysis3Link.AnalyzeRequested 로 연결)</summary>
        public const string A3Analyze = "분석";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string A3AnalyzeTip = "분석 3 을 돌립니다.";
        /// <summary>단추 설명 — 분석 1 칸 [분석] (1.53)</summary>
        public const string LogAnalyzeTip = "이전 · 이후 로그를 읽고 분석 1 을 돌립니다.";
        /// <summary>단추 설명 — 분석 2 칸 [분석] (1.53)</summary>
        public const string DbAnalyzeTip = "이전 · 이후 DB 를 읽고 분석 2 를 돌립니다.";
        /// <summary>메인 칸에서 고르거나 놓기만 하고 아직 [분석] 을 안 눌렀을 때 줄 오른쪽에 적는 말 (1.53)</summary>
        public const string Waiting = "분석 대기";
        /// <summary>분석 1 [분석] 을 눌렀는데 이전 · 이후 중 빈 쪽이 있을 때</summary>
        public const string LogNeedBoth = "이전 · 이후 로그를 둘 다 넣은 뒤 [분석] 을 눌러 주세요.\n\n칸을 누르거나 파일을 끌어다 놓으면 됩니다.";
        /// <summary>분석 1 칸에 놓은 것 중 읽을 수 있는 로그가 없을 때</summary>
        public const string LogNotSupported = "읽을 수 있는 로그 파일이 아닙니다.";

        /// <summary>분석 3 의 이전/이후 칸을 아직 안 정했을 때 칸에 적는 말</summary>
        public const string A3NoneOnCard = "누르면 폴더 고르기 · 끌어다 놓기";
        /// <summary>단추에 연결된 기능이 없을 때 알림</summary>
        public const string A3NoHandler = "분석 3 쪽에 이 단추와 연결된 기능이 아직 없습니다.";

        // ---- 이 분석의 요약 (그룹별) ----

        /// <summary>글</summary>
        public const string GroupNoteLabel = "요약";

        // ---- 분석 2 의 요약 줄 ----

        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string CardIoClickTip = "분석 2 화면에서 이 IO 를 찾아 줍니다.";
        /// <summary>단추 마우스를 올리면 뜨는 설명 — 분석 3 칸의 요약 줄 (1.40)</summary>
        public const string CardA3LineTip = "분석 3 화면으로 넘어갑니다.";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string GroupClickTip = "이 그룹의 IO 만 히트맵에서 봅니다.";

        // ---- 저장된 분석 ----

        /// <summary>글</summary>
        public const string HistoryNoteLabel = "History";
        /// <summary>단추 마우스를 올리면 뜨는 설명 — History 한 줄</summary>
        public const string HistoryRowTip = "누르면 그때의 요약을 위 칸에 펼칩니다. 다시 누르면 지금 결과로 돌아옵니다.";
        /// <summary>단추 이름 — History 를 펼쳐 보는 중일 때 칸 위 띠</summary>
        public const string ShowLive = "지금 결과로";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowLiveTip = "History 를 접고 지금 분석 결과를 보여 줍니다.";
        /// <summary>단추 이름</summary>
        public const string SaveResult = "지금 결과 저장";
        /// <summary>글 마우스를 올리면 뜨는 설명</summary>
        public const string ScoreTextTip = "분석 1 / 2 / 3";
        /// <summary>단추 이름</summary>
        public const string DeleteSaved = "삭제";
    }
}
