using System;
using System.Collections.Generic;
using LogScope.Core.Model;

namespace LogScope.Core.Io
{
    /// <summary>
    /// 한 줄씩 들어오는 표를 시간축 + 채널 배열로 바꿉니다.
    ///
    /// 표를 통째로 메모리에 올리거나 전치하지 않습니다. 배치를 판별할 만큼만
    /// 앞부분을 들고 있다가(Probe), 나머지는 흘려 읽으면서 바로 채널 배열에
    /// 씁니다. 그래서 여는 시간이 파일을 한 번 읽는 시간에 가깝습니다.
    /// </summary>
    public static class TableBuilder
    {
        /// <summary>
        /// 배치 판별용으로 앞부분을 붙들어 둘 때의 칸 수 상한.
        /// 줄 수만으로 제한하면 "행이 IO" 배치(한 줄이 표본 5 만 칸)에서
        /// 200 줄 x 5 만 칸 = 1000 만 칸을 붙들게 됩니다. 칸 수로도 막습니다.
        /// </summary>
        private const long ProbeCellBudget = 2000000;

        public static LogDataset Build(IRowSource src, OpenOptions opt, LoadProgress prog, string sourcePath)
        {
            if (opt == null) opt = new OpenOptions();
            var ds = new LogDataset();
            ds.SourcePath = sourcePath ?? string.Empty;

            // ---- 1. 앞부분을 붙들어 둔다 -----------------------------------
            var probe = new List<Cell[]>();
            long probeCells = 0;
            Cell[] row; int count;
            int probeRows = opt.ProbeRows > 0 ? opt.ProbeRows : 200;

            while (probe.Count < probeRows && probeCells < ProbeCellBudget)
            {
                if (prog != null) prog.ThrowIfCancelled();
                if (!src.NextRow(out row, out count)) break;
                Cell[] copy = new Cell[count];
                Array.Copy(row, copy, count);
                probe.Add(copy);
                probeCells += count;
            }

            if (probe.Count == 0)
            {
                ds.AddNote("파일에 읽을 줄이 없습니다.");
                ds.Times = new double[0];
                return ds;
            }

            // ---- 2. 표가 시작하는 자리를 찾는다 -----------------------------
            int r0, c0;
            Locate(probe, out r0, out c0);
            ds.FirstRow = r0 + 1;
            ds.FirstCol = c0 + 1;
            if (r0 > 0)
                ds.AddNote("표가 " + ExcelColumnName(c0) + (r0 + 1) + " 부터 시작합니다 (앞의 " + r0 + " 줄은 머리말).");

            // ---- 3. 배치를 정한다 -------------------------------------------
            Orientation orient = opt.Orientation;
            if (orient == Orientation.Auto)
            {
                orient = Decide(probe, r0, c0);
                ds.AddNote(orient == Orientation.Cols
                    ? "배치: 열이 IO 로 판별했습니다."
                    : "배치: 행이 IO 로 판별했습니다.");
            }
            ds.Orientation = orient;

            // ---- 4. 읽는다 ---------------------------------------------------
            if (orient == Orientation.Rows) BuildRows(ds, probe, r0, c0, src, opt, prog);
            else BuildCols(ds, probe, r0, c0, src, opt, prog);

            return ds;
        }

        // ================= 표 시작 위치 =====================================

