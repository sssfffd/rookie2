using System;

namespace LogScope.App.Texts
{
    /// <summary>
    /// <b>맨 위 메뉴 줄 (모든 화면 공통)</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope.App/Shell/ShellWindow.xaml
    /// </summary>
    public static class ShellTexts
    {
        // ---- 위쪽 메뉴 줄 (진한 파랑) ----

        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowMainTip = "점수와 저장된 분석을 봅니다.";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowAnalysis1Tip = "두 로그를 견주는 화면입니다.";
        /// <summary>고르기 단추 이름</summary>
        public const string ShowDashboard = "대시보드";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowDashboardTip = "요약 화면입니다 (F1).";
        /// <summary>고르기 단추 이름</summary>
        public const string ShowGraph = "그래프";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowGraphTip = "시간에 따른 파형을 봅니다 (F2).";
        /// <summary>고르기 단추 이름</summary>
        public const string ShowHeatmap = "히트맵";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowHeatmapTip = "시간 구간별로 어디서 차이가 났는지 한눈에 봅니다 (F3).";
        /// <summary>단추 이름</summary>
        public const string OpenBefore = "이전 로그 열기";
        /// <summary>단추 이름</summary>
        public const string OpenAfter = "이후 로그 열기";
        /// <summary>단추 이름</summary>
        public const string Recompare = "비교 다시 하기";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string RecompareTip = "허용 오차 등을 바꾼 뒤 누르세요 (F5).";
        /// <summary>단추 이름</summary>
        public const string Settings = "설정";
    }
}
