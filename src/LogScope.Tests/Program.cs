using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LogScope.Analysis1.Align;
using LogScope.Analysis1.Compare;
using LogScope.Analysis1.Graph;
using LogScope.Analysis1.Heatmap;
using LogScope.Analysis1.LogFile;
using LogScope.Analysis2;
using LogScope.Common;
using LogScope.Main;
using LogScope.Settings;

namespace LogScope.Tests
{
    /// <summary>
    /// 자체 테스트. 실패가 하나라도 있으면 1 로 끝납니다.
    /// 실행: LogScope.Tests.exe
    /// </summary>
    internal static class Program
    {
        private static int _pass, _fail;
        private static string _dir;

        /// <summary>
        /// 실패한 줄만 모아 둡니다. 시험이 670 개라 FAIL 줄이 OK 줄 사이에
        /// 묻혀 스크롤로 올라갑니다 — <b>맨 밑에 다시 모아 찍습니다.</b>
        ///
        /// build.bat 도 이 목록을 파일로 받아 빌드가 다 끝난 뒤 한 번 더
        /// 찍습니다 (테스트는 빌드 중간에 지나갑니다).
        /// </summary>
        private static readonly List<string> _fails = new List<string>();

        /// <summary>지금 돌고 있는 묶음 이름. 어느 묶음에서 났는지 함께 적습니다.</summary>
        private static string _group = string.Empty;

        /// <summary>
        /// 콘솔 글자 인코딩을 정합니다.
        ///
        /// <b>윈도우에서는 손대지 않습니다.</b> 코드 페이지는 프로세스가 아니라
        /// <b>콘솔 창의 성질</b>이라, 여기서 65001 로 바꾸면 이 프로그램이 끝난
        /// 뒤에도 그 창에 그대로 남습니다. 그러면 build.bat 이 그 뒤에 찍는
        /// CP949 한글이 전부 깨집니다 — 테스트를 돌린 시점부터 갑자기 글자가
        /// 사라지는 것처럼 보입니다.
        ///
        /// 한국어 윈도우 콘솔은 어차피 CP949 로 시작하고, .NET 이 한글을 그
        /// 코드 페이지로 알아서 바꿔 내보냅니다. 그대로 두는 것이 맞습니다.
        ///
        /// 리눅스(Mono)에서는 반대입니다. 터미널이 UTF-8 인데 LANG 이 비어
        /// 있으면 기본이 ASCII 로 잡혀 한글이 물음표가 됩니다. 거기서만 UTF-8
        /// 로 맞춥니다. 리눅스에는 "콘솔 코드 페이지" 라는 것이 없어서 남의
        /// 뒷정리를 망칠 일도 없습니다.
        /// </summary>
        private static bool Windows()
        {
            PlatformID id = Environment.OSVersion.Platform;
            return id == PlatformID.Win32NT || id == PlatformID.Win32Windows
                || id == PlatformID.Win32S || id == PlatformID.WinCE;
        }

        private static void UseConsoleEncoding()
        {
            if (Windows()) return;

            // 출력이 파일로 돌려져 있으면 런타임에 따라 예외가 납니다.
            // 인코딩 하나 때문에 테스트가 안 돌아가면 곤란합니다.
            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch (IOException) { }
            catch (NotSupportedException) { }
        }

        private static int Main(string[] args)
        {
            UseConsoleEncoding();
            _dir = Path.Combine(Path.GetTempPath(), "logscope-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_dir);
            try
            {
                CsvColumnsLayout();
                CsvRowsLayout();
                PreambleAndMidSheetTable();
                SparseTimeColumn();
                RepeatedTimestampsSpread();
                AllSameTimestampsFallBackToIndex();
                ZeroRunSurvives();
                XlsxRoundTrip();
                DigitalAndStateKinds();
                DecimationKeepsExtremes();
                DecimationAccumulatesPerColumn();
                ZoomedInTraceSurvives();
                CompareMetrics();
                ExistenceDifference();
                ToleranceIsPercentOfBefore();
                ChangedMatchesMaxPercent();
                HeatmapBuckets();
                HeatmapRelativeTolerance();
                HeatmapCellNumberDoesNotFollowTolerance();
                HeatmapStateNamesIgnoreTolerance();
                UnitsFromUnitRowAndName();
                NumberTextHasNoExponent();
                HeatmapOrderIsSameForEveryBucketWidth();
                TriggerAlignOnEdge();
                TriggerAlignRefusesBadIo();
                TriggerAlignCountsStartAsEdge();
                TriggerAlignOnAnyLevel();
                DifferenceOfTwoLogs();
                DifferenceAcrossGrids();
                DifferenceOfStateChannels();
                DifferenceOutsideOverlap();
                HeatmapNameFilter();
                HistorySaveAndLoad();
                HistoryEdgeCases();
                HistorySnapshots();
                LlmPanelRules();
                JsonRoundTrip();
                SettingsRoundTrip();
                AppConfigParse();
                AppConfigFile();
                GroupFileParse();
                WindowTitleText();
                TraceColorRule();
                DbSqlDump();
                TextSniffRules();
                DbDumpBytes();
                DbCsvTable();
                DbNestedFolders();
                DbFrmIbdNote();
                FrmShape();
                FrmDefaultUnknown();
                FrmInFolder();
                DbSingleFile();
                DbCompare();
                DbDiffLines();
                DbColumnNames();
                DbTallyAndMarks();
                DbPairByShape();
                CsvKeyAndValue();
                DbShowAllIos();
                DbVerdictRule();
                DbManyRows();
                DbChangedValuesRule();
                DbDeltaText();
                DbPathNames();
                Analysis3LinkRules();
                Analysis3AttachRules();
                Analysis3CardRules();
                StepsFromChannel();
                StepPairing();
                HeatmapByStep();
                StepBandWidths();
                HeatmapPerStepShift();
                HeatmapGroupsAndSearch();
                NameSearchRule();
                DbScript();
                ToleranceTableRules();
                TolerancePerIo();
            }
            finally
            {
                try { Directory.Delete(_dir, true); } catch (IOException) { }
            }

            Console.WriteLine();
            Console.WriteLine("통과 " + _pass + " / 실패 " + _fail);

            Report(args);
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>
        /// 실패한 줄만 맨 밑에 모아 찍습니다. 인수로 파일 자리를 주면 그
        /// 파일에도 같은 내용을 적습니다 (build.bat 이 끝에서 다시 찍습니다).
        ///
        /// 파일은 <b>시스템 기본 인코딩</b>으로 적습니다. 한국어 윈도우면
        /// CP949 입니다 — build.bat 이 그걸 <c>type</c> 으로 찍는데, UTF-8 로
        /// 적어 두면 콘솔에서 한글이 깨집니다.
        ///
        /// 성공했으면 <b>그 파일을 지웁니다.</b> 남겨 두면 다음 빌드에서
        /// 지난번 실패가 "지금 실패" 로 보입니다.
        /// </summary>
        private static void Report(string[] args)
        {
            string path = args != null && args.Length > 0 ? args[0] : null;

            if (_fails.Count == 0)
            {
                if (path != null) Erase(path);
                return;
            }

            Console.WriteLine();
            Console.WriteLine("실패한 것만 모으면 (" + _fails.Count + " 개)");
            Console.WriteLine("  ------------------------------------------------------------");
            for (int i = 0; i < _fails.Count; i++) Console.WriteLine("  " + _fails[i]);

            if (path == null) return;
            try
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _fails.Count; i++) sb.Append("  ").Append(_fails[i]).Append("\r\n");
                File.WriteAllText(path, sb.ToString(), LogEncoding());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                // 파일에 못 적어도 위에 이미 찍었습니다. 그것 때문에 테스트가
                // 실패한 것처럼 보이면 안 됩니다.
            }
        }

        /// <summary>
        /// 실패 목록 파일의 인코딩.
        ///
        /// 윈도우에서는 <b>시스템 기본(ANSI)</b> 입니다 — build.bat 이
        /// <c>type</c> 으로 찍는데, UTF-8 로 적어 두면 CP949 콘솔에서 한글이
        /// 깨집니다. 윈도우 밖에서는 반대로 Encoding.Default 가 ASCII 로
        /// 잡혀 한글이 물음표가 되므로 UTF-8 로 적습니다.
        ///
        /// 콘솔 인코딩을 정하는 쪽(UseConsoleEncoding)과 <b>같은 판단</b>을
        /// 씁니다. 두 군데서 따로 판단하면 한쪽만 고쳐지는 날이 옵니다.
        /// </summary>
        private static Encoding LogEncoding()
        {
            return Windows() ? Encoding.Default : (Encoding)new UTF8Encoding(false);
        }

        private static void Erase(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
            }
        }

        // ---------------- 도우미 ----------------

        /// <summary>
        /// 묶음 머리글. 글자를 찍으면서 <b>지금 묶음 이름을 기억</b>합니다 —
        /// 그래야 맨 밑에 모아 찍을 때 "어느 묶음에서 났나" 를 적을 수 있습니다.
        /// </summary>
        private static void Say(string group)
        {
            _group = group ?? string.Empty;
            Console.WriteLine(_group);
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; Console.WriteLine("  OK    " + name); return; }

