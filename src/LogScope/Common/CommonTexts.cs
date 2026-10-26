using System;

namespace LogScope.Common
{
    /// <summary>
    /// <b>작은 창들 · 준비 중 화면</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Analysis3/Analysis3View.xaml (분석 3 자리) · src/LogScope/Common/LoadingWindow.xaml · src/LogScope/Common/TextInputWindow.xaml · src/LogScope/Common/TextViewWindow.xaml
    /// </summary>
    public static class CommonTexts
    {
        // ---- 분석 3 자리 (Analysis3View) ----

        /// <summary>글</summary>
        public const string PlaceholderNote = "아직 만들지 않았습니다.";
        /// <summary>글</summary>
        public const string PlaceholderLogScopeNote = "왼쪽 위 [LogScope] 를 누르면 다른 화면으로 넘어갈 수 있습니다.";

        // ---- LoadingWindow ----

        /// <summary>창 제목</summary>
        public const string LoadingWindowTitle = "읽는 중";
        /// <summary>단추 이름</summary>
        public const string LoadingCancel = "취소";

        // ---- TextInputWindow ----

        /// <summary>창 제목</summary>
        public const string InputWindowTitle = "이름";
        /// <summary>단추 이름</summary>
        public const string InputOk = "확인";
        /// <summary>단추 이름</summary>
        public const string InputButton = "취소";

        // ---- TextViewWindow ----

        /// <summary>창 제목</summary>
        public const string TextViewWindowTitle = "글 보기";
        /// <summary>단추 이름</summary>
        public const string TextViewCopy = "글 복사";
        /// <summary>단추 이름</summary>
        public const string TextViewSave = "파일로 저장";
        /// <summary>단추 이름</summary>
        public const string TextViewButton = "닫기";
    }
}
