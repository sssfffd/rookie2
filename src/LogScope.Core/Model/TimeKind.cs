namespace LogScope.Core.Model
{
    /// <summary>시간축 값이 무엇을 뜻하는지. 눈금 글자를 어떻게 쓸지 정한다.</summary>
    public enum TimeKind
    {
        /// <summary>시간을 읽어내지 못해 샘플 번호를 쓴다.</summary>
        Index = 0,
        /// <summary>단위 없는 숫자. TimeUnit 에 단위 글자가 있을 수 있다.</summary>
        Number = 1,
        /// <summary>자정 기준 경과 밀리초 (HH:MM:SS.mmm).</summary>
        ClockMs = 2,
        /// <summary>1970-01-01 기준 밀리초.</summary>
        DateMs = 3,
    }

    /// <summary>시트에서 IO 가 행으로 놓였는지 열로 놓였는지.</summary>
    public enum Orientation
    {
        /// <summary>배치를 자동으로 찾는다.</summary>
        Auto = 0,
        /// <summary>각 행이 IO 채널, 한 행이 시간축.</summary>
        Rows = 1,
        /// <summary>각 열이 IO 채널, 한 열이 시간축.</summary>
        Cols = 2,
    }
}