            _fail++;
            string line = "  FAIL  " + name + (detail != null ? "  — " + detail : "");
            Console.WriteLine(line);
            _fails.Add((_group.Length > 0 ? "[" + _group + "] " : string.Empty) + line.Trim());
        }

        private static void Near(string name, double actual, double expected, double tol)
        {
            Check(name, Math.Abs(actual - expected) <= tol,
                "기대 " + expected.ToString("R", CultureInfo.InvariantCulture)
                + ", 실제 " + actual.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string WriteCsv(string name, string text)
        {
            string p = Path.Combine(_dir, name);
            File.WriteAllText(p, text, new UTF8Encoding(false));
            return p;
        }

        private static LogDataset Open(string path, Orientation orient)
        {
            var o = new OpenOptions();
            o.Orientation = orient;
            return LogReader.Open(path, o, null);
        }

        /// <summary>단계 IO 를 지정해 엽니다. StepRead 를 거치는 길입니다.</summary>
        private static LogDataset OpenWithSteps(string path, string stepIo)
        {
            var o = new OpenOptions();
            o.Orientation = Orientation.Auto;
            o.StepIo = stepIo;
            return LogReader.Open(path, o, null);
        }

        /// <summary>히트맵 결과에서 그 IO 의 줄. 없으면 null.</summary>
        private static HeatRow FindRow(HeatmapResult r, string name)
        {
            if (r == null) return null;
            for (int i = 0; i < r.Rows.Count; i++)
                if (r.Rows[i].Name == name) return r.Rows[i];
            return null;
        }

        private static float[] Values(LogDataset ds, string name)
        {
            int i = ds.FindChannel(name);
            return i < 0 ? null : ds.Channels[i].Values;
        }

        // ---------------- 테스트 ----------------

        private static void CsvColumnsLayout()
        {
            Say("열이 IO 인 CSV");
            string p = WriteCsv("cols.csv",
                "Time,밸브 OPEN,압력 PT-01,상태\n" +
                "0.0,0,101.5,IDLE\n" +
                "0.1,1,101.7,RUN\n" +
                "0.2,1,101.9,RUN\n");
            LogDataset ds = Open(p, Orientation.Auto);

            Check("배치를 열이 IO 로 판별", ds.Orientation == Orientation.Cols, ds.Orientation.ToString());
            Check("채널 3개", ds.ChannelCount == 3, "실제 " + ds.ChannelCount);
            Check("표본 3개", ds.SampleCount == 3, "실제 " + ds.SampleCount);
            Check("엑셀에 적힌 이름을 그대로 사용", ds.FindChannel("압력 PT-01") >= 0, null);
            Near("값을 그대로 읽음", Values(ds, "압력 PT-01")[2], 101.9, 1e-4);
        }

        private static void CsvRowsLayout()
        {
            Say("행이 IO 인 CSV");
            string p = WriteCsv("rows.csv",
                "이름,0.0,0.1,0.2,0.3\n" +
                "DI_00,0,1,1,0\n" +
                "AI_TEMP,20.1,20.2,20.4,20.9\n");
            LogDataset ds = Open(p, Orientation.Auto);

            Check("배치를 행이 IO 로 판별", ds.Orientation == Orientation.Rows, ds.Orientation.ToString());
            Check("채널 2개", ds.ChannelCount == 2, "실제 " + ds.ChannelCount);
            Check("표본 4개", ds.SampleCount == 4, "실제 " + ds.SampleCount);
            Near("마지막 값", Values(ds, "AI_TEMP")[3], 20.9, 1e-4);
        }

        private static void PreambleAndMidSheetTable()
        {
            Say("머리말이 붙어 표가 중간에서 시작하는 경우");
            string p = WriteCsv("preamble.csv",
                "장비명,3호기\n" +
                "측정일시,2024-05-02 09:00\n" +
                "\n" +
                ",,Time,IO_A,IO_B,IO_C\n" +
                ",,0.0,1,2,3\n" +
                ",,0.1,2,3,4\n" +
                ",,0.2,3,4,5\n");
            LogDataset ds = Open(p, Orientation.Auto);

            Check("표 시작 행을 찾음", ds.FirstRow == 4, "실제 " + ds.FirstRow);
            Check("표 시작 열을 찾음", ds.FirstCol == 3, "실제 " + ds.FirstCol);
            Check("채널 3개", ds.ChannelCount == 3, "실제 " + ds.ChannelCount);
            Check("머리말을 채널로 잘못 넣지 않음", ds.FindChannel("장비명") < 0, null);
        }

        private static void SparseTimeColumn()
        {
            Say("시간이 바뀔 때만 적힌 경우");
            string p = WriteCsv("sparse.csv",
                "Time,V\n" +
                "10,0\n" +
                ",1\n" +
                ",2\n" +
                "20,3\n" +
                ",4\n" +
                "30,5\n");
            LogDataset ds = Open(p, Orientation.Auto);

            Check("표본 6개", ds.SampleCount == 6, "실제 " + ds.SampleCount);
            Near("빈 칸을 앞뒤 사이로 채움", ds.Times[1], 10 + 10.0 / 3, 1e-6);
            Near("두 번째 빈 칸", ds.Times[2], 10 + 20.0 / 3, 1e-6);
            Near("적혀 있던 시각은 그대로", ds.Times[3], 20, 1e-9);

            bool increasing = true;
            for (int i = 1; i < ds.SampleCount; i++) if (ds.Times[i] <= ds.Times[i - 1]) increasing = false;
            Check("시간축이 계속 늘어남", increasing, null);
        }

        private static void RepeatedTimestampsSpread()
        {
            Say("같은 시각이 반복되는 경우 (분 단위 표기 + 초 단위 기록)");
            var sb = new StringBuilder("Time,V\n");
            for (int minute = 0; minute < 3; minute++)
                for (int k = 0; k < 4; k++)
                    sb.Append("00:0").Append(minute).Append(":00,").Append(k).Append('\n');
            string p = WriteCsv("repeat.csv", sb.ToString());
            LogDataset ds = Open(p, Orientation.Auto);

            Check("표본 12개", ds.SampleCount == 12, "실제 " + ds.SampleCount);
            Check("시각으로 인식", ds.TimeKind == TimeKind.ClockMs, ds.TimeKind.ToString());

            bool distinct = true;
            for (int i = 1; i < ds.SampleCount; i++) if (ds.Times[i] <= ds.Times[i - 1]) distinct = false;
            Check("겹치지 않게 펴짐", distinct, null);

            // 첫 분의 네 표본은 0s, 15s, 30s, 45s 로 고르게 퍼져야 합니다.
            Near("고르게 퍼짐 (0초)", ds.Times[0], 0, 1e-6);
            Near("고르게 퍼짐 (15초)", ds.Times[1], 15000, 1e-3);
            Near("고르게 퍼짐 (30초)", ds.Times[2], 30000, 1e-3);
            Near("다음 분으로 이어짐", ds.Times[4], 60000, 1e-3);
        }

        private static void AllSameTimestampsFallBackToIndex()
        {
            Say("시각이 전부 같은 경우");
            var sb = new StringBuilder("Time,V\n");
            for (int i = 0; i < 5; i++) sb.Append("09:00:00,").Append(i).Append('\n');
            LogDataset ds = Open(WriteCsv("same.csv", sb.ToString()), Orientation.Auto);

            Check("샘플 번호를 시간축으로 사용", ds.TimeKind == TimeKind.Index, ds.TimeKind.ToString());
            Near("0 부터 시작", ds.Times[0], 0, 1e-9);
            Near("마지막은 4", ds.Times[4], 4, 1e-9);
        }

        private static void ZeroRunSurvives()
        {
            Say("0 이 오래 이어지는 구간");
            var sb = new StringBuilder("Time,V\n");
            for (int i = 0; i < 200; i++) sb.Append(i).Append(',').Append(i >= 50 && i < 150 ? 0 : 1).Append('\n');
            LogDataset ds = Open(WriteCsv("zeros.csv", sb.ToString()), Orientation.Auto);

            float[] v = Values(ds, "V");
            int zeros = 0;
            for (int i = 0; i < v.Length; i++) if (v[i] == 0f) zeros++;
            Check("0 구간 100 표본이 그대로 남음", zeros == 100, "실제 " + zeros);
            Check("표본 수가 줄지 않음", ds.SampleCount == 200, "실제 " + ds.SampleCount);
        }

        private static void XlsxRoundTrip()
        {
            Say("xlsx 읽기");
            var rows = new List<object[]>();
            rows.Add(new object[] { "설비", "A라인" });
            rows.Add(new object[] { });
            rows.Add(new object[] { "Time", "밸브 OPEN", "압력 PT-01" });
            for (int i = 0; i < 20; i++)
                rows.Add(new object[] { (double)i * 0.5, (double)(i % 2), 100.0 + i * 0.25 });

            string p = Path.Combine(_dir, "sample.xlsx");
            XlsxWriter.Write(p, rows);
            LogDataset ds = Open(p, Orientation.Auto);

            Check("채널 2개", ds.ChannelCount == 2, "실제 " + ds.ChannelCount);
            Check("표본 20개", ds.SampleCount == 20, "실제 " + ds.SampleCount);
            Check("한글 이름 그대로", ds.FindChannel("밸브 OPEN") >= 0, null);
            Near("마지막 값", Values(ds, "압력 PT-01")[19], 100.0 + 19 * 0.25, 1e-4);
            Near("시간 마지막", ds.Times[19], 9.5, 1e-6);

            List<string> sheets = LogReader.ListSheets(p);
            Check("시트 이름을 읽음", sheets.Count == 1 && sheets[0] == "로그",
                  sheets.Count > 0 ? sheets[0] : "(없음)");
        }

        private static void DigitalAndStateKinds()
        {
            Say("채널 타입 판별");
            string p = WriteCsv("kinds.csv",
                "Time,DI,AI,ST\n" +
                "0,0,12.5,IDLE\n" +
                "1,1,12.6,RUN\n" +
                "2,0,12.4,RUN\n" +
                "3,1,12.8,STOP\n");
            LogDataset ds = Open(p, Orientation.Auto);

            Check("0/1 은 디지털", ds.Channels[ds.FindChannel("DI")].Kind == ChannelKind.Digital, null);
            Check("실수는 아날로그", ds.Channels[ds.FindChannel("AI")].Kind == ChannelKind.Analog, null);

            Channel st = ds.Channels[ds.FindChannel("ST")];
            Check("문자열은 상태", st.Kind == ChannelKind.State, st.Kind.ToString());
            Check("상태 이름 3종", st.States != null && st.States.Length == 3,
                  st.States != null ? st.States.Length.ToString() : "null");
            Check("커서 표시가 상태 이름", st.FormatValue(st.Values[0]) == "IDLE", st.FormatValue(st.Values[0]));
        }

        private static void DecimationKeepsExtremes()
        {
            Say("픽셀 열 접기");
            var sb = new StringBuilder("Time,V\n");
            for (int i = 0; i < 10000; i++)
                sb.Append(i).Append(',').Append(i == 5000 ? 999 : (i % 2)).Append('\n');
            LogDataset ds = Open(WriteCsv("decim.csv", sb.ToString()), Orientation.Auto);

            var cols = new Decimator.Column[100];
            Decimator.Build(ds, 0, ds.TimeStart, ds.TimeEnd, cols, 100);

            double max = double.NegativeInfinity;
            int used = 0;
            for (int i = 0; i < 100; i++) { if (!cols[i].HasValue) continue; used++; if (cols[i].Max > max) max = cols[i].Max; }
            Check("모든 열에 값이 들어감", used == 100, "실제 " + used);
            Near("한 번뿐인 꼭짓점이 살아남음", max, 999, 1e-6);

            double lo, hi;
            Check("보이는 구간 범위를 구함", Decimator.RangeIn(ds, 0, 0, 100, out lo, out hi), null);
            Near("그 구간의 최대", hi, 1, 1e-9);
        }

        /// <summary>
        /// 접기를 "열이 바뀔 때 한 번만 쓰기" 로 고친 뒤에도 결과가 같은지.
        ///
        /// 표본마다 배열의 구조체를 읽고 고쳐 쓰던 것을, 지역 변수에 모았다가
        /// 열이 바뀔 때 한 번만 쓰도록 바꿨습니다. 빠르지만 경계 처리를
        /// 틀리기 쉬운 부분이라 따로 못박아 둡니다.
        /// </summary>
        private static void DecimationAccumulatesPerColumn()
        {
            Say("픽셀 열 접기 — 열 경계와 최소/최대");

            // 값이 시간과 함께 0 에서 999 까지 늘어나는 표본 1000 개.
            var sb = new StringBuilder("Time,V\n");
            for (int i = 0; i < 1000; i++) sb.Append(i).Append(',').Append(i).Append('\n');
            LogDataset ds = Open(WriteCsv("decim_acc.csv", sb.ToString()), Orientation.Auto);

            var cols = new Decimator.Column[10];
            Decimator.Build(ds, 0, ds.TimeStart, ds.TimeEnd, cols, 10);

            bool allFilled = true, sane = true, ordered = true;
            float gmin = float.MaxValue, gmax = float.MinValue;
            float prevMax = float.NegativeInfinity;

            for (int i = 0; i < 10; i++)
            {
                if (!cols[i].HasValue) { allFilled = false; continue; }
                if (cols[i].Min > cols[i].Max) sane = false;
                // 값이 계속 커지므로 앞 열의 최대보다 뒤 열의 최소가 큽니다.
                if (cols[i].Min < prevMax) ordered = false;
                prevMax = cols[i].Max;
                if (cols[i].Min < gmin) gmin = cols[i].Min;
                if (cols[i].Max > gmax) gmax = cols[i].Max;
            }

            Check("열 10 개가 모두 참", allFilled, null);
            Check("열마다 최소 <= 최대", sane, null);
            Check("열끼리 값 범위가 겹치지 않음", ordered, null);
            Near("전체 최소", gmin, 0, 1e-6);
            Near("전체 최대 (마지막 표본까지 들어감)", gmax, 999, 1e-6);

            // 구간을 좁혀 잡아도 오른쪽 끝 너머의 한 점을 마지막 열에 넣습니다.
            // 그래야 선이 화면 오른쪽 끝까지 이어집니다.
            var part = new Decimator.Column[10];
            Decimator.Build(ds, 0, 100, 200, part, 10);
            Check("좁힌 구간에서도 마지막 열이 참", part[9].HasValue, null);
            Check("좁힌 구간의 값만 들어감",
                  part[0].Min >= 100 && part[9].Max <= 202,
                  "첫 열 " + part[0].Min + ", 끝 열 " + part[9].Max);
        }

        private static void ZoomedInTraceSurvives()
        {
            Say("확대했을 때 선이 남아 있는지");

            // 표본 100 개. 크게 확대하면 화면 안에 표본이 서너 개뿐입니다.
            var sb = new StringBuilder("Time,V,D\n");
            for (int i = 0; i < 100; i++)
                sb.Append(i).Append(',').Append(i).Append(',').Append(i % 2).Append('\n');
            LogDataset ds = Open(WriteCsv("zoom.csv", sb.ToString()), Orientation.Auto);

            // 10.2 ~ 13.8 구간. 안에 든 표본은 11, 12, 13 뿐입니다.
            double t0 = 10.2, t1 = 13.8;
            int first, last;
            Check("걸치는 표본을 찾음", Decimator.VisibleRange(ds, t0, t1, out first, out last), null);

            // 화면 가장자리에서 선이 잘리지 않도록 양쪽 바깥의 표본 하나씩을
            // 함께 돌려줘야 합니다.
            Check("왼쪽 바깥 표본까지 포함", first == 10, "실제 " + first);
            Check("오른쪽 바깥 표본까지 포함", last == 14, "실제 " + last);

            const int columns = 200;

            // 접어서 그리면 표본이 있는 열만 값이 찹니다. 예전에는 이 상태로
            // 선을 그리면서 빈 열마다 선을 끊어, 표본마다 점 하나짜리 도형이
            // 되어 아무것도 안 보였습니다. 그 상황을 여기서 확인해 둡니다.
            var cols = new Decimator.Column[columns];
            Decimator.Build(ds, 0, t0, t1, cols, columns);
            int filled = 0;
            for (int i = 0; i < columns; i++) if (cols[i].HasValue) filled++;
            Check("접으면 대부분의 열이 빔 (그래서 접어 그리면 안 됨)", filled < columns / 4,
                  "찬 열 " + filled + " / " + columns);

            // 열마다 값을 뽑으면 빈 열이 없습니다. 차이 영역 표시가 이걸 씁니다.
            var band = new float[columns];
            Decimator.SampleColumns(ds, 0, t0, t1, band, columns);
            int holes = 0;
            for (int i = 0; i < columns; i++) if (float.IsNaN(band[i])) holes++;
            Check("열마다 뽑으면 빈 칸 없음", holes == 0, "빈 칸 " + holes);

            Check("왼쪽 끝 값이 구간에 맞음", band[0] >= 10 && band[0] <= 11,
                  band[0].ToString("0.###"));
            Check("오른쪽 끝 값이 구간에 맞음", band[columns - 1] >= 13 && band[columns - 1] <= 14,
                  band[columns - 1].ToString("0.###"));

            bool rising = true;
            for (int i = 1; i < columns; i++) if (band[i] < band[i - 1]) rising = false;
            Check("아날로그는 앞뒤를 보간해 매끈하게", rising, null);

            // 디지털은 보간하면 안 됩니다. 0 과 1 사이의 중간값은 없던 값입니다.
            int d = ds.FindChannel("D");
            Check("D 는 디지털로 읽힘", ds.Channels[d].Kind == ChannelKind.Digital,
                  ds.Channels[d].Kind.ToString());

            var stepBand = new float[columns];
            Decimator.SampleColumns(ds, d, t0, t1, stepBand, columns);
            bool onlyZeroOne = true;
            for (int i = 0; i < columns; i++)
                if (!float.IsNaN(stepBand[i]) && stepBand[i] != 0f && stepBand[i] != 1f) onlyZeroOne = false;
            Check("디지털은 0 과 1 만 (계단 유지)", onlyZeroOne, null);

            // 표본이 픽셀보다 촘촘할 때(표본 100 개, 열 50 개)는 접는 길로 갑니다.
            var wide = new Decimator.Column[50];
            Decimator.Build(ds, 0, ds.TimeStart, ds.TimeEnd, wide, 50);
            int wideFilled = 0;
            for (int i = 0; i < 50; i++) if (wide[i].HasValue) wideFilled++;
            Check("그때는 모든 열이 참", wideFilled == 50, "찬 열 " + wideFilled);
        }

        private static void CompareMetrics()
        {
            Say("두 로그 비교");
            // V 는 뒤쪽 20 개만 1 만큼 벌어집니다 (한 번 크게 틀어진 채널).
            // W 는 한 표본 걸러 1 씩 흔들립니다 (+1/-1 이 반복되는 채널).
            // 최대 차이는 둘 다 1 로 같지만, 구간 수와 시간 비율은 크게 다릅니다.
            var a = new StringBuilder("Time,V,W\n");
            var b = new StringBuilder("Time,V,W\n");
            for (int i = 0; i < 100; i++)
            {
                a.Append(i).Append(",10,0\n");
                b.Append(i).Append(',').Append(i >= 80 ? 11 : 10).Append(',').Append(i % 2).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("cmp_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("cmp_b.csv", b.ToString()), Orientation.Auto);

            var opt = new DiffOptions();
            opt.AbsoluteTolerance = 0.0;
            CompareResult r = DiffEngine.Compare(dsA, dsB, opt, null);

            Check("양쪽에 다 있는 채널 2개", r.CommonCount == 2, "실제 " + r.CommonCount);
            Check("둘 다 달라졌다고 판단", r.ChangedCount == 2, "실제 " + r.ChangedCount);

            ChannelDiff v = r.Items.Find(d => d.Name == "V");
            ChannelDiff w = r.Items.Find(d => d.Name == "W");
            Check("V 와 W 를 모두 찾음", v != null && w != null, null);

            Near("V 최대 차이", v.MaxAbs, 1, 1e-6);
            Near("W 최대 차이", w.MaxAbs, 1, 1e-6);

            // 최대 차이는 같지만, 구간 수로 보면 흔들리는 W 가 훨씬 큽니다.
            // 이게 "차이 총합만 쓰면 안 되는" 이유입니다.
            Check("흔들리는 채널의 구간 수가 훨씬 큼", w.Segments > 10 && w.Segments > v.Segments,
                  "V " + v.Segments + " / W " + w.Segments);
            Check("한 번만 벌어진 채널의 구간 수는 1", v.Segments == 1, "실제 " + v.Segments);
            Check("허용 오차를 넘긴 시간 비율이 V 쪽이 작음", v.TimeRatio < w.TimeRatio,
                  v.TimeRatio.ToString("0.000") + " / " + w.TimeRatio.ToString("0.000"));

            // 허용 오차를 키우면 둘 다 같은 것으로 봅니다.
            opt.AbsoluteTolerance = 1.5;
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, opt, null);
            Check("허용 오차를 넘기면 차이 없음", r2.ChangedCount == 0, "실제 " + r2.ChangedCount);
        }

        private static void ExistenceDifference()
        {
            Say("IO 존재 차이");
            LogDataset a = Open(WriteCsv("ex_a.csv", "Time,A,B\n0,1,2\n1,1,2\n"), Orientation.Auto);
            LogDataset b = Open(WriteCsv("ex_b.csv", "Time,B,C\n0,2,3\n1,2,3\n"), Orientation.Auto);

            CompareResult r = DiffEngine.Compare(a, b, new DiffOptions(), null);
            Check("이전에만 있는 IO 는 A", r.OnlyBefore.Count == 1 && r.OnlyBefore[0].Name == "A",
                  r.OnlyBefore.Count.ToString());
            Check("이후에만 있는 IO 는 C", r.OnlyAfter.Count == 1 && r.OnlyAfter[0].Name == "C",
                  r.OnlyAfter.Count.ToString());
            Check("공통은 B 하나", r.CommonCount == 1, "실제 " + r.CommonCount);
        }

        /// <summary>
        /// 허용 오차는 <b>그 순간의 이전 값 대비 몇 %</b> 입니다.
        /// 설정에 적는 숫자와 화면에 적히는 숫자가 같은 잣대여야 합니다.
        /// </summary>
        private static void ToleranceIsPercentOfBefore()
        {
            Say("허용 오차 — 이전 값 대비 %");

            var opt = new DiffOptions();
            Near("기본값은 0.1%", opt.RelativePercent, 0.1, 1e-9);

            // 계산식 자체를 먼저 못박습니다.
            Near("100 → 101 은 1%", ToleranceRule.ErrorPercent(100, 101), 1, 1e-9);
            Near("6466 → 6467 은 0.0155%", ToleranceRule.ErrorPercent(6466, 6467), 0.015465, 1e-5);
            Near("같으면 0%", ToleranceRule.ErrorPercent(50, 50), 0, 1e-12);
            Near("줄어도 크기로 봅니다", ToleranceRule.ErrorPercent(200, 100), 50, 1e-9);
            Near("0 에서 벗어나면 100%", ToleranceRule.ErrorPercent(0, 1), 100, 1e-9);
            Near("0 에서 0 은 0%", ToleranceRule.ErrorPercent(0, 0), 0, 1e-12);
            Check("한쪽이 없으면 NaN", double.IsNaN(ToleranceRule.ErrorPercent(double.NaN, 1)), null);

            Check("1% 기준에서 1% 는 넘지 않음", !ToleranceRule.IsOver(100, 101, 0, 1), null);
            Check("1% 기준에서 2% 는 넘음", ToleranceRule.IsOver(100, 102, 0, 1), null);
            Check("절대 오차 안이면 퍼센트와 무관하게 같은 값",
                  !ToleranceRule.IsOver(0.001, 0.002, 0.5, 0.1), null);

            // 값 크기가 전혀 다른 두 채널이 같은 퍼센트로 판정되는지.
            //   BIG   : 5000 근처에서 50 만큼 벌어짐  → 1%
            //   SMALL : 5 근처에서 0.05 만큼 벌어짐   → 1%
            // 예전 규칙(채널 값 범위 x 퍼센트)에서는 이 둘이 달랐습니다.
            var a = new StringBuilder("Time,BIG,SMALL\n");
            var b = new StringBuilder("Time,BIG,SMALL\n");
            for (int i = 0; i < 100; i++)
            {
                a.Append(i).Append(",5000,5\n");
                b.Append(i).Append(i >= 50 ? ",5050,5.05\n" : ",5000,5\n");
            }
            LogDataset dsA = Open(WriteCsv("tol_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("tol_b.csv", b.ToString()), Orientation.Auto);

            var o2 = new DiffOptions();
            o2.RelativePercent = 2.0;                 // 2% → 둘 다 안 걸림
            CompareResult r = DiffEngine.Compare(dsA, dsB, o2, null);
            Check("2% 기준에서는 둘 다 차이 아님", r.ChangedCount == 0, "실제 " + r.ChangedCount);

            o2.RelativePercent = 0.5;                 // 0.5% → 둘 다 걸림
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, o2, null);
            Check("0.5% 기준에서는 둘 다 차이", r2.ChangedCount == 2, "실제 " + r2.ChangedCount);

            ChannelDiff big = r2.Items.Find(d => d.Name == "BIG");
            ChannelDiff small = r2.Items.Find(d => d.Name == "SMALL");
            Check("두 채널 모두 찾음", big != null && small != null, null);
            if (big == null || small == null) return;

            Near("BIG 의 최대 오차는 1%", big.MaxPercent, 1, 0.01);
            Near("SMALL 의 최대 오차도 1%", small.MaxPercent, 1, 0.01);
            Check("값 크기가 달라도 같은 퍼센트",
                  Math.Abs(big.MaxPercent - small.MaxPercent) < 0.01,
                  big.MaxPercent + " / " + small.MaxPercent);

            // 절대 오차는 0 언저리에서 퍼센트가 튀는 것을 막는 탈출구입니다.
            var o3 = new DiffOptions();
            o3.RelativePercent = 0.5;
            o3.AbsoluteTolerance = 100;               // 50 은 이 안쪽
            CompareResult r3 = DiffEngine.Compare(dsA, dsB, o3, null);
            ChannelDiff big3 = r3.Items.Find(d => d.Name == "BIG");
            Check("절대 오차 안이면 퍼센트를 넘어도 차이 아님",
                  big3 != null && !big3.Changed, null);
        }

        /// <summary>
        /// 목록에 적히는 "최대 오차 %" 와 "차이 났다" 는 판정이 어긋나지
        /// 않아야 합니다. 둘 다 같은 순간에서 나오기 때문입니다.
        /// </summary>
        private static void ChangedMatchesMaxPercent()
        {
            Say("허용 오차 — 목록의 숫자와 판정이 맞는지");

            // 한 표본만 5% 벌어집니다. 나머지는 똑같습니다.
            var a = new StringBuilder("Time,V\n");
            var b = new StringBuilder("Time,V\n");
            for (int i = 0; i < 200; i++)
            {
                a.Append(i).Append(",1000\n");
                b.Append(i).Append(i == 150 ? ",1050\n" : ",1000\n");
            }
            LogDataset dsA = Open(WriteCsv("tol_one_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("tol_one_b.csv", b.ToString()), Orientation.Auto);

            var opt = new DiffOptions();
            opt.RelativePercent = 1.0;
            CompareResult r = DiffEngine.Compare(dsA, dsB, opt, null);
            ChannelDiff v = r.Items.Find(d => d.Name == "V");
            Check("채널을 찾음", v != null, null);
            if (v == null) return;

            Near("최대 오차 5%", v.MaxPercent, 5, 0.01);
            Check("5% > 1% 이므로 차이", v.Changed, null);
            Check("한 표본만 넘음", v.DiffSamples == 1, "실제 " + v.DiffSamples);

            // 기준을 올리면 판정이 뒤집히지만 적히는 숫자는 그대로여야
            // 합니다. 재는 값이 재는 잣대에 딸려 있으면 안 됩니다.
            opt.RelativePercent = 10.0;
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, opt, null);
            ChannelDiff v2 = r2.Items.Find(d => d.Name == "V");
            Check("기준을 올리면 차이 아님", v2 != null && !v2.Changed, null);
            Near("그래도 최대 오차는 5% 그대로", v2.MaxPercent, 5, 0.01);
        }

        private static void HeatmapBuckets()
        {
            Say("히트맵 — 1분 칸으로 자르기");

            // 5 분치, 1 초 간격. 2 분대(120~179초)에서만 SMALL 이 10 만큼 벌어집니다.
            var a = new StringBuilder("Time,SMALL\n");
            var b = new StringBuilder("Time,SMALL\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                a.Append(t).Append(",100\n");
                b.Append(t).Append(',').Append(i >= 120 && i < 180 ? 110 : 100).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_b.csv", b.ToString()), Orientation.Auto);

            Check("시각으로 읽음", dsA.TimeKind == TimeKind.ClockMs, dsA.TimeKind.ToString());

            var opt = new HeatmapOptions();
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Near("칸 폭이 1분", r.BucketSpan, 60000, 1e-6);
            Check("칸 5개", r.BucketCount == 5, "실제 " + r.BucketCount);
            Check("차이가 난 IO 한 줄", r.Rows.Count == 1, "실제 " + r.Rows.Count);

            HeatRow row = r.Rows[0];
            Check("2분대 칸만 차이 발생", row.OverBuckets == 1, "실제 " + row.OverBuckets);
            Check("차이가 난 칸은 세 번째 (0부터)", HeatmapBuilder.IsOver(row, 2), null);
            Check("1분대는 정상", !HeatmapBuilder.IsOver(row, 1), null);
            Check("3분대는 정상", !HeatmapBuilder.IsOver(row, 3), null);

            // 칸에 적히는 값은 "가장 크게 벌어진 순간" 입니다. 여기서는
            // 2 분대 60 표본이 모두 10 만큼 벌어져서 평균과 같습니다.
            Near("2분대 평균 차이", row.Cells[2].Mean, 10, 1e-3);
            Near("2분대 칸에 적히는 값", row.ValueOf(row.Cells[2]), 10, 1e-3);
            Near("2분대 칸의 오차", row.PercentOf(row.Cells[2]), 10, 1e-3);   // 10 / 100
            Near("1분대 평균 차이", row.Cells[1].Mean, 0, 1e-6);
            Check("1분대 칸에 적히는 값은 0", row.ValueOf(row.Cells[1]) == 0, null);
            Check("1분대에도 기록은 있음", row.Cells[1].HasData, null);
        }

        /// <summary>
        /// 히트맵도 "이전 값 대비 몇 %" 로 판정하는지.
        /// </summary>
        private static void HeatmapRelativeTolerance()
        {
            Say("히트맵 — 이전 값 대비 %로 판정");

            // 1000 에 있다가 2 분대에서만 1002 가 됩니다 → 오차 0.2%.
            var a = new StringBuilder("Time,V\n");
            var b = new StringBuilder("Time,V\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                a.Append(t).Append(",1000\n");
                b.Append(t).Append(i >= 120 && i < 180 ? ",1002\n" : ",1000\n");
            }
            LogDataset dsA = Open(WriteCsv("heat_rel_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_rel_b.csv", b.ToString()), Orientation.Auto);

            var opt = new HeatmapOptions();
            opt.RelativePercent = 0.5;          // 0.2% < 0.5% → 차이 아님
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("허용 오차 안쪽은 차이로 세지 않음", r.Rows.Count == 0, "실제 " + r.Rows.Count);

            opt.RelativePercent = 0.1;          // 0.2% > 0.1% → 차이
            HeatmapResult r2 = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("기준을 낮추면 잡힘", r2.Rows.Count == 1, "실제 " + r2.Rows.Count);
            if (r2.Rows.Count == 1)
            {
                HeatRow row = r2.Rows[0];
                Check("2분대 한 칸만", row.OverBuckets == 1, "실제 " + row.OverBuckets);
                Near("그 칸의 오차", row.PercentOf(row.Cells[2]), 0.2, 1e-3);
                Near("값으로 보면 2", row.ValueOf(row.Cells[2]), 2, 1e-3);
            }

            // 차이가 없는 IO 도 보기로 하면 줄이 나옵니다.
            opt.RelativePercent = 0.5;
            opt.IncludeUnchanged = true;
            HeatmapResult r3 = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("차이 없는 IO도 보기", r3.Rows.Count == 1 && r3.Rows[0].OverBuckets == 0,
                  "줄 " + r3.Rows.Count);
        }

        /// <summary>
        /// <b>허용 오차를 바꿔도 칸에 적히는 숫자는 그대로여야 합니다.</b>
        ///
        /// 예전에는 칸에 "기준을 넘은 표본만의 평균" 을 적었습니다. 기준을
        /// 바꾸면 평균 낼 표본이 바뀌어서 숫자가 따라 움직였고, 그래서
        /// 설정한 퍼센트와 화면의 퍼센트가 계속 어긋나 보였습니다.
        /// 지금은 "가장 크게 벌어진 순간" 이라 잣대와 무관합니다.
        /// </summary>
        private static void HeatmapCellNumberDoesNotFollowTolerance()
        {
            Say("히트맵 — 허용 오차를 바꿔도 칸의 숫자는 그대로");

            // 2 분대 60 표본 중 한 표본만 5% 벌어지고, 나머지 59 개는 0.5%.
            var a = new StringBuilder("Time,V\n");
            var b = new StringBuilder("Time,V\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                a.Append(t).Append(",1000\n");
                string after = "1000";
                if (i >= 120 && i < 180) after = (i == 150) ? "1050" : "1005";
                b.Append(t).Append(',').Append(after).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_stable_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_stable_b.csv", b.ToString()), Orientation.Auto);

            // 기준 0.1% → 60 표본 모두 넘습니다.
            var loose = new HeatmapOptions();
            loose.RelativePercent = 0.1;
            HeatmapResult r1 = HeatmapBuilder.Build(dsA, dsB, loose, null);

            // 기준 1% → 5% 짜리 한 표본만 넘습니다.
            var tight = new HeatmapOptions();
            tight.RelativePercent = 1.0;
            HeatmapResult r2 = HeatmapBuilder.Build(dsA, dsB, tight, null);

            Check("두 기준 모두 한 줄", r1.Rows.Count == 1 && r2.Rows.Count == 1,
                  r1.Rows.Count + " / " + r2.Rows.Count);
            if (r1.Rows.Count != 1 || r2.Rows.Count != 1) return;

            HeatRow a1 = r1.Rows[0], a2 = r2.Rows[0];

            // 넘은 표본 수는 기준에 따라 달라집니다 (당연합니다).
            Check("0.1% 에서는 60 표본이 넘음", a1.Cells[2].OverSamples == 60,
                  "실제 " + a1.Cells[2].OverSamples);
            Check("1% 에서는 1 표본만 넘음", a2.Cells[2].OverSamples == 1,
                  "실제 " + a2.Cells[2].OverSamples);

            // 그런데 칸에 적히는 숫자는 양쪽이 같아야 합니다.
            Near("0.1% 에서 칸의 오차", a1.PercentOf(a1.Cells[2]), 5, 1e-3);
            Near("1% 에서 칸의 오차", a2.PercentOf(a2.Cells[2]), 5, 1e-3);
            Check("기준을 바꿔도 칸의 숫자가 그대로",
                  Math.Abs(a1.PercentOf(a1.Cells[2]) - a2.PercentOf(a2.Cells[2])) < 1e-6, null);

            // 값으로 봐도 같은 순간이라 같은 숫자입니다.
            Near("값으로 본 칸", a1.ValueOf(a1.Cells[2]), 50, 1e-3);
            Check("값으로 봐도 그대로",
                  Math.Abs(a1.ValueOf(a1.Cells[2]) - a2.ValueOf(a2.Cells[2])) < 1e-6, null);

            // 그리고 빨간 칸의 숫자는 늘 허용 오차보다 큽니다.
            Check("칸의 숫자 > 허용 오차 (0.1%)", a1.PercentOf(a1.Cells[2]) > 0.1, null);
            Check("칸의 숫자 > 허용 오차 (1%)", a2.PercentOf(a2.Cells[2]) > 1.0, null);

            // 정상 칸은 0 입니다.
            Check("1분대는 정상", a1.PercentOf(a1.Cells[1]) == 0, null);
        }

        /// <summary>
        /// 이름으로 견주는 줄은 허용 오차와 무관하게 잡히고, 오차는 100% 입니다.
        /// 절대 오차를 크게 잡아도 상태가 바뀐 것을 놓치면 안 됩니다.
        /// </summary>
        private static void HeatmapStateNamesIgnoreTolerance()
        {
            Say("히트맵 — 상태 이름은 허용 오차와 무관");

            var a = new StringBuilder("Time,상태\n");
            var b = new StringBuilder("Time,상태\n");
            for (int i = 0; i < 180; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                a.Append(t).Append(",IDLE\n");
                b.Append(t).Append(i >= 120 ? ",RUN\n" : ",IDLE\n");
            }
            LogDataset dsA = Open(WriteCsv("heat_state_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_state_b.csv", b.ToString()), Orientation.Auto);

            var opt = new HeatmapOptions();
            opt.AbsoluteTolerance = 1000;       // 아주 크게 잡아도
            opt.RelativePercent = 100;          // 퍼센트도 최대로 잡아도
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("허용 오차를 아무리 키워도 이름 차이는 잡힘", r.Rows.Count == 1,
                  "실제 " + r.Rows.Count);
            if (r.Rows.Count != 1) return;

            HeatRow row = r.Rows[0];
            Check("이름으로 견준 줄", row.ByName, null);
            Near("오차는 100%", row.PercentOf(row.Cells[2]), 100, 1e-6);
            Check("1 분대는 정상", row.PercentOf(row.Cells[1]) == 0, null);
        }

        /// <summary>
        /// 세로 눈금에 적을 단위가 두 갈래 모두에서 들어오는지.
        ///  (1) 머리 행 아래의 단위 줄
        ///  (2) 이름 끝의 대괄호/소괄호
        /// 이름 자체는 엑셀에 적힌 그대로여야 합니다.
        /// </summary>
        private static void UnitsFromUnitRowAndName()
        {
            Say("세로축 단위");

            // (1) 단위 줄이 따로 있는 경우
            string p1 = WriteCsv("unit_row.csv",
                "Time,PT01,TT01\n" +
                "sec,bar,degC\n" +
                "0.0,1,20\n" +
                "0.1,2,21\n" +
                "0.2,3,22\n");
            LogDataset a = Open(p1, Orientation.Auto);

            Check("단위 줄을 값으로 읽지 않음", a.SampleCount == 3, "실제 " + a.SampleCount);
            Check("단위 줄에서 단위를 가져옴",
                  a.Channels[a.FindChannel("PT01")].Unit == "bar",
                  "실제 '" + a.Channels[a.FindChannel("PT01")].Unit + "'");
            Check("둘째 열의 단위도", a.Channels[a.FindChannel("TT01")].Unit == "degC", null);

            // (2) 단위 줄이 없고 이름 끝에 붙은 경우
            string p2 = WriteCsv("unit_name.csv",
                "Time,압력 PT-01 [bar],밸브(2)\n" +
                "0.0,1,0\n" +
                "0.1,2,1\n" +
                "0.2,3,1\n");
            LogDataset b = Open(p2, Orientation.Auto);

            Check("이름은 엑셀에 적힌 그대로", b.FindChannel("압력 PT-01 [bar]") >= 0, null);
            Check("이름 끝 대괄호에서 단위를 가져옴",
                  b.Channels[b.FindChannel("압력 PT-01 [bar]")].Unit == "bar",
                  "실제 '" + b.Channels[b.FindChannel("압력 PT-01 [bar]")].Unit + "'");
            Check("괄호 안이 숫자면 단위가 아님",
                  b.Channels[b.FindChannel("밸브(2)")].Unit.Length == 0,
                  "실제 '" + b.Channels[b.FindChannel("밸브(2)")].Unit + "'");
        }

        /// <summary>
        /// 칸 폭을 1 분에서 5 분으로 바꿔도 IO 차례가 같아야 합니다.
        ///
        /// 예전에는 "칸들 중 가장 큰 평균" 과 "차이 난 칸 수" 로 줄을 세웠는데,
        /// 둘 다 칸을 어떻게 잘랐는지에 딸린 값이라 폭을 바꾸면 차례가
        /// 뒤바뀌었습니다.
        /// </summary>
        private static void HeatmapOrderIsSameForEveryBucketWidth()
        {
            Say("히트맵 — 칸 폭을 바꿔도 IO 차례가 같은지");

            // 세 채널. 벌어지는 모양을 일부러 다르게 둡니다.
            //   SPIKE : 딱 한 순간만 크게 (한 칸 안에서도 평균이 묻히는 모양)
            //   WIDE  : 오래 조금씩 (칸을 넓히면 평균이 잘 살아남는 모양)
            //   SMALL : 아주 조금
            // 세 채널 모두 0~100 을 오르내려서 값 범위가 같습니다. 그래야
            // 차례를 정하는 것이 "얼마나 벌어졌나" 하나로 좁혀집니다.
            var a = new StringBuilder("Time,SPIKE,WIDE,SMALL\n");
            var b = new StringBuilder("Time,SPIKE,WIDE,SMALL\n");
            for (int i = 0; i < 600; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                double v = i * (100.0 / 599.0);
                string vs = v.ToString("0.###", CultureInfo.InvariantCulture);
                a.Append(t).Append(',').Append(vs).Append(',').Append(vs)
                 .Append(',').Append(vs).Append('\n');

                double spike = v + (i == 130 ? 40.0 : 0.0);   // 한 표본만 크게
                double wide = v + (i >= 60 && i < 360 ? 8.0 : 0.0);   // 오래 조금씩
                double small = v + (i >= 200 && i < 260 ? 2.0 : 0.0);  // 잠깐 아주 조금
                b.Append(t).Append(',')
                 .Append(spike.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                 .Append(wide.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                 .Append(small.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_order_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_order_b.csv", b.ToString()), Orientation.Auto);

            var one = new HeatmapOptions();
            one.BucketSpan = 60000.0;           // 1 분
            HeatmapResult r1 = HeatmapBuilder.Build(dsA, dsB, one, null);

            var five = new HeatmapOptions();
            five.BucketSpan = 5 * 60000.0;      // 5 분
            HeatmapResult r5 = HeatmapBuilder.Build(dsA, dsB, five, null);

            Check("두 폭 모두 세 줄", r1.Rows.Count == 3 && r5.Rows.Count == 3,
                  "1분 " + r1.Rows.Count + ", 5분 " + r5.Rows.Count);
            if (r1.Rows.Count != 3 || r5.Rows.Count != 3) return;

            string n1 = r1.Rows[0].Name + "," + r1.Rows[1].Name + "," + r1.Rows[2].Name;
            string n5 = r5.Rows[0].Name + "," + r5.Rows[1].Name + "," + r5.Rows[2].Name;
            Check("칸 폭이 달라도 IO 차례가 같음", n1 == n5, "1분 [" + n1 + "], 5분 [" + n5 + "]");

            // 가장 크게 벌어진 순간(오차 %)이 큰 쪽이 위입니다. WIDE 는 벌어진
            // 표본이 300 개로 훨씬 많지만, 벌어진 정도는 SPIKE 가 훨씬 큽니다.
            Check("제일 크게 튄 IO 가 맨 위", n1 == "SPIKE,WIDE,SMALL", "실제 " + n1);

            // 차례를 정하는 값 자체가 칸 폭과 무관한지도 직접 봅니다.
            for (int i = 0; i < 3; i++)
            {
                HeatRow x = r1.Rows[i], y = r5.Rows[i];
                Near(x.Name + " 의 최대 오차가 폭과 무관", x.PeakMax, y.PeakMax, 1e-6);
                Check(x.Name + " 의 넘은 표본 수가 폭과 무관",
                      x.TotalOverSamples == y.TotalOverSamples,
                      "1분 " + x.TotalOverSamples + ", 5분 " + y.TotalOverSamples);
            }

            // 반대로, 칸 폭에 딸린 값은 실제로 달라집니다 — 그래서 정렬에
            // 쓰면 안 된다는 것을 여기서 못박아 둡니다.
            Check("차이 난 칸 수는 폭에 따라 달라짐",
                  r1.Rows[0].OverBuckets != r5.Rows[0].OverBuckets
                  || r1.Rows[1].OverBuckets != r5.Rows[1].OverBuckets,
                  "1분/5분 칸 수가 같게 나왔습니다");
        }

        /// <summary>
        /// 숫자를 글자로 바꿀 때 지수 표기(1.2e+07)가 나오지 않아야 합니다.
        /// .NET 의 "G4" 가 자리 수에 따라 멋대로 지수로 바꾸던 것을 막은 것이라,
        /// 경계가 되는 값들을 직접 넣어 봅니다.
        /// </summary>
        private static void NumberTextHasNoExponent()
        {
            Say("숫자 표기 — 지수 표기 안 씀");

            double[] probes =
            {
                0, 1, -1, 0.5, 12.34, 999.99, 1000, 123456, 999999,
                1000000, 12345678, 1.5e9, -2.5e7,
                0.001, 0.0005, 0.000123, 1e-6, -1e-6, 1e-9,
            };

            bool clean = true;
            string bad = null;
            for (int i = 0; i < probes.Length; i++)
            {
                string a = NumberText.Plain(probes[i]);
                string b = NumberText.Short(probes[i]);
                if (HasExponent(a)) { clean = false; bad = probes[i] + " -> " + a; break; }
                if (HasExponent(b)) { clean = false; bad = probes[i] + " -> " + b; break; }
            }
            Check("어떤 값에서도 e / E 가 안 나옴", clean, bad);

            Check("큰 수는 천 단위로 끊음", NumberText.Plain(12345678) == "12,345,678",
                  "실제 " + NumberText.Plain(12345678));
            Check("작은 수도 자리를 살림", NumberText.Plain(0.000123) == "0.000123",
                  "실제 " + NumberText.Plain(0.000123));
            Check("0 은 그냥 0", NumberText.Plain(0) == "0", "실제 " + NumberText.Plain(0));

            // 대시보드 목록도 같은 서식을 씁니다.
            var d = new ChannelDiff();
            d.MaxAbs = 1234567.0;
            Check("대시보드 칸도 지수 표기 안 씀",
                  !HasExponent(d.Format(DiffMetric.MaxAbs)), d.Format(DiffMetric.MaxAbs));
        }

        private static bool HasExponent(string s)
        {
            return s != null && (s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0);
        }

        /// <summary>
        /// 특정 IO 가 바뀌는 순간으로 두 로그의 시간축을 맞춥니다.
        /// 시작 시각이 달라도 사건끼리 겹쳐야 합니다.
        /// </summary>
        private static void TriggerAlignOnEdge()
        {
            Say("시간 맞추기 — IO 가 바뀌는 순간 기준");

            // 이전 로그: 0 초에 시작, 30 초에 START 가 0 → 1
            // 이후 로그: 100 초에 시작, 145 초에 START 가 0 → 1
            //   시작 시각 차이는 100, 사건 시각 차이는 115 입니다.
            //   사건으로 맞추면 밀기 값이 30 - 145 = -115 여야 합니다.
            var a = new StringBuilder("Time,START,ANALOG\n");
            for (int i = 0; i < 100; i++)
                a.Append(i).Append(',').Append(i >= 30 ? 1 : 0)
                           .Append(',').Append(i >= 30 ? 80 : 20).Append('\n');

            var b = new StringBuilder("Time,START,ANALOG\n");
            for (int i = 0; i < 100; i++)
                b.Append(100 + i).Append(',').Append(i >= 45 ? 1 : 0)
                                 .Append(',').Append(i >= 45 ? 80 : 20).Append('\n');

            LogDataset dsA = Open(WriteCsv("align_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("align_b.csv", b.ToString()), Orientation.Auto);

            AlignResult r = TriggerAlign.Compute(dsA, dsB, "START", EdgeKind.Rising, 1, double.NaN);
            Check("맞춰짐", r.Ok, r.Message);
            if (!r.Ok) return;

            Near("이전 로그의 변화 시각", r.Before.Time, 30, 1e-9);
            Near("이후 로그의 변화 시각", r.After.Time, 145, 1e-9);
            Near("밀기 값", r.Shift, -115, 1e-9);

            // "이후 시각 + 밀기 = 이전 시각" 이라는 약속이 지켜지는지.
            Near("사건이 겹침", r.After.Time + r.Shift, r.Before.Time, 1e-9);

            // 시작 시각으로 맞추면 100 이 나옵니다 — 사건은 15 만큼 어긋납니다.
            Near("시작 시각 맞추기는 다른 값", dsA.TimeStart - dsB.TimeStart, -100, 1e-9);

            // 아날로그 이름을 달았어도 값이 20 과 80 둘뿐이라, 문턱을 잡지 않고
            // 그 값 그대로 봅니다. 알아서 고르는 기준 값은 1 이 없으니 최댓값 80.
            AlignResult r2 = TriggerAlign.Compute(dsA, dsB, "ANALOG", EdgeKind.Rising, 1, double.NaN);
            Check("아날로그로도 맞춰짐", r2.Ok, r2.Message);
            if (r2.Ok) Near("아날로그 밀기 값도 같음", r2.Shift, -115, 1e-9);
            LevelSet two = TriggerAlign.LevelsOf(dsA, dsA.FindChannel("ANALOG"));
            Check("값이 둘뿐이면 문턱을 안 씀", !two.Thresholded, null);
            Near("알아서 고르는 값은 최댓값", two.Fallback(), 80, 1e-9);

            // 내려가는 쪽만 찾으면 없습니다 (한 번 올라가고 끝이므로).
            AlignResult r3 = TriggerAlign.Compute(dsA, dsB, "START", EdgeKind.Falling, 1, double.NaN);
            Check("내려가는 변화는 없음", !r3.Ok, r3.Message);

            // 값 종류가 많은 진짜 아날로그는 문턱 하나로 위/아래를 가릅니다.
            // RAMP 는 0 에서 99 까지 1 씩 올라 100 가지라 MaxLevels(64)를 넘습니다.
            var c = new StringBuilder("Time,RAMP\n");
            var d = new StringBuilder("Time,RAMP\n");
            for (int i = 0; i < 100; i++)
            {
                c.Append(i).Append(',').Append(i).Append('\n');
                d.Append(500 + i).Append(',').Append(i).Append('\n');
            }
            LogDataset dsC = Open(WriteCsv("align_ramp_a.csv", c.ToString()), Orientation.Auto);
            LogDataset dsD = Open(WriteCsv("align_ramp_b.csv", d.ToString()), Orientation.Auto);

            LevelSet ramp = TriggerAlign.LevelsOf(dsC, dsC.FindChannel("RAMP"));
            Check("값이 많으면 문턱으로 가름", ramp.Thresholded, null);
            Near("문턱은 값 범위의 한가운데", ramp.Threshold, 49.5, 1e-9);
            Check("고를 값은 0 과 1 둘뿐", ramp.Values.Length == 2, ramp.Values.Length.ToString());

            AlignResult ramped = TriggerAlign.Compute(dsC, dsD, "RAMP", EdgeKind.Rising, 1, 1);
            Check("문턱으로도 맞춰짐", ramped.Ok, ramped.Message);
            if (ramped.Ok)
            {
                Near("문턱을 넘은 시각", ramped.Before.Time, 50, 1e-9);
                Near("문턱값이 결과에 실림", ramped.Before.Level, 49.5, 1e-9);
                Near("밀기 값", ramped.Shift, -500, 1e-9);
            }
        }

        /// <summary>
        /// 기록을 늦게 건 로그는 그 IO 가 <b>이미 1 인 채로</b> 시작합니다.
        /// 그때 "1 이 되는 순간" 은 그 로그의 첫 표본입니다. 이걸 놓치면
        /// 맞출 수 있는 로그를 못 맞춥니다.
        /// </summary>
        private static void TriggerAlignCountsStartAsEdge()
        {
            Say("시간 맞추기 — 처음부터 그 값인 로그");

            // 이전 로그: 0 초 시작, 30 초에 START 가 0 → 1
            // 이후 로그: 200 초 시작, 처음부터 끝까지 START 가 1
            //   맞추면 200 이 30 으로 와야 하므로 밀기 값은 30 - 200 = -170.
            var a = new StringBuilder("Time,START\n");
            for (int i = 0; i < 100; i++) a.Append(i).Append(',').Append(i >= 30 ? 1 : 0).Append('\n');

            var b = new StringBuilder("Time,START\n");
            for (int i = 0; i < 100; i++) b.Append(200 + i).Append(",1\n");

            LogDataset dsA = Open(WriteCsv("align_start_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("align_start_b.csv", b.ToString()), Orientation.Auto);

            AlignResult r = TriggerAlign.Compute(dsA, dsB, "START", EdgeKind.Rising, 1, 1);
            Check("처음부터 1 인 로그도 맞춰짐", r.Ok, r.Message);
            if (!r.Ok) return;

            Near("이후 로그는 첫 표본이 그 순간", r.After.Time, 200, 1e-9);
            Check("처음부터였다고 표시", r.After.AtStart, null);
            Check("이전 로그는 진짜 변화", !r.Before.AtStart, null);
            Near("밀기 값", r.Shift, -170, 1e-9);

            // 안 바뀌는 쪽이라도 반대쪽이 바뀌면 후보에 올라야 합니다.
            Check("한쪽만 바뀌어도 후보",
                  TriggerAlign.CanTrigger(dsA, dsA.FindChannel("START"))
                  || TriggerAlign.CanTrigger(dsB, dsB.FindChannel("START")), null);

            // "아무 변화" 로는 못 맞춥니다 — 이후 로그에 변화가 없으니까요.
            AlignResult any = TriggerAlign.Compute(dsA, dsB, "START", EdgeKind.Any, 1, double.NaN);
            Check("아무 변화로는 못 맞춤", !any.Ok, any.Message);
        }

        /// <summary>
        /// 0/1 만이 아니라 그 IO 에 나온 값이면 무엇이든 기준이 됩니다.
        /// 0~5 로 오르는 단계 신호는 "5 가 되는 순간" 으로도 맞춰야 합니다.
        /// </summary>
        private static void TriggerAlignOnAnyLevel()
        {
            Say("시간 맞추기 — 0/1 말고 최댓값까지");

            // STEP 은 10 표본마다 한 단계씩 0 → 5 로 오릅니다.
            //   이전 로그: i 초,      5 가 되는 시각 = 50
            //   이후 로그: 300 + i 초, 5 가 되는 시각 = 350
            var a = new StringBuilder("Time,STEP\n");
            var b = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 70; i++)
            {
                int step = i / 10; if (step > 5) step = 5;
                a.Append(i).Append(',').Append(step).Append('\n');
                b.Append(300 + i).Append(',').Append(step).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("align_step_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("align_step_b.csv", b.ToString()), Orientation.Auto);

            LevelSet ls = TriggerAlign.LevelsOf(dsA, dsA.FindChannel("STEP"));
            Check("고를 수 있는 값이 6 개", ls.Values.Length == 6, ls.Values.Length.ToString());
            Check("값 그대로 보는 채널", !ls.Thresholded, null);
            Near("최댓값은 5", ls.Values[5], 5, 1e-9);

            AlignResult five = TriggerAlign.Compute(dsA, dsB, "STEP", EdgeKind.Rising, 1, 5);
            Check("최댓값으로 맞춰짐", five.Ok, five.Message);
            if (five.Ok)
            {
                Near("이전 로그에서 5 가 된 시각", five.Before.Time, 50, 1e-9);
                Near("이후 로그에서 5 가 된 시각", five.After.Time, 350, 1e-9);
                Near("밀기 값", five.Shift, -300, 1e-9);
            }

            // 중간 단계로도 맞춰지고, 그 값이 결과에 반영돼야 합니다.
            AlignResult three = TriggerAlign.Compute(dsA, dsB, "STEP", EdgeKind.Rising, 1, 3);
            Check("중간 값으로도 맞춰짐", three.Ok, three.Message);
            if (three.Ok) Near("3 이 된 시각", three.Before.Time, 30, 1e-9);

            // 벗어나는 순간: 3 에서 4 로 가는 자리(=40)입니다.
            AlignResult off = TriggerAlign.Compute(dsA, dsB, "STEP", EdgeKind.Falling, 1, 3);
            Check("벗어나는 순간도 잡힘", off.Ok, off.Message);
            if (off.Ok) Near("3 에서 벗어난 시각", off.Before.Time, 40, 1e-9);

            // 없는 값을 고르면 조용히 다른 값으로 바꿔치기하지 않습니다.
            AlignResult ghost = TriggerAlign.Compute(dsA, dsB, "STEP", EdgeKind.Rising, 1, 9);
            Check("없는 값으로는 못 맞춤", !ghost.Ok, ghost.Message);
        }

        /// <summary>
        /// 기준이 될 수 없는 IO 는 조용히 넘어가지 않고 이유를 돌려줘야 합니다.
        /// </summary>
        private static void TriggerAlignRefusesBadIo()
        {
            Say("시간 맞추기 — 못 맞출 때 이유를 돌려주는지");

            var a = new StringBuilder("Time,FLAT,ONLYHERE\n");
            var b = new StringBuilder("Time,FLAT\n");
            for (int i = 0; i < 50; i++)
            {
                a.Append(i).Append(",1,").Append(i % 2).Append('\n');
                b.Append(i).Append(",1\n");
            }
            LogDataset dsA = Open(WriteCsv("align_bad_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("align_bad_b.csv", b.ToString()), Orientation.Auto);

            AlignResult flat = TriggerAlign.Compute(dsA, dsB, "FLAT", EdgeKind.Any, 1, double.NaN);
            Check("안 바뀌는 IO 로는 못 맞춤", !flat.Ok, null);
            Check("이유를 알려 줌", flat.Message.Length > 0, null);

            AlignResult missing = TriggerAlign.Compute(dsA, dsB, "ONLYHERE", EdgeKind.Any, 1, double.NaN);
            Check("한쪽에만 있는 IO 로는 못 맞춤", !missing.Ok, null);
            Check("어느 쪽에 없는지 알려 줌", missing.Message.Contains("이후 로그"), missing.Message);

            AlignResult none = TriggerAlign.Compute(dsA, dsB, "", EdgeKind.Any, 1, double.NaN);
            Check("IO 를 안 고르면 그렇게 알려 줌", !none.Ok, null);

            // 후보 추리기: FLAT 은 안 바뀌므로 빠져야 합니다.
            int flatCh = dsA.FindChannel("FLAT");
            Check("안 바뀌는 IO 는 후보가 아님", !TriggerAlign.CanTrigger(dsA, flatCh), null);
        }

        /// <summary>
        /// 로그의 시간 열 대신 다른 IO 를 가로축으로 끼웁니다.
        /// 값이 뒤로 가는 IO 는 거부해야 합니다 — 이진 탐색과 접기가
        /// 오름차순을 전제하기 때문입니다.
        /// </summary>
        /// <summary>
        /// 차이 (이후−이전) = <b>두 로그의 차이</b>. 선 하나입니다.
        /// 같으면 0, 이후가 1 크면 +1, 1 작으면 -1.
        /// </summary>
        private static void DifferenceOfTwoLogs()
        {
            Say("차이 — 두 로그의 차이");

            // 두 로그가 같은 격자(0..9)에 있고 V 만 다릅니다.
            //   이전 10,10,10,10,10,10,10,10,10,10
            //   이후 10,11,10, 9,10,10,12,10,10,10
            //   차이  0,+1, 0,-1, 0, 0,+2, 0, 0, 0
            var b = new StringBuilder("Time,V\n");
            var a = new StringBuilder("Time,V\n");
            int[] av = { 10, 11, 10, 9, 10, 10, 12, 10, 10, 10 };
            for (int i = 0; i < 10; i++)
            {
                b.Append(i).Append(",10\n");
                a.Append(i).Append(',').Append(av[i]).Append('\n');
            }
            LogDataset dsB = Open(WriteCsv("two_b.csv", b.ToString()), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("two_a.csv", a.ToString()), Orientation.Auto);

            int bi = dsB.FindChannel("V"), ai = dsA.FindChannel("V");

            Near("같은 자리는 0", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 0), 0, 1e-6);
            Near("이후가 1 크면 +1", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 1), 1, 1e-6);
            Near("이후가 1 작으면 -1", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 3), -1, 1e-6);
            Near("이후가 2 크면 +2", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 6), 2, 1e-6);

            double lo, hi;
            Check("차이 폭이 나옴",
                  DifferenceSeries.RangeIn(dsB, bi, dsA, ai, 0, 0, 9, out lo, out hi), null);
            Near("차이 최소", lo, -1, 1e-6);
            Near("차이 최대", hi, 2, 1e-6);

            // 표본 그대로 뽑기 (성긴 경우). 격자가 같으니 10 점입니다.
            var cols = new Decimator.Column[64];
            var t = new double[256];
            var v = new float[256];
            int n = DifferenceSeries.Build(dsB, bi, dsA, ai, 0, 0, 9, cols, 64, t, v, 256);
            Check("표본 10 개", n == 10, "실제 " + n);
            Near("넷째 표본의 차이", v[3], -1, 1e-6);
            Near("일곱째 표본의 차이", v[6], 2, 1e-6);

            // 자리가 모자라면 -1 을 돌려주고, 접은 쪽은 그래도 채워집니다.
            int small = DifferenceSeries.Build(dsB, bi, dsA, ai, 0, 0, 9, cols, 64, t, v, 3);
            Check("자리가 모자라면 -1", small == -1, "실제 " + small);
            bool anyCol = false;
            for (int i = 0; i < 64; i++) if (cols[i].HasValue) { anyCol = true; break; }
            Check("그래도 접은 쪽은 채워짐", anyCol, null);
        }

        /// <summary>
        /// 두 로그의 시간 격자가 달라도 됩니다. 한쪽에만 있는 표본에서도
        /// 반대쪽 값을 읽어 견줍니다 — 이전 로그 격자만 쓰면 이후 로그에서만
        /// 튄 자리를 통째로 놓칩니다. 밀기 값도 지켜야 합니다.
        /// </summary>
        private static void DifferenceAcrossGrids()
        {
            Say("차이 — 격자가 다르고 시간이 어긋난 두 로그");

            // 이전: 0,2,4,6,8 에서 값 100 (2 초마다)
            // 이후: 1000 부터 1 초마다 값 100, 단 1005 에서만 105
            //   밀기 -1000 이면 이후 1005 는 이전 5 자리입니다.
            //   이전 격자(짝수)에는 5 가 없으므로, 격자를 합쳐야 +5 가 보입니다.
            var b = new StringBuilder("Time,V\n");
            for (int i = 0; i <= 8; i += 2) b.Append(i).Append(",100\n");

            var a = new StringBuilder("Time,V\n");
            for (int i = 0; i <= 8; i++)
                a.Append(1000 + i).Append(',').Append(i == 5 ? 105 : 100).Append('\n');

            LogDataset dsB = Open(WriteCsv("grid_b.csv", b.ToString()), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("grid_a.csv", a.ToString()), Orientation.Auto);
            int bi = dsB.FindChannel("V"), ai = dsA.FindChannel("V");

            double lo, hi;
            Check("맞춘 뒤 차이가 나옴",
                  DifferenceSeries.RangeIn(dsB, bi, dsA, ai, -1000, 0, 8, out lo, out hi), null);
            Near("이후에서만 튄 자리를 놓치지 않음", hi, 5, 1e-6);
            Near("그 밖에는 0", lo, 0, 1e-6);

            // 밀기를 안 하면 두 로그가 겹치지 않아 견줄 자리가 없습니다.
            Check("안 맞추면 겹치는 자리가 없음",
                  !DifferenceSeries.RangeIn(dsB, bi, dsA, ai, 0, 0, 8, out lo, out hi), null);
        }

        /// <summary>
        /// 상태(문자열) 채널은 값을 빼면 안 됩니다. 상태 번호가 로그마다 따로
        /// 매겨지기 때문입니다. 이름이 같으면 0, 다르면 1 — 대시보드 · 히트맵과
        /// 같은 규칙입니다.
        /// </summary>
        private static void DifferenceOfStateChannels()
        {
            Say("차이 — 상태 채널은 이름으로");

            // 두 로그에서 상태가 나오는 차례가 달라 번호가 서로 어긋납니다.
            string b = "Time,MODE\n0,IDLE\n1,IDLE\n2,RUN\n3,RUN\n";
            string a = "Time,MODE\n0,RUN\n1,IDLE\n2,RUN\n3,STOP\n";
            LogDataset dsB = Open(WriteCsv("state_b.csv", b), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("state_a.csv", a), Orientation.Auto);
            int bi = dsB.FindChannel("MODE"), ai = dsA.FindChannel("MODE");

            Check("상태 채널로 알아봄",
                  DifferenceSeries.ByName(dsB.Channels[bi], dsA.Channels[ai]), null);

            Near("이름이 다르면 1", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 0), 1, 1e-9);
            Near("이름이 같으면 0", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 1), 0, 1e-9);
            Near("이름이 같으면 0 (RUN)", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 2), 0, 1e-9);
            Near("이름이 다르면 1 (STOP)", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 3), 1, 1e-9);

            double lo, hi;
            DifferenceSeries.RangeIn(dsB, bi, dsA, ai, 0, 0, 3, out lo, out hi);
            Near("상태 차이의 최소는 0", lo, 0, 1e-9);
            Near("상태 차이의 최대는 1", hi, 1, 1e-9);
        }

        /// <summary>
        /// 기록 구간 밖에서는 차이를 만들지 않습니다. 없는 값을 지어내면
        /// 로그가 짧은 쪽 끝에서 가짜 차이가 생깁니다.
        /// </summary>
        private static void DifferenceOutsideOverlap()
        {
            Say("차이 — 겹치지 않는 구간");

            // 이전은 0~9, 이후는 5~14. 겹치는 곳은 5~9 뿐입니다.
            var b = new StringBuilder("Time,V\n");
            for (int i = 0; i <= 9; i++) b.Append(i).Append(",10\n");
            var a = new StringBuilder("Time,V\n");
            for (int i = 5; i <= 14; i++) a.Append(i).Append(",12\n");

            LogDataset dsB = Open(WriteCsv("ovl_b.csv", b.ToString()), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("ovl_a.csv", a.ToString()), Orientation.Auto);
            int bi = dsB.FindChannel("V"), ai = dsA.FindChannel("V");

            Check("겹치기 전은 값이 없음", double.IsNaN(DifferenceSeries.At(dsB, bi, dsA, ai, 0, 2)), null);
            Near("겹치는 곳은 +2", DifferenceSeries.At(dsB, bi, dsA, ai, 0, 7), 2, 1e-6);
            Check("겹친 뒤도 값이 없음", double.IsNaN(DifferenceSeries.At(dsB, bi, dsA, ai, 0, 12)), null);

            // 한쪽에만 있는 IO 도 마찬가지입니다.
            Check("없는 채널은 값이 없음",
                  double.IsNaN(DifferenceSeries.At(dsB, bi, dsA, -1, 0, 7)), null);
        }

        /// <summary>
        /// 분석 결과를 남기고 다시 읽습니다. 기록 하나에 파일 하나이고,
        /// 최근 것이 앞에 옵니다.
        /// </summary>
        /// <summary>
        /// 히트맵을 한 그룹의 IO 만 보도록 추립니다. 메인 화면에서 그룹을
        /// 눌러 넘어올 때 씁니다.
        /// </summary>
        private static void HeatmapNameFilter()
        {
            Say("히트맵 — 그룹만 추려 보기");

            // 세 IO 가 다 달라집니다. 그중 둘만 남기는지 봅니다.
            var b = new StringBuilder("Time,밸브 A,밸브 B,압력 PT-01\n");
            var a = new StringBuilder("Time,밸브 A,밸브 B,압력 PT-01\n");
            for (int i = 0; i < 40; i++)
            {
                b.Append(i * 1000).Append(",10,20,30\n");
                a.Append(i * 1000).Append(",11,21,31\n");
            }
            LogDataset dsB = Open(WriteCsv("hmf_b.csv", b.ToString()), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("hmf_a.csv", a.ToString()), Orientation.Auto);

            var all = new HeatmapOptions();
            all.RelativePercent = 0.1;
            HeatmapResult full = HeatmapBuilder.Build(dsB, dsA, all, null);
            Check("추리기 전에는 셋 다", full.Rows.Count == 3, "실제 " + full.Rows.Count);

            var one = new HeatmapOptions();
            one.RelativePercent = 0.1;
            one.OnlyNames = HeatmapOptions.NameFilter(new[] { "밸브 A", "압력 PT-01" });
            Check("추리기가 걸림", one.HasFilter, null);

            HeatmapResult part = HeatmapBuilder.Build(dsB, dsA, one, null);
            Check("둘만 남음", part.Rows.Count == 2, "실제 " + part.Rows.Count);
            Check("견준 수도 둘", part.ComparedChannels == 2, part.ComparedChannels.ToString());

            bool hasValve = false, hasPress = false, hasB = false;
            for (int i = 0; i < part.Rows.Count; i++)
            {
                if (part.Rows[i].Name == "밸브 A") hasValve = true;
                if (part.Rows[i].Name == "압력 PT-01") hasPress = true;
                if (part.Rows[i].Name == "밸브 B") hasB = true;
            }
            Check("고른 것은 남고", hasValve && hasPress, null);
            Check("안 고른 것은 빠짐", !hasB, null);

            // 이름이 살짝 달라도 붙어야 합니다. 그룹은 사람이 손으로 담습니다.
            var loose = new HeatmapOptions();
            loose.RelativePercent = 0.1;
            loose.OnlyNames = HeatmapOptions.NameFilter(new[] { "밸브A", "압력_PT01" });
            HeatmapResult l = HeatmapBuilder.Build(dsB, dsA, loose, null);
            Check("공백과 기호가 달라도 붙음", l.Rows.Count == 2, "실제 " + l.Rows.Count);

            // 빈 목록은 "추리지 않음" 입니다. 아무것도 안 남으면 안 됩니다.
            var empty = new HeatmapOptions();
            empty.RelativePercent = 0.1;
            empty.OnlyNames = HeatmapOptions.NameFilter(new string[0]);
            Check("빈 목록은 추리기가 아님", !empty.HasFilter, null);
            Check("그래서 셋 다", HeatmapBuilder.Build(dsB, dsA, empty, null).Rows.Count == 3, null);
        }

        private static void HistorySaveAndLoad()
        {
            Say("분석 결과 저장");

            string folder = Path.Combine(_dir, "history-test");

            var a = new AnalysisRecord();
            a.SavedAt = new DateTime(2026, 3, 14, 9, 30, 0);
            a.Label = "기동 전";
            a.BeforeName = "2026-03-14_before.xlsx";
            a.AfterName = "2026-03-21_after.xlsx";
            a.BeforeTime = "2026-03-14";
            a.AfterTime = "2026-03-21";
            a.BeforePath = @"D:\로그\2026-03-14_before.xlsx";
            a.AfterPath = @"D:\로그\2026-03-21_after.xlsx";
            a.SetScore(1, true, 100);
            a.SetScore(2, true, 72);
            a.SetScore(3, false, 0);
            a.ComparedCount = 184; a.ChangedCount = 12; a.OneSidedCount = 3;
            a.TolerancePercent = 0.1; a.AbsoluteTolerance = 0.25;
            a.AlignIo = "START 신호"; a.AppliedShift = -115;

            string problem;
            string id = HistoryStore.Save(folder, a, out problem);
            Check("저장됨", id.Length > 0, problem);
            Check("파일 이름이 시각", id.StartsWith("20260314-093000"), id);

            // 두 번째 건. 더 나중 시각이라 목록에서 앞에 와야 합니다.
            var b = new AnalysisRecord();
            b.SavedAt = new DateTime(2026, 3, 21, 17, 5, 0);
            b.ComparedCount = 184; b.ChangedCount = 20;
            b.TolerancePercent = 0.1; b.AbsoluteTolerance = 0.25;
            b.AlignIo = "START 신호";
            HistoryStore.Save(folder, b, out problem);
            Check("둘째도 저장됨", problem.Length == 0, problem);

            List<AnalysisRecord> back = HistoryStore.Load(folder);
            Check("두 건이 읽힘", back.Count == 2, "실제 " + back.Count);
            if (back.Count != 2) return;

            Check("최근 것이 앞", back[0].SavedAt > back[1].SavedAt, null);

            AnalysisRecord r = back[1];
            Check("이름", r.Label == "기동 전", r.Label);
            Check("이전 파일", r.BeforeName == "2026-03-14_before.xlsx", r.BeforeName);
            Check("발생시간", r.AfterTime == "2026-03-21", r.AfterTime);
            Check("분석 1 점수 있음", r.HasScores[0], null);
            Near("분석 1 점수", r.Scores[0], 100, 1e-9);
            Check("분석 2 점수 있음", r.HasScores[1], null);
            Near("분석 2 점수", r.Scores[1], 72, 1e-9);
            Check("분석 3 은 점수 없음", !r.HasScores[2], null);
            Check("점수 셋을 한 줄로", r.AllScoresText == "100 / 72 / ??", r.AllScoresText);
            Check("이전 경로", r.BeforePath == @"D:\로그\2026-03-14_before.xlsx", r.BeforePath);
            Check("이후 이름", r.AfterName == "2026-03-21_after.xlsx", r.AfterName);
            Check("견준 IO", r.ComparedCount == 184, r.ComparedCount.ToString());
            Check("달라진 IO", r.ChangedCount == 12, r.ChangedCount.ToString());
            Check("한쪽에만", r.OneSidedCount == 3, r.OneSidedCount.ToString());
            Near("허용 오차", r.TolerancePercent, 0.1, 1e-9);
            Near("절대 오차", r.AbsoluteTolerance, 0.25, 1e-9);
            Check("맞추기 IO", r.AlignIo == "START 신호", r.AlignIo);
            Near("밀기 값", r.AppliedShift, -115, 1e-9);
            Check("저장 시각", r.SavedAt == new DateTime(2026, 3, 14, 9, 30, 0),
                  r.SavedAt.ToString("s"));

            // 이름을 안 붙이면 저장 시각으로 적힙니다.
            Check("이름 없으면 시각", back[0].DisplayName.StartsWith("2026-03-21"), back[0].DisplayName);

            // 지우기
            Check("지워짐", HistoryStore.Delete(folder, id), null);
            Check("한 건만 남음", HistoryStore.Load(folder).Count == 1, null);
            Check("없는 것을 지우면 거짓", !HistoryStore.Delete(folder, id), null);

            // 경로가 섞인 열쇠는 거부합니다. 엉뚱한 곳을 지울 수 있습니다.
            Check("경로가 섞이면 거부", !HistoryStore.Delete(folder, "..\\settings.json"), null);
            Check("폴더 구분자도 거부", !HistoryStore.Delete(folder, "sub/other.json"), null);
        }

        /// <summary>
        /// 같은 밀리초에 두 번 저장해도 한 건이 사라지면 안 됩니다.
        /// 그리고 기준이 다른 기록은 그대로 견줄 수 없다고 말해야 합니다.
        /// </summary>
        private static void HistoryEdgeCases()
        {
            Say("분석 결과 저장 — 가장자리");

            string folder = Path.Combine(_dir, "history-edge");
            var t = new DateTime(2026, 5, 1, 12, 0, 0);

            string problem;
            var a = new AnalysisRecord(); a.SavedAt = t; a.Label = "첫째";
            var b = new AnalysisRecord(); b.SavedAt = t; b.Label = "둘째";
            string ia = HistoryStore.Save(folder, a, out problem);
            string ib = HistoryStore.Save(folder, b, out problem);

            Check("같은 시각이어도 이름이 다름", ia != ib, ia + " / " + ib);
            Check("두 건 다 남음", HistoryStore.Load(folder).Count == 2, null);

            // 기준 견주기
            var r = new AnalysisRecord();
            r.TolerancePercent = 0.1; r.AbsoluteTolerance = 0;
            r.AlignIo = "START";

            Check("같은 기준", r.SameBasis(0.1, 0, "START"), null);
            Check("허용 오차가 다르면", !r.SameBasis(1.0, 0, "START"), null);
            Check("절대 오차가 다르면", !r.SameBasis(0.1, 0.5, "START"), null);
            Check("맞추기가 다르면", !r.SameBasis(0.1, 0, "RUN"), null);

            // 없는 폴더를 읽으면 빈 목록입니다 (터지지 않습니다).
            Check("없는 폴더는 빈 목록",
                  HistoryStore.Load(Path.Combine(_dir, "history-none")).Count == 0, null);

            // 점수가 하나뿐이던 시절의 파일도 읽혀야 합니다. 그때 점수는
            // 분석 1 것으로 봅니다.
            string oldJson = "{\"savedAt\":\"2026-01-02T03:04:05.0000000\","
                           + "\"label\":\"옛 기록\",\"hasScore\":true,\"score\":88,"
                           + "\"changedCount\":7}";
            AnalysisRecord old = AnalysisRecord.FromJson(Json.Parse(oldJson));
            Check("옛 파일도 읽힘", old.Label == "옛 기록", old.Label);
            Check("옛 점수는 분석 1 것", old.HasScores[0], null);
            Near("옛 점수 값", old.Scores[0], 88, 1e-9);
            Check("나머지는 비어 있음", !old.HasScores[1] && !old.HasScores[2], null);
            Check("옛 파일의 점수 줄", old.AllScoresText == "88 / ?? / ??", old.AllScoresText);
        }

        private static void JsonRoundTrip()
        {
            Say("JSON");
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["name"] = "밸브 \"OPEN\"\n둘째 줄";
            root["n"] = 12.5;
            root["flag"] = true;
            root["none"] = null;
            root["list"] = new List<object> { "a", 1.0, false };

            string text = Json.Write(root);
            Dictionary<string, object> back = Json.AsObject(Json.Parse(text));

            Check("따옴표와 줄바꿈이 살아남음", Json.GetString(back, "name", null) == "밸브 \"OPEN\"\n둘째 줄", null);
            Near("숫자", Json.GetDouble(back, "n", 0), 12.5, 1e-9);
            Check("참거짓", Json.GetBool(back, "flag", false), null);
            Check("배열 길이", Json.GetArray(back, "list").Count == 3, null);
        }

        /// <summary>
        /// IO 별 허용 오차 표. 이름을 느슨하게 맞추는 것과, 안 정한 자리가
        /// 기본값으로 떨어지는 것이 핵심입니다.
        /// </summary>
        private static void ToleranceTableRules()
        {
            Say("IO 별 허용 오차 — 표");

            var rules = new List<ToleranceOverride>();
            var t1 = new ToleranceOverride(); t1.Io = "밸브 OPEN"; t1.Percent = 0.0;
            var t2 = new ToleranceOverride(); t2.Io = "온도_TT-01"; t2.Absolute = 2.5;
            var t3 = new ToleranceOverride(); t3.Io = "압력"; t3.Percent = 5; t3.Absolute = 0.1;
            rules.Add(t1); rules.Add(t2); rules.Add(t3);

            ToleranceTable t = ToleranceTable.From(0.1, 0, rules);
            Check("규칙 셋", t.OverrideCount == 3, "실제 " + t.OverrideCount);

            // 퍼센트만 건 IO 는 절대값이 기본값입니다. 여기서 0 과 "안 정함"
            // 이 섞이면 절대 오차가 저절로 켜지거나 꺼집니다.
            Near("퍼센트만 건 IO 의 퍼센트", t.PercentFor("밸브 OPEN"), 0.0, 1e-12);
            Near("퍼센트만 건 IO 의 절대값은 기본", t.AbsoluteFor("밸브 OPEN"), 0.0, 1e-12);

            // 절대값만 건 IO 는 퍼센트가 기본값입니다.
            Near("절대값만 건 IO 의 절대값", t.AbsoluteFor("온도_TT-01"), 2.5, 1e-12);
            Near("절대값만 건 IO 의 퍼센트는 기본", t.PercentFor("온도_TT-01"), 0.1, 1e-12);

            // 이름은 느슨하게 맞춥니다 — 두 로그에서 적히는 모양이 다릅니다.
            Near("띄어쓰기가 달라도 같은 IO", t.AbsoluteFor("온도 TT 01"), 2.5, 1e-12);
            Near("대소문자가 달라도 같은 IO", t.PercentFor("압력"), 5, 1e-12);

            // 안 건 IO 는 기본값입니다.
            Near("안 건 IO 의 퍼센트", t.PercentFor("다른 IO"), 0.1, 1e-12);
            Near("이름이 없어도 견딤", t.PercentFor(null), 0.1, 1e-12);
            Check("안 건 IO 는 규칙 없음", !t.HasOverrideFor("다른 IO"), null);

            // 같은 IO 가 두 번이면 뒤의 것이 이깁니다.
            var dup = new ToleranceOverride(); dup.Io = "압력"; dup.Percent = 9;
            rules.Add(dup);
            ToleranceTable t2t = ToleranceTable.From(0.1, 0, rules);
            Near("같은 IO 는 뒤의 것", t2t.PercentFor("압력"), 9, 1e-12);

            // 쓸모없는 줄은 표에 들어가지 않습니다.
            var empty = new List<ToleranceOverride>();
            var noName = new ToleranceOverride(); noName.Percent = 1;
            var noValue = new ToleranceOverride(); noValue.Io = "이름만";
            empty.Add(noName); empty.Add(noValue); empty.Add(null);
            ToleranceTable none = ToleranceTable.From(0.1, 0, empty);
            Check("쓸모없는 줄은 안 들어감", none.OverrideCount == 0, "실제 " + none.OverrideCount);
            Check("규칙이 없으면 없다고", !none.HasOverrides, null);
            Check("null 목록도 견딤", !ToleranceTable.From(0.1, 0, null).HasOverrides, null);

            // 한 줄로 적은 것(기준 비교용)은 줄 순서가 달라도 같아야 합니다.
            var order1 = new List<ToleranceOverride> { t1, t2 };
            var order2 = new List<ToleranceOverride> { t2, t1 };
            Check("순서가 달라도 같은 서명",
                  ToleranceTable.From(0.1, 0, order1).Signature()
                  == ToleranceTable.From(0.1, 0, order2).Signature(),
                  ToleranceTable.From(0.1, 0, order1).Signature());
            Check("규칙이 없으면 서명도 빈 글자",
                  ToleranceTable.From(0.1, 0, null).Signature().Length == 0, null);
            Check("값이 다르면 서명도 다름",
                  ToleranceTable.From(0.1, 0, order1).Signature()
                  != ToleranceTable.From(0.1, 0, new List<ToleranceOverride> { dup, t2 }).Signature(), null);
        }

        /// <summary>
        /// IO 별 허용 오차가 <b>실제 비교</b>에 먹는지. 대시보드(DiffEngine)와
        /// 히트맵이 같은 답을 내야 합니다 — 화면마다 다르면 어느 쪽을 믿을지
        /// 알 수 없습니다.
        /// </summary>
        private static void TolerancePerIo()
        {
            Say("IO 별 허용 오차 — 실제 비교");

            // 두 IO 가 똑같이 10 → 11 로 벌어집니다 (10%).
            var b = new StringBuilder("Time,흔들리는 온도,밸브 OPEN\n");
            var a = new StringBuilder("Time,흔들리는 온도,밸브 OPEN\n");
            for (int i = 0; i < 40; i++)
            {
                b.Append(i * 1000).Append(",10,10\n");
                a.Append(i * 1000).Append(",11,11\n");
            }
            LogDataset dsB = Open(WriteCsv("tol_b.csv", b.ToString()), Orientation.Auto);
            LogDataset dsA = Open(WriteCsv("tol_a.csv", a.ToString()), Orientation.Auto);

            // 기본값 0.1% 면 둘 다 차이입니다.
            var opt = new DiffOptions();
            opt.RelativePercent = 0.1;
            Check("기본값만 쓰면 둘 다 차이",
                  DiffEngine.Compare(dsB, dsA, opt, null).ChangedCount == 2, null);

            // 온도만 20% 까지 봐줍니다. 밸브는 그대로 0.1%.
            var rules = new List<ToleranceOverride>();
            var loose = new ToleranceOverride(); loose.Io = "흔들리는온도"; loose.Percent = 20;
            rules.Add(loose);

            opt.Tolerances = ToleranceTable.From(0.1, 0, rules);
            CompareResult r = DiffEngine.Compare(dsB, dsA, opt, null);
            Check("온도만 빠져 하나만 차이", r.ChangedCount == 1, "실제 " + r.ChangedCount);

            ChannelDiff temp = r.Items.Find(d => d.Name == "흔들리는 온도");
            ChannelDiff valve = r.Items.Find(d => d.Name == "밸브 OPEN");
            Check("봐준 IO 는 차이 아님", temp != null && !temp.Changed, null);
            Check("안 봐준 IO 는 차이", valve != null && valve.Changed, null);

            // 최대 오차 퍼센트는 그대로 적힙니다 — 잣대를 바꾼 것이지 값이
            // 바뀐 것이 아닙니다. 화면에 10% 로 적히고도 "차이 아님" 인 것이
            // 맞습니다.
            Near("봐준 IO 도 오차 퍼센트는 그대로", temp.MaxPercent, 10, 1e-6);

            // 히트맵도 같은 답이어야 합니다.
            var h = new HeatmapOptions();
            h.RelativePercent = 0.1;
            h.IncludeUnchanged = true;
            h.Tolerances = ToleranceTable.From(0.1, 0, rules);
            HeatmapResult hr = HeatmapBuilder.Build(dsB, dsA, h, null);

            HeatRow hTemp = hr.Rows.Find(x => x.Name == "흔들리는 온도");
            HeatRow hValve = hr.Rows.Find(x => x.Name == "밸브 OPEN");
            Check("히트맵도 두 줄", hTemp != null && hValve != null, null);
            Check("히트맵에서도 봐준 IO 는 넘은 칸이 없음", hTemp.TotalOverSamples == 0,
                  "실제 " + hTemp.TotalOverSamples);
            Check("히트맵에서도 안 봐준 IO 는 넘음", hValve.TotalOverSamples > 0,
                  "실제 " + hValve.TotalOverSamples);

            // 절대값으로도 됩니다. 1.5 면 1 만큼 벌어진 것은 같은 것으로 봅니다.
            var byValue = new List<ToleranceOverride>();
            var abs = new ToleranceOverride(); abs.Io = "밸브 OPEN"; abs.Absolute = 1.5;
            byValue.Add(abs);
            opt.Tolerances = ToleranceTable.From(0.1, 0, byValue);
            CompareResult r2 = DiffEngine.Compare(dsB, dsA, opt, null);
            ChannelDiff valve2 = r2.Items.Find(d => d.Name == "밸브 OPEN");
            Check("절대값으로 봐준 IO 는 차이 아님", valve2 != null && !valve2.Changed, null);
            Check("나머지는 그대로 차이", r2.ChangedCount == 1, "실제 " + r2.ChangedCount);

            // 설정 파일에 담았다 꺼내도 그대로여야 합니다.
            var s = new AppSettings();
            s.RelativeTolerancePercent = 0.5;
            s.Tolerances.Add(loose);
            s.Tolerances.Add(abs);
            var onlyName = new ToleranceOverride(); onlyName.Io = "이름만";   // 버려질 줄
            s.Tolerances.Add(onlyName);

            AppSettings back = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("쓸모없는 줄은 저장되지 않음", back.Tolerances.Count == 2,
                  "실제 " + back.Tolerances.Count);

            ToleranceTable bt = ToleranceTable.From(back.RelativeTolerancePercent, 0, back.Tolerances);
            Near("퍼센트가 그대로", bt.PercentFor("흔들리는 온도"), 20, 1e-9);
            Near("절대값이 그대로", bt.AbsoluteFor("밸브 OPEN"), 1.5, 1e-9);
            // 안 정한 자리는 다시 읽어도 "안 정함" 이어야 합니다. 0 으로
            // 떨어지면 절대 오차가 저절로 켜집니다.
            Near("안 정한 자리는 기본값 그대로", bt.AbsoluteFor("흔들리는 온도"), 0, 1e-9);
            Near("안 정한 퍼센트도 기본값", bt.PercentFor("밸브 OPEN"), 0.5, 1e-9);

            // 저장해 둔 분석과 "같은 기준" 인지 가릴 때도 규칙이 들어갑니다.
            var rec = new AnalysisRecord();
            rec.TolerancePercent = 0.5;
            rec.AbsoluteTolerance = 0;
            rec.ToleranceRules = bt.Signature();
            Check("같은 규칙이면 같은 기준",
                  rec.SameBasis(0.5, 0, "", bt.Signature()), null);
            Check("규칙이 달라지면 다른 기준",
                  !rec.SameBasis(0.5, 0, "", string.Empty), null);
        }

        // ======================= 분석 2 : DB =======================

        private static string WriteText(string name, string body)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, body, new UTF8Encoding(false));
            return path;
        }

        /// <summary>.sql 덤프 읽기. mysqldump 가 뽑는 모양을 그대로 흉내 냈습니다.</summary>
        /// <summary>
        /// 손으로 고치는 그룹 파일 (groups.txt).
        ///
        /// 여기서 제일 중요한 것은 <b>전부 주석인 파일이 아무것도 하지 않는
        /// 것</b>입니다. "파일이 있으면 주인" 으로 했다면, 설명만 적힌 빈 틀이
        /// 들어 있는 것만으로 그때까지 묶어 둔 그룹이 전부 지워집니다.
        /// </summary>
        /// <summary>
        /// 긴 이름이 창 제목에 들어가는지. 위 줄에서 빼고 창 제목으로 옮긴
        /// 자리입니다 (0.68).
        ///
        /// 창 제목 글을 만드는 규칙만 봅니다 — 화면은 여기서 못 띄웁니다.
        /// </summary>
        private static void WindowTitleText()
        {
            Say("창 제목 — 긴 이름");

            // 규칙: "이름 — 긴 이름". 긴 이름이 없으면 전에 쓰던 글.
            Check("긴 이름을 적으면 제목에",
                  Title("LogScope", "설비 Log 비교 도구") == "LogScope — 설비 Log 비교 도구",
                  Title("LogScope", "설비 Log 비교 도구"));
            Check("안 적으면 전에 쓰던 글",
                  Title("LogScope", "") == "LogScope — " + AppConfig.DefaultSubtitle,
                  Title("LogScope", ""));
            // 이름을 바꿨으면 제목도 그 이름입니다.
            Check("바꾼 이름을 따라감",
                  Title("로그보기", "사내용").StartsWith("로그보기 — "),
                  Title("로그보기", "사내용"));
            // 아무리 길어도 자르지 않습니다 — 창 제목은 폭을 안 먹습니다.
            string longName = new string('가', 120);
            Check("길어도 자르지 않음", Title("X", longName).Length == 4 + 120,
                  "실제 " + Title("X", longName).Length);
        }

        /// <summary>
        /// 화면이 쓰는 그 규칙을 그대로 부릅니다 (AppConfig.WindowTitle).
        /// 여기서 규칙을 한 번 더 적으면, 한쪽만 고쳐지는 날이 옵니다.
        /// </summary>
        private static string Title(string name, string full)
        {
            string line = full.Length == 0 ? "name = " + name
                                           : "name = " + name + "\nfullname = " + full;
            return AppConfig.Parse(line.Split('\n')).WindowTitle;
        }

        private static void GroupFileParse()
        {
            Say("그룹 파일 — groups.txt");

            List<GroupDef> g = GroupFile.Parse(new[]
            {
                "# 설명 줄",
                "",
                "  가열부 = TEMP_1, TEMP_2 , HEATER_ON  ",
                "압력\t=\tPRS_MAIN; PRS_SUB",
                "예비 =",
                "가열부 = TEMP_3",          // 같은 이름은 앞의 그룹에 더합니다
                "등호없는줄",
                " = 이름이 빈 줄",
                "#가열부 = 주석이라 안 읽힘",
            });

            Check("그룹 셋", g.Count == 3, "실제 " + g.Count);
            Check("차례대로", g[0].Name == "가열부" && g[1].Name == "압력" && g[2].Name == "예비",
                  g[0].Name + "/" + g[1].Name + "/" + g[2].Name);

            // 앞뒤 공백은 떼고, 쉼표·세미콜론·탭을 다 가릅니다.
            Check("IO 를 가름", g[0].Members.Count == 4, "실제 " + g[0].Members.Count);
            Check("공백을 뗌", g[0].Members[0] == "TEMP_1" && g[0].Members[2] == "HEATER_ON",
                  string.Join("|", g[0].Members.ToArray()));
            Check("같은 이름은 더함", g[0].Members[3] == "TEMP_3", g[0].Members[3]);
            Check("세미콜론도 가름", g[1].Members.Count == 2, string.Join("|", g[1].Members.ToArray()));
            Check("탭도 가름", g[1].Members[0] == "PRS_MAIN", g[1].Members[0]);

            // 이름만 적은 줄은 빈 그룹입니다. 화면에서 IO 를 끌어다 넣을
            // 자리로 쓰므로 빼지 않습니다.
            Check("빈 그룹도 만듦", g[2].Members.Count == 0, "실제 " + g[2].Members.Count);

            // 같은 IO 를 두 번 적어도 한 번만 들어갑니다.
            List<GroupDef> dup = GroupFile.Parse(new[] { "a = X, X, Y" });
            Check("같은 IO 는 한 번만", dup[0].Members.Count == 2,
                  string.Join("|", dup[0].Members.ToArray()));

            // ---- 전부 주석이면 아무것도 ----
            List<GroupDef> none = GroupFile.Parse(new[]
            {
                "# 그룹 설정",
                "#   가열부 = TEMP_1, TEMP_2",
                "",
            });
            Check("전부 주석이면 빈 목록", none.Count == 0, "실제 " + none.Count);

            Check("null 도 견딤", GroupFile.Parse(null).Count == 0, null);
            Check("없는 파일은 빈 목록",
                  GroupFile.Load(Path.Combine(_dir, "없는그룹.txt")).Count == 0, null);

            // ---- 파일에서 읽기 (인코딩은 config.txt 와 같은 길) ----
            string path = Path.Combine(_dir, "groups.txt");
            File.WriteAllText(path, "온도그룹 = 가열_온도, 냉각_온도\n", new UTF8Encoding(true));
            List<GroupDef> fromFile = GroupFile.Load(path);
            Check("파일에서 읽음", fromFile.Count == 1 && fromFile[0].Name == "온도그룹",
                  fromFile.Count == 0 ? "없음" : fromFile[0].Name);
            Check("한글 IO 이름", fromFile[0].Members[0] == "가열_온도", fromFile[0].Members[0]);

            // CP949(ANSI)로 저장된 파일도 읽혀야 합니다 — 예전 메모장이 그렇게 씁니다.
            try
            {
                Encoding cp949 = Encoding.GetEncoding(949);
                string p2 = Path.Combine(_dir, "groups_ansi.txt");
                File.WriteAllBytes(p2, cp949.GetBytes("압력그룹 = 주_압력\n"));
                List<GroupDef> ansi = GroupFile.Load(p2);
                Check("CP949 파일도 읽음", ansi.Count == 1 && ansi[0].Name == "압력그룹",
                      ansi.Count == 0 ? "없음" : ansi[0].Name);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                // 리눅스 모노에는 CP949 가 없습니다 (윈도우에는 늘 있습니다).
                // 던지는 예외가 둘이라 둘 다 받습니다 — AppConfig.Legacy() 가
                // 같은 두 가지를 받습니다.
                Console.WriteLine("  --    CP949 가 없어 건너뜀");
            }

            // ---- 다시 적기 : 적어 낸 글을 다시 읽으면 같아야 합니다 ----
            string text = GroupFile.Write(g);
            List<GroupDef> back = GroupFile.Parse(text.Split('\n'));
            Check("적고 다시 읽어도 같음", back.Count == g.Count, back.Count + " / " + g.Count);
            Check("적은 IO 도 같음",
                  back[0].Members.Count == g[0].Members.Count && back[0].Name == g[0].Name, null);
            // 적어 낸 글의 머리에는 "누가 이기나" 가 적혀 있어야 합니다.
            Check("적어 낸 글이 규칙을 밝힘", text.Contains("주인") && text.Contains("주석"), null);
        }

        private static void DbSqlDump()
        {
            Say("DB — .sql 덤프 읽기");

            string dump =
                "-- MySQL dump 10.13  Distrib 8.0.36\n" +
                "/*!40101 SET NAMES utf8 */;\n" +
                "DROP TABLE IF EXISTS `recipe`;\n" +
                "CREATE TABLE `recipe` (\n" +
                "  `id` int(11) NOT NULL AUTO_INCREMENT,\n" +
                "  `name` varchar(64) NOT NULL DEFAULT 'none',\n" +
                "  `temp_max` decimal(6,2) DEFAULT NULL,\n" +
                "  `note` text,\n" +
                "  PRIMARY KEY (`id`),\n" +
                "  KEY `by_name` (`name`)\n" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;\n" +
                "INSERT INTO `recipe` VALUES " +
                "(1,'가열 A',182.50,NULL)," +
                "(2,'가열; B',NULL,'쉼표, 그리고 세미콜론;')," +
                "(3,'따옴표 \\' 포함',0.00,'줄\\n바꿈');\n" +
                "CREATE TABLE `empty_one` (`k` int NOT NULL, PRIMARY KEY (`k`));\n";

            var snap = new DbSnapshot();
            SqlDumpReader.Read(WriteText("dump_a.sql", dump), snap, 0);

            Check("표 둘", snap.Tables.Count == 2, "실제 " + snap.Tables.Count);

            DbTable t = snap.Find("recipe");
            Check("표를 찾음", t != null, null);
            Check("열 넷", t.Columns.Count == 4, "실제 " + t.Columns.Count);
            Check("열 이름", t.Columns[1].Name == "name", t.Columns[1].Name);
            Check("타입 그대로", t.Columns[2].Type == "decimal(6,2)", t.Columns[2].Type);
            Check("NOT NULL 읽음", !t.Columns[0].Nullable, null);
            Check("NULL 허용 읽음", t.Columns[2].Nullable, null);
            Check("기본값 읽음", t.Columns[1].Default == "'none'", t.Columns[1].Default);
            Check("기본 키", t.Columns[0].IsKey && !t.Columns[1].IsKey, null);
            Check("KEY 는 열이 아님", t.Find("by_name") == null, null);

            Check("행 셋", t.Rows.Count == 3, "실제 " + t.Rows.Count);
            Check("값 읽음", t.Rows[0].Get(1) == "가열 A", t.Rows[0].Get(1));
            Check("NULL 은 null 로", t.Rows[0].Get(3) == null, "실제 " + t.Rows[0].Get(3));

            // 값 안의 세미콜론과 쉼표 때문에 문장이나 값이 잘리면 안 됩니다.
            Check("값 안의 세미콜론", t.Rows[1].Get(1) == "가열; B", t.Rows[1].Get(1));
            Check("값 안의 쉼표", t.Rows[1].Get(3) == "쉼표, 그리고 세미콜론;", t.Rows[1].Get(3));

            // 이스케이프는 원래 글자로 되돌립니다. 그래야 같은 값을 다르게 적어
            // 둔 두 덤프를 "차이" 로 세지 않습니다.
            Check("이스케이프한 따옴표는 글자 하나", t.Rows[2].Get(1) == "따옴표 ' 포함", t.Rows[2].Get(1));
            Check("줄바꿈 이스케이프", t.Rows[2].Get(3) == "줄\n바꿈", t.Rows[2].Get(3));

            DbTable e = snap.Find("empty_one");
            Check("행 없는 표도 읽음", e != null && e.Columns.Count == 1, null);
            Check("행이 없으면 HasRows 거짓", e != null && !e.HasRows, null);

            // 열 목록이 붙은 INSERT, CREATE 없는 INSERT
            string dump2 =
                "CREATE TABLE `t1` (`a` int NOT NULL, `b` int, PRIMARY KEY (`a`));\n" +
                "INSERT INTO `t1` (`b`,`a`) VALUES (20,1);\n" +
                "INSERT INTO `orphan` VALUES (1,'x');\n";
            var s2 = new DbSnapshot();
            SqlDumpReader.Read(WriteText("dump_b.sql", dump2), s2, 0);

            DbTable t1 = s2.Find("t1");
            Check("열 목록 순서를 따름", t1.Rows[0].Get(0) == "1" && t1.Rows[0].Get(1) == "20",
                  t1.Rows[0].Get(0) + "/" + t1.Rows[0].Get(1));

            DbTable orphan = s2.Find("orphan");
            Check("CREATE 없는 INSERT 도 읽음", orphan != null && orphan.Rows.Count == 1, null);
            Check("그때는 모른다고 적음", orphan != null && orphan.Note.Contains("CREATE TABLE 이 없어"),
                  orphan == null ? null : orphan.Note);

            // 행 상한
            var big = new StringBuilder(
                "CREATE TABLE `b` (`k` int NOT NULL, PRIMARY KEY (`k`));\nINSERT INTO `b` VALUES ");
            for (int i = 0; i < 50; i++) { if (i > 0) big.Append(','); big.Append('(').Append(i).Append(')'); }
            big.Append(";\n");
            var s3 = new DbSnapshot();
            SqlDumpReader.Read(WriteText("dump_c.sql", big.ToString()), s3, 10);
            DbTable b = s3.Find("b");
            Check("행 상한이 걸림", b.Rows.Count == 10, "실제 " + b.Rows.Count);
            Check("상한을 밝힘", b.Note.Contains("상한"), b.Note);
        }

        private static void DbCsvTable()
        {
            Say("DB — .csv 표 읽기");

            string path = WriteText("설비목록.csv", "id,name,temp\n1,가열 A,182.5\n2,\"쉼표, 포함\",0\n");
            DbTable t = CsvTableReader.Read(path, 0);

            Check("파일 이름이 표 이름", t.Name == "설비목록", t.Name);
            Check("첫 줄이 열 이름", t.Columns.Count == 3 && t.Columns[1].Name == "name", t.Columns[1].Name);
            Check("행 둘", t.Rows.Count == 2, "실제 " + t.Rows.Count);
            Check("따옴표 안의 쉼표", t.Rows[1].Get(1) == "쉼표, 포함", t.Rows[1].Get(1));
            Check("CSV 에는 키가 없음", !t.HasKey, null);

            // 폴더로 읽기 + 못 읽는 파일을 밝히는지
            string dir = Path.Combine(_dir, "dbfolder");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "t.csv"), "a,b\n1,2\n", new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(dir, "big_table.ibd"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(dir, "big_table.frm"), new byte[] { 1, 2, 3 });

            DbSnapshot snap = DbFolderReader.Read(dir, null);
            Check("폴더에서 csv 를 읽음", snap.Find("t") != null, null);
            Check("못 읽는 파일을 밝힘", snap.Notes.Count > 0, null);
            // 어느 줄에 적히는지가 아니라 적히는지를 봅니다. 줄 차례는
            // 안내가 늘면 바뀝니다.
            string folderNotes = string.Join(" / ", snap.Notes.ToArray());
            Check("ibd 를 적음", folderNotes.Contains(".ibd"), folderNotes);
            Check("frm 도 적음", folderNotes.Contains(".frm"), folderNotes);

            DbSnapshot none = DbFolderReader.Read(Path.Combine(_dir, "없는폴더"), null);
            Check("없는 폴더는 그렇다고만", none.Tables.Count == 0 && none.Notes.Count == 1, null);
        }


        /// <summary>
        /// 폴더 안에 폴더가 나뉘어 있을 때. MySQL 데이터 폴더가
        /// Data\&lt;DB 이름&gt;\&lt;표&gt; 꼴이라 이 모양이 실제 모양입니다.
        /// </summary>
        private static void DbNestedFolders()
        {
            Say("DB — 하위 폴더까지 읽기");

            string root = Path.Combine(_dir, "nested");
            Directory.CreateDirectory(Path.Combine(root, "db1"));
            Directory.CreateDirectory(Path.Combine(root, "db2", "깊은곳"));

            // 뿌리에 바로 있는 것, 그리고 폴더마다 같은 이름의 표
            File.WriteAllText(Path.Combine(root, "root_table.csv"), "a\n1\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "db1", "users.csv"), "id,name\n1,가\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "db2", "users.csv"), "id,name\n1,나\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "db2", "깊은곳", "deep.sql"),
                "CREATE TABLE `t` (`k` int NOT NULL, PRIMARY KEY (`k`));\nINSERT INTO `t` VALUES (1);\n",
                new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(root, "db1", "users.ibd"), new byte[] { 1, 2, 3 });

            DbSnapshot snap = DbFolderReader.Read(root, null);

            // 같은 이름의 표가 폴더마다 있어도 덮이지 않아야 합니다. 덮이면
            // 한쪽 DB 가 조용히 사라집니다.
            Check("뿌리의 표는 이름 그대로", snap.Find("root_table") != null, null);
            Check("폴더 이름이 붙음 (db1)", snap.Find("db1.users") != null, null);
            Check("폴더 이름이 붙음 (db2)", snap.Find("db2.users") != null, null);
            Check("표 넷을 다 읽음", snap.Tables.Count == 4, "실제 " + snap.Tables.Count);

            DbTable u1 = snap.Find("db1.users");
            DbTable u2 = snap.Find("db2.users");
            Check("같은 이름이어도 값이 섞이지 않음",
                  u1.Rows[0].Get(1) == "가" && u2.Rows[0].Get(1) == "나",
                  u1.Rows[0].Get(1) + "/" + u2.Rows[0].Get(1));

            // 두 겹 아래의 .sql 도, 그 안의 표 이름에 폴더가 붙습니다.
            Check("두 겹 아래의 덤프", snap.Find("db2.깊은곳.t") != null, null);

            // 하위 폴더를 읽었다는 것과 .ibd 를 못 읽는다는 것을 둘 다 적습니다.
            string notes = string.Join(" / ", snap.Notes.ToArray());
            Check("하위 폴더를 읽었다고 적음", notes.Contains("하위 폴더"), notes);
            Check("ibd 는 못 읽는다고 적음", notes.Contains(".ibd"), notes);

            // 겹 수 상한: 0 이면 뿌리만 봅니다.
            var shallow = new DbFolderReader.Options();
            shallow.MaxDepth = 0;
            DbSnapshot top = DbFolderReader.Read(root, shallow);
            Check("겹 수 상한이 걸림", top.Tables.Count == 1 && top.Find("root_table") != null,
                  "실제 " + top.Tables.Count);

            // 파일 수 상한
            var few = new DbFolderReader.Options();
            few.MaxFiles = 2;
            var notes2 = new List<string>();
            List<string> some = DbFolderReader.Files(root, few, notes2);
            Check("파일 수 상한이 걸림", some.Count == 2, "실제 " + some.Count);
            Check("그렇다고 적음", string.Join(" ", notes2.ToArray()).Contains("상한"),
                  string.Join(" ", notes2.ToArray()));

            // 폴더 이름 붙이기 자체
            Check("뿌리 파일에는 안 붙음",
                  DbFolderReader.Prefix(root, Path.Combine(root, "x.csv")) == "", 
                  DbFolderReader.Prefix(root, Path.Combine(root, "x.csv")));
            Check("한 겹은 한 번",
                  DbFolderReader.Prefix(root, Path.Combine(root, "db1", "x.csv")) == "db1.",
                  DbFolderReader.Prefix(root, Path.Combine(root, "db1", "x.csv")));
            Check("두 겹은 점으로 이음",
                  DbFolderReader.Prefix(root, Path.Combine(root, "db2", "깊은곳", "x.csv")) == "db2.깊은곳.",
                  DbFolderReader.Prefix(root, Path.Combine(root, "db2", "깊은곳", "x.csv")));
            Check("바깥 경로면 안 붙음",
                  DbFolderReader.Prefix(root, Path.Combine(_dir, "x.csv")) == "",
                  DbFolderReader.Prefix(root, Path.Combine(_dir, "x.csv")));

            // 폴더 구조가 같으면 그대로 견줘집니다 (이름이 같으니까).
            string rootB = Path.Combine(_dir, "nested_b");
            Directory.CreateDirectory(Path.Combine(rootB, "db1"));
            File.WriteAllText(Path.Combine(rootB, "db1", "users.csv"), "id,name\n1,다\n",
                              new UTF8Encoding(false));
            DbDiffResult d = DbDiff.Compare(DbFolderReader.Read(root, null),
                                            DbFolderReader.Read(rootB, null));
            TableDiff td = null;
            for (int i = 0; i < d.Tables.Count; i++) if (d.Tables[i].Name == "db1.users") td = d.Tables[i];
            Check("폴더가 같으면 같은 표로 짝지음", td != null && td.Change == DbChange.Changed,
                  td == null ? "못 찾음" : td.Change.ToString());
        }

        /// <summary>
        /// .frm / .ibd 를 못 읽을 때 적는 안내가 <b>두 갈래</b>인지.
        ///
        /// .frm 이 함께 있으면 MySQL 5.x 이고, 그 .ibd 안에는 ibd2sdi 가 꺼낼
        /// SDI 가 없습니다 (8.0 부터 들어갑니다). 그런 폴더에 "ibd2sdi 경로를
        /// 적으세요" 라고 안내하면 적어 보고 또 안 되는 길로 보내는 셈입니다.
        /// </summary>
        private static void DbFrmIbdNote()
        {
            Say("DB — .frm / .ibd 안내가 갈라지는지");

            // 5.x : 표마다 .frm + .ibd 짝
            string five = Path.Combine(_dir, "five");
            Directory.CreateDirectory(five);
            File.WriteAllBytes(Path.Combine(five, "recipe.frm"), new byte[] { 0xFE, 0x01, 0x0A });
            File.WriteAllBytes(Path.Combine(five, "recipe.ibd"), new byte[] { 1, 2, 3 });

            string note5 = string.Join(" / ", DbFolderReader.Read(five, null).Notes.ToArray());
            Check("5.x 임을 알려 줌", note5.Contains("5.x"), note5);
            Check("ibd2sdi 로는 안 된다고 알려 줌",
                  note5.Contains("SDI") && note5.Contains("8.0"), note5);
            Check("5.x 에 ibd2sdi 경로를 적으라고 하지 않음",
                  !note5.Contains("ibd2sdi 경로를"), note5);
            Check("대신 덤프 길을 알려 줌",
                  note5.Contains("mysqldump") && note5.Contains("ibdata1"), note5);

            // 8.0 : .ibd 만 (.frm 없음)
            string eight = Path.Combine(_dir, "eight");
            Directory.CreateDirectory(eight);
            File.WriteAllBytes(Path.Combine(eight, "recipe.ibd"), new byte[] { 1, 2, 3 });

            string note8 = string.Join(" / ", DbFolderReader.Read(eight, null).Notes.ToArray());
            Check("8.0 쪽은 ibd2sdi 경로를 알려 줌", note8.Contains("ibd2sdi 경로를"), note8);
            Check("8.0 쪽은 5.x 라고 하지 않음", !note8.Contains("5.x"), note8);
            Check("값은 못 읽는다고 밝힘", note8.Contains("값은 못 읽"), note8);
        }

        /// <summary>
        /// .frm 자리값대로 파일을 <b>만들어서</b> 읽혀 봅니다.
        ///
        /// <b>이 시험의 한계를 분명히 해 둡니다.</b> 만드는 쪽과 읽는 쪽이 같은
        /// 자리값 표를 쓰므로, 이 시험이 지키는 것은 "자리값 표대로 읽는가" 와
        /// "어긋난 파일에서 터지지 않고 이유를 적는가" 입니다. <b>진짜 MySQL 이
        /// 쓴 파일과 맞는지는 증명하지 못합니다</b> — 그건 실제 .frm 하나를
        /// 읽혀 봐야 합니다. 그래서 읽은 판 번호를 화면에 적게 해 뒀습니다
        /// (5.6.19 로 찍히면 머리 부분은 제대로 읽은 것입니다).
        /// </summary>
        private static void FrmShape()
        {
            Say("DB — .frm 에서 표 모양 읽기");

            byte[] frm = BuildFrm(50619);
            FrmReader.Result r = FrmReader.Read(frm, "recipe");

            Check("읽음", r.Ok, r.Why);
            Check("판 번호", r.VersionId == 50619, "실제 " + r.VersionId);
            Check("판 번호 글자", DbFolderReader.VersionText(r.VersionId) == "5.6.19",
                  DbFolderReader.VersionText(r.VersionId));

            DbTable t = r.Table;
            Check("표 이름", t.Name == "recipe", t.Name);
            Check("값은 없음", !t.HasRows, null);
            Check("열 다섯", t.Columns.Count == 5, "실제 " + t.Columns.Count);

            Check("열 이름 차례",
                  t.Columns[0].Name == "id" && t.Columns[1].Name == "name"
                  && t.Columns[2].Name == "temp_max" && t.Columns[3].Name == "made_at",
                  string.Join(",", Names(t)));
            // 이름은 UTF-8 입니다. 한글 열 이름이 깨지면 표가 달라 보입니다.
            Check("한글 열 이름", t.Columns[4].Name == "상태", t.Columns[4].Name);

            Check("int(11)", t.Columns[0].Type == "int(11)", t.Columns[0].Type);
            // .frm 의 길이는 바이트입니다. 문자셋으로 안 나누면 varchar(192) 가 됩니다.
            Check("varchar 는 글자 수로", t.Columns[1].Type == "varchar(64)", t.Columns[1].Type);
            Check("decimal(6,2)", t.Columns[2].Type == "decimal(6,2)", t.Columns[2].Type);
            Check("datetime(3)", t.Columns[3].Type == "datetime(3)", t.Columns[3].Type);
            Check("enum 값까지", t.Columns[4].Type == "enum('on','off')", t.Columns[4].Type);

            Check("NOT NULL 을 읽음", !t.Columns[0].Nullable, null);
            Check("NULL 허용을 읽음", t.Columns[1].Nullable, null);

            Check("기본 키를 찾음", t.Columns[0].IsKey && t.HasKey, null);
            Check("키가 아닌 열엔 표시 없음", !t.Columns[1].IsKey, null);

            // 기본값은 .frm 에서 읽지 않습니다. "없음" 이 아니라 "모름" 이어야
            // 합니다 — 없음으로 치면 덤프 쪽(DEFAULT '0')과 견줄 때 없는 차이가
            // 생기고, 그걸 맞추는 ALTER 까지 만들어 줍니다.
            for (int i = 0; i < t.Columns.Count; i++)
                if (t.Columns[i].DefaultKnown) { Check("기본값은 모른다고 둠", false, t.Columns[i].Name); break; }
            Check("기본값은 모른다고 둠", !t.Columns[0].DefaultKnown, null);

            // 타입 글자에 NOT NULL 을 섞지 않습니다 (Nullable 로 따로 들고 있습니다).
            Check("타입에 NOT NULL 을 안 섞음", t.Columns[0].Type.IndexOf("NULL") < 0, t.Columns[0].Type);

            // ---- 모양이 아닌 파일 ----
            var res = FrmReader.Read(new byte[] { 1, 2, 3 }, "x");
            Check("짧은 파일은 이유를 적음", !res.Ok && res.Why.Contains("짧"), res.Why);

            byte[] bad = BuildFrm(50619);
            bad[0] = 0x00;
            res = FrmReader.Read(bad, "x");
            Check("머리 표식이 다르면 이유를 적음", !res.Ok && res.Why.Contains("표식"), res.Why);

            byte[] view = Encoding.ASCII.GetBytes("TYPE=VIEW\nquery=select 1\n" + new string(' ', 60));
            res = FrmReader.Read(view, "v");
            Check("뷰는 뷰라고 적음", !res.Ok && res.Why.Contains("뷰"), res.Why);

            // 잘린 파일 — 터지지 않고 이유를 적어야 합니다.
            byte[] cut = BuildFrm(50619);
            var shorter = new byte[cut.Length - 40];
            Array.Copy(cut, shorter, shorter.Length);
            res = FrmReader.Read(shorter, "x");
            Check("잘린 파일도 터지지 않음", !res.Ok && res.Why.Length > 0, res.Why);

            // 열 수가 이름 수와 안 맞으면 조용히 넘기지 않습니다.
            byte[] miscount = BuildFrm(50619);
            Put16(miscount, FrmFormInfo + 258, 9);      // 열이 9 개라고 거짓으로 적음
            res = FrmReader.Read(miscount, "x");
            Check("열 수가 안 맞으면 이유를 적음", !res.Ok && res.Why.Length > 0, res.Why);

            // ---- 파일 이름이 표 이름 (MySQL 은 못 쓰는 글자를 @00NN 로 바꿔 둡니다) ----
            Check("한글 표 이름을 되돌림", FrmReader.Unescape("@c628@b3c4") == "온도",
                  FrmReader.Unescape("@c628@b3c4"));
            Check("@ 가 없으면 그대로", FrmReader.Unescape("recipe") == "recipe", null);
            // 모르는 꼴은 그대로 둡니다. 억지로 바꾸면 표가 없어진 것처럼 보입니다.
            Check("모르는 꼴은 그대로", FrmReader.Unescape("a@zz") == "a@zz", FrmReader.Unescape("a@zz"));

            Check("5.0 미만", DbFolderReader.VersionText(0) == "5.0 미만", null);
        }

        private static string[] Names(DbTable t)
        {
            var s = new string[t.Columns.Count];
            for (int i = 0; i < t.Columns.Count; i++) s[i] = t.Columns[i].Name;
            return s;
        }

        /// <summary>
        /// .frm 과 덤프를 섞어 읽을 때, <b>기본값을 모르는 쪽 때문에 없는 차이가
        /// 생기지 않는지</b>. 이게 .frm 읽기에서 가장 조용히 틀릴 수 있는 자리입니다.
        /// </summary>
        private static void FrmDefaultUnknown()
        {
            Say("DB — 모르는 기본값을 차이로 세지 않기");

            var shape = new DbColumn { Name = "a", Type = "int(11)", Nullable = true };
            shape.Default = null; shape.DefaultKnown = false;        // .frm 에서 읽은 열
            var dumped = new DbColumn { Name = "a", Type = "int(11)", Nullable = true };
            dumped.Default = "0"; dumped.DefaultKnown = true;        // 덤프에서 읽은 열

            var b = new DbTable { Name = "t" }; b.Columns.Add(shape);
            var a = new DbTable { Name = "t" }; a.Columns.Add(dumped);

            TableDiff td = DbDiff.CompareTable(b, a);
            Check("기본값을 모르면 차이로 세지 않음", !td.Columns[0].DefaultChanged,
                  td.Columns[0].What());
            Check("모른다고 표시됨", !td.Columns[0].DefaultKnown, null);
            Check("그래서 열이 같음으로 남음", td.Columns[0].Change == DbChange.Same,
                  td.Columns[0].Change.ToString());

            // 양쪽을 다 알면 그때는 견줍니다.
            var k1 = new DbColumn { Name = "a", Type = "int(11)", Nullable = true, Default = "1" };
            var k2 = new DbColumn { Name = "a", Type = "int(11)", Nullable = true, Default = "2" };
            var b2 = new DbTable { Name = "t" }; b2.Columns.Add(k1);
            var a2 = new DbTable { Name = "t" }; a2.Columns.Add(k2);
            TableDiff td2 = DbDiff.CompareTable(b2, a2);
            Check("양쪽을 알면 기본값을 견줌", td2.Columns[0].DefaultChanged, td2.Columns[0].What());

            // 모양(Shape)에도 모르는 기본값은 적지 않습니다 — 적으면 이름 변경
            // 짐작이 엉뚱해집니다.
            Check("모양에 모르는 기본값을 안 적음", shape.Shape().IndexOf("DEFAULT") < 0, shape.Shape());
            Check("아는 기본값은 모양에 적음", k1.Shape().IndexOf("DEFAULT") >= 0, k1.Shape());
        }

        /// <summary>폴더에서 .frm 을 읽고, 같은 이름의 덤프가 있으면 덤프가 이기는지.</summary>
        private static void FrmInFolder()
        {
            Say("DB — 폴더의 .frm");

            string dir = Path.Combine(_dir, "frmdir");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "recipe.frm"), BuildFrm(50619));
            File.WriteAllBytes(Path.Combine(dir, "recipe.ibd"), new byte[] { 1, 2, 3 });

            DbSnapshot snap = DbFolderReader.Read(dir, null);
            string notes = string.Join(" / ", snap.Notes.ToArray());

            Check("frm 에서 표를 읽음", snap.Find("recipe") != null, notes);
            Check("판 번호를 적음", notes.Contains("5.6.19"), notes);
            Check("값은 없다고 적음", notes.Contains("값과 기본값"), notes);
            Check("ibd 는 5.x 라고 적음", notes.Contains("5.x"), notes);

            // 같은 이름의 덤프가 있으면 값까지 있는 쪽이 이깁니다. .frm 이
            // 나중에 읽혀도 덮지 않아야 합니다 (아는 것이 줄어듭니다).
            File.WriteAllText(Path.Combine(dir, "recipe.sql"),
                "CREATE TABLE `recipe` (`id` int NOT NULL, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1);\n", new UTF8Encoding(false));

            DbSnapshot both = DbFolderReader.Read(dir, null);
            DbTable t = both.Find("recipe");
            Check("덤프가 이김", t != null && t.HasRows, t == null ? "없음" : "HasRows=" + t.HasRows);
            Check("표는 하나만", both.Tables.Count == 1, "실제 " + both.Tables.Count);
            string notes2 = string.Join(" / ", both.Notes.ToArray());
            Check("안 쓴 frm 을 밝힘", notes2.Contains("쓰지 않았습니다"), notes2);
        }

        // ---- 시험용 .frm 만들기 ----
        //
        // 자리값은 FrmReader 의 주석과 같은 표입니다. 그래서 이 시험은
        // "표대로 읽는가" 만 지킵니다 (FrmShape 의 설명 참고).

        private const int FrmKeyAt = 68;
        private const int FrmKeyLen = 32;
        private const int FrmFormInfo = FrmKeyAt + FrmKeyLen;   // 100

        private static void Put16(byte[] b, int at, int v)
        {
            b[at] = (byte)(v & 0xFF);
            b[at + 1] = (byte)((v >> 8) & 0xFF);
        }

        private static void Put32(byte[] b, int at, int v)
        {
            b[at] = (byte)(v & 0xFF);
            b[at + 1] = (byte)((v >> 8) & 0xFF);
            b[at + 2] = (byte)((v >> 16) & 0xFF);
            b[at + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static int Put(byte[] b, int at, byte[] v)
        {
            Array.Copy(v, 0, b, at, v.Length);
            return at + v.Length;
        }

        private static byte[] BuildFrm(int versionId)
        {
            var names = new List<byte>();
            names.Add(0x03);                                  // 앞 한 바이트 (버립니다)
            foreach (string n in new string[] { "id", "name", "temp_max", "made_at", "상태" })
            {
                names.AddRange(new UTF8Encoding(false).GetBytes(n));
                names.Add(0xFF);
            }
            names.Add(0x00); names.Add(0x00);                 // 뒤 두 바이트 (버립니다)

            // ENUM 값 묶음: 앞뒤 한 바이트를 버리고 0xFF 로 가릅니다.
            var labels = new List<byte>();
            labels.Add(0x02);
            labels.AddRange(Encoding.ASCII.GetBytes("on"));
            labels.Add(0xFF);
            labels.AddRange(Encoding.ASCII.GetBytes("off"));
            labels.Add(0xFF);
            labels.Add(0x00);

            int columns = 5;
            int metaLen = 17 * columns;
            int total = FrmFormInfo + 288 + metaLen + names.Count + labels.Count + 64;
            var b = new byte[total];

            // ---- 머리 ----
            b[0] = 0xFE; b[1] = 0x01;
            b[3] = 0x0C;                        // InnoDB
            Put16(b, 0x04, 0);                  // 이 뒤(64)에 forminfo 자리가 적힙니다
            Put16(b, 0x06, FrmKeyAt);
            Put16(b, 0x0E, FrmKeyLen);
            Put16(b, 0x10, 0);                  // 기본값 조각 길이
            Put32(b, 0x33, versionId);
            Put32(b, 0x37, 0);                  // extrainfo 길이
            Put32(b, 64, FrmFormInfo);

            // ---- 키: PRIMARY (id) ----
            int k = FrmKeyAt;
            b[k] = 1;                           // 키 1 개
            b[k + 1] = 1;                       // 조각 1 개
            Put16(b, k + 4, 9);                 // 이름 조각 길이
            int kh = k + 6;
            Put16(b, kh + 0, 1 ^ 1);            // flags: MySQL 은 HA_NOSAME 과 XOR 해서 적습니다
            Put16(b, kh + 2, 4);                // 길이
            b[kh + 4] = 1;                      // 조각 수
            b[kh + 5] = 1;                      // BTREE
            Put16(b, kh + 6, 0);                // key_block_size
            int kp = kh + 8;
            Put16(b, kp + 0, 1);                // 첫 열 (1 부터)
            Put16(b, kp + 2, 1);                // 자리
            b[kp + 4] = 0;                      // flags
            Put16(b, kp + 5, 0);                // key_type
            Put16(b, kp + 7, 4);                // 길이
            Put(b, kp + 9, new byte[] { (byte)'P', (byte)'R', (byte)'I', (byte)'M',
                                        (byte)'A', (byte)'R', (byte)'Y', 0xFF, 0x00 });

            // ---- forminfo ----
            Put16(b, FrmFormInfo + 258, columns);
            Put16(b, FrmFormInfo + 260, 0);                 // screens 길이
            Put16(b, FrmFormInfo + 268, names.Count);
            Put16(b, FrmFormInfo + 274, labels.Count);
            Put16(b, FrmFormInfo + 282, 3);                 // NULL 허용 열 수
            Put16(b, FrmFormInfo + 284, 0);                 // 주석 길이

            // ---- 열 정보 (17 바이트씩) ----
            int m = FrmFormInfo + 288;
            //            길이  flags                 타입  문자셋  label
            PutColumn(b, m + 17 * 0, 11, 1, 3, 63, 0);                  // int(11) NOT NULL
            PutColumn(b, m + 17 * 1, 192, 32768, 15, 33, 0);            // varchar(64) NULL (utf8)
            PutColumn(b, m + 17 * 2, 8, 32768 | (2 << 8), 246, 63, 0);  // decimal(6,2) NULL
            PutColumn(b, m + 17 * 3, 23, 1, 18, 63, 0);                 // datetime(3) NOT NULL
            PutColumn(b, m + 17 * 4, 3, 32768, 247, 33, 1);             // enum('on','off') NULL

            int at = m + metaLen;
            at = Put(b, at, names.ToArray());
            at = Put(b, at, labels.ToArray());

            var cut = new byte[at];
            Array.Copy(b, cut, at);
            return cut;
        }

        private static void PutColumn(byte[] b, int at, int length, int flags,
                                      int type, int charset, int labelId)
        {
            Put16(b, at + 3, length);
            Put16(b, at + 8, flags);
            b[at + 10] = 0;                             // unireg
            b[at + 11] = (byte)((charset >> 8) & 0xFF);
            b[at + 12] = (byte)labelId;
            b[at + 13] = (byte)type;
            b[at + 14] = (byte)(charset & 0xFF);
            Put16(b, at + 15, 0);                       // 주석 길이
        }


        /// <summary>
        /// 선 색이 무엇을 가리키는지 정하는 규칙.
        ///
        /// 이 규칙을 선 그리는 쪽과 범례 적는 쪽이 <b>각자</b> 판단하고
        /// 있었습니다. 한쪽만 고친 날부터 범례가 "파란 선은 이전" 이라고
        /// 하면서 선은 다른 색인 일이 생깁니다. 그래서 한 줄로 모았고,
        /// 여기서 그 한 줄을 지킵니다.
        /// </summary>
        private static void TraceColorRule()
        {
            Say("그래프 — 선 색이 무엇을 가리키나");

            // 레인으로 나눠 보면 IO 마다 칸이 따로라 색으로 IO 를 가릴 일이
            // 없습니다. 그때 색은 이전(파랑)/이후(빨강)입니다.
            Check("레인 · IO 하나면 이전/이후 색", !TraceColors.ByIo(true, 1), null);
            Check("레인 · IO 여럿이어도 이전/이후 색", !TraceColors.ByIo(true, 9), null);

            // 겹쳐 보기에서 IO 가 하나면 가릴 IO 가 없습니다.
            Check("겹쳐 · IO 하나면 이전/이후 색", !TraceColors.ByIo(false, 1), null);
            Check("겹쳐 · IO 없어도 이전/이후 색", !TraceColors.ByIo(false, 0), null);

            // 겹쳐 보기에서 IO 가 여럿이면 색이 IO 를 가립니다. 모두
            // 파랑·빨강으로 그리면 어느 선이 어느 IO 인지 알 수 없습니다.
            Check("겹쳐 · IO 둘이면 IO 색", TraceColors.ByIo(false, 2), null);
            Check("겹쳐 · IO 여럿이면 IO 색", TraceColors.ByIo(false, 30), null);

            // 범례는 색이 이전/이후를 가리킬 때만 나옵니다. 거꾸로 나오면
            // 거짓말이 됩니다 — 없는 것보다 나쁩니다.
            for (int n = 0; n <= 4; n++)
            {
                foreach (bool lane in new bool[] { true, false })
                {
                    if (TraceColors.ShowSideLegend(lane, n) == TraceColors.ByIo(lane, n))
                    {
                        Check("범례는 IO 색일 때 숨음", false, "lane=" + lane + " n=" + n);
                        return;
                    }
                }
            }
            Check("범례는 IO 색일 때 숨음", true, null);
        }

        /// <summary>
        /// 폴더가 아니라 <b>파일 하나</b>를 줘도 읽히는지. 세 가지 다 봅니다.
        ///
        /// 화면에서 [파일] 로 고르거나 칸에 파일을 끌어다 놓으면 이 길로
        /// 들어옵니다. 폴더만 되는 줄 알고 쓰면 "읽을 게 없다" 는 말만 보게
        /// 됩니다.
        /// </summary>
        private static void DbSingleFile()
        {
            Say("DB — 파일 하나만 줘도 읽기");

            string sql = WriteText("one.sql",
                "CREATE TABLE `recipe` (`id` int NOT NULL, `name` varchar(64), PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1,'가열 A');\n");
            DbSnapshot a = DbFolderReader.Read(sql, null);
            Check(".sql 하나", a.Find("recipe") != null, Join(a.Notes));
            Check(".sql 하나는 값까지", a.Find("recipe").Rows.Count == 1, null);
            // 파일 하나를 줬으면 표 이름 앞에 폴더 이름을 붙이지 않습니다.
            Check(".sql 하나는 이름 그대로", a.Tables[0].Name == "recipe", a.Tables[0].Name);

            string csv = WriteText("설비.csv", "id,name\n1,가열\n2,냉각\n");
            DbSnapshot b = DbFolderReader.Read(csv, null);
            Check(".csv 하나", b.Find("설비") != null, Join(b.Notes));
            Check(".csv 하나는 행 둘", b.Find("설비").Rows.Count == 2, null);

            string frm = Path.Combine(_dir, "온도.frm");
            File.WriteAllBytes(frm, BuildFrm(50619));
            DbSnapshot c = DbFolderReader.Read(frm, null);
            Check(".frm 하나", c.Tables.Count == 1, Join(c.Notes));
            Check(".frm 하나는 모양만", !c.Tables[0].HasRows, null);
            Check(".frm 하나도 판 번호를 적음", Join(c.Notes).Contains("5.6.19"), Join(c.Notes));

            // 없는 파일과 읽을 수 없는 파일은 조용히 넘기지 않습니다.
            DbSnapshot none = DbFolderReader.Read(Path.Combine(_dir, "없는파일.sql"), null);
            Check("없는 자리는 그렇다고", none.Tables.Count == 0 && none.Notes.Count > 0, null);

            string junk = WriteText("쓰레기.frm", "이건 frm 이 아닙니다");
            DbSnapshot bad = DbFolderReader.Read(junk, null);
            Check("못 읽은 파일은 이유를 적음",
                  bad.Tables.Count == 0 && Join(bad.Notes).Contains("읽지 못"), Join(bad.Notes));
        }

        private static string Join(List<string> notes)
        {
            return string.Join(" / ", notes.ToArray());
        }

        private static void DbCompare()
        {
            Say("DB — 두 벌 견주기");

            string before =
                "CREATE TABLE `recipe` (`id` int NOT NULL, `name` varchar(64) NOT NULL," +
                " `temp_max` decimal(6,2) DEFAULT NULL, `old_only` int, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1,'가열 A',182.50,7),(2,'가열 B',150.00,8),(3,'사라질 행',1.00,9);\n" +
                // gone 과 born 의 <b>열 구성을 일부러 다르게</b> 뒀습니다.
                // 0.86 부터 "이름이 달라도 열 구성이 똑같으면 짝짓기" 가
                // 생겼습니다. 둘 다 (k) 한 열이면 그 규칙에 걸려 짝지어지고,
                // 그러면 이 시험이 보려는 것("한쪽에만 있는 표")이 사라집니다.
                "CREATE TABLE `gone` (`k` int NOT NULL, `only_here` int, PRIMARY KEY (`k`));\n" +
                "INSERT INTO `gone` VALUES (1,1);\n";

            string after =
                "CREATE TABLE `recipe` (`id` int NOT NULL, `name` varchar(128) NOT NULL," +
                " `temp_max` decimal(6,2) DEFAULT NULL, `renamed_only` int, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1,'가열 A',999.90,7),(2,'가열 B',150.00,8),(4,'새 행',2.00,10);\n" +
                "CREATE TABLE `born` (`k` int NOT NULL, PRIMARY KEY (`k`));\n" +
                "INSERT INTO `born` VALUES (1);\n";

            var b = new DbSnapshot();
            SqlDumpReader.Read(WriteText("cmp_before.sql", before), b, 0);
            var a = new DbSnapshot();
            SqlDumpReader.Read(WriteText("cmp_after.sql", after), a, 0);

            DbDiffResult d = DbDiff.Compare(b, a);

            Check("이전에만 있는 표", d.TablesOnlyBefore == 1, "실제 " + d.TablesOnlyBefore);
            Check("이후에만 있는 표", d.TablesOnlyAfter == 1, "실제 " + d.TablesOnlyAfter);
            Check("달라진 표", d.TablesChanged == 1, "실제 " + d.TablesChanged);
            Check("달라진 표가 위로", d.Tables[0].Change == DbChange.Changed, d.Tables[0].Change.ToString());

            TableDiff td = null;
            for (int i = 0; i < d.Tables.Count; i++) if (d.Tables[i].Name == "recipe") td = d.Tables[i];
            Check("recipe 를 찾음", td != null, null);

            ColumnDiff name = null, oldOnly = null, newOnly = null;
            for (int i = 0; i < td.Columns.Count; i++)
            {
                if (td.Columns[i].Name == "name") name = td.Columns[i];
                if (td.Columns[i].Name == "old_only") oldOnly = td.Columns[i];
                if (td.Columns[i].Name == "renamed_only") newOnly = td.Columns[i];
            }

            Check("타입 변경을 잡음", name != null && name.TypeChanged, null);
            Check("타입 변경을 적음",
                  name.What().Contains("varchar(64)") && name.What().Contains("varchar(128)"), name.What());

            // 모양과 자리가 같은 짝 하나뿐이면 "이름이 바뀐 듯" 으로 짐작합니다.
            Check("없어진 열", oldOnly != null && oldOnly.Change == DbChange.OnlyBefore, null);
            Check("이름 변경을 짐작", oldOnly.RenameGuess == "renamed_only", oldOnly.RenameGuess);
            Check("짝의 반대쪽도 짐작", newOnly != null && newOnly.RenameGuess == "old_only", null);
            // 짐작은 "~ 듯" 으로만 적습니다. 단정하면 그걸 믿고 ALTER 를 돌립니다.
            Check("단정하지 않음", oldOnly.What().Contains("듯"), oldOnly.What());

            Check("값 바뀐 행", td.RowsChanged == 1, "실제 " + td.RowsChanged);
            Check("없어진 행", td.RowsRemoved == 1, "실제 " + td.RowsRemoved);
            Check("생긴 행", td.RowsAdded == 1, "실제 " + td.RowsAdded);

            RowDiff changed = null;
            for (int i = 0; i < td.Rows.Count; i++)
                if (td.Rows[i].Change == DbChange.Changed) changed = td.Rows[i];
            Check("바뀐 열만 집어냄", changed != null && changed.ChangedColumns.Count == 1, null);
            Check("그 열이 temp_max", td.Before.Columns[changed.ChangedColumns[0]].Name == "temp_max",
                  td.Before.Columns[changed.ChangedColumns[0]].Name);

            // 키가 없으면 짝지을 수 없다고 밝힙니다.
            var kb = new DbSnapshot();
            SqlDumpReader.Read(WriteText("nokey_b.sql",
                "CREATE TABLE `t` (`a` int, `b` int);\nINSERT INTO `t` VALUES (1,2);\n"), kb, 0);
            var ka = new DbSnapshot();
            SqlDumpReader.Read(WriteText("nokey_a.sql",
                "CREATE TABLE `t` (`a` int, `b` int);\nINSERT INTO `t` VALUES (1,3);\n"), ka, 0);

            TableDiff kt = DbDiff.Compare(kb, ka).Tables[0];
            Check("키가 없으면 지우고 생긴 것으로",
                  kt.RowsRemoved == 1 && kt.RowsAdded == 1 && kt.RowsChanged == 0, kt.Summary());
            Check("그렇다고 밝힘", kt.RowNote.Contains("기본 키가 없어"), kt.RowNote);

            // 행을 못 읽은 표(모양만)는 "행 차이 없음" 이 아니라 "못 읽음" 입니다.
            var sb2 = new DbSnapshot();
            var shape = new DbTable(); shape.Name = "x"; shape.HasRows = false;
            shape.Columns.Add(new DbColumn { Name = "k", Type = "int" });
            sb2.Tables.Add(shape);
            var sa2 = new DbSnapshot();
            var shape2 = new DbTable(); shape2.Name = "x"; shape2.HasRows = false;
            shape2.Columns.Add(new DbColumn { Name = "k", Type = "int" });
            sa2.Tables.Add(shape2);
            TableDiff st = DbDiff.Compare(sb2, sa2).Tables[0];
            Check("행을 못 읽었다고 밝힘", st.RowNote.Contains("행을 읽지 못했"), st.RowNote);
        }

        /// <summary>
        /// 차이를 칸으로 쪼갠 목록. 글 한 덩이로 적던 것을 목록으로 바꿨습니다.
        ///
        /// 여기서 지키려는 것은 네 가지입니다.
        ///   1. 값이 바뀐 행은 <b>바뀐 열마다</b> 한 줄 (한 줄에 값 하나)
        ///   2. 생기거나 없어진 행도 열마다 한 줄, 없는 쪽 칸은 <b>빈 칸</b>
        ///      (NULL 과 다릅니다 — NULL 은 값이 NULL 인 것입니다)
        ///   3. 표 자체가 한쪽에만 있으면 <b>행은 펼치지 않음</b>
        ///   4. 상한에 닿으면 그렇다고 적음
        /// </summary>
        private static void DbDiffLines()
        {
            Say("DB — 차이를 칸으로 쪼갠 목록");

            var b = new DbSnapshot();
            SqlDumpReader.Read(WriteText("ln_before.sql",
                "CREATE TABLE `t` (`id` int NOT NULL, `a` varchar(10), `b` int, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `t` VALUES (1,'x',10),(2,'y',20),(3,NULL,30);\n" +
                "CREATE TABLE `only_b` (`k` int NOT NULL, PRIMARY KEY (`k`));\n" +
                "INSERT INTO `only_b` VALUES (1),(2);\n"), b, 0);

            var a = new DbSnapshot();
            SqlDumpReader.Read(WriteText("ln_after.sql",
                "CREATE TABLE `t` (`id` int NOT NULL, `a` varchar(20), `b` int, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `t` VALUES (1,'X',11),(2,'y',20),(4,'new',40);\n"), a, 0);

            DbDiffResult d = DbDiff.Compare(b, a);
            List<DbDiffLine> lines = DbDiffList.Build(d, null);

            // ---- 열 ----
            DbDiffLine col = Line(lines, "t", "열", "a");
            Check("열 차이가 한 줄", col != null, null);
            Check("열의 이전 모양", col.Before.Contains("VARCHAR(10)"), col.Before);
            Check("열의 이후 모양", col.After.Contains("VARCHAR(20)"), col.After);

            // ---- 값이 바뀐 행 : 바뀐 열마다 한 줄 ----
            int changed = 0;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == DbDiffLine.KindRow && lines[i].Change == DbDiffLine.ChangeDiff) changed++;
            Check("바뀐 열마다 한 줄", changed == 2, "실제 " + changed);

            DbDiffLine bb = null;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == DbDiffLine.KindRow && lines[i].Column == "b" && lines[i].Change == DbDiffLine.ChangeDiff)
                    bb = lines[i];
            Check("바뀐 값이 칸에 하나씩", bb != null && bb.Before == "10" && bb.After == "11",
                  bb == null ? "없음" : bb.Before + " / " + bb.After);
            Check("어느 행인지 적힘", bb.Where == "id=1", bb.Where);

            // ---- 없어진 행 : 열마다 한 줄, 이후 칸은 빈 칸 ----
            //
            // 0.87 부터 <b>열쇠 열은 빼고</b> 셉니다. 그 값은 이미 IO명 칸에
            // 있어서, 적으면 "TEMP_MAX → TEMP_MAX" 같은 줄이 하나 더 생기고
            // 없어진 행 하나가 줄 둘로 보입니다. 여기 표는 열이 셋(id · a · b)
            // 이고 id 가 열쇠라 둘이 됩니다.
            var gone = new List<DbDiffLine>();
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == DbDiffLine.KindRow && lines[i].Change == DbDiffLine.ChangeGone) gone.Add(lines[i]);
            Check("없어진 행을 열마다 펼침 (열쇠 열 빼고)", gone.Count == 2, "실제 " + gone.Count);
            Check("없어진 행의 이후 칸은 빔", gone[0].After.Length == 0, gone[0].After);

            // NULL 은 NULL 로 적습니다. "없는 쪽" 의 빈 칸과 다른 것입니다.
            DbDiffLine nul = null;
            for (int i = 0; i < gone.Count; i++) if (gone[i].Column == "a") nul = gone[i];
            Check("NULL 은 NULL 로", nul != null && nul.Before == "NULL",
                  nul == null ? "없음" : nul.Before);

            var born = new List<DbDiffLine>();
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == DbDiffLine.KindRow && lines[i].Change == DbDiffLine.ChangeAdded) born.Add(lines[i]);
            Check("생긴 행도 열마다 (열쇠 열 빼고)", born.Count == 2, "실제 " + born.Count);
            Check("생긴 행의 이전 칸은 빔", born[0].Before.Length == 0, born[0].Before);

            // ---- 한쪽에만 있는 표 : 행은 펼치지 않습니다 ----
            int onlyRows = 0, onlyAll = 0;
            DbDiffLine head = null;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Table != "only_b") continue;
                onlyAll++;
                if (lines[i].Kind == DbDiffLine.KindRow) onlyRows++;
                if (lines[i].Kind == DbDiffLine.KindNote && lines[i].Note.Contains("only_b")) head = lines[i];
            }
            Check("표가 한쪽에만 있으면 행은 안 펼침", onlyRows == 0, "실제 " + onlyRows);
            Check("그래도 열 모양은 적음", onlyAll == 2, "실제 " + onlyAll);
            Check("행 수는 알려 줌", head != null && head.Note.Contains("행 2"),
                  head == null ? "없음" : head.Note);

            // ---- 같은 표는 줄을 만들지 않습니다 ----
            TableDiff same = null;
            for (int i = 0; i < d.Tables.Count; i++)
                if (d.Tables[i].Change == DbChange.Same) same = d.Tables[i];
            if (same != null)
                Check("같은 표는 줄이 없음", DbDiffList.Build(same, null).Count == 0, null);

            // ---- 표 하나만 뽑기 ----
            TableDiff t = null;
            for (int i = 0; i < d.Tables.Count; i++) if (d.Tables[i].Name == "t") t = d.Tables[i];
            List<DbDiffLine> one = DbDiffList.Build(t, null);
            for (int i = 0; i < one.Count; i++)
                if (one[i].Table != "t") { Check("한 표만 뽑기", false, one[i].Table); break; }
            Check("한 표만 뽑기", one.Count > 0 && one.Count < lines.Count,
                  one.Count + " / " + lines.Count);

            // ---- 상한 : 끊었으면 끊었다고 적습니다 ----
            var opt = new DbDiffList.Options();
            opt.MaxLinesPerTable = 2;
            List<DbDiffLine> cut = DbDiffList.Build(d, opt);
            bool said = false;
            for (int i = 0; i < cut.Count; i++)
                if (cut[i].Kind == DbDiffLine.KindNote && cut[i].Note.Contains("상한")) said = true;
            Check("표 상한을 밝힘", said, null);
            // 한 표가 상한에 걸려도 다음 표는 그대로 나옵니다.
            int after = 0;
            for (int i = 0; i < cut.Count; i++) if (cut[i].Table == "only_b") after++;
            Check("상한은 표마다 따로", after == 2, "실제 " + after);

            var opt2 = new DbDiffList.Options();
            opt2.MaxLines = 2;
            List<DbDiffLine> cut2 = DbDiffList.Build(d, opt2);
            Check("목록 상한을 지킴", cut2.Count == 3, "실제 " + cut2.Count);
            Check("목록 상한을 밝힘", cut2[cut2.Count - 1].Note.Contains("상한"),
                  cut2[cut2.Count - 1].Note);

            // ---- 행을 묶어 보는 선택 ----
            var opt3 = new DbDiffList.Options();
            opt3.ExpandWholeRows = false;
            List<DbDiffLine> flat = DbDiffList.Build(d, opt3);
            int flatGone = 0;
            for (int i = 0; i < flat.Count; i++)
                if (flat[i].Kind == DbDiffLine.KindRow && flat[i].Change == DbDiffLine.ChangeGone) flatGone++;
            Check("묶으면 행 하나에 한 줄", flatGone == 1, "실제 " + flatGone);

            // ---- CSV ----
            string csv = DbDiffList.ToCsv(lines);
            // 머리글 글자를 여기 또 적지 않습니다 — 이름을 바꿀 때 고칠 곳이
            // 하나(DbNames)여야 합니다. 여기서는 "머리글이 그 이름들과 같은
            // 순서로 나오는지" 만 봅니다.
            Check("CSV 머리글", csv.StartsWith(string.Join(",", DbNames.CsvNames())),
                  csv.Substring(0, 40));
            Check("CSV 칸 수", DbNames.CsvNames().Length == 8,
                  DbNames.CsvNames().Length.ToString(CultureInfo.InvariantCulture));
            Check("CSV 줄 수", CountLines(csv) == lines.Count + 1,
                  CountLines(csv) + " / " + (lines.Count + 1));

            var tricky = new List<DbDiffLine>();
            tricky.Add(new DbDiffLine { Table = "t", Before = "a,b", After = "그가 \"말\"했다" });
            string csv2 = DbDiffList.ToCsv(tricky);
            Check("쉼표 든 값은 따옴표로", csv2.Contains("\"a,b\""), csv2);
            Check("따옴표는 두 번으로", csv2.Contains("\"그가 \"\"말\"\"했다\""), csv2);

            // IO 이름은 "그 줄을 가리키는 가장 좁은 이름" 입니다.
            //
            // 열쇠가 <b>한 열</b>이면 그 값만 적습니다 (0.87) — `.csv` 는
            // 3 번 열이 열쇠고 거기 IO 이름이 들어 있어서, "io=TEMP_MAX" 가
            // 아니라 "TEMP_MAX" 가 나와야 합니다. 여기 id 도 한 열이라
            // "id=1" 이 아니라 "1" 입니다. 행을 가리키는 글("id=1")은
            // Where 칸에 그대로 있습니다.
            Check("행 줄의 IO 는 열쇠 값", bb.Io == "1", bb.Io);
            Check("행 줄의 Where 는 열 이름까지", bb.Where == "id=1", bb.Where);
            Check("열 줄의 IO 는 열 이름", col.Io == "a", col.Io);
            // 0.87 부터 "파일 한쪽이 통째로 없다" 는 목록 줄이 아니라
            // <b>알림</b>입니다 (목록에서 "표" 갈래를 없앴습니다). 알림은
            // 목록 위에 적히므로 사실은 그대로 남습니다.
            Check("한쪽에만 있는 파일은 알림으로", head != null && head.Note.Contains("이전 쪽에만"),
                  head == null ? "못 찾음" : head.Note);

            // 갈래·구분에는 정해 둔 글자만 들어가야 합니다. 어딘가에서
            // 글자를 손으로 적어 넣으면 (상수를 안 쓰면) 여기서 걸립니다 —
            // DbVm 은 알림 줄을 빼낼 때 이 글자를 견주므로, 하나만 어긋나도
            // 알림이 목록에 섞여 나옵니다.
            var okKind = new List<string>
            {
                DbDiffLine.KindColumn,
                DbDiffLine.KindRow, DbDiffLine.KindNote,
            };
            var okChange = new List<string>
            {
                string.Empty, DbDiffLine.ChangeAdded, DbDiffLine.ChangeGone,
                DbDiffLine.ChangeDiff, DbDiffLine.ChangeMoved,
            };
            string strange = null;
            for (int i = 0; i < lines.Count && strange == null; i++)
            {
                if (!okKind.Contains(lines[i].Kind)) strange = "갈래 " + lines[i].Kind;
                else if (!okChange.Contains(lines[i].Change)) strange = "구분 " + lines[i].Change;
            }
            Check("갈래·구분은 정해 둔 글자뿐", strange == null, strange);

            // 마우스 설명에는 잘린 값이 다 들어 있어야 합니다.
            Check("설명 글에 값이 다 있음",
                  bb.Tip.Contains("10") && bb.Tip.Contains("11") && bb.Tip.Contains("id=1"), bb.Tip);
        }

        /// <summary>
        /// 목록 칸 이름은 <b>한 곳</b>(DbNames)에만 있어야 합니다.
        ///
        /// 전에는 같은 이름이 화면·CSV·설명 글·시험에 흩어져 있어서, 이름
        /// 하나 바꾸려면 네 파일을 고쳐야 했습니다. 한 군데를 빠뜨리면 화면과
        /// CSV 가 서로 다른 이름을 쓰게 되고, 그건 눈으로 봐야 알게 됩니다.
        ///
        /// 그래서 <b>이 시험에는 이름 글자가 하나도 없습니다.</b> 글자를 여기
        /// 적으면 이름을 바꿀 때 고칠 곳이 하나 더 생깁니다. 대신 "CSV 와
        /// 설명 글이 DbNames 를 따라가는지" 만 봅니다.
        /// </summary>
        private static void DbColumnNames()
        {
            Say("DB — 목록 칸 이름은 한 곳");

            Check("칸 수", DbNames.CsvNames().Length == 8,
                  DbNames.CsvNames().Length.ToString(CultureInfo.InvariantCulture));

            string[] names = DbNames.CsvNames();
            bool allFilled = true;
            for (int i = 0; i < names.Length; i++)
                if (string.IsNullOrEmpty(names[i])) allFilled = false;
            Check("빈 이름이 없음", allFilled, string.Join(" · ", names));

            // CSV 머리글은 DbNames 의 이름과 순서를 그대로 따라가야 합니다.
            string csv = DbDiffList.ToCsv(new List<DbDiffLine>());
            Check("CSV 머리글이 DbNames 를 따라감",
                  csv.StartsWith(string.Join(",", names)), csv);

            // 이름에 쉼표가 들어가도 CSV 가 깨지면 안 됩니다. 값과 같은
            // 함수로 찍으므로 따옴표가 붙습니다 (이름을 "설정값, 단위" 처럼
            // 바꿔 둘 수 있으니까요).
            var lines = new List<DbDiffLine>();
            lines.Add(new DbDiffLine { Io = "a,b" });
            Check("쉼표 든 값은 따옴표로", DbDiffList.ToCsv(lines).Contains("\"a,b\""),
                  DbDiffList.ToCsv(lines));
        }

        /// <summary>
        /// 인코딩을 가리는 규칙. <b>CP949 코드페이지가 없는 환경에서도</b>
        /// 돕니다 — 아래 CP949 덤프 시험은 리눅스의 mono 에서 건너뛰어지므로,
        /// 정작 고친 "어느 인코딩으로 읽을지" 를 아무도 안 보게 됩니다.
        /// 그래서 <b>결정만</b> 따로 봅니다 (글자로 바꾸지 않습니다).
        /// </summary>
        private static void TextSniffRules()
        {
            Say("글자 — 인코딩 가리기");

            const int Utf8Page = 65001;

            // CP949 로 "가나다" (B0A1 B3AA B4D9). UTF-8 로서는 올바르지 않습니다.
            byte[] cp949 = { 0xB0, 0xA1, 0xB3, 0xAA, 0xB4, 0xD9 };
            Check("CP949 바이트는 UTF-8 이 아님", !TextSniff.LooksUtf8(cp949, cp949.Length), null);
            Check("CP949 바이트는 UTF-8 로 읽지 않음",
                  TextSniff.Of(cp949, cp949.Length).CodePage != Utf8Page,
                  TextSniff.Of(cp949, cp949.Length).WebName);

            // UTF-8 로 "가나다"
            byte[] utf8 = Encoding.UTF8.GetBytes("가나다");
            Check("UTF-8 바이트는 UTF-8 로 읽음",
                  TextSniff.Of(utf8, utf8.Length).CodePage == Utf8Page,
                  TextSniff.Of(utf8, utf8.Length).WebName);

            // BOM 은 그대로 따릅니다
            byte[] bom = { 0xEF, 0xBB, 0xBF, (byte)'a' };
            Check("UTF-8 BOM", TextSniff.Of(bom, bom.Length).CodePage == Utf8Page, null);
            byte[] le = { 0xFF, 0xFE, (byte)'a', 0 };
            Check("UTF-16 LE BOM", TextSniff.Of(le, le.Length).CodePage == 1200, null);
            byte[] be = { 0xFE, 0xFF, 0, (byte)'a' };
            Check("UTF-16 BE BOM", TextSniff.Of(be, be.Length).CodePage == 1201, null);

            // ASCII 만 있으면 어느 쪽으로 읽어도 같습니다 — UTF-8 로 둡니다
            byte[] ascii = Encoding.ASCII.GetBytes("id,name\n1,a\n");
            Check("ASCII 는 UTF-8", TextSniff.Of(ascii, ascii.Length).CodePage == Utf8Page, null);

            // 빈 파일에서 터지지 않아야 합니다
            Check("빈 바이트도 괜찮음", TextSniff.Of(new byte[0], 0) != null, null);
            Check("null 도 괜찮음", TextSniff.Of(null, 0) != null, null);

            // 마지막 글자가 잘려 있어도 UTF-8 로 봅니다 (흘려 읽는 앞부분).
            byte[] cut = { 0xEA, 0xB0, 0x80, 0xEA, 0xB0 };   // "가" + 잘린 "가"
            Check("잘린 마지막 글자 때문에 CP949 로 넘어가지 않음",
                  TextSniff.Of(cut, cut.Length).CodePage == Utf8Page,
                  TextSniff.Of(cut, cut.Length).WebName);
        }

        /// <summary>
        /// 덤프의 <b>바이트</b> 쪽. 값이 쓰레기로 보이던 두 자리입니다.
        ///
        ///   1. CP949(ANSI)로 저장된 덤프 — 전에는 BOM 이 없으면 무조건
        ///      UTF-8 로 읽어서 한글이 통째로 깨졌습니다.
        ///   2. 16진수로 적힌 값 — mysqldump 는 글자가 아닌 바이트가 섞인
        ///      열을 0x4D7953 처럼 적습니다 (--hex-blob 이면 늘).
        ///
        /// 둘 다 <b>읽기는 성공하고 값만 틀립니다.</b> 그래서 시험이 없으면
        /// 화면을 눈으로 봐야만 압니다.
        /// </summary>
        private static void DbDumpBytes()
        {
            Say("DB — 덤프의 인코딩과 16진수 값");

            // ---- 1. CP949 덤프 ----
            string body =
                "CREATE TABLE `레시피` (\n" +
                "  `이름` varchar(64) NOT NULL\n" +
                ") ENGINE=InnoDB;\n" +
                "INSERT INTO `레시피` VALUES ('가열 온도'),('냉각 시간');\n";

            Encoding cp949;
            try { cp949 = Encoding.GetEncoding(949); }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
            {
                cp949 = null;
            }

            if (cp949 == null)
            {
                Console.WriteLine("  (CP949 가 없는 환경이라 이 시험은 건너뜁니다)");
            }
            else
            {
                string path = Path.Combine(_dir, "ansi.sql");
                File.WriteAllBytes(path, cp949.GetBytes(body));

                var snap = new DbSnapshot();
                SqlDumpReader.Read(path, snap, 100);

                DbTable t = snap.Find("레시피");
                Check("CP949 덤프의 표 이름이 안 깨짐", t != null,
                      t == null ? Names(snap) : "ok");
                if (t != null)
                {
                    Check("CP949 덤프의 열 이름이 안 깨짐",
                          t.Columns.Count == 1 && t.Columns[0].Name == "이름",
                          t.Columns.Count > 0 ? t.Columns[0].Name : "열이 없음");
                    Check("CP949 덤프의 값이 안 깨짐",
                          t.Rows.Count == 2 && t.Rows[0].Get(0) == "가열 온도",
                          t.Rows.Count > 0 ? t.Rows[0].Get(0) : "행이 없음");
                }
            }

            // ---- 2. 16진수로 적힌 값 ----
            //  mysqldump 가 같은 값을 한쪽은 따옴표로, 다른 쪽은 16진수로
            //  적어 두는 일이 있습니다. 그걸 차이로 세면 안 됩니다.
            string hexDump =
                "CREATE TABLE `t` (`a` varbinary(64), `b` varbinary(64), `c` varbinary(8));\n" +
                "INSERT INTO `t` VALUES (0xEAB080EC9798,X'EAB080EC9798',0x00010203);\n";
            string hp = WriteText("hex.sql", hexDump);
            var hs = new DbSnapshot();
            SqlDumpReader.Read(hp, hs, 100);

            DbTable ht = hs.Find("t");
            Check("16진수 표를 읽음", ht != null && ht.Rows.Count == 1,
                  ht == null ? Names(hs) : ht.Rows.Count.ToString(CultureInfo.InvariantCulture));
            if (ht != null && ht.Rows.Count == 1)
            {
                Check("0x… 가 글자로 돌아옴", ht.Rows[0].Get(0) == "가엘",
                      Show(ht.Rows[0].Get(0)));
                Check("X'…' 도 같은 글자", ht.Rows[0].Get(1) == ht.Rows[0].Get(0),
                      Show(ht.Rows[0].Get(1)));
                // 글자가 아닌 바이트는 글자로 바꾸는 척하지 않습니다.
                Check("진짜 바이너리는 0x… 로 남음", ht.Rows[0].Get(2) == "0x00010203",
                      Show(ht.Rows[0].Get(2)));
            }

            // _binary'…' 접두어. 따옴표 값 앞에 붙어 옵니다.
            string binDump =
                "CREATE TABLE `u` (`a` varbinary(64));\n" +
                "INSERT INTO `u` VALUES (_binary'abc');\n";
            var bs = new DbSnapshot();
            SqlDumpReader.Read(WriteText("bin.sql", binDump), bs, 100);
            DbTable bt = bs.Find("u");
            Check("_binary 접두어를 떼고 읽음",
                  bt != null && bt.Rows.Count == 1 && bt.Rows[0].Get(0) == "abc",
                  bt == null || bt.Rows.Count == 0 ? "행이 없음" : Show(bt.Rows[0].Get(0)));

            // 16진수가 아닌 글자가 섞이면 손대지 않습니다 (짐작 금지).
            string odd =
                "CREATE TABLE `v` (`a` varchar(64));\n" +
                "INSERT INTO `v` VALUES (0xZZ);\n";
            var os_ = new DbSnapshot();
            SqlDumpReader.Read(WriteText("odd.sql", odd), os_, 100);
            DbTable ot = os_.Find("v");
            Check("16진수가 아니면 적힌 그대로",
                  ot != null && ot.Rows.Count == 1 && ot.Rows[0].Get(0) == "0xZZ",
                  ot == null || ot.Rows.Count == 0 ? "행이 없음" : Show(ot.Rows[0].Get(0)));
        }

        private static string Names(DbSnapshot snap)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < snap.Tables.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(snap.Tables[i].Name);
            }
            return sb.Length == 0 ? "표가 없음" : sb.ToString();
        }

        private static string Show(string v)
        {
            return v == null ? "(NULL)" : v;
        }

        /// <summary>
        /// 줄 색(Mark)과 화면 위 세 숫자(Tally).
        ///
        /// 둘 다 <b>보는 사람이 수를 믿을 수 있어야</b> 하는 자리입니다.
        ///   · IO 를 셉니다 — 한 IO 가 세 열에서 달라져도 1 개입니다
        ///   · 값이 달라진 줄은 빨강
        ///   · 생기거나 없어진 줄은 칠하지 않고 따로 셉니다
        /// </summary>
        private static void DbTallyAndMarks()
        {
            Say("DB — 줄 색과 세 숫자");

            string before =
                // 기본 키가 있어야 행이 짝지어집니다. 없으면 모든 열로
                // 짝지어서 "달라짐" 이 "없어짐 + 생김" 으로 보입니다.
                "CREATE TABLE `t` (`id` int, `a` varchar(8), `b` varchar(8), `v1` varchar(8),\n" +
                "  PRIMARY KEY (`id`));\n" +
                "INSERT INTO `t` VALUES " +
                "(1,'x','y','10'),(2,'x','y','10'),(3,'사라짐','y','10');\n";
            string after =
                "CREATE TABLE `t` (`id` int, `a` varchar(8), `b` varchar(8), `v1` varchar(8),\n" +
                "  PRIMARY KEY (`id`));\n" +
                "INSERT INTO `t` VALUES " +
                "(1,'X','Y','10'),(2,'x','y','99'),(4,'새로','y','10');\n";

            var b = new DbSnapshot();
            var a2 = new DbSnapshot();
            SqlDumpReader.Read(WriteText("mark_b.sql", before), b, 100);
            SqlDumpReader.Read(WriteText("mark_a.sql", after), a2, 100);

            DbDiffResult d = DbDiff.Compare(b, a2);
            List<DbDiffLine> lines = DbDiffList.Build(d, null);

            // id=1 은 a · b 두 열이, id=2 는 v1 열이 달라졌습니다.
            int red = 0, plain = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Kind == DbDiffLine.KindNote) continue;
                if (lines[i].Mark == DbDiffLine.MarkValue) red++;
                else plain++;
            }
            Check("값이 달라진 줄은 빨강", red == 3,
                  red.ToString(CultureInfo.InvariantCulture));
            Check("생기거나 없어진 줄은 안 칠함", plain > 0,
                  plain.ToString(CultureInfo.InvariantCulture));

            // 세 숫자. id=1 은 두 줄이지만 IO 는 하나입니다.
            DbDiffList.Tally t = DbDiffList.Count(lines);
            Check("차이 발생은 IO 로 셈 (id=1 · id=2)", t.Changed == 2,
                  t.Changed.ToString(CultureInfo.InvariantCulture));
            Check("없어진 IO 하나 (id=3)", t.Gone == 1,
                  t.Gone.ToString(CultureInfo.InvariantCulture));
            Check("새로 생긴 IO 하나 (id=4)", t.Added == 1,
                  t.Added.ToString(CultureInfo.InvariantCulture));

            string head = DbDiffList.Headline(lines);
            Check("머리글에 세 숫자가 다 있음",
                  head.Contains("차이 발생 : 2개") && head.Contains("없어진 IO : 1개")
                  && head.Contains("새로 생긴 IO : 1개"), head);
            Check("표 수는 머리글에 없음", !head.Contains("표 "), head);

            // 걸러 보기: 칠한 줄만 남습니다
            List<DbDiffLine> only = DbDiffList.OnlyChanged(lines);
            Check("걸러 보면 칠한 줄만", only.Count == red,
                  only.Count.ToString(CultureInfo.InvariantCulture));
            bool allMarked = true;
            for (int i = 0; i < only.Count; i++) if (only[i].Mark.Length == 0) allMarked = false;
            Check("걸러 낸 줄에 안 칠한 것이 없음", allMarked, null);

            // 걸러 봐도 세 숫자는 그대로여야 합니다 (감춘 것이 없어진 것이 아닙니다).
            DbDiffList.Tally t2 = DbDiffList.Count(lines);
            Check("걸러 보기가 세 숫자를 바꾸지 않음",
                  t2.Gone == t.Gone && t2.Added == t.Added && t2.Changed == t.Changed, null);

            // 빈 목록에서 터지지 않아야 합니다.
            DbDiffList.Tally z = DbDiffList.Count(null);
            Check("빈 목록은 0 0 0", z.Changed == 0 && z.Gone == 0 && z.Added == 0, null);
        }

        /// <summary>
        /// <b>이름이 달라도 짝짓기.</b> `.csv` 는 파일 이름이 표 이름이라,
        /// 같은 자료를 다른 이름으로 뽑으면 이름으로만 짝지을 때
        /// "없어짐 + 생김" 이 되어 차이를 하나도 못 봅니다.
        ///
        /// 규칙은 하나입니다 — <b>열 구성이 똑같은 표가 양쪽에 하나씩</b>.
        /// 그래서 여기서는 "짝지어야 할 때" 와 <b>"안 지어야 할 때"</b> 를
        /// 함께 봅니다. 뒤쪽이 더 중요합니다: 상관없는 표 둘을 말없이
        /// 견주면 없는 차이를 만들어 냅니다.
        /// </summary>
        private static void DbPairByShape()
        {
            Say("DB — 이름이 달라도 짝짓기");

            // ---- 1. 이름만 다르고 열 구성이 같음 → 짝지어야 합니다 ----
            var b1 = new DbSnapshot();
            var a1 = new DbSnapshot();
            b1.Tables.Add(CsvLike("2026-03-14", "id,name,v1", "1,가열,10"));
            a1.Tables.Add(CsvLike("2026-03-21", "id,name,v1", "1,냉각,10"));

            DbDiffResult d1 = DbDiff.Compare(b1, a1);
            Check("이름이 달라도 짝지음", d1.TablesChanged == 1 && d1.TablesOnlyBefore == 0
                  && d1.TablesOnlyAfter == 0,
                  "달라짐 " + d1.TablesChanged + " · 이전만 " + d1.TablesOnlyBefore
                  + " · 이후만 " + d1.TablesOnlyAfter);
            Check("짝지었다고 적음", Joined(d1.Notes).Contains("이름이 다른 파일을 짝지었습니다"),
                  Joined(d1.Notes));
            Check("짝지은 이후 쪽 이름을 들고 있음",
                  d1.Tables.Count == 1 && d1.Tables[0].PairedName == "2026-03-21",
                  d1.Tables.Count == 0 ? "표가 없음" : d1.Tables[0].PairedName);
            Check("SQL 이 쓰는 이름은 이전 쪽 그대로",
                  d1.Tables[0].Name == "2026-03-14", d1.Tables[0].Name);

            // 값 차이가 실제로 보여야 합니다 (이게 안 되면 고친 뜻이 없습니다).
            List<DbDiffLine> lines = DbDiffList.Build(d1, null);
            bool sawValue = false;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Mark == DbDiffLine.MarkValue) sawValue = true;
            Check("짝지은 뒤 값 차이가 보임", sawValue, lines.Count + " 줄");

            // ---- 2. 이름도 열 구성도 다름 → 짝짓지 않아야 합니다 ----
            var b2 = new DbSnapshot();
            var a2 = new DbSnapshot();
            b2.Tables.Add(CsvLike("레시피", "id,name", "1,가열"));
            a2.Tables.Add(CsvLike("알람", "time,code,level", "0,E1,3"));

            DbDiffResult d2 = DbDiff.Compare(b2, a2);
            Check("열 구성이 다르면 짝짓지 않음",
                  d2.TablesOnlyBefore == 1 && d2.TablesOnlyAfter == 1 && d2.TablesChanged == 0,
                  "이전만 " + d2.TablesOnlyBefore + " · 이후만 " + d2.TablesOnlyAfter);
            Check("왜 못 지었는지 적음", Joined(d2.Notes).Contains("파일 이름을 맞춰"),
                  Joined(d2.Notes));

            // ---- 3. 같은 열 구성이 여럿 → 애매하므로 짝짓지 않아야 합니다 ----
            var b3 = new DbSnapshot();
            var a3 = new DbSnapshot();
            b3.Tables.Add(CsvLike("b1", "id,v", "1,1"));
            a3.Tables.Add(CsvLike("a1", "id,v", "1,2"));
            a3.Tables.Add(CsvLike("a2", "id,v", "1,3"));

            DbDiffResult d3 = DbDiff.Compare(b3, a3);
            Check("짝이 여럿이면 짝짓지 않음", d3.TablesChanged == 0,
                  "달라짐 " + d3.TablesChanged);
            Check("어느 것과 견줄지 모른다고 적음",
                  Joined(d3.Notes).Contains("어느 것과 견줄지"), Joined(d3.Notes));

            // ---- 4. 이름이 같으면 전처럼 조용히 짝지어야 합니다 ----
            var b4 = new DbSnapshot();
            var a4 = new DbSnapshot();
            b4.Tables.Add(CsvLike("recipe", "id,v", "1,1"));
            a4.Tables.Add(CsvLike("recipe", "id,v", "1,2"));

            DbDiffResult d4 = DbDiff.Compare(b4, a4);
            Check("이름이 같으면 그대로 짝지음", d4.TablesChanged == 1, d4.TablesChanged.ToString(CultureInfo.InvariantCulture));
            Check("이름이 같으면 짝지었다는 말이 없음",
                  !Joined(d4.Notes).Contains("이름이 다른 파일"), Joined(d4.Notes));
            Check("이름이 같으면 짝 이름이 빈 글자",
                  d4.Tables[0].PairedName.Length == 0, d4.Tables[0].PairedName);

            // ---- 5. 열 순서가 달라도 같은 구성으로 봅니다 ----
            var b5 = new DbSnapshot();
            var a5 = new DbSnapshot();
            b5.Tables.Add(CsvLike("x", "id,name,v1", "1,가열,10"));
            a5.Tables.Add(CsvLike("y", "v1,id,name", "10,1,가열"));

            DbDiffResult d5 = DbDiff.Compare(b5, a5);
            Check("열 순서가 달라도 같은 구성", d5.TablesChanged + d5.TablesSame == 1,
                  "달라짐 " + d5.TablesChanged + " · 같음 " + d5.TablesSame);
        }

        /// <summary>
        /// CSV 한 장처럼 생긴 표 하나. 첫 줄이 열 이름입니다.
        ///
        /// <b>첫 열을 열쇠로 둡니다.</b> 진짜 `.csv` 에는 열쇠가 없어서 값이
        /// 바뀐 행이 "없어진 행 + 생긴 행" 으로 보입니다. 이 시험이 보려는
        /// 것은 <b>표를 짝지었는지</b>라서, 그 문제가 끼어들지 않게 열쇠를
        /// 줬습니다.
        /// </summary>
        private static DbTable CsvLike(string name, string header, string row)
        {
            var t = new DbTable();
            t.Name = name;

            string[] cols = header.Split(',');
            for (int i = 0; i < cols.Length; i++)
            {
                var c = new DbColumn();
                c.Name = cols[i];
                c.Ordinal = i;
                c.IsKey = i == 0;
                t.Columns.Add(c);
            }

            string[] cells = row.Split(',');
            // HasRows 를 켜 주어야 행을 견줍니다. 끄면 "모양만 견줬습니다" 로
            // 넘어가서, 짝은 지었는데 "같음" 으로 세어집니다.
            t.HasRows = true;

            var r = new DbRow(cols.Length);
            for (int i = 0; i < cols.Length && i < cells.Length; i++) r.Values[i] = cells[i];
            t.Rows.Add(r);
            return t;
        }

        private static string Joined(List<string> notes)
        {
            return notes == null ? string.Empty : string.Join(" | ", notes.ToArray());
        }

        /// <summary>
        /// `.csv` 는 <b>3 번 열을 열쇠로, 4 번 열을 값으로</b> 읽습니다 (0.87).
        ///
        /// 쓰는 파일이 이렇게 생겼습니다.
        /// <code>
        ///   1,2,3,4,5,6         ← 첫 줄은 열 이름
        ///   …,…,IO 이름,값,…,…   ← 둘째 줄부터 자료
        /// </code>
        ///
        /// 전에는 열쇠가 없어서 값이 하나 바뀐 행이 "없어진 행 + 생긴 행"
        /// 으로 보였습니다. 그게 이 시험이 지키려는 것입니다.
        /// </summary>
        private static void CsvKeyAndValue()
        {
            Say("DB — .csv 의 3 번 열쇠 · 4 번 값");

            string head = "no,addr,io,value,unit,note\n";
            string before = head
                + "1,%M0,TEMP_MAX,180,C,설정\n"
                + "2,%M1,FLOW_SET,12.0,L,설정\n"
                + "3,%M2,GONE,1,-,없어질 것\n";
            string after = head
                + "1,%M0,TEMP_MAX,185,C,설정\n"          // 값이 달라짐
                + "2,%M1,FLOW_SET,12.0,L,바뀐 설명\n"     // 안 보는 열만 달라짐
                + "4,%M3,BORN,7,-,새로 생긴 것\n";

            DbTable tb = CsvTableReader.Read(WriteText("io_before.csv", before), 0);
            DbTable ta = CsvTableReader.Read(WriteText("io_after.csv", after), 0);

            Check("두 열만 읽음", tb.Columns.Count == 2,
                  tb.Columns.Count.ToString(CultureInfo.InvariantCulture));
            Check("첫 열은 3 번 (io)", tb.Columns[0].Name == "io", tb.Columns[0].Name);
            Check("둘째 열은 4 번 (value)", tb.Columns[1].Name == "value", tb.Columns[1].Name);
            Check("3 번이 열쇠", tb.Columns[0].IsKey && !tb.Columns[1].IsKey, null);
            Check("안 본 열을 적음", tb.Note.Contains("안 본 열"), tb.Note);
            Check("행 셋", tb.Rows.Count == 3, tb.Rows.Count.ToString(CultureInfo.InvariantCulture));
            Check("3 번 값이 들어옴", tb.Rows[0].Get(0) == "TEMP_MAX", tb.Rows[0].Get(0));
            Check("4 번 값이 들어옴", tb.Rows[0].Get(1) == "180", tb.Rows[0].Get(1));

            var b = new DbSnapshot(); b.Tables.Add(tb);
            var a2 = new DbSnapshot(); a2.Tables.Add(ta);
            DbDiffResult d = DbDiff.Compare(b, a2);
            List<DbDiffLine> lines = DbDiffList.Build(d, null);

            // 값이 바뀐 행은 "달라짐" 한 줄입니다 (없어짐 + 생김이 아닙니다).
            DbDiffLine changed = null;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Io == "TEMP_MAX" && lines[i].Change == DbDiffLine.ChangeDiff)
                    changed = lines[i];
            Check("값이 바뀐 행은 달라짐 한 줄", changed != null,
                  changed == null ? "못 찾음" : "ok");
            if (changed != null)
            {
                Check("IO 이름이 3 번 값", changed.Io == "TEMP_MAX", changed.Io);
                Check("이전 값", changed.Before == "180", changed.Before);
                Check("이후 값", changed.After == "185", changed.After);
                Check("빨간 줄", changed.Mark == DbDiffLine.MarkValue, changed.Mark);
            }

            // 안 보는 열(note)만 달라진 행은 차이가 아니어야 합니다.
            bool flowSaid = false;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Io == "FLOW_SET") flowSaid = true;
            Check("안 보는 열만 달라진 행은 차이 아님", !flowSaid, "FLOW_SET 이 목록에 있음");

            // 세 숫자.
            DbDiffList.Tally t = DbDiffList.Count(lines);
            Check("차이 발생 하나", t.Changed == 1, t.Changed.ToString(CultureInfo.InvariantCulture));
            Check("없어진 IO 하나", t.Gone == 1, t.Gone.ToString(CultureInfo.InvariantCulture));
            Check("새로 생긴 IO 하나", t.Added == 1, t.Added.ToString(CultureInfo.InvariantCulture));

            // 열이 모자란 파일: 짐작하지 않고 그렇다고 적습니다.
            DbTable thin = CsvTableReader.Read(WriteText("thin.csv", "a,b\n1,2\n"), 0);
            Check("열이 모자라면 통째로 읽음", thin.Columns.Count == 2, thin.Note);
            Check("열쇠를 두지 않음", !thin.Columns[0].IsKey && !thin.Columns[1].IsKey, null);
            Check("왜 그런지 적음", thin.Note.Contains("쓸 수 없습니다"), thin.Note);
        }

        /// <summary>
        /// <b>모든 IO 보기</b> — 두 쪽이 같은 IO 도 목록에 나오는지.
        ///
        /// 0.97 까지는 나올 수가 없었습니다. 견주는 쪽이 <b>차이만</b>
        /// 담았기 때문입니다 (같은 행은 아예 기록하지 않았습니다). 그래서
        /// 화면에서 무엇을 어떻게 걸러도 차이 난 IO 만 나왔습니다 — 걸러
        /// 보기 단추를 꺼도 똑같았습니다.
        ///
        /// 같은 IO 가 목록에 없으면 "이 IO 는 봤는데 괜찮았다" 를 확인할
        /// 길이 없습니다. 안 본 것과 보고 같았던 것이 똑같이 안 보입니다.
        /// </summary>
        private static void DbShowAllIos()
        {
            Say("DB — 모든 IO 보기");

            string head = "no,addr,io,value,unit\n";
            string before = head
                + "1,%M0,TEMP_MAX,180,C\n"
                + "2,%M1,FLOW_SET,12.0,L\n"
                + "3,%M2,PRES_SET,5,bar\n";
            string after = head
                + "1,%M0,TEMP_MAX,185,C\n"      // 달라짐
                + "2,%M1,FLOW_SET,12.0,L\n"     // 같음
                + "3,%M2,PRES_SET,5,bar\n";     // 같음

            DbTable tb = CsvTableReader.Read(WriteText("all_b.csv", before), 0);
            DbTable ta = CsvTableReader.Read(WriteText("all_a.csv", after), 0);
            var b = new DbSnapshot(); b.Tables.Add(tb);
            var a2 = new DbSnapshot(); a2.Tables.Add(ta);

            DbDiffResult d = DbDiff.Compare(b, a2);
            Check("같은 행을 세어 둠", d.Tables[0].RowsSame == 2,
                  d.Tables[0].RowsSame.ToString(CultureInfo.InvariantCulture));
            Check("같은 행을 담아 둠", d.Tables[0].SameRows.Count == 2,
                  d.Tables[0].SameRows.Count.ToString(CultureInfo.InvariantCulture));

            // ---- 평소(차이만) ----
            List<DbDiffLine> diffs = DbDiffList.Build(d, null);
            Check("차이만 보면 한 줄", CountRows(diffs) == 1,
                  CountRows(diffs).ToString(CultureInfo.InvariantCulture));

            // ---- 모든 IO ----
            var opt = new DbDiffList.Options();
            opt.IncludeSame = true;
            List<DbDiffLine> all = DbDiffList.Build(d, opt);

            Check("모든 IO 를 보면 세 줄", CountRows(all) == 3,
                  CountRows(all).ToString(CultureInfo.InvariantCulture));

            DbDiffLine same = FindLine(all, "FLOW_SET");
            Check("같은 IO 도 줄이 있음", same != null, null);
            if (same != null)
            {
                Check("같음이라고 적힘", same.Change == DbDiffLine.ChangeSame, same.Change);
                Check("칠하지 않음", same.Mark.Length == 0, same.Mark);
                Check("값이 적힘", same.Before == "12.0" && same.After == "12.0",
                      same.Before + " / " + same.After);
                Check("값 열 이름이 적힘", same.Column == "value", same.Column);
            }

            // 차이 난 줄은 그대로 있고, 색도 그대로입니다.
            DbDiffLine hot = FindLine(all, "TEMP_MAX");
            Check("차이 난 줄은 그대로", hot != null && hot.Mark == DbDiffLine.MarkValue,
                  hot == null ? "없음" : hot.Mark);

            // 같은 줄은 차이 <b>뒤에</b> 옵니다 (섞이면 차이를 눈으로 찾아야 합니다).
            Check("차이가 먼저", all.IndexOf(hot) < all.IndexOf(same),
                  all.IndexOf(hot) + " / " + all.IndexOf(same));

            // ---- 센 수는 보기와 무관해야 합니다 ----
            //
            // 같은 줄을 목록에 넣었다고 "차이 발생" 이 늘어나면, 보기를
            // 바꿀 때마다 판정(PASS/WARNING/FAIL)이 흔들립니다.
            DbDiffList.Tally t1 = DbDiffList.Count(diffs);
            DbDiffList.Tally t2 = DbDiffList.Count(all);
            Check("차이 발생 수가 같음", t1.Changed == t2.Changed && t1.Changed == 1,
                  t1.Changed + " / " + t2.Changed);
            Check("없어진 IO 수가 같음", t1.Gone == t2.Gone, t1.Gone + " / " + t2.Gone);

            // 걸러 보기를 켜면 같은 줄은 빠집니다 (값이 달라진 줄만 남습니다).
            List<DbDiffLine> onlyChanged = DbDiffList.OnlyChanged(all);
            Check("값이 달라진 줄만 걸러도 한 줄", onlyChanged.Count == 1,
                  onlyChanged.Count.ToString(CultureInfo.InvariantCulture));

            // 차이가 하나도 없는 표도 "모든 IO" 에서는 보여야 합니다.
            DbTable t3 = CsvTableReader.Read(WriteText("all_c.csv", before), 0);
            var c1 = new DbSnapshot(); c1.Tables.Add(t3);
            var c2 = new DbSnapshot(); c2.Tables.Add(CsvTableReader.Read(WriteText("all_d.csv", before), 0));
            DbDiffResult sameAll = DbDiff.Compare(c1, c2);

            Check("같은 표는 차이 목록이 빔", CountRows(DbDiffList.Build(sameAll, null)) == 0, null);
            Check("같은 표도 모든 IO 에서는 보임", CountRows(DbDiffList.Build(sameAll, opt)) == 3,
                  CountRows(DbDiffList.Build(sameAll, opt)).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>알림을 뺀 줄 수 (목록의 "줄" 은 알림도 한 줄입니다).</summary>
        private static int CountRows(List<DbDiffLine> lines)
        {
            int n = 0;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind != DbDiffLine.KindNote) n++;
            return n;
        }

        /// <summary>그 IO 의 첫 줄. 없으면 null.</summary>
        private static DbDiffLine FindLine(List<DbDiffLine> lines, string io)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Io == io && lines[i].Kind != DbDiffLine.KindNote) return lines[i];
            return null;
        }

        /// <summary>
        /// 히트맵을 단계로 자를 때 <b>칸마다 그 단계의 밀기 값으로</b>
        /// 견주는지.
        ///
        /// 이게 틀리면 "오차가 잘 안 잡힌다" 는 모양으로 나옵니다 — 칸은
        /// 단계로 잘라 놓고 밀기 값은 하나만 쓰면, 그 하나에 맞는 단계만
        /// 제대로 견주고 나머지 칸은 어긋난 채 견줍니다. 그러면 차이가
        /// 없는데 있다고 나오거나(여기서 보는 경우), 있는데 묻힙니다.
        ///
        /// 공정이 중간에 늘어진 로그가 늘 이 모양입니다 — 뒤 단계가 앞
        /// 단계보다 더 밀립니다.
        /// </summary>
        /// <summary>
        /// 단계 이름에 StepText 의 앞뒤 글을 붙인 꼴 (1.15). 시험이 이걸 거쳐
        /// 견주므로 StepText.cs 에서 글을 바꿔도 시험이 깨지지 않습니다.
        /// </summary>
        private static string Lb(string name)
        {
            return StepText.LabelBefore + name + StepText.LabelAfter;
        }

        /// <summary>
        /// 히트맵 줄을 그래프 화면과 같은 그룹으로 나누고 검색으로 거르기 (1.07).
        /// </summary>
        private static void HeatmapGroupsAndSearch()
        {
            Say("히트맵 — 그룹으로 나누기 · 검색");

            // 히트맵 차례 (많이 벌어진 것이 위): C, A, X, B
            var rows = new List<HeatRow>();
            foreach (string n in new[] { "TEMP_C", "TEMP_A", "X_LONE", "PRES_B" })
                rows.Add(new HeatRow { Name = n });

            var groups = new List<GroupDef>
            {
                new GroupDef("온도") { Members = new List<string> { "TEMP_A", "TEMP_C" } },
                new GroupDef("빈 그룹") { Members = new List<string> { "NOT_CHANGED" } },
                // PRES_B 가 둘에 들어 있으면 앞 그룹이 이깁니다 (그래프 목록과 같음).
                new GroupDef("압력") { Members = new List<string> { "PRES_B" } },
                new GroupDef("또 압력") { Members = new List<string> { "PRES_B" } },
            };

            List<HeatLine> all = HeatLayout.Build(rows, groups, "", null);
            string shape = Shape(all);
            Check("그룹 차례대로, 그룹에 없는 IO 는 맨 아래",
                  shape == "[온도:2] TEMP_C TEMP_A [압력:1] PRES_B [그룹에 없는 IO:1] X_LONE", shape);
            Check("그룹 안은 히트맵 차례 그대로 (C 가 A 보다 위)",
                  all[1].Row.Name == "TEMP_C", all[1].Row.Name);
            Check("남은 IO 가 없는 그룹은 이름 줄도 없음", !shape.Contains("빈 그룹"), shape);
            Check("IO 수는 그룹 줄 빼고", HeatLayout.IoCount(all) == 4,
                  HeatLayout.IoCount(all).ToString(CultureInfo.InvariantCulture));

            List<HeatLine> temp = HeatLayout.Build(rows, groups, "temp", null);
            Check("검색은 대소문자 안 가림 · 안 맞는 그룹은 사라짐",
                  Shape(temp) == "[온도:2] TEMP_C TEMP_A", Shape(temp));

            List<HeatLine> none = HeatLayout.Build(rows, groups, "없는이름", null);
            Check("아무것도 안 맞으면 빈 목록", none.Count == 0, null);

            var shut = new HashSet<string> { "온도" };
            List<HeatLine> folded = HeatLayout.Build(rows, groups, "", shut);
            Check("접은 그룹은 이름 줄만 (수는 그대로)",
                  Shape(folded) == "[온도:2>] [압력:1] PRES_B [그룹에 없는 IO:1] X_LONE", Shape(folded));
            Check("접어도 IO 수는 셈", HeatLayout.IoCount(folded) == 4, null);

            List<HeatLine> noGroups = HeatLayout.Build(rows, null, "", null);
            Check("그룹이 없으면 전부 \"그룹에 없는 IO\" 하나",
                  noGroups.Count == 5 && noGroups[0].Group == HeatLayout.Ungrouped, Shape(noGroups));
        }

        /// <summary>IO 이름 검색 규칙 한 곳 (1.17) — 그래프 · 히트맵 · 분석 2 가 같이 씀.</summary>
        private static void NameSearchRule()
        {
            Say("IO 검색 — 세 화면이 같은 규칙");
            Check("가운데 글자도 맞음", NameSearch.Matches("TEMP_MAX", "MP_M"), null);
            Check("대소문자 안 가림", NameSearch.Matches("TEMP_MAX", "temp"), null);
            Check("안 들었으면 아님", !NameSearch.Matches("TEMP_MAX", "PRES"), null);
            Check("빈 검색은 모두 맞음", NameSearch.Matches("X", "") && NameSearch.Matches("X", null), null);
            Check("앞뒤 빈칸 무시", NameSearch.Matches("TEMP_MAX", "  temp "), null);
            Check("빈칸만이면 거르지 않음", !NameSearch.IsActive("   "), null);
            Check("이름이 null 이면 아님 (검색 중)", !NameSearch.Matches(null, "a"), null);
        }

        private static string Shape(List<HeatLine> lines)
        {
            var parts = new List<string>();
            foreach (HeatLine l in lines)
                parts.Add(l.IsGroup ? "[" + l.Group + ":" + l.Count + (l.Collapsed ? ">" : "") + "]"
                                    : l.Row.Name);
            return string.Join(" ", parts.ToArray());
        }

        private static void HeatmapPerStepShift()
        {
            Say("히트맵 — 칸마다 그 단계의 밀기 값");

            // 이전: 0~59초. 단계 A(0~19) B(20~39) C(40~59)
            //       PV 는 단계마다 0 에서 다시 올라가는 톱니입니다.
            var a = new StringBuilder("Time,STEP,PV\n");
            for (int i = 0; i < 60; i++)
            {
                string st = i < 20 ? "A" : i < 40 ? "B" : "C";
                int baseAt = i < 20 ? 0 : i < 40 ? 20 : 40;
                a.Append(i).Append(',').Append(st).Append(',').Append(i - baseAt).Append('\n');
            }

            // 이후: 100초부터. 같은 단계지만 <b>단계마다 밀린 만큼이 다릅니다</b>.
            //       A 는 100 에서, B 는 125 에서, C 는 150 에서 시작 →
            //       밀기 값이 -100 / -105 / -110 입니다.
            var b = new StringBuilder("Time,STEP,PV\n");
            for (int i = 0; i < 70; i++)
            {
                int t = 100 + i;
                string st = (t >= 100 && t < 120) ? "A"
                          : (t >= 125 && t < 145) ? "B"
                          : (t >= 150 && t < 170) ? "C" : "";
                string val;
                if (st == "A") val = (t - 100).ToString(CultureInfo.InvariantCulture);
                else if (st == "B") val = (t - 125).ToString(CultureInfo.InvariantCulture);
                else if (st == "C") val = (t - 150).ToString(CultureInfo.InvariantCulture);
                else val = "0";
                b.Append(t).Append(',').Append(st).Append(',').Append(val).Append('\n');
            }

            LogDataset dsA = OpenWithSteps(WriteCsv("hps_a.csv", a.ToString()), "STEP");
            LogDataset dsB = OpenWithSteps(WriteCsv("hps_b.csv", b.ToString()), "STEP");

            StepPairList pairs = StepPairList.Build(dsA, dsB);
            Check("짝 셋", pairs.UsableCount == 3, pairs.UsableCount.ToString());
            Near("A 의 밀기 값", pairs[0].Shift, -100, 1e-9);
            Near("B 의 밀기 값", pairs[1].Shift, -105, 1e-9);
            Near("C 의 밀기 값", pairs[2].Shift, -110, 1e-9);

            // ---- 단계로 자르면: 칸마다 그 단계로 맞춰 견줍니다 ----
            //
            // PV 는 단계마다 같은 톱니라, 제대로 맞추면 차이가 0 입니다.
            var opt = new HeatmapOptions();
            opt.ByStep = true;
            opt.Steps = pairs;
            opt.Shift = pairs[0].Shift;          // 화면이 넘기는 값 (첫 단계 기준)
            opt.IncludeUnchanged = true;
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("단계로 잘림", r.ByStep, r.Warning);
            Check("칸 셋", r.BucketCount == 3, r.BucketCount.ToString());

            HeatRow pv = FindRow(r, "PV");
            Check("PV 줄이 있음", pv != null, null);
            if (pv == null) return;

            Check("A 칸에 표본이 있음", pv.Cells[0].Samples > 0, pv.Cells[0].Samples.ToString());
            Check("B 칸에 표본이 있음", pv.Cells[1].Samples > 0, pv.Cells[1].Samples.ToString());
            Check("C 칸에 표본이 있음", pv.Cells[2].Samples > 0, pv.Cells[2].Samples.ToString());

            Check("A 칸은 차이 없음", !HeatmapBuilder.IsOver(pv, 0), NumberText.Plain(pv.Cells[0].Max));
            Check("B 칸도 차이 없음", !HeatmapBuilder.IsOver(pv, 1), NumberText.Plain(pv.Cells[1].Max));
            Check("C 칸도 차이 없음", !HeatmapBuilder.IsOver(pv, 2), NumberText.Plain(pv.Cells[2].Max));
            Near("B 칸의 최대 차이가 0", pv.Cells[1].Max, 0, 1e-6);

            // ---- 밀기 값 하나로 보면: 뒤 단계가 어긋납니다 ----
            //
            // 같은 로그를 -100 하나로만 맞추면 B 는 5 초, C 는 10 초 어긋나서
            // 톱니가 그만큼 벌어집니다. 이게 0.95 까지 단계로 자른 칸에서
            // 벌어지던 일입니다.
            var one = new HeatmapOptions();
            one.Shift = -100;
            one.IncludeUnchanged = true;
            HeatmapResult r2 = HeatmapBuilder.Build(dsA, dsB, one, null);

            HeatRow pv2 = FindRow(r2, "PV");
            Check("한 밀기 값으로는 PV 가 차이 남", pv2 != null && pv2.HasDifference,
                  pv2 == null ? "줄 없음" : pv2.OverBuckets.ToString());
            if (pv2 != null)
                Check("벌어진 폭이 5 이상", pv2.PeakMax > 0, NumberText.Plain(pv2.PeakMax));
        }

        /// <summary>
        /// <b>단계별로 나눠 보기</b> — 가로를 단계 칸 여러 개로 쪼갤 때의
        /// 폭 나누기.
        ///
        /// 폭이 틀리면 그래프가 거짓말을 합니다. 똑같은 폭으로 나누면 짧은
        /// 단계가 늘어나고 긴 단계가 눌려서, <b>같은 기울기가 칸마다 다른
        /// 기울기로 보입니다.</b> 그래서 길이에 비례해야 하고, 그러면서도
        /// 짧은 단계가 선 한 줄로 찌그러지지 않아야 합니다.
        ///
        /// 폭의 합이 전체와 어긋나면 칸 사이에 틈이 생기는데, 그 자리는
        /// "기록 없음" 으로 보입니다.
        /// </summary>
        private static void StepBandWidths()
        {
            Say("단계 — 나눠 보기 칸 폭");

            // 이전: 준비(0~9) 가열(10~49) 유지(50~59)
            // 이후: 같은 이름, 시작만 100 늦음 → 칸마다 밀기 값 -100
            var a = new StringBuilder("Time,STEP\n");
            var b = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 60; i++)
            {
                string st = i < 10 ? "준비" : i < 50 ? "가열" : "유지";
                a.Append(i).Append(',').Append(st).Append('\n');
                b.Append(100 + i).Append(',').Append(st).Append('\n');
            }
            LogDataset dsA = OpenWithSteps(WriteCsv("band_a.csv", a.ToString()), "STEP");
            LogDataset dsB = OpenWithSteps(WriteCsv("band_b.csv", b.ToString()), "STEP");

            StepBands bands = StepBands.Build(StepPairList.Build(dsA, dsB));
            Check("칸 셋", bands.Count == 3, bands.Count.ToString());
            Check("칸 글자가 단계 이름", bands[1].Label == Lb("가열"), bands[1].Label);
            Near("가열 칸의 시작", bands[1].T0, 10, 1e-9);
            Near("가열 칸의 밀기 값", bands[1].Shift, -100, 1e-9);
            Check("뺀 단계 없음", bands.Skipped == 0, bands.Skipped.ToString());

            // 길이 9 / 39 / 9 (마지막 표본까지) → 폭도 그 비례
            double[] w = bands.Widths(1000, 0);
            Near("폭의 합은 전체", w[0] + w[1] + w[2], 1000, 1e-6);
            Check("긴 단계가 더 넓음", w[1] > w[0] && w[1] > w[2], null);
            Near("비례가 맞음", w[1] / w[0], bands[1].Length / bands[0].Length, 1e-6);

            // 최소 폭 — 짧은 칸이 선 한 줄이 되면 그 단계는 못 봅니다.
            double[] m = bands.Widths(1000, 200);
            Near("폭의 합은 그대로", m[0] + m[1] + m[2], 1000, 1e-6);
            Check("짧은 칸도 최소 폭", m[0] >= 199.9 && m[2] >= 199.9,
                  m[0].ToString("0.#", CultureInfo.InvariantCulture));
            Check("긴 칸이 그만큼 줄어듦", m[1] < w[1], null);
            Check("긴 칸이 여전히 제일 넓음", m[1] > m[0], null);

            // ---- 칸 사이 틈 ----
            //
            // 금 한 줄로만 가르면 두 칸의 선이 이어진 하나로 읽힙니다.
            // 자리를 내는 곳은 한 곳이어야 합니다 — 그리는 쪽·눈금 글자·
            // 마우스가 각자 셈하면, 눈금은 2 번 칸을 가리키는데 눌러 보면
            // 3 번 칸이 잡힙니다.
            double[] gx, gw;
            bands.Layout(100, 1000, 10, 0, out gx, out gw);
            Near("첫 칸은 왼쪽 그대로", gx[0], 100, 1e-9);
            Near("칸과 칸 사이가 틈만큼 떨어짐", gx[1] - (gx[0] + gw[0]), 10, 1e-6);
            Near("둘째 틈도 같음", gx[2] - (gx[1] + gw[1]), 10, 1e-6);
            Near("칸 폭 + 틈 = 전체", gw[0] + gw[1] + gw[2] + 20, 1000, 1e-6);
            Near("마지막 칸의 오른쪽 끝", gx[2] + gw[2], 1100, 1e-6);
            Check("틈이 있어도 비례는 그대로", gw[1] > gw[0] && gw[1] > gw[2], null);

            // 틈이 너비를 다 먹으면 틈을 줄입니다 — 칸이 하나도 안 보이는
            // 것보다 낫습니다.
            double[] tx, tw;
            bands.Layout(0, 30, 100, 0, out tx, out tw);
            Check("틈이 너무 크면 줄임", tw[0] > 0 && tw[1] > 0 && tw[2] > 0,
                  tw[0].ToString("0.##", CultureInfo.InvariantCulture));
            Check("그래도 전체를 안 넘음", tx[2] + tw[2] <= 30.001,
                  (tx[2] + tw[2]).ToString("0.##", CultureInfo.InvariantCulture));

            // 최소 폭조차 안 들어가면 똑같이 나눕니다 (비례를 지킬 자리가 없음).
            double[] tight = bands.Widths(300, 500);
            Near("똑같이 나눔", tight[0], 100, 1e-6);
            Near("합은 그대로", tight[0] + tight[1] + tight[2], 300, 1e-6);

            // 폭이 0 이면 0 을 돌려줍니다 (창을 아직 안 그린 상태).
            double[] none = bands.Widths(0, 10);
            Check("폭 0 이면 0", none.Length == 3 && none[0] == 0, null);

            // ---- 하나만 보기 ----
            //
            // 칸이 여섯이면 한 칸이 화면 폭의 육분의 일입니다. 하나를
            // 들여다볼 길이 있어야 합니다 — 그때도 <b>칸 모드 그대로</b>
            // 여야 뜻이 안 바뀝니다 (그 단계의 밀기 값, 단계 이름 눈금).
            StepBands one = StepBands.Build(StepPairList.Build(dsA, dsB), 1);
            Check("칸 하나만", one.Count == 1, one.Count.ToString(CultureInfo.InvariantCulture));
            Check("고른 그 단계", one[0].Label == Lb("가열"), one[0].Label);
            // 칸이 어느 단계 짝인지 들고 있어야 칸 머리를 눌러 그 단계로 갈 수
            // 있습니다 (1.16).
            Check("칸이 단계 짝 번호를 앎", bands[1].Pair == 1 && one[0].Pair == 1,
                  bands[1].Pair + " / " + one[0].Pair);
            // 두 로그의 단계 끝을 칸이 들고 있어야 짧은 쪽 선을 제 끝에서 끊습니다 (1.18).
            StepPair sp1 = StepPairList.Build(dsA, dsB)[1];
            Check("칸이 이전 단계 끝을 앎", bands[1].BeforeEnd == sp1.BeforeEnd,
                  bands[1].BeforeEnd + " / " + sp1.BeforeEnd);
            Check("칸이 이후 단계 끝을 이전 시간으로 앎", bands[1].AfterEnd == sp1.AfterEnd + sp1.Shift,
                  bands[1].AfterEnd + " / " + (sp1.AfterEnd + sp1.Shift));
            Check("칸 끝은 둘 중 긴 쪽", bands[1].T1 == Math.Max(bands[1].BeforeEnd, bands[1].AfterEnd),
                  bands[1].T1.ToString(CultureInfo.InvariantCulture));
            Near("밀기 값도 그 단계 것", one[0].Shift, bands[1].Shift, 1e-9);

            double[] ox, ow;
            one.Layout(0, 500, 10, 0, out ox, out ow);
            Near("하나면 폭을 다 씁니다", ow[0], 500, 1e-6);
            Near("틈도 없습니다", ox[0], 0, 1e-9);

            // -1 은 전부입니다 (평소).
            Check("-1 은 전부", StepBands.Build(StepPairList.Build(dsA, dsB), -1).Count == 3, null);

            // 없는 번호를 주면 빈 목록입니다 (짐작해서 아무 칸이나 고르지 않습니다).
            Check("없는 번호면 빈 목록",
                  StepBands.Build(StepPairList.Build(dsA, dsB), 9).IsEmpty, null);

            // 짝 없는 단계는 칸이 안 됩니다 — 이전 로그만 덜렁 그려지면
            // "이후가 없다" 로 읽힙니다. 몇 개를 뺐는지는 말해 줍니다.
            var c = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 30; i++)
                c.Append(i).Append(',').Append(i < 10 ? "준비" : i < 20 ? "가열" : "나만있음").Append('\n');
            LogDataset dsC = OpenWithSteps(WriteCsv("band_c.csv", c.ToString()), "STEP");

            StepBands part = StepBands.Build(StepPairList.Build(dsC, dsA));
            Check("짝 있는 둘만 칸", part.Count == 2, part.Count.ToString());
            Check("뺀 단계를 셈", part.Skipped >= 1, part.Skipped.ToString());

            Check("단계가 없으면 빈 칸 목록",
                  StepBands.Build(StepPairList.None).IsEmpty, null);
        }

        /// <summary>
        /// 히트맵의 가로를 시간 대신 <b>단계</b>로 놓습니다.
        ///
        /// 여기가 틀리는 모양은 늘 같습니다 — 칸이 한 칸 밀립니다. 그러면
        /// 가열에서 난 차이가 유지 칸에 적히는데, <b>그림은 멀쩡해 보입니다.</b>
        /// 그래서 어느 칸이 차이 났는지를 번호로 직접 셉니다.
        ///
        /// 단계 사이의 빈틈(공정이 멈춘 구간)도 봅니다. 그 구간의 차이를
        /// 옆 칸으로 밀어 넣으면, 그 단계에서 나지 않은 차이가 그 단계에
        /// 적힙니다.
        /// </summary>
        private static void HeatmapByStep()
        {
            Say("히트맵 — 가로를 단계로");

            // 0~9 준비 · 10~19 가열 · 20~29 (빈틈) · 30~39 유지
            // 차이는 가열 구간(12~15초)과 빈틈(22~25초)에서만 납니다.
            var a = new StringBuilder("Time,STEP,PV\n");
            var b = new StringBuilder("Time,STEP,PV\n");
            for (int i = 0; i < 40; i++)
            {
                string st = i < 10 ? "준비" : i < 20 ? "가열" : i < 30 ? "" : "유지";
                bool diff = (i >= 12 && i <= 15) || (i >= 22 && i <= 25);
                a.Append(i).Append(',').Append(st).Append(",100\n");
                b.Append(i).Append(',').Append(st).Append(',').Append(diff ? 150 : 100).Append('\n');
            }
            LogDataset dsA = OpenWithSteps(WriteCsv("hs_a.csv", a.ToString()), "STEP");
            LogDataset dsB = OpenWithSteps(WriteCsv("hs_b.csv", b.ToString()), "STEP");

            Check("단계 셋", dsA.Steps.Count == 3, dsA.Steps.Count.ToString());

            var opt = new HeatmapOptions();
            opt.ByStep = true;
            opt.Steps = StepPairList.Build(dsA, dsB);
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("단계로 잘림", r.ByStep, r.Warning);
            Check("칸 셋 (단계 수와 같음)", r.BucketCount == 3, r.BucketCount.ToString());
            Check("칸 글자가 단계 이름", r.BucketText(1) == Lb("가열"), r.BucketText(1));
            Check("가로축 이름도 단계", r.BucketLabel == "단계", r.BucketLabel);

            Check("차이 난 IO 는 PV 한 줄", r.Rows.Count == 1, r.Rows.Count.ToString());
            if (r.Rows.Count == 0) return;
            HeatRow row = r.Rows[0];

            Check("가열 칸만 차이", row.OverBuckets == 1, row.OverBuckets.ToString());
            Check("차이 난 칸은 가열(1)", HeatmapBuilder.IsOver(row, 1), null);
            Check("준비 칸은 정상", !HeatmapBuilder.IsOver(row, 0), null);
            Check("유지 칸은 정상", !HeatmapBuilder.IsOver(row, 2), null);

            // 빈틈의 차이(22~25초)는 어느 칸에도 안 들어갑니다. 유지 칸으로
            // 밀려 들어갔으면 위의 "유지 칸은 정상" 이 깨집니다.
            Check("빈틈의 표본은 어느 칸에도 안 셈",
                  row.Cells[0].Samples + row.Cells[1].Samples + row.Cells[2].Samples == 30,
                  (row.Cells[0].Samples + row.Cells[1].Samples + row.Cells[2].Samples).ToString());

            // 칸 하나의 폭은 그 단계의 폭입니다 (다 다릅니다).
            Near("가열 칸 시작", r.BucketStart(1), 10, 1e-9);
            Near("유지 칸 시작", r.BucketStart(2), 30, 1e-9);

            // 단계가 없는 로그에 단계로 보라고 하면, 시간으로 돌아가되
            // <b>그렇다고 말해 줍니다.</b> 조용히 돌아가면 가로가 단계인
            // 줄 알고 읽습니다.
            LogDataset pA = Open(WriteCsv("hs_c.csv", a.ToString()), Orientation.Auto);
            LogDataset pB = Open(WriteCsv("hs_d.csv", b.ToString()), Orientation.Auto);
            var opt2 = new HeatmapOptions();
            opt2.ByStep = true;
            opt2.Steps = StepPairList.Build(pA, pB);
            HeatmapResult r2 = HeatmapBuilder.Build(pA, pB, opt2, null);
            Check("단계가 없으면 시간으로", !r2.ByStep, null);
            Check("시간으로 돌아갔다고 말함", r2.Warning.Contains("단계"), r2.Warning);
            Check("그래도 칸은 있음", r2.BucketCount > 0, r2.BucketCount.ToString());

            // 시간으로 자른 칸도 같은 자리에서 셉니다 (칸 목록이 하나라서).
            var opt3 = new HeatmapOptions();
            HeatmapResult r3 = HeatmapBuilder.Build(dsA, dsB, opt3, null);
            Check("시간 칸도 칸 목록이 있음", r3.Buckets != null, null);
            Check("시간 칸은 빈틈이 없음", r3.Buckets.Find(r3.End - 1e-9) == r3.BucketCount - 1,
                  r3.Buckets.Find(r3.End - 1e-9).ToString());
            // 끝 시각의 표본이 어느 칸에도 안 들면 그 표본은 조용히
            // 빠집니다 (칸 폭으로 나눈 나머지 때문에 생기던 일입니다).
            Check("끝 시각의 표본도 칸에 듦", r3.Buckets.Find(r3.End) >= 0,
                  r3.Buckets.Find(r3.End).ToString());
            Check("끝 시각은 마지막 칸", r3.Buckets.Find(r3.End) == r3.BucketCount - 1,
                  r3.Buckets.Find(r3.End).ToString());
        }

        /// <summary>
        /// 로그 안의 <b>단계</b>를 IO 하나에서 뽑아냅니다.
        ///
        /// 여기가 틀리면 그 뒤가 전부 틀립니다 — 히트맵의 칸도, 그래프가
        /// 보여 주는 구간도 단계에서 나옵니다. 그런데 <b>그림만 봐서는 맞는지
        /// 알 수가 없습니다</b> (단계 경계가 한 칸 밀려도 그래프는 멀쩡해
        /// 보입니다). 그래서 경계를 표본 번호로 직접 셉니다.
        /// </summary>
        private static void StepsFromChannel()
        {
            Say("단계 — IO 에서 뽑기");

            // 0~9 준비 · 10~29 가열 · 30~39 유지 · 40~49 가열(두 번째)
            // 50~59 는 빈 칸 — 단계 밖입니다.
            var sb = new StringBuilder("Time,STEP,PV\n");
            for (int i = 0; i < 60; i++)
            {
                string name = i < 10 ? "준비" : i < 30 ? "가열" : i < 40 ? "유지" : i < 50 ? "가열" : "";
                sb.Append(i).Append(',').Append(name).Append(',').Append(i).Append('\n');
            }
            LogDataset ds = OpenWithSteps(WriteCsv("steps.csv", sb.ToString()), "STEP");

            Check("단계를 찾음", ds.HasSteps, ds.StepNote);
            if (!ds.HasSteps) return;

            StepTable t = ds.Steps;
            Check("단계 넷", t.Count == 4, t.Count.ToString());
            Check("첫 단계 이름", t[0].Name == "준비", t[0].Name);
            Check("첫 단계 시작 표본", t[0].First == 0, t[0].First.ToString());
            Check("첫 단계 끝 표본", t[0].Last == 9, t[0].Last.ToString());
            Check("둘째 단계 시작 표본", t[1].First == 10, t[1].First.ToString());
            Check("셋째 단계 끝 표본", t[2].Last == 39, t[2].Last.ToString());

            // 값이 빈 구간은 단계가 아닙니다. 직전 단계로 끌고 가면 그
            // 구간의 차이가 엉뚱한 단계에 적힙니다.
            Check("마지막 단계는 49 에서 끝", t[3].Last == 49, t[3].Last.ToString());
            Check("빈 구간은 어느 단계도 아님", t.IndexOfSample(55) < 0,
                  t.IndexOfSample(55).ToString());
            Check("단계 안의 표본은 그 단계", t.IndexOfSample(20) == 1,
                  t.IndexOfSample(20).ToString());

            // 시각은 표본 번호에서 그때그때 읽습니다 (가로축을 바꿔도 맞게).
            Near("둘째 단계 시작 시각", t.StartTime(ds, 1), 10, 1e-9);
            Near("둘째 단계 끝 시각", t.EndTime(ds, 1), 29, 1e-9);

            // 목록 글은 이름 그대로 (1.13, 전에는 "4. 가열"). 같은 이름이
            // 두 번째부터는 몇 번째인지 붙여 가립니다.
            Check("목록 글은 이름 그대로", t.Label(1) == Lb("가열"), t.Label(1));
            Check("같은 이름 둘째는 (2)", t.Label(3) == Lb("가열") + " (2)", t.Label(3));
            Check("순번 \"1. \" 이 안 붙음", !t.Label(0).StartsWith("1."), t.Label(0));
            Check("같은 이름의 첫째", t.IndexOfName("가열", 0) == 1,
                  t.IndexOfName("가열", 0).ToString());
            Check("같은 이름의 둘째", t.IndexOfName("가열", 1) == 3,
                  t.IndexOfName("가열", 1).ToString());

            // 단계는 표본 번호로 적혀 있고, 시각은 그 번호에서 그때그때
            // 읽습니다. 그래서 시간축을 손대는 어떤 기능이 들어와도
            // 단계 경계는 어긋나지 않습니다.
            Check("단계 경계는 표본 번호", t[1].First == 10 && t[1].Last == 29,
                  t[1].First + "~" + t[1].Last);

            // ---- 읽어 둔 로그에 나중에 단계를 넣기 ----
            //
            // 설정에 단계 IO 를 적는 일은 보통 로그를 연 <b>뒤</b>입니다
            // (이름을 한 번에 맞게 적는 일이 드물어서 — 대소문자·밑줄·공백).
            // 그때 파일을 다시 읽어야 하면 10M 칸을 다시 읽는 꼴입니다.
            // 이미 메모리에 있는 채널을 한 번 훑으면 되어야 합니다.
            LogDataset later = Open(WriteCsv("steps_later.csv", sb.ToString()), Orientation.Auto);
            Check("처음엔 단계가 없음", !later.HasSteps, null);

            var fill = new OpenOptions();
            fill.StepIo = "STEP";
            StepRead.Fill(later, fill);

            Check("다시 읽지 않고 단계가 들어옴", later.HasSteps, later.StepNote);
            Check("찾은 단계 수가 같음", later.Steps.Count == 4,
                  later.Steps.Count.ToString(CultureInfo.InvariantCulture));
            Check("경계도 같음", later.Steps[1].First == 10 && later.Steps[1].Last == 29,
                  later.Steps[1].First + "~" + later.Steps[1].Last);

            // 이름을 지우면 단계도 없어집니다 (적어 둔 것을 되돌릴 수 있어야 합니다).
            var off = new OpenOptions();
            StepRead.Fill(later, off);
            Check("이름을 비우면 단계도 없어짐", !later.HasSteps, null);
            Check("비웠으면 말도 없음", later.StepNote.Length == 0, later.StepNote);

            // 없는 IO 를 집으면 짐작하지 않고 이유를 돌려줍니다.
            LogDataset none = OpenWithSteps(WriteCsv("steps2.csv", sb.ToString()), "NO_SUCH_IO");
            Check("없는 IO 면 단계 없음", !none.HasSteps, null);
            Check("왜 없는지 적어 줌", none.StepNote.Contains("NO_SUCH_IO"), none.StepNote);

            // 단계 IO 를 안 적으면 찾지 않습니다 (말도 없습니다).
            LogDataset plain = Open(WriteCsv("steps3.csv", sb.ToString()), Orientation.Auto);
            Check("안 적으면 단계 없음", !plain.HasSteps, null);
            Check("안 적었으면 말도 없음", plain.StepNote.Length == 0, plain.StepNote);

            // ---- 구간을 직접 넣는 길 ----
            var mine = new List<LogStep>();
            mine.Add(new LogStep("나중", 30, 100));   // 로그보다 긴 구간
            mine.Add(new LogStep("먼저", 0, 20));     // 차례가 뒤바뀜
            mine.Add(new LogStep("겹침", 15, 25));    // 앞 단계와 겹침
            mine.Add(new LogStep("거꾸로", 40, 35));  // 끝이 시작보다 앞
            StepTable mt = StepTable.FromRanges(ds, mine, "시험");

            Check("차례대로 세움", mt.Count == 3 && mt[0].Name == "먼저", mt.Count.ToString());
            Check("로그 끝으로 잘림", mt[2].Last == ds.SampleCount - 1, mt[2].Last.ToString());
            Check("겹침은 앞 단계 다음으로 밀림", mt[1].First == 21, mt[1].First.ToString());
            Check("밀었다고 말해 줌", mt.Note.Contains("겹치는 단계"), mt.Note);
            Check("버렸다고 말해 줌", mt.Note.Contains("버렸습니다"), mt.Note);
        }

        /// <summary>
        /// 두 로그의 단계를 짝짓습니다. <b>틀린 짝으로 맞추면 두 로그가
        /// 나란히 그려져서 맞는 것처럼 보입니다</b> — 그래서 이름이 다르면
        /// 짝짓지 않습니다 (DB 에서 0.86 에 겪은 것과 같은 짐작입니다).
        /// </summary>
        private static void StepPairing()
        {
            Say("단계 — 두 로그 짝짓기");

            // 이전: 0 초 시작, 준비(0~9) 가열(10~29) 유지(30~49)
            // 이후: 100 초 시작, 준비(0~14) 가열(15~39) 냉각(40~49)
            //   가열의 시작은 이전 10 초, 이후 115 초 → 밀기 값 -105
            var a = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 50; i++)
                a.Append(i).Append(',').Append(i < 10 ? "준비" : i < 30 ? "가열" : "유지").Append('\n');

            var b = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 50; i++)
                b.Append(100 + i).Append(',').Append(i < 15 ? "준비" : i < 40 ? "가열" : "냉각").Append('\n');

            LogDataset dsA = OpenWithSteps(WriteCsv("pair_a.csv", a.ToString()), "STEP");
            LogDataset dsB = OpenWithSteps(WriteCsv("pair_b.csv", b.ToString()), "STEP");

            StepPairList pl = StepPairList.Build(dsA, dsB);
            Check("짝 넷 (셋 + 이후에만 있는 하나)", pl.Count == 4, pl.Count.ToString());
            Check("고를 수 있는 것은 둘", pl.UsableCount == 2, pl.UsableCount.ToString());

            StepPair heat = pl[1];
            Check("둘째는 가열", heat.Name == "가열", heat.Name);
            Check("양쪽에 다 있음", heat.HasBoth, null);
            Near("이전 가열 시작", heat.BeforeStart, 10, 1e-9);
            Near("이후 가열 시작", heat.AfterStart, 115, 1e-9);
            Near("밀기 값", heat.Shift, -105, 1e-9);
            Near("맞추면 시작이 겹침", heat.AfterStart + heat.Shift, heat.BeforeStart, 1e-9);

            // 길이가 19 와 24 로 1.26 배 차이입니다 — 기준(1.5 배) 아래라
            // 아무 말도 안 합니다. 조금 다른 것까지 말하면 늘 떠 있는 글이
            // 되어, 정말 잘릴 때도 눈에 안 들어옵니다.
            Check("조금 다른 길이는 말 없음", heat.Note.Length == 0, heat.Note);

            // 이전에만 있는 단계는 맞출 수 없고, 그렇다고 말해 줍니다.
            StepPair hold = pl[2];
            Check("셋째는 유지", hold.Name == "유지", hold.Name);
            Check("이전에만 있음", !hold.HasBoth && hold.HasBefore, null);
            Check("못 맞춘다고 적음", hold.Note.Contains("맞출 수 없습니다"), hold.Note);
            Check("밀기 값은 NaN", double.IsNaN(hold.Shift), null);

            // 이후에만 있는 단계도 목록에 남습니다 (조용히 빠지지 않게).
            StepPair cool = pl[3];
            Check("이후에만 있는 단계가 남음", cool.HasAfter && !cool.HasBefore, cool.Label);
            Check("이후만이라고 적힘", cool.Label.Contains("이후만"), cool.Label);
            Check("요약에 짝 없는 수가 적힘",
                  pl.Note.Contains("이전 로그에만") && pl.Note.Contains("이후 로그에만"), pl.Note);

            // 많이 다르면 알려 줍니다 — 짧은 쪽에 맞춰 보면 긴 쪽의
            // 뒷부분이 화면 밖으로 잘리는데, 모르면 "왜 뒤가 없지" 가 됩니다.
            var g = new StringBuilder("Time,STEP\n");
            var h = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 40; i++)
            {
                g.Append(i).Append(',').Append(i < 10 ? "가열" : "유지").Append('\n');
                h.Append(i).Append(',').Append(i < 30 ? "가열" : "유지").Append('\n');
            }
            LogDataset dsG = OpenWithSteps(WriteCsv("pair_g.csv", g.ToString()), "STEP");
            LogDataset dsH = OpenWithSteps(WriteCsv("pair_h.csv", h.ToString()), "STEP");

            StepPair longer = StepPairList.Build(dsG, dsH)[0];
            Check("많이 다른 길이는 알려 줌", longer.Note.Contains("길이"), longer.Note);
            Check("두 길이를 다 적어 줌",
                  longer.Note.Contains("9") && longer.Note.Contains("29"), longer.Note);

            // ---- 같은 이름이 두 번 나오는 공정 ----
            //
            // 가열 → 냉각 → 가열. n 번째끼리 맞춰야 합니다. 하나씩 남았다고
            // 아무거나 붙이면 두 번째 가열이 첫 번째와 맞춰집니다.
            var c = new StringBuilder("Time,STEP\n");
            var d = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 30; i++)
            {
                c.Append(i).Append(',').Append(i < 10 ? "가열" : i < 20 ? "냉각" : "가열").Append('\n');
                d.Append(i).Append(',').Append(i < 12 ? "가열" : i < 22 ? "냉각" : "가열").Append('\n');
            }
            LogDataset dsC = OpenWithSteps(WriteCsv("pair_c.csv", c.ToString()), "STEP");
            LogDataset dsD = OpenWithSteps(WriteCsv("pair_d.csv", d.ToString()), "STEP");

            StepPairList rep = StepPairList.Build(dsC, dsD);
            Check("짝 셋", rep.Count == 3, rep.Count.ToString());
            Check("첫 가열끼리", rep[0].BeforeIndex == 0 && rep[0].AfterIndex == 0, null);
            Check("둘째 가열은 둘째끼리", rep[2].BeforeIndex == 2 && rep[2].AfterIndex == 2,
                  rep[2].AfterIndex.ToString());
            Near("둘째 가열의 밀기 값", rep[2].Shift, 20 - 22, 1e-9);
            Check("짝이 다 맞으면 요약 없음", rep.Note.Length == 0, rep.Note);

            // 이름이 다른 단계는 하나씩 남아도 붙이지 않습니다.
            var e = new StringBuilder("Time,STEP\n");
            var f = new StringBuilder("Time,STEP\n");
            for (int i = 0; i < 20; i++)
            {
                e.Append(i).Append(',').Append("세척").Append('\n');
                f.Append(i).Append(',').Append("건조").Append('\n');
            }
            LogDataset dsE = OpenWithSteps(WriteCsv("pair_e.csv", e.ToString()), "STEP");
            LogDataset dsF = OpenWithSteps(WriteCsv("pair_f.csv", f.ToString()), "STEP");

            StepPairList no = StepPairList.Build(dsE, dsF);
            Check("이름이 다르면 안 붙임", no.UsableCount == 0, no.UsableCount.ToString());
            Check("둘 다 목록에는 남음", no.Count == 2, no.Count.ToString());

            // ---- 그래프가 보여 줄 구간 ----
            //
            // 긴 쪽까지 덮어야 합니다. 이전 로그의 단계에만 맞추면 이후
            // 로그의 뒷부분이 조용히 화면 밖으로 나가는데, 그래프만 봐서는
            // 잘린 줄 모릅니다.
            //
            // dsG 의 가열은 0~9, dsH 의 가열은 0~29 입니다. 맞추면 밀기
            // 값이 0 이라 덮어야 할 구간은 0~29 입니다.
            StepPair wide = StepPairList.Build(dsG, dsH)[0];
            double w0, w1;
            Check("구간이 나옴", StepWindow.Of(wide, out w0, out w1), null);
            Check("긴 쪽까지 덮음", w1 >= 29, w1.ToString("0.###", CultureInfo.InvariantCulture));
            Check("여유가 양쪽에 조금", w0 < 0 && w1 > 29, w0.ToString("0.###", CultureInfo.InvariantCulture));
            Near("여유는 구간의 2%", (w1 - w0) - 29, 29 * 2 * StepWindow.Pad, 1e-6);

            // 짝이 없는 단계는 구간이 없습니다 (맞출 수가 없으므로).
            double n0, n1;
            Check("짝 없는 단계는 구간 없음", !StepWindow.Of(hold, out n0, out n1), null);
            Check("null 도 구간 없음", !StepWindow.Of(null, out n0, out n1), null);

            // ---- 경과 시간 글자 ----
            //
            // 단계 시작을 0 으로 놓으면 가로축이 경과 시간이 됩니다.
            // 날짜 로그에서 이걸 그 로그의 모양으로 적으면 1970 년이
            // 찍히는데, 그게 날짜로 읽히면 로그를 잘못 읽은 줄 압니다.
            var clock = new LogDataset();
            clock.TimeKind = TimeKind.DateMs;
            Check("날짜 로그의 경과 시간은 시계로",
                  TimeText.Elapsed(clock, 5000) == "+00:00:05", TimeText.Elapsed(clock, 5000));
            Check("0 자리 앞은 음수로",
                  TimeText.Elapsed(clock, -1500).StartsWith("-"), TimeText.Elapsed(clock, -1500));

            var idx = new LogDataset();
            idx.TimeKind = TimeKind.Index;
            Check("표본 번호 로그는 표본으로",
                  TimeText.Elapsed(idx, 12) == "+12표본", TimeText.Elapsed(idx, 12));

            // 단계가 없는 로그끼리는 빈 목록입니다.
            LogDataset bare = Open(WriteCsv("pair_bare.csv", a.ToString()), Orientation.Auto);
            Check("단계 없으면 빈 목록", StepPairList.Build(bare, bare).IsEmpty, null);
        }

        /// <summary>
        /// 견줄 자리를 칸에 적을 때 쓰는 이름 조각.
        ///
        /// <b>Windows 경로를 리눅스에서도 같게 끊어야 합니다.</b> 여기 시험이
        /// 도는 자리에서는 <c>\</c> 가 구분 기호가 아니라서,
        /// <c>Path.GetFileName</c> 을 쓰면 <c>C:\db\io</c> 가 통째로 "이름" 이
        /// 됩니다 — 그러면 칸에 전체 경로가 박혀 들어가고, 그건 Windows 에서
        /// 열어 봐야만 드러납니다.
        /// </summary>
        /// <summary>
        /// 분석 3 게시판 (1.29). 메인 칸과 갈아 끼운 분석 3 화면이 이것 하나로만
        /// 주고받으므로, 알림이 <b>한 번만 · 바뀔 때만</b> 울리는지가 중요합니다 —
        /// 두 번 울리면 분석 3 이 두 번 돌고, 켤 때 울리면 켜자마자 돕니다.
        /// </summary>
        /// <summary>
        /// History (1.42): 저장 한 번에 칸 셋의 요약을 찍고, 파일로 갔다 와도 그대로이며,
        /// 다시 칸에 펼치면 같은 글이 보이는지. 1.41 까지의 기록(요약 없음)도 칸 셋을 만듦.
        /// </summary>
        private static void HistorySnapshots()
        {
            Say("History — 칸 셋 요약 찍기 · 펼치기");

            // 분석 2 칸: 낱말 + 개수 + 요약 줄
            var c2 = new AnalysisCardVm(2, "DB 비교", "", true);
            c2.SetVerdict("FAIL", ScoreBands.GradeBad, "", "2");
            var lines = new List<CardLineVm>();
            var l1 = new CardLineVm("TEMP_MAX", "1 → 2", ScoreBands.GradeBad, "", "TEMP_MAX");
            l1.ValueIsChange = true;
            lines.Add(l1);
            lines.Add(new CardLineVm("PRES_SET", "5 → 6", ScoreBands.GradeBad, "열 2 개 더"));
            c2.SetLines(lines, "");

            // 분석 1 칸: 점수 + 그룹 줄
            var c1 = new AnalysisCardVm(1, "로그 비교", "", true);
            c1.SetScore(87);
            c1.SetCount("3");
            var g = new GroupSummaryVm("밸브", new List<string> { "V1", "V2", "V3" }, 3, 1);
            g.SetDetail("V2 늦게 열림");
            c1.SetGroups(new List<GroupSummaryVm> { g });

            // 분석 3 칸: 비어 있음
            var c3 = new AnalysisCardVm(3, "분석 3", "", true);

            var rec = new AnalysisRecord();
            rec.SavedAt = new DateTime(2026, 10, 29, 14, 2, 0);
            rec.Label = "기동 전";
            foreach (AnalysisCardVm c in new[] { c1, c2, c3 })
            {
                CardSnapshot s = c.TakeSnapshot();
                if (c.Number == 2) { s.BeforeName = "a.sql"; s.AfterName = "b.sql"; }
                rec.Cards.Add(s);
            }

            AnalysisRecord back = AnalysisRecord.FromJson(Json.Parse(Json.Write(rec.ToJson())));
            Check("칸 셋이 다 돌아옴", back.Cards.Count == 3 && back.CardAt(1) != null && back.CardAt(3) != null,
                  back.Cards.Count.ToString());

            CardSnapshot s2 = back.CardAt(2);
            Check("분석 2 낱말 · 개수", s2.Word == "FAIL" && s2.Count == "2" && s2.WordGrade == ScoreBands.GradeBad,
                  s2.Word + "|" + s2.Count);
            Check("분석 2 줄 둘 · 값 · 빨강 표시", s2.Lines.Count == 2 && s2.Lines[0].Value == "1 → 2"
                  && s2.Lines[0].ValueIsChange && s2.Lines[0].Target == "TEMP_MAX", s2.Lines.Count.ToString());
            Check("분석 2 한 칸 글", s2.HeadText == "FAIL" && s2.CountText == "차이 발생 2개"
                  && s2.SummaryText == "TEMP_MAX  1 → 2  외 1줄" && s2.InputsText == "a.sql  →  b.sql",
                  s2.HeadText + "|" + s2.SummaryText + "|" + s2.InputsText);

            CardSnapshot s1 = back.CardAt(1);
            Check("분석 1 점수 · 그룹 · IO 이름", s1.HasScore && s1.Score == 87 && s1.Groups.Count == 1
                  && s1.Groups[0].Members.Count == 3 && s1.Groups[0].Detail == "V2 늦게 열림",
                  s1.Groups.Count.ToString());
            Check("분석 1 한 칸 글", s1.HeadText == "87" && s1.HeadGrade == ScoreBands.GradeOf(87)
                  && s1.SummaryText == "그룹 1개 중 1개 달라짐", s1.HeadText + "|" + s1.SummaryText);

            // 다시 펼치기
            var shown = new AnalysisCardVm(2, "DB 비교", "", true);
            shown.ApplySnapshot(s2);
            Check("펼치면 낱말 · 개수 · 줄 그대로", shown.ScoreText == "FAIL" && shown.VerdictCount == "2"
                  && shown.Lines.Count == 2 && shown.Lines[1].Detail == "열 2 개 더" && shown.Grade == ScoreBands.GradeBad,
                  shown.ScoreText + "|" + shown.Lines.Count);
            var shown1 = new AnalysisCardVm(1, "로그 비교", "", true);
            shown1.ApplySnapshot(s1);
            Check("펼치면 점수 · 그룹 그대로", shown1.ScoreText == "87" && shown1.Groups.Count == 1
                  && shown1.Groups[0].CountText == g.CountText, shown1.ScoreText);

            // 줄 = 칸 셋 (가로)
            var row = new HistoryRowVm(back);
            Check("줄 하나에 칸 셋", row.Cells.Count == 3 && row.Cells[1].HeadText == "FAIL", row.Cells.Count.ToString());
            Check("이름이 있으면 옆에 시각", row.SavedAtText == "2026-10-29 14:02:00", row.SavedAtText);

            // 1.41 까지의 기록: 요약 없음 → 점수로 칸 셋
            var old = new AnalysisRecord();
            old.SetScore(1, true, 100);
            old.BeforeName = "x.xlsx"; old.AfterName = "y.xlsx";
            var oldRow = new HistoryRowVm(AnalysisRecord.FromJson(Json.Parse(Json.Write(old.ToJson()))));
            Check("옛 기록도 칸 셋", oldRow.Cells.Count == 3 && oldRow.Cells[0].HeadText == "100"
                  && oldRow.Cells[1].HeadText == "-" && oldRow.Cells[0].InputsText == "x.xlsx  →  y.xlsx",
                  oldRow.Cells[0].HeadText + "|" + oldRow.Cells[1].HeadText);
            Check("옛 기록 요약 자리 글", oldRow.Cells[2].SummaryText == "요약 없음 (1.42 전에 저장)", oldRow.Cells[2].SummaryText);

            // 파일로 저장 → 읽기
            string folder = Path.Combine(_dir, "history-snap");
            string problem;
            HistoryStore.Save(folder, rec, out problem);
            List<AnalysisRecord> loaded = HistoryStore.Load(folder);
            Check("파일로 갔다 와도 칸 셋", problem.Length == 0 && loaded.Count == 1 && loaded[0].Cards.Count == 3
                  && loaded[0].CardAt(2).Lines.Count == 2, problem);
        }

        /// <summary>
        /// 종합 분석 결과 (LLM) 칸 (1.45): 체크 표시 · 버튼 켜짐 · 진행 · 결과 · 실패 · 취소.
        /// </summary>
        private static void LlmPanelRules()
        {
            Say("종합 분석 결과 (LLM) 칸");

            var p = new LlmPanelVm();
            Check("처음엔 버튼 꺼짐 · 안내 보임", !p.CanRun && p.ShowEmpty && p.Checks.Count == 3, "");
            p.SetReady(2, "DB 비교", true, "DB 를 견줌");
            Check("하나만 완료여도 버튼 켜짐", p.CanRun && p.Checks[1].Ready && p.Checks[1].StateText == "완료"
                  && p.Checks[0].StateText == "안 함" && p.Checks[1].Title == "분석 2 · DB 비교", p.Checks[1].Title);

            p.Start();
            Check("도는 중엔 버튼 꺼짐 · 진행 모름", p.IsRunning && !p.CanRun && p.ProgressUnknown && !p.ShowEmpty, "");
            p.Report("서버에 보내는 중", 0.4);
            Check("진행 40 · 글", !p.ProgressUnknown && p.Progress == 40 && p.Message == "서버에 보내는 중", p.Progress.ToString());
            p.Report(null, 1.7);
            Check("1 을 넘으면 100 에 붙임 · 글은 그대로", p.Progress == 100 && p.Message == "서버에 보내는 중", "");

            p.Finish("종합 판정: WARNING", LlmPanelVm.UsedText(false, true, false), new DateTime(2026, 10, 29, 15, 2, 10));
            Check("결과 받음", !p.IsRunning && p.CanRun && p.HasResult && p.ResultText == "종합 판정: WARNING"
                  && p.FinishedText == "2026-10-29 15:02:10 · 분석 2 결과로 분석", p.FinishedText);

            p.Start();
            p.Fail("TimeoutException: 응답 없음");
            Check("실패해도 앞 결과는 남고 오류만", p.HasResult && p.HasError && p.ErrorText.Contains("응답 없음") && !p.IsRunning, p.ErrorText);
            p.Start();
            Check("다시 돌리면 오류는 지움", !p.HasError, "");
            p.Cancelled();
            Check("취소", !p.IsRunning && p.ErrorText == "취소했습니다." && p.HasResult, "");

            Check("쓴 분석 글", LlmPanelVm.UsedText(true, false, true) == "분석 1 · 3"
                  && LlmPanelVm.UsedText(false, false, false) == "분석 없음", LlmPanelVm.UsedText(true, false, true));

            var q = new LlmRequest();
            Check("요청: 아무것도 없으면 Any 거짓", !q.Any, "");
            q.Use3 = true;
            Check("요청: 분석 3 만 있어도 Any", q.Any, "");
        }

        /// <summary>
        /// IO 표가 몇만 행일 때 (1.49). 1.48 까지는 표마다 행 차이 5 천 · 목록 5 천 줄 · 같은 줄 2 만 에서
        /// 끊겨 "차이 발생 N 개" 가 적게 나왔습니다. 3 만 행 · 1 만 2 천 개 바뀜으로 다 세는지 봅니다.
        /// </summary>
        private static void DbManyRows()
        {
            Say("DB — 몇만 행");
            const int n = 30000;
            var b = new StringBuilder();
            var a = new StringBuilder();
            string head = "CREATE TABLE `io` (`name` varchar(64) NOT NULL, `v` int, PRIMARY KEY (`name`));\n";
            b.Append(head); a.Append(head);
            for (int i = 0; i < n; i += 1000)
            {
                b.Append("INSERT INTO `io` VALUES "); a.Append("INSERT INTO `io` VALUES ");
                for (int k = i; k < i + 1000; k++)
                {
                    string sep = k > i ? "," : "";
                    b.Append(sep).Append("('IO_").Append(k).Append("',").Append(k).Append(')');
                    a.Append(sep).Append("('IO_").Append(k).Append("',").Append(k % 5 < 2 ? k + 1 : k).Append(')');   // 40 % 바뀜
                }
                b.Append(";\n"); a.Append(";\n");
            }
            var sb = new DbSnapshot(); SqlDumpReader.Read(WriteText("many_b.sql", b.ToString()), sb, 0);
            var sa = new DbSnapshot(); SqlDumpReader.Read(WriteText("many_a.sql", a.ToString()), sa, 0);
            DbDiffResult d = DbDiff.Compare(sb, sa);

            List<DbDiffLine> lines = DbDiffList.Build(d, null);
            DbDiffList.Tally t = DbDiffList.Count(lines);
            Check("3 만 행 다 읽음", sb.RowCount == n && sa.RowCount == n, sb.RowCount + "/" + sa.RowCount);
            Check("바뀐 1 만 2 천 개를 다 셈 (전엔 5 천에서 끊김)", t.Changed == 12000, t.Changed.ToString());
            Check("표의 바뀐 행 수와 같음", d.Tables[0].RowsChanged == 12000 && d.Tables[0].Rows.Count == 12000,
                  d.Tables[0].RowsChanged + "/" + d.Tables[0].Rows.Count);
            Check("상한 알림 없음", d.Tables[0].RowNote.IndexOf("상한", StringComparison.Ordinal) < 0, d.Tables[0].RowNote);

            var o = new DbDiffList.Options();
            o.IncludeSame = true;
            List<DbDiffLine> all = DbDiffList.Build(d, o);
            Check("모든 IO 보기: 같은 1 만 8 천 줄까지 (전엔 2 만 상한 근처)", d.Tables[0].SameRows.Count == 18000
                  && all.Count >= n, d.Tables[0].SameRows.Count + " / " + all.Count);
        }

        private static void Analysis3LinkRules()
        {
            Say("분석 3 — 게시판");

            int paths = 0, results = 0;
            string opened = null;
            EventHandler onPaths = (s, e) => paths++;
            EventHandler onResult = (s, e) => results++;
            EventHandler<Analysis3OpenEventArgs> onOpen = (s, e) => opened = e.Target;
            Analysis3Link.PathsChanged += onPaths;
            Analysis3Link.ResultChanged += onResult;
            Analysis3Link.OpenRequested += onOpen;
            try
            {
                Analysis3Link.Restore(@"C:\a\before.csv", null);
                Check("되살리기는 알리지 않음", paths == 0, "울림 " + paths);
                Check("되살린 자리", Analysis3Link.BeforePath == @"C:\a\before.csv" && Analysis3Link.AfterPath == "",
                      Analysis3Link.BeforePath + " | " + Analysis3Link.AfterPath);
                Check("한쪽만이면 HasBoth 아님", !Analysis3Link.HasBoth, "");

                Analysis3Link.SetPath(false, @" C:\a\after.csv ");
                Check("바꾸면 한 번 울림", paths == 1, "울림 " + paths);
                Check("앞뒤 빈칸은 뗌", Analysis3Link.AfterPath == @"C:\a\after.csv", Analysis3Link.AfterPath);
                Check("둘 다 차면 HasBoth", Analysis3Link.HasBoth, "");

                Analysis3Link.SetPath(false, @"C:\a\after.csv");
                Check("같은 값이면 안 울림", paths == 1, "울림 " + paths);

                Analysis3Link.SetPaths("x", "y");
                Check("둘을 한꺼번에 바꿔도 한 번", paths == 2, "울림 " + paths);

                var r = new Analysis3Result();
                r.Verdict = "FAIL";
                Analysis3Link.Publish(r);
                Check("결과를 올리면 울림", results == 1 && Analysis3Link.Result == r, "울림 " + results);
                Analysis3Link.ClearResult();
                Check("지우면 울리고 비움", results == 2 && Analysis3Link.Result == null, "울림 " + results);

                Analysis3Link.RequestOpen("항목 A");
                Check("줄 누름이 실려 옴", opened == "항목 A", opened ?? "null");

                // [분석] (1.35): 아무도 안 들으면 false — 창이 "연결된 기능 없음" 을 띄움.
                Check("[분석] 연결 없으면 false", !Analysis3Link.RequestAnalyze(), "");
                int analyze = 0, report = 0;
                EventHandler onAnalyze = (s, e) => analyze++;
                EventHandler onReport = (s, e) => report++;
                Analysis3Link.AnalyzeRequested += onAnalyze;
                Analysis3Link.ReportRequested += onReport;   // 1.39 에 뺀 단추 — 연결은 되지만 안 울림
                bool a1 = Analysis3Link.RequestAnalyze();
                Check("[분석] 은 분석만 한 번", a1 && analyze == 1 && report == 0, analyze + "/" + report);
                Analysis3Link.AnalyzeRequested -= onAnalyze;
                Analysis3Link.ReportRequested -= onReport;
            }
            finally
            {
                Analysis3Link.PathsChanged -= onPaths;
                Analysis3Link.ResultChanged -= onResult;
                Analysis3Link.OpenRequested -= onOpen;
                Analysis3Link.Restore(string.Empty, string.Empty);
                Analysis3Link.ClearResult();
            }
        }

        private sealed class A3WithState
        {
            public AppState Got;
            public int Calls;
            public void Attach(AppState state) { Got = state; Calls++; }
            public void Attach() { Calls += 100; }   // 둘 다 있으면 AppState 쪽이 먼저
        }

        private sealed class A3NoArgs
        {
            public int Calls;
            public void Attach() { Calls++; }
        }

        private sealed class A3None { }

        private sealed class A3Throws
        {
            public void Attach(AppState state) { throw new InvalidOperationException("설정 없음"); }
        }

        /// <summary>
        /// 분석 3 화면의 Attach 를 이름으로 찾아 부르기 (1.33). 사용자가 ShellWindow 를 직접
        /// 고쳐 붙였던 것을 창 쪽으로 옮긴 것 — 받는 꼴 둘과, 없을 때 · 오류 날 때.
        /// </summary>
        private static void Analysis3AttachRules()
        {
            Say("분석 3 — Attach 부르기");

            var state = new AppState();
            var a = new A3WithState();
            Check("Attach(AppState) 를 부름", LogScope.Shell.ShellWindow.CallAttach(a, state) && a.Got == state && a.Calls == 1,
                  "부름 " + a.Calls);

            var b = new A3NoArgs();
            Check("Attach() 도 부름", LogScope.Shell.ShellWindow.CallAttach(b, state) && b.Calls == 1, "부름 " + b.Calls);

            Check("없으면 false", !LogScope.Shell.ShellWindow.CallAttach(new A3None(), state), "");
            Check("null 이면 false", !LogScope.Shell.ShellWindow.CallAttach(null, state), "");

            bool threw = false;
            string why = string.Empty;
            try { LogScope.Shell.ShellWindow.CallAttach(new A3Throws(), state); }
            catch (System.Reflection.TargetInvocationException ex)
            {
                threw = true;
                why = ex.InnerException != null ? ex.InnerException.Message : string.Empty;
            }
            Check("Attach 오류는 속에 싸여 올라옴", threw && why == "설정 없음", why);
        }

        /// <summary>
        /// 분석 3 칸 (1.29). 결과가 오기 전에는 "?? / 100" 을 띄우지 않고, 낱말이 오면
        /// 분석 2 처럼, 점수가 오면 분석 1 처럼 적습니다.
        /// </summary>
        private static void Analysis3CardRules()
        {
            Say("분석 3 — 메인 칸");

            var card = new AnalysisCardVm(3, "분석 3", "", true);
            Check("이전/이후 칸이 있음", card.HasA3Box && !card.HasDbBox && !card.HasLogBox, "");
            Check("분석 3 요약 줄은 높이 한도 없음 (1.41)", double.IsPositiveInfinity(card.LinesMaxHeight), card.LinesMaxHeight.ToString());
            Check("분석 2 요약 줄은 230 에서 굴림", new AnalysisCardVm(2, "", "", true).LinesMaxHeight == 230.0, "");
            Check("결과 전에는 크게 적는 것 없음", card.ScoreText == "" && card.ScoreOutOf == "",
                  card.ScoreText + "|" + card.ScoreOutOf);

            card.SetScore(87);
            Check("점수가 오면 점수 칸", card.ScoreText == "87" && card.ScoreOutOf == "/ 100",
                  card.ScoreText + "|" + card.ScoreOutOf);
            card.ClearScore();
            Check("점수를 지우면 다시 빈 칸", card.ScoreText == "", card.ScoreText);

            card.SetVerdict("FAIL", MainVm.A3Grade(Analysis3Grade.None, "FAIL"), "", "3");
            Check("낱말이 오면 낱말", card.ScoreText == "FAIL" && card.Grade == ScoreBands.GradeBad
                  && card.VerdictCount == "3", card.ScoreText + "|" + card.Grade);

            Check("PASS 는 초록", MainVm.A3Grade(Analysis3Grade.None, "pass") == ScoreBands.GradeGood, "");
            Check("WARNING 은 노랑", MainVm.A3Grade(Analysis3Grade.None, "WARNING") == ScoreBands.GradeWarn, "");
            Check("모르는 낱말은 회색", MainVm.A3Grade(Analysis3Grade.None, "검토") == ScoreBands.GradeNone, "");
            Check("정해 온 색이 이김", MainVm.A3Grade(Analysis3Grade.Good, "FAIL") == ScoreBands.GradeGood, "");

            var empty = new AnalysisCardVm(3, "분석 3", "", true);
            empty.SetLines(new List<CardLineVm>(), string.Empty);
            Check("분석 3 요약은 글이 없으면 빈 칸 (그룹 이야기 안 함)", empty.GroupNote == "", empty.GroupNote);
            Check("분석 3 결과를 메인 칸에 적음 (1.37 에 다시 켬)", MainVm.A3ResultOnCard, "");
            Check("분석 3 칸 머리 줄(낱말 · 개수 · Note)은 안 적음 (1.38)", !MainVm.A3HeadlineOnCard, "");

            var db = new AnalysisCardVm(2, "분석 2", "", true);
            Check("분석 2 는 늘 판정 칸", db.UsesVerdict, "");
            var line = new CardLineVm("a", "b", ScoreBands.GradeNone, "");
            Check("줄은 기본이 분석 2 칸", line.Card == 2, line.Card.ToString());
            Check("분석 2 줄은 자름 (줄 안 바꿈)", !line.Wrap, "");
            line.Card = 3;
            Check("분석 3 줄은 줄을 바꿈", line.Wrap, "");

            // 꼬리표 (1.40): "[오류] …" 의 [오류] 만 점 색으로.
            var tagged = new CardLineVm("[오류] 압력 센서 응답 없음", "", ScoreBands.GradeBad, "");
            Check("꼬리표 뗌", tagged.LabelTag == "[오류]" && tagged.LabelRest == " 압력 센서 응답 없음",
                  tagged.LabelTag + "|" + tagged.LabelRest);
            Check("꼬리표 없으면 이름 그대로", new CardLineVm("TEMP_MAX", "", "", "").LabelRest == "TEMP_MAX"
                  && new CardLineVm("TEMP_MAX", "", "", "").LabelTag == "", "");
            Check("닫는 괄호가 멀면 꼬리표 아님", CardLineVm.TagOf("[이건 꼬리표가 아니라 그냥 긴 글] 내용") == "", "");
            Check("[] 는 꼬리표 아님", CardLineVm.TagOf("[] 내용") == "", "");
            Check("색을 안 정하면 꼬리표로 — 경고", new CardLineVm("[경고] x", "", ScoreBands.GradeNone, "").Grade == ScoreBands.GradeWarn, "");
            Check("색을 안 정하면 꼬리표로 — 정상", new CardLineVm("[정상] x", "", null, "").Grade == ScoreBands.GradeGood, "");
            Check("정한 색이 이김", new CardLineVm("[정상] x", "", ScoreBands.GradeBad, "").Grade == ScoreBands.GradeBad, "");
        }

        private static void DbPathNames()
        {
            Say("DB — 견줄 자리 이름");

            Check("파일 이름만 뗌", DbPathText.Name("C:\\db\\before\\io.csv") == "io.csv",
                  DbPathText.Name("C:\\db\\before\\io.csv"));
            Check("폴더도 마지막 조각", DbPathText.Name("C:\\db\\before") == "before",
                  DbPathText.Name("C:\\db\\before"));
            Check("끝에 붙은 구분 기호는 떼고",
                  DbPathText.Name("C:\\db\\before\\") == "before",
                  DbPathText.Name("C:\\db\\before\\"));
            Check("빗금 경로도 같게", DbPathText.Name("/srv/db/after") == "after",
                  DbPathText.Name("/srv/db/after"));
            Check("조각이 하나면 그대로", DbPathText.Name("io.csv") == "io.csv",
                  DbPathText.Name("io.csv"));

            // 뿌리만 적은 경우. 빈 칸을 보여 주면 "안 넣었다" 로 읽히므로
            // 무엇이든 남겨야 합니다 — 드라이브는 글자가 남고, 구분 기호
            // 하나뿐이면 적은 그대로 보여 줍니다.
            Check("드라이브만 적었으면 드라이브", DbPathText.Name("C:\\") == "C:",
                  DbPathText.Name("C:\\"));
            Check("구분 기호 하나면 적은 그대로", DbPathText.Name("/") == "/",
                  DbPathText.Name("/"));
            Check("비어 있으면 빈 글", DbPathText.Name(string.Empty).Length == 0, null);
            Check("null 도 빈 글", DbPathText.Name(null).Length == 0, null);

            // ---- 아래 한 줄 ----
            Check("안 정했으면 어떻게 넣는지 알려 줌",
                  DbPathText.Note(string.Empty) == DbPathText.None, DbPathText.Note(string.Empty));
            Check("없는 자리는 없다고 말함",
                  DbPathText.Note(Path.Combine(Path.GetTempPath(), "logscope-no-such-place"))
                      == "이 자리에 아무것도 없습니다",
                  DbPathText.Note(Path.Combine(Path.GetTempPath(), "logscope-no-such-place")));

            string dir = Path.Combine(Path.GetTempPath(), "logscope-note-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "io.csv");
            File.WriteAllText(file, "a,b\n");
            try
            {
                Check("폴더는 폴더라고", DbPathText.Note(dir) == "폴더", DbPathText.Note(dir));
                Check("파일은 파일이라고", DbPathText.Note(file) == "파일", DbPathText.Note(file));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// 메인 화면 칸의 판정 — PASS · WARNING · FAIL.
        ///
        /// <b>경계를 봅니다.</b> "100 개 이상이 FAIL" 을 99/100 으로 한 번
        /// 틀리면, 딱 100 개인 날에 FAIL 이 안 뜨고 아무도 모릅니다.
        ///
        /// 그리고 <b>판정에 안 들어간 수가 글에 남는지</b> 봅니다. 없어진
        /// IO 가 스무 개인데 PASS 만 보이면 "아무 일 없었다" 로 읽힙니다.
        /// </summary>
        private static void DbVerdictRule()
        {
            Say("DB — 판정 PASS · WARNING · FAIL");

            Check("0 개는 PASS", DbVerdict.Of(0) == DbVerdict.Pass, DbVerdict.Of(0));
            Check("1 개는 WARNING", DbVerdict.Of(1) == DbVerdict.Warning, DbVerdict.Of(1));
            Check("99 개는 WARNING", DbVerdict.Of(99) == DbVerdict.Warning, DbVerdict.Of(99));
            Check("100 개는 FAIL", DbVerdict.Of(100) == DbVerdict.Fail, DbVerdict.Of(100));
            Check("101 개도 FAIL", DbVerdict.Of(101) == DbVerdict.Fail, DbVerdict.Of(101));
            Check("기준이 100", DbVerdict.FailFrom == 100,
                  DbVerdict.FailFrom.ToString(CultureInfo.InvariantCulture));

            // 세 낱말이 서로 달라야 합니다 (하나라도 같으면 색이 엉킵니다).
            Check("세 낱말이 서로 다름",
                  DbVerdict.Pass != DbVerdict.Warning && DbVerdict.Warning != DbVerdict.Fail
                  && DbVerdict.Pass != DbVerdict.Fail, null);

            // ---- 낱말 아래 글 ----
            //
            // 판정마다 <b>다음에 뭘 해야 하는지</b>가 달라서 글도 다릅니다.
            // 셋을 한 함수에 둔 까닭은 화면 두 곳(메인 칸 · 분석 2)이 같은
            // 글을 보여 주기 때문입니다.

            var clean = new DbDiffList.Tally();
            Check("PASS 는 차이 없다고 말함", DbVerdict.Why(clean) == "차이 없습니다.",
                  DbVerdict.Why(clean));

            var lots = new DbDiffList.Tally();
            lots.Changed = 1200;
            Check("FAIL 은 파일을 다시 보라고 말함",
                  DbVerdict.Why(lots).Contains("파일을 다시 확인"), DbVerdict.Why(lots));
            Check("FAIL 은 개수도 적음", DbVerdict.Why(lots).Contains("1,200개"),
                  DbVerdict.Why(lots));

            // FAIL 에서는 이름을 안 적습니다. 1200 개 중 여덟 개만 적으면
            // "이 여덟 개가 문제" 로 읽히는데, 그게 아니라 파일이 잘못
            // 걸렸을 가능성이 먼저입니다.
            var manyNames = new List<string> { "IO_A", "IO_B" };
            Check("FAIL 은 이름을 안 적음", !DbVerdict.Why(lots, manyNames).Contains("IO_A"),
                  DbVerdict.Why(lots, manyNames));

            // WARNING 은 어떤 IO 가 달라졌는지 적습니다 — 개수만 보면
            // 결국 분석 2 화면을 열어 목록을 뒤져야 합니다.
            var few = new DbDiffList.Tally();
            few.Changed = 3;
            var three = new List<string> { "TEMP_MAX", "PRES_SET", "VALVE_1" };
            string w = DbVerdict.Why(few, three);
            Check("WARNING 은 이름을 적음",
                  w.Contains("TEMP_MAX") && w.Contains("PRES_SET") && w.Contains("VALVE_1"), w);
            Check("WARNING 은 개수도 적음", w.Contains("3개"), w);
            Check("다 적었으면 \"외 N개\" 는 없음", !w.Contains("외 "), w);

            // 이름을 다 못 적었으면 몇 개를 안 적었는지 말해 줍니다.
            var lots2 = new DbDiffList.Tally();
            lots2.Changed = 30;
            string w2 = DbVerdict.Why(lots2, three);
            Check("못 적은 개수를 알려 줌", w2.Contains("외 27개"), w2);

            // 이름을 모으지 않은 자리에서는 FAIL 기준을 알려 줍니다.
            Check("이름이 없으면 FAIL 기준을 알려 줌",
                  DbVerdict.Why(few).Contains("100개부터 FAIL"), DbVerdict.Why(few));

            // ---- 판정에 안 들어간 수 ----
            //
            // 없어진 IO 가 스무 개인데 "차이 없습니다" 만 보이면 거짓말입니다.
            var hidden = new DbDiffList.Tally();
            hidden.Gone = 20;
            Check("차이 0 이라도 없어진 IO 는 글에 남음",
                  DbVerdict.Why(hidden).Contains("없어진 IO 20개"), DbVerdict.Why(hidden));
            Check("판정에 안 넣는다고 적음",
                  DbVerdict.Why(hidden).Contains("판정에 안 넣음"), DbVerdict.Why(hidden));
            Check("그래도 판정은 PASS", DbVerdict.Of(hidden.Changed) == DbVerdict.Pass, null);

            var both = new DbDiffList.Tally();
            both.Changed = 2; both.Added = 5;
            Check("WARNING 에도 덧붙음", DbVerdict.Why(both, three).Contains("새로 생긴 IO 5개"),
                  DbVerdict.Why(both, three));
            Check("FAIL 에도 덧붙음", DbVerdict.Why(lots).Length > 0, null);

            Check("아무것도 없으면 덧말 없음", !DbVerdict.Why(clean).Contains("판정에 안 넣음"),
                  DbVerdict.Why(clean));
        }

        /// <summary>
        /// 메인 화면 분석 2 요약의 "이전 → 이후" (1.04). 이름 목록과 값
        /// 목록이 같은 잣대로 골라져야 "차이 발생 N개" 와 줄 수가 맞습니다.
        /// </summary>
        private static void DbChangedValuesRule()
        {
            Say("DB — 달라진 IO 의 이전 → 이후 값");

            var lines = new List<DbDiffLine>
            {
                ChangedLine("io", "TEMP_MAX", "VALUE", "1", "2"),
                ChangedLine("io", "TEMP_MAX", "MAX", "5", "6"),
                ChangedLine("io", "PRES_SET", "VALUE", "", "3"),
                new DbDiffLine { Table = "io", Io = "SAME", Kind = DbDiffLine.KindRow,
                                 Change = DbDiffLine.ChangeSame, Before = "7", After = "7" },
                ChangedLine("other", "TEMP_MAX", "VALUE", "a", "b"),
            };

            List<DbChangedIo> v = DbDiffList.ChangedValues(lines, 10);
            Check("칠한 줄의 IO 만, 표+이름으로 셈", v.Count == 3,
                  v.Count.ToString(CultureInfo.InvariantCulture));
            Check("개수(Count) 와 같은 수", v.Count == DbDiffList.Count(lines).Changed, null);
            Check("대표 값은 먼저 나온 열", v[0].Change == "1 → 2", v[0].Change);
            Check("나머지 열은 Others 로", v[0].Others.Count == 1 && v[0].Others[0].Column == "MAX", null);
            Check("열이 여럿이면 덧말에 열마다",
                  v[0].OthersText(3) == "VALUE: 1 → 2  ·  MAX: 5 → 6", v[0].OthersText(3));
            Check("열이 하나면 덧말 없음", v[1].OthersText(3).Length == 0, v[1].OthersText(3));
            Check("빈 값은 (빈 값) 으로", v[1].Change == "(빈 값) → 3", v[1].Change);

            List<DbChangedIo> one = DbDiffList.ChangedValues(lines, 1);
            Check("상한까지만 IO 를 모음", one.Count == 1, null);
            Check("상한이 차도 적은 IO 의 열은 다 모음", one[0].Others.Count == 1, null);

            List<string> names = DbDiffList.ChangedIos(lines, 10);
            Check("이름 목록과 값 목록의 차례가 같음",
                  names.Count == v.Count && names[1] == v[1].Io && names[2] == v[2].Io,
                  string.Join(",", names.ToArray()));

            var many = new DbChangedIo(ChangedLine("t", "X", "A", "1", "2"));
            for (int i = 0; i < 5; i++) many.Others.Add(ChangedLine("t", "X", "C" + i, "0", "1"));
            Check("덧말이 넘치면 \"외 N칸\"", many.OthersText(3).EndsWith("외 2칸"),
                  many.OthersText(3));
        }

        /// <summary>분석 2 목록의 빨간 "(+3)" · "(a → b)" (1.08).</summary>
        private static void DbDeltaText()
        {
            Say("DB — 얼마나 달라졌나 (+N / a → b)");

            Check("숫자는 이후 − 이전, + 붙임",
                  ChangedLine("t", "A", "v", "1", "4").Delta == "(+3)", ChangedLine("t", "A", "v", "1", "4").Delta);
            Check("줄면 −",
                  ChangedLine("t", "A", "v", "2.5", "2.25").Delta == "(-0.25)",
                  ChangedLine("t", "A", "v", "2.5", "2.25").Delta);
            Check("소수 빼기에 찌꺼기 없음 (0.3 − 0.1)",
                  ChangedLine("t", "A", "v", "0.1", "0.3").Delta == "(+0.2)",
                  ChangedLine("t", "A", "v", "0.1", "0.3").Delta);
            Check("음수끼리", ChangedLine("t", "A", "v", "-5", "-7").Delta == "(-2)",
                  ChangedLine("t", "A", "v", "-5", "-7").Delta);
            Check("글자는 a → b",
                  ChangedLine("t", "A", "v", "OPEN", "CLOSE").Delta == "(OPEN → CLOSE)",
                  ChangedLine("t", "A", "v", "OPEN", "CLOSE").Delta);
            Check("한쪽만 숫자면 글자 꼴",
                  ChangedLine("t", "A", "v", "NULL", "3").Delta == "(NULL → 3)",
                  ChangedLine("t", "A", "v", "NULL", "3").Delta);
            Check("숫자로 같고 글자만 다르면 글자 꼴 (+0 이 아님)",
                  ChangedLine("t", "A", "v", "1.0", "1").Delta == "(1.0 → 1)",
                  ChangedLine("t", "A", "v", "1.0", "1").Delta);
            Check("빈 값은 (빈 값)",
                  ChangedLine("t", "A", "v", "", "x").Delta == "((빈 값) → x)",
                  ChangedLine("t", "A", "v", "", "x").Delta);

            var same = new DbDiffLine { Before = "1", After = "1", Change = DbDiffLine.ChangeSame };
            Check("칠하지 않은 줄은 빈 글자", same.Delta.Length == 0, same.Delta);
        }

        private static DbDiffLine ChangedLine(string table, string io, string column,
                                              string before, string after)
        {
            return new DbDiffLine
            {
                Table = table, Io = io, Column = column, Kind = DbDiffLine.KindRow,
                Change = DbDiffLine.ChangeDiff, Mark = DbDiffLine.MarkValue,
                Before = before, After = after,
            };
        }

        /// <summary>목록에서 표·갈래·열 이름으로 한 줄 찾기.</summary>
        private static DbDiffLine Line(List<DbDiffLine> lines, string table, string kind, string column)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                DbDiffLine ln = lines[i];
                if (ln.Table == table && ln.Kind == kind && ln.Column == column) return ln;
            }
            return null;
        }

        private static int CountLines(string csv)
        {
            int n = 0;
            for (int i = 0; i < csv.Length; i++) if (csv[i] == '\n') n++;
            return n;
        }

        private static void DbScript()
        {
            Say("DB — SQL 글 만들기");

            var b = new DbSnapshot();
            SqlDumpReader.Read(WriteText("sc_before.sql",
                "CREATE TABLE `recipe` (`id` int NOT NULL, `name` varchar(64) NOT NULL," +
                " `temp_max` decimal(6,2) DEFAULT NULL, PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1,'가열 A',182.50),(3,'지울 행',1.00);\n"), b, 0);

            var a = new DbSnapshot();
            SqlDumpReader.Read(WriteText("sc_after.sql",
                "CREATE TABLE `recipe` (`id` int NOT NULL, `name` varchar(64) NOT NULL," +
                " `temp_max` decimal(6,2) DEFAULT NULL, `added` int DEFAULT '0', PRIMARY KEY (`id`));\n" +
                "INSERT INTO `recipe` VALUES (1,'가열 A2',999.90,5),(4,'새 행',2.00,6);\n"), a, 0);

            DbDiffResult d = DbDiff.Compare(b, a);
            string sql = SqlScript.Build(d, null);

            Check("UPDATE 를 만듦", sql.Contains("UPDATE `recipe` SET"), null);
            Check("바뀐 값이 들어감", sql.Contains("999.90"), null);
            Check("WHERE 가 키로", sql.Contains("WHERE `id` = 1"), null);
            Check("새 열은 ADD COLUMN", sql.Contains("ADD COLUMN `added`"), null);
            Check("INSERT 를 만듦", sql.Contains("INSERT INTO `recipe`"), null);

            // 지우는 문장은 늘 주석입니다. 글만 보고 돌렸을 때 값이 사라지면 안 됩니다.
            int at = sql.IndexOf("DELETE FROM");
            Check("DELETE 가 들어감", at > 0, null);
            int lineStart = sql.LastIndexOf('\n', at) + 1;
            Check("DELETE 는 주석으로", sql.Substring(lineStart, at - lineStart).Trim().StartsWith("--"),
                  sql.Substring(lineStart, Math.Min(40, sql.Length - lineStart)));

            // 따옴표가 든 값은 반드시 막아야 합니다. 안 막으면 글이 깨지고,
            // 깨진 글을 사람이 돌리면 엉뚱한 문장이 됩니다.
            Check("따옴표를 막음", SqlScript.Literal("가열 A'") == "'가열 A\\''", SqlScript.Literal("가열 A'"));
            Check("역슬래시도 막음", SqlScript.Literal("a\\b") == "'a\\\\b'", SqlScript.Literal("a\\b"));
            Check("NULL 은 따옴표 없이", SqlScript.Literal(null) == "NULL", SqlScript.Literal(null));
            Check("숫자는 따옴표 없이", SqlScript.Literal("182.50") == "182.50", SqlScript.Literal("182.50"));
            Check("이름의 역따옴표를 막음", SqlScript.Quote("a`b") == "`a``b`", SqlScript.Quote("a`b"));

            // 방향은 하나뿐입니다 (0.88 에 되돌리는 방향을 뺐습니다).
            // 만든 글의 머리에 어느 방향인지 적혀 있어야 합니다 — 저장한
            // .sql 을 나중에 열어 본 사람이 짐작하게 두면 안 됩니다.
            Check("글 머리에 방향이 적힘", sql.StartsWith("-- 이전 DB 를 이후와 같게"),
                  sql.Substring(0, Math.Min(40, sql.Length)));

            // 명령 줄 나누기
            string exe, args;
            Check("따옴표 경로를 나눔",
                  DbTools.SplitCommand("\"C:\\My Tools\\mysqldump.exe\" -u root db", out exe, out args)
                  && exe == "C:\\My Tools\\mysqldump.exe" && args == "-u root db", exe + " | " + args);
            Check("따옴표 없는 것도", DbTools.SplitCommand("mysqldump db", out exe, out args)
                  && exe == "mysqldump" && args == "db", exe + " | " + args);
            Check("빈 줄은 거짓", !DbTools.SplitCommand("   ", out exe, out args), null);

            // 실행 파일 경로를 따로 적어 두면 아래 줄은 인수만입니다.
            Check("경로 + 인수",
                  DbTools.Resolve("C:\\MySQL\\bin\\mysqldump.exe", "--login-path=old --databases mydb",
                                  out exe, out args)
                  && exe == "C:\\MySQL\\bin\\mysqldump.exe"
                  && args == "--login-path=old --databases mydb", exe + " | " + args);

            // 경로에 따옴표를 붙여 적어도 됩니다 — 공백이 든 경로를 그렇게 적는
            // 습관이 있어서, 그걸 그대로 실행 파일 이름으로 넘기면 못 찾습니다.
            Check("경로의 따옴표는 떼어 냄",
                  DbTools.Resolve("\"C:\\My Tools\\mysqldump.exe\"", "--databases mydb",
                                  out exe, out args)
                  && exe == "C:\\My Tools\\mysqldump.exe", exe);

            // 경로를 안 적었으면 아래 줄이 명령 줄 전체입니다 (예전 방식).
            Check("경로가 없으면 명령 줄 전체로",
                  DbTools.Resolve("", "\"C:\\My Tools\\mysqldump.exe\" --databases mydb",
                                  out exe, out args)
                  && exe == "C:\\My Tools\\mysqldump.exe" && args == "--databases mydb",
                  exe + " | " + args);

            Check("경로만 있고 인수가 없어도 됨",
                  DbTools.Resolve("mysqldump.exe", "", out exe, out args)
                  && exe == "mysqldump.exe" && args == "", exe + " | " + args);

            Check("둘 다 비면 거짓", !DbTools.Resolve("", "", out exe, out args), null);
        }

        /// <summary>
        /// 설정 파일(config.txt) 읽기. 손으로 고치는 파일이라 <b>엉망으로
        /// 적힌 경우</b>가 실제로 생깁니다 — 그때 기본값으로 버텨야 합니다.
        /// </summary>
        private static void AppConfigParse()
        {
            Say("설정 파일(config.txt) 읽기");

            AppConfig def = AppConfig.Default;
            Check("기본 프로그램 이름", def.Name == "LogScope", def.Name);
            Check("기본 이름 1", def.Title(1) == "로그 비교", def.Title(1));
            Check("기본 이름 2", def.Title(2) == "DB 분석", def.Title(2));
            Check("기본 이름 3", def.Title(3) == "분석 3", def.Title(3));

            AppConfig c = AppConfig.Parse(new[]
            {
                "# 주석",
                "",
                "  Name  =  로그보기  ",
                "FullName = 설비 Log 비교 · 분석 도구 (사내용)",
                "analysis1 = 타이밍 점검 | 켜고 끄는 시각이 밀렸는지 봅니다.",
                "ANALYSIS2=압력 비교",
                "analysis3 = 세 번째 | 설명만 있음",
                "analysis9 = 없는 자리",
                "열쇠없는줄",
                "= 값만",
                "모르는열쇠 = 아무거나",
            });
            Check("이름 읽기 (앞뒤 공백 떼기)", c.Name == "로그보기", c.Name);
            // 긴 이름은 보여 주기만 하는 값이라 괄호·가운뎃점이 들어가도 됩니다.
            Check("긴 이름 읽기", c.FullName == "설비 Log 비교 · 분석 도구 (사내용)", c.FullName);
            Check("열쇠는 대소문자 안 가림", c.Title(2) == "압력 비교", c.Title(2));
            Check("분석 이름 읽기", c.Title(1) == "타이밍 점검", c.Title(1));
            Check("분석 설명 읽기", c.Summary(1) == "켜고 끄는 시각이 밀렸는지 봅니다.", c.Summary(1));
            Check("설명 없으면 기본 설명", c.Summary(2) == def.Summary(2), c.Summary(2));
            Check("세 번째 이름", c.Title(3) == "세 번째", c.Title(3));

            // 모르는 열쇠, 열쇠 없는 줄, 값 없는 줄은 건너뜁니다. 뒤 버전에서
            // 생긴 열쇠가 적힌 파일로 옛 프로그램을 켜도 터지면 안 됩니다.
            AppConfig junk = AppConfig.Parse(new[] { "모르는 열쇠 = 1", "그냥 글자", "name =" });
            Check("모르는 줄은 기본값 그대로", junk.Name == "LogScope", junk.Name);
            // 긴 이름은 안 적으면 빈 글자입니다. 그러면 화면에서 그 줄이
            // 사라집니다 — 짧은 이름으로 대신 채우면, 같은 이름이 두 번
            // 적혀 지저분합니다.
            Check("긴 이름은 안 적으면 빔", junk.FullName.Length == 0, junk.FullName);
            Check("기본값도 빔", AppConfig.Default.FullName.Length == 0, AppConfig.Default.FullName);

            // 이름 자리를 비워 둔 분석 줄은 그 자리의 기본값입니다.
            AppConfig blank = AppConfig.Parse(new[] { "analysis1 = | 설명만" });
            Check("이름 비운 줄은 기본 이름", blank.Title(1) == "로그 비교", blank.Title(1));
            Check("그 줄의 설명은 살림", blank.Summary(1) == "설명만", blank.Summary(1));

            // 아예 없어도 됩니다.
            AppConfig none = AppConfig.Parse(new string[0]);
            Check("빈 파일은 기본값", none.Name == "LogScope" && none.Title(3) == "분석 3", null);
            Check("null 도 견딤", AppConfig.Parse(null).Title(1) == "로그 비교", null);

            // 범위 밖은 빈 글자입니다. 터지면 안 됩니다.
            Check("0 번은 빈 글자", def.Title(0) == "", def.Title(0));
            Check("4 번은 빈 글자", def.Title(4) == "", def.Title(4));
            Check("범위 밖 설명도 빈 글자", def.Summary(0) == "", def.Summary(0));
        }

        /// <summary>
        /// 파일로 읽을 때. 메모장이 UTF-8 로도, CP949 로도 저장하므로 둘 다
        /// 읽혀야 합니다. 전에 쓰던 두 파일(appname.txt / screens.txt)도
        /// 그대로 읽혀야 합니다 — 적어 둔 것을 파일 이름이 바뀌었다는 이유로
        /// 잃으면 안 됩니다.
        /// </summary>
        private static void AppConfigFile()
        {
            Say("설정 파일 인코딩과 옛 파일");

            string dir = Path.Combine(_dir, "cfg");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, AppConfig.FileName);
            string body = "name = 로그보기\nanalysis1 = 타이밍 점검 | 밀렸는지 봅니다\nanalysis2 = 압력 비교\n";

            File.WriteAllText(path, body, new UTF8Encoding(true));
            Check("BOM 붙은 UTF-8", AppConfig.Load(path).Name == "로그보기", AppConfig.Load(path).Name);

            File.WriteAllText(path, body, new UTF8Encoding(false));
            Check("BOM 없는 UTF-8", AppConfig.Load(path).Title(2) == "압력 비교",
                  AppConfig.Load(path).Title(2));

            // CP949 는 리눅스 mono 에 없을 수 있습니다. 있을 때만 봅니다.
            Encoding cp949 = null;
            try { cp949 = Encoding.GetEncoding(949); }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            if (cp949 != null)
            {
                File.WriteAllBytes(path, cp949.GetBytes(body));
                Check("메모장 ANSI(CP949)", AppConfig.Load(path).Title(1) == "타이밍 점검",
                      AppConfig.Load(path).Title(1));
            }

            // 없는 파일은 기본값입니다. 이름 하나 때문에 안 켜지면 안 됩니다.
            string empty = Path.Combine(_dir, "nothing");
            Directory.CreateDirectory(empty);
            Check("없는 파일은 기본값",
                  AppConfig.Load(Path.Combine(empty, AppConfig.FileName)).Name == "LogScope", null);
            Check("빈 경로도 견딤", AppConfig.Load(null).Title(1) == "로그 비교", null);

            // ---- 옛 파일 두 개 ----
            string old = Path.Combine(_dir, "old");
            Directory.CreateDirectory(old);
            File.WriteAllText(Path.Combine(old, AppConfig.LegacyNameFile), "옛이름\n",
                              new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(old, AppConfig.LegacyScreenFile),
                              "# 주석\n옛 분석 | 옛 설명\n둘째\n", new UTF8Encoding(false));

            AppConfig legacy = AppConfig.Load(Path.Combine(old, AppConfig.FileName));
            Check("옛 appname.txt 를 읽음", legacy.Name == "옛이름", legacy.Name);
            Check("옛 screens.txt 를 읽음", legacy.Title(1) == "옛 분석", legacy.Title(1));
            Check("옛 파일의 설명도 읽음", legacy.Summary(1) == "옛 설명", legacy.Summary(1));
            Check("옛 파일 둘째 줄", legacy.Title(2) == "둘째", legacy.Title(2));

            // config.txt 가 있으면 옛 파일은 보지 않습니다. 둘 다 있을 때
            // 어느 것이 이기는지 헷갈리면 안 됩니다.
            File.WriteAllText(Path.Combine(old, AppConfig.FileName), "name = 새이름\n",
                              new UTF8Encoding(false));
            AppConfig both = AppConfig.Load(Path.Combine(old, AppConfig.FileName));
            Check("config.txt 가 이김", both.Name == "새이름", both.Name);
            Check("그때 옛 분석 이름은 안 읽음", both.Title(1) == "로그 비교", both.Title(1));

            // 본보기 글은 그대로 읽혀야 합니다 — build.bat 이 out 폴더에
            // 넣어 주는 것이 이 글입니다.
            AppConfig sample = AppConfig.Parse(
                AppConfig.Sample().Replace("\r\n", "\n").Split('\n'));
            Check("본보기는 기본값과 같음",
                  sample.Name == "LogScope" && sample.Title(1) == "로그 비교"
                  && sample.Title(3) == "분석 3", sample.Title(1));
            Check("본보기 설명도 같음",
                  sample.Summary(2) == AppConfig.Default.Summary(2), sample.Summary(2));
        }

        private static void SettingsRoundTrip()
        {
            Say("설정 저장/불러오기");
            var s = new AppSettings();
            s.Theme = "dark";
            s.ActiveSet = 3;
            s.Sets[3].Title = "3호기 개조";
            s.Sets[3].BeforeFolder = @"D:\로그\이전";
            s.AbsoluteTolerance = 0.25;
            s.AlignIo = "START 신호";
            s.AlignEdge = 1;
            s.AlignOccurrence = 3;
            s.AlignLevel = 5;
            s.RelativeTolerancePercent = 0.25;
            s.SortMetric = DiffMetric.TimeRatio;

            var g = new GroupDef("밸브 묶음");
            g.Members.Add("밸브 OPEN");
            g.Members.Add("밸브 CLOSE");
            s.Groups.Add(g);

            s.A3BeforePath = @"D:\분석3\이전";
            s.A3AfterPath = @"D:\분석3\이후.csv";
            AppSettings back = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("분석 3 자리", back.A3BeforePath == @"D:\분석3\이전" && back.A3AfterPath == @"D:\분석3\이후.csv",
                  back.A3BeforePath + " | " + back.A3AfterPath);

            Check("테마", back.Theme == "dark", back.Theme);
            Check("고른 세트", back.ActiveSet == 3, back.ActiveSet.ToString());
            Check("세트 이름", back.Sets[3].Title == "3호기 개조", back.Sets[3].Title);
            Check("세트별 기본 폴더", back.Sets[3].BeforeFolder == @"D:\로그\이전", back.Sets[3].BeforeFolder);
            Near("절대 허용 오차", back.AbsoluteTolerance, 0.25, 1e-9);
            Check("맞추기 기준 IO", back.AlignIo == "START 신호", back.AlignIo);
            Check("맞추기 변화 방향", back.AlignEdge == 1, "실제 " + back.AlignEdge);
            Check("맞추기 몇 번째", back.AlignOccurrence == 3, "실제 " + back.AlignOccurrence);
            Near("맞추기 기준 값", back.AlignLevel, 5, 1e-9);

            // NaN("알아서 고름")은 JSON 에 담을 수 없어서 아예 안 적습니다.
            // 다시 읽으면 도로 NaN 이어야 합니다 — 0 으로 떨어지면 "0 이 되는
            // 순간" 이라는 엉뚱한 기준이 됩니다.
            s.AlignLevel = double.NaN;
            AppSettings auto = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("알아서 고름은 그대로 남음", double.IsNaN(auto.AlignLevel), "실제 " + auto.AlignLevel);

            // 없앤 눈금(0–1 정규화)이 적힌 옛 설정 파일은 "값 그대로" 로
            // 되돌아와야 합니다. 그대로 두면 어느 단추도 안 켜진 채 뜹니다.
            s.ValueScaleMode = "normalized";
            AppSettings old = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("없앤 눈금은 값 그대로로", old.ValueScaleMode == AppSettings.ScaleRaw, old.ValueScaleMode);

            s.ValueScaleMode = AppSettings.ScaleDelta;
            AppSettings keep = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("차이 눈금은 그대로 남음", keep.ValueScaleMode == AppSettings.ScaleDelta, keep.ValueScaleMode);

            // 차이 영역 표시와 파형 분리 보기는 같이 켜지지 않습니다. 둘을 따로
            // 켜던 시절의 설정 파일이 남아 있어도 화면에서 만들 수 없는 상태로
            // 뜨지 않아야 합니다.
            s.ShadeDifference = true;
            s.SeparateTraces = true;
            AppSettings both = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("둘 다 켜져 있으면 하나만 남음", !(both.ShadeDifference && both.SeparateTraces),
                  "음영 " + both.ShadeDifference + " / 분리 " + both.SeparateTraces);
            Check("차이 영역 쪽을 남김", both.ShadeDifference, null);

            // 선 하나씩 끄고 보기. 둘 다 꺼진 파일로 켜면 빈 그래프가 뜨고,
            // 그 기억이 하루 뒤에도 남아 "그래프가 안 나온다" 가 됩니다.
            s.ShowBefore = false;
            s.ShowAfter = false;
            AppSettings both2 = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("둘 다 끈 것은 둘 다 켠 것으로", both2.ShowBefore && both2.ShowAfter, null);

            // 한쪽만 끈 것은 그대로 살아야 합니다 — 그게 쓰는 상태입니다.
            s.ShowBefore = true;
            s.ShowAfter = false;
            AppSettings oneSide = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("이전만 켠 것은 그대로", oneSide.ShowBefore && !oneSide.ShowAfter, null);

            s.ShowBefore = false;
            s.ShowAfter = true;
            AppSettings otherSide = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("이후만 켠 것도 그대로", !otherSide.ShowBefore && otherSide.ShowAfter, null);

            // 옛 설정 파일에는 이 열쇠가 없습니다. 그때는 둘 다 그렸습니다.
            var old3 = new Dictionary<string, object>();
            AppSettings legacy = AppSettings.FromJson(old3);
            Check("옛 설정은 둘 다 켠 것", legacy.ShowBefore && legacy.ShowAfter, null);

            s.ShowBefore = true;
            s.ShowAfter = true;

            // 파형 분리만 켜 둔 것은 그대로 살아야 합니다.
            s.ShadeDifference = false;
            s.SeparateTraces = true;
            AppSettings sep = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("파형 분리만 켠 것은 그대로", sep.SeparateTraces && !sep.ShadeDifference, null);
            Near("비율 허용 오차(%)", back.RelativeTolerancePercent, 0.25, 1e-9);
            Check("정렬 기준", back.SortMetric == DiffMetric.TimeRatio, back.SortMetric.ToString());

            // 화면에서 뺀 차이량(차이 면적 · RMS · 구간 수)이 예전 설정에
            // 남아 있으면 기본값으로 돌아와야 합니다. 그대로 두면 안 보이는
            // 기준으로 정렬돼 "왜 이 차례지" 를 알 수 없게 됩니다.
            DiffMetric[] gone = { DiffMetric.Area, DiffMetric.Rms, DiffMetric.SegmentCount };
            for (int i = 0; i < gone.Length; i++)
            {
                s.SortMetric = gone[i];
                AppSettings old2 = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
                Check("뺀 차이량(" + gone[i] + ")은 기본값으로",
                      old2.SortMetric == DiffMetric.MaxAbs, old2.SortMetric.ToString());
            }
            Check("그룹 이름", back.Groups.Count == 1 && back.Groups[0].Name == "밸브 묶음", null);
            Check("그룹 구성원을 이름으로 저장", back.Groups[0].Members.Count == 2
                  && back.Groups[0].Members[0] == "밸브 OPEN", null);
        }
    }
}
