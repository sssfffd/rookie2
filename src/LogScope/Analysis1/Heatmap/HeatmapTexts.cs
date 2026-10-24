using System;

namespace LogScope.Analysis1.Heatmap
{
    /// <summary>
    /// <b>분석 1 — 히트맵</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Analysis1/Heatmap/HeatmapView.xaml
    /// </summary>
    public static class HeatmapTexts
    {
        // ---- HeatmapView ----

        /// <summary>글</summary>
        public const string Heading = "히트맵";
        /// <summary>단추 이름</summary>
        public const string Recalculate = "히트맵 다시 계산";
        /// <summary>켜고 끄는 단추 이름</summary>
        public const string ByStep = "가로를 단계로";
        /// <summary>글</summary>
        public const string BucketIndexLabel = "칸 폭";
        /// <summary>글</summary>
        public const string RelativeTolerancePercentLabel = "허용 오차 (이전 값 대비 %)";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string RelativeTolerancePercentTip = "오차 = |이후 − 이전| / |이전| x 100. 기본 0.1%.\n칸에 적히는 퍼센트와 같은 잣대라 그대로 견주면 됩니다.";
        /// <summary>켜고 끄는 단추 이름</summary>
        public const string IncludeUnchanged = "차이 없는 IO도 보기";
        /// <summary>글</summary>
        public const string ShowValueLabel = "칸에 적을 값";
        /// <summary>고르기 단추 이름</summary>
        public const string ShowValue = "값 그대로";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowValueTip = "그 구간의 절대 차이 평균을 값 그대로 적습니다.";
        /// <summary>고르기 단추 이름</summary>
        public const string ShowPercent = "% (값 범위 대비)";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShowPercentTip = "이전 값 대비 몇 % 벌어졌는지. 설정한 허용 오차와 같은 잣대라 바로 견줄 수 있고,\n값 크기가 다른 IO 끼리도 견줄 수 있습니다.";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string AlignOpenTip = "특정 IO 가 어떤 값이 되는 순간으로 두 로그의 시간을 맞춥니다.\n그래프 화면과 같은 설정입니다.";
        /// <summary>글</summary>
        public const string WiderLabel = "칸 크기";
        /// <summary>단추 이름</summary>
        public const string Wider = "칸 넓게";
        /// <summary>단추 이름</summary>
        public const string Narrower = "칸 좁게";
        /// <summary>단추 이름</summary>
        public const string Taller = "줄 높게";
        /// <summary>단추 이름</summary>
        public const string Shorter = "줄 낮게";
        /// <summary>단추 이름</summary>
        public const string ClearFocus = "모든 IO 보기";
        /// <summary>글</summary>
        public const string Note = "정상 (허용 오차 안)";
        /// <summary>글</summary>
        public const string Note2 = "기록 없음";
        /// <summary>글</summary>
        public const string Note3 = "IO 검색";
        /// <summary>단추 이름</summary>
        public const string ClearSearch = "✕";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ClearSearchTip = "검색 지우기";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string SearchTip = "IO 이름으로 찾습니다 (그래프 화면의 검색과 같습니다).";
    }
}
