using System;
using System.Collections.Generic;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.LogFile;
using LogScope.Analysis2;
using LogScope.Common;

namespace LogScope.LlmExport
{
    /// <summary>
    /// <b>분석 1 ↔ 분석 2 연결</b> — 같은 IO 이름이 양쪽에 나오는 것을 프로그램이 미리 짝지어 줍니다 (1.43).
    ///
    /// 왜 LLM 에게 맡기지 않나: 수천 줄짜리 두 표에서 이름을 맞추게 하면 빠뜨리거나 비슷한 이름을 잘못
    /// 짝짓습니다. 이름 맞추기는 결정적인 일이라 여기서 하고, LLM 은 "설정이 바뀌어 동작이 달라졌나" 를
    /// 해석만 하게 합니다.
    ///
    /// 맞추는 규칙: 분석 1 이 쓰는 느슨한 비교 (<see cref="LogDataset.LooseKey"/> — 대소문자 · 공백 · 구분 기호 무시).
    /// 이름이 다른데 같은 IO (예: DB 는 PRES_SET, 로그는 PRES_MAIN) 는 못 맞춥니다 — 그런 짝이 많으면
    /// 별칭 표를 더하는 자리가 여기입니다 (<see cref="Build"/> 의 key 계산).
    ///
    ///   links.io   L1  IO · 분석 2 의 값 변화 · 분석 1 의 동작 (달라짐이면 지표, 아니면 "안 바뀜")
    /// </summary>
    public static class CrossLink
    {
        public static LlmPart Build(AppState state, DbDiffResult diff, LlmExportOptions opt)
        {
            var part = new LlmPart("links", "분석 사이 연결");
            CompareResult cmp = state != null ? state.Comparison : null;
            if (cmp == null || diff == null || state == null || !state.HasDbResult)
            {
                part.Missing = "분석 1 과 분석 2 결과가 둘 다 있어야 같은 IO 를 짝지을 수 있습니다.";
                return part;
            }

            // 분석 1: 느슨한 이름 → 그 IO 의 차이
            var a1 = new Dictionary<string, ChannelDiff>(StringComparer.Ordinal);
            for (int i = 0; i < cmp.Items.Count; i++)
            {
                string k = LogDataset.LooseKey(cmp.Items[i].Name);
                if (!a1.ContainsKey(k)) a1[k] = cmp.Items[i];
            }

            var o = new DbDiffList.Options();
            o.ExpandWholeRows = false;
            List<DbChangedIo> a2 = DbDiffList.ChangedValues(DbDiffList.Build(diff, o), int.MaxValue);

            var s = new LlmSection("links.io", "분석 1 ↔ 분석 2 같은 IO", 1);
            s.Note = "분석 2 에서 값이 바뀐 IO 가 분석 1 로그에도 있으면 한 줄. 분석1 = 그 IO 가 로그에서 달라졌는지와 지표. "
                   + "'안 바뀜' 도 근거입니다 (설정은 바뀌었는데 동작은 같음).";
            s.Columns = new[] { "IO", "분석2 (표.열: 이전 → 이후)", "분석1", "분석1 최대차%", "분석1 차이시간%" };
            int matched = 0;
            for (int i = 0; i < a2.Count; i++)
            {
                ChannelDiff d;
                if (!a1.TryGetValue(LogDataset.LooseKey(a2[i].Io), out d)) continue;
                matched++;
                string what = a2[i].Table + "." + a2[i].Column + ": " + a2[i].Before + " → " + a2[i].After;
                if (a2[i].Others != null && a2[i].Others.Count > 0) what += " (외 " + a2[i].Others.Count + " 열)";
                s.AddRow(d.Name, what, d.Changed ? "달라짐" : "안 바뀜",
                         d.Changed ? LlmFormat.Num(d.MaxPercent) : string.Empty,
                         d.Changed ? LlmFormat.Pct(d.TimeRatio) : string.Empty);
            }
            s.AddFact("분석 2 값 변화 IO", LlmFormat.Int(a2.Count));
            s.AddFact("그중 분석 1 로그에도 있는 IO", LlmFormat.Int(matched));
            if (matched == 0) s.Columns = null;
            s.Cap(opt != null ? opt.MaxDbIoChanges : int.MaxValue);
            part.Add(s);
            return part;
        }
    }
}
