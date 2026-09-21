using System;
using LogScope.Core.Model;

namespace LogScope.Core.Align
{
    /// <summary>어느 쪽으로 바뀐 순간을 잡을지.</summary>
    public enum EdgeKind
    {
        /// <summary>0 에서 1 로 (꺼짐 → 켜짐).</summary>
        Rising = 0,
        /// <summary>1 에서 0 으로 (켜짐 → 꺼짐).</summary>
        Falling = 1,
        /// <summary>어느 쪽이든 바뀐 순간.</summary>
        Any = 2,
    }

    /// <summary>한쪽 로그에서 찾아낸 변화 순간.</summary>
    public sealed class EdgeHit
    {
        public bool Found;
        public double Time;
        /// <summary>바뀌기 직전 / 직후의 0-1 값.</summary>
        public int From, To;
        /// <summary>몇 번째 변화였는지 (1 부터).</summary>
        public int Index;
        /// <summary>0/1 로 바꿀 때 쓴 문턱값. 디지털·상태 채널은 NaN.</summary>
        public double Level = double.NaN;
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
    /// <b>특정 IO 가 바뀌는 순간</b>을 기준으로 두 로그의 시간축을 맞춥니다.
    ///
    /// 이전 로그와 이후 로그는 시작 시각이 다를 수 있습니다. 시작 시각끼리
    /// 맞추는 것(자동 맞추기)은 기록을 시작한 시점이 같을 때만 뜻이 있는데,
    /// 실제로는 "설비가 기동한 순간" 처럼 <b>로그 안의 어떤 사건</b>을 기준으로
    /// 맞춰야 두 로그가 같은 이야기를 하게 됩니다.
    ///
    /// 그래서 기준으로 삼을 IO 를 하나 고르고, 그 IO 가 <b>바뀌는 순간</b>을
    /// 양쪽에서 각각 찾아 그 둘이 겹치도록 이후 로그를 밉니다.
    ///
    ///   밀기 값 = 이전 로그의 변화 시각 − 이후 로그의 변화 시각
    ///
    /// 이 값을 더하면 이후 로그의 시각이 이전 로그의 시각으로 옮겨집니다.
    /// (그리는 쪽과 견주는 쪽 모두 "이후 시각 + 밀기 = 이전 시각" 으로 씁니다.)
    ///
    /// <b>0/1 로 바꿔서 봅니다.</b> 고른 IO 가 아날로그여도 상관없습니다.
    /// 값 범위의 한가운데를 문턱으로 잡아 0 과 1 로 나눕니다. 상태(문자열)
    /// 채널은 이름이 바뀌는 순간을 변화로 봅니다.
    /// </summary>
    public static class TriggerAlign
    {
        /// <summary>
        /// 채널 하나에서 n 번째 변화 순간을 찾습니다.
        /// </summary>
        /// <param name="ds">로그.</param>
        /// <param name="channel">채널 번호.</param>
        /// <param name="kind">어느 쪽으로 바뀐 것을 찾을지.</param>
        /// <param name="occurrence">몇 번째 변화인지. 1 이 첫 번째입니다.</param>
        public static EdgeHit FindEdge(LogDataset ds, int channel, EdgeKind kind, int occurrence)
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
            if (n < 2)
            {
                hit.Problem = "표본이 너무 적습니다.";
                return hit;
            }

            double level = Threshold(c);
            hit.Level = c.Kind == ChannelKind.Analog ? level : double.NaN;

            int prev = -1;
            int seen = 0;

            for (int i = 0; i < n; i++)
            {
                float raw = v[i];
                if (float.IsNaN(raw)) { prev = -1; continue; }

                int cur = raw >= level ? 1 : 0;
                if (prev < 0) { prev = cur; continue; }
                if (cur == prev) continue;

                bool want = kind == EdgeKind.Any
                            || (kind == EdgeKind.Rising && prev == 0 && cur == 1)
                            || (kind == EdgeKind.Falling && prev == 1 && cur == 0);
                prev = cur;
                if (!want) continue;

                seen++;
                if (seen < occurrence) continue;

                hit.Found = true;
                hit.Time = t[i];
                hit.From = cur == 1 ? 0 : 1;
                hit.To = cur;
                hit.Index = seen;
                return hit;
            }

            hit.Problem = seen == 0
                ? "이 로그에서는 그 IO 가 한 번도 바뀌지 않았습니다."
                : "변화가 " + seen + " 번뿐이라 " + occurrence + " 번째를 찾을 수 없습니다.";
            return hit;
        }

        /// <summary>
        /// 0 과 1 을 가르는 문턱값.
        ///
        /// 디지털과 상태 채널은 값 자체가 이미 번호라 0.5 면 충분합니다.
        /// 아날로그는 값 범위의 한가운데를 씁니다 — 4~20mA 든 0~10V 든
        /// "켜짐/꺼짐" 을 나누는 자리는 대체로 한가운데입니다.
        /// </summary>
        public static double Threshold(Channel c)
        {
            if (c == null) return 0.5;
            if (c.Kind != ChannelKind.Analog) return 0.5;
            if (double.IsNaN(c.Min) || double.IsNaN(c.Max)) return 0.5;
            if (c.Max - c.Min <= 0) return c.Max + 0.5;   // 움직이지 않는 채널
            return (c.Min + c.Max) * 0.5;
        }

        /// <summary>
        /// 두 로그를 그 IO 의 변화 순간으로 맞춥니다.
        /// </summary>
        /// <param name="before">이전 로그.</param>
        /// <param name="after">이후 로그.</param>
        /// <param name="ioName">기준으로 삼을 IO 이름. 양쪽에 다 있어야 합니다.</param>
        /// <param name="kind">어느 쪽으로 바뀐 것을 잡을지.</param>
        /// <param name="occurrence">몇 번째 변화인지. 1 이 첫 번째입니다.</param>
        public static AlignResult Compute(LogDataset before, LogDataset after,
                                          string ioName, EdgeKind kind, int occurrence)
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

            r.Before = FindEdge(before, bi, kind, occurrence);
            r.After = FindEdge(after, ai, kind, occurrence);

            if (!r.Before.Found || !r.After.Found)
            {
                r.Message = "\"" + ioName + "\" 로는 맞출 수 없습니다. "
                          + (!r.Before.Found ? "이전 로그: " + r.Before.Problem + " " : "")
                          + (!r.After.Found ? "이후 로그: " + r.After.Problem : "");
                return r;
            }

            r.Ok = true;
            r.Shift = r.Before.Time - r.After.Time;
            r.Message = "\"" + ioName + "\" 의 " + Describe(kind)
                      + (occurrence > 1 ? " " + occurrence + "번째" : "")
                      + " 로 맞췄습니다.   이전 " + before.FormatTime(r.Before.Time)
                      + "  ↔  이후 " + after.FormatTime(r.After.Time);
            return r;
        }

        public static string Describe(EdgeKind kind)
        {
            switch (kind)
            {
                case EdgeKind.Rising: return "0 → 1 변화";
                case EdgeKind.Falling: return "1 → 0 변화";
                default: return "변화";
            }
        }

        /// <summary>
        /// 이 채널로 맞출 수 있는지 미리 봅니다. 목록에서 고를 후보를
        /// 추리는 데 씁니다 — 한 번도 안 바뀌는 IO 는 기준이 될 수 없습니다.
        /// </summary>
        public static bool CanTrigger(LogDataset ds, int channel)
        {
            EdgeHit h = FindEdge(ds, channel, EdgeKind.Any, 1);
            return h.Found;
        }
    }
}
