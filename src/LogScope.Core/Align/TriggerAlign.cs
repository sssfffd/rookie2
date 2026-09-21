using System;
using System.Collections.Generic;
using LogScope.Core.Model;

namespace LogScope.Core.Align
{
    /// <summary>어느 쪽으로 바뀐 순간을 잡을지.</summary>
    public enum EdgeKind
    {
        /// <summary>고른 값이 <b>되는</b> 순간. 처음부터 그 값이면 시작 지점.</summary>
        Rising = 0,
        /// <summary>고른 값에서 <b>벗어나는</b> 순간.</summary>
        Falling = 1,
        /// <summary>값이 바뀌는 순간이면 어디든.</summary>
        Any = 2,
    }

    /// <summary>
    /// 한 채널에서 고를 수 있는 값들.
    ///
    /// 대개는 채널에 실제로 나온 값들을 그대로 씁니다 (0, 1, 2, 3 …).
    /// 값이 너무 많아 고르는 뜻이 없는 채널(진짜 아날로그)만 문턱 하나로
    /// 위/아래를 갈라 0 과 1 로 봅니다.
    /// </summary>
    public sealed class LevelSet
    {
        /// <summary>고를 수 있는 값들. 오름차순.</summary>
        public double[] Values = new double[0];

        /// <summary>참이면 Values 는 {0,1} 이고 Threshold 로 가른 것입니다.</summary>
        public bool Thresholded;

        /// <summary>Thresholded 일 때 0 과 1 을 가르는 값.</summary>
        public double Threshold = double.NaN;

        /// <summary>화면에 적을 이름. 상태 채널이면 상태 이름입니다.</summary>
        public string[] Names = new string[0];

        public bool IsEmpty { get { return Values.Length == 0; } }

        /// <summary>value 가 몇 번째 값인지. 없으면 -1.</summary>
        public int IndexOf(double value)
        {
            for (int i = 0; i < Values.Length; i++)
                if (Values[i] == value) return i;
            return -1;
        }

        /// <summary>고를 값이 비었거나 이 채널에 없을 때 대신 쓸 값.</summary>
        public double Fallback()
        {
            if (Values.Length == 0) return double.NaN;
            int one = IndexOf(1.0);
            if (one >= 0) return 1.0;
            return Values[Values.Length - 1];   // 최댓값
        }

        public string NameOf(double value)
        {
            int i = IndexOf(value);
            return i >= 0 ? Names[i] : NumberText.Plain(value);
        }
    }

    /// <summary>한쪽 로그에서 찾아낸 변화 순간.</summary>
    public sealed class EdgeHit
    {
        public bool Found;
        public double Time;
        /// <summary>바뀌기 직전 / 직후의 값.</summary>
        public double From = double.NaN, To = double.NaN;
        /// <summary>몇 번째 변화였는지 (1 부터).</summary>
        public int Index;
        /// <summary>문턱으로 갈랐다면 그 문턱값. 값 그대로 봤으면 NaN.</summary>
        public double Level = double.NaN;
        /// <summary>바뀐 게 아니라 <b>처음부터</b> 그 값이었던 경우.</summary>
        public bool AtStart;
        public string Problem = string.Empty;
    }

    /// <summary>두 로그를 맞춘 결과.</summary>
    public sealed class AlignResult
    {
        public bool Ok;
        public double Shift;
        public EdgeHit Before, After;
        public string Message = string.Empty;
    }

