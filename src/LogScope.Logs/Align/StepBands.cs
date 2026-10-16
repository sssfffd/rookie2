using System;
using System.Collections.Generic;

namespace LogScope.Logs.Align
{
    /// <summary>
    /// 그래프 가로에 나란히 놓는 <b>단계 칸</b> 하나.
    ///
    /// 칸 하나가 단계 하나이고, 칸마다 <b>제 밀기 값</b>을 들고 있습니다.
    /// 그래서 한 화면에서 모든 단계가 저마다 맞춰진 채로 보입니다 — 단계마다
    /// 손으로 맞출 일이 없습니다.
    /// </summary>
    public sealed class StepBand
    {
        /// <summary>칸 위에 적는 글 ("3. 가열").</summary>
        public string Label = string.Empty;

        /// <summary>이 칸이 보여 주는 구간 (이전 로그 시간 기준).</summary>
        public double T0;
        public double T1;

        /// <summary>이 칸에서 이후 로그를 미는 값 (이후 시각 + Shift = 이전 시각).</summary>
        public double Shift;

        public double Length { get { return T1 - T0; } }
    }

    /// <summary>
    /// <b>단계별로 나눠 보기</b>. 가로를 한 줄 시간으로 쭉 늘어놓는 대신,
    /// 단계마다 칸을 하나씩 두고 나란히 놓습니다.
    ///
    /// <code>
    ///   [1. 준비 ][ 2. 가열     ][3. 유지 ][ 4. 냉각   ]
    /// </code>
    ///
    /// 칸마다 밀기 값이 따로라서, 이전·이후 로그가 <b>그 단계 안에서</b>
    /// 맞춰집니다. 공정이 중간에 늘어지거나 당겨진 로그를 견줄 때 한 줄
    /// 시간으로는 뒤로 갈수록 어긋나는데, 단계로 끊으면 그 어긋남이 칸
    /// 안에서 끝납니다.
    ///
    /// <b>단계 사이의 빈틈은 칸이 없습니다.</b> 공정이 멈춘 구간은 볼 것이
    /// 없고, 자리를 주면 그만큼 볼 것이 줄어듭니다 (히트맵의 칸 자르기와
    /// 같은 규칙입니다).
    /// </summary>
    public sealed class StepBands
    {
        private readonly List<StepBand> _bands;

        /// <summary>칸으로 만들지 못한 단계 수 (짝이 없는 단계).</summary>
        public readonly int Skipped;

        private StepBands(List<StepBand> bands, int skipped)
        {
            _bands = bands;
            Skipped = skipped;
        }

        public int Count { get { return _bands.Count; } }
        public StepBand this[int i] { get { return _bands[i]; } }
        public bool IsEmpty { get { return _bands.Count == 0; } }

        /// <summary>모든 칸의 길이 합. 폭을 길이에 비례해 나누는 데 씁니다.</summary>
        public double TotalLength
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _bands.Count; i++) sum += _bands[i].Length;
                return sum;
            }
        }

        /// <summary>
        /// 짝지어 둔 단계로 칸을 만듭니다. <b>양쪽에 다 있는 단계만</b>
        /// 칸이 됩니다 — 한쪽에만 있는 단계는 맞출 값이 없어서, 칸을 만들면
        /// 이전 로그만 덜렁 그려지고 그게 "이후가 없다" 로 읽힙니다.
        /// 몇 개를 뺐는지는 <see cref="Skipped"/> 가 들고 있습니다.
        /// </summary>
        public static StepBands Build(StepPairList pairs)
        {
            var list = new List<StepBand>();
            int skipped = 0;
            if (pairs == null) return new StepBands(list, 0);

            for (int i = 0; i < pairs.Count; i++)
            {
                StepPair p = pairs[i];
                if (!p.HasBoth || double.IsNaN(p.Shift)) { skipped++; continue; }

                double t0 = p.BeforeStart;
                double t1 = Math.Max(p.BeforeEnd, p.AfterEnd + p.Shift);
                if (!(t1 > t0)) { skipped++; continue; }

                var b = new StepBand();
                b.Label = p.Label;
                b.T0 = t0;
                b.T1 = t1;
                b.Shift = p.Shift;
                list.Add(b);
            }
            return new StepBands(list, skipped);
        }

        /// <summary>
        /// 칸들의 가로 폭을 나눕니다. <b>길이에 비례</b>해서 나눕니다 —
        /// 똑같은 폭으로 나누면 짧은 단계가 늘어나고 긴 단계가 눌려서,
        /// 같은 기울기가 칸마다 다른 기울기로 보입니다.
        ///
        /// 다만 너무 짧은 단계가 <b>선 한 줄</b>이 되어 버리면 그 단계는
        /// 아예 못 봅니다. 그래서 최소 폭을 보장하고, 모자란 만큼은 넉넉한
        /// 칸들에서 비례로 떼어 옵니다.
        ///
        /// 칸이 많아 최소 폭조차 안 되면 <b>똑같이 나눕니다</b> (그 경우엔
        /// 비례를 지킬 자리가 없습니다).
        /// </summary>
        public double[] Widths(double total, double min)
        {
            int n = _bands.Count;
            var w = new double[n];
            if (n == 0) return w;

            if (!(total > 0)) return w;
            if (min < 0) min = 0;

            // 최소 폭조차 안 들어가면 똑같이 나눕니다.
            if (min * n >= total)
            {
                for (int i = 0; i < n; i++) w[i] = total / n;
                return w;
            }

            double sum = TotalLength;
            if (!(sum > 0))
            {
                for (int i = 0; i < n; i++) w[i] = total / n;
                return w;
            }

            // 1) 길이에 비례해 나눕니다.
            for (int i = 0; i < n; i++) w[i] = total * (_bands[i].Length / sum);

            // 2) 최소 폭에 못 미치는 칸을 올려 주고, 올려 준 만큼을
            //    남은 칸들에서 비례로 떼어 옵니다. 떼어 오다 또 최소 폭
            //    아래로 내려가는 칸이 생기므로 더 내려갈 칸이 없을 때까지
            //    돌립니다 (칸 수만큼이면 반드시 끝납니다).
            for (int round = 0; round < n; round++)
            {
                double need = 0, spare = 0;
                for (int i = 0; i < n; i++)
                {
                    if (w[i] < min) need += min - w[i];
                    else spare += w[i] - min;
                }
                if (need <= 0 || spare <= 0) break;

                double take = Math.Min(need, spare);
                double scale = take / spare;
                for (int i = 0; i < n; i++)
                {
                    if (w[i] < min) w[i] = min;
                    else w[i] -= (w[i] - min) * scale;
                }
            }

            // 3) 반올림 오차로 전체가 조금 모자라거나 넘칠 수 있습니다.
            //    마지막 칸에서 맞춥니다 — 칸 사이에 틈이 생기면 그 자리가
            //    "기록 없음" 으로 보입니다.
            double got = 0;
            for (int i = 0; i < n; i++) got += w[i];
            w[n - 1] += total - got;
            return w;
        }
    }
}
