using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Logs.Align;

namespace LogScope.Logs.Compare
{
    // 히트맵이 다루는 것들 — 칸 하나(HeatCell), 가로 한 줄(HeatRow),
    // 무엇을 어떻게 셀지(HeatmapOptions), 센 결과(HeatmapResult).
    //
    // 세는 코드는 옆 파일(HeatmapBuilder.cs)입니다. 530 줄 한 파일에
    // 같이 있었는데, 뜻을 적어 둔 주석이 길어서 (무엇을 차이로 볼지,
    // 왜 그 값으로 줄을 세우는지) 세는 코드를 찾으려면 그걸 다 지나가야
    // 했습니다.
    /// <summary>
    /// 한 칸(시간 구간 하나 x IO 하나)의 요약.
    ///
    /// 차이량으로 무엇을 쓸지가 중요합니다.
    ///  - 부호 있는 합계: +1 / -1 로 진동하면 0 이 되어 버려 차이를 놓칩니다.
    ///  - 절대값 합계: 구간이 길수록 무조건 커져서 구간끼리 비교가 안 됩니다.
    ///  - 절대 차이의 평균: 앞 둘을 피하지만, 잠깐 크게 튄 것이 묻힙니다.
    ///  - <b>가장 크게 벌어진 순간의 오차(%)</b>: 구간 길이에 휘둘리지 않고,
    ///    진동해도 0 이 되지 않고, 튄 것도 묻히지 않습니다. 무엇보다 설정에
    ///    적는 허용 오차와 <b>같은 잣대</b>라 눈으로 바로 견줄 수 있습니다.
    ///    그래서 칸에 적는 숫자는 이 값입니다.
    /// 평균 · 최대 · RMS 도 함께 담아 설명 줄에 보여 줍니다.
    /// </summary>
    public struct HeatCell
    {
        /// <summary>
        /// 이 구간에서 <b>가장 크게 벌어진 순간의 오차(%)</b>.
        /// 칸에 적히는 값이고, "차이 났다" 는 판정도 이 값으로 내립니다.
        ///
        /// 이 값은 허용 오차를 <b>바꿔도 변하지 않습니다.</b> 예전에는 칸에
        /// "기준을 넘은 표본만의 평균" 을 적었는데, 기준을 바꾸면 평균 낼
        /// 표본이 바뀌어서 숫자가 따라 움직였습니다. 재는 값이 재는 잣대에
        /// 딸려 있으면 안 됩니다.
        ///
        /// 최대를 쓰면 구간이 길다고 커지지도 않고(합계의 문제), 진동해도
        /// 0 이 되지 않습니다(부호 있는 합계의 문제). 잠깐 크게 튄 것도
        /// 평균에 묻히지 않습니다.
        /// </summary>
        public float PeakPercent;

        /// <summary>
        /// PeakPercent 가 나온 <b>그 순간</b>의 절대 차이. 칸을 값으로 볼 때
        /// 적히는 숫자입니다. 같은 표본에서 나온 값이라 %로 보다가 값으로
        /// 바꿔도 가리키는 순간이 달라지지 않습니다.
        /// </summary>
        public float PeakDiff;

        /// <summary>구간 안 모든 표본의 절대 차이 평균. 설명 줄에만 씁니다.</summary>
        public float Mean;

        /// <summary>이 구간에서 가장 크게 벌어진 절대 차이.</summary>
        public float Max;

        /// <summary>제곱평균제곱근. 큰 차이에 더 무게를 둡니다.</summary>
        public float Rms;

        /// <summary>견준 표본 수. 0 이면 이 구간에 기록이 없습니다.</summary>
        public int Samples;

        /// <summary>허용 오차를 넘은 표본 수.</summary>
        public int OverSamples;

        public bool HasData { get { return Samples > 0; } }
    }

    /// <summary>IO 하나의 가로 한 줄.</summary>
    public sealed class HeatRow
    {
        public string Name;
        public int BeforeIndex = -1;
        public int AfterIndex = -1;

        public HeatCell[] Cells;

        /// <summary>
        /// 차이가 난 칸들 중 가장 큰 오차(%). <b>색 진하기에만</b> 씁니다.
        ///
        /// 한 줄 안에서 "어느 시간대가 제일 심한가" 를 색으로 보여 주는 값이라
        /// 칸 폭이 바뀌면 같이 바뀝니다 — 1 분씩 따로 세던 것을 5 분으로 묶어
        /// 평균 내면 봉우리가 낮아집니다. 줄 차례를 매기는 데 쓰면 안 됩니다.
        /// </summary>
        public double PeakMean;

