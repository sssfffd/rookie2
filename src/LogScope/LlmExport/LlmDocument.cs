using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LogScope.Analysis2;
using LogScope.Common;

namespace LogScope.LlmExport
{
    /// <summary>
    /// <b>사내 LLM 에 보낼 종합 분석 입력 한 벌</b> (1.43).
    ///
    /// 쓰는 법 (서버로 보내는 코드를 따로 짤 때도 이 순서):
    /// <code>
    ///   LlmDocument doc = LlmDocument.Build(state, dbDiff, new LlmExportOptions(), progress);
    ///   string md   = doc.ToMarkdown();          // LLM 에 그대로 넣을 글
    ///   string json = doc.ToJsonText();          // 구조 그대로 (서버 코드가 섹션을 골라 쓰기 좋음)
    ///   doc.Save(folder);                        // llm_input_yyyyMMdd-HHmmss.md / .json 두 파일
    /// </code>
    ///
    /// 담기는 차례 (= Markdown 의 차례)
    /// <code>
    ///   # 종합 분석 입력          만든 때 · 프로그램 버전 · 층 · 글자 수
    ///   (요청 — 역할)            LlmPrompt.Intro
    ///   ## 분석 1   [analysis1]   Analysis1Digest
    ///   ## 분석 2   [analysis2]   Analysis2Digest
    ///   ## 분석 3   [analysis3]   Analysis3Digest
    ///   ## 분석 사이 연결 [links] CrossLink
    ///   (요청 — 할 일)           LlmPrompt.Request
    /// </code>
    ///
    /// <b>이 클래스는 파일만 만듭니다.</b> 네트워크로 보내는 코드는 없습니다 — 사내 서버 주소 · 인증은
    /// 이 프로그램 밖(사내 AI 가 짤 코드)에서 정합니다. 인증 정보(토큰 · 비밀번호)를 이 프로그램의 설정이나
    /// 코드에 적지 않는 것이 원칙입니다.
    ///
    /// 담기는 것은 IO 이름 · 표 이름 · 숫자 · DB 칸 값입니다. DB 칸 값에 민감한 내용이 있을 수 있으니
    /// 사내 규정상 보내도 되는지 확인하세요 (a2.rows · a2.values).
    /// </summary>
    public sealed class LlmDocument
    {
        public DateTime CreatedAt = DateTime.Now;
        public string ProgramVersion = string.Empty;
        public LlmExportOptions Options = new LlmExportOptions();
        public List<LlmPart> Parts = new List<LlmPart>();

        /// <summary>글자 수 한도로 줄을 줄였는지 (머리에 적음).</summary>
        public bool Trimmed;

        /// <summary>
        /// 지금 화면의 결과로 한 벌을 만듭니다. 배경 스레드에서 불러도 됩니다 (화면을 건드리지 않음).
        /// 분석 1 은 로그가 크면 수 초~수십 초 걸립니다 (히트맵과 같은 계산 두 번).
        /// </summary>
        public static LlmDocument Build(AppState state, DbDiffResult dbDiff, LlmExportOptions opt, LoadProgress prog)
        {
            var doc = new LlmDocument();
            if (opt != null) doc.Options = opt;
            doc.ProgramVersion = BuildInfo.Version;

            doc.Parts.Add(Analysis1Digest.Build(state, doc.Options, prog));
            doc.Parts.Add(Analysis2Digest.Build(state, dbDiff, doc.Options, prog));
            doc.Parts.Add(Analysis3Digest.Build());
            doc.Parts.Add(CrossLink.Build(state, dbDiff, doc.Options));

            if (prog != null) prog.Report("글자 수 맞추기", 0.97);
            doc.FitToBudget();
            return doc;
        }

        // ================= 글자 수 한도 =================

