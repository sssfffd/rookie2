using System;

namespace LogScope.App.Texts
{
    /// <summary>
    /// <b>저장된 분석 보기 창 (메인 화면에서 엶)</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope.App/Shell/SavedResultWindow.xaml
    /// </summary>
    public static class SavedTexts
    {
        // ---- SavedResultWindow ----

        /// <summary>창 제목</summary>
        public const string WindowTitle = "저장된 분석";
        /// <summary>글</summary>
        public const string LogLabel = "그때 비교한 Log 파일";
        /// <summary>글</summary>
        public const string Text = "이전";
        /// <summary>글</summary>
        public const string Text2 = "이후";
        /// <summary>글</summary>
        public const string Label = "그때의 점수";
        /// <summary>글</summary>
        public const string Note = "분석 1";
        /// <summary>글</summary>
        public const string Note2 = "분석 2";
        /// <summary>글</summary>
        public const string Note3 = "분석 3";
        /// <summary>글</summary>
        public const string Text3 = "이 기록을 지금 분석과 나란히 놓고 봅니다.";
        /// <summary>단추 이름</summary>
        public const string Compare = "현재 분석과 비교";
        /// <summary>단추 이름</summary>
        public const string Close = "닫기";
    }
}
