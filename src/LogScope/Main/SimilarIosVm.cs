using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Analysis1.Judge;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// "파형은 같고 조금 어긋난 IO" 창 (SimilarIosWindow, 1.74) 의 값. WPF 를 쓰지 않습니다 (시험이 닿음).
    ///
    /// 판정이 오차로 잡았지만, 그 IO · 단계를 <b>시간만 조금 밀거나 높이만 조금 옮기면</b> 오차가 (거의) 없어지는 것들을
    /// IO 하나에 한 줄로 묶어 사람 말로 보여 줍니다 ("이후가 1.2초 늦음", "이후 값이 0.3 높음"). 판정은 그대로 두고
    /// (사용자가 묻고 정함), 고른 줄을 [이번만 허용] · [앞으로도 허용] 으로 한꺼번에 허용합니다.
    /// 허용하는 것은 <b>그 제안이 붙은 오차 구간만</b>입니다 — 같은 IO 의 다른 오차 (튐 등) 는 그대로 남습니다.
    /// </summary>
    public sealed class SimilarIosVm : ObservableObject
    {
        private readonly MainVm _main;
        private readonly Func<JudgeResult> _judge;

        public SimilarIosVm(MainVm main, Func<JudgeResult> judge)
        {
            _main = main;
            _judge = judge;
            Rows = new List<SimilarIoRowVm>();
            Refresh();
        }

        public List<SimilarIoRowVm> Rows { get; private set; }
        public bool NoRows { get { return Rows.Count == 0; } }

        /// <summary>"IO 7 개 — 시간 밀림 4 · 높이 차이 2 · 둘 다 1".</summary>
        public string Summary { get; private set; }

        public void Refresh()
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (SimilarIoRowVm r in Rows) if (!r.Selected) keep.Add(r.Io);   // 고른 것 그대로
            Rows = Collect(_judge != null ? _judge() : null);
            foreach (SimilarIoRowVm r in Rows) if (keep.Contains(r.Io)) r.Selected = false;
            Summary = SummaryOf(Rows);
            Raise("Rows"); Raise("NoRows"); Raise("Summary");
        }

        /// <summary>모두 고르기 · 모두 풀기.</summary>
        public void SelectAll(bool on)
        {
            foreach (SimilarIoRowVm r in Rows) r.Selected = on;
        }

        /// <summary>고른 줄의 (제안이 붙은) 오차를 한꺼번에 허용. 허용한 오차 수.</summary>
        public int AllowSelected(AllowChoice choice)
        {
            var items = new List<JudgeItem>();
            foreach (SimilarIoRowVm r in Rows) if (r.Selected) items.AddRange(r.Items);
            int n = _main != null ? _main.AllowMany(items, choice) : 0;
            Refresh();
            return n;
        }

        // ---------------- 묶기 (메인 칸 알림 줄도 씀) ----------------

        /// <summary>허용 안 된 값 오차 중 제안이 붙은 것을 IO 마다 한 줄로.</summary>
        public static List<SimilarIoRowVm> Collect(JudgeResult r)
        {
            var rows = new List<SimilarIoRowVm>();
            if (r == null) return rows;
            var byIo = new Dictionary<string, SimilarIoRowVm>(StringComparer.Ordinal);
            var otherOpen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JudgeItem it in r.Items)
            {
                if (it.Kind != JudgeKind.Value || it.IsAllowed) continue;
                if (it.HintKind == HintKind.None)
                {
                    int c; otherOpen.TryGetValue(it.Io, out c); otherOpen[it.Io] = c + 1;
                    continue;
                }
                SimilarIoRowVm row;
                if (!byIo.TryGetValue(it.Io, out row))
                {
                    row = new SimilarIoRowVm(it.Io);
                    byIo[it.Io] = row;
                    rows.Add(row);
                }
                row.Items.Add(it);
            }
            foreach (SimilarIoRowVm row in rows)
            {
                int other; otherOpen.TryGetValue(row.Io, out other);
                row.Describe(r, other);
            }
            return rows;
        }

        public static string SummaryOf(List<SimilarIoRowVm> rows)
        {
            if (rows == null || rows.Count == 0) return "조금 어긋난 IO 가 없습니다.";
            int t = 0, v = 0, b = 0;
            foreach (SimilarIoRowVm row in rows)
            {
                if (row.Kind == HintKind.Time) t++;
                else if (row.Kind == HintKind.Offset) v++;
                else b++;
            }
            var parts = new List<string>();
            if (t > 0) parts.Add("시간 밀림 " + t);
            if (v > 0) parts.Add("높이 차이 " + v);
            if (b > 0) parts.Add("둘 다 " + b);
            return "IO " + rows.Count.ToString("N0") + " 개 — " + string.Join(" · ", parts.ToArray());
        }
    }

    /// <summary>"조금 어긋난 IO" 한 줄.</summary>
    public sealed class SimilarIoRowVm : ObservableObject
    {
        public SimilarIoRowVm(string io)
        {
            Io = io ?? string.Empty;
            Items = new List<JudgeItem>();
            Text = string.Empty;
            Detail = string.Empty;
        }

        public string Io { get; private set; }

        /// <summary>이 줄로 허용할 오차 (제안이 붙은 것).</summary>
        public List<JudgeItem> Items { get; private set; }

        /// <summary>"이후가 1.2초 늦음" · "이후 값이 0.3 높음" · 둘 다면 " · " 로.</summary>
        public string Text { get; private set; }

        /// <summary>"단계 2, 3 · 오차 구간 4 개" (+ 그 밖의 오차가 있으면 그 말).</summary>
        public string Detail { get; private set; }

        /// <summary>줄 전체의 종류 — 단계마다 다르면 Both.</summary>
        public HintKind Kind { get; private set; }

        /// <summary>그래프로 볼 때 — 첫 오차.</summary>
        public JudgeItem First { get { return Items.Count > 0 ? Items[0] : null; } }

        private bool _selected = true;
        public bool Selected
        {
            get { return _selected; }
            set { Set(ref _selected, value); }
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal void Describe(JudgeResult r, int otherOpen)
        {
            var shifts = new List<double>();
            var offsets = new List<double>();
            var steps = new List<string>();
            bool time = false, off = false;
            foreach (JudgeItem it in Items)
            {
                if ((it.HintKind == HintKind.Time || it.HintKind == HintKind.Both) && !double.IsNaN(it.HintShift)) { shifts.Add(it.HintShift); time = true; }
                if ((it.HintKind == HintKind.Offset || it.HintKind == HintKind.Both) && !double.IsNaN(it.HintOffset)) { offsets.Add(it.HintOffset); off = true; }
                if (it.Step.Length > 0 && !steps.Contains(it.Step)) steps.Add(it.Step);
            }
            Kind = time && off ? HintKind.Both : time ? HintKind.Time : HintKind.Offset;

            var parts = new List<string>();
            if (time)
            {
                // HintShift = 이후 로그를 이만큼 늦추면 맞음 → 음수면 이후가 늦은 것
                double med = Median(shifts), lo = Min(shifts), hi = Max(shifts);
                string word = med < 0 ? "늦음" : "빠름";
                bool spread = hi - lo > Math.Max(Math.Abs(med) * 0.25, 1e-12) && Math.Sign(lo) == Math.Sign(hi);
                parts.Add("이후가 " + (spread
                    ? "단계마다 " + r.Duration(Math.Min(Math.Abs(lo), Math.Abs(hi))) + " ~ " + r.Duration(Math.Max(Math.Abs(lo), Math.Abs(hi)))
                    : r.Duration(Math.Abs(med))) + " " + word);
            }
            if (off)
            {
                double med = Median(offsets);
                parts.Add("이후 값이 " + NumberText.Plain(Math.Abs(med)) + (med > 0 ? " 높음" : " 낮음"));
            }
            Text = string.Join(" · ", parts.ToArray());

            var d = new List<string>();
            if (steps.Count > 0) d.Add("단계 " + string.Join(", ", steps.ToArray()));
            d.Add("오차 구간 " + Items.Count.ToString("N0", Inv) + " 개");
            if (otherOpen > 0) d.Add("그 밖의 오차 " + otherOpen.ToString("N0", Inv) + " 개는 그대로 (IO 줄을 눌러 확인)");
            Detail = string.Join(" · ", d.ToArray());
        }

        private static double Median(List<double> v)
        {
            var c = new List<double>(v);
            c.Sort();
            int n = c.Count;
            return n == 0 ? 0 : n % 2 == 1 ? c[n / 2] : (c[n / 2 - 1] + c[n / 2]) / 2.0;
        }

        private static double Min(List<double> v) { double m = double.MaxValue; foreach (double x in v) if (x < m) m = x; return m; }
        private static double Max(List<double> v) { double m = double.MinValue; foreach (double x in v) if (x > m) m = x; return m; }
    }
}
