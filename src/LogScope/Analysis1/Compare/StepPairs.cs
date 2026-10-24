using System;
using System.Collections.Generic;
using LogScope.Analysis1.LogFile;
using LogScope.Common;

namespace LogScope.Analysis1.Compare
{
    /// <summary>
    /// 두 로그의 <b>같은 단계</b> 한 짝. 이전 로그의 "가열" 과 이후 로그의
    /// "가열" 입니다.
    ///
    /// 이게 있으면 단계마다 기준 IO·값·몇 번째 사건을 손으로 맞출 일이
    /// 없습니다. 단계가 시작하는 두 시각을 알고 있으니 밀기 값이 바로
    /// 나옵니다 (<see cref="Shift"/>).
    /// </summary>
    public sealed class StepPair
    {
        /// <summary>단계 이름. 한쪽에만 있어도 그 쪽 이름이 들어갑니다.</summary>
        public string Name = string.Empty;

        /// <summary>목록에 적는 글 ("가열", 둘째부터 "가열 (2)"). 몇 번째인지는 이전 로그 차례입니다.</summary>
        public string Label = string.Empty;

        /// <summary>이전 로그에서 몇 번째 단계인지. 이전 로그에 없으면 -1.</summary>
        public int BeforeIndex = -1;

        /// <summary>이후 로그에서 몇 번째 단계인지. 이후 로그에 없으면 -1.</summary>
        public int AfterIndex = -1;

        public double BeforeStart = double.NaN;
        public double BeforeEnd = double.NaN;
        public double AfterStart = double.NaN;
        public double AfterEnd = double.NaN;

        /// <summary>양쪽에 다 있는 단계인지. 아니면 맞출 수가 없습니다.</summary>
        public bool HasBoth { get { return BeforeIndex >= 0 && AfterIndex >= 0; } }

        /// <summary>
        /// 이 단계를 맞추려면 <b>이후 로그를 이만큼 밀면</b> 됩니다.
        /// 두 단계의 시작이 겹칩니다. 한쪽만 있으면 NaN.
        ///
        /// 부호 뜻은 다른 밀기 값과 같습니다 — 이후 시각 + Shift = 이전 시각.
        /// </summary>
        public double Shift
        {
            get { return HasBoth ? BeforeStart - AfterStart : double.NaN; }
        }

        /// <summary>이전 로그에서 이 단계의 길이. 그래프가 보여 줄 폭입니다.</summary>
        public double BeforeLength
        {
            get { return HasBefore ? BeforeEnd - BeforeStart : double.NaN; }
        }

        public double AfterLength
        {
            get { return HasAfter ? AfterEnd - AfterStart : double.NaN; }
        }

        public bool HasBefore { get { return BeforeIndex >= 0; } }
        public bool HasAfter { get { return AfterIndex >= 0; } }

        /// <summary>
        /// 이 짝에 대해 알아 둘 점. 한쪽만 있거나, 길이가 많이 다르거나.
        /// 없으면 빈 글자입니다.
        /// </summary>
        public string Note = string.Empty;
    }

    /// <summary>
    /// 단계 하나를 볼 때 그래프가 보여 줄 <b>구간</b>.
    ///
    /// 화면이 아니라 여기 둔 까닭: 여기가 틀리면 로그의 일부가 조용히
    /// 화면 밖으로 나갑니다. 사람이 보기엔 그냥 그래프라서 잘린 줄
    /// 모릅니다 — 그래서 시험이 닿는 자리에 둡니다.
    /// </summary>
    public static class StepWindow
    {
        /// <summary>
        /// 양쪽에 두는 여유 (구간 길이의 몫). 단계 경계의 값이 화면 끝에
        /// 딱 붙어 있으면 그 자리의 변화가 안 보입니다.
        /// </summary>
        public const double Pad = 0.02;

