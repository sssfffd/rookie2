using System;
using System.Collections.Generic;
using LogScope.Core.Model;
using LogScope.Logs.Align;

namespace LogScope.Logs.Compare
{
    /// <summary>
    /// 히트맵의 <b>가로 칸</b>들. 칸 하나가 세로줄 하나입니다.
    ///
    /// 자르는 길이 둘입니다.
    ///   <see cref="Uniform"/> : 일정한 시간 폭 (기본 1 분)
    ///   <see cref="Steps"/>   : <b>단계 하나가 칸 하나</b>
    ///
    /// 왜 따로 뺐나. 전에는 칸 번호를 <c>(t - t0) / 폭</c> 으로 바로
    /// 셈했습니다. 그 셈은 "칸은 다 같은 폭이고 빈틈 없이 붙어 있다" 를
    /// 전제하는데, 단계는 폭이 다 다르고 <b>단계 사이에 빈틈이 있습니다</b>
    /// (공정이 멈춘 구간). 그 전제를 코드 여러 곳에 흩어 두면 단계로
    /// 바꾸는 날 한 곳만 고쳐도 조용히 틀립니다 — 칸이 밀려도 그림은
    /// 멀쩡해 보입니다.
    ///
    /// 그래서 칸의 경계는 여기 한 곳에만 있고, 밖에서는 <see cref="Find"/>
    /// 로 묻습니다.
    /// </summary>
    public sealed class HeatBuckets
    {
        private readonly double[] _from;
        private readonly double[] _to;
        private readonly string[] _labels;   // null 이면 시각으로 적습니다

        /// <summary>
        /// 칸마다의 밀기 값. null 이면 칸이 다 같은 값을 씁니다.
        ///
        /// <b>단계로 자르면 칸마다 다릅니다.</b> 단계마다 두 로그가 어긋난
        /// 만큼이 다르기 때문입니다 — 공정이 중간에 조금 늘어지면 뒤 단계는
        /// 앞 단계보다 더 밀립니다. 칸을 단계로 잘라 놓고 밀기 값은 하나만
        /// 쓰면, <b>그 하나로 맞은 단계만 제대로 견주고 나머지 칸은 어긋난
        /// 채 견줍니다</b> — 차이가 없는데 있다고 나오거나, 있는데 묻힙니다.
        /// </summary>
        private readonly double[] _shifts;

        /// <summary>단계로 자른 것인지. 화면의 가로축 글자가 달라집니다.</summary>
        public readonly bool ByStep;

        /// <summary>일정 폭으로 잘랐을 때의 그 폭. 단계로 자르면 NaN.</summary>
        public readonly double Span;

        private HeatBuckets(double[] from, double[] to, string[] labels, double[] shifts,
                            bool byStep, double span)
        {
            _from = from;
            _to = to;
            _labels = labels;
            _shifts = shifts;
            ByStep = byStep;
            Span = span;
        }

        /// <summary>칸마다 밀기 값이 따로인지.</summary>
        public bool HasShifts { get { return _shifts != null; } }

        /// <summary>
        /// 그 칸에서 이후 로그를 미는 값. 칸마다의 값이 없으면
        /// <paramref name="fallback"/> 을 돌려줍니다.
        /// </summary>
        public double ShiftOf(int i, double fallback)
        {
            if (_shifts == null || i < 0 || i >= _shifts.Length) return fallback;
            return _shifts[i];
        }

        public int Count { get { return _from.Length; } }

        /// <summary>칸이 시작하는 시각 (이전 로그 기준).</summary>
        public double From(int i) { return _from[i]; }

        /// <summary>칸이 끝나는 시각. 이 시각은 칸에 <b>안</b> 들어갑니다.</summary>
        public double To(int i) { return _to[i]; }

        /// <summary>
        /// 그 시각이 든 칸. 어느 칸에도 안 들면 -1 입니다.
        ///
        /// -1 이 나오는 것은 <b>단계 사이의 빈틈</b>뿐입니다. 일정 폭으로
        /// 자르면 칸이 빈틈 없이 붙어 있어서 -1 이 없습니다.
        /// </summary>
        public int Find(double t)
        {
            int lo = 0, hi = _from.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (t < _from[mid]) hi = mid - 1;
                else if (t >= _to[mid]) lo = mid + 1;
                else return mid;
            }
            return -1;
        }

        /// <summary>
        /// 시각이 커지는 순서로 훑을 때 쓰는 빠른 길. <paramref name="k"/> 를
        /// 앞으로만 밀면서 찾습니다 (표본마다 이진 탐색을 하지 않게).
        /// 빈틈에 든 시각은 -1 을 돌려주고 k 는 그대로 둡니다.
        /// </summary>
        public int Walk(double t, ref int k)
        {
            int n = _from.Length;
            if (k < 0) k = 0;
            while (k < n && t >= _to[k]) k++;
            if (k >= n) return -1;
            return t >= _from[k] ? k : -1;
        }

