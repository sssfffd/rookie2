using System;

namespace LogScope.Analysis1.Align
{
    /// <summary>
    /// <b>분석 1 — 시간 맞추기 판 (그래프 · 히트맵이 같이 씀)</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Analysis1/Align/AlignPanel.xaml
    /// </summary>
    public static class AlignTexts
    {
        // ---- AlignPanel ----

        /// <summary>글</summary>
        public const string Heading = "시간 맞추기";
        /// <summary>글</summary>
        public const string Note = "고른 IO 가 어떤 값이 되는 순간이 겹치도록 이후 로그를 밉니다. 시작 시각이 달라도 사건끼리 맞춰집니다.";
        /// <summary>글</summary>
        public const string AlignChoicesLabel = "기준 IO";
        /// <summary>글</summary>
        public const string Label = "기준 값";
        /// <summary>글</summary>
        public const string Label2 = "어느 순간";
        /// <summary>글</summary>
        public const string LevelIndexLabel = "몇 번째";
        /// <summary>목록 마우스를 올리면 뜨는 설명</summary>
        public const string LevelIndexTip = "그 IO 에 실제로 나온 값들입니다. 0~5 로 움직이는 단계 신호면 5 도 고를 수 있습니다.\n값 종류가 너무 많은 아날로그 IO 만 문턱 하나로 위/아래를 가릅니다.";
        /// <summary>목록 마우스를 올리면 뜨는 설명</summary>
        public const string EdgeIndexTip = "[그 값이 될 때] 는 처음부터 그 값인 로그도 잡습니다 — 그 로그에서는 시작 지점이 그 순간입니다.";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string OccurrenceTextTip = "같은 일이 여러 번 일어날 때 몇 번째로 맞출지. 1 이 첫 번째입니다.";
        /// <summary>단추 이름</summary>
        public const string ClearAlign = "맞추기 해제";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ClearAlignTip = "기준 IO 를 지우고 시작 시각 자동 맞춤으로 돌아갑니다.";
        /// <summary>단추 이름</summary>
        public const string Close = "닫기";
    }
}