        private static void Locate(List<Cell[]> probe, out int r0, out int c0)
        {
            int n = probe.Count;
            int[] filled = new int[n];
            int[] firstCol = new int[n];
            int max = 0;

            for (int i = 0; i < n; i++)
            {
                Cell[] r = probe[i];
                int f = 0, fc = -1;
                for (int j = 0; j < r.Length; j++)
                {
                    if (r[j].IsEmpty) continue;
                    f++;
                    if (fc < 0) fc = j;
                }
                filled[i] = f;
                firstCol[i] = fc < 0 ? 0 : fc;
                if (f > max) max = f;
            }

            // 머리말 줄("장비명, 3호기")은 칸이 두어 개뿐이고, 표 줄은 훨씬
            // 넓습니다. 그 차이로 표가 시작하는 줄을 찾습니다. 기준을 2 로
            // 낮추면 머리말이 표로 잡히므로 최소 3 칸을 요구합니다.
            int threshold = Math.Max(3, (int)Math.Ceiling(max * 0.6));

            r0 = -1;
            for (int i = 0; i < n; i++)
            {
                if (filled[i] < threshold) continue;
                // 한 줄만 넓은 것은 제목일 수 있습니다. 다음 줄도 넓어야 표로 봅니다.
                if (i + 1 < n && filled[i + 1] < threshold) continue;
                r0 = i;
                break;
            }

            // 열이 두 개뿐인 로그(Time + 값 하나)는 위 기준을 넘지 못합니다.
            // 그럴 때는 "가장 넓은 폭이 두 줄 이어지는 첫 자리" 를 표의 시작으로 봅니다.
            if (r0 < 0 && max > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (filled[i] != max) continue;
                    if (i + 1 < n && filled[i + 1] != max) continue;
                    r0 = i;
                    break;
                }
            }
            if (r0 < 0)
            {
                for (int i = 0; i < n; i++) if (filled[i] >= 2) { r0 = i; break; }
            }
            if (r0 < 0) r0 = 0;

            // 시작 열은 표 줄들이 공통으로 쓰는 가장 왼쪽 칸입니다.
            int need = Math.Min(threshold, filled[r0]);
            if (need < 1) need = 1;
            int minCol = int.MaxValue;
            for (int i = r0; i < n && i < r0 + 10; i++)
            {
                if (filled[i] < need) continue;
                if (firstCol[i] < minCol) minCol = firstCol[i];
            }
            c0 = minCol == int.MaxValue ? firstCol[r0] : minCol;
        }

        // ================= 배치 판별 ========================================

        /// <summary>
        /// 이름처럼 보이는 칸인지. 글자이면서 시각으로도 숫자로도 읽히지 않으면
        /// 이름입니다. 시각이 "12:34:56" 처럼 글자로 들어오는 로그가 흔해서,
        /// 단순히 "글자면 이름" 으로 보면 배치를 잘못 잡습니다.
        /// </summary>
        private static bool IsNameLike(Cell c)
        {
            if (c.Kind != CellKind.Text) return false;
            TimeKind k; double v;
            return !ValueParse.TryTimeValue(c, out k, out v);
        }

        private static Orientation Decide(List<Cell[]> probe, int r0, int c0)
        {
            Cell[] head = probe[r0];
            int rowNames = 0;
            for (int j = c0; j < head.Length; j++) if (IsNameLike(head[j])) rowNames++;

            int colNames = 0;
            for (int i = r0; i < probe.Count; i++)
            {
                Cell[] r = probe[i];
                if (c0 < r.Length && IsNameLike(r[c0])) colNames++;
            }

            // 비기면 열이 IO 로 봅니다. 현장 로그가 거의 그 모양입니다.
            return colNames > rowNames ? Orientation.Rows : Orientation.Cols;
        }

        // ================= 열이 IO 인 배치 ==================================

