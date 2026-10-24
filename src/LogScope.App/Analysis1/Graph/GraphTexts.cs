using System;

namespace LogScope.App.Analysis1.Graph
{
    /// <summary>
    /// <b>분석 1 — 그래프</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope.App/Analysis1/Graph/GraphView.xaml
    /// </summary>
    public static class GraphTexts
    {
        // ---- 정렬 (단계) — 도구 줄의 단계 묶음 ----
        //
        // 단계 이름 앞뒤에 붙는 글("1단계" 의 "단계")은 여기가 아니라
        // src/LogScope.Core/Model/StepText.cs 입니다 — 그 이름은 히트맵도 써서
        // 화면 쪽이 아닌 Core 에 있어야 합니다.

        /// <summary>단계 묶음의 맨 왼쪽 이름</summary>
        public const string StepCaption = "정렬";
        /// <summary>켜고 끄는 단추 이름 — 단계마다 칸을 나눠 나란히</summary>
        public const string StepEach = "단계마다";
        /// <summary>단계 목록 맨 위 (단계를 안 고른 상태)</summary>
        public const string StepWhole = "전체 구간";
        /// <summary>커서 값 — 그 로그의 그 단계가 이미 끝난 자리 (단계마다 칸의 꼬리)</summary>
        public const string NoValue = "— (단계 끝)";

        // ---- 왼쪽: IO 목록과 그룹 ----

        /// <summary>글</summary>
        public const string Heading = "IO 목록";
        /// <summary>단추 이름</summary>
        public const string ClearSearch = "검색 지우기";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string SearchTip = "IO 이름으로 찾습니다.";
        /// <summary>목록 항목 이름</summary>
        public const string ScopeAll = "전체 IO";
        /// <summary>목록 항목 이름</summary>
        public const string ScopePicked = "고른 IO만";
        /// <summary>목록 항목 이름</summary>
        public const string ScopeChanged = "차이 발생 IO";
        /// <summary>단추 이름</summary>
        public const string SelectAll = "전체 선택";
        /// <summary>단추 이름</summary>
        public const string ClearAll = "전체 해제";
        /// <summary>글</summary>
        public const string ShowBeforeLabel = "그릴 로그";
        /// <summary>체크 이름</summary>
        public const string ShowBefore = "이전";
        /// <summary>체크 마우스를 올리면 뜨는 설명</summary>
        public const string ShowBeforeTip = "이전 로그의 선을 그립니다.\n끄면 세로 눈금도 켜 둔 쪽에만 맞춰집니다.";
        /// <summary>체크 이름</summary>
        public const string ShowAfter = "이후";
        /// <summary>체크 마우스를 올리면 뜨는 설명</summary>
        public const string ShowAfterTip = "이후 로그의 선을 그립니다.\n끄면 세로 눈금도 켜 둔 쪽에만 맞춰집니다.";
        /// <summary>단추 이름</summary>
        public const string AddGroup = "새 그룹 만들기";
        /// <summary>단추 이름</summary>
        public const string RenameGroup = "이름 바꾸기";
        /// <summary>단추 이름</summary>
        public const string MoveGroupUp = "▲";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string MoveGroupUpTip = "고른 그룹을 위로 옮기기";
        /// <summary>단추 이름</summary>
        public const string MoveGroupDown = "▼";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string MoveGroupDownTip = "고른 그룹을 아래로 옮기기";
        /// <summary>단추 이름</summary>
        public const string PutSelected = "고른 IO 담기";
        /// <summary>단추 이름</summary>
        public const string DeleteGroup = "그룹 삭제";
        /// <summary>단추 이름</summary>
        public const string CopyGroupFile = "groups.txt 글 복사";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string CopyGroupFileTip = "지금 묶어 둔 그룹을 groups.txt 꼴로 클립보드에 담습니다.\n저장소의 groups.txt 에 붙여넣고 빌드하면 그 그룹으로 켜집니다.";
        /// <summary>칸 마우스를 올리면 뜨는 설명</summary>
        public const string RowTip = "누르면 접혔다 펴집니다. IO 를 끌어다 놓으면 이 그룹에 들어갑니다.";
        /// <summary>체크 마우스를 올리면 뜨는 설명</summary>
        public const string IsAllSelectedTip = "이 그룹의 IO 를 모두 켜거나 끕니다.";

        // ---- 오른쪽: 도구 줄 + 그래프 ----

