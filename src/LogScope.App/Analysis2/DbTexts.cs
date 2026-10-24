using System;

namespace LogScope.App.Analysis2
{
    /// <summary>
    /// <b>분석 2 — DB 비교</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope.App/Analysis2/DbView.xaml
    /// </summary>
    public static class DbTexts
    {
        // ---- DbView ----

        /// <summary>단추 이름</summary>
        public const string Read = "데이터 읽기";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ReadTip = "아래 두 칸을 읽어 견줍니다.";
        /// <summary>단추 이름</summary>
        public const string ShowScript = "SQL 문 출력";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowScriptTip = "지금 차이로 맞추는 SQL 을 만들어 창에 띄웁니다. 그 창에서 복사하거나 파일로 저장할 수 있습니다. 이 프로그램이 돌리지는 않습니다.";
        /// <summary>글</summary>
        public const string Text = "이전";
        /// <summary>단추 이름</summary>
        public const string PickPath = "폴더";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string PickPathTip = "폴더를 고릅니다 (그 안의 .sql / .csv / .frm 을 하위 폴더까지 읽습니다).";
        /// <summary>단추 이름</summary>
        public const string PickFile = "파일";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string PickFileTip = "파일 하나를 고릅니다 (.sql 덤프 · .csv · .frm).";
        /// <summary>글</summary>
        public const string Text2 = "이후";
        /// <summary>글</summary>
        public const string Label = "차이 목록";
        /// <summary>글</summary>
        public const string Note = "IO 검색";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string SearchTip = "IO 이름으로 찾습니다 (그래프 · 히트맵 검색과 같습니다).";
        /// <summary>단추 이름</summary>
        public const string ClearSearch = "✕";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ClearSearchTip = "검색 지우기";
        /// <summary>글</summary>
        public const string Text3 = "⚠";
        /// <summary>글</summary>
        public const string ViewAllDiffsLabel = "보기";
        /// <summary>고르기 단추 이름</summary>
        public const string ViewAllDiffs = "차이 발생";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ViewAllDiffsTip = "차이가 있는 줄만 — 값 차이(빨강) · 생김 · 없어짐 · 열 차이 (같은 IO 는 뺍니다).";
        /// <summary>고르기 단추 이름</summary>
        public const string ViewEveryIo = "전체";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ViewEveryIoTip = "두 쪽이 같은 IO 까지 모두 보여 줍니다 — \"이 IO 는 봤고 괜찮았다\" 를 확인하는 자리입니다.\n같은 줄은 차이 뒤에 옵니다. 표 하나에서 2 만 개까지 담습니다 (넘으면 그렇다고 적습니다).";
        /// <summary>글</summary>
        public const string Note2 = "값이 달라진 줄";
        /// <summary>글</summary>
        public const string Note3 = "차이 없는 줄";
        /// <summary>단추 이름</summary>
        public const string SaveList = ".CSV로 저장";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string SaveListTip = "차이 목록 전체를 칸 그대로 저장합니다 — 걸러 보는 중이어도 감춘 줄과 알림까지 다 나갑니다. 거르고 정렬하는 일은 엑셀이 이 화면보다 훨씬 잘합니다.";
    }
}
