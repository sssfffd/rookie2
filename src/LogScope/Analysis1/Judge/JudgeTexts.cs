using System;

namespace LogScope.Analysis1.Judge
{
    /// <summary>
    /// <b>분석 1 — 오차 목록</b> 의 글자 (1.56).
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Analysis1/Judge/JudgeView.xaml
    /// </summary>
    public static class JudgeTexts
    {
        /// <summary>제목</summary>
        public const string Heading = "오차 목록";
        /// <summary>제목 옆 설명</summary>
        public const string HeadingNote = "한 줄을 누르면 그 오차를 허용할지 고릅니다. 허용한 오차는 판정에서 빠집니다.";
        /// <summary>판정 칸 이름</summary>
        public const string VerdictLabel = "분석 1 판정";
        /// <summary>까닭 칸 이름</summary>
        public const string ReasonsLabel = "판정 까닭 (허용 안 된 오차 중 나쁜 것부터)";
        /// <summary>규칙 칸 이름</summary>
        public const string RulesLabel = "앞으로도 허용한 것";
        /// <summary>규칙이 없을 때</summary>
        public const string RulesEmpty = "아직 없습니다. 오차를 눌러 \"앞으로도 허용\" 을 고르면 여기에 쌓입니다.";
        /// <summary>단추 — 규칙 지우기</summary>
        public const string RemoveRule = "지우기";
        /// <summary>단추 설명 — 규칙 지우기</summary>
        public const string RemoveRuleTip = "이 허용을 지웁니다. 다음 판정부터 다시 오차로 뜹니다.";
        /// <summary>판정이 없을 때</summary>
        public const string Empty = "아직 판정이 없습니다. 메인 화면 분석 1 칸에 이전 · 이후 로그를 넣고 [분석] 을 눌러 주세요.";
        /// <summary>오차가 하나도 없을 때</summary>
        public const string NoItems = "오차가 없습니다. 모든 IO 가 시간 여유 띠 안에 있습니다.";
        /// <summary>목록 맨 아래 — 줄이 너무 많아 잘랐을 때 (앞에 개수가 붙음)</summary>
        public const string MoreRows = "개는 목록에 다 싣지 않았습니다 (판정에는 모두 들어감). 큰 것부터 보입니다.";
        /// <summary>허용 상태 글</summary>
        public const string AllowedOnce = "허용됨 · 이번만";
        /// <summary>허용 상태 글</summary>
        public const string AllowedRule = "허용됨 · 규칙";

        // ---- 허용 고르는 창 (AllowPrompt) ----

        /// <summary>창 제목</summary>
        public const string PromptTitle = "오차 허용";
        /// <summary>고르기</summary>
        public const string ChoiceNone = "허용 안 함";
        /// <summary>고르기</summary>
        public const string ChoiceOnce = "이번만 허용 — 이 로그 짝에서만";
        /// <summary>고르기 (앞에 크기 설명이 붙음)</summary>
        public const string ChoiceAlways = "앞으로도 허용 — 같은 IO · 같은 단계에서 이 크기까지";
        /// <summary>고르기 (앞에 IO 이름 · 개수가 붙음)</summary>
        public const string ChoiceIoOnce = "이 IO 의 허용 안 된 오차 모두 이번만 허용";
        /// <summary>앞으로도 허용을 못 고를 때</summary>
        public const string AlwaysNotHere = "단계가 없어지거나 차례가 바뀐 것은 \"이번만\" 만 됩니다.";
        /// <summary>허용된 오차를 눌렀을 때</summary>
        public const string ChoiceUnallow = "허용 풀기 — 다시 오차로";
        /// <summary>허용된 오차를 눌렀을 때</summary>
        public const string ChoiceKeep = "그대로 두기";
        /// <summary>단추</summary>
        public const string Ok = "확인";
        /// <summary>단추</summary>
        public const string Cancel = "취소";
    }
}
