using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Judge
{
    // =====================================================================
    //  분석 1 정상 판정 (1.56) — docs/ANALYSIS1-JUDGEMENT-GUIDE.md 의 ① ③ ⑤ ⑦ ⑨
    //
    //   ① 단계마다 시작 맞추기   (StepPairList 의 단계별 밀기 값. 단계 안은 늘이거나 줄이지 않음)
    //   ③ 시간 여유 띠           이후 값이 이전 로그의 "앞뒤 시간 여유 동안 나온 값의 범위" (+ 값 허용 오차)
    //                            안이면 같음. <b>양쪽에서</b> 봄 — 이후 → 이전, 이전 → 이후. 한쪽만 보면
    //                            "이전에는 있던 짧은 펄스가 이후에는 없음" 을 놓칩니다.
    //   ⑤ 정도                   오차 길이 · 단계 안 비율 · 가장 긴 오차 · 구간 수 · 최대로 벗어난 값
    //   ⑦ 공정 순서              단계가 없어짐 · 생김 · 차례가 바뀜 (CRITICAL), 단계 길이 차이 (오차 항목)
    //   ⑨ 허용                   오차마다 "이번만" · "앞으로도 (본 크기까지)" — 허용된 것은 판정에서 빠짐
    //
    //  하지 않는 것 (사용자 결정, 146): 단계 안 늘이기 · 짧은 튐 지우기 · 치우침 · 중요 표시 · 엄격도.
    //  그래서 <b>0.1 초짜리 한 점도 범위 밖이면 오차</b>입니다 (아킹).
    //
    //  비교는 <b>모든 표본</b>으로 합니다. 대시보드 비교(DiffEngine)는 2 만 점으로 줄여 보므로
    //  한 점짜리 튐을 건너뛸 수 있습니다. 여기는 IO 하나씩, 로그 길이에 비례하는 시간.
    // =====================================================================

    /// <summary>오차 종류.</summary>
    public enum JudgeKind
    {
        /// <summary>값이 시간 여유 띠 밖.</summary>
        Value = 0,
        /// <summary>단계 길이가 시간 여유보다 많이 다름.</summary>
        StepLength = 1,
        /// <summary>이전 로그의 단계가 이후 로그에 없음.</summary>
        StepMissing = 2,
        /// <summary>이후 로그에만 있는 단계.</summary>
        StepExtra = 3,
        /// <summary>단계 차례가 다름.</summary>
        StepOrder = 4,
        /// <summary>이전 로그의 IO 가 이후 로그에 없음.</summary>
        IoMissing = 5,
        /// <summary>이후 로그에만 있는 IO.</summary>
        IoExtra = 6,
    }

    /// <summary>판정. 1.64: FAIL 을 CRITICAL 로 (사용자 요청 — 모든 분석에서 같은 낱말).</summary>
    public enum JudgeGrade
    {
        Pass = 0,
        Warning = 1,
        Critical = 2,
        /// <summary>1.63 까지의 이름 — 사용자 코드가 부를 수 있어 남겨 둠 (Critical 과 같은 값).</summary>
        [System.Obsolete("Critical 을 쓰세요 (1.64)")]
        Fail = Critical,
    }

    /// <summary>허용 상태.</summary>
    public enum AllowState { None = 0, Once = 1, Rule = 2 }

    public sealed class JudgeOptions
    {
        /// <summary>시간 여유 = 단계 길이의 이 % (가이드 ③).</summary>
        public double SlackPercent = 2.0;

        /// <summary>시간 여유 최소 = 표본 간격의 이 배 (0.1 초 기록이면 0.2 초).</summary>
        public int SlackMinSamples = 2;

        /// <summary>시간 여유 최대 (시각 로그일 때, ms). 시각이 아닌 로그는 위 % 만 씁니다.</summary>
        public double SlackMaxMs = 5000.0;

        /// <summary>
        /// 오차 한 줄의 낱말 — 한 단계 안에서 오차 시간이 이 비율 이상이면 CRITICAL, 그 아래는 WARNING.
        /// 오차가 얼마나 심한지 보여 주는 표시이고, <b>분석 1 판정은 아래 IO 수로</b> 냅니다 (1.64).
        /// </summary>
        public double CriticalRatio = 0.20;

        /// <summary>
        /// 분석 1 판정 (1.64): 허용 안 된 오차가 있는 IO 가 0 개면 PASS, 이 수 이하면 WARNING, 넘으면 CRITICAL.
        /// 단계가 없어지거나 · 생기거나 · 차례가 바뀌면 IO 수와 상관없이 CRITICAL, 단계 길이 오차만 있으면 WARNING.
        /// </summary>
        public int CriticalIoCount = 10;

        /// <summary>값 허용 오차 (지금 쓰는 표 그대로).</summary>
        public ToleranceTable Tolerances;

        /// <summary>
        /// 전체 맞춤의 밀기 값 — 이후 시각 + Shift = 이전 시각. 단계가 없으면 이것으로 견주고, 단계가 있으면
        /// 단계 맞춤과 함께 봐서 둘 다에서 벗어난 곳만 오차로 남깁니다 (1.63). 그래프에서 고른 단계와 상관없는 값.
        /// </summary>
        public double Shift;

        /// <summary>"앞으로도 허용" 규칙.</summary>
        public List<AllowRule> Rules = new List<AllowRule>();

        /// <summary>"이번만 허용" 한 오차의 열쇠 (<see cref="JudgeItem.Key"/>).</summary>
        public ICollection<string> Once;

        /// <summary>견주지 않을 IO (느슨한 열쇠) — 단계 IO (1.66).</summary>
        public HashSet<string> Exclude;

        /// <summary>
        /// 사용자가 분석 1 에서 지금 건 밀기 값 (1.67 — 그래프에서 고른 단계의 시작 맞춤 등). Shift 와 다르면 이것으로도 봐서
        /// <b>모든 맞춤에서 벗어난 곳만</b> 오차로 남깁니다. NaN 이면 안 봄.
        /// </summary>
        public double ViewShift = double.NaN;
    }

    /// <summary>오차 하나 — 오차 목록의 한 줄.</summary>
    public sealed class JudgeItem
    {
        public JudgeKind Kind;
        public string Io = string.Empty;

        /// <summary>단계 이름 (목록 글). 단계 없이 비교했으면 빈 글자.</summary>
        public string Step = string.Empty;

        /// <summary>시작 · 끝 (이전 로그 시간축).</summary>
        public double Start;
        public double End;

        /// <summary>길이 (시간축 단위). 값 오차만.</summary>
        public double Duration;

        /// <summary>
        /// 크기 — 값 오차면 범위에서 가장 멀리 벗어난 만큼 (값 단위), 단계 길이면 이후 − 이전 (시간축 단위).
        /// </summary>
        public double MaxExcess;

        /// <summary>그때의 오차 % (범위 끝 값 대비). 모르면 NaN.</summary>
        public double MaxExcessPercent = double.NaN;

        /// <summary>가장 크게 벗어난 순간과 그때의 값 설명.</summary>
        public double PeakTime;
        public string PeakText = string.Empty;

        /// <summary>이전 로그에 있던 모양이 이후 로그에 없어서 잡힌 것만으로 된 오차 (거꾸로 본 것).</summary>
        public bool MissingInAfter;

        /// <summary>그 단계의 길이 (비율 계산용, 시간축 단위).</summary>
        public double SegLength;

        /// <summary>이 IO 의 이 단계에서 허용 안 된 오차 시간 비율 (0~1).</summary>
        public double StepRatio;

        public JudgeGrade Grade;
        public AllowState Allowed;

        public bool IsAllowed { get { return Allowed != AllowState.None; } }

        /// <summary>"앞으로도 허용" 을 할 수 있는 종류인지. 단계가 없어지거나 차례가 바뀐 것은 이번만.</summary>
        public bool CanAllowAlways
        {
            get
            {
                return Kind == JudgeKind.Value || Kind == JudgeKind.StepLength
                    || Kind == JudgeKind.IoMissing || Kind == JudgeKind.IoExtra;
            }
        }

        /// <summary>"이번만 허용" 을 기억하는 열쇠. 같은 로그 · 같은 오차면 같음.</summary>
        public string Key
        {
            get
            {
                return Kind + "|" + Io + "|" + Step + "|"
                     + Start.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>목록 첫 줄.</summary>
        public string What = string.Empty;

        /// <summary>목록 둘째 줄 (작은 글).</summary>
        public string Detail = string.Empty;

        /// <summary>
        /// 1.66 보정 제안 — 이 IO 의 이 단계를 시간만 조금 밀거나 값만 조금 올리고 내리면 오차가 (거의) 없어질 때의 안내.
        /// 판정은 바꾸지 않습니다 (알려 주기만). 없으면 빈 글자.
        /// </summary>
        public string Hint = string.Empty;

        /// <summary>짧은 제안 ("시간 +1.2초" · "값 −0.3"). 메인 IO 줄에 붙입니다.</summary>
        public string HintShort = string.Empty;
    }

    /// <summary>IO 하나의 정도 (가이드 ⑤).</summary>
    public sealed class JudgeIo
    {
        public string Name = string.Empty;
        public int Items;
        public int AllowedItems;

        /// <summary>허용 안 된 오차의 합 (시간축 단위).</summary>
        public double ErrorTime;

        /// <summary>가장 나쁜 단계의 비율 (0~1) 과 그 단계.</summary>
        public double WorstRatio;
        public string WorstStep = string.Empty;

        /// <summary>가장 길게 이어진 오차 (허용 안 된 것).</summary>
        public double LongestRun;

        /// <summary>가장 크게 벗어난 값 (허용 안 된 것).</summary>
        public double MaxExcess;

        public List<string> Steps = new List<string>();
        public JudgeGrade Grade;
    }

    public sealed class JudgeResult
    {
        public List<JudgeItem> Items = new List<JudgeItem>();
        public List<JudgeIo> Ios = new List<JudgeIo>();

        /// <summary>보정 제안을 찾아본 IO · 단계 수 (1.66 — 너무 많으면 멈춤, 큰 로그에서 느려지지 않게).</summary>
        internal int HintTried;

        /// <summary>보정 제안에 쓴 일의 양 (훑은 표본 수 합). 한도를 넘으면 나머지는 제안 없이.</summary>
        internal long HintWork;

        public JudgeGrade Grade;

        /// <summary>허용 안 된 오차 수 · 허용된 오차 수.</summary>
        public int OpenCount;

        /// <summary>허용 안 된 오차가 있는 IO 수 (1.63 — 메인 칸 "차이 발생 N 개" 가 이 수). 단계 오차는 IO 가 아니라 안 셈.</summary>
        public int OpenIoCount;
        public int AllowedCount;

        /// <summary>판정 까닭 — 허용 안 된 오차 중 나쁜 것부터 다섯.</summary>
        public List<string> Reasons = new List<string>();

        /// <summary>알아 둘 점 (단계 없이 비교함 등).</summary>
        public string Note = string.Empty;

        /// <summary>단계별로 견줬는지 · 단계 수.</summary>
        public bool ByStep;
        public int StepCount;

        /// <summary>시간축이 시각(ms)인지. 아니면 길이를 시간축 단위 그대로 적습니다.</summary>
        public bool Clock;
        public string TimeUnit = string.Empty;

        /// <summary>견준 IO 수.</summary>
        public int IoCount;

        public string Word { get { return WordOf(Grade); } }

        public static string WordOf(JudgeGrade g)
        {
            return g == JudgeGrade.Critical ? "CRITICAL" : g == JudgeGrade.Warning ? "WARNING" : "PASS";
        }

        /// <summary>길이를 글로 — 시각 로그면 "0.1초" · "8분 3초".</summary>
        public string Duration(double d)
        {
            return JudgeEngine.DurationText(d, Clock, TimeUnit);
        }
    }

    public static class JudgeEngine
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>단계 하나 (또는 단계 없이 전체) — 견줄 구간.</summary>
        private sealed class Seg
        {
            public string Step = string.Empty;
            public int B0, B1, A0, A1;   // 표본 번호 (끝 포함)
            public double Shift;         // 이후 시각 + Shift = 이전 시각
            public double Slack;         // 시간 여유
            public double Length;        // 이전 로그의 이 구간 길이
        }

        /// <summary>한 방향으로 본 오차 덩어리 (이전 로그 시간축).</summary>
        private sealed class ErrRun
        {
            public double Start, End, Excess, Percent = double.NaN, PeakTime;
            public string PeakText = string.Empty;
            public bool Reverse;
            public bool Forward;

            /// <summary>
            /// 훑은 쪽(target) 로그 자신의 시간 (1.63). 이후 → 이전으로 훑었으면 이후 로그 시각, 거꾸로면 이전 로그 시각.
            /// 단계 맞춤 · 전체 맞춤의 오차를 견줄 때는 이 시간으로 견줍니다 — 이전 로그 시간축으로 옮기면
            /// 밀기 값이 달라 같은 튐이 서로 다른 자리에 놓입니다. Off = 이전 로그 시각 − 이 시각.
            /// </summary>
            public double Ts, Te, Off;
        }

        public static JudgeResult Run(LogDataset before, LogDataset after, StepPairList pairs,
                                      JudgeOptions opt, LoadProgress prog)
        {
            if (opt == null) opt = new JudgeOptions();
            var res = new JudgeResult();
            if (before == null || after == null || before.SampleCount == 0 || after.SampleCount == 0)
                return res;

            res.Clock = before.TimeKind == TimeKind.ClockMs || before.TimeKind == TimeKind.DateMs;
            res.TimeUnit = before.TimeKind == TimeKind.Index ? "표본" : (before.TimeUnit ?? string.Empty);

            List<Seg> segs = Segments(before, after, pairs, opt, res);
            Seg whole = WholeSeg(before, after, opt, res);

            var afterUsed = new bool[after.ChannelCount];
            for (int i = 0; i < before.ChannelCount; i++)
            {
                if (prog != null && (i & 0x0F) == 0)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("정상 판정 — IO " + i + " / " + before.ChannelCount,
                                before.ChannelCount > 0 ? (double)i / before.ChannelCount : -1);
                }

                Channel bc = before.Channels[i];
                int j = after.FindChannel(bc.Name);
                if (opt.Exclude != null && opt.Exclude.Contains(LogDataset.LooseKey(bc.Name)))
                {
                    if (j >= 0) afterUsed[j] = true;
                    continue;
                }
                if (j < 0)
                {
                    var it = new JudgeItem();
                    it.Kind = JudgeKind.IoMissing;
                    it.Io = bc.Name;
                    it.What = bc.Name + " — 이후 로그에 없음";
                    it.Detail = "이전 로그에만 있는 IO 입니다.";
                    res.Items.Add(it);
                    continue;
                }
                afterUsed[j] = true;
                res.IoCount++;
                CompareIo(before, i, after, j, segs, whole, opt, res);
            }
            for (int j = 0; j < after.ChannelCount; j++)
            {
                if (afterUsed[j]) continue;
                var it = new JudgeItem();
                it.Kind = JudgeKind.IoExtra;
                it.Io = after.Channels[j].Name;
                it.What = it.Io + " — 이후 로그에만 있음";
                it.Detail = "이전 로그에 없는 IO 입니다.";
                res.Items.Add(it);
            }

            if (!double.IsNaN(opt.ViewShift))
            {
                const string v = "분석 1 그래프에서 고른 단계의 시작 맞춤으로도 봐서, 거기서 맞는 곳은 오차로 세지 않았습니다.";
                res.Note = res.Note.Length > 0 ? res.Note + " " + v : v;
            }
            Regrade(res, opt);
            return res;
        }

        // ---------------- 구간 (단계) ----------------

        private static List<Seg> Segments(LogDataset before, LogDataset after, StepPairList pairs,
                                          JudgeOptions opt, JudgeResult res)
        {
            var segs = new List<Seg>();
            bool useSteps = pairs != null && pairs.UsableCount > 0
                         && before.HasSteps && after.HasSteps;

            if (!useSteps)
            {
                if (before.HasSteps != after.HasSteps)
                    res.Note = "한쪽 로그에만 단계가 있어 단계 없이 전체를 한 구간으로 견줬습니다.";

                Seg s = WholeSeg(before, after, opt, res);
                double t0 = Math.Max(before.TimeStart, after.TimeStart + s.Shift);
                double t1 = Math.Min(before.TimeEnd, after.TimeEnd + s.Shift);
                if (t1 <= t0)
                    res.Note = "두 로그의 시간 구간이 겹치지 않습니다. 시간 맞추기를 확인해 주세요.";
                segs.Add(s);
                return segs;
            }

            res.ByStep = true;
            int lastAfter = -1;
            var orderBroken = new List<string>();

            for (int k = 0; k < pairs.Count; k++)
            {
                StepPair p = pairs[k];
                if (!p.HasAfter)
                {
                    res.Items.Add(StepItem(JudgeKind.StepMissing, p.Label, p.BeforeStart,
                        "단계 " + p.Label + " — 이후 로그에 없음",
                        "이전 로그에 있던 단계가 이후 로그에 없습니다. 공정 순서가 다릅니다."));
                    continue;
                }
                if (!p.HasBefore)
                {
                    res.Items.Add(StepItem(JudgeKind.StepExtra, p.Label, p.AfterStart,
                        "단계 " + p.Label + " — 이후 로그에만 있음",
                        "이전 로그에 없던 단계입니다. 공정 순서가 다릅니다."));
                    continue;
                }

                res.StepCount++;
                if (p.AfterIndex < lastAfter) orderBroken.Add(p.Label);
                lastAfter = Math.Max(lastAfter, p.AfterIndex);

                LogStep bs = before.Steps[p.BeforeIndex];
                LogStep as_ = after.Steps[p.AfterIndex];
                var s = new Seg();
                s.Step = p.Label;
                s.B0 = bs.First; s.B1 = bs.Last;
                s.A0 = as_.First; s.A1 = as_.Last;
                s.Shift = p.Shift;
                s.Length = p.BeforeLength > 0 ? p.BeforeLength : 0;
                s.Slack = Slack(before, s.B0, s.B1, after, s.A0, s.A1, s.Length, res.Clock, opt);
                segs.Add(s);

                // 단계 길이 — 늘이거나 줄이지 않고 그대로 알립니다 (146). 시간 여유 안은 같음.
                double diff = p.AfterLength - p.BeforeLength;
                if (!double.IsNaN(diff) && Math.Abs(diff) > s.Slack)
                {
                    var it = new JudgeItem();
                    it.Kind = JudgeKind.StepLength;
                    it.Step = p.Label;
                    it.Start = p.BeforeStart;
                    it.End = p.BeforeEnd;
                    it.MaxExcess = diff;
                    it.SegLength = s.Length;
                    it.MaxExcessPercent = s.Length > 0 ? Math.Abs(diff) / s.Length * 100.0 : double.NaN;
                    it.What = "단계 " + p.Label + " — 길이 " + (diff > 0 ? "+" : "-")
                            + DurationText(Math.Abs(diff), res.Clock, res.TimeUnit);
                    it.Detail = "이전 " + DurationText(p.BeforeLength, res.Clock, res.TimeUnit)
                              + " → 이후 " + DurationText(p.AfterLength, res.Clock, res.TimeUnit)
                              + (double.IsNaN(it.MaxExcessPercent) ? string.Empty
                                 : " (" + it.MaxExcessPercent.ToString("0.#", Inv) + " %)")
                              + " · 시간 여유 " + DurationText(s.Slack, res.Clock, res.TimeUnit);
                    res.Items.Add(it);
                }
            }

            if (orderBroken.Count > 0)
            {
                res.Items.Add(StepItem(JudgeKind.StepOrder, string.Empty, before.TimeStart,
                    "단계 차례가 다름",
                    "이후 로그에서 차례가 바뀐 단계: " + string.Join(", ", orderBroken.ToArray())));
            }
            return segs;
        }

        /// <summary>
        /// 로그 전체를 한 구간으로 — <b>단계 없이 볼 때와 똑같은</b> 구간 (밀기 값 opt.Shift, 시간 여유 = 전체 길이 기준).
        /// 단계가 있을 때도 이것으로 한 번 더 봐서 겹친 곳만 남기므로, 단계를 켜서 오차 IO 가 늘 수 없습니다 (1.70).
        /// </summary>
        private static Seg WholeSeg(LogDataset before, LogDataset after, JudgeOptions opt, JudgeResult res)
        {
            var s = new Seg();
            s.B0 = 0; s.B1 = before.SampleCount - 1;
            s.A0 = 0; s.A1 = after.SampleCount - 1;
            s.Shift = opt.Shift;
            double t0 = Math.Max(before.TimeStart, after.TimeStart + s.Shift);
            double t1 = Math.Min(before.TimeEnd, after.TimeEnd + s.Shift);
            s.Length = t1 > t0 ? t1 - t0 : before.TimeEnd - before.TimeStart;
            s.Slack = Slack(before, s.B0, s.B1, after, s.A0, s.A1, s.Length, res.Clock, opt);
            return s;
        }

        private static JudgeItem StepItem(JudgeKind kind, string step, double at, string what, string detail)
        {
            var it = new JudgeItem();
            it.Kind = kind;
            it.Step = step ?? string.Empty;
            it.Start = double.IsNaN(at) ? 0 : at;
            it.End = it.Start;
            it.What = what;
            it.Detail = detail;
            return it;
        }

        /// <summary>
        /// 시간 여유 = 단계 길이 × % , 최소 표본 간격 × 2, 최대 5 초 (시각 로그).
        /// 표본 간격은 두 로그 중 성긴 쪽 — 성긴 쪽 한 칸보다 작은 여유는 뜻이 없습니다.
        /// </summary>
        private static double Slack(LogDataset b, int b0, int b1, LogDataset a, int a0, int a1,
                                    double length, bool clock, JudgeOptions opt)
        {
            double step = Math.Max(MeanInterval(b, b0, b1), MeanInterval(a, a0, a1));
            double min = step * Math.Max(0, opt.SlackMinSamples);
            double s = length > 0 ? length * opt.SlackPercent / 100.0 : 0;
            if (clock && opt.SlackMaxMs > 0 && s > opt.SlackMaxMs) s = opt.SlackMaxMs;
            if (s < min) s = min;
            return s;
        }

        private static double MeanInterval(LogDataset ds, int i0, int i1)
        {
            if (i1 <= i0 || i0 < 0 || i1 >= ds.SampleCount) return 0;
            double d = ds.Times[i1] - ds.Times[i0];
            return d > 0 ? d / (i1 - i0) : 0;
        }

        // ---------------- IO 하나 ----------------

        private static void CompareIo(LogDataset before, int bi, LogDataset after, int ai,
                                      List<Seg> segs, Seg whole, JudgeOptions opt, JudgeResult res)
        {
            Channel bc = before.Channels[bi];
            Channel ac = after.Channels[ai];
            bool cat = bc.Kind == ChannelKind.State || ac.Kind == ChannelKind.State;

            double abs = 0, pct = ToleranceRule.DefaultPercent;
            if (opt.Tolerances != null)
            {
                abs = opt.Tolerances.AbsoluteFor(bc.Name);
                pct = opt.Tolerances.PercentFor(bc.Name);
            }

            // 1.70: 전체 맞춤 · 고른 단계 맞춤은 <b>단계 없이 볼 때와 똑같이</b> (로그 전체 · 전체 길이로 정한 시간 여유)
            // IO 마다 한 번만 훑어 둡니다 (오차가 있는 단계가 나올 때 처음 한 번). 1.63 ~ 1.69 에는 이것을 단계 구간 ·
            // 단계 시간 여유로 봐서, 짧은 단계는 여유가 좁고 단계 경계에서 창이 잘려 <b>단계를 켜면 오차가 늘었습니다</b>.
            List<ErrRun> wholeRuns = null, viewRuns = null;
            bool useView = !double.IsNaN(opt.ViewShift) && Math.Abs(opt.ViewShift - whole.Shift) > 1e-9;

            for (int s = 0; s < segs.Count; s++)
            {
                Seg seg = segs[s];
                var runs = new List<ErrRun>();
                // 이후 → 이전: 이후 값이 이전 범위 밖
                Scan(after, ai, seg.A0, seg.A1, before, bi, seg.B0, seg.B1, seg.Shift, seg.Slack,
                     cat, abs, pct, false, runs);
                // 이전 → 이후: 이전에 있던 모양이 이후에 없음 (짧은 펄스가 빠진 것)
                Scan(before, bi, seg.B0, seg.B1, after, ai, seg.A0, seg.A1, -seg.Shift, seg.Slack,
                     cat, abs, pct, true, runs);
                if (runs.Count == 0) continue;

                List<ErrRun> merged;

                // 1.63: 단계로 나눠 맞추면 없던 오차가 <b>새로 생기는</b> 자리가 있습니다 — 단계 시작과 상관없이
                // 움직인 IO 를 단계 시작에 맞춰 버린 경우 (단계 경계 바로 뒤 등). 그래서 같은 구간을 <b>전체 맞춤</b>
                // (단계 없이 쓰는 밀기 값) 으로도 보고, <b>두 맞춤 모두에서 벗어난 곳만</b> 오차로 남깁니다.
                // 단계로 맞춰 사라진 오차는 그대로 사라지고, 단계로 맞춰 생긴 오차는 없어집니다.
                List<ErrRun> kept = runs;
                if (res.ByStep)
                {
                    // 방향마다, 훑은 쪽 로그 자신의 시간으로 겹치는 곳만 — 결과는 늘 "단계 없이 본 오차" 안
                    if (wholeRuns == null) wholeRuns = WholeRuns(before, bi, after, ai, whole, whole.Shift, cat, abs, pct);
                    kept = Intersect(kept, wholeRuns);
                }
                // 1.67: 사용자가 분석 1 에서 지금 건 맞춤 (그래프에서 고른 단계의 시작 맞춤 등) 으로도 봐서,
                // 거기서 맞으면 오차가 아닙니다 — 분석 1 에서 맞춰서 없어진 차이는 메인 칸에서도 없어집니다.
                // 그 맞춤으로 <b>새로 생기는</b> 차이는 겹친 곳만 남기므로 늘지 않습니다.
                if (useView && kept.Count > 0)
                {
                    if (viewRuns == null) viewRuns = WholeRuns(before, bi, after, ai, whole, opt.ViewShift, cat, abs, pct);
                    kept = Intersect(kept, viewRuns);
                }
                if (kept.Count == 0) continue;
                merged = Merge(kept, seg.Slack);

                string hint = string.Empty, hintShort = string.Empty;
                if (res.HintTried < MaxHintTries)
                {
                    res.HintTried++;
                    Suggest(before, bi, after, ai, seg, cat, abs, pct, res, out hint, out hintShort);
                }

                foreach (ErrRun r in merged)
                {
                    var it = new JudgeItem();
                    it.Kind = JudgeKind.Value;
                    it.Io = bc.Name;
                    it.Step = seg.Step;
                    it.Start = r.Start;
                    it.End = r.End;
                    it.Duration = Math.Max(0, r.End - r.Start);
                    it.MaxExcess = r.Excess;
                    it.MaxExcessPercent = r.Percent;
                    it.PeakTime = r.PeakTime;
                    it.PeakText = r.PeakText;
                    it.MissingInAfter = r.Reverse && !r.Forward;
                    it.SegLength = seg.Length;
                    it.Hint = hint;
                    it.HintShort = hintShort;
                    it.What = bc.Name + (seg.Step.Length > 0 ? "  ·  " + seg.Step : string.Empty)
                            + "  ·  " + before.FormatTime(r.Start)
                            + "  ·  " + DurationText(it.Duration, res.Clock, res.TimeUnit);
                    res.Items.Add(it);
                }
            }
        }

        /// <summary>로그 전체를 밀기 값 shift 로, 전체 구간의 시간 여유로 견준 오차 (두 방향) — 단계 없이 볼 때와 같은 셈.</summary>
        private static List<ErrRun> WholeRuns(LogDataset before, int bi, LogDataset after, int ai, Seg whole,
                                              double shift, bool cat, double abs, double pct)
        {
            var g = new List<ErrRun>();
            Scan(after, ai, whole.A0, whole.A1, before, bi, whole.B0, whole.B1, shift, whole.Slack, cat, abs, pct, false, g);
            Scan(before, bi, whole.B0, whole.B1, after, ai, whole.A0, whole.A1, -shift, whole.Slack, cat, abs, pct, true, g);
            return g;
        }

        // ---------------- 보정 제안 (1.66) ----------------

        /// <summary>보정 제안을 찾아볼 IO · 단계 수 한도 — 넘으면 나머지는 제안 없이 (판정은 그대로).</summary>
        private const int MaxHintTries = 150;

        /// <summary>제안으로 남기려면 오차가 이만큼 이하로 줄어야 합니다 (처음 오차 대비).</summary>
        private const double HintKeepRatio = 0.1;

        /// <summary>보정 제안에 쓸 일의 양 한도 (훑는 표본 수 합 — 대략 1 ~ 2 초).</summary>
        private const long MaxHintWork = 150000000;

        /// <summary>
        /// "시간을 조금 밀면 · 값을 조금 올리고 내리면 정상" 인지 찾아봅니다 (1.66). 판정 · 오차 목록은 바꾸지 않고 글만 냅니다.
        /// ① 시간: 이 단계 맞춤에서 ± (단계 길이 25 %, 시간 여유 10 배 중 작은 쪽) 안을 촘촘히 밀어 보며
        ///    오차 시간이 가장 짧은 곳 (그런 곳이 이어져 있으면 그 가운데). 처음의 10 % 이하로 줄면 제안.
        /// ② 값 (숫자 IO 만): 같은 시각끼리 (이후 − 이전) 의 가운데 값만큼 이후를 내려 보고, 처음의 10 % 이하로 줄면 제안.
        /// 오차 시간은 두 방향으로 훑은 길이의 합 — 판정과 같은 규칙 (시간 여유 · 값 허용) 그대로.
        /// </summary>
        private static void Suggest(LogDataset before, int bi, LogDataset after, int ai, Seg seg, bool cat,
                                    double abs, double pct, JudgeResult res, out string hint, out string hintShort)
        {
            hint = string.Empty; hintShort = string.Empty;
            double interval = Math.Max(MeanInterval(before, seg.B0, seg.B1), MeanInterval(after, seg.A0, seg.A1));
            double w = Math.Min(seg.Length * 0.25, seg.Slack * 10);
            double step = Math.Max(w / 16, interval);
            int kmax = step > 0 && w >= step ? (int)Math.Floor(w / step + 1e-9) : 0;
            long n = (long)(seg.B1 - seg.B0 + 1) + (seg.A1 - seg.A0 + 1);
            long cost = n * 2 * (2L * kmax + 3);   // 처음 · 시간 후보 · 값 — 후보마다 두 방향
            if (res.HintWork + cost > MaxHintWork) return;
            res.HintWork += cost;

            double baseErr = ErrAmount(before, bi, after, ai, seg, seg.Shift, 0, cat, abs, pct);
            if (baseErr <= 0) return;

            // ① 시간
            if (kmax > 0)
            {
                var err = new double[2 * kmax + 1];   // err[k + kmax] = d = k × step 일 때
                err[kmax] = baseErr;
                double best = baseErr;
                int bestK = 0;
                for (int m = 1; m <= kmax; m++)
                {
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        int k = sg * m;
                        double e = ErrAmount(before, bi, after, ai, seg, seg.Shift + k * step, 0, cat, abs, pct);
                        err[k + kmax] = e;
                        if (e < best - 1e-12) { best = e; bestK = k; }   // 같으면 덜 민 쪽
                    }
                }
                // 가장 좋은 곳이 이어진 구간이면 그 <b>가운데</b> — 시간 여유 때문에 덜 밀어도 맞아 보이지만,
                // 실제로 밀린 만큼은 구간 가운데입니다 (8 초 밀림 · 여유 5 초면 3 ~ 13 초가 다 정상 → 8 초).
                int kl = bestK, kr = bestK;
                while (kl - 1 >= -kmax && kl - 1 != 0 && err[kl - 1 + kmax] <= best + 1e-12) kl--;
                while (kr + 1 <= kmax && kr + 1 != 0 && err[kr + 1 + kmax] <= best + 1e-12) kr++;
                double bestD = (kl + kr) / 2.0 * step;
                if (bestK != 0 && best <= baseErr * HintKeepRatio)
                {
                    // 이후 시각 + Shift = 이전 시각 → Shift 를 d 늘리는 것 = 이후 로그를 d 만큼 늦추는 것
                    string amount = DurationText(Math.Abs(bestD), res.Clock, res.TimeUnit);
                    hint = "이후 로그를 " + amount + (bestD > 0 ? " 늦추면(뒤로)" : " 앞당기면")
                         + " 이 단계 오차가 " + (best <= 0 ? "없어집니다" : "거의 없어집니다")
                         + " — 파형은 같고 시간만 밀린 것 같습니다";
                    hintShort = "시간 " + (bestD > 0 ? "+" : "−") + amount;
                    return;
                }
            }

            // ② 값 (숫자 IO)
            if (cat) return;
            double off = MedianOffset(before, bi, after, ai, seg);
            if (double.IsNaN(off) || off == 0) return;
            double e2 = ErrAmount(before, bi, after, ai, seg, seg.Shift, off, cat, abs, pct);
            if (e2 <= baseErr * HintKeepRatio)
            {
                string amount = NumberText.Plain(Math.Abs(off));
                hint = "이후 값이 이전보다 " + amount + (off > 0 ? " 높습니다 — " + amount + " 내리면" : " 낮습니다 — " + amount + " 올리면")
                     + " 이 단계 오차가 " + (e2 <= 0 ? "없어집니다" : "거의 없어집니다")
                     + " (모양은 같고 높이만 다름 · 영점/오프셋 확인)";
                hintShort = "값 " + (off > 0 ? "−" : "+") + amount;
            }
        }

        /// <summary>
        /// 이 단계를 밀기 값 shift · 값 보정 off (이후 값 − off 를 이전과 견줌) 로 볼 때의 오차 비율 (두 방향 오차 시간 합 ÷ 견준 시간).
        /// 이후 로그 범위는 밀기 값에 맞춰 다시 잡아 끝자락이 빠져서 오차가 줄어 보이지 않게 합니다.
        /// </summary>
        private static double ErrAmount(LogDataset before, int bi, LogDataset after, int ai, Seg seg,
                                        double shift, double off, bool cat, double abs, double pct)
        {
            int a0 = seg.A0, a1 = seg.A1;
            if (shift != seg.Shift)
            {
                if (!AfterRange(before, after, seg.B0, seg.B1, shift, out a0, out a1)) return double.MaxValue;
            }
            var runs = new List<ErrRun>();
            Scan(after, ai, a0, a1, before, bi, seg.B0, seg.B1, shift, seg.Slack, cat, abs, pct, false, runs, -off, true);
            Scan(before, bi, seg.B0, seg.B1, after, ai, a0, a1, -shift, seg.Slack, cat, abs, pct, true, runs, off, true);
            double sum = 0;
            foreach (ErrRun r in runs) sum += Math.Max(0, r.End - r.Start);
            // 비율로 (오차 시간 ÷ 견준 시간) — 밀면 로그 끝에서 견줄 곳이 조금 줄어드니, 길이 대신 비율로 견줍니다.
            double covered = a1 >= a0 && seg.B1 >= seg.B0
                ? Math.Min(after.Times[a1] - after.Times[a0], before.Times[seg.B1] - before.Times[seg.B0]) : 0;
            if (covered <= 0) return sum > 0 ? double.MaxValue : 0;
            return sum / covered;
        }

        /// <summary>같은 시각끼리 (이후 − 이전) 의 가운데 값. 표본은 최대 4000 개만 (고르게 건너뜀).</summary>
        private static double MedianOffset(LogDataset before, int bi, LogDataset after, int ai, Seg seg)
        {
            int n = seg.B1 - seg.B0 + 1;
            if (n <= 0) return double.NaN;
            int stride = Math.Max(1, n / 4000);
            var d = new List<double>();
            float[] bv = before.Channels[bi].Values;
            for (int k = seg.B0; k <= seg.B1; k += stride)
            {
                float b = bv[k];
                if (float.IsNaN(b)) continue;
                double a = after.SampleAt(ai, before.Times[k] - seg.Shift);
                if (double.IsNaN(a)) continue;
                d.Add(a - b);
            }
            if (d.Count == 0) return double.NaN;
            d.Sort();
            int c = d.Count;
            return c % 2 == 1 ? d[c / 2] : (d[c / 2 - 1] + d[c / 2]) / 2.0;
        }

        /// <summary>
        /// 한 방향으로 훑습니다. target 의 표본마다, ref 에서 그 시각 앞뒤 slack 안의 값 범위
        /// (최소 ~ 최대, 미는 창 — 표본마다 다시 찾지 않음) 를 구해 견줍니다.
        /// shift: target 시각 + shift = ref 시각. reverse 면 target 이 이전 로그.
        /// </summary>
        private static void Scan(LogDataset tgt, int ti, int t0, int t1,
                                 LogDataset rf, int ri, int r0, int r1,
                                 double shift, double slack, bool cat, double abs, double pct,
                                 bool reverse, List<ErrRun> runs, double addToTarget = 0, bool quick = false)
        {
            if (t1 < t0 || r1 < r0) return;
            double[] xt = tgt.Times; float[] xv = tgt.Channels[ti].Values;
            double[] rt = rf.Times; float[] rv = rf.Channels[ri].Values;
            Channel tc = tgt.Channels[ti], rc = rf.Channels[ri];

            double refStart = rt[r0], refEnd = rt[r1];
            double interval = MeanInterval(tgt, t0, t1);

            int cap = r1 - r0 + 1;
            var dqMin = new int[cap]; int minHead = 0, minTail = 0;
            var dqMax = new int[cap]; int maxHead = 0, maxTail = 0;
            Dictionary<int, int> counts = cat ? new Dictionary<int, int>() : null;
            int[] map = cat ? StateMap(tc, rc) : null;   // target 상태 번호 → ref 상태 번호
            int add = r0, drop = r0;

            ErrRun cur = null;
            for (int k = t0; k <= t1; k++)
            {
                double t = xt[k] + shift;                    // ref 시간축
                double outT = reverse ? xt[k] : t;           // 이전 로그 시간축
                float x = cat || addToTarget == 0 ? xv[k] : (float)(xv[k] + addToTarget);   // 1.66 값 보정 제안용
                if (t < refStart || t > refEnd || float.IsNaN(x)) { cur = Close(cur, runs); continue; }

                while (add <= r1 && rt[add] <= t + slack)
                {
                    float v = rv[add];
                    if (!float.IsNaN(v))
                    {
                        if (cat)
                        {
                            int key = (int)Math.Round(v);
                            int c; counts.TryGetValue(key, out c); counts[key] = c + 1;
                        }
                        else
                        {
                            while (minTail > minHead && rv[dqMin[minTail - 1]] >= v) minTail--;
                            dqMin[minTail++] = add;
                            while (maxTail > maxHead && rv[dqMax[maxTail - 1]] <= v) maxTail--;
                            dqMax[maxTail++] = add;
                        }
                    }
                    add++;
                }
                while (drop < add && rt[drop] < t - slack)
                {
                    float v = rv[drop];
                    if (cat && !float.IsNaN(v))
                    {
                        int key = (int)Math.Round(v);
                        int c;
                        if (counts.TryGetValue(key, out c)) { if (c <= 1) counts.Remove(key); else counts[key] = c - 1; }
                    }
                    drop++;
                }
                if (!cat)
                {
                    while (minTail > minHead && dqMin[minHead] < drop) minHead++;
                    while (maxTail > maxHead && dqMax[maxHead] < drop) maxHead++;
                }

                // 그 시각의 값 (보간 · 계단) 도 범위에 넣습니다 — 시간 여유가 표본 간격보다
                // 작아 창이 비어도 "같은 시각끼리" 는 늘 견줍니다 (여유 0 = 예전 비교).
                double at = rf.SampleAt(ri, t);

                bool inside;
                double excess = 0, percent = double.NaN;
                double tLo = double.NaN, tHi = double.NaN;   // 글은 새 최대일 때만 만듭니다 (1.66 — 표본마다 만들면 느림)

                if (cat)
                {
                    int xs = (int)Math.Round(x);
                    int mapped = xs >= 0 && xs < map.Length ? map[xs] : -1;
                    int atKey = double.IsNaN(at) ? int.MinValue : (int)Math.Round(at);
                    if (counts.Count == 0 && atKey == int.MinValue) { cur = Close(cur, runs); continue; }
                    inside = mapped >= 0 && (counts.ContainsKey(mapped) || atKey == mapped);
                    if (inside) { cur = Close(cur, runs); continue; }
                    excess = 1;
                    percent = ToleranceRule.NameMismatchPercent;
                    tLo = atKey == int.MinValue ? double.NaN : at;
                }
                else
                {
                    double lo = double.NaN, hi = double.NaN;
                    if (minTail > minHead) { lo = rv[dqMin[minHead]]; hi = rv[dqMax[maxHead]]; }
                    if (!double.IsNaN(at))
                    {
                        if (double.IsNaN(lo) || at < lo) lo = at;
                        if (double.IsNaN(hi) || at > hi) hi = at;
                    }
                    if (double.IsNaN(lo)) { cur = Close(cur, runs); continue; }

                    inside = (x >= lo && x <= hi)
                          || !ToleranceRule.IsOver(lo, x, abs, pct)
                          || !ToleranceRule.IsOver(hi, x, abs, pct);
                    if (inside) { cur = Close(cur, runs); continue; }

                    if (x > hi) { excess = x - hi; percent = ToleranceRule.ErrorPercent(hi, x); }
                    else { excess = lo - x; percent = ToleranceRule.ErrorPercent(lo, x); }
                    tLo = lo; tHi = hi;
                }

                double end = outT + interval;
                if (cur == null)
                {
                    cur = new ErrRun();
                    cur.Start = outT;
                    cur.Ts = xt[k];
                    cur.Off = outT - xt[k];
                    cur.Reverse = reverse;
                    cur.Forward = !reverse;
                    cur.Excess = -1;
                }
                cur.End = end;
                cur.Te = xt[k] + interval;
                if (excess > cur.Excess)
                {
                    cur.Excess = excess;
                    cur.Percent = percent;
                    cur.PeakTime = outT;
                    cur.PeakText = quick ? string.Empty
                        : cat ? Side(reverse, true) + " '" + tc.FormatValue(x) + "' · "
                                + Side(reverse, false) + " '" + (double.IsNaN(tLo) ? "-" : rc.FormatValue(tLo)) + "'"
                              : Side(reverse, true) + " " + tc.FormatValue(x) + " · "
                                + Side(reverse, false) + " 범위 " + rc.FormatValue(tLo) + " ~ " + rc.FormatValue(tHi);
                }
            }
            Close(cur, runs);
        }

        private static string Side(bool reverse, bool target)
        {
            // target 이 이후 로그면 (reverse 아님) "이후 값 · 이전 범위".
            return (target != reverse) ? "이후" : "이전";
        }

        private static ErrRun Close(ErrRun cur, List<ErrRun> runs)
        {
            if (cur != null) runs.Add(cur);
            return null;
        }

        /// <summary>target 상태 번호 → ref 상태 번호 (이름으로). 없으면 -1.</summary>
        private static int[] StateMap(Channel tc, Channel rc)
        {
            int n = tc.Kind == ChannelKind.State && tc.States != null ? tc.States.Length : 0;
            if (n == 0)
            {
                // 상태 이름이 없는 쪽 (숫자 0/1 등) 은 값 그대로를 이름으로 봅니다.
                n = 256;
            }
            var map = new int[n];
            for (int i = 0; i < n; i++)
            {
                string name = tc.FormatValue(i);
                map[i] = IndexOfState(rc, name);
            }
            return map;
        }

        private static int IndexOfState(Channel c, string name)
        {
            if (c.Kind == ChannelKind.State && c.States != null)
            {
                for (int i = 0; i < c.States.Length; i++)
                    if (string.Equals(c.States[i], name, StringComparison.Ordinal)) return i;
                return -1;
            }
            double v;
            if (double.TryParse(name, NumberStyles.Float, Inv, out v) && v == Math.Round(v)) return (int)v;
            return -1;
        }

        /// <summary>이전 로그 [b0, b1] 구간에 (밀기 값 shift 로) 겹치는 이후 로그 표본 범위. 없으면 false.</summary>
        private static bool AfterRange(LogDataset before, LogDataset after, int b0, int b1, double shift,
                                       out int a0, out int a1)
        {
            a0 = 0; a1 = -1;
            if (after.SampleCount == 0 || b1 < b0) return false;
            double t0 = before.Times[b0] - shift, t1 = before.Times[b1] - shift;   // 이후 시간축
            int i0 = after.IndexAtOrBefore(t0);
            if (i0 < 0) i0 = 0;
            else if (after.Times[i0] < t0) i0++;
            int i1 = after.IndexAtOrBefore(t1);
            if (i1 < i0 || i0 >= after.SampleCount) return false;
            a0 = i0; a1 = i1;
            return true;
        }

        /// <summary>
        /// 두 오차 목록이 <b>겹치는 곳</b>만 (1.63 — 단계 맞춤 · 전체 맞춤 둘 다에서 벗어난 곳). 같은 방향끼리,
        /// <b>훑은 쪽 로그 자신의 시간</b>(Ts · Te)으로 겹침을 봅니다. 결과는 단계 맞춤 쪽의 이전 로그 시간축에 놓고,
        /// 크기 · 그 순간 글은 단계 맞춤 쪽 것 (그 순간이 겹친 곳 밖이면 전체 맞춤 쪽).
        /// 방향마다 Ts 로 늘어놓고 두 손가락으로 훑습니다 (오차가 수천 개인 IO 에서도 길이에 비례).
        /// </summary>
        private static List<ErrRun> Intersect(List<ErrRun> stepRuns, List<ErrRun> globalRuns)
        {
            var outList = new List<ErrRun>();
            for (int dir = 0; dir < 2; dir++)
            {
                bool reverse = dir == 1;
                var a = new List<ErrRun>();
                var b = new List<ErrRun>();
                foreach (ErrRun r in stepRuns) if (r.Reverse == reverse) a.Add(r);
                foreach (ErrRun g in globalRuns) if (g.Reverse == reverse) b.Add(g);
                Comparison<ErrRun> byTs = delegate (ErrRun x, ErrRun y) { return x.Ts.CompareTo(y.Ts); };
                a.Sort(byTs);
                b.Sort(byTs);

                int j = 0;
                foreach (ErrRun r in a)
                {
                    while (j < b.Count && b[j].Te <= r.Ts) j++;
                    for (int k = j; k < b.Count && b[k].Ts < r.Te; k++)
                    {
                        ErrRun g = b[k];
                        double s = Math.Max(r.Ts, g.Ts), e = Math.Min(r.Te, g.Te);
                        if (!(e > s)) continue;
                        double rPeak = r.PeakTime - r.Off, gPeak = g.PeakTime - g.Off;
                        bool useR = (rPeak >= s && rPeak <= e) || !(gPeak >= s && gPeak <= e);
                        var x = new ErrRun();
                        x.Ts = s; x.Te = e; x.Off = r.Off;
                        x.Start = s + r.Off; x.End = e + r.Off;
                        ErrRun src = useR ? r : g;
                        x.Excess = src.Excess; x.Percent = src.Percent; x.PeakText = src.PeakText;
                        x.PeakTime = useR ? r.PeakTime : gPeak + r.Off;
                        x.Reverse = r.Reverse; x.Forward = r.Forward;
                        outList.Add(x);
                    }
                }
            }
            return outList;
        }

        /// <summary>
        /// 두 방향의 오차를 하나로 — 겹치거나 시간 여유 안에 붙어 있으면 한 항목.
        /// 지우는 것이 아니라 <b>묶는</b> 것입니다 (길이는 처음부터 끝까지).
        /// </summary>
        private static List<ErrRun> Merge(List<ErrRun> runs, double gap)
        {
            runs.Sort(delegate (ErrRun a, ErrRun b) { return a.Start.CompareTo(b.Start); });
            var outList = new List<ErrRun>();
            ErrRun cur = null;
            foreach (ErrRun r in runs)
            {
                if (cur != null && r.Start <= cur.End + gap)
                {
                    if (r.End > cur.End) cur.End = r.End;
                    if (r.Excess > cur.Excess)
                    {
                        cur.Excess = r.Excess; cur.Percent = r.Percent;
                        cur.PeakTime = r.PeakTime; cur.PeakText = r.PeakText;
                    }
                    cur.Reverse |= r.Reverse;
                    cur.Forward |= r.Forward;
                    continue;
                }
                cur = new ErrRun();
                cur.Start = r.Start; cur.End = r.End; cur.Excess = r.Excess; cur.Percent = r.Percent;
                cur.PeakTime = r.PeakTime; cur.PeakText = r.PeakText;
                cur.Reverse = r.Reverse; cur.Forward = r.Forward;
                outList.Add(cur);
            }
            return outList;
        }

        // ---------------- 허용 · 판정 ----------------

        /// <summary>
        /// 허용을 다시 적용하고 판정을 다시 냅니다. 오차를 다시 찾지는 않습니다 —
        /// 목록에서 허용을 바꿀 때마다 이것만 부릅니다 (빠름).
        /// </summary>
        public static void Regrade(JudgeResult res, JudgeOptions opt)
        {
            if (res == null) return;
            if (opt == null) opt = new JudgeOptions();

            foreach (JudgeItem it in res.Items)
            {
                if (opt.Once != null && opt.Once.Contains(it.Key)) it.Allowed = AllowState.Once;
                else if (it.CanAllowAlways && MatchesAny(it, opt.Rules)) it.Allowed = AllowState.Rule;
                else it.Allowed = AllowState.None;
            }

            // IO · 단계마다 허용 안 된 오차 시간 → 비율
            var errTime = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JudgeItem it in res.Items)
            {
                if (it.Kind != JudgeKind.Value || it.IsAllowed) continue;
                string k = it.Io + "\n" + it.Step;
                double v; errTime.TryGetValue(k, out v);
                errTime[k] = v + it.Duration;
            }

            foreach (JudgeItem it in res.Items)
            {
                switch (it.Kind)
                {
                    case JudgeKind.Value:
                        {
                            double v; errTime.TryGetValue(it.Io + "\n" + it.Step, out v);
                            it.StepRatio = it.SegLength > 0 ? Math.Min(1.0, v / it.SegLength) : 0;
                            it.Grade = it.StepRatio >= opt.CriticalRatio ? JudgeGrade.Critical : JudgeGrade.Warning;
                            it.Detail = it.PeakText
                                      + (it.MissingInAfter ? " (이전에 있던 모양이 이후에 없음)" : string.Empty)
                                      + " · 최대 " + NumberText.Plain(it.MaxExcess)
                                      + (double.IsNaN(it.MaxExcessPercent) ? string.Empty
                                         : " (" + NumberText.Plain(it.MaxExcessPercent) + " %)")
                                      + " · 이 단계 오차 " + (it.StepRatio * 100.0).ToString("0.0", Inv) + " %"
                                      + (string.IsNullOrEmpty(it.Hint) ? string.Empty : " · 제안: " + it.Hint);
                            break;
                        }
                    case JudgeKind.StepMissing:
                    case JudgeKind.StepExtra:
                    case JudgeKind.StepOrder:
                        it.Grade = JudgeGrade.Critical;
                        break;
                    default:
                        it.Grade = JudgeGrade.Warning;
                        break;
                }
            }

            // IO 마다 정도 (⑤)
            var byIo = new Dictionary<string, JudgeIo>(StringComparer.Ordinal);
            res.Ios = new List<JudgeIo>();
            foreach (JudgeItem it in res.Items)
            {
                if (it.Kind != JudgeKind.Value) continue;
                JudgeIo io;
                if (!byIo.TryGetValue(it.Io, out io))
                {
                    io = new JudgeIo();
                    io.Name = it.Io;
                    byIo[it.Io] = io;
                    res.Ios.Add(io);
                }
                io.Items++;
                if (it.IsAllowed) { io.AllowedItems++; continue; }
                io.ErrorTime += it.Duration;
                if (it.Duration > io.LongestRun) io.LongestRun = it.Duration;
                if (it.MaxExcess > io.MaxExcess) io.MaxExcess = it.MaxExcess;
                if (it.StepRatio > io.WorstRatio || io.WorstStep.Length == 0 && it.StepRatio > 0)
                {
                    io.WorstRatio = it.StepRatio;
                    io.WorstStep = it.Step;
                }
                if (!io.Steps.Contains(it.Step)) io.Steps.Add(it.Step);
                if (it.Grade > io.Grade) io.Grade = it.Grade;
            }
            res.Ios.Sort(delegate (JudgeIo a, JudgeIo b)
            {
                int c = b.Grade.CompareTo(a.Grade);
                if (c != 0) return c;
                c = b.ErrorTime.CompareTo(a.ErrorTime);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            // 목록 차례: 허용 안 된 것 → 나쁜 것 → 긴 것
            res.Items.Sort(Compare);

            res.OpenCount = 0;
            var openIos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            res.AllowedCount = 0;
            res.Reasons = new List<string>();
            foreach (JudgeItem it in res.Items)
            {
                if (it.IsAllowed) { res.AllowedCount++; continue; }
                res.OpenCount++;
                if (it.Io.Length > 0) openIos.Add(it.Io);
                if (res.Reasons.Count < 5) res.Reasons.Add(JudgeResult.WordOf(it.Grade) + "  " + it.What);
            }
            res.OpenIoCount = openIos.Count;

            // 분석 1 판정 = 허용 안 된 오차가 있는 IO 수 (1.64): 0 → PASS, ~10 → WARNING, 넘으면 CRITICAL.
            // 구간 수로 하면 IO 하나의 잔떨림만으로 수가 커져서 IO 수로 셉니다.
            JudgeGrade g = res.OpenIoCount == 0 ? JudgeGrade.Pass
                         : res.OpenIoCount <= opt.CriticalIoCount ? JudgeGrade.Warning : JudgeGrade.Critical;
            foreach (JudgeItem it in res.Items)
            {
                if (it.IsAllowed) continue;
                if (it.Kind == JudgeKind.StepMissing || it.Kind == JudgeKind.StepExtra || it.Kind == JudgeKind.StepOrder)
                    g = JudgeGrade.Critical;                       // 공정 순서가 다름
                else if (it.Kind == JudgeKind.StepLength && g == JudgeGrade.Pass)
                    g = JudgeGrade.Warning;                        // 단계 길이만 다름
            }
            res.Grade = g;
        }

        private static int Compare(JudgeItem a, JudgeItem b)
        {
            int c = a.IsAllowed.CompareTo(b.IsAllowed);
            if (c != 0) return c;
            c = b.Grade.CompareTo(a.Grade);
            if (c != 0) return c;
            c = Size(b).CompareTo(Size(a));
            if (c != 0) return c;
            c = string.Compare(a.Io, b.Io, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : a.Start.CompareTo(b.Start);
        }

        private static double Size(JudgeItem it)
        {
            if (it.Kind == JudgeKind.Value) return it.Duration;
            if (it.Kind == JudgeKind.StepLength) return Math.Abs(it.MaxExcess);
            return double.MaxValue;   // 단계 · IO 가 없어진 것은 맨 위
        }

        public static bool MatchesAny(JudgeItem it, List<AllowRule> rules)
        {
            if (rules == null) return false;
            for (int i = 0; i < rules.Count; i++) if (Matches(it, rules[i])) return true;
            return false;
        }

        public static bool Matches(JudgeItem it, AllowRule r)
        {
            if (it == null || r == null) return false;
            if (!string.Equals(r.Kind, it.Kind.ToString(), StringComparison.Ordinal)) return false;
            if (!string.Equals(r.Io ?? string.Empty, it.Io, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(r.Step ?? string.Empty, it.Step, StringComparison.Ordinal)) return false;

            switch (it.Kind)
            {
                case JudgeKind.Value:
                    return LessOrSame(it.Duration, r.MaxDuration) && LessOrSame(it.MaxExcess, r.MaxExcess);
                case JudgeKind.StepLength:
                    return LessOrSame(Math.Abs(it.MaxExcess), r.MaxExcess);
                case JudgeKind.IoMissing:
                case JudgeKind.IoExtra:
                    return true;
                default:
                    return false;
            }
        }

        private static bool LessOrSame(double v, double limit)
        {
            return v <= limit + 1e-9 * Math.Max(1.0, Math.Abs(limit));
        }

        /// <summary>"앞으로도 허용" 규칙 — 이번에 본 크기까지만.</summary>
        public static AllowRule RuleFor(JudgeItem it)
        {
            var r = new AllowRule();
            r.Kind = it.Kind.ToString();
            r.Io = it.Io ?? string.Empty;
            r.Step = it.Step ?? string.Empty;
            r.MaxDuration = it.Duration;
            r.MaxExcess = Math.Abs(it.MaxExcess);
            r.CreatedAt = DateTime.Now;
            return r;
        }

        // ---------------- 글 ----------------

        /// <summary>길이 글. 시각 로그(ms)면 "0.1초" · "8분 3초" · "1시간 2분".</summary>
        public static string DurationText(double d, bool clock, string unit)
        {
            if (double.IsNaN(d)) return "-";
            if (!clock) return NumberText.Plain(d) + (string.IsNullOrEmpty(unit) ? string.Empty : " " + unit);

            double sec = d / 1000.0;
            if (sec < 60) return sec.ToString(sec < 10 ? "0.0#" : "0.#", Inv) + "초";
            if (sec < 3600)
            {
                int m = (int)(sec / 60);
                return m + "분 " + ((int)Math.Round(sec - m * 60)) + "초";
            }
            int h = (int)(sec / 3600);
            return h + "시간 " + ((int)((sec - h * 3600) / 60)) + "분";
        }
    }
}