        /// <summary>
        /// 그 단계를 보여 줄 구간. 맞출 수 없는 단계면 거짓.
        ///
        /// <b>두 로그 중 긴 쪽까지 덮습니다.</b> 이전 로그의 단계에만
        /// 맞추면, 이후 로그에서 그 단계가 더 길 때 뒷부분이 화면 밖으로
        /// 나갑니다. 잘린 줄 모르면 "이후 로그는 왜 중간에 끊겼지" 가
        /// 됩니다.
        ///
        /// 돌려주는 시각은 <b>이전 로그 기준</b>입니다 (그래프의 가로축이
        /// 그 기준입니다). 이후 로그의 끝은 밀기 값을 더해 옮겨 옵니다.
        /// </summary>
        public static bool Of(StepPair p, out double t0, out double t1)
        {
            t0 = 0; t1 = 1;
            if (p == null || !p.HasBoth) return false;

            double shift = p.Shift;
            if (double.IsNaN(shift)) return false;

            double a = p.BeforeStart;
            double b = Math.Max(p.BeforeEnd, p.AfterEnd + shift);
            if (!(b > a)) return false;

            double pad = (b - a) * Pad;
            t0 = a - pad;
            t1 = b + pad;
            return true;
        }
    }

    /// <summary>
    /// 두 로그의 단계를 짝지은 목록.
    ///
    /// <b>이름으로, 차례대로 짝짓습니다.</b> 같은 이름이 여러 번 나오는
    /// 공정(가열 → 냉각 → 가열)에서는 <b>n 번째 끼리</b> 맞춥니다.
    ///
    /// 이름이 다른 단계를 "하나씩 남았으니 같은 것이겠지" 로 붙이지
    /// 않습니다. DB 쪽에서 같은 짐작을 했다가, 아무 상관 없는 표 둘이
    /// 짝지어진 일이 있었습니다 (0.86). 그래프는 더 나쁩니다 — 틀린
    /// 짝으로 맞춰 놓으면 두 로그가 나란히 그려지니 맞는 것처럼 보입니다.
    ///
    /// 짝이 안 맞은 단계도 <b>목록에 남깁니다</b> (고를 수는 없음). 조용히
    /// 빠지면 "이전에는 있던 단계가 왜 없지" 를 알 수 없습니다.
    /// </summary>
    public sealed class StepPairList
    {
        public static readonly StepPairList None = new StepPairList();

        public List<StepPair> Pairs = new List<StepPair>();

        /// <summary>짝이 안 맞은 것 요약. 없으면 빈 글자.</summary>
        public string Note = string.Empty;

        /// <summary>단계가 어디서 왔는지 (두 로그가 다르면 둘 다 적습니다).</summary>
        public string Source = string.Empty;

        public int Count { get { return Pairs.Count; } }
        public bool IsEmpty { get { return Pairs.Count == 0; } }
        public StepPair this[int i] { get { return Pairs[i]; } }

