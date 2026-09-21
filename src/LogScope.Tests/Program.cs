using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LogScope.Core.Align;
using LogScope.Core.Compare;
using LogScope.Core.Io;
using LogScope.Core.Model;
using LogScope.Core.Render;
using LogScope.Core.Settings;

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
        private static void UseConsoleEncoding()
        {
            PlatformID id = Environment.OSVersion.Platform;
            bool windows = id == PlatformID.Win32NT || id == PlatformID.Win32Windows
                        || id == PlatformID.Win32S || id == PlatformID.WinCE;
            if (windows) return;

            // 출력이 파일로 돌려져 있으면 런타임에 따라 예외가 납니다.
            // 인코딩 하나 때문에 테스트가 안 돌아가면 곤란합니다.
            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch (IOException) { }
            catch (NotSupportedException) { }
        }

        private static int Main()
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
                DiffSeries();
                DiffAcrossGaps();
                AxisChannelSwap();
                AxisRefusesCoarseValues();
                AxisWarnsOnFlatRuns();
                JsonRoundTrip();
                SettingsRoundTrip();
            }
            finally
            {
                try { Directory.Delete(_dir, true); } catch (IOException) { }
            }

            Console.WriteLine();
            Console.WriteLine("통과 " + _pass + " / 실패 " + _fail);
            return _fail == 0 ? 0 : 1;
        }

        // ---------------- 도우미 ----------------

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; Console.WriteLine("  OK    " + name); }
            else { _fail++; Console.WriteLine("  FAIL  " + name + (detail != null ? "  — " + detail : "")); }
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

        private static float[] Values(LogDataset ds, string name)
        {
            int i = ds.FindChannel(name);
            return i < 0 ? null : ds.Channels[i].Values;
        }

        // ---------------- 테스트 ----------------

        private static void CsvColumnsLayout()
        {
            Console.WriteLine("열이 IO 인 CSV");
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
            Console.WriteLine("행이 IO 인 CSV");
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
            Console.WriteLine("머리말이 붙어 표가 중간에서 시작하는 경우");
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
            Console.WriteLine("시간이 바뀔 때만 적힌 경우");
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
            Console.WriteLine("같은 시각이 반복되는 경우 (분 단위 표기 + 초 단위 기록)");
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
            Console.WriteLine("시각이 전부 같은 경우");
            var sb = new StringBuilder("Time,V\n");
            for (int i = 0; i < 5; i++) sb.Append("09:00:00,").Append(i).Append('\n');
            LogDataset ds = Open(WriteCsv("same.csv", sb.ToString()), Orientation.Auto);

            Check("샘플 번호를 시간축으로 사용", ds.TimeKind == TimeKind.Index, ds.TimeKind.ToString());
            Near("0 부터 시작", ds.Times[0], 0, 1e-9);
            Near("마지막은 4", ds.Times[4], 4, 1e-9);
        }

        private static void ZeroRunSurvives()
        {
            Console.WriteLine("0 이 오래 이어지는 구간");
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
            Console.WriteLine("xlsx 읽기");
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
            Console.WriteLine("채널 타입 판별");
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
            Console.WriteLine("픽셀 열 접기");
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
            Console.WriteLine("픽셀 열 접기 — 열 경계와 최소/최대");

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
            Console.WriteLine("확대했을 때 선이 남아 있는지");

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

            // 열마다 값을 뽑으면 빈 열이 없습니다. 차이 음영이 이걸 씁니다.
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
            Console.WriteLine("두 로그 비교");
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
            Console.WriteLine("IO 존재 차이");
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
            Console.WriteLine("허용 오차 — 이전 값 대비 %");

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
            Console.WriteLine("허용 오차 — 목록의 숫자와 판정이 맞는지");

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

            // 기준을 올리면 판정이 뒤집히지만 <b>적히는 숫자는 그대로</b>여야
            // 합니다. 재는 값이 재는 잣대에 딸려 있으면 안 됩니다.
            opt.RelativePercent = 10.0;
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, opt, null);
            ChannelDiff v2 = r2.Items.Find(d => d.Name == "V");
            Check("기준을 올리면 차이 아님", v2 != null && !v2.Changed, null);
            Near("그래도 최대 오차는 5% 그대로", v2.MaxPercent, 5, 0.01);
        }

        private static void HeatmapBuckets()
        {
            Console.WriteLine("히트맵 — 1분 칸으로 자르기");

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
            Console.WriteLine("히트맵 — 이전 값 대비 %로 판정");

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
            Console.WriteLine("히트맵 — 허용 오차를 바꿔도 칸의 숫자는 그대로");

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

            // 그런데 칸에 적히는 숫자는 <b>양쪽이 같아야</b> 합니다.
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
            Console.WriteLine("히트맵 — 상태 이름은 허용 오차와 무관");

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
            Console.WriteLine("세로축 단위");

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
            Console.WriteLine("히트맵 — 칸 폭을 바꿔도 IO 차례가 같은지");

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
            Console.WriteLine("숫자 표기 — 지수 표기 안 씀");

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
            Console.WriteLine("시간 맞추기 — IO 가 바뀌는 순간 기준");

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
            Console.WriteLine("시간 맞추기 — 처음부터 그 값인 로그");

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
            Console.WriteLine("시간 맞추기 — 0/1 말고 최댓값까지");

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
            Console.WriteLine("시간 맞추기 — 못 맞출 때 이유를 돌려주는지");

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
        /// 변화량(차분) — 이웃한 두 표본의 차이. 안 바뀌면 0, 1 오르면 +1,
        /// 1 내리면 -1.
        /// </summary>
        private static void DiffSeries()
        {
            Console.WriteLine("변화량 (차분)");

            // STEP: 0,0,1,1,2,1,0,0  →  차이 0,0,1,0,1,-1,-1,0
            var sb = new StringBuilder("Time,STEP\n");
            int[] step = { 0, 0, 1, 1, 2, 1, 0, 0 };
            for (int i = 0; i < step.Length; i++)
                sb.Append(i).Append(',').Append(step[i]).Append('\n');
            LogDataset ds = Open(WriteCsv("diff.csv", sb.ToString()), Orientation.Auto);

            Channel c = ds.Channels[ds.FindChannel("STEP")];
            float[] d = c.Diff;

            Check("길이는 값 배열과 같음", d.Length == step.Length, d.Length.ToString());
            Near("첫 표본은 0 (직전이 없음)", d[0], 0, 1e-9);
            Near("안 바뀌면 0", d[1], 0, 1e-9);
            Near("1 오르면 +1", d[2], 1, 1e-9);
            Near("그대로면 0", d[3], 0, 1e-9);
            Near("또 1 오르면 +1", d[4], 1, 1e-9);
            Near("1 내리면 -1", d[5], -1, 1e-9);
            Near("또 1 내리면 -1", d[6], -1, 1e-9);
            Near("마지막도 안 바뀌면 0", d[7], 0, 1e-9);

            Near("변화량 최소", c.DiffMin, -1, 1e-9);
            Near("변화량 최대", c.DiffMax, 1, 1e-9);

            // 같은 배열을 다시 받아도 다시 계산하지 않습니다 (갈무리).
            Check("두 번째 호출은 같은 배열", ReferenceEquals(d, c.Diff), null);

            // 접기도 차이를 봐야 합니다. 값을 먼저 접고 빼면 그 열 안에서
            // 얼마나 움직였는지가 이미 사라집니다.
            var cols = new Decimator.Column[4];
            Decimator.Build(ds, ds.FindChannel("STEP"), 0, 8, cols, 4, true);
            Check("접은 값도 차이", cols[1].Max >= 1 - 1e-6, "실제 " + cols[1].Max);

            Decimator.Build(ds, ds.FindChannel("STEP"), 0, 8, cols, 4, false);
            Check("false 면 값 그대로", cols[1].Max >= 1 - 1e-6 && cols[1].Min >= 0, null);

            double lo, hi;
            Check("범위도 차이로", Decimator.RangeIn(ds, ds.FindChannel("STEP"), 0, 8, out lo, out hi, true), null);
            Near("차이 범위 최소", lo, -1, 1e-9);
            Near("차이 범위 최대", hi, 1, 1e-9);
        }

        /// <summary>
        /// 값이 빈 자리(NaN)에서는 변화량도 없고, 그 뒤 첫 표본은 0 입니다.
        /// 빈 구간을 건너뛴 값 차이를 "변화" 로 그리면 없던 계단이 생깁니다.
        /// </summary>
        private static void DiffAcrossGaps()
        {
            Console.WriteLine("변화량 — 빈 구간 건너뛰기");

            // 5 에서 끊겼다가 100 으로 돌아옵니다. 95 짜리 변화로 보면 안 됩니다.
            string csv = "Time,V\n0,5\n1,5\n2,\n3,\n4,100\n5,101\n";
            LogDataset ds = Open(WriteCsv("diff_gap.csv", csv), Orientation.Auto);

            float[] d = ds.Channels[ds.FindChannel("V")].Diff;
            Near("첫 표본 0", d[0], 0, 1e-9);
            Near("안 바뀌면 0", d[1], 0, 1e-9);
            Check("빈 자리는 변화량도 없음", float.IsNaN(d[2]) && float.IsNaN(d[3]), null);
            Near("빈 구간 뒤 첫 표본은 0", d[4], 0, 1e-9);
            Near("그 다음은 제대로 +1", d[5], 1, 1e-9);

            Near("빈 구간을 건너뛴 95 는 안 생김",
                 ds.Channels[ds.FindChannel("V")].DiffMax, 1, 1e-9);
        }

        private static void AxisChannelSwap()
        {
            Console.WriteLine("가로축 바꿔 끼우기");

            // ELAPSED 는 0 에서 990 까지 10 씩 늘어납니다.
            // WOBBLE 은 오르내립니다 — 가로축이 될 수 없습니다.
            var sb = new StringBuilder("Time,ELAPSED,WOBBLE,V\n");
            for (int i = 0; i < 100; i++)
                sb.Append(i).Append(',').Append(i * 10)
                  .Append(',').Append(i % 7)
                  .Append(',').Append(i * 2).Append('\n');
            LogDataset ds = Open(WriteCsv("axis.csv", sb.ToString()), Orientation.Auto);

            Check("처음에는 로그의 시간 열", ds.UsesOwnTime, ds.AxisChannel);
            Near("원래 시간축 끝", ds.TimeEnd, 99, 1e-9);

            string problem;
            Check("늘어나는 IO 는 가로축이 됨", ds.SetAxisChannel("ELAPSED", out problem), problem);
            Check("가로축 이름이 바뀜", ds.AxisChannel == "ELAPSED", ds.AxisChannel);
            Check("이제 로그의 시간 열이 아님", !ds.UsesOwnTime, null);
            Near("가로축 시작", ds.TimeStart, 0, 1e-9);
            Near("가로축 끝", ds.TimeEnd, 990, 1e-9);

            // 값 읽기가 새 가로축을 따라가는지. ELAPSED 500 은 i = 50 이고,
            // 그때 V 는 100 입니다.
            int v = ds.FindChannel("V");
            Near("새 가로축으로 값을 읽음", ds.SampleAt(v, 500), 100, 1e-6);

            // 되돌리기
            ds.ClearAxisChannel();
            Check("되돌리면 로그의 시간 열", ds.UsesOwnTime, null);
            Near("원래 시간축이 그대로 살아 있음", ds.TimeEnd, 99, 1e-9);
            Near("값도 원래대로", ds.SampleAt(v, 50), 100, 1e-6);

            // 오르내리는 IO 는 거부합니다.
            Check("뒤로 가는 IO 는 거부", !ds.SetAxisChannel("WOBBLE", out problem), null);
            Check("이유를 알려 줌", problem.Contains("뒤로"), problem);
            Check("거부했으면 시간축은 그대로", ds.UsesOwnTime, ds.AxisChannel);

            // 후보 추리기
            Check("늘어나는 IO 는 후보", ds.CanBeAxis(ds.FindChannel("ELAPSED")), null);
            Check("오르내리는 IO 는 후보 아님", !ds.CanBeAxis(ds.FindChannel("WOBBLE")), null);
        }

        /// <summary>
        /// 채널 값은 float 라 유효자리가 약 7 자리입니다. 1970 년부터 센
        /// 밀리초 같은 큰 수를 가로축으로 놓으면 이웃 표본이 같은 값으로
        /// 뭉개져 그래프가 계단이 됩니다. 조용히 그리지 말고 막아야 합니다.
        /// </summary>
        private static void AxisRefusesCoarseValues()
        {
            Console.WriteLine("가로축 — 값이 너무 커서 표본이 뭉개질 때");

            // EPOCH 는 1.7e12 근처에서 100 씩 오릅니다. 그 크기에서 float 눈금은
            // 약 20 만이라, 100 간격은 아예 담기지 않습니다.
            // SMALL 은 같은 간격인데 0 에서 시작해서 멀쩡합니다.
            var sb = new StringBuilder("Time,EPOCH,SMALL,V\n");
            for (int i = 0; i < 200; i++)
                sb.Append(i).Append(',').Append(1700000000000L + i * 100L)
                  .Append(',').Append(i * 100)
                  .Append(',').Append(i).Append('\n');
            LogDataset ds = Open(WriteCsv("axis_coarse.csv", sb.ToString()), Orientation.Auto);

            string problem;
            Check("뭉개지는 IO 는 거부", !ds.SetAxisChannel("EPOCH", out problem), problem);
            Check("왜 안 되는지 알려 줌", problem.Contains("너무 커서"), problem);
            Check("거부했으면 시간축은 그대로", ds.UsesOwnTime, ds.AxisChannel);
            Check("뭉개지는 IO 는 후보도 아님", !ds.CanBeAxis(ds.FindChannel("EPOCH")), null);

            Check("같은 간격이라도 작은 수는 됨", ds.SetAxisChannel("SMALL", out problem), problem);
            Near("가로축 끝", ds.TimeEnd, 199 * 100, 1e-6);
            ds.ClearAxisChannel();
        }

        /// <summary>
        /// 여러 표본이 같은 값을 갖는 IO(1 초 단위 카운터 같은 것)는 막지
        /// 않습니다. 대신 왜 가로로 겹쳐 보이는지 말해 줘야 합니다.
        /// </summary>
        private static void AxisWarnsOnFlatRuns()
        {
            Console.WriteLine("가로축 — 여러 표본이 같은 자리에 겹칠 때");

            // TICK 은 10 표본마다 1 씩 오릅니다. 100 표본 중 90 개가 직전과
            // 같은 값입니다.
            var sb = new StringBuilder("Time,TICK,V\n");
            for (int i = 0; i < 100; i++)
                sb.Append(i).Append(',').Append(i / 10).Append(',').Append(i).Append('\n');
            LogDataset ds = Open(WriteCsv("axis_flat.csv", sb.ToString()), Orientation.Auto);

            string problem;
            Check("겹쳐도 가로축은 됨", ds.SetAxisChannel("TICK", out problem), problem);
            Check("겹친다고 알려 줌", ds.AxisNote.Length > 0, ds.AxisNote);
            Check("몇 개가 겹치는지 적음", ds.AxisNote.Contains("90"), ds.AxisNote);

            // 되돌리면 알림도 사라집니다.
            ds.ClearAxisChannel();
            Check("되돌리면 알림도 사라짐", ds.AxisNote.Length == 0, ds.AxisNote);
        }

        private static void JsonRoundTrip()
        {
            Console.WriteLine("JSON");
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

        private static void SettingsRoundTrip()
        {
            Console.WriteLine("설정 저장/불러오기");
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
            s.AxisIo = "경과 시간";
            s.RelativeTolerancePercent = 0.25;
            s.SortMetric = DiffMetric.SegmentCount;

            var g = new GroupDef("밸브 묶음");
            g.Members.Add("밸브 OPEN");
            g.Members.Add("밸브 CLOSE");
            s.Groups.Add(g);

            AppSettings back = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));

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
            Check("없앤 눈금은 값 그대로로", old.ValueScaleMode == "raw", old.ValueScaleMode);

            s.ValueScaleMode = "delta";
            AppSettings keep = AppSettings.FromJson(Json.Parse(Json.Write(s.ToJson())));
            Check("변화량은 그대로 남음", keep.ValueScaleMode == "delta", keep.ValueScaleMode);
            Check("가로축 IO", back.AxisIo == "경과 시간", back.AxisIo);
            Near("비율 허용 오차(%)", back.RelativeTolerancePercent, 0.25, 1e-9);
            Check("정렬 기준", back.SortMetric == DiffMetric.SegmentCount, back.SortMetric.ToString());
            Check("그룹 이름", back.Groups.Count == 1 && back.Groups[0].Name == "밸브 묶음", null);
            Check("그룹 구성원을 이름으로 저장", back.Groups[0].Members.Count == 2
                  && back.Groups[0].Members[0] == "밸브 OPEN", null);
        }
    }
}
