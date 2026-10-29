using System;
using System.Collections.Generic;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.Heatmap;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.LlmExport
{
    /// <summary>
    /// <b>분석 1 (로그 비교)</b> 을 LLM 이 읽을 표로 줄입니다 (1.43).
    ///
    /// 원본 로그는 수천만 칸이지만 LLM 에 그대로 주면 패턴을 못 찾습니다. 이 프로그램이 이미 계산한
    /// <b>차이</b>를 표로 줍니다 — 화면(대시보드 · 히트맵 · 그래프)과 <b>같은 계산</b>을 다시 불러
    /// 쓰므로 화면에 보이는 수와 LLM 이 받는 수가 같습니다.
    ///
    /// 만드는 섹션 (Id · 층 · 내용)
    /// <code>
    ///   a1.head      L1  파일 · 길이 · 시간 맞추기 · 허용 오차 · 비교 구간 · 판정 수
    ///   a1.groups    L1  그룹마다 IO 수 · 달라진 수 · 등급
    ///   a1.ios       L1  달라진 IO 전부 — 종류 · 그룹 · 최대차 · 최대차% · 평균차 · RMS · 차이 시간% ·
    ///                    차이 구간 수 · 처음 달라진 시각(대략) · 이전/이후 값 범위
    ///   a1.steps     L2  단계마다 이전/이후 길이 · 길이 차 · 허용 오차를 넘은 IO 수 · 상위 IO
    ///   a1.time      L2  비교 구간을 TimeBins 칸으로 — 칸마다 넘은 IO 수 · 상위 IO ("어디서 몰리나")
    ///   a1.events    L2  디지털 · 상태 IO 가 바뀐 시각 이전 vs 이후 짝 (k 번째 같은 바뀜끼리)
    ///   a1.oneside   L2  한쪽 로그에만 있는 IO 이름
    ///   a1.shape     L3  지표 큰 아날로그 IO 의 구간(단계 또는 같은 폭)별 최소 · 평균 · 최대
    /// </code>
    /// 안 바뀐 IO 는 <b>개수만</b> 적습니다 (a1.head) — 이름까지 주면 길이만 늘고 판단에 쓸 것이 없습니다.
    ///
    /// 시간 표기: 시각은 로그 모양 그대로(시계면 HH:MM:SS.mmm), 길이 · 차는 초 (a1.head 의 "길이 단위").
    /// 이후 로그의 시각은 <b>맞춘 뒤</b>의 시각(이전 로그 시간축)으로 적습니다 — 둘을 바로 빼 볼 수 있게.
    /// </summary>
    public static class Analysis1Digest
    {
        public static LlmPart Build(AppState state, LlmExportOptions opt, LoadProgress prog)
        {
            if (opt == null) opt = new LlmExportOptions();
            var part = new LlmPart("analysis1", AppConfig.Current.Title(1));

            CompareResult cmp = state != null ? state.Comparison : null;
            LogDataset b = state != null ? state.Before : null;
            LogDataset a = state != null ? state.After : null;
            if (cmp == null || b == null || a == null)
            {
                part.Missing = "이전 · 이후 로그를 둘 다 열고 비교한 결과가 없습니다. 분석 1 은 판단에서 빼 주세요.";
                return part;
            }

            AppSettings st = state.Settings ?? new AppSettings();
            double shift = cmp.AppliedShift;

            // 달라진 IO (지표 큰 순) — 여러 섹션이 같이 씁니다.
            var changed = new List<ChannelDiff>(cmp.Changed());
            changed.Sort(CompareSeverity);
            var changedNames = new List<string>();
            for (int i = 0; i < changed.Count; i++) changedNames.Add(changed[i].Name);

            Map groupOf = GroupMap(st.Groups);

            Report(prog, "분석 1 — 머리 · 그룹", 0.02);
            part.Add(Head(state, cmp, b, a, st));
            part.Add(Groups(cmp, st.Groups));

            // 시간대 히트맵 한 번 — "처음 달라진 시각" 과 a1.time 이 같이 씁니다.
            Report(prog, "분석 1 — 시간대 (히트맵과 같은 계산)", 0.05);
            HeatmapResult byTime = TimeHeatmap(state, cmp, b, a, changedNames, opt, prog);

            Report(prog, "분석 1 — 달라진 IO", 0.40);
            part.Add(ChangedIos(changed, b, a, groupOf, byTime, opt));

            Report(prog, "분석 1 — 단계", 0.45);
            if (opt.MaxLevel >= 2) part.Add(Steps(state, b, a, changedNames, prog));
            if (opt.MaxLevel >= 2) part.Add(TimeBins(byTime, b));

            Report(prog, "분석 1 — 디지털 · 상태 사건", 0.70);
            if (opt.MaxLevel >= 2) part.Add(Events(changed, b, a, cmp, opt, prog));
            if (opt.MaxLevel >= 2) part.Add(OneSided(cmp, opt));

            Report(prog, "분석 1 — 아날로그 모양", 0.85);
            if (opt.MaxLevel >= 3) part.Add(Shapes(state, changed, b, a, cmp, opt));

            part.Sections.RemoveAll(s => s == null);
            return part;
        }

        // ================= a1.head =================

        private static LlmSection Head(AppState state, CompareResult cmp, LogDataset b, LogDataset a, AppSettings st)
        {
            var s = new LlmSection("a1.head", "분석 1 — 요약", 1);
            s.Note = "두 로그(이전 · 이후)의 IO 값을 같은 시간축에 맞춰 견준 결과입니다. 허용 오차를 넘으면 '달라짐'.";
            s.AddFact("이전 파일", FileName(state.BeforePath));
            s.AddFact("이후 파일", FileName(state.AfterPath));
            s.AddFact("이전 로그", LlmFormat.Time(b, b.TimeStart) + " ~ " + LlmFormat.Time(b, b.TimeEnd)
                      + " · 길이 " + LlmFormat.Span(b, b.TimeEnd - b.TimeStart)
                      + " · 표본 " + LlmFormat.Int(b.SampleCount) + " · IO " + LlmFormat.Int(b.ChannelCount));
            s.AddFact("이후 로그", LlmFormat.Time(a, a.TimeStart) + " ~ " + LlmFormat.Time(a, a.TimeEnd)
                      + " · 길이 " + LlmFormat.Span(a, a.TimeEnd - a.TimeStart)
                      + " · 표본 " + LlmFormat.Int(a.SampleCount) + " · IO " + LlmFormat.Int(a.ChannelCount));
            s.AddFact("길이 단위", LlmFormat.SpanUnit(b));
            s.AddFact("시간 맞추기", (string.IsNullOrEmpty(st.AlignIo) ? "시작 시각끼리" : "기준 IO \"" + st.AlignIo + "\" 의 사건")
                      + " · 이후 로그를 " + LlmFormat.Span(b, cmp.AppliedShift) + " 밀어 맞춤");
            s.AddFact("비교 구간", LlmFormat.Time(b, cmp.OverlapStart) + " ~ " + LlmFormat.Time(b, cmp.OverlapEnd)
                      + " (이전 로그 시간축, 견준 점 " + LlmFormat.Int(cmp.ComparePoints) + " 개)");
            s.AddFact("허용 오차", LlmFormat.Num(st.RelativeTolerancePercent) + " % 또는 절대 " + LlmFormat.Num(st.AbsoluteTolerance)
                      + " 안이면 같음" + (st.Tolerances != null && st.Tolerances.Count > 0
                          ? " · IO 별 규칙 " + LlmFormat.Int(st.Tolerances.Count) + " 개" : string.Empty));
            s.AddFact("견준 IO (양쪽에 다 있음)", LlmFormat.Int(cmp.CommonCount));
            s.AddFact("달라진 IO", LlmFormat.Int(cmp.ChangedCount));
            s.AddFact("안 바뀐 IO", LlmFormat.Int(Math.Max(0, cmp.CommonCount - cmp.ChangedCount)) + " (이름은 생략)");
            s.AddFact("이전에만 있는 IO", LlmFormat.Int(cmp.OnlyBefore.Count));
            s.AddFact("이후에만 있는 IO", LlmFormat.Int(cmp.OnlyAfter.Count));
            s.AddFact("단계 (양쪽에 다 있음)", LlmFormat.Int(state.StepPairs().UsableCount));
            if (state.ComparedAt != DateTime.MinValue)
                s.AddFact("비교한 때", state.ComparedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            if (!string.IsNullOrEmpty(cmp.Warning)) s.AddFact("알림", cmp.Warning);
            return s;
        }

        // ================= a1.groups =================

        private static LlmSection Groups(CompareResult cmp, List<GroupDef> groups)
        {
            var s = new LlmSection("a1.groups", "분석 1 — 그룹별", 1);
            s.Note = "사용자가 묶어 둔 IO 그룹. 등급: 정상 = 달라진 IO 없음, 주의 = 절반 미만, 문제 = 절반 이상 (메인 화면과 같은 규칙).";
            s.Columns = new[] { "그룹", "IO 수", "달라진 IO", "등급", "달라진 IO 이름 (앞 10 개)" };

            var all = new Dictionary<string, ChannelDiff>(StringComparer.Ordinal);
            for (int i = 0; i < cmp.Items.Count; i++)
            {
                string k = LogDataset.LooseKey(cmp.Items[i].Name);
                if (!all.ContainsKey(k)) all[k] = cmp.Items[i];
            }
            var used = new HashSet<string>(StringComparer.Ordinal);

            if (groups != null)
            {
                for (int g = 0; g < groups.Count; g++)
                {
                    var names = new List<string>();
                    int n = 0, ch = 0;
                    for (int m = 0; m < groups[g].Members.Count; m++)
                    {
                        string k = LogDataset.LooseKey(groups[g].Members[m]);
                        ChannelDiff d;
                        if (!all.TryGetValue(k, out d) || !used.Add(k)) continue;
                        n++;
                        if (d.Changed) { ch++; if (names.Count < 10) names.Add(d.Name); }
                    }
                    s.AddRow(groups[g].Name, LlmFormat.Int(n), LlmFormat.Int(ch), Grade(n, ch), string.Join(", ", names.ToArray()));
                }
            }

            // 어느 그룹에도 안 든 IO — 빼면 그룹 합과 전체 수가 안 맞아 LLM 이 헷갈립니다.
            int rn = 0, rc = 0;
            var rnames = new List<string>();
            foreach (KeyValuePair<string, ChannelDiff> kv in all)
            {
                if (used.Contains(kv.Key)) continue;
                rn++;
                if (kv.Value.Changed) { rc++; if (rnames.Count < 10) rnames.Add(kv.Value.Name); }
            }
            if (rn > 0) s.AddRow("(묶지 않은 IO)", LlmFormat.Int(rn), LlmFormat.Int(rc), Grade(rn, rc), string.Join(", ", rnames.ToArray()));
            return s;
        }

        private static string Grade(int members, int changed)
        {
            if (members == 0) return "-";
            if (changed == 0) return "정상";
            return changed * 2 >= members ? "문제" : "주의";
        }

        // ================= a1.ios =================

        private static LlmSection ChangedIos(List<ChannelDiff> changed, LogDataset b, LogDataset a,
                                             Map groupOf, HeatmapResult byTime, LlmExportOptions opt)
        {
            var s = new LlmSection("a1.ios", "분석 1 — 달라진 IO", 1);
            s.Note = "허용 오차를 넘은 IO 전부, 최대차% 큰 순. 최대차 = 가장 크게 벌어진 순간의 |이후-이전|, "
                   + "최대차% = 그 오차의 이전 값 대비 %, 차이시간% = 비교 구간 중 넘은 시간 비율, 구간수 = 넘은 덩어리 수, "
                   + "처음달라짐 = 시간대 칸(a1.time) 기준이라 칸 폭만큼 대략.";
            s.Columns = new[] { "IO", "종류", "그룹", "단위", "최대차", "최대차%", "평균차", "RMS", "차이시간%", "구간수",
                                "처음달라짐", "이전범위", "이후범위" };

            Dictionary<string, double> first = FirstOver(byTime);

            for (int i = 0; i < changed.Count; i++)
            {
                ChannelDiff d = changed[i];
                Channel cb = d.BeforeIndex >= 0 && d.BeforeIndex < b.Channels.Count ? b.Channels[d.BeforeIndex] : null;
                Channel ca = d.AfterIndex >= 0 && d.AfterIndex < a.Channels.Count ? a.Channels[d.AfterIndex] : null;
                double t;
                string firstText = first.TryGetValue(d.Name, out t) ? LlmFormat.Time(b, t) : string.Empty;

                s.AddRow(d.Name,
                         cb != null ? LlmFormat.Kind(cb.Kind) : string.Empty,
                         groupOf.Get(d.Name),
                         cb != null ? cb.Unit : string.Empty,
                         LlmFormat.Num(d.MaxAbs), LlmFormat.Num(d.MaxPercent), LlmFormat.Num(d.MeanAbs), LlmFormat.Num(d.Rms),
                         LlmFormat.Pct(d.TimeRatio), LlmFormat.Int(d.Segments),
                         firstText,
                         Range(cb), Range(ca));
            }
            s.Cap(opt.MaxChangedIos);
            return s;
        }

        private static string Range(Channel c)
        {
            if (c == null) return string.Empty;
            if (c.Kind == ChannelKind.State && c.States != null) return string.Join("/", Limit(c.States, 6));
            return LlmFormat.Num(c.Min) + "~" + LlmFormat.Num(c.Max);
        }

        private static string[] Limit(string[] a, int n)
        {
            if (a.Length <= n) return a;
            var r = new string[n + 1];
            Array.Copy(a, r, n);
            r[n] = "…";
            return r;
        }

        /// <summary>IO 이름 → 처음으로 허용 오차를 넘은 시간대 칸의 시작 시각.</summary>
        private static Dictionary<string, double> FirstOver(HeatmapResult h)
        {
            var r = new Dictionary<string, double>(StringComparer.Ordinal);
            if (h == null || h.Buckets == null) return r;
            for (int i = 0; i < h.Rows.Count; i++)
            {
                HeatRow row = h.Rows[i];
                for (int k = 0; row.Cells != null && k < row.Cells.Length; k++)
                {
                    if (!HeatmapBuilder.IsOver(row, k)) continue;
                    if (!r.ContainsKey(row.Name)) r[row.Name] = h.Buckets.From(k);
                    break;
                }
            }
            return r;
        }

        // ================= 히트맵 (화면과 같은 계산) =================

        private static HeatmapOptions HeatOptions(AppState state, List<string> names)
        {
            AppSettings st = state.Settings ?? new AppSettings();
            var o = new HeatmapOptions();
            o.OnlyNames = HeatmapOptions.NameFilter(names);
            o.AbsoluteTolerance = st.AbsoluteTolerance;
            o.RelativePercent = st.RelativeTolerancePercent;
            o.Tolerances = ToleranceTable.From(st.RelativeTolerancePercent, st.AbsoluteTolerance, st.Tolerances);
            o.IncludeUnchanged = false;
            o.Shift = state.AppliedShift;
            return o;
        }

        private static HeatmapResult TimeHeatmap(AppState state, CompareResult cmp, LogDataset b, LogDataset a,
                                                 List<string> names, LlmExportOptions opt, LoadProgress prog)
        {
            if (names.Count == 0) return null;
            HeatmapOptions o = HeatOptions(state, names);
            double span = cmp.OverlapEnd - cmp.OverlapStart;
            int bins = Math.Max(1, opt.TimeBins);
            if (span > 0) o.BucketSpan = span / bins;
            return HeatmapBuilder.Build(b, a, o, prog);
        }

        // ================= a1.steps =================

        private static LlmSection Steps(AppState state, LogDataset b, LogDataset a, List<string> names, LoadProgress prog)
        {
            StepPairList pairs = state.StepPairs();
            if (pairs == null || pairs.UsableCount == 0) return null;

            var s = new LlmSection("a1.steps", "분석 1 — 단계별", 2);
            s.Note = "로그 안의 단계(공정 순서)마다. 길이 = 단계 시작~끝(초), 넘은IO = 그 단계 안에서 허용 오차를 넘은 IO 수 "
                   + "(히트맵 화면의 단계 보기와 같은 계산), 상위 = 그 단계에서 최대차% 큰 IO.";
            s.Columns = new[] { "단계", "이전시작", "이전길이", "이후길이", "길이차", "넘은IO", "상위 IO (최대차%)" };

            HeatmapResult h = null;
            if (names.Count > 0)
            {
                HeatmapOptions o = HeatOptions(state, names);
                o.ByStep = true;
                o.Steps = pairs;
                h = HeatmapBuilder.Build(b, a, o, prog);
            }

            var byLabel = new Dictionary<string, StepPair>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Count; i++) if (!byLabel.ContainsKey(pairs[i].Label)) byLabel[pairs[i].Label] = pairs[i];

            var overByLabel = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (h != null && h.ByStep && h.Buckets != null)
            {
                for (int k = 0; k < h.Buckets.Count; k++)
                {
                    string label = h.Buckets.Label(k, b);
                    overByLabel[label] = OverIn(h, k, 5);
                }
            }

            for (int i = 0; i < pairs.Count; i++)
            {
                StepPair p = pairs[i];
                string[] over;
                overByLabel.TryGetValue(p.Label, out over);
                s.AddRow(p.Label,
                         p.HasBefore ? LlmFormat.Time(b, p.BeforeStart) : "(이전에 없음)",
                         p.HasBefore ? LlmFormat.Span(b, p.BeforeLength) : string.Empty,
                         p.HasAfter ? LlmFormat.Span(a, p.AfterLength) : "(이후에 없음)",
                         p.HasBoth ? LlmFormat.Span(b, p.AfterLength - p.BeforeLength) : string.Empty,
                         over != null ? over[0] : string.Empty,
                         over != null ? over[1] : string.Empty);
            }
            if (pairs.Note.Length > 0) s.AddFact("짝 알림", pairs.Note);
            return s;
        }

        /// <summary>칸 k 에서 넘은 IO 수와 상위 top 개 "이름(12.3%)".</summary>
        private static string[] OverIn(HeatmapResult h, int k, int top)
        {
            var over = new List<HeatRow>();
            for (int i = 0; i < h.Rows.Count; i++) if (HeatmapBuilder.IsOver(h.Rows[i], k)) over.Add(h.Rows[i]);
            over.Sort((x, y) => y.Cells[k].PeakPercent.CompareTo(x.Cells[k].PeakPercent));
            var names = new List<string>();
            for (int i = 0; i < over.Count && i < top; i++)
                names.Add(over[i].Name + "(" + LlmFormat.Num(over[i].Cells[k].PeakPercent) + "%)");
            return new[] { LlmFormat.Int(over.Count), string.Join(", ", names.ToArray()) };
        }

        // ================= a1.time =================

        private static LlmSection TimeBins(HeatmapResult h, LogDataset b)
        {
            if (h == null || h.Buckets == null || h.Buckets.Count == 0) return null;
            var s = new LlmSection("a1.time", "분석 1 — 시간대별", 2);
            s.Note = "비교 구간을 같은 폭으로 자른 칸마다 허용 오차를 넘은 IO 수. 차이가 어디서 몰리는지 봅니다. "
                   + "칸 폭 " + LlmFormat.Span(b, h.BucketSpan) + " (길이 단위). 넘은 IO 가 0 인 칸도 적습니다 (빠진 것과 구별).";
            s.Columns = new[] { "칸", "시작", "끝", "넘은IO", "상위 IO (최대차%)" };
            for (int k = 0; k < h.Buckets.Count; k++)
            {
                string[] over = OverIn(h, k, 3);
                s.AddRow(LlmFormat.Int(k + 1), LlmFormat.Time(b, h.Buckets.From(k)), LlmFormat.Time(b, h.Buckets.To(k)),
                         over[0], over[1]);
            }
            return s;
        }

        // ================= a1.events =================

        /// <summary>디지털 · 상태 값이 바뀐 한 순간.</summary>
        private struct Edge
        {
            public double Time;      // 이전 로그 시간축 (이후 쪽은 밀어 맞춘 뒤)
            public string From;
            public string To;
        }

        private static LlmSection Events(List<ChannelDiff> changed, LogDataset b, LogDataset a, CompareResult cmp,
                                         LlmExportOptions opt, LoadProgress prog)
        {
            var s = new LlmSection("a1.events", "분석 1 — 디지털 · 상태 사건", 2);
            s.Note = "달라진 디지털 · 상태 IO 의 값이 바뀐 순간을 이전 vs 이후로 짝지음 (같은 '바뀜' 의 k 번째끼리). "
                   + "시각은 이전 로그 시간축 (이후는 맞춘 뒤), 차 = 이후-이전 (초). 횟수가 다르면 짝 없는 줄이 생깁니다.";
            s.Columns = new[] { "IO", "차례", "바뀜", "이전시각", "이후시각", "차", "비고" };

            int ios = 0, total = 0;
            for (int i = 0; i < changed.Count && ios < opt.MaxEventIos; i++)
            {
                ChannelDiff d = changed[i];
                if (d.BeforeIndex < 0 || d.AfterIndex < 0) continue;
                Channel cb = b.Channels[d.BeforeIndex];
                Channel ca = a.Channels[d.AfterIndex];
                if (cb.Kind == ChannelKind.Analog || ca.Kind == ChannelKind.Analog) continue;
                ios++;
                if (prog != null) prog.ThrowIfCancelled();

                int nb, na;
                List<Edge> eb = Edges(b, cb, cmp.OverlapStart, cmp.OverlapEnd, 0.0, opt.MaxEventsPerIo * 4, out nb);
                List<Edge> ea = Edges(a, ca, cmp.OverlapStart, cmp.OverlapEnd, cmp.AppliedShift, opt.MaxEventsPerIo * 4, out na);

                // 같은 "바뀜(From→To)" 의 k 번째끼리 짝 — 한쪽에 사건이 하나 더 있어도 뒤가 다 밀리지 않게.
                var used = new bool[ea.Count];
                int written = 0;
                for (int x = 0; x < eb.Count && written < opt.MaxEventsPerIo; x++)
                {
                    int y = -1;
                    for (int j = 0; j < ea.Count; j++)
                        if (!used[j] && ea[j].From == eb[x].From && ea[j].To == eb[x].To) { y = j; break; }
                    if (y >= 0) used[y] = true;
                    s.AddRow(d.Name, LlmFormat.Int(written + 1), eb[x].From + "→" + eb[x].To,
                             LlmFormat.Time(b, eb[x].Time),
                             y >= 0 ? LlmFormat.Time(b, ea[y].Time) : string.Empty,
                             y >= 0 ? LlmFormat.Span(b, ea[y].Time - eb[x].Time) : string.Empty,
                             y >= 0 ? string.Empty : "이후에 없음");
                    written++;
                }
                for (int j = 0; j < ea.Count && written < opt.MaxEventsPerIo; j++)
                {
                    if (used[j]) continue;
                    s.AddRow(d.Name, LlmFormat.Int(written + 1), ea[j].From + "→" + ea[j].To,
                             string.Empty, LlmFormat.Time(b, ea[j].Time), string.Empty, "이전에 없음");
                    written++;
                }
                if (nb != na || nb > opt.MaxEventsPerIo)
                    s.AddRow(d.Name, "합계", string.Empty, "이전 " + LlmFormat.Int(nb) + " 번", "이후 " + LlmFormat.Int(na) + " 번",
                             string.Empty, nb > opt.MaxEventsPerIo || na > opt.MaxEventsPerIo
                                 ? "앞 " + LlmFormat.Int(opt.MaxEventsPerIo) + " 개만 적음" : "횟수 다름");
                total += Math.Max(nb, na);
            }
            if (ios == 0) return null;
            s.AddFact("사건을 적은 IO", LlmFormat.Int(ios));
            s.AddFact("사건 수 (많은 쪽 합)", LlmFormat.Int(total));
            return s;
        }

        /// <summary>
        /// 채널에서 값이 바뀐 순간을 모읍니다. 비교 구간 [t0, t1] (이전 시간축) 안만, 앞에서 keep 개까지.
        /// count 에는 구간 안 전체 횟수 (keep 를 넘어도 셈). 빈 값(NaN) 은 건너뜁니다.
        /// </summary>
        private static List<Edge> Edges(LogDataset ds, Channel c, double t0, double t1, double shift, int keep, out int count)
        {
            var list = new List<Edge>();
            count = 0;
            float[] v = c.Values;
            if (v == null || v.Length < 2) return list;

            int prev = -1;
            for (int i = 0; i < v.Length && i < ds.Times.Length; i++)
            {
                if (float.IsNaN(v[i])) continue;
                if (prev >= 0 && v[i] != v[prev])
                {
                    double t = ds.Times[i] + shift;
                    if (t >= t0 && t <= t1)
                    {
                        count++;
                        if (list.Count < keep)
                        {
                            var e = new Edge();
                            e.Time = t;
                            e.From = c.TextAt(prev);
                            e.To = c.TextAt(i);
                            list.Add(e);
                        }
                    }
                }
                prev = i;
            }
            return list;
        }

        // ================= a1.oneside =================

        private static LlmSection OneSided(CompareResult cmp, LlmExportOptions opt)
        {
            if (cmp.OnlyBefore.Count + cmp.OnlyAfter.Count == 0) return null;
            var s = new LlmSection("a1.oneside", "분석 1 — 한쪽에만 있는 IO", 2);
            s.Note = "이름이 한쪽 로그에만 있어 견주지 못한 IO. 추가 · 삭제 · 이름 변경일 수 있습니다.";
            s.Columns = new[] { "쪽", "IO" };
            for (int i = 0; i < cmp.OnlyBefore.Count; i++) s.AddRow("이전에만", cmp.OnlyBefore[i].Name);
            for (int i = 0; i < cmp.OnlyAfter.Count; i++) s.AddRow("이후에만", cmp.OnlyAfter[i].Name);
            s.Cap(opt.MaxOneSidedIos);
            return s;
        }

        // ================= a1.shape =================

        private static LlmSection Shapes(AppState state, List<ChannelDiff> changed, LogDataset b, LogDataset a,
                                         CompareResult cmp, LlmExportOptions opt)
        {
            var s = new LlmSection("a1.shape", "분석 1 — 아날로그 모양 (구간별)", 3);
            s.Columns = new[] { "IO", "구간", "이전최소", "이전평균", "이전최대", "이후최소", "이후평균", "이후최대", "평균차" };

            // 구간: 단계가 있으면 단계, 없으면 비교 구간을 같은 폭으로.
            var segs = new List<Seg>();
            StepPairList pairs = state.StepPairs();
            if (pairs != null && pairs.UsableCount > 0)
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    StepPair p = pairs[i];
                    if (!p.HasBoth) continue;
                    LogStep sb = b.Steps[p.BeforeIndex], sa = a.Steps[p.AfterIndex];
                    segs.Add(new Seg(p.Label, sb.First, sb.Last, sa.First, sa.Last));
                }
                s.Note = "최대차% 큰 아날로그 IO 상위 " + opt.ShapeIos + " 개를 단계마다 최소 · 평균 · 최대로. 곡선을 다 주는 대신 모양만.";
            }
            else
            {
                int n = Math.Max(1, opt.ShapeSegments);
                double t0 = cmp.OverlapStart, t1 = cmp.OverlapEnd, w = (t1 - t0) / n;
                for (int i = 0; i < n; i++)
                {
                    double x0 = t0 + w * i, x1 = t0 + w * (i + 1);
                    segs.Add(new Seg(LlmFormat.Time(b, x0) + "~",
                                     b.IndexAt(x0), b.IndexAt(x1),
                                     a.IndexAt(x0 - cmp.AppliedShift), a.IndexAt(x1 - cmp.AppliedShift)));
                }
                s.Note = "최대차% 큰 아날로그 IO 상위 " + opt.ShapeIos + " 개를 비교 구간 " + n
                       + " 등분마다 최소 · 평균 · 최대로 (단계가 없어서). 곡선을 다 주는 대신 모양만.";
            }

            int ios = 0;
            for (int i = 0; i < changed.Count && ios < opt.ShapeIos; i++)
            {
                ChannelDiff d = changed[i];
                if (d.BeforeIndex < 0 || d.AfterIndex < 0) continue;
                Channel cb = b.Channels[d.BeforeIndex], ca = a.Channels[d.AfterIndex];
                if (cb.Kind != ChannelKind.Analog || ca.Kind != ChannelKind.Analog) continue;
                ios++;
                for (int k = 0; k < segs.Count; k++)
                {
                    Stat sb = Stat.Of(cb.Values, segs[k].B0, segs[k].B1);
                    Stat sa = Stat.Of(ca.Values, segs[k].A0, segs[k].A1);
                    s.AddRow(d.Name, segs[k].Label,
                             LlmFormat.Num(sb.Min), LlmFormat.Num(sb.Mean), LlmFormat.Num(sb.Max),
                             LlmFormat.Num(sa.Min), LlmFormat.Num(sa.Mean), LlmFormat.Num(sa.Max),
                             sb.Count > 0 && sa.Count > 0 ? LlmFormat.Num(sa.Mean - sb.Mean) : string.Empty);
                }
            }
            return ios == 0 ? null : s;
        }

        private struct Seg
        {
            public string Label;
            public int B0, B1, A0, A1;   // 표본 번호 (끝 포함)
            public Seg(string label, int b0, int b1, int a0, int a1) { Label = label; B0 = b0; B1 = b1; A0 = a0; A1 = a1; }
        }

        private struct Stat
        {
            public double Min, Max, Mean;
            public int Count;

            public static Stat Of(float[] v, int from, int to)
            {
                var s = new Stat();
                s.Min = double.NaN; s.Max = double.NaN; s.Mean = double.NaN;
                if (v == null || from < 0 || to < from) return s;
                if (to >= v.Length) to = v.Length - 1;
                double sum = 0, mn = double.MaxValue, mx = double.MinValue;
                for (int i = from; i <= to; i++)
                {
                    float x = v[i];
                    if (float.IsNaN(x)) continue;
                    sum += x;
                    if (x < mn) mn = x;
                    if (x > mx) mx = x;
                    s.Count++;
                }
                if (s.Count > 0) { s.Min = mn; s.Max = mx; s.Mean = sum / s.Count; }
                return s;
            }
        }

        // ================= 작은 도움 =================

        /// <summary>최대차% 큰 순, 같으면 최대차 큰 순, 같으면 이름 순 (늘 같은 차례가 나오게).</summary>
        private static int CompareSeverity(ChannelDiff x, ChannelDiff y)
        {
            int c = SafeDesc(x.MaxPercent, y.MaxPercent);
            if (c != 0) return c;
            c = SafeDesc(x.MaxAbs, y.MaxAbs);
            if (c != 0) return c;
            return string.CompareOrdinal(x.Name, y.Name);
        }

        private static int SafeDesc(double x, double y)
        {
            bool nx = double.IsNaN(x), ny = double.IsNaN(y);
            if (nx && ny) return 0;
            if (nx) return 1;
            if (ny) return -1;
            return y.CompareTo(x);
        }

        /// <summary>
        /// 경로의 이름만 (폴더는 안 보냄 — 사내 경로가 LLM 서버에 남지 않게). Path.GetFileName 대신
        /// DbPathText.Name 을 씁니다 — \ 와 / 를 다 구분 기호로 봐서 어디서 돌려도 같습니다.
        /// </summary>
        private static string FileName(string path)
        {
            return LogScope.Analysis2.DbPathText.Name(path);
        }

        private static void Report(LoadProgress p, string stage, double f)
        {
            if (p == null) return;
            p.ThrowIfCancelled();
            p.Report(stage, f);
        }

        /// <summary>IO 이름(느슨하게) → 그룹 이름.</summary>
        private sealed class Map
        {
            public readonly Dictionary<string, string> D = new Dictionary<string, string>(StringComparer.Ordinal);
            public string Get(string io)
            {
                string g;
                return D.TryGetValue(LogDataset.LooseKey(io), out g) ? g : string.Empty;
            }
        }

        private static Map GroupMap(List<GroupDef> groups)
        {
            var m = new Map();
            if (groups == null) return m;
            for (int g = 0; g < groups.Count; g++)
                for (int i = 0; i < groups[g].Members.Count; i++)
                {
                    string k = LogDataset.LooseKey(groups[g].Members[i]);
                    if (!m.D.ContainsKey(k)) m.D[k] = groups[g].Name;
                }
            return m;
        }
    }
}
