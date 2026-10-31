using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LogScope.Analysis1.LogFile;
using LogScope.Common;

namespace LogScope.Analysis1.Judge
{
    /// <summary>
    /// 분석 1 정상 판정을 <b>LLM 에 넣을 글</b>로 (1.62). 허용 안 된 오차와 <b>사용자가 허용한 오차</b>를 따로
    /// 적어서, LLM 이 허용한 것을 이상으로 읽지 않게 합니다. LlmRequest.Analysis1Judgement 로 넘어갑니다.
    ///
    /// Markdown 표입니다 (사내 LLM 이 표를 잘 읽음). 줄이 너무 많으면 큰 것부터 <see cref="MaxRows"/> 줄까지.
    /// </summary>
    public static class JudgeText
    {
        /// <summary>표 하나에 싣는 줄 상한.</summary>
        public const int MaxRows = 300;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string ForLlm(JudgeResult r, LogDataset before)
        {
            if (r == null) return string.Empty;
            var sb = new StringBuilder(8 * 1024);
            sb.Append("## 분석 1 정상 판정 (이전 로그 = 정상 기준)\n\n");
            sb.Append("- 판정: **").Append(r.Word).Append("** — 허용 안 된 오차 ").Append(r.OpenCount.ToString("N0"))
              .Append(" 개, 사용자가 허용한 오차 ").Append(r.AllowedCount.ToString("N0")).Append(" 개\n");
            sb.Append("- 비교 방법: ").Append(r.ByStep ? "단계마다 시작을 맞춤 (단계 " + r.StepCount + " 개)" : "단계 없이 전체를 한 구간으로")
              .Append(". 이후 값이 이전 로그의 앞뒤 시간 여유(단계 길이 2 %, 0.2~5 초) 동안의 값 범위 + IO 값 허용 오차 안이면 같음. ")
              .Append("양쪽(이전→이후, 이후→이전)으로 봄. 한 단계에서 오차 시간이 20 % 이상이면 FAIL, 그 밖의 오차는 WARNING, ")
              .Append("단계가 없어지거나 차례가 바뀌면 FAIL.\n");
            if (r.Note.Length > 0) sb.Append("- 알아 둘 점: ").Append(r.Note).Append('\n');
            sb.Append("- **사용자가 허용한 오차는 공정 담당자가 정상 변동으로 판단한 것입니다. 이상의 근거로 쓰지 마세요.**\n\n");

            var open = new List<JudgeItem>();
            var allowed = new List<JudgeItem>();
            foreach (JudgeItem it in r.Items) (it.IsAllowed ? allowed : open).Add(it);

            sb.Append("### 허용 안 된 오차 (판정에 들어감)\n\n");
            Table(sb, r, before, open, false);
            sb.Append("\n### 사용자가 허용한 오차 (정상으로 판단됨 — 판정에서 뺌)\n\n");
            Table(sb, r, before, allowed, true);
            return sb.ToString();
        }

        private static void Table(StringBuilder sb, JudgeResult r, LogDataset before, List<JudgeItem> items, bool allowed)
        {
            if (items.Count == 0) { sb.Append("없음\n"); return; }
            sb.Append(allowed ? "| 허용 | 종류 | IO | 단계 | 시작 | 길이 | 크기 | 설명 |\n|---|---|---|---|---|---|---|---|\n"
                              : "| 판정 | 종류 | IO | 단계 | 시작 | 길이 | 크기 | 설명 |\n|---|---|---|---|---|---|---|---|\n");
            int n = Math.Min(MaxRows, items.Count);
            for (int i = 0; i < n; i++)
            {
                JudgeItem it = items[i];
                string first = allowed ? (it.Allowed == AllowState.Rule ? "앞으로도 (규칙)" : "이번만")
                                       : JudgeResult.WordOf(it.Grade);
                string at = before != null ? before.FormatTime(it.Start) : NumberText.Plain(it.Start);
                string size = it.Kind == JudgeKind.Value
                    ? NumberText.Plain(it.MaxExcess) + (double.IsNaN(it.MaxExcessPercent) ? string.Empty : " (" + NumberText.Plain(it.MaxExcessPercent) + " %)")
                    : it.Kind == JudgeKind.StepLength ? (it.MaxExcess > 0 ? "+" : "-") + r.Duration(Math.Abs(it.MaxExcess)) : "-";
                string dur = it.Kind == JudgeKind.Value ? r.Duration(it.Duration) : "-";
                string what = it.Kind == JudgeKind.Value ? it.Detail : it.Detail.Length > 0 ? it.Detail : it.What;
                sb.Append("| ").Append(first)
                  .Append(" | ").Append(KindText(it.Kind))
                  .Append(" | ").Append(Cell(it.Io))
                  .Append(" | ").Append(Cell(it.Step))
                  .Append(" | ").Append(at)
                  .Append(" | ").Append(dur)
                  .Append(" | ").Append(size)
                  .Append(" | ").Append(Cell(what))
                  .Append(" |\n");
            }
            if (items.Count > n) sb.Append("\n외 ").Append((items.Count - n).ToString("N0")).Append(" 개 (큰 것부터 실음)\n");
        }

        private static string KindText(JudgeKind k)
        {
            switch (k)
            {
                case JudgeKind.Value: return "값";
                case JudgeKind.StepLength: return "단계 길이";
                case JudgeKind.StepMissing: return "단계 없어짐";
                case JudgeKind.StepExtra: return "단계 생김";
                case JudgeKind.StepOrder: return "단계 차례";
                case JudgeKind.IoMissing: return "IO 없어짐";
                case JudgeKind.IoExtra: return "IO 생김";
                default: return k.ToString();
            }
        }

        /// <summary>표 칸 — 세로줄 · 줄바꿈이 표를 깨지 않게.</summary>
        private static string Cell(string s)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            return s.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