    /// <summary>
    /// <b>특정 IO 가 어떤 값이 되는 순간</b>을 기준으로 두 로그의 시간축을
    /// 맞춥니다.
    ///
    /// 이전 로그와 이후 로그는 시작 시각이 다를 수 있습니다. 시작 시각끼리
    /// 맞추는 것(자동 맞추기)은 기록을 시작한 시점이 같을 때만 뜻이 있는데,
    /// 실제로는 "설비가 기동한 순간" 처럼 <b>로그 안의 어떤 사건</b>을 기준으로
    /// 맞춰야 두 로그가 같은 이야기를 하게 됩니다.
    ///
    ///   밀기 값 = 이전 로그의 사건 시각 − 이후 로그의 사건 시각
    ///
    /// 이 값을 더하면 이후 로그의 시각이 이전 로그의 시각으로 옮겨집니다.
    /// (그리는 쪽과 견주는 쪽 모두 "이후 시각 + 밀기 = 이전 시각" 으로 씁니다.)
    ///
    /// <b>0/1 만 보지 않습니다.</b> 채널에 실제로 나온 값들을 그대로 고를 수
    /// 있어서, 0~5 로 움직이는 단계 신호라면 "5 가 되는 순간" 으로도 맞출 수
    /// 있습니다. 값 종류가 너무 많은 진짜 아날로그 채널만 값 범위의 한가운데를
    /// 문턱으로 잡아 위/아래로 가릅니다.
    ///
    /// <b>처음부터 그 값이면 시작 지점을 씁니다.</b> "1 이 되는 순간" 을
    /// 골랐는데 로그가 이미 1 로 시작한다면, 그 로그에서는 첫 표본이 그
    /// 순간입니다. 기록을 늦게 건 로그에서 흔한 일이라, 이걸 놓치면 맞출 수
    /// 있는 로그를 못 맞춥니다.
    /// </summary>
    public static class TriggerAlign
    {
        /// <summary>
        /// 이 개수를 넘게 값이 나오면 "고를 수 있는 값" 으로 보지 않고
        /// 문턱 하나로 가릅니다. 목록에 수천 줄을 올릴 수는 없습니다.
        /// </summary>
        public const int MaxLevels = 64;

        /// <summary>
        /// 채널에서 고를 수 있는 값들을 뽑습니다.
        /// </summary>
        public static LevelSet LevelsOf(LogDataset ds, int channel)
        {
            var set = new LevelSet();
            if (ds == null || channel < 0 || channel >= ds.ChannelCount) return set;

            Channel c = ds.Channels[channel];
            float[] v = c.Values;
            int n = Math.Min(v.Length, ds.SampleCount);
            if (n < 1) return set;

            // 값 그대로 모읍니다. 파일에서 읽은 값이라 계산이 섞이지 않았고,
            // 그래서 같은 값끼리는 비트까지 같습니다 — 그대로 견줘도 됩니다.
            //
            // 해시로 모읍니다. 이 함수는 채널마다 불리고 표본이 수백만 개일
            // 수 있어서, 값 하나마다 목록을 훑으면 그것만으로 몇 초가 갑니다.
            // 종류가 MaxLevels 를 넘는 순간 바로 멈춥니다 — 진짜 아날로그
            // 채널은 대개 몇십 표본 만에 넘어서라 끝까지 볼 일이 없습니다.
            var uniq = new HashSet<double>();
            var seen = new List<double>();
            bool tooMany = false;
            for (int i = 0; i < n; i++)
            {
                float raw = v[i];
                if (float.IsNaN(raw)) continue;
                double x = raw;
                if (!uniq.Add(x)) continue;
                seen.Add(x);
                if (seen.Count > MaxLevels) { tooMany = true; break; }
            }

            if (seen.Count == 0) return set;

            if (tooMany)
            {
                // 진짜 아날로그. 값 범위의 한가운데로 가릅니다 —
                // 4~20mA 든 0~10V 든 "켜짐/꺼짐" 을 나누는 자리는 대개 거깁니다.
                set.Thresholded = true;
                set.Threshold = Threshold(c);
                set.Values = new double[] { 0, 1 };
                set.Names = new string[]
                {
                    "0 (문턱 " + NumberText.Plain(set.Threshold) + " 아래)",
                    "1 (문턱 " + NumberText.Plain(set.Threshold) + " 위)",
                };
                return set;
            }

            seen.Sort();
            set.Values = seen.ToArray();
            set.Names = new string[set.Values.Length];
            for (int i = 0; i < set.Values.Length; i++)
                set.Names[i] = c.FormatValue(set.Values[i]);
            return set;
        }

        /// <summary>
        /// 0 과 1 을 가르는 문턱값. 값 종류가 너무 많은 채널에만 씁니다.
        /// </summary>
        public static double Threshold(Channel c)
        {
            if (c == null) return 0.5;
            if (double.IsNaN(c.Min) || double.IsNaN(c.Max)) return 0.5;
            if (c.Max - c.Min <= 0) return c.Max + 0.5;   // 움직이지 않는 채널
            return (c.Min + c.Max) * 0.5;
        }

