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
        /// <summary>글</summary>
        public const string Heading = "분석 결과";
        /// <summary>단추 이름 — 종합 분석 결과 칸 오른쪽 위 (1.43)</summary>
        public const string LlmExport = "LLM 입력 저장";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string LlmExportTip = "분석 1 · 2 · 3 결과를 사내 LLM 에 넣을 글(.md)과 구조(.json)로 저장합니다. 보내지는 않습니다.";
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
        /// <summary>글</summary>
        public const string LogNote = "점수를 매기는 규칙은 아직 정해지지 않았습니다. 지금은 Log 파일을 비교한 후에 100 점으로 둡니다.";
        /// <summary>글</summary>
        public const string Note = "점수 색";
    }
}
