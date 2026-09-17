using LogScope.Core.Model;

namespace LogScope.Core.Io
{
    /// <summary>
    /// 로그를 열 때의 선택지.
    ///
    /// 상한값은 "파일 크기 상한은 없게" 라는 요구에 맞춰 기본이 모두 무제한입니다.
    /// 다만 남이 준 파일을 열 때를 위해 걸 수 있는 자리는 남겨 뒀습니다.
    /// 0 이하이면 검사하지 않습니다.
    /// </summary>
    public sealed class OpenOptions
    {
        /// <summary>Auto 면 배치를 스스로 찾습니다. Rows/Cols 면 지정한 대로 읽습니다.</summary>
        public Orientation Orientation = Orientation.Auto;

        /// <summary>배치 판별에 쓸 앞쪽 줄 수. 머리말이 길면 늘리세요.</summary>
        public int ProbeRows = 200;

        /// <summary>0 이하 = 무제한.</summary>
        public int MaxChannels = 0;
        public int MaxSamples = 0;
        public long MaxCells = 0;

        /// <summary>xlsx 압축을 푼 총량 상한. 0 이하 = 무제한.</summary>
        public long MaxUncompressedBytes = 0;

        /// <summary>상태 채널 하나가 가질 수 있는 서로 다른 문자열 수. 0 이하 = 무제한.</summary>
        public int MaxStateValues = 4096;

        /// <summary>읽을 시트 이름. 비우면 첫 번째 시트.</summary>
        public string SheetName = null;

        public OpenOptions Clone()
        {
            return (OpenOptions)MemberwiseClone();
        }
    }
}