        /// <summary>
        /// 채널 하나에서 n 번째 사건을 찾습니다.
        /// </summary>
        /// <param name="ds">로그.</param>
        /// <param name="channel">채널 번호.</param>
        /// <param name="kind">되는 순간인지, 벗어나는 순간인지, 아무 변화인지.</param>
        /// <param name="occurrence">몇 번째인지. 1 이 첫 번째입니다.</param>
        /// <param name="target">기준이 되는 값. NaN 이면 이 채널에 맞게 고릅니다.</param>
        public static EdgeHit FindEdge(LogDataset ds, int channel, EdgeKind kind,
                                       int occurrence, double target)
        {
            var hit = new EdgeHit();
            if (ds == null || channel < 0 || channel >= ds.ChannelCount)
            {
                hit.Problem = "채널을 찾지 못했습니다.";
                return hit;
            }
            if (occurrence < 1) occurrence = 1;

            Channel c = ds.Channels[channel];
            float[] v = c.Values;
            double[] t = ds.Times;
            int n = Math.Min(v.Length, t.Length);
            if (n < 1)
            {
                hit.Problem = "표본이 없습니다.";
                return hit;
            }

            LevelSet ls = LevelsOf(ds, channel);
            if (ls.IsEmpty)
            {
                hit.Problem = "값이 하나도 없습니다.";
                return hit;
            }

            // 고른 값이 이 로그에 아예 없으면 맞출 수가 없습니다. 조용히
            // 다른 값으로 바꿔치기하면 두 로그가 서로 다른 사건에 맞춰집니다.
            if (kind != EdgeKind.Any)
            {
                if (double.IsNaN(target)) target = ls.Fallback();
                else if (ls.IndexOf(target) < 0)
                {
                    hit.Problem = "이 로그에서는 그 IO 가 "
                                + NumberText.Plain(target) + " 이 된 적이 없습니다.";
                    return hit;
                }
            }
            hit.Level = ls.Thresholded ? ls.Threshold : double.NaN;

            bool started = false, havePrev = false;
            double prev = 0;
            int seen = 0;

            for (int i = 0; i < n; i++)
            {
                float raw = v[i];
                if (float.IsNaN(raw)) { havePrev = false; continue; }

                double cur = ls.Thresholded ? (raw >= ls.Threshold ? 1.0 : 0.0) : (double)raw;

                bool isEdge, atStart = false;
                switch (kind)
                {
                    case EdgeKind.Rising:
                        // 그 값이 "되는" 순간. 처음부터 그 값이면 시작 지점이
                        // 곧 그 순간입니다. 빈 값 뒤는 직전을 모르니 세지
                        // 않습니다 — 모르는 것을 사건으로 만들지 않습니다.
                        atStart = !started;
                        isEdge = cur == target && (!started || (havePrev && prev != target));
                        break;
                    case EdgeKind.Falling:
                        isEdge = havePrev && prev == target && cur != target;
                        break;
                    default:
                        isEdge = havePrev && cur != prev;
                        break;
                }

                double before = havePrev ? prev : double.NaN;
                prev = cur; havePrev = true; started = true;
                if (!isEdge) continue;

                seen++;
                if (seen < occurrence) continue;

                hit.Found = true;
                hit.Time = t[i];
                hit.From = before;
                hit.To = cur;
                hit.Index = seen;
                hit.AtStart = atStart;
                return hit;
            }

            hit.Problem = seen == 0
                ? Nothing(kind, target, ls)
                : "그런 순간이 " + seen + " 번뿐이라 " + occurrence + " 번째를 찾을 수 없습니다.";
            return hit;
        }

        private static string Nothing(EdgeKind kind, double target, LevelSet ls)
        {
            switch (kind)
            {
                case EdgeKind.Rising:
                    return "이 로그에서는 그 IO 가 " + ls.NameOf(target) + " 이 된 적이 없습니다.";
                case EdgeKind.Falling:
                    return "이 로그에서는 그 IO 가 " + ls.NameOf(target) + " 에서 벗어난 적이 없습니다.";
                default:
                    return "이 로그에서는 그 IO 가 한 번도 바뀌지 않았습니다.";
            }
        }