        private static void BuildCols(LogDataset ds, List<Cell[]> probe, int r0, int c0,
                                      IRowSource src, OpenOptions opt, LoadProgress prog)
        {
            Cell[] header = probe[r0];
            int timeCol = PickTimeColumn(probe, r0, c0, header);

            int dataStart = r0 + 1;
            Cell[] unitRow = null;
            if (IsUnitRow(probe, dataStart, c0, timeCol))
            {
                // 값이 아니라 단위가 적힌 줄입니다. 표에서는 빼되 버리지는
                // 않습니다 — 세로 눈금에 단위를 적는 데 씁니다.
                unitRow = probe[dataStart];
                dataStart++;
                ds.AddNote("머리 행 아래의 단위 줄에서 단위를 가져왔습니다.");
            }

            // 채널 이름은 머리 행에 적힌 그대로 씁니다.
            var accs = new List<ChannelAccum>();
            var colOf = new List<int>();
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            int blanks = 0;

            for (int j = c0; j < header.Length; j++)
            {
                if (j == timeCol) continue;
                string name = header[j].Display();
                if (string.IsNullOrEmpty(name))
                {
                    // 값은 있는데 이름이 없는 열. 버리지 않고 열 문자로 표시합니다.
                    if (!ColumnHasData(probe, dataStart, j)) continue;
                    name = "(이름 없음 " + ExcelColumnName(j) + "열)";
                    blanks++;
                }
                name = Unique(used, name);
                if (opt.MaxChannels > 0 && accs.Count >= opt.MaxChannels) break;

                var acc = new ChannelAccum(name, 1024, opt.MaxStateValues);
                acc.Unit = UnitFor(unitRow, j, name);
                accs.Add(acc);
                colOf.Add(j);
            }
            if (blanks > 0) ds.AddNote("머리 행에 이름이 없는 열 " + blanks + " 개를 열 문자로 이름 붙였습니다.");

            if (accs.Count == 0)
            {
                ds.AddNote("채널을 하나도 찾지 못했습니다.");
                ds.Times = new double[0];
                return;
            }

            // 시간 칸과 값 칸을 한 번에 훑습니다.
            var rawTimes = new List<double>(4096);
            var hasTime = new List<bool>(4096);
            var kindVote = new int[4];
            int skippedEmpty = 0;
            int sample = 0;
            int nAcc = accs.Count;

            Action<Cell[], int> take = delegate (Cell[] cells, int len)
            {
                // 시간
                Cell tc = timeCol >= 0 && timeCol < len ? cells[timeCol] : Cell.Empty;
                TimeKind tk; double tv;
                if (!tc.IsEmpty && ValueParse.TryTimeValue(tc, out tk, out tv))
                {
                    rawTimes.Add(tv); hasTime.Add(true); kindVote[(int)tk]++;
                }
                else { rawTimes.Add(0); hasTime.Add(false); }

                // 값
                for (int k = 0; k < nAcc; k++)
                {
                    int j = colOf[k];
                    accs[k].Add(j < len ? cells[j] : Cell.Empty);
                }
                sample++;
            };

            for (int i = dataStart; i < probe.Count; i++)
            {
                Cell[] r = probe[i];
                if (AllEmpty(r, r.Length)) { skippedEmpty++; continue; }
                take(r, r.Length);
                if (opt.MaxSamples > 0 && sample >= opt.MaxSamples) break;
            }

            Cell[] row; int count;
            long reported = 0;
            while ((opt.MaxSamples <= 0 || sample < opt.MaxSamples) && src.NextRow(out row, out count))
            {
                if (AllEmpty(row, count)) { skippedEmpty++; continue; }
                take(row, count);

                if ((++reported & 0xFF) == 0 && prog != null)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("값을 읽는 중… 표본 " + sample.ToString("N0"), src.Progress);
                }
            }

