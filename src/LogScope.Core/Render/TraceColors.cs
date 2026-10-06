namespace LogScope.Core.Render
{
    /// <summary>
    /// 선 색이 <b>무엇을 가리키는지</b> 정하는 규칙 한 줄.
    ///
    /// 두 가지 뜻 중 하나입니다.
    ///
    ///   거짓 — 색이 <b>이전/이후</b>를 가리킵니다. 이전은 파랑, 이후는 빨강.
    ///   참   — 색이 <b>IO</b> 를 가리킵니다. 같은 IO 의 이전/이후는 같은 색의
    ///          옅고 짙음으로 나뉩니다.
    ///
    /// 겹쳐 보기에서 IO 가 여럿이면 뒤쪽입니다. 모두 파랑·빨강으로 그리면
    /// 어느 선이 어느 IO 인지 알 수 없습니다. 레인으로 나눠 보면 IO 마다 칸이
    /// 따로 있으니 색으로 IO 를 가릴 필요가 없고, 겹쳐 보기라도 IO 가 하나면
    /// 가릴 것이 없습니다.
    ///
    /// <b>이 한 줄을 Core 에 둔 이유.</b> 선을 그리는 쪽(GraphCanvas)과
    /// 범례를 적는 쪽(GraphVm)이 각자 판단하고 있었습니다. 그러면 조건을 한쪽만
    /// 고친 날부터 <b>범례가 "파란 선은 이전" 이라고 하면서 선은 다른 색</b>인
    /// 일이 생깁니다. 거짓말하는 범례는 범례가 없는 것보다 나쁩니다 — 없으면
    /// 물어보고, 있으면 믿습니다.
    ///
    /// 화면이 없어도 돌아가는 규칙이라 여기서 시험할 수 있습니다.
    /// </summary>
    public static class TraceColors
    {
        /// <param name="laneMode">IO 마다 칸을 따로 두고 그리는 중인지.</param>
        /// <param name="channelCount">지금 그리는 IO 수.</param>
        public static bool ByIo(bool laneMode, int channelCount)
        {
            return !laneMode && channelCount > 1;
        }

        /// <summary>파랑/빨강 범례를 보여도 되는지. <see cref="ByIo"/> 의 반대입니다.</summary>
        public static bool ShowSideLegend(bool laneMode, int channelCount)
        {
            return !ByIo(laneMode, channelCount);
        }
    }
}
