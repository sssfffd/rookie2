namespace LogScope.LlmExport
{
    /// <summary>
    /// LLM 입력을 얼마나 담을지 (1.43). <b>숫자만 바꿔 다시 빌드하면 됩니다.</b>
    ///
    /// 크기 감 (한 줄 ≈ 글자 수):
    ///   분석 1 달라진 IO 한 줄 ≈ 100 자 · 사건 한 줄 ≈ 50 자 · 모양 한 줄 ≈ 90 자
    ///   분석 2 값 변화 한 줄 ≈ 60 자 · 바뀐 행 한 줄 ≈ 80 자
    ///
    /// 사내 LLM 의 입력 한도(토큰)를 먼저 확인하세요. 한국어 · 숫자는 1 자 ≈ 0.5~1 토큰입니다.
    /// 한도가 작으면 <see cref="MaxLevel"/> 을 1 이나 2 로, <see cref="MaxChars"/> 를 줄이세요.
    /// </summary>
    public sealed class LlmExportOptions
    {
        /// <summary>몇 층까지 담을지. 1 = 꼭 · 2 = 근거 · 3 = 자세히.</summary>
        public int MaxLevel = 3;

        /// <summary>
        /// Markdown 전체 글자 수 한도. 넘으면 3 층 → 2 층 순서로 표의 줄을 반씩 줄입니다
        /// (1 층은 줄이지 않음 — 결론의 근거라서). 0 이하면 한도 없음.
        /// </summary>
        public int MaxChars = 300000;

        // ---- 분석 1 ----

        /// <summary>달라진 IO 표의 줄 수 상한 (지표 큰 순).</summary>
        public int MaxChangedIos = 3000;

        /// <summary>시간대 표의 칸 수 — 비교 구간을 이만큼 같은 폭으로 자릅니다.</summary>
        public int TimeBins = 100;

        /// <summary>사건(디지털 · 상태가 바뀐 시각)을 적을 IO 수 상한.</summary>
        public int MaxEventIos = 300;

        /// <summary>IO 하나에서 적을 사건 수 상한 (앞에서부터).</summary>
        public int MaxEventsPerIo = 50;

        /// <summary>모양(구간별 최소 · 평균 · 최대)을 적을 아날로그 IO 수 (지표 큰 순).</summary>
        public int ShapeIos = 40;

        /// <summary>단계가 없을 때 모양을 볼 구간 수 (비교 구간을 같은 폭으로).</summary>
        public int ShapeSegments = 10;

        /// <summary>한쪽에만 있는 IO 이름 수 상한.</summary>
        public int MaxOneSidedIos = 500;

        // ---- 분석 2 ----

        /// <summary>IO 값 변화 표의 줄 수 상한.</summary>
        public int MaxDbIoChanges = 3000;

        /// <summary>표 하나에서 적을 바뀐 행(바뀐 칸) 수 상한.</summary>
        public int MaxChangedCellsPerTable = 200;

        /// <summary>표 하나에서 적을 추가 · 삭제된 행 키 수 상한.</summary>
        public int MaxKeysPerTable = 100;
    }
}
