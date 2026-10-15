using System;
using System.Collections.Generic;
using LogScope.Core.Model;

namespace LogScope.Core.Io
{
    /// <summary>
    /// 로그에서 <b>단계</b>를 찾아 넣는 곳. 로그를 다 읽은 바로 뒤에
    /// <see cref="LogReader"/> 가 한 번 부릅니다.
    ///
    /// <b>단계를 넣는 자리는 여기 하나입니다.</b> 화면도 히트맵도 그래프도
    /// <c>ds.Steps</c> 만 보므로, 로그 모양이 달라 손으로 파싱해야 하면
    /// 아래 2번 자리만 채우면 그 뒤는 전부 따라옵니다.
    ///
    /// 넣는 길이 둘입니다.
    ///   1. <b>단계를 담은 IO 가 로그 안에 있는 경우</b> — 설정에 그 IO
    ///      이름만 적으면 됩니다 (설정 창의 [단계] 칸). 코딩할 것이 없습니다.
    ///   2. <b>그 밖의 모양</b> — 아래 <c>Mine</c> 를 채웁니다.
    /// </summary>
    public static class StepRead
    {
        /// <summary>
        /// 단계를 찾아 <c>ds.Steps</c> 에 넣습니다. 못 찾으면 빈 표가
        /// 그대로 남고 <c>ds.StepNote</c> 에 이유가 적힙니다 — <b>짐작해서
        /// 아무 단계나 만들지 않습니다.</b> 단계가 틀리면 그 뒤의 히트맵과
        /// 그래프가 전부 틀리는데, 그림만 봐서는 맞는지 알 수가 없습니다.
        /// </summary>
        public static void Fill(LogDataset ds, OpenOptions opt)
        {
            if (ds == null) return;

            // ---- 2. 직접 파싱한 단계가 있으면 그것을 먼저 씁니다 ----
            //
            // 설정보다 앞에 둡니다. 직접 넣었다는 것은 로그 모양이
            // 특별해서인데, 그걸 설정의 IO 이름이 덮으면 왜 안 들어가는지
            // 찾기 어렵습니다.
            StepTable mine = Mine(ds);
            if (mine != null && !mine.IsEmpty)
            {
                ds.Steps = mine;
                ds.StepNote = mine.Note;
                return;
            }

            // ---- 1. 설정에 적힌 IO 로 ----
            string io = opt != null ? (opt.StepIo ?? string.Empty) : string.Empty;
            if (io.Length == 0)
            {
                ds.Steps = StepTable.None;
                ds.StepNote = string.Empty;
                return;
            }

            string problem;
            StepTable t = StepTable.FromChannel(ds, io, out problem);
            ds.Steps = t;
            ds.StepNote = problem.Length > 0 ? problem : t.Note;
        }

        /// <summary>
        /// ↓↓↓ <b>직접 파싱해 넣는 자리입니다.</b> ↓↓↓
        ///
        /// 지금은 아무것도 넣지 않습니다 (null). 로그에서 단계를 읽어
        /// <see cref="StepTable"/> 을 돌려주면 그 뒤는 다 따라옵니다.
        ///
        /// 쓸 수 있는 길이 셋입니다. 어느 쪽으로 넣어도 차례 세우기·겹침
        /// 밀기·상한 자르기는 <c>StepTable</c> 이 똑같이 해 줍니다.
        ///
        /// <b>가) 표본마다 단계 이름이 있는 경우</b> (가장 흔합니다)
        /// <code>
        ///   int ch = ds.FindChannel("STEP_NAME");
        ///   if (ch &lt; 0) return null;
        ///   var names = new string[ds.SampleCount];
        ///   Channel c = ds.Channels[ch];
        ///   for (int i = 0; i &lt; names.Length; i++)
        ///       names[i] = c.TextAt(i);           // 빈 글자 = 단계 밖
        ///   return StepTable.FromLabels(ds, names, "STEP_NAME 글자");
        /// </code>
        ///
        /// <b>나) 구간을 따로 적어 둔 경우</b> (단계 시작 시각 목록 등)
        /// <code>
        ///   var list = new List&lt;LogStep&gt;();
        ///   list.Add(new LogStep("준비", 0, 1199));          // 표본 번호
        ///   list.Add(new LogStep("가열", 1200, 4999));
        ///   return StepTable.FromRanges(ds, list, "내 파서");
        /// </code>
        /// 시각만 알고 표본 번호를 모르면 <c>ds.IndexAt(시각)</c> 으로
        /// 바꿉니다 — 시간 격자가 고르지 않아도 맞습니다.
        ///
        /// <b>다) 단계 번호가 숫자 IO 로 들어 있는 경우</b>
        /// <code>
        ///   string problem;
        ///   return StepTable.FromChannel(ds, "STEP_NO", out problem);
        /// </code>
        ///
        /// 돌려줄 것이 없으면 null 을 돌려주세요 — 그러면 설정에 적은
        /// IO 로 넘어갑니다.
        /// </summary>
        private static StepTable Mine(LogDataset ds)
        {
            // 예시 한 줄 (지웠다 켜 보세요):
            //   return StepTable.FromChannel(ds, "STEP_NO", out _);   // C# 7.0 이상
            return null;
        }

        /// <summary>
        /// 시각 목록을 표본 번호 구간으로 바꿉니다. 단계 시작 시각만 아는
        /// 경우에 씁니다 — 마지막 단계는 로그 끝까지입니다.
        ///
        /// 직접 파싱하는 쪽에서 쓰라고 내어 둔 도구입니다. 시간 격자가
        /// 고르지 않아도 맞습니다 (이진 탐색으로 찾습니다).
        /// </summary>
        public static List<LogStep> RangesFromStarts(LogDataset ds,
                                                     IList<string> names,
                                                     IList<double> startTimes)
        {
            var list = new List<LogStep>();
            if (ds == null || names == null || startTimes == null) return list;
            int n = Math.Min(names.Count, startTimes.Count);
            if (n == 0 || ds.SampleCount == 0) return list;

            for (int i = 0; i < n; i++)
            {
                int first = ds.IndexAt(startTimes[i]);
                if (first < 0) continue;

                int last;
                if (i + 1 < n)
                {
                    int next = ds.IndexAt(startTimes[i + 1]);
                    last = next > first ? next - 1 : first;
                }
                else last = ds.SampleCount - 1;

                list.Add(new LogStep(names[i], first, last));
            }
            return list;
        }
    }
}
