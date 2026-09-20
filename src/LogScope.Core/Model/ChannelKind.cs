namespace LogScope.Core.Model
{
    /// <summary>채널을 어떻게 그리고 어떻게 비교할지를 가르는 구분.</summary>
    public enum ChannelKind
    {
        /// <summary>0/1, TRUE/FALSE, ON/OFF, HIGH/LOW. 계단으로 그린다.</summary>
        Digital = 0,
        /// <summary>실수 값. 점을 이어서 그린다.</summary>
        Analog = 1,
        /// <summary>문자열 상태. 값은 States 배열의 인덱스이고 계단으로 그린다.</summary>
        State = 2,
    }
}