        /// <summary>
        /// 차이가 난 칸의 개수.
        /// <b>칸 폭에 따라 달라집니다</b> — 1 분 칸 5 개가 5 분 칸 1 개가 되면
        /// 이 값은 5 분의 1 이 됩니다. 그래서 줄 차례를 매기는 데 쓰면 안 됩니다.
        /// </summary>
        public int OverBuckets;

        /// <summary>
        /// 기준을 넘은 표본의 총 개수. 칸을 어떻게 자르든 같습니다.
        /// </summary>
        public int TotalOverSamples;

        /// <summary>
        /// 줄 전체에서 가장 크게 벌어진 순간의 오차(%).
        /// 칸을 어떻게 자르든 같습니다.
        /// </summary>
        public double PeakMax;

        /// <summary>
        /// 값이 아니라 이름(상태 문자열)으로 견준 줄인지.
        /// 이런 줄은 이름이 다르면 허용 오차와 상관없이 차이이고,
        /// 오차는 0% 아니면 100% 입니다.
        /// </summary>
        public bool ByName;

        /// <summary>값의 단위. 칸 설명에 같이 적습니다. 없으면 빈 문자열.</summary>
        public string Unit = string.Empty;

        public bool HasDifference { get { return OverBuckets > 0; } }

        /// <summary>
        /// 칸에 적을 값(%). 색을 정하는 것과 같은 값입니다.
        /// 넘은 표본이 없으면 0 이고, 그 칸은 정상입니다.
        /// </summary>
        public double PercentOf(HeatCell c)
        {
            if (!c.HasData || c.OverSamples == 0) return 0;
            return c.PeakPercent;
        }

        /// <summary>칸을 값으로 볼 때 적을 숫자. PercentOf 와 같은 순간입니다.</summary>
        public double ValueOf(HeatCell c)
        {
            if (!c.HasData || c.OverSamples == 0) return 0;
            return c.PeakDiff;
        }

        /// <summary>
        /// 이 IO 를 얼마나 먼저 봐야 하는지. 줄 차례를 매기는 값입니다.
        ///
        /// 줄 전체에서 가장 크게 벌어진 순간의 오차(%)입니다. 두 가지를
        /// 동시에 만족해야 해서 이렇게 골랐습니다.
        ///
        ///  - <b>칸 폭과 무관해야 합니다.</b> 1 분으로 보든 5 분으로 보든
        ///    같은 로그이므로 줄 차례가 바뀌면 안 됩니다. 표본 하나하나에서
        ///    나온 값이라 칸을 어떻게 묶든 최대는 최대입니다.
        ///  - <b>IO 끼리 견줄 수 있어야 합니다.</b> 퍼센트라 값 크기가 다른
        ///    채널끼리도 그대로 견줍니다.
        /// </summary>
        public double Severity { get { return PeakMax; } }

    }

    public sealed class HeatmapOptions
    {
        /// <summary>한 칸의 시간 폭. 0 이면 스스로 정합니다 (시각 로그면 1 분).</summary>
        public double BucketSpan;

        /// <summary>절대 허용 오차. 이 값 이하의 차이는 같은 것으로 봅니다.</summary>
        public double AbsoluteTolerance;

        /// <summary>
        /// 허용 오차 퍼센트. 설정에 적는 그 값 그대로입니다 (0.1 이 0.1%).
        /// 그 순간의 오차 = |이후 − 이전| / |이전| x 100 과 견줍니다.
        /// 자세한 규칙은 ToleranceRule 에 있습니다.
        /// </summary>
        public double RelativePercent = ToleranceRule.DefaultPercent;

        /// <summary>
        /// IO 별 허용 오차. 있으면 이것이 이깁니다. 대시보드·그래프와 같은
        /// 표를 씁니다 — 화면마다 "차이 난 IO" 가 달라지면 안 됩니다.
        /// </summary>
        public ToleranceTable Tolerances;

        /// <summary>칸이 너무 많아지지 않게 하는 상한.</summary>
        public int MaxBuckets = 4000;

        /// <summary>차이가 없는 IO 도 줄로 넣을지.</summary>
        public bool IncludeUnchanged;

        /// <summary>이후 로그의 시간을 이만큼 밀어 맞춥니다.</summary>
        public double Shift;

