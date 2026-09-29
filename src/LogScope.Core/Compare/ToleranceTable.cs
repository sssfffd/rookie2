using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LogScope.Core.Model;

namespace LogScope.Core.Compare
{
    /// <summary>
    /// IO 하나에만 따로 걸어 둔 허용 오차.
    ///
    /// <see cref="Percent"/> 와 <see cref="Absolute"/> 는 <b>NaN 이면 "기본값을
    /// 쓴다"</b> 는 뜻입니다. 0 과 구별해야 하기 때문입니다 — 절대 오차 0 은
    /// "절대 오차를 끈다" 는 뜻이고, 퍼센트 0 은 "조금이라도 벌어지면 차이"
    /// 라는 뜻이라 둘 다 실제로 쓰는 값입니다.
    /// </summary>
    public sealed class ToleranceOverride
    {
        public string Io = string.Empty;
        public double Percent = double.NaN;
        public double Absolute = double.NaN;

        public bool HasPercent { get { return !double.IsNaN(Percent); } }
        public bool HasAbsolute { get { return !double.IsNaN(Absolute); } }

        /// <summary>이름이 없거나 둘 다 안 정했으면 쓸모가 없습니다.</summary>
        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Io) || Io.Trim().Length == 0 || (!HasPercent && !HasAbsolute); }
        }

        public ToleranceOverride Clone()
        {
            var c = new ToleranceOverride();
            c.Io = Io;
            c.Percent = Percent;
            c.Absolute = Absolute;
            return c;
        }
    }

    /// <summary>
    /// "이 IO 는 이만큼까지 봐준다" 를 찾아 주는 표.
    ///
    /// 기본값 하나로 다 보는 것이 맞지 않는 경우가 있습니다. 온도처럼 원래
    /// 조금씩 흔들리는 값과, 밸브 열림처럼 조금도 달라지면 안 되는 값에
    /// 같은 잣대를 대면 한쪽은 늘 빨갛고 다른 쪽은 놓칩니다.
    ///
    /// <b>이름은 느슨하게 맞춥니다</b> (<see cref="LogDataset.LooseKey"/>).
    /// 두 로그에서 같은 IO 가 "밸브_OPEN" 과 "밸브 OPEN" 으로 적히는 일이
    /// 흔해서, 비교 자체가 이미 그 규칙으로 짝을 짓고 있습니다. 여기만 엄격히
    /// 보면 설정한 값이 조용히 안 먹습니다.
    ///
    /// 대시보드 · 그래프 · 히트맵이 <b>모두 이 표 하나</b>를 씁니다.
    /// </summary>
    public sealed class ToleranceTable
    {
        private readonly Dictionary<string, ToleranceOverride> _byKey =
            new Dictionary<string, ToleranceOverride>(StringComparer.Ordinal);

        public double DefaultPercent { get; private set; }
        public double DefaultAbsolute { get; private set; }

        private ToleranceTable(double percent, double absolute)
        {
            DefaultPercent = percent;
            DefaultAbsolute = absolute;
        }

        /// <summary>
        /// 기본값과 IO 별 규칙으로 표를 만듭니다. 규칙이 없어도 됩니다 —
        /// 그때는 모든 IO 가 기본값입니다.
        ///
        /// 같은 IO 가 두 번 적혀 있으면 <b>뒤의 것</b>이 이깁니다. 설정 창에서
        /// 방금 고친 줄이 아래에 있기 때문입니다.
        /// </summary>
        public static ToleranceTable From(double percent, double absolute,
                                          IEnumerable<ToleranceOverride> rules)
        {
            var t = new ToleranceTable(percent, absolute);
            if (rules == null) return t;

            foreach (ToleranceOverride r in rules)
            {
                if (r == null || r.IsEmpty) continue;
                string key = LogDataset.LooseKey(r.Io);
                if (key.Length == 0) continue;
                t._byKey[key] = r;
            }
            return t;
        }

        public int OverrideCount { get { return _byKey.Count; } }
        public bool HasOverrides { get { return _byKey.Count > 0; } }

        public bool HasOverrideFor(string io)
        {
            return Find(io) != null;
        }

        /// <summary>이 IO 에 걸린 허용 오차 퍼센트. 안 걸려 있으면 기본값.</summary>
        public double PercentFor(string io)
        {
            ToleranceOverride r = Find(io);
            return r != null && r.HasPercent ? r.Percent : DefaultPercent;
        }

        /// <summary>이 IO 에 걸린 절대 허용 오차. 안 걸려 있으면 기본값.</summary>
        public double AbsoluteFor(string io)
        {
            ToleranceOverride r = Find(io);
            return r != null && r.HasAbsolute ? r.Absolute : DefaultAbsolute;
        }

        private ToleranceOverride Find(string io)
        {
            if (string.IsNullOrEmpty(io) || _byKey.Count == 0) return null;
            ToleranceOverride r;
            return _byKey.TryGetValue(LogDataset.LooseKey(io), out r) ? r : null;
        }

        /// <summary>
        /// 이 표를 한 줄로 적은 것. 저장해 둔 분석과 <b>같은 기준이었는지</b>
        /// 가리는 데 씁니다 (AnalysisRecord.SameBasis).
        ///
        /// 줄 순서가 달라도 같은 글자가 나와야 해서 열쇠 순으로 정렬합니다.
        /// 기본값은 여기 넣지 않습니다 — 그건 따로 견줍니다.
        /// </summary>
        public string Signature()
        {
            if (_byKey.Count == 0) return string.Empty;

            var keys = new List<string>(_byKey.Keys);
            keys.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            foreach (string k in keys)
            {
                ToleranceOverride r = _byKey[k];
                if (sb.Length > 0) sb.Append(';');
                sb.Append(k).Append('=');
                if (r.HasPercent) sb.Append(r.Percent.ToString("R", CultureInfo.InvariantCulture));
                sb.Append('/');
                if (r.HasAbsolute) sb.Append(r.Absolute.ToString("R", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
