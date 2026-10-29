using System;
using System.Collections.Generic;
using LogScope.Analysis2;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.LlmExport
{
    /// <summary>
    /// <b>분석 2 (DB 비교)</b> 를 LLM 이 읽을 표로 줄입니다 (1.43).
    ///
    /// DB 덤프는 수백만 자라도 대부분 "같은 행" 입니다. 같은 것은 <b>개수만</b>, 다른 것은
    /// <b>바뀐 칸만</b> 줍니다. 분석 2 화면의 목록과 같은 계산(<see cref="DbDiffList.Build"/>)을 다시 불러 써서
    /// 화면과 수가 같습니다.
    ///
    /// 만드는 섹션 (Id · 층 · 내용)
    /// <code>
    ///   a2.head     L1  견준 자리 · 판정(PASS/WARNING/FAIL) · 차이 발생 IO 수 · 표 수
    ///   a2.values   L1  IO 값 변화 전부 — 표 · IO · 열 · 이전 → 이후 (+ 같은 IO 의 다른 열)
    ///                   ← 메인 화면 분석 2 요약 칸과 같은 것. 사람이 제일 먼저 보는 것.
    ///   a2.schema   L1  구조 변화 — 열 추가 · 삭제 · 이름 바뀜(짐작) · 타입 · NULL · 기본값 · 자리
    ///   a2.tables   L2  표마다 생긴 · 없어진 · 바뀐 · 같은 행 수
    ///   a2.rows     L2  바뀐 행의 <b>바뀐 칸만</b> — 표 · 키 · IO · 열 · 이전 · 이후 (표마다 상한)
    ///   a2.notes    L2  읽기 · 짝짓기 알림 (키가 없어 짐작으로 짝지었다 등) — 결과를 얼마나 믿을지의 근거
    ///   a2.keys     L3  생긴 · 없어진 행의 키만 (표마다 상한)
    /// </code>
    /// 프로그램은 DB 에 접속하거나 SQL 을 실행하지 않습니다 — 파일(덤프 · CSV · frm)을 읽은 결과뿐입니다.
    /// </summary>
    public static class Analysis2Digest
    {
        public static LlmPart Build(AppState state, DbDiffResult diff, LlmExportOptions opt, LoadProgress prog)
        {
            if (opt == null) opt = new LlmExportOptions();
            var part = new LlmPart("analysis2", AppConfig.Current.Title(2));
            if (diff == null || state == null || !state.HasDbResult)
            {
                part.Missing = "이전 · 이후 DB 를 읽어 견준 결과가 없습니다. 분석 2 는 판단에서 빼 주세요.";
                return part;
            }
            if (prog != null) { prog.ThrowIfCancelled(); prog.Report("분석 2 — DB 차이 목록", 0.90); }

            // 화면 목록과 같은 줄 — 단, 생기거나 없어진 행은 열마다 펼치지 않고 한 줄로 (키만 보내려고).
            var o = new DbDiffList.Options();
            o.ExpandWholeRows = false;
            o.IncludeSame = false;
            List<DbDiffLine> lines = DbDiffList.Build(diff, o);

            part.Add(Head(state, diff));
            part.Add(Values(lines, opt));
            part.Add(Schema(lines));
            if (opt.MaxLevel >= 2) part.Add(Tables(diff));
            if (opt.MaxLevel >= 2) part.Add(Rows(lines, opt));
            if (opt.MaxLevel >= 2) part.Add(Notes(lines, diff));
            if (opt.MaxLevel >= 3) part.Add(Keys(lines, opt));
            part.Sections.RemoveAll(s => s == null);
            return part;
        }

        // ================= a2.head =================

        private static LlmSection Head(AppState state, DbDiffResult diff)
        {
            var s = new LlmSection("a2.head", "분석 2 — 요약", 1);
            s.Note = "이전 · 이후 DB(덤프 파일 · 폴더)를 표 · 행 · 열 단위로 견준 결과. "
                   + "판정: 값이 달라진 IO 가 0 이면 PASS, " + DbVerdict.FailFrom + " 개 이상이면 FAIL, 그 사이 WARNING (메인 화면과 같은 규칙).";
            AppSettings st = state.Settings ?? new AppSettings();
            s.AddFact("이전 자리", DbPathText.Name(st.DbBeforePath));
            s.AddFact("이후 자리", DbPathText.Name(st.DbAfterPath));
            s.AddFact("판정", DbVerdict.Of(state.DbChanged));
            s.AddFact("값이 달라진 IO", LlmFormat.Int(state.DbChanged));
            s.AddFact("없어진 IO (행)", LlmFormat.Int(state.DbGone));
            s.AddFact("새로 생긴 IO (행)", LlmFormat.Int(state.DbAdded));
            s.AddFact("표", "바뀜 " + LlmFormat.Int(diff.TablesChanged) + " · 같음 " + LlmFormat.Int(diff.TablesSame)
                      + " · 이전에만 " + LlmFormat.Int(diff.TablesOnlyBefore) + " · 이후에만 " + LlmFormat.Int(diff.TablesOnlyAfter));
            return s;
        }

        // ================= a2.values =================

        private static LlmSection Values(List<DbDiffLine> lines, LlmExportOptions opt)
        {
            var s = new LlmSection("a2.values", "분석 2 — IO 값 변화", 1);
            s.Note = "값이 달라진 IO 한 줄씩 (메인 화면 분석 2 요약과 같은 것). 다른열 = 같은 IO 에서 함께 달라진 다른 열.";
            s.Columns = new[] { "표", "IO", "열", "이전", "이후", "다른열" };
            List<DbChangedIo> ios = DbDiffList.ChangedValues(lines, int.MaxValue);
            for (int i = 0; i < ios.Count; i++)
            {
                DbChangedIo io = ios[i];
                var others = new List<string>();
                for (int k = 0; io.Others != null && k < io.Others.Count && k < 10; k++)
                    others.Add(io.Others[k].Column + ": " + io.Others[k].Before + " → " + io.Others[k].After);
                if (io.Others != null && io.Others.Count > 10) others.Add("외 " + (io.Others.Count - 10) + " 열");
                s.AddRow(io.Table, io.Io, io.Column, io.Before, io.After, string.Join("; ", others.ToArray()));
            }
            s.Cap(opt.MaxDbIoChanges);
            return s;
        }

        // ================= a2.schema =================

        private static LlmSection Schema(List<DbDiffLine> lines)
        {
            var s = new LlmSection("a2.schema", "분석 2 — 구조(열) 변화", 1);
            s.Note = "표의 열 정의가 바뀐 곳. 바뀜: 생김 · 없어짐 · 달라짐(타입 · NULL · 기본값) · 자리(순서만). 비고에 이름 바뀜 짐작 등.";
            s.Columns = new[] { "표", "열", "바뀜", "이전", "이후", "비고" };
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine l = lines[i];
                if (l.Kind != DbDiffLine.KindColumn) continue;
                s.AddRow(l.Table, l.Column, l.Change, l.Before, l.After, l.Note);
            }
            return s.TotalRows == 0 ? Empty(s, "구조 변화 없음") : s;
        }

        // ================= a2.tables =================

        private static LlmSection Tables(DbDiffResult diff)
        {
            var s = new LlmSection("a2.tables", "분석 2 — 표별 행 수", 2);
            s.Note = "표마다 행 비교 결과. 같은 행은 개수만 (내용은 안 보냄). 알림 = 키가 없어 짐작으로 짝지음 등.";
            s.Columns = new[] { "표", "이후쪽 이름", "상태", "열바뀜", "생긴행", "없어진행", "바뀐행", "같은행", "알림" };
            for (int i = 0; i < diff.Tables.Count; i++)
            {
                TableDiff t = diff.Tables[i];
                s.AddRow(t.Name, t.PairedName, t.Summary(),
                         LlmFormat.Int(t.ColumnsChanged), LlmFormat.Int(t.RowsAdded), LlmFormat.Int(t.RowsRemoved),
                         LlmFormat.Int(t.RowsChanged), LlmFormat.Int(t.RowsSame), t.RowNote);
            }
            return s;
        }

        // ================= a2.rows =================

        private static LlmSection Rows(List<DbDiffLine> lines, LlmExportOptions opt)
        {
            var s = new LlmSection("a2.rows", "분석 2 — 바뀐 행 (바뀐 칸만)", 2);
            s.Note = "값이 바뀐 행에서 바뀐 칸만 한 줄씩. 키 = 그 행을 가리키는 열쇠(예: id=7). 표마다 앞 "
                   + opt.MaxChangedCellsPerTable + " 줄까지.";
            s.Columns = new[] { "표", "키", "IO", "열", "이전", "이후" };
            var perTable = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine l = lines[i];
                if (l.Kind != DbDiffLine.KindRow || l.Change != DbDiffLine.ChangeDiff) continue;
                total++;
                if (Bump(perTable, l.Table) > opt.MaxChangedCellsPerTable) continue;
                s.AddRow(l.Table, l.Where, l.Io, l.Column, l.Before, l.After);
            }
            s.TotalRows = Math.Max(s.Rows.Count, total);
            return s.TotalRows == 0 ? null : s;
        }

        // ================= a2.notes =================

        private static LlmSection Notes(List<DbDiffLine> lines, DbDiffResult diff)
        {
            var s = new LlmSection("a2.notes", "분석 2 — 읽기 · 짝짓기 알림", 2);
            s.Note = "결과를 얼마나 믿을지의 근거 (못 읽은 표, 키가 없어 짐작으로 짝지음, 상한으로 덜 적음 등).";
            s.Columns = new[] { "표", "알림" };
            for (int i = 0; i < diff.Notes.Count; i++) s.AddRow(string.Empty, diff.Notes[i]);
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == DbDiffLine.KindNote) s.AddRow(lines[i].Table, Join(lines[i].Note, lines[i].After, lines[i].Before));
            return s.TotalRows == 0 ? null : s;
        }

        // ================= a2.keys =================

        private static LlmSection Keys(List<DbDiffLine> lines, LlmExportOptions opt)
        {
            var s = new LlmSection("a2.keys", "분석 2 — 생긴 · 없어진 행 (키만)", 3);
            s.Note = "한쪽에만 있는 행의 키. 내용은 안 보냄. 표마다 앞 " + opt.MaxKeysPerTable + " 개까지.";
            s.Columns = new[] { "표", "바뀜", "키", "IO" };
            var perTable = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine l = lines[i];
                if (l.Kind != DbDiffLine.KindRow) continue;
                if (l.Change != DbDiffLine.ChangeAdded && l.Change != DbDiffLine.ChangeGone) continue;
                total++;
                if (Bump(perTable, l.Table + "|" + l.Change) > opt.MaxKeysPerTable) continue;
                s.AddRow(l.Table, l.Change, l.Where, l.Io);
            }
            s.TotalRows = Math.Max(s.Rows.Count, total);
            return s.TotalRows == 0 ? null : s;
        }

        // ================= 작은 도움 =================

        private static int Bump(Dictionary<string, int> d, string key)
        {
            int n;
            d.TryGetValue(key ?? string.Empty, out n);
            n++;
            d[key ?? string.Empty] = n;
            return n;
        }

        private static string Join(params string[] parts)
        {
            var keep = new List<string>();
            for (int i = 0; i < parts.Length; i++) if (!string.IsNullOrEmpty(parts[i])) keep.Add(parts[i]);
            return string.Join(" · ", keep.ToArray());
        }

        /// <summary>표가 비었다는 것을 적어 둡니다 — 섹션을 아예 빼면 "안 봤다" 로 읽힙니다.</summary>
        private static LlmSection Empty(LlmSection s, string why)
        {
            s.Columns = null;
            s.AddFact("결과", why);
            return s;
        }
    }
}
