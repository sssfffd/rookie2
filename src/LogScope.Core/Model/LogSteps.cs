using System;
using System.Collections.Generic;

namespace LogScope.Core.Model
{
    /// <summary>
    /// 로그 안의 <b>단계</b> 하나. "준비 → 가열 → 유지 → 냉각" 처럼 공정이
    /// 밟아 가는 토막입니다.
    ///
    /// <b>시간이 아니라 표본 번호로 적습니다.</b> 가로축은 바꿔 끼울 수
    /// 있어서(로그의 시간 → 다른 IO), 시각을 적어 두면 축을 바꾼 날
    /// 단계가 엉뚱한 자리를 가리킵니다. 표본 번호는 축을 바꿔도 그대로이고,
    /// 그 번호의 시각은 <see cref="StepTable.StartTime"/> 가 그때그때 읽습니다.
    /// </summary>
    public struct LogStep
    {
        /// <summary>단계 이름. 숫자뿐인 신호면 "3" 처럼 값이 그대로 들어옵니다.</summary>
        public string Name;

        /// <summary>첫 표본 번호 (이 표본 포함).</summary>
        public int First;

        /// <summary>끝 표본 번호 (이 표본 포함).</summary>
        public int Last;

        public LogStep(string name, int first, int last)
        {
            Name = name ?? string.Empty;
            First = first;
            Last = last;
        }

        public int SampleCount { get { return Last - First + 1; } }
    }

    /// <summary>
    /// 로그 하나의 단계 목록. 비어 있을 수 있습니다 (단계가 없는 로그).
    ///
    /// 만드는 길이 셋입니다 — 어느 쪽으로 넣어도 아래 규칙을 똑같이 거칩니다.
    ///   <see cref="FromChannel"/>  : 단계를 담은 IO 의 값이 바뀌면 새 단계
    ///   <see cref="FromLabels"/>   : 표본마다의 이름 글자 (직접 파싱한 경우)
    ///   <see cref="FromRanges"/>   : 구간을 직접 적어 넣는 경우
    ///
    /// 규칙은 한 곳(<see cref="Check"/>)에 있습니다. 넣는 길마다 따로 재면
    /// 어느 길로 넣었는지에 따라 단계가 달라지는데, 그건 화면만 봐서는
    /// 알 수가 없습니다.
    /// </summary>
    public sealed class StepTable
    {
        /// <summary>단계가 없는 로그. null 대신 이것을 씁니다.</summary>
        public static readonly StepTable None = new StepTable(new List<LogStep>(), string.Empty, string.Empty);

        private readonly List<LogStep> _steps;

        /// <summary>어디서 온 단계인지. 화면에 그대로 적습니다 ("단계" IO 에서 …).</summary>
        public readonly string Source;

        /// <summary>버린 것·자른 것. 비어 있으면 그대로 다 쓴 것입니다.</summary>
        public readonly string Note;

        private StepTable(List<LogStep> steps, string source, string note)
        {
            _steps = steps ?? new List<LogStep>();
            Source = source ?? string.Empty;
            Note = note ?? string.Empty;
        }

        /// <summary>
        /// 단계 수 상한.
        ///
        /// 잘못된 IO 를 단계로 집으면 (예를 들어 매 표본 흔들리는 아날로그)
        /// 단계가 표본 수만큼 생깁니다. 그대로 두면 히트맵 칸이 수십만 개가
        /// 되어 화면이 멈춥니다. 자르고 <b>자랐다고 말해 줍니다</b> — 조용히
        /// 자르면 "왜 뒷부분이 없지" 가 됩니다.
        /// </summary>
        public const int MaxSteps = 2000;

        public int Count { get { return _steps.Count; } }
        public bool IsEmpty { get { return _steps.Count == 0; } }
        public LogStep this[int i] { get { return _steps[i]; } }
        public IList<LogStep> All { get { return _steps; } }

        /// <summary>그 단계가 시작하는 시각. 가로축을 바꿔 끼우면 같이 따라갑니다.</summary>
        public double StartTime(LogDataset ds, int i)
        {
            return TimeOf(ds, i, true);
        }

        /// <summary>그 단계가 끝나는 시각 (마지막 표본의 시각).</summary>
        public double EndTime(LogDataset ds, int i)
        {
            return TimeOf(ds, i, false);
        }

