using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.Judge;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면 위쪽 <b>대시보드</b> (1.94, 사용자 요청 — Grafana · Splunk · WhaTap · 장비 모니터링 참고).
    ///
    ///   숫자 패널 줄   최종 판정 · 점수 게이지 · 차이 IO · 평균 오차율 · 로그 길이 · 분석 2 · 분석 3
    ///   시간대별 차이  이전 로그 100 칸 막대 (차이 난 IO 수, 크게 벌어진 칸은 진하게) + 공정 단계 띠
    ///   차이 큰 IO     크기 반영 오차율 순
    ///   그룹 상태      차이 IO / 전체, 색
    ///
    /// 숫자는 모두 점수와 <b>같은 셈</b> (JudgeScore) 에서 나옵니다 — 패널마다 따로 세면 수가 어긋납니다.
    /// History 를 펼쳐 보는 중에는 비웁니다 (그때의 대시보드는 저장하지 않으므로, 지금 것을 그때 것으로 읽지 않게).
    /// </summary>
    public sealed class MainDashVm : ObservableObject
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>차이 큰 IO 줄 수.</summary>
        public const int TopCount = 8;

        private bool _has;
        private string _empty = "로그 두 개를 열고 [분석] 을 누르면 여기에 결과가 나옵니다.";

        public bool HasResult { get { return _has; } }
        public bool IsEmpty { get { return !_has; } }
        public string EmptyText { get { return _empty; } }

        // ---- 숫자 패널 ----
        public double Score { get; private set; }
        public string ScoreText { get; private set; }
        public string ScoreGrade { get; private set; }
        public string BandsText { get; private set; }

        public string DiffMain { get; private set; }
        public string DiffTotal { get; private set; }
        public double DiffShare { get; private set; }
        /// <summary>막대용 0 ~ 100.</summary>
        public double DiffSharePercent { get { return DiffShare * 100.0; } }
        public string DiffBreak { get; private set; }
        public string DiffGrade { get; private set; }

        public string RateMain { get; private set; }
        public string RateSub { get; private set; }

        public string LengthMain { get; private set; }
        public string LengthSub { get; private set; }
        public string LengthGrade { get; private set; }

        /// <summary>오차가 가장 많이 몰린 공정 단계 ("가열 유지 구간에 몰림"). 없으면 빈 글자.</summary>
        public string Hotspot { get; private set; }
        public bool HasHotspot { get { return !string.IsNullOrEmpty(Hotspot); } }

        // ---- 시간대별 차이 ----
        public DashTimeline Timeline { get; private set; }

        // ---- 차이 큰 IO · 그룹 ----
        public List<DashIoRow> TopIos { get; private set; }
        public bool HasTopIos { get { return TopIos != null && TopIos.Count > 0; } }
        public bool NoTopIos { get { return !HasTopIos; } }
        public string TopNote { get; private set; }
        public List<DashGroupTile> Groups { get; private set; }
        public bool HasGroups { get { return Groups != null && Groups.Count > 0; } }
        public bool NoGroups { get { return !HasGroups; } }

        public MainDashVm()
        {
            Clear(null);
        }

        /// <summary>결과 없음 (또는 History 보는 중). why 가 있으면 그 글을 보입니다.</summary>
        public void Clear(string why)
        {
            _has = false;
            if (!string.IsNullOrEmpty(why)) _empty = why;
            else _empty = "로그 두 개를 열고 [분석] 을 누르면 여기에 결과가 나옵니다.";
            Score = double.NaN; ScoreText = "—"; ScoreGrade = ScoreBands.GradeNone; BandsText = string.Empty;
            DiffMain = "—"; DiffTotal = string.Empty; DiffShare = 0; DiffBreak = string.Empty; DiffGrade = ScoreBands.GradeNone;
            RateMain = "—"; RateSub = string.Empty;
            LengthMain = "—"; LengthSub = string.Empty; LengthGrade = ScoreBands.GradeNone;
            Hotspot = string.Empty;
            Timeline = null;
            TopIos = new List<DashIoRow>(); TopNote = string.Empty;
            Groups = new List<DashGroupTile>();
            RaiseAll();
        }

        /// <summary>판정 · 점수로 채웁니다.</summary>
        public void Update(JudgeScore js, JudgeResult judge, LogDataset before, LogDataset after,
                           StepPairList steps, List<GroupDef> groups)
        {
            if (js == null || judge == null || before == null) { Clear(null); return; }
            _has = true;

            Score = Math.Round(js.Score, 1);
            ScoreText = Score.ToString("0.#", Inv);
            ScoreGrade = ScoreBands.GradeOf(Score);
            BandsText = ScoreBands.GoodFrom.ToString("0.#", Inv) + " 정상 · " + ScoreBands.WarnFrom.ToString("0.#", Inv) + " 주의 · 그 아래 이상";

            // 차이 IO — 판정의 IO 등급 (이상 = CRITICAL, 주의 = WARNING)
            var gradeOf = new Dictionary<string, JudgeGrade>(StringComparer.Ordinal);
            foreach (JudgeIo io in judge.Ios) if (io.Items > io.AllowedItems) gradeOf[io.Name] = io.Grade;
            int bad = 0, warn = 0;
            foreach (string name in js.Rates.Keys)
            {
                JudgeGrade g;
                if (js.OneSide.ContainsKey(name) || (gradeOf.TryGetValue(name, out g) && g == JudgeGrade.Critical)) bad++;
                else warn++;
            }
            DiffMain = js.DiffIos.ToString("N0");
            DiffTotal = "/ " + js.TotalIos.ToString("N0");
            DiffShare = js.TotalIos > 0 ? Math.Min(1.0, (double)js.DiffIos / js.TotalIos) : 0;
            DiffBreak = js.DiffIos == 0 ? "차이 없음" : "이상 " + bad + " · 주의 " + warn;
            DiffGrade = js.DiffIos == 0 ? ScoreBands.GradeGood : bad > 0 ? ScoreBands.GradeBad : ScoreBands.GradeWarn;

            RateMain = js.MeanErrorRate.ToString("0.#", Inv);
            RateSub = js.DiffIos == 0 ? "차이 없음" : "크기 반영 " + js.MeanWeightedRate.ToString("0.#", Inv) + " %";

            double cover = js.LengthFactor * 100.0;
            LengthMain = cover.ToString("0.#", Inv);
            JudgeItem len = judge.Items.Find(x => x.Kind == JudgeKind.LogLength && !x.IsAllowed);
            LengthSub = len == null ? "이전 · 이후 길이 같음" : len.What;
            LengthGrade = len == null ? ScoreBands.GradeGood : (len.Grade == JudgeGrade.Critical ? ScoreBands.GradeBad : ScoreBands.GradeWarn);

            Timeline = BuildTimeline(js, before, steps);
            Hotspot = HotspotOf(Timeline);
            TopIos = BuildTop(js, gradeOf, before, groups);
            TopNote = js.DiffIos > TopCount ? "차이 IO " + js.DiffIos + " 개 중 위 " + TopCount + " 개" : string.Empty;
            Groups = BuildGroups(js, gradeOf, before, after, groups);
            RaiseAll();
        }

        private void RaiseAll()
        {
            Raise("HasResult"); Raise("IsEmpty"); Raise("EmptyText");
            Raise("Score"); Raise("ScoreText"); Raise("ScoreGrade"); Raise("BandsText");
            Raise("DiffMain"); Raise("DiffTotal"); Raise("DiffShare"); Raise("DiffSharePercent"); Raise("DiffBreak"); Raise("DiffGrade");
            Raise("RateMain"); Raise("RateSub");
            Raise("LengthMain"); Raise("LengthSub"); Raise("LengthGrade");
            Raise("Hotspot"); Raise("HasHotspot"); Raise("Timeline");
            Raise("TopIos"); Raise("HasTopIos"); Raise("NoTopIos"); Raise("TopNote"); Raise("Groups"); Raise("HasGroups"); Raise("NoGroups");
        }

        // ---------------- 시간대별 차이 ----------------

        public static DashTimeline BuildTimeline(JudgeScore js, LogDataset before, StepPairList steps)
        {
            var t = new DashTimeline();
            int n = JudgeScore.Buckets;
            t.Counts = (int[])js.BucketIos.Clone();
            t.Weights = (double[])js.BucketWeight.Clone();
            t.Gaps = (bool[])js.BucketGap.Clone();
            for (int k = 0; k < n; k++) if (t.Counts[k] > t.MaxCount) t.MaxCount = t.Counts[k];

            double t0 = before.TimeStart, t1 = before.TimeEnd, len = t1 - t0;
            t.Labels = new string[5];
            for (int i = 0; i < 5; i++) t.Labels[i] = before.FormatTime(t0 + len * i / 4.0);

            if (steps != null && len > 0)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    StepPair p = steps[i];
                    if (double.IsNaN(p.BeforeStart) || double.IsNaN(p.BeforeEnd) || !(p.BeforeEnd > p.BeforeStart)) continue;
                    var s = new DashStep();
                    s.Name = string.IsNullOrEmpty(p.Label) ? p.Name : p.Label;
                    s.From = Math.Max(0, Math.Min(1, (p.BeforeStart - t0) / len));
                    s.To = Math.Max(0, Math.Min(1, (p.BeforeEnd - t0) / len));
                    if (s.To > s.From) t.Steps.Add(s);
                }
            }
            return t;
        }

        /// <summary>
        /// 차이가 <b>몰린</b> 공정 단계 — 단계 길이에 비해 차이가 많은 정도 (차이 몫 ÷ 시간 몫) 가 가장 큰 단계,
        /// 그 정도가 1.5 배 이상이고 전체 차이의 30 % 이상일 때만. 긴 단계라서 많아 보이는 것은 몰린 것이 아닙니다.
        /// </summary>
        public static string HotspotOf(DashTimeline t)
        {
            if (t == null || t.Steps.Count == 0) return string.Empty;
            int n = t.Counts.Length, total = 0;
            for (int k = 0; k < n; k++) total += t.Counts[k];
            if (total == 0) return string.Empty;
            string best = string.Empty; int bestSum = 0; double bestDensity = 0;
            foreach (DashStep s in t.Steps)
            {
                int sum = 0;
                int k0 = (int)Math.Floor(s.From * n), k1 = (int)Math.Ceiling(s.To * n) - 1;
                for (int k = Math.Max(0, k0); k <= Math.Min(n - 1, k1); k++) sum += t.Counts[k];
                double timeShare = s.To - s.From;
                if (!(timeShare > 0)) continue;
                double density = ((double)sum / total) / timeShare;
                if (density > bestDensity) { bestDensity = density; bestSum = sum; best = s.Name; }
            }
            if (bestDensity < 1.5 || bestSum * 10 < total * 3) return string.Empty;
            return "'" + best + "' 구간에 몰림 (차이의 " + (bestSum * 100 / total) + " %)";
        }

        // ---------------- 차이 큰 IO ----------------

        private static List<DashIoRow> BuildTop(JudgeScore js, Dictionary<string, JudgeGrade> gradeOf,
                                                LogDataset before, List<GroupDef> groups)
        {
            var names = new List<string>(js.Rates.Keys);
            names.Sort(delegate (string a, string b)
            {
                double wa, wb; js.WeightedRates.TryGetValue(a, out wa); js.WeightedRates.TryGetValue(b, out wb);
                int c = wb.CompareTo(wa);
                return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            var list = new List<DashIoRow>();
            for (int i = 0; i < names.Count && list.Count < TopCount; i++)
            {
                string name = names[i];
                var r = new DashIoRow();
                r.Name = name;
                r.Group = GroupOf(name, groups);
                r.TimeRate = js.Rates[name];
                double wr; js.WeightedRates.TryGetValue(name, out wr);
                r.WeightedRate = wr;
                r.RateText = r.TimeRate.ToString("0.#", Inv) + " % (" + wr.ToString("0.#", Inv) + " %)";
                bool missing;
                JudgeGrade g;
                if (js.OneSide.TryGetValue(name, out missing))
                {
                    r.LampGrade = ScoreBands.GradeBad;
                    r.PeakText = missing ? "이후에 없음" : "이후에만 있음";
                    r.WhenText = "전체";
                }
                else
                {
                    r.LampGrade = gradeOf.TryGetValue(name, out g) && g == JudgeGrade.Critical ? ScoreBands.GradeBad : ScoreBands.GradeWarn;
                    double pk;
                    r.PeakText = js.Peaks.TryGetValue(name, out pk) ? "최대 " + NumberText.Short(pk) + " %" : "-";
                    double[] sp;
                    if (js.Spans.TryGetValue(name, out sp) && sp[1] >= sp[0])
                    {
                        string a = before.FormatTime(sp[0]), b = before.FormatTime(sp[1]);
                        r.WhenText = a == b ? a : a + " ~ " + b;
                    }
                    else r.WhenText = "-";
                }
                r.Word = AnalysisCardVm.WordOf(r.LampGrade);
                list.Add(r);
            }
            return list;
        }

        private static string GroupOf(string io, List<GroupDef> groups)
        {
            if (groups == null) return string.Empty;
            string key = LogDataset.LooseKey(io);
            foreach (GroupDef g in groups)
                foreach (string m in g.Members)
                    if (LogDataset.LooseKey(m) == key) return g.Name;
            return string.Empty;
        }

        // ---------------- 그룹 상태 ----------------

        private static List<DashGroupTile> BuildGroups(JudgeScore js, Dictionary<string, JudgeGrade> gradeOf,
                                                       LogDataset before, LogDataset after, List<GroupDef> groups)
        {
            var list = new List<DashGroupTile>();
            if (groups == null) return list;
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (LogDataset ds in new[] { before, after })
                if (ds != null) for (int i = 0; i < ds.ChannelCount; i++) present.Add(LogDataset.LooseKey(ds.Channels[i].Name));
            var diffKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string n in js.Rates.Keys) diffKeys[LogDataset.LooseKey(n)] = n;

            foreach (GroupDef g in groups)
            {
                if (g == null) continue;
                int total = 0, diff = 0; bool bad = false;
                foreach (string m in g.Members)
                {
                    string k = LogDataset.LooseKey(m);
                    if (!present.Contains(k)) continue;
                    total++;
                    string name;
                    if (!diffKeys.TryGetValue(k, out name)) continue;
                    diff++;
                    JudgeGrade gr;
                    if (js.OneSide.ContainsKey(name) || (gradeOf.TryGetValue(name, out gr) && gr == JudgeGrade.Critical)) bad = true;
                }
                if (total == 0) continue;
                var t = new DashGroupTile();
                t.Name = g.Name;
                t.Diff = diff.ToString("N0");
                t.Total = "/ " + total.ToString("N0");
                t.LampGrade = diff == 0 ? ScoreBands.GradeGood : bad ? ScoreBands.GradeBad : ScoreBands.GradeWarn;
                t.Word = AnalysisCardVm.WordOf(t.LampGrade);
                list.Add(t);
            }
            return list;
        }
    }

    /// <summary>시간대별 차이 막대의 자료.</summary>
    public sealed class DashTimeline
    {
        public int[] Counts = new int[0];
        public double[] Weights = new double[0];
        public bool[] Gaps = new bool[0];
        public int MaxCount;
        public string[] Labels = new string[0];
        public List<DashStep> Steps = new List<DashStep>();
    }

    /// <summary>공정 단계 띠의 한 칸 (0 ~ 1 — 이전 로그 시간 비율).</summary>
    public sealed class DashStep
    {
        public string Name = string.Empty;
        public double From, To;
    }

    /// <summary>차이 큰 IO 표의 한 줄.</summary>
    public sealed class DashIoRow
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string Word { get; set; }
        public string LampGrade { get; set; }
        public double TimeRate { get; set; }
        public double WeightedRate { get; set; }
        public string RateText { get; set; }
        public string PeakText { get; set; }
        public string WhenText { get; set; }
    }

    /// <summary>그룹 상태 타일.</summary>
    public sealed class DashGroupTile
    {
        public string Name { get; set; }
        public string Diff { get; set; }
        public string Total { get; set; }
        public string Word { get; set; }
        public string LampGrade { get; set; }
    }
}
