using System;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.LlmExport
{
    /// <summary>
    /// <b>분석 3</b> — 분석 3 화면이 메인 화면에 올린 결과(<see cref="Analysis3Link.Result"/>)를 그대로 표로 (1.43).
    ///
    /// 분석 3 은 사용자 폴더(Analysis3)의 코드라 무엇을 담는지 이 프로그램은 모릅니다. 그래서 메인 화면 칸에 올린
    /// 것(낱말 · 점수 · 개수 · 요약 줄)만 넣습니다. <b>분석 3 의 자세한 결과를 서버로 보내는 코드는 분석 3 쪽에서
    /// 따로</b> 붙이면 됩니다 — 이 섹션의 Id(a3.head · a3.lines)는 그대로 두고, 같은 모양(LlmSection)으로
    /// 섹션을 더하면 Markdown · JSON 에 같이 나갑니다 (README.md 참고).
    ///
    ///   a3.head   L1  이전 · 이후 자리 · 낱말 · 점수 · 개수 · 작은 글
    ///   a3.lines  L1  요약 줄 (꼬리표 [오류] 등 · 내용 · 값 · 색 · 덧글)
    /// </summary>
    public static class Analysis3Digest
    {
        public static LlmPart Build()
        {
            var part = new LlmPart("analysis3", AppConfig.Current.Title(3));
            Analysis3Result r = Analysis3Link.Result;

            var head = new LlmSection("a3.head", "분석 3 — 요약", 1);
            head.AddFact("이전 자리", DbPathTextName(Analysis3Link.BeforePath));
            head.AddFact("이후 자리", DbPathTextName(Analysis3Link.AfterPath));
            part.Add(head);

            if (r == null)
            {
                part.Missing = "분석 3 이 아직 결과를 올리지 않았습니다 (Analysis3Link.Publish). 분석 3 은 판단에서 빼 주세요.";
                return part;
            }

            if (!string.IsNullOrEmpty(r.Verdict)) head.AddFact("판정", r.Verdict);
            if (!double.IsNaN(r.Score)) head.AddFact("점수 (0~100)", LlmFormat.Num(r.Score));
            if (!string.IsNullOrEmpty(r.Count)) head.AddFact("차이 발생", r.Count);
            if (!string.IsNullOrEmpty(r.Note)) head.AddFact("덧글", r.Note);

            var lines = new LlmSection("a3.lines", "분석 3 — 요약 줄", 1);
            lines.Note = "분석 3 이 메인 화면에 올린 요약 줄 그대로. 색: 빨강 = 문제, 노랑 = 주의, 초록 = 정상.";
            lines.Columns = new[] { "내용", "값", "색", "덧글" };
            for (int i = 0; r.Lines != null && i < r.Lines.Count; i++)
            {
                Analysis3Line l = r.Lines[i];
                if (l == null) continue;
                lines.AddRow(l.Label, l.Value, GradeWord(l.Grade, l.Label), l.Detail);
            }
            if (lines.TotalRows == 0) { lines.Columns = null; lines.AddFact("결과", string.IsNullOrEmpty(r.EmptyText) ? "요약 줄 없음" : r.EmptyText); }
            part.Add(lines);
            return part;
        }

        private static string GradeWord(Analysis3Grade g, string label)
        {
            switch (g)
            {
                case Analysis3Grade.Bad: return "빨강";
                case Analysis3Grade.Warn: return "노랑";
                case Analysis3Grade.Good: return "초록";
            }
            // 색을 안 정했으면 메인 화면처럼 꼬리표([오류] 등)로 고릅니다.
            string tag = LogScope.Main.CardLineVm.GradeOfTag(LogScope.Main.CardLineVm.TagOf(label));
            if (tag == LogScope.Main.ScoreBands.GradeBad) return "빨강";
            if (tag == LogScope.Main.ScoreBands.GradeWarn) return "노랑";
            if (tag == LogScope.Main.ScoreBands.GradeGood) return "초록";
            return string.Empty;
        }

        private static string DbPathTextName(string path)
        {
            return LogScope.Analysis2.DbPathText.Name(path);
        }
    }
}