        private double TimeOf(LogDataset ds, int i, bool first)
        {
            if (ds == null || i < 0 || i >= _steps.Count) return double.NaN;
            int n = ds.SampleCount;
            if (n == 0) return double.NaN;
            int k = first ? _steps[i].First : _steps[i].Last;
            if (k < 0) k = 0;
            if (k >= n) k = n - 1;
            return ds.Times[k];
        }

        /// <summary>그 표본이 든 단계. 어느 단계에도 안 들면 -1.</summary>
        public int IndexOfSample(int sample)
        {
            int lo = 0, hi = _steps.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (sample < _steps[mid].First) hi = mid - 1;
                else if (sample > _steps[mid].Last) lo = mid + 1;
                else return mid;
            }
            return -1;
        }

        /// <summary>
        /// 이름이 같은 단계 중 <paramref name="skip"/> 개를 건너뛴 다음 것.
        /// 같은 이름이 여러 번 나오는 공정(가열 → 냉각 → 가열)을 짝지을 때
        /// 씁니다. 없으면 -1.
        /// </summary>
        public int IndexOfName(string name, int skip)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < _steps.Count; i++)
            {
                if (!Same(_steps[i].Name, name)) continue;
                if (skip-- > 0) continue;
                return i;
            }
            return -1;
        }

        /// <summary>단계 이름 견주기. IO 이름과 같은 느슨한 잣대입니다.</summary>
        public static bool Same(string a, string b)
        {
            return string.Equals(LogDataset.LooseKey(a), LogDataset.LooseKey(b), StringComparison.Ordinal);
        }

        /// <summary>
        /// 목록에 적는 글. <b>번호를 앞에 붙입니다</b> — 같은 이름이 두 번
        /// 나오는 공정에서 이름만 적으면 어느 쪽인지 고를 수가 없습니다.
        /// </summary>
        public string Label(int i)
        {
            if (i < 0 || i >= _steps.Count) return string.Empty;
            string name = _steps[i].Name;
            string head = (i + 1).ToString();
            return name.Length > 0 ? head + ". " + name : head;
        }

        // ================= 짓는 길 =================

        /// <summary>
        /// 구간을 직접 적어 넣습니다. 직접 파싱한 결과를 넣는 자리입니다.
        /// 규칙은 <see cref="Check"/> 가 봅니다.
        /// </summary>
        public static StepTable FromRanges(LogDataset ds, IEnumerable<LogStep> steps, string source)
        {
            var list = new List<LogStep>();
            if (steps != null) list.AddRange(steps);
            return Check(ds, list, source);
        }

        /// <summary>
        /// 표본마다 적힌 이름에서 단계를 뽑습니다. 이름이 바뀌는 자리가
        /// 단계가 바뀌는 자리입니다.
        ///
        /// 빈 글자(또는 null)인 표본은 <b>어느 단계도 아닙니다</b> — 기록이
        /// 시작되기 전이나 공정이 멈춘 구간입니다. 직전 단계로 끌고 가면
        /// 그 구간의 차이가 엉뚱한 단계에 적힙니다.
        /// </summary>
        public static StepTable FromLabels(LogDataset ds, IList<string> perSample, string source)
        {
            var list = new List<LogStep>();
            if (perSample != null)
            {
                int n = perSample.Count;
                int start = -1;
                string cur = null;

                for (int i = 0; i < n; i++)
                {
                    string s = perSample[i];
                    if (s != null) s = s.Trim();
                    bool blank = string.IsNullOrEmpty(s);

                    if (blank)
                    {
                        if (start >= 0) { list.Add(new LogStep(cur, start, i - 1)); start = -1; cur = null; }
                        continue;
                    }
                    if (start < 0) { start = i; cur = s; continue; }
                    if (!string.Equals(s, cur, StringComparison.Ordinal))
                    {
                        list.Add(new LogStep(cur, start, i - 1));
                        start = i; cur = s;
                    }
                }
                if (start >= 0) list.Add(new LogStep(cur, start, n - 1));
            }
            return Check(ds, list, source);
        }

        /// <summary>
        /// 단계를 담은 IO 의 값이 바뀌는 자리를 단계 경계로 봅니다.
        ///
        /// 글자 채널(상태)이면 그 글자가 단계 이름이고, 숫자면 값이 그대로
        /// 이름이 됩니다 ("3"). 값이 빈 자리(NaN)는 단계 밖입니다.
        ///
        /// 못 쓰면 빈 표를 돌려주고 problem 에 이유를 담습니다 — <b>짐작해서
        /// 아무 단계나 만들지 않습니다.</b> 단계가 틀리면 그 뒤의 히트맵과
        /// 그래프가 전부 틀리는데, 그림만 봐서는 맞는지 알 수가 없습니다.
        /// </summary>
        public static StepTable FromChannel(LogDataset ds, string ioName, out string problem)
        {
            problem = string.Empty;
            if (ds == null || string.IsNullOrEmpty(ioName)) return None;

            int ch = ds.FindChannel(ioName);
            if (ch < 0)
            {
                problem = "단계 IO \"" + ioName + "\" 를 이 로그에서 찾지 못했습니다.";
                return None;
            }

            Channel c = ds.Channels[ch];
            int n = ds.SampleCount;
            if (n < 2 || c.Values == null || c.Values.Length < n)
            {
                problem = "단계 IO \"" + ioName + "\" 에 읽을 값이 모자랍니다.";
                return None;
            }

            // 글자로 바꾸는 규칙은 Channel.TextAt 한 곳입니다 (값 없음 =
            // 빈 글자 = 단계 밖).
            var labels = new string[n];
            for (int i = 0; i < n; i++) labels[i] = c.TextAt(i);

            StepTable t = FromLabels(ds, labels, "\"" + c.Name + "\" IO 의 값이 바뀌는 자리");
            if (t.IsEmpty) problem = "단계 IO \"" + ioName + "\" 에서 단계를 하나도 못 찾았습니다.";
            return t;
        }

        // ================= 규칙 한 곳 =================

        /// <summary>
        /// 넣은 구간을 다듬습니다. 어느 길로 넣어도 여기를 거칩니다.
        ///
        ///   1. 표본 범위 밖은 잘라 들입니다 (로그보다 긴 단계표가 올 수 있습니다).
        ///   2. 거꾸로거나 빈 구간은 버립니다.
        ///   3. 차례대로 세웁니다 (시작 번호 순).
        ///   4. 겹치면 뒤 단계의 시작을 앞 단계 다음으로 밉니다. 한 표본이
        ///      두 단계에 들면 그 표본의 차이가 두 번 세어집니다.
        ///   5. 너무 많으면 자르고 말해 줍니다.
        /// </summary>
        private static StepTable Check(LogDataset ds, List<LogStep> raw, string source)
        {
            int n = ds != null ? ds.SampleCount : 0;
            if (n == 0 || raw == null || raw.Count == 0) return None;

            var kept = new List<LogStep>(raw.Count);
            int dropped = 0;

            for (int i = 0; i < raw.Count; i++)
            {
                LogStep s = raw[i];
                if (s.First < 0) s.First = 0;
                if (s.Last > n - 1) s.Last = n - 1;
                if (s.Last < s.First) { dropped++; continue; }
                if (s.Name == null) s.Name = string.Empty;
                kept.Add(s);
            }

            kept.Sort(delegate (LogStep a, LogStep b) { return a.First.CompareTo(b.First); });

            var outp = new List<LogStep>(kept.Count);
            int overlapped = 0;
            int end = -1;
            for (int i = 0; i < kept.Count; i++)
            {
                LogStep s = kept[i];
                if (s.First <= end)
                {
                    overlapped++;
                    s.First = end + 1;
                    if (s.Last < s.First) { dropped++; continue; }
                }
                outp.Add(s);
                end = s.Last;
            }

            string note = string.Empty;
            if (outp.Count > MaxSteps)
            {
                note = "단계가 " + outp.Count.ToString("N0") + " 개나 되어 앞 "
                     + MaxSteps.ToString("N0") + " 개만 씁니다. 단계가 아닌 IO 를 "
                     + "단계로 집었을 수 있습니다.";
                outp.RemoveRange(MaxSteps, outp.Count - MaxSteps);
            }
            if (overlapped > 0)
            {
                note = Join(note, "겹치는 단계 " + overlapped + " 개의 시작을 앞 단계 다음으로 밀었습니다.");
            }
            if (dropped > 0)
            {
                note = Join(note, "쓸 수 없는 구간 " + dropped + " 개는 버렸습니다.");
            }

            return outp.Count == 0 ? None : new StepTable(outp, source, note);
        }

        private static string Join(string a, string b)
        {
            if (a.Length == 0) return b;
            return a + " " + b;
        }
    }
}
