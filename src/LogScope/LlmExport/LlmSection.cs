using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LogScope.LlmExport
{
    // =====================================================================
    //  사내 LLM 에 보낼 "종합 분석 입력" 의 기본 단위 (1.43)
    //
    //  전체 모양
    //    LlmDocument            문서 한 벌 (파일 하나 = 이것 하나)
    //      └ LlmPart            분석 하나 — "analysis1" · "analysis2" · "analysis3" · "links"
    //          └ LlmSection     표 하나 (또는 "이름 = 값" 목록 하나)
    //
    //  LlmSection 은 두 가지를 담습니다.
    //    Facts    "이름 = 값" 몇 줄 (머리 정보: 파일 이름 · 판정 · 기준 …)
    //    표       Columns (열 이름) + Rows (줄마다 칸 배열)
    //  둘 다 있어도 되고 하나만 있어도 됩니다.
    //
    //  같은 내용을 두 모양으로 내보냅니다.
    //    Markdown (llm_input.md)   사람이 읽고, LLM 에 그대로 붙여 넣는 글.
    //                              표는 탭(\t)으로 칸을 가른 TSV — JSON 보다 30~50 % 짧습니다.
    //    JSON     (llm_input.json) 서버로 보내는 코드가 읽기 쉬운 구조.
    //                              섹션마다 id 가 있어 필요한 것만 골라 보낼 수 있습니다.
    //
    //  Level (층) — 중요한 것부터 쌓습니다. 글자 수 한도를 넘으면 높은 층부터 줄을 줄입니다.
    //    1  꼭 필요 (판정 · 달라진 것 목록)
    //    2  근거 (단계 · 시간대 · 사건 · 표별 수)
    //    3  자세히 (곡선 모양 · 추가/삭제된 행 키)
    //
    //  WPF 를 쓰지 않습니다 — 화면 없이 시험이 닿고, 서버 쪽 코드에서도 그대로 쓸 수 있습니다.
    // =====================================================================

    /// <summary>표 하나 (또는 "이름 = 값" 목록 하나). 위 머리 주석 참고.</summary>
    public sealed class LlmSection
    {
        /// <summary>
        /// 찾는 열쇠. "a1.ios" 처럼 "분석.무엇". 서버로 보내는 코드가 이 값으로 섹션을 고릅니다.
        /// <b>바꾸면 그 코드가 깨집니다</b> — 이름(Title)은 바꿔도 되지만 Id 는 그대로 두세요.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>사람이 읽는 제목 ("달라진 IO").</summary>
        public string Title = string.Empty;

        /// <summary>층 — 1 꼭 · 2 근거 · 3 자세히.</summary>
        public int Level = 1;

        /// <summary>표 위 한두 줄 설명 (열의 뜻 · 단위 · 대략인 값이면 그 사실). LLM 이 표를 바로 읽게.</summary>
        public string Note = string.Empty;

        /// <summary>"이름 = 값" 목록. 차례가 그대로 나갑니다.</summary>
        public List<KeyValuePair<string, string>> Facts = new List<KeyValuePair<string, string>>();

        /// <summary>표의 열 이름. null 이면 표가 없는 섹션.</summary>
        public string[] Columns;

        /// <summary>표의 줄. 줄마다 Columns 와 같은 수의 칸.</summary>
        public List<string[]> Rows = new List<string[]>();

        /// <summary>
        /// 잘리기 전 전체 줄 수. Rows 가 이보다 적으면 "외 N 줄" 을 적습니다 —
        /// LLM 이 "목록이 이게 전부" 로 읽지 않게 하는 것이 중요합니다.
        /// </summary>
        public int TotalRows;

        public LlmSection(string id, string title, int level)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Level = level;
        }

        /// <summary>잘려 나간 줄 수.</summary>
        public int OmittedRows { get { return Math.Max(0, TotalRows - Rows.Count); } }

        public bool IsEmpty { get { return Facts.Count == 0 && Rows.Count == 0 && TotalRows == 0; } }

        public void AddFact(string name, string value)
        {
            Facts.Add(new KeyValuePair<string, string>(name ?? string.Empty, value ?? string.Empty));
        }

        /// <summary>표 줄 하나. 칸 수가 열 수와 다르면 모자란 칸은 빈 글자로 채웁니다.</summary>
        public void AddRow(params string[] cells)
        {
            int n = Columns != null ? Columns.Length : (cells != null ? cells.Length : 0);
            var row = new string[n];
            for (int i = 0; i < n; i++)
                row[i] = cells != null && i < cells.Length && cells[i] != null ? cells[i] : string.Empty;
            Rows.Add(row);
            if (TotalRows < Rows.Count) TotalRows = Rows.Count;
        }

        /// <summary>앞에서 max 줄만 남깁니다 (TotalRows 는 그대로 — "외 N 줄" 이 됨).</summary>
        public void Cap(int max)
        {
            if (TotalRows < Rows.Count) TotalRows = Rows.Count;
            if (max < 0) max = 0;
            if (Rows.Count > max) Rows.RemoveRange(max, Rows.Count - max);
        }

        // ---------------- Markdown (TSV) ----------------

        /// <summary>
        /// Markdown 으로 적습니다.
        ///
        ///   ### 제목  [L1 · a1.ios]
        ///   설명 한 줄
        ///   - 이름: 값
        ///   열1	열2	열3          ← 탭으로 가름 (TSV)
        ///   값	값	값
        ///   … 외 12 줄
        /// </summary>
        public void WriteMarkdown(StringBuilder sb)
        {
            sb.Append("### ").Append(Title).Append("  [L").Append(Level.ToString(CultureInfo.InvariantCulture))
              .Append(" · ").Append(Id).Append("]\n");
            if (Note.Length > 0) sb.Append(Note).Append('\n');
            for (int i = 0; i < Facts.Count; i++)
                sb.Append("- ").Append(Facts[i].Key).Append(": ").Append(OneLine(Facts[i].Value)).Append('\n');

            if (Columns != null && (Rows.Count > 0 || TotalRows > 0))
            {
                sb.Append('\n');
                sb.Append(string.Join("\t", Clean(Columns))).Append('\n');
                for (int i = 0; i < Rows.Count; i++) sb.Append(string.Join("\t", Clean(Rows[i]))).Append('\n');
                if (OmittedRows > 0)
                    sb.Append("… 외 ").Append(OmittedRows.ToString("N0", CultureInfo.InvariantCulture))
                      .Append(" 줄 (글자 수 한도로 뺌)\n");
            }
            else if (Columns != null)
            {
                sb.Append("(없음)\n");
            }
            sb.Append('\n');
        }

        /// <summary>칸 안의 탭 · 줄바꿈을 빈칸으로 — TSV 의 칸 · 줄이 어긋나지 않게.</summary>
        private static string[] Clean(string[] cells)
        {
            var r = new string[cells.Length];
            for (int i = 0; i < cells.Length; i++) r[i] = OneLine(cells[i]);
            return r;
        }

        internal static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        }

        // ---------------- JSON ----------------

        /// <summary>
        /// JSON 모양:
        /// <code>
        /// { "id": "a1.ios", "title": "달라진 IO", "level": 1, "note": "...",
        ///   "facts": { "이름": "값", ... },
        ///   "columns": ["IO", "종류", ...],
        ///   "rows": [["PRES_MAIN", "아날로그", ...], ...],
        ///   "totalRows": 152, "omittedRows": 0 }
        /// </code>
        /// facts 는 차례가 중요할 수 있어 [이름, 값] 쌍의 배열로도 같이 넣습니다 ("factList").
        /// </summary>
        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["id"] = Id;
            d["title"] = Title;
            d["level"] = (double)Level;
            d["note"] = Note;

            var facts = new Dictionary<string, object>(StringComparer.Ordinal);
            var factList = new List<object>();
            for (int i = 0; i < Facts.Count; i++)
            {
                facts[Facts[i].Key] = Facts[i].Value;
                factList.Add(new List<object> { Facts[i].Key, Facts[i].Value });
            }
            d["facts"] = facts;
            d["factList"] = factList;

            if (Columns != null)
            {
                d["columns"] = new List<object>(Columns);
                var rows = new List<object>();
                for (int i = 0; i < Rows.Count; i++) rows.Add(new List<object>(Rows[i]));
                d["rows"] = rows;
            }
            d["totalRows"] = (double)TotalRows;
            d["omittedRows"] = (double)OmittedRows;
            return d;
        }
    }

    /// <summary>분석 하나 (또는 분석 사이 연결) — 섹션 여럿.</summary>
    public sealed class LlmPart
    {
        /// <summary>"analysis1" · "analysis2" · "analysis3" · "links". 서버 코드가 고르는 열쇠.</summary>
        public string Id = string.Empty;

        /// <summary>제목 (config.txt 의 분석 이름 등).</summary>
        public string Title = string.Empty;

        /// <summary>
        /// 이 분석을 못 담은 까닭 ("로그를 아직 비교하지 않았습니다" 등). 비었으면 담겼음.
        /// LLM 이 "없음" 과 "괜찮음" 을 헷갈리지 않게 꼭 적습니다.
        /// </summary>
        public string Missing = string.Empty;

        public List<LlmSection> Sections = new List<LlmSection>();

        public LlmPart(string id, string title)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
        }

        public LlmSection Add(LlmSection s)
        {
            if (s != null) Sections.Add(s);
            return s;
        }

        public LlmSection Find(string id)
        {
            for (int i = 0; i < Sections.Count; i++) if (Sections[i].Id == id) return Sections[i];
            return null;
        }

        public void WriteMarkdown(StringBuilder sb, int maxLevel)
        {
            sb.Append("## ").Append(Title).Append("  [").Append(Id).Append("]\n\n");
            if (Missing.Length > 0) sb.Append("**담지 못함:** ").Append(Missing).Append("\n\n");
            for (int i = 0; i < Sections.Count; i++)
                if (Sections[i].Level <= maxLevel) Sections[i].WriteMarkdown(sb);
        }

        public Dictionary<string, object> ToJson(int maxLevel)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["id"] = Id;
            d["title"] = Title;
            d["missing"] = Missing;
            var list = new List<object>();
            for (int i = 0; i < Sections.Count; i++)
                if (Sections[i].Level <= maxLevel) list.Add(Sections[i].ToJson());
            d["sections"] = list;
            return d;
        }
    }
}
