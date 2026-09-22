using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Core.Settings;

namespace LogScope.Core.History
{
    /// <summary>
    /// 분석을 한 번 돌린 결과를 <b>요약해서</b> 남긴 것.
    ///
    /// 로그 값 자체는 담지 않습니다. 표본이 수백만 개라 파일이 원본만큼
    /// 커지고, 그러면 "지난번과 견줘 보기" 가 로그를 한 번 더 여는 일이
    /// 됩니다. 나중에 견주는 데 실제로 쓰는 것은 <b>몇 개의 수</b>뿐입니다.
    ///
    /// 어떤 기준으로 나온 수인지도 같이 적습니다. 허용 오차를 0.1% 에서 1%
    /// 로 바꿔 놓고 "달라진 IO 가 줄었다" 고 읽으면 안 되기 때문입니다.
    /// </summary>
    public sealed class AnalysisRecord
    {
        /// <summary>파일 이름에서 온 열쇠. 저장할 때 정해집니다.</summary>
        public string Id = string.Empty;

        public DateTime SavedAt = DateTime.MinValue;

        /// <summary>사람이 붙인 이름. 비어 있으면 화면이 저장 시각으로 적습니다.</summary>
        public string Label = string.Empty;

        // ---- 무엇을 견줬나 ----
        public string BeforeName = string.Empty;
        public string AfterName = string.Empty;
        public string BeforeTime = string.Empty;   // 파일 이름 앞부분 (발생시간)
        public string AfterTime = string.Empty;

        // ---- 결과 ----
        public bool HasScore;
        public double Score;

        public int ComparedCount;    // 양쪽에 다 있는 IO
        public int ChangedCount;     // 허용 오차를 넘은 IO
        public int OneSidedCount;    // 한쪽에만 있는 IO

        // ---- 어떤 기준으로 나온 수인가 ----
        public double TolerancePercent;
        public double AbsoluteTolerance;
        public string AlignIo = string.Empty;
        public string AxisIo = string.Empty;
        public double AppliedShift;

        /// <summary>화면에 적는 이름. 붙인 이름이 없으면 저장 시각을 씁니다.</summary>
        public string DisplayName
        {
            get
            {
                if (Label.Length > 0) return Label;
                return SavedAt == DateTime.MinValue
                    ? "이름 없음"
                    : SavedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// 그때와 지금이 <b>같은 기준</b>으로 나온 수인지.
        ///
        /// 기준이 다르면 수를 나란히 놓아도 뜻이 없습니다. 화면에서 그렇다고
        /// 알려 주려고 여기서 가립니다.
        /// </summary>
        public bool SameBasis(double tolerancePercent, double absoluteTolerance,
                              string alignIo, string axisIo)
        {
            return TolerancePercent == tolerancePercent
                && AbsoluteTolerance == absoluteTolerance
                && string.Equals(AlignIo ?? string.Empty, alignIo ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(AxisIo ?? string.Empty, axisIo ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>어떤 기준이었는지 한 줄로.</summary>
        public string BasisText
        {
            get
            {
                string s = "허용 오차 " + TolerancePercent.ToString("0.####", CultureInfo.InvariantCulture) + "%";
                if (AbsoluteTolerance > 0)
                    s += " / 절대 " + AbsoluteTolerance.ToString("0.######", CultureInfo.InvariantCulture);
                if (AlignIo.Length > 0) s += "   시간 맞추기 \"" + AlignIo + "\"";
                if (AxisIo.Length > 0) s += "   가로축 \"" + AxisIo + "\"";
                return s;
            }
        }

        // ---------------- JSON ----------------

        public const int FileVersion = 1;

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["fileVersion"] = (double)FileVersion;
            d["savedAt"] = SavedAt.ToString("o", CultureInfo.InvariantCulture);
            d["label"] = Label ?? string.Empty;

            d["beforeName"] = BeforeName ?? string.Empty;
            d["afterName"] = AfterName ?? string.Empty;
            d["beforeTime"] = BeforeTime ?? string.Empty;
            d["afterTime"] = AfterTime ?? string.Empty;

            d["hasScore"] = HasScore;
            d["score"] = Score;

            d["comparedCount"] = (double)ComparedCount;
            d["changedCount"] = (double)ChangedCount;
            d["oneSidedCount"] = (double)OneSidedCount;

            d["tolerancePercent"] = TolerancePercent;
            d["absoluteTolerance"] = AbsoluteTolerance;
            d["alignIo"] = AlignIo ?? string.Empty;
            d["axisIo"] = AxisIo ?? string.Empty;
            d["appliedShift"] = AppliedShift;
            return d;
        }

        public static AnalysisRecord FromJson(object parsed)
        {
            var r = new AnalysisRecord();
            Dictionary<string, object> d = Json.AsObject(parsed);
            if (d.Count == 0) return r;

            r.SavedAt = ParseTime(Json.GetString(d, "savedAt", string.Empty));
            r.Label = Json.GetString(d, "label", string.Empty);

            r.BeforeName = Json.GetString(d, "beforeName", string.Empty);
            r.AfterName = Json.GetString(d, "afterName", string.Empty);
            r.BeforeTime = Json.GetString(d, "beforeTime", string.Empty);
            r.AfterTime = Json.GetString(d, "afterTime", string.Empty);

            r.HasScore = Json.GetBool(d, "hasScore", false);
            r.Score = Json.GetDouble(d, "score", 0);

            r.ComparedCount = Json.GetInt(d, "comparedCount", 0);
            r.ChangedCount = Json.GetInt(d, "changedCount", 0);
            r.OneSidedCount = Json.GetInt(d, "oneSidedCount", 0);

            r.TolerancePercent = Json.GetDouble(d, "tolerancePercent", 0);
            r.AbsoluteTolerance = Json.GetDouble(d, "absoluteTolerance", 0);
            r.AlignIo = Json.GetString(d, "alignIo", string.Empty);
            r.AxisIo = Json.GetString(d, "axisIo", string.Empty);
            r.AppliedShift = Json.GetDouble(d, "appliedShift", 0);
            return r;
        }

        private static DateTime ParseTime(string s)
        {
            DateTime t;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                                  DateTimeStyles.RoundtripKind, out t)) return t;
            return DateTime.MinValue;
        }
    }
}