            if (skippedEmpty > 0) ds.AddNote("빈 줄 " + skippedEmpty + " 개를 건너뛰었습니다.");
            FinishDataset(ds, accs, rawTimes, hasTime, kindVote, sample, prog);
        }

        private static bool ColumnHasData(List<Cell[]> probe, int from, int col)
        {
            for (int i = from; i < probe.Count; i++)
            {
                Cell[] r = probe[i];
                if (col < r.Length && !r[col].IsEmpty) return true;
            }
            return false;
        }

        /// <summary>
        /// 시간 열을 고릅니다. 보통 표의 첫 열이지만, 앞에 "No." 같은 열이
        /// 붙는 로그가 있어 앞쪽 네 열 중 가장 시간축다운 열을 고릅니다.
        /// </summary>
        private static int PickTimeColumn(List<Cell[]> probe, int r0, int c0, Cell[] header)
        {
            int best = c0, bestScore = int.MinValue;
            int last = Math.Min(c0 + 3, header.Length - 1);

            for (int j = c0; j <= last; j++)
            {
                int score = 0;
                double prev = double.NaN;
                bool increasing = true;
                int parsed = 0, seen = 0;

                for (int i = r0 + 1; i < probe.Count && seen < 64; i++)
                {
                    Cell[] r = probe[i];
                    if (j >= r.Length || r[j].IsEmpty) continue;
                    seen++;
                    TimeKind k; double v;
                    if (!ValueParse.TryTimeValue(r[j], out k, out v)) { increasing = false; continue; }
                    parsed++;
                    if (k == TimeKind.ClockMs || k == TimeKind.DateMs) score += 2;
                    if (!double.IsNaN(prev) && v < prev) increasing = false;
                    prev = v;
                }

                if (seen > 0 && parsed == seen) score += 4;
                if (increasing && parsed > 1) score += 6;
                score += parsed;

                string h = header.Length > j ? header[j].Display() : string.Empty;
                if (!string.IsNullOrEmpty(h))
                {
                    string hl = h.ToLowerInvariant();
                    if (hl.Contains("time") || hl.Contains("date") || hl.Contains("시간")
                        || hl.Contains("시각") || hl.Contains("일시") || hl.Contains("날짜"))
                        score += 10;
                }
                if (j == c0) score += 1;   // 같은 점수면 첫 열

                if (score > bestScore) { bestScore = score; best = j; }
            }
            return best;
        }

        /// <summary>
        /// 이 열의 단위. 단위 줄이 있으면 거기서, 없으면 이름 끝의 괄호에서
        /// 찾습니다. 둘 다 없으면 빈 글자입니다.
        /// </summary>
        private static string UnitFor(Cell[] unitRow, int column, string name)
        {
            if (unitRow != null && column < unitRow.Length)
            {
                string u = unitRow[column].Display();
                if (!string.IsNullOrEmpty(u) && u.Length <= 16) return u;
            }
            return ValueParse.ExtractUnit(name);
        }

        /// <summary>머리 행 바로 아래가 단위 줄("mm", "V", "℃" 등)인지.</summary>
        private static bool IsUnitRow(List<Cell[]> probe, int idx, int c0, int timeCol)
        {
            if (idx >= probe.Count) return false;
            Cell[] r = probe[idx];
            int text = 0, other = 0;
            for (int j = c0; j < r.Length; j++)
            {
                if (j == timeCol || r[j].IsEmpty) continue;
                if (IsNameLike(r[j])) text++; else other++;
            }
            return other == 0 && text >= 2;
        }

        // ================= 행이 IO 인 배치 ==================================

        private static void BuildRows(LogDataset ds, List<Cell[]> probe, int r0, int c0,
                                      IRowSource src, OpenOptions opt, LoadProgress prog)
        {
            Cell[] timeRow = probe[r0];

            // 첫 칸이 이름이면("이름", "Time" 등) 시각은 그 다음 칸부터입니다.
            int tStart = (c0 < timeRow.Length && IsNameLike(timeRow[c0])) ? c0 + 1 : c0;

            int tEnd = timeRow.Length - 1;
            while (tEnd >= tStart && timeRow[tEnd].IsEmpty) tEnd--;
            int sample = tEnd - tStart + 1;
            if (sample < 0) sample = 0;
            if (opt.MaxSamples > 0 && sample > opt.MaxSamples) sample = opt.MaxSamples;

            var rawTimes = new List<double>(Math.Max(4, sample));
            var hasTime = new List<bool>(Math.Max(4, sample));
            var kindVote = new int[4];

            for (int m = 0; m < sample; m++)
            {
                Cell tc = timeRow[tStart + m];
                TimeKind tk; double tv;
                if (!tc.IsEmpty && ValueParse.TryTimeValue(tc, out tk, out tv))
                {
                    rawTimes.Add(tv); hasTime.Add(true); kindVote[(int)tk]++;
                }
                else { rawTimes.Add(0); hasTime.Add(false); }
            }

            var accs = new List<ChannelAccum>();
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            int skippedEmpty = 0, noName = 0;
            long rowIndex = r0;

            Action<Cell[], int> take = delegate (Cell[] cells, int len)
            {
                rowIndex++;
                string name = c0 < len ? cells[c0].Display() : string.Empty;
                if (string.IsNullOrEmpty(name)) { name = "(이름 없음 " + (rowIndex + 1) + "행)"; noName++; }
                name = Unique(used, name);

                var acc = new ChannelAccum(name, sample, opt.MaxStateValues);
                // 행이 IO 인 배치에는 단위 줄이라는 게 없습니다. 이름 끝의
                // 괄호에서만 찾습니다.
                acc.Unit = ValueParse.ExtractUnit(name);
                for (int m = 0; m < sample; m++)
                {
                    int j = tStart + m;
                    acc.Add(j < len ? cells[j] : Cell.Empty);
                }
                accs.Add(acc);
            };

            for (int i = r0 + 1; i < probe.Count; i++)
            {
                if (opt.MaxChannels > 0 && accs.Count >= opt.MaxChannels) break;
                Cell[] r = probe[i];
                if (AllEmpty(r, r.Length)) { skippedEmpty++; rowIndex++; continue; }
                take(r, r.Length);
            }

            Cell[] row; int count;
            while ((opt.MaxChannels <= 0 || accs.Count < opt.MaxChannels) && src.NextRow(out row, out count))
            {
                if (AllEmpty(row, count)) { skippedEmpty++; rowIndex++; continue; }
                take(row, count);
                if ((accs.Count & 0x1F) == 0 && prog != null)
                {
                    prog.ThrowIfCancelled();
                    prog.Report("채널을 읽는 중… " + accs.Count + " 개", src.Progress);
                }
            }

            if (skippedEmpty > 0) ds.AddNote("빈 줄 " + skippedEmpty + " 개를 건너뛰었습니다.");
            if (noName > 0) ds.AddNote("이름이 없는 행 " + noName + " 개를 행 번호로 이름 붙였습니다.");
            FinishDataset(ds, accs, rawTimes, hasTime, kindVote, sample, prog);
        }

        // ================= 마무리 ===========================================

        private static void FinishDataset(LogDataset ds, List<ChannelAccum> accs,
                                          List<double> rawTimes, List<bool> hasTime,
                                          int[] kindVote, int sample, LoadProgress prog)
        {
            if (prog != null) prog.Report("시간축을 정리하는 중…", -1);

            TimeKind kind = TimeKind.Index;
            int best = 0;
            for (int i = 1; i < kindVote.Length; i++) if (kindVote[i] > kindVote[best]) best = i;
            if (kindVote[best] > 0) kind = (TimeKind)best;
            // 시각과 숫자가 섞이면 시각 쪽을 따릅니다.
            if (kindVote[(int)TimeKind.DateMs] > 0) kind = TimeKind.DateMs;
            else if (kindVote[(int)TimeKind.ClockMs] > 0) kind = TimeKind.ClockMs;

            TimeAxis.Result t = TimeAxis.Build(rawTimes.ToArray(), hasTime.ToArray(), sample, kind, string.Empty);
            ds.Times = t.Times;
            ds.TimeKind = t.Kind;
            ds.TimeUnit = t.Unit;
            foreach (string note in t.Notes) ds.AddNote(note);

            if (prog != null) prog.Report("채널을 마무리하는 중…", -1);

            int overflow = 0;
            for (int i = 0; i < accs.Count; i++)
            {
                if (prog != null && (i & 0x3F) == 0) prog.ThrowIfCancelled();
                if (accs[i].StateOverflow) overflow++;
                ds.Channels.Add(accs[i].Finish(sample));
            }
            if (overflow > 0) ds.AddNote("상태 값 종류가 너무 많은 채널 " + overflow + " 개는 일부 값을 비웠습니다.");
        }

        // ================= 잡다한 도우미 =====================================

        private static bool AllEmpty(Cell[] cells, int len)
        {
            for (int i = 0; i < len && i < cells.Length; i++) if (!cells[i].IsEmpty) return false;
            return true;
        }

        /// <summary>
        /// 같은 이름이 두 번 나오면 뒤엣것에 번호를 붙입니다. 그룹 구성원을
        /// 이름으로 저장하기 때문에, 이름이 겹치면 엉뚱한 채널이 붙습니다.
        /// </summary>
        private static string Unique(Dictionary<string, int> used, string name)
        {
            int n;
            if (!used.TryGetValue(name, out n)) { used[name] = 1; return name; }
            n++;
            used[name] = n;
            string candidate = name + " #" + n;
            while (used.ContainsKey(candidate)) { n++; used[name] = n; candidate = name + " #" + n; }
            used[candidate] = 1;
            return candidate;
        }

        /// <summary>0 -> "A", 26 -> "AA".</summary>
        public static string ExcelColumnName(int index)
        {
            if (index < 0) return "?";
            string s = string.Empty;
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                s = (char)('A' + rem) + s;
                index = (index - 1) / 26;
            }
            return s;
        }
    }
}