        /// <summary>글</summary>
        public const string LaneModeCaption = "보기";
        /// <summary>고르기 단추 이름</summary>
        public const string LaneMode = "Lane";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string LaneModeTip = "IO 마다 한 줄씩 따로 그립니다. 줄마다 세로 눈금이 따로라 값 크기가 달라도 각각 꽉 차게 보입니다.";
        /// <summary>고르기 단추 이름</summary>
        public const string OverlayMode = "Overlay";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string OverlayModeTip = "한 판에 겹쳐 그리고 세로 눈금을 같이 씁니다. 값 크기가 비슷한 IO 끼리 견줄 때 씁니다.";
        /// <summary>글</summary>
        public const string ScaleRawCaption = "세로 눈금";
        /// <summary>고르기 단추 이름</summary>
        public const string ScaleRaw = "Value";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ScaleRawTip = "이전 로그와 이후 로그를 각각 그립니다. 로그에 적힌 값 그대로.";
        /// <summary>고르기 단추 이름</summary>
        public const string ScaleDelta = "Difference";
        /// <summary>고르기 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ScaleDeltaTip = "두 로그의 차이를 선 하나로 그립니다.\n같으면 0, 이후가 1 크면 +1, 1 작으면 -1.\n상태 채널은 이름이 같으면 0, 다르면 1 입니다.\n한쪽에만 있는 IO 는 견줄 상대가 없어 비어 있습니다.";
        /// <summary>글</summary>
        public const string ZoomTimeInCaption = "Time Scale";
        /// <summary>단추 이름</summary>
        public const string ZoomTimeIn = "+";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ZoomTimeInTip = "시간축 확대. 휠로도 됩니다.";
        /// <summary>단추 이름</summary>
        public const string ZoomTimeOut = "−";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ZoomTimeOutTip = "시간축 축소. 휠로도 됩니다.";
        /// <summary>단추 이름</summary>
        public const string ResetTime = "초기화";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ResetTimeTip = "전체 구간으로 되돌립니다 (Home).";
        /// <summary>글</summary>
        public const string ZoomValueInCaption = "Value Scale";
        /// <summary>단추 이름</summary>
        public const string ZoomValueIn = "+";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ZoomValueInTip = "값 축 확대. Shift+휠로도 됩니다.";
        /// <summary>단추 이름</summary>
        public const string ZoomValueOut = "−";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ZoomValueOutTip = "값 축 축소. Shift+휠로도 됩니다.";
        /// <summary>단추 이름</summary>
        public const string ResetValueZoom = "초기화";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string ResetValueZoomTip = "값 배율을 처음으로 되돌립니다.";
        /// <summary>단추 이름</summary>
        public const string FitValueOnce = "구간 맞춤";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string FitValueOnceTip = "지금 보이는 시간 구간에 세로 눈금을 한 번 맞춥니다. 그 뒤로는 그대로 둡니다.";
        /// <summary>켜고 끄는 단추 이름</summary>
        public const string FitVisible = "계속 맞춤";
        /// <summary>켜고 끄는 단추 마우스를 올리면 뜨는 설명</summary>
        public const string FitVisibleTip = "켜 두면 시간축을 밀 때마다 세로 눈금을 다시 맞춥니다. 위아래 움직임이 심하면 끄고 쓰세요.";
        /// <summary>글</summary>
        public const string ShadeDifferenceCaption = "비교";
        /// <summary>켜고 끄는 단추 이름</summary>
        public const string ShadeDifference = "차이 영역 표시";
        /// <summary>켜고 끄는 단추 마우스를 올리면 뜨는 설명</summary>
        public const string ShadeDifferenceTip = "허용 오차를 넘은 구간을 칠합니다.\n[파형 분리 보기] 와 같이 켤 수 없습니다.";
        /// <summary>켜고 끄는 단추 이름</summary>
        public const string SeparateTraces = "파형 분리 보기";
        /// <summary>켜고 끄는 단추 마우스를 올리면 뜨는 설명</summary>
        public const string SeparateTracesTip = "이전과 이후를 위아래로 조금 벌려 그립니다. 두 선이 겹쳐 하나로 보일 때 씁니다.\n[차이 영역 표시] 와 같이 켤 수 없습니다.";
        /// <summary>글</summary>
        public const string RelativeTolerancePercentTextCaption = "허용 오차";
        /// <summary>글</summary>
        public const string Caption = "%";

        // ---- 정렬 (단계) (1.16) ----

        /// <summary>켜고 끄는 단추 마우스를 올리면 뜨는 설명</summary>
        public const string SplitByStepTip = "가로를 단계 칸 여러 개로 나눠 나란히 놓습니다. 칸마다 그 단계의 시작끼리 맞춥니다.\n그림 위 단계 이름을 누르면 그 단계만, 한 번 더 누르면 모든 칸.\n(잠겨 있으면: 두 로그를 열고 설정 [비교] 의 \"단계 IO\" 를 적으면 켜집니다)";
        /// <summary>목록 마우스를 올리면 뜨는 설명</summary>
        public const string PickIndexTip = "한 단계만 골라 봅니다. 두 로그는 그 단계의 시작끼리 맞춥니다.\n맨 위를 고르면 다시 전체입니다.";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string AlignOpenTip = "특정 IO 가 어떤 값이 되는 순간으로 두 로그의 시간을 맞춥니다.";
        /// <summary>스크롤 막대 마우스를 올리면 뜨는 설명</summary>
        public const string VerticalOffsetTip = "레인이 많을 때 위아래로 굴립니다.";
    }
}