        /// <summary>
        /// 가로축에 적을 글자. 단계로 자르면 단계 이름이고, 그게 아니면
        /// 칸이 시작하는 시각입니다.
        /// </summary>
        public string Label(int i, LogDataset timeRef)
        {
            if (i < 0 || i >= _from.Length) return string.Empty;
            if (_labels != null) return _labels[i];
            return timeRef != null ? timeRef.FormatTime(_from[i]) : NumberText.Plain(_from[i]);
        }

        // ================= 자르는 길 =================

        /// <summary>일정한 시간 폭으로 자릅니다. 칸이 빈틈 없이 붙습니다.</summary>
        public static HeatBuckets Uniform(double t0, double t1, double span)
        {
            if (!(span > 0)) span = Math.Max((t1 - t0) / 60.0, 1e-9);
            int n = (int)Math.Ceiling((t1 - t0) / span);
            if (n < 1) n = 1;

            var from = new double[n];
            var to = new double[n];
            for (int i = 0; i < n; i++)
            {
                from[i] = t0 + i * span;
                // 마지막 칸은 끝 시각까지 덮습니다. 나눗셈이 딱 맞지 않으면
                // 마지막 표본 몇 개가 어느 칸에도 안 들어갑니다.
                to[i] = i == n - 1 ? Math.Max(t1 + Math.Abs(t1) * 1e-12 + 1e-9, from[i] + span)
                                   : t0 + (i + 1) * span;
            }
            return new HeatBuckets(from, to, null, null, false, span);
        }

        /// <summary>
        /// <b>단계 하나를 칸 하나로</b> 자릅니다. 가로가 시간 대신 공정
        /// 차례가 됩니다.
        ///
        /// 칸의 경계는 <b>이전 로그</b>의 단계 구간입니다. 히트맵은 이전
        /// 로그의 시간을 바탕으로 그려지고 이후 로그를 밀어서 견주기
        /// 때문입니다 — 이후 로그의 단계 경계로 자르면 밀기 값을 두 번
        /// 거친 자리가 되어 칸이 어긋납니다.
        ///
        /// 양쪽에 다 있는 단계만 칸이 됩니다. 한쪽에만 있는 단계는 견줄
        /// 것이 없으니 칸을 만들어도 비어 있고, 빈 칸이 줄줄이 있으면
        /// "차이가 없다" 로 읽힙니다. 몇 개를 뺐는지는 말해 줍니다.
        /// </summary>
        public static HeatBuckets Steps(StepPairList pairs, LogDataset before,
                                        double t0, double t1, out string warning)
        {
            warning = string.Empty;
            if (pairs == null || pairs.IsEmpty || before == null) return null;

            var from = new List<double>();
            var to = new List<double>();
            var labels = new List<string>();
            var shifts = new List<double>();

            int outside = 0, oneSided = 0;

            for (int i = 0; i < pairs.Count; i++)
            {
                StepPair p = pairs[i];
                if (!p.HasBefore) { oneSided++; continue; }
                if (!p.HasBoth) { oneSided++; continue; }

                double a = p.BeforeStart, b = p.BeforeEnd;
                if (double.IsNaN(a) || double.IsNaN(b)) continue;

                // 견주는 구간 밖은 잘라 들입니다. 통째로 밖이면 뺍니다.
                if (b < t0 || a > t1) { outside++; continue; }
                if (a < t0) a = t0;
                if (b > t1) b = t1;

                // 끝 시각은 칸에 안 들어가므로 마지막 표본이 빠지지 않게
                // 아주 조금 넘겨 둡니다.
                double end = b + Math.Max(Math.Abs(b) * 1e-12, 1e-9);
                if (end <= a) end = a + 1e-9;

                from.Add(a);
                to.Add(end);
                labels.Add(p.Label);

                // 그 단계의 밀기 값. 칸마다 다릅니다 — 이것이 단계로
                // 자르는 뜻의 절반입니다 (나머지 절반은 칸 자리).
                shifts.Add(p.Shift);
            }

            if (from.Count == 0)
            {
                warning = "견주는 구간 안에 양쪽에 다 있는 단계가 없습니다. 가로를 시간으로 둡니다.";
                return null;
            }

            if (oneSided > 0)
                warning = "짝이 없는 단계 " + oneSided + " 개는 칸으로 만들지 않았습니다.";
            if (outside > 0)
                warning = (warning.Length > 0 ? warning + " " : string.Empty)
                        + "견주는 구간 밖의 단계 " + outside + " 개도 뺐습니다.";

            return new HeatBuckets(from.ToArray(), to.ToArray(), labels.ToArray(),
                                   shifts.ToArray(), true, double.NaN);
        }
    }
}
