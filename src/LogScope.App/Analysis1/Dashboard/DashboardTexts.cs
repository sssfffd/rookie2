using System;

namespace LogScope.App.Analysis1.Dashboard
{
    /// <summary>
    /// <b>분석 1 — 대시보드</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope.App/Analysis1/Dashboard/DashboardView.xaml
    /// </summary>
    public static class DashboardTexts
    {
        // ---- DashboardView ----

        /// <summary>글</summary>
        public const string Heading = "대시보드";
        /// <summary>글</summary>
        public const string Note = "이전 로그 IO 개수";
        /// <summary>글</summary>
        public const string Note2 = "이후 로그 IO 개수";
        /// <summary>글</summary>
        public const string Note3 = "차이 발생 IO 개수";
        /// <summary>글</summary>
        public const string Note4 = "표본 수 (이전 / 이후)";
        /// <summary>글</summary>
        public const string Section = "차이가 난 IO 목록   (한 줄을 누르면 그래프 화면으로, 제목 칸을 누르면 그 기준으로 정렬)";
        /// <summary>글</summary>
        public const string Section2 = "이전 로그에만 있는 IO";
        /// <summary>글</summary>
        public const string Note5 = "없음";
        /// <summary>글</summary>
        public const string Section3 = "이후 로그에만 있는 IO";
    }
}