        /// <summary>
        /// 두 로그를 그 IO 의 사건으로 맞춥니다.
        /// </summary>
        /// <param name="before">이전 로그.</param>
        /// <param name="after">이후 로그.</param>
        /// <param name="ioName">기준으로 삼을 IO 이름. 양쪽에 다 있어야 합니다.</param>
        /// <param name="kind">되는 순간인지, 벗어나는 순간인지, 아무 변화인지.</param>
        /// <param name="occurrence">몇 번째인지. 1 이 첫 번째입니다.</param>
        /// <param name="target">기준이 되는 값. NaN 이면 채널에 맞게 고릅니다.</param>
        public static AlignResult Compute(LogDataset before, LogDataset after,
                                          string ioName, EdgeKind kind,
                                          int occurrence, double target)
        {
            var r = new AlignResult();
            if (before == null || after == null)
            {
                r.Message = "맞추려면 이전 로그와 이후 로그가 모두 있어야 합니다.";
                return r;
            }
            if (string.IsNullOrEmpty(ioName))
            {
                r.Message = "기준으로 삼을 IO 를 하나 골라 주세요.";
                return r;
            }

            int bi = before.FindChannel(ioName);
            int ai = after.FindChannel(ioName);
            if (bi < 0 || ai < 0)
            {
                r.Message = "\"" + ioName + "\" 는 "
                          + (bi < 0 && ai < 0 ? "양쪽 로그에" : bi < 0 ? "이전 로그에" : "이후 로그에")
                          + " 없습니다. 양쪽에 다 있는 IO 로 골라 주세요.";
                return r;
            }

            r.Before = FindEdge(before, bi, kind, occurrence, target);
            r.After = FindEdge(after, ai, kind, occurrence, target);

            if (!r.Before.Found || !r.After.Found)
            {
                r.Message = "\"" + ioName + "\" 로는 맞출 수 없습니다. "
                          + (!r.Before.Found ? "이전 로그: " + r.Before.Problem + " " : "")
                          + (!r.After.Found ? "이후 로그: " + r.After.Problem : "");
                return r;
            }

            r.Ok = true;
            r.Shift = r.Before.Time - r.After.Time;
            r.Message = "\"" + ioName + "\" " + Describe(kind, target, LevelsOf(before, bi))
                      + (occurrence > 1 ? " (" + occurrence + "번째)" : "")
                      + " 로 맞췄습니다.   이전 " + before.FormatTime(r.Before.Time)
                      + StartNote(r.Before)
                      + "  ↔  이후 " + after.FormatTime(r.After.Time)
                      + StartNote(r.After);
            return r;
        }

        private static string StartNote(EdgeHit h)
        {
            return h != null && h.AtStart ? " (처음부터)" : string.Empty;
        }

        public static string Describe(EdgeKind kind, double target, LevelSet ls)
        {
            string name = ls != null ? ls.NameOf(target) : NumberText.Plain(target);
            switch (kind)
            {
                case EdgeKind.Rising: return "가 " + name + " 이 되는 순간";
                case EdgeKind.Falling: return "가 " + name + " 에서 벗어나는 순간";
                default: return "의 값이 바뀌는 순간";
            }
        }

        /// <summary>
        /// 이 채널로 맞출 수 있는지 미리 봅니다. 목록에서 고를 후보를
        /// 추리는 데 씁니다.
        ///
        /// <b>한쪽 로그에서만 바뀌어도 후보입니다.</b> 반대쪽이 처음부터 그
        /// 값이면 거기서는 시작 지점이 그 순간이라, 그래도 맞춰집니다 —
        /// 기록을 늦게 건 로그에서 흔한 모양입니다.
        /// </summary>
        public static bool CanTrigger(LogDataset ds, int channel)
        {
            EdgeHit h = FindEdge(ds, channel, EdgeKind.Any, 1, double.NaN);
            return h.Found;
        }
    }
}