        /// <summary>양쪽에 다 있어 고를 수 있는 단계 수.</summary>
        public int UsableCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Pairs.Count; i++) if (Pairs[i].HasBoth) n++;
                return n;
            }
        }

        /// <summary>목록 글로 찾기. 설정에 적어 둔 단계를 다시 고를 때 씁니다.</summary>
        public int IndexOfLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < Pairs.Count; i++)
                if (string.Equals(Pairs[i].Label, label, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// 길이가 이 배수 넘게 다르면 알려 줍니다. 단계 하나만 보려고 폭을
        /// 좁히는데 한쪽이 훨씬 길면 <b>그 쪽은 화면 밖으로 잘립니다</b> —
        /// 잘린 것을 모르면 "뒷부분이 왜 없지" 가 됩니다.
        /// </summary>
        public const double LengthWarnRatio = 1.5;

        public static StepPairList Build(LogDataset before, LogDataset after)
        {
            var res = new StepPairList();
            if (before == null || after == null) return res;

            StepTable bs = before.Steps, as_ = after.Steps;
            if (bs.IsEmpty && as_.IsEmpty) return res;

            res.Source = SourceText(bs, as_);

            // 이후 로그의 어느 단계를 이미 썼는지. 같은 이름의 n 번째를
            // 찾는 데 쓰면서, 한 단계가 두 번 짝지어지는 것을 막습니다.
            var used = new bool[as_.Count];
            int unmatchedBefore = 0;

            for (int i = 0; i < bs.Count; i++)
            {
                LogStep b = bs[i];
                var p = new StepPair();
                p.Name = b.Name;
                p.Label = bs.Label(i);
                p.BeforeIndex = i;
                p.BeforeStart = bs.StartTime(before, i);
                p.BeforeEnd = bs.EndTime(before, i);

                int j = NextByName(as_, b.Name, used);
                if (j >= 0)
                {
                    used[j] = true;
                    p.AfterIndex = j;
                    p.AfterStart = as_.StartTime(after, j);
                    p.AfterEnd = as_.EndTime(after, j);
                    p.Note = LengthNote(p);
                }
                else
                {
                    unmatchedBefore++;
                    p.Note = "이후 로그에 이 단계가 없습니다. 맞출 수 없습니다.";
                }
                res.Pairs.Add(p);
            }

            // 이후 로그에만 있는 단계. 뒤에 붙입니다 — 이전 로그 차례
            // 사이에 끼워 넣으면 번호가 밀려서, 목록의 "3." 과 이전 로그의
            // 3 번째 단계가 어긋납니다.
            int onlyAfter = 0;
            for (int j = 0; j < as_.Count; j++)
            {
                if (used[j]) continue;
                onlyAfter++;
                var p = new StepPair();
                p.Name = as_[j].Name;
                p.Label = "(이후만) " + as_.Label(j);
                p.AfterIndex = j;
                p.AfterStart = as_.StartTime(after, j);
                p.AfterEnd = as_.EndTime(after, j);
                p.Note = "이전 로그에 이 단계가 없습니다. 맞출 수 없습니다.";
                res.Pairs.Add(p);
            }

            res.Note = Summary(unmatchedBefore, onlyAfter);
            return res;
        }

        /// <summary>같은 이름의, 아직 안 쓴 첫 단계. 없으면 -1.</summary>
        private static int NextByName(StepTable t, string name, bool[] used)
        {
            for (int j = 0; j < t.Count; j++)
            {
                if (used[j]) continue;
                if (StepTable.Same(t[j].Name, name)) return j;
            }
            return -1;
        }

        private static string LengthNote(StepPair p)
        {
            double b = p.BeforeLength, a = p.AfterLength;
            if (!(b > 0) || !(a > 0)) return string.Empty;

            double big = Math.Max(b, a), small = Math.Min(b, a);
            if (big / small < LengthWarnRatio) return string.Empty;

            return "이 단계의 길이가 두 로그에서 다릅니다 (이전 "
                 + NumberText.Plain(b) + " · 이후 " + NumberText.Plain(a)
                 + "). 짧은 쪽에 맞춰 보면 긴 쪽의 뒷부분은 화면 밖입니다.";
        }

        private static string Summary(int unmatchedBefore, int onlyAfter)
        {
            if (unmatchedBefore == 0 && onlyAfter == 0) return string.Empty;

            string s = string.Empty;
            if (unmatchedBefore > 0)
                s = "이전 로그에만 있는 단계 " + unmatchedBefore + " 개";
            if (onlyAfter > 0)
                s = (s.Length > 0 ? s + " · " : string.Empty) + "이후 로그에만 있는 단계 " + onlyAfter + " 개";
            return s + " 는 짝이 없어 고를 수 없습니다 (목록에는 남겨 둡니다).";
        }

        private static string SourceText(StepTable b, StepTable a)
        {
            if (b.Source.Length == 0) return a.Source;
            if (a.Source.Length == 0 || string.Equals(a.Source, b.Source, StringComparison.Ordinal))
                return b.Source;
            return "이전: " + b.Source + " / 이후: " + a.Source;
        }
    }
}