        /// <summary>
        /// Markdown 이 <see cref="LlmExportOptions.MaxChars"/> 를 넘으면, 3 층 → 2 층 순서로 줄이 가장 많은
        /// 섹션의 줄을 반씩 줄입니다. 1 층은 줄이지 않습니다 (결론의 근거). 줄인 섹션에는 "외 N 줄" 이 남습니다.
        /// 그래도 넘으면 그대로 둡니다 (1 층만으로 넘는 경우 — MaxLevel 이나 상한을 줄이세요).
        /// </summary>
        public void FitToBudget()
        {
            int max = Options.MaxChars;
            if (max <= 0) return;
            for (int guard = 0; guard < 200; guard++)
            {
                if (ToMarkdown().Length <= max) return;
                LlmSection big = Biggest(3) ?? Biggest(2);
                if (big == null) return;
                big.Cap(big.Rows.Count / 2);
                Trimmed = true;
            }
        }

        private LlmSection Biggest(int level)
        {
            LlmSection best = null;
            for (int p = 0; p < Parts.Count; p++)
                for (int i = 0; i < Parts[p].Sections.Count; i++)
                {
                    LlmSection s = Parts[p].Sections[i];
                    if (s.Level != level || s.Level > Options.MaxLevel || s.Rows.Count < 2) continue;
                    if (best == null || s.Rows.Count > best.Rows.Count) best = s;
                }
            return best;
        }

        // ================= 내보내기 =================

        public string ToMarkdown()
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("# 종합 분석 입력\n\n");
            sb.Append("- 만든 때: ").Append(CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("- 프로그램: LogScope ").Append(ProgramVersion).Append('\n');
            sb.Append("- 담은 층: L1 ~ L").Append(Options.MaxLevel.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (Trimmed) sb.Append("- 글자 수 한도(").Append(Options.MaxChars.ToString(CultureInfo.InvariantCulture))
                           .Append(")로 일부 표의 줄을 줄였습니다 ('… 외 N 줄').\n");
            sb.Append('\n').Append(LlmPrompt.Intro).Append("\n\n");

            for (int i = 0; i < Parts.Count; i++) Parts[i].WriteMarkdown(sb, Options.MaxLevel);

            sb.Append("## 요청\n\n").Append(LlmPrompt.Request).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// JSON 모양:
        /// <code>
        /// { "format": "logscope-llm-input", "formatVersion": 1, "createdAt": "...", "programVersion": "1.43",
        ///   "maxLevel": 3, "trimmed": false, "intro": "...", "request": "...",
        ///   "parts": [ { "id": "analysis1", "title": "...", "missing": "", "sections": [ LlmSection.ToJson(), ... ] }, ... ] }
        /// </code>
        /// 서버 코드는 parts[].id 와 sections[].id 로 필요한 것만 골라 보내도 됩니다.
        /// </summary>
        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["format"] = "logscope-llm-input";
            d["formatVersion"] = 1.0;
            d["createdAt"] = CreatedAt.ToString("o", CultureInfo.InvariantCulture);
            d["programVersion"] = ProgramVersion;
            d["maxLevel"] = (double)Options.MaxLevel;
            d["trimmed"] = Trimmed;
            d["intro"] = LlmPrompt.Intro;
            d["request"] = LlmPrompt.Request;
            var parts = new List<object>();
            for (int i = 0; i < Parts.Count; i++) parts.Add(Parts[i].ToJson(Options.MaxLevel));
            d["parts"] = parts;
            return d;
        }

        public string ToJsonText() { return Json.Write(ToJson()); }

        /// <summary>
        /// 폴더에 두 파일을 씁니다 — llm_input_시각.md 와 .json (UTF-8, BOM 없음). 만든 .md 경로를 돌려줍니다.
        /// 같은 이름이 있으면 덮어쓰지 않고 뒤에 -2, -3 을 붙입니다.
        /// </summary>
        public string Save(string folder)
        {
            Directory.CreateDirectory(folder);
            string stem = "llm_input_" + CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string md = Path.Combine(folder, stem + ".md");
            for (int i = 2; File.Exists(md) && i < 1000; i++) md = Path.Combine(folder, stem + "-" + i + ".md");
            string json = Path.ChangeExtension(md, ".json");

            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(md, ToMarkdown(), utf8);
            File.WriteAllText(json, ToJsonText(), utf8);
            return md;
        }
    }
}