        /// <summary>
        /// <b>가로를 단계로 자를지.</b> 참이면 단계 하나가 칸 하나입니다
        /// (시간 폭 대신). 단계가 없으면 시간으로 자르고 그렇다고 말해
        /// 줍니다 — 조용히 시간으로 돌아가면 "단계로 봤다" 고 착각합니다.
        /// </summary>
        public bool ByStep;

        /// <summary>
        /// 짝지어 둔 단계. <see cref="ByStep"/> 일 때만 씁니다.
        /// 여기서 짓지 않고 받는 이유: 그래프 화면도 같은 목록을 쓰는데,
        /// 각자 지으면 두 화면의 단계 번호가 어긋날 수 있습니다.
        /// </summary>
        public StepPairList Steps;

        /// <summary>
        /// 이 이름들만 봅니다. 비어 있으면 모두 봅니다.
        ///
        /// 메인 화면에서 그룹을 눌러 넘어왔을 때, 그 그룹의 IO 만 남깁니다.
        /// 넘어와 놓고 수백 줄 가운데서 다시 찾아야 하면 넘어온 뜻이 없습니다.
        ///
        /// 이름은 <b>느슨하게</b> 맞춥니다 (공백·대소문자·_-. 무시). 그룹은
        /// 사람이 손으로 담는 것이라 이전 로그와 이후 로그의 이름이 살짝
        /// 다를 수 있는데, 그 규칙은 채널을 찾을 때 이미 쓰고 있습니다.
        /// </summary>
        public HashSet<string> OnlyNames;

        public bool HasFilter { get { return OnlyNames != null && OnlyNames.Count > 0; } }

        /// <summary>이름 목록을 느슨한 열쇠로 바꿔 담습니다.</summary>
        public static HashSet<string> NameFilter(IEnumerable<string> names)
        {
            if (names == null) return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                set.Add(LogDataset.LooseKey(n));
            }
            return set.Count > 0 ? set : null;
        }

        public HeatmapOptions Clone() { return (HeatmapOptions)MemberwiseClone(); }
    }

    public sealed class HeatmapResult
    {
        public List<HeatRow> Rows = new List<HeatRow>();

        public double Start;
        public double End;
        public double BucketSpan;
        public int BucketCount;

        /// <summary>칸 시각을 글자로 바꿀 때 쓸 기준 로그.</summary>
        public LogDataset TimeReference;

        /// <summary>1 분 단위로 잘랐는지, 아니면 다른 폭으로 잘랐는지.</summary>
        public string BucketLabel = string.Empty;

        /// <summary>
        /// 칸의 경계. 가로축 글자도 여기서 나옵니다.
        /// <b>칸 번호를 셈하는 곳은 여기 하나입니다</b> — 단계로 자르면
        /// 칸 폭이 다 다르고 칸 사이에 빈틈이 있어서, 셈하는 식을 여러
        /// 곳에 두면 한 곳만 고쳐도 조용히 어긋납니다.
        /// </summary>
        public HeatBuckets Buckets;

        /// <summary>단계로 잘렸는지. 화면이 가로축 이름을 바꿔 적습니다.</summary>
        public bool ByStep { get { return Buckets != null && Buckets.ByStep; } }

        public string Warning = string.Empty;

        public int ComparedChannels;
        public int ChangedChannels;

        /// <summary>
        /// 칸이 시작하는 시각. <b>칸 목록에게 묻습니다</b> — 단계로 자르면
        /// 폭이 다 달라서 "시작 + 번호 x 폭" 이 맞지 않습니다.
        /// </summary>
        public double BucketStart(int index)
        {
            if (Buckets != null && index >= 0 && index < Buckets.Count) return Buckets.From(index);
            return Start + index * BucketSpan;
        }

        /// <summary>
        /// 칸이 끝나는 시각. <b>"시작 + 폭" 으로 셈하면 안 됩니다</b> —
        /// 단계로 자르면 폭이 다 다르고 BucketSpan 은 NaN 입니다.
        /// </summary>
        public double BucketEnd(int index)
        {
            if (Buckets != null && index >= 0 && index < Buckets.Count) return Buckets.To(index);
            return Start + (index + 1) * BucketSpan;
        }

        /// <summary>칸 하나의 가로축 글자. 단계면 단계 이름입니다.</summary>
        public string BucketText(int index)
        {
            if (Buckets == null) return string.Empty;
            return Buckets.Label(index, TimeReference);
        }
    }
}
