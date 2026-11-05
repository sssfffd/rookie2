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

        // ---- 대시보드 (1.94) ----
        /// <summary>칸 머리 아래 "뜻" 앞 글 (1.92)</summary>
        public const string MeaningLabel = "뜻  ";
        public const string FinalVerdictTile = "최종 판정";
        public const string ScoreTile = "공정 일치율";
        public const string RateTile = "IO별 평균 오차율";
        public const string RateTileNote = "차이 난 IO 마다 로그 시간을 100 칸으로 나눠 오차가 걸친 칸 비율, 그 평균";
        public const string LengthTile = "공정 시간 일치율";
        public const string TimelineHeading = "시간대별 차이";
        public const string TimelineNote = "이전 로그 시간을 100 칸으로 — 막대 높이 = 그 칸에서 차이 난 IO 수";
        public const string LegendOver = "허용 오차 넘음";
        public const string LegendBig = "이전 값보다 100 % 넘게";
        public const string LegendGap = "이후 로그 없음";
        public const string TopHeading = "차이 큰 IO";
        public const string TopNote = "크기 반영 오차율 순 · 진한 막대 = 시간, 옅은 막대 = 크기 반영";
        public const string TopEmpty = "차이 난 IO 가 없습니다.";
        public const string GroupHeading = "그룹 상태";
        public const string GroupNote = "차이 IO / 전체";
        public const string GroupEmpty = "그룹이 없습니다 — 분석 1 그래프 화면에서 IO 를 묶을 수 있습니다.";
        public const string DetailHeading = "분석별 자세히 — 파일 넣기 · [분석] · 그룹별 요약";
        /// <summary>단추 이름</summary>
        public const string CardClick = "↗";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string CardClickTip = "이 분석 화면으로 넘어갑니다.";
        /// <summary>글</summary>
        public const string Text = "차이 발생";
        /// <summary>글</summary>
        public const string Text2 = "개";
        /// <summary>분석 1 점수 옆 (1.81) — "차이 5 / 120 IO" 의 앞 글</summary>
        public const string CountBlockLabel = "차이";
        /// <summary>분석 1 점수 옆 (1.81) — 뒤 글</summary>
        public const string CountBlockUnit = "IO";

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
        public const string GroupClickTip = "누르면 이 그룹의 IO 를 펼칩니다 (다시 누르면 접음). IO 를 누르면 구간별 오차가 나옵니다.";

        // ---- 분석 1 칸 펼치기 · 허용 (1.59) ----

        /// <summary>단추 이름 — 그룹 · IO · 오차 줄 오른쪽</summary>
        public const string ShowGraph = "그래프";
        /// <summary>단추 설명</summary>
        public const string GroupGraphTip = "이 그룹의 IO 만 켜고 그래프로 갑니다.";
        /// <summary>단추 설명</summary>
        public const string IoGraphTip = "이 IO 만 켜고 그래프로 갑니다.";
        /// <summary>단추 설명</summary>
        public const string ErrorGraphTip = "이 오차 구간을 그래프로 봅니다.";
        /// <summary>IO 줄 설명</summary>
        public const string IoClickTip = "누르면 이 IO 의 구간별 오차가 작은 창으로 뜹니다. 거기서 허용합니다.";
        /// <summary>오차 창 (1.62) — 닫기 단추</summary>
        public const string IoErrorsClose = "닫기";

        // ---- 분석 1 칸 알림 줄 · 조금 어긋난 IO 창 (1.74) ----
        /// <summary>알림 줄 머리</summary>
        public const string NoticeSteps = "공정 단계 (IO 아님)";
        /// <summary>알림 줄 머리</summary>
        public const string NoticeSimilar = "파형은 같고 조금 어긋난 IO";
        /// <summary>알림 줄 오른쪽</summary>
        public const string NoticeOpen = "보기 ›";
        /// <summary>알림 줄 오른쪽</summary>
        public const string NoticeSimilarOpen = "확인하고 허용 ›";
        /// <summary>알림 줄 마우스를 올리면 뜨는 설명</summary>
        public const string NoticeTip = "누르면 자세히 봅니다.";
        /// <summary>창 제목</summary>
        public const string SimilarTitle = "파형은 같고 조금 어긋난 IO";
        /// <summary>창 머리 아래 설명</summary>
        public const string SimilarHelp = "시간만 조금 밀리거나 높이만 조금 다른 IO 입니다. 판정은 오차로 셌습니다 — 정상 변동이면 골라서 허용하세요.\n허용하는 것은 이 제안이 붙은 오차 구간만입니다 (같은 IO 의 튐 같은 다른 오차는 그대로).";
        /// <summary>단추 이름</summary>
        public const string SimilarAllowOnce = "고른 것 이번만 허용";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string SimilarAllowOnceTip = "이 로그 짝에서만 허용합니다. 로그를 바꾸면 다시 오차로 셉니다.";
        /// <summary>단추 이름</summary>
        public const string SimilarAllowAlways = "고른 것 앞으로도 허용";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string SimilarAllowAlwaysTip = "같은 IO · 같은 단계에서 이만한 어긋남은 다음 로그에서도 허용합니다 (설정 창 [허용한 오차] 에서 지울 수 있음).";
        /// <summary>단추 이름</summary>
        public const string SimilarSelectAll = "모두 고르기";
        /// <summary>단추 이름</summary>
        public const string SimilarSelectNone = "모두 풀기";
        /// <summary>글 — 줄이 없을 때</summary>
        public const string SimilarNone = "조금 어긋난 IO 가 없습니다.";
        /// <summary>오차 창 — 오차가 없을 때</summary>
        public const string IoErrorsNone = "오차가 없습니다.";
        /// <summary>단추 이름</summary>
        public const string AllowOnce = "이번만 허용";
        /// <summary>단추 설명</summary>
        public const string AllowOnceTip = "이 로그 짝에서만 이 오차를 판정에서 뺍니다. 로그를 바꾸면 풀립니다.";
        /// <summary>단추 이름</summary>
        public const string AllowAlways = "앞으로도 허용";
        /// <summary>단추 설명</summary>
        public const string AllowAlwaysTip = "같은 IO · 같은 단계에서 이번에 본 크기(길이 · 벗어난 값)까지는 다음 비교에서도 저절로 허용합니다. 더 크면 다시 오차로 뜹니다. 설정 창 [허용한 오차] 에서 지웁니다.";
        /// <summary>단추 이름</summary>
        public const string Unallow = "허용 풀기";
        /// <summary>단추 설명</summary>
        public const string UnallowTip = "다시 오차로 셉니다. 이 오차를 덮는 \"앞으로도 허용\" 도 지웁니다.";

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
        /// <summary>단추 이름</summary>
        public const string DeleteSaved = "삭제";
    }
}
