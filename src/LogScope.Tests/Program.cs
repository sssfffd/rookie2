using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
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
                ZoomedInTraceSurvives();
                CompareMetrics();
                ExistenceDifference();
                ToleranceIsRelativeToRange();
                ToleranceUsesValueSizeNotJustSpan();
                HeatmapBuckets();
                HeatmapRelativeTolerance();
                UnitsFromUnitRowAndName();
                HeatmapRangeForPercent();
                NumberTextHasNoExponent();
                HeatmapCellNumberMatchesTolerance();
                HeatmapStateNamesIgnoreAbsoluteTolerance();
                HeatmapOrderIsSameForEveryBucketWidth();
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

        private static void ToleranceIsRelativeToRange()
        {
            Console.WriteLine("허용 오차 — 채널 값 범위의 0.1% (기본)");

            // BIG  : 0 에서 5000 까지 오르내립니다. 범위가 5000 이라 0.1% = 5.
            //        2 만큼 벌어지는 것은 잡음이지 차이가 아닙니다.
            // SMALL: 0 과 1 만 오가는 디지털. 범위가 1 이라 0.1% = 0.001.
            //        1 만큼 벌어지는 것은 완전히 다른 상태입니다.
            // 절대값 하나로는 이 둘을 같이 다룰 수 없다는 것이 요점입니다.
            var a = new StringBuilder("Time,BIG,SMALL\n");
            var b = new StringBuilder("Time,BIG,SMALL\n");
            for (int i = 0; i < 200; i++)
            {
                double v = i * (5000.0 / 199.0);
                int d = (i / 10) % 2;
                a.Append(i).Append(',').Append(v.ToString("0.###", CultureInfo.InvariantCulture))
                 .Append(',').Append(d).Append('\n');

                double w = v + (i >= 80 && i < 120 ? 2.0 : 0.0);
                int e = (i >= 80 && i < 120) ? 1 - d : d;
                b.Append(i).Append(',').Append(w.ToString("0.###", CultureInfo.InvariantCulture))
                 .Append(',').Append(e).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("tol_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("tol_b.csv", b.ToString()), Orientation.Auto);

            var opt = new DiffOptions();
            Near("기본값은 0.1%", ToleranceRule.ToPercent(opt.RelativeTolerance), 0.1, 1e-9);

            CompareResult r = DiffEngine.Compare(dsA, dsB, opt, null);
            ChannelDiff big = r.Items.Find(d => d.Name == "BIG");
            ChannelDiff small = r.Items.Find(d => d.Name == "SMALL");
            Check("두 채널 모두 찾음", big != null && small != null, null);

            Near("BIG 의 기준값은 범위 5000 의 0.1%", big.Threshold, 5, 0.05);
            Near("SMALL 의 기준값은 범위 1 의 0.1%", small.Threshold, 0.001, 1e-9);

            Check("BIG 은 차이 아님 (2 < 5)", !big.Changed, "최대 " + big.MaxAbs.ToString("0.###"));
            Check("SMALL 은 차이 맞음 (1 > 0.001)", small.Changed, null);
            Check("차이 난 IO 는 하나", r.ChangedCount == 1, "실제 " + r.ChangedCount);

            // 기준을 0.01% 로 낮추면 BIG 도 걸립니다.
            opt.RelativeTolerance = ToleranceRule.FromPercent(0.01);
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, opt, null);
            Check("기준을 낮추면 BIG 도 차이", r2.ChangedCount == 2, "실제 " + r2.ChangedCount);

            // 절대 오차를 크게 주면 둘 중 큰 쪽이 기준이 되어 둘 다 빠집니다.
            opt.RelativeTolerance = ToleranceRule.FromPercent(0.1);
            opt.AbsoluteTolerance = 3;
            CompareResult r3 = DiffEngine.Compare(dsA, dsB, opt, null);
            Check("절대 오차가 크면 둘 중 큰 쪽이 기준", r3.ChangedCount == 0, "실제 " + r3.ChangedCount);

            // 값이 한 자리에 머문 채널도 값의 크기를 밑값으로 씁니다.
            LogDataset flatA = Open(WriteCsv("tol_flat_a.csv", "Time,F\n0,3000\n1,3000\n2,3000\n"), Orientation.Auto);
            LogDataset flatB = Open(WriteCsv("tol_flat_b.csv", "Time,F\n0,3001\n1,3001\n2,3001\n"), Orientation.Auto);
            CompareResult r4 = DiffEngine.Compare(flatA, flatB, new DiffOptions(), null);
            Check("3000 에 머물던 값이 3001 이 된 것은 잡음", r4.ChangedCount == 0,
                  "기준 " + (r4.Items.Count > 0 ? r4.Items[0].Threshold.ToString("0.###") : "?"));
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

            // 칸에 적히는 값은 "기준을 넘은 표본들의 평균" 입니다. 여기서는
            // 2 분대 60 표본이 모두 10 만큼 벌어져서 구간 전체 평균과 같습니다.
            Near("2분대 평균 차이", row.Cells[2].Mean, 10, 1e-3);
            Near("2분대 칸에 적히는 값", row.ValueOf(row.Cells[2]), 10, 1e-3);
            Near("1분대 평균 차이", row.Cells[1].Mean, 0, 1e-6);
            Check("1분대 칸에 적히는 값은 0", row.ValueOf(row.Cells[1]) == 0, null);
            Check("1분대에도 기록은 있음", row.Cells[1].HasData, null);
        }

        private static void HeatmapRelativeTolerance()
        {
            Console.WriteLine("히트맵 — 값 범위의 0.1% 는 오류로 세지 않음");

            // BIG 은 0 에서 5000 까지 올라가는 채널입니다. 범위가 5000 이므로
            // 0.1% = 5. 2 만큼 벌어지는 것은 오류가 아닙니다.
            var a = new StringBuilder("Time,BIG\n");
            var b = new StringBuilder("Time,BIG\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                double v = i * (5000.0 / 299.0);
                a.Append(t).Append(',').Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
                double w = v + (i >= 120 && i < 180 ? 2.0 : 0.0);
                b.Append(t).Append(',').Append(w.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_rel_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_rel_b.csv", b.ToString()), Orientation.Auto);

            var opt = new HeatmapOptions();
            opt.RelativeTolerance = 0.001;      // 0.1%
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("0.1% 안쪽은 차이로 세지 않음", r.Rows.Count == 0, "실제 " + r.Rows.Count);

            // 기준을 0.01% 로 낮추면 같은 2 가 차이로 잡힙니다.
            opt.RelativeTolerance = 0.0001;
            HeatmapResult r2 = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("기준을 낮추면 잡힘", r2.Rows.Count == 1, "실제 " + r2.Rows.Count);
            if (r2.Rows.Count == 1)
            {
                Check("2분대 한 칸만", r2.Rows[0].OverBuckets == 1, "실제 " + r2.Rows[0].OverBuckets);
                Near("그 칸의 평균 차이", r2.Rows[0].Cells[2].Mean, 2, 5e-3);
            }

            // 차이가 없는 IO 도 보기로 하면 줄이 나옵니다.
            opt.RelativeTolerance = 0.001;
            opt.IncludeUnchanged = true;
            HeatmapResult r3 = HeatmapBuilder.Build(dsA, dsB, opt, null);
            Check("차이 없는 IO도 보기", r3.Rows.Count == 1 && r3.Rows[0].OverBuckets == 0,
                  "줄 " + r3.Rows.Count);
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
        /// 히트맵에서 차이를 퍼센트로 보여 줄 때 나눌 밑값(HeatRow.Range)이
        /// 허용 오차와 같은 값 범위인지. 여기가 어긋나면 "허용 오차 0.1%" 와
        /// 칸에 적히는 "%" 가 서로 다른 뜻이 됩니다.
        /// </summary>
        private static void HeatmapRangeForPercent()
        {
            Console.WriteLine("히트맵 — 퍼센트로 볼 때의 밑값");

            // 0 에서 100 까지 오르는 채널. 2 분대에서만 2 만큼 벌어집니다.
            var a = new StringBuilder("Time,P\n");
            var b = new StringBuilder("Time,P\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                double v = i * (100.0 / 299.0);
                a.Append(t).Append(',').Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
                double w = v + (i >= 120 && i < 180 ? 2.0 : 0.0);
                b.Append(t).Append(',').Append(w.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_pct_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_pct_b.csv", b.ToString()), Orientation.Auto);

            var opt = new HeatmapOptions();
            opt.RelativeTolerance = 0.0001;     // 0.01% = 0.01. 2 는 차이로 잡힙니다.
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("줄 하나", r.Rows.Count == 1, "실제 " + r.Rows.Count);
            if (r.Rows.Count != 1) return;

            HeatRow row = r.Rows[0];
            Near("밑값은 값 범위", row.Range, 100, 0.5);
            Near("허용 오차와 같은 규칙",
                 row.Threshold, ToleranceRule.For(dsA.Channels[0], dsB.Channels[0], 0, 0.0001), 1e-9);
            Near("평균 차이", row.Cells[2].Mean, 2, 5e-3);

            // 칸에 적힐 퍼센트: 2 / 100 * 100 = 2%
            Near("퍼센트", row.Cells[2].Mean / row.Range * 100.0, 2, 0.02);
            Check("이름으로 견준 줄이 아님", !row.ByName, null);
        }

        /// <summary>
        /// 빨간 칸에 적히는 숫자는 <b>언제나 허용 오차보다 커야 합니다.</b>
        ///
        /// 예전에는 칸에 구간 전체의 평균을 적었습니다. 60 표본 중 하나만
        /// 5 만큼 튀면 평균은 0.083 이라, 기준 0.1 로 잡은 빨간 칸에
        /// "0.083" 이 적혔습니다. 위에 적어 둔 허용 오차와 아래 칸의 숫자가
        /// 달라 보이던 것이 이것입니다.
        /// </summary>
        private static void HeatmapCellNumberMatchesTolerance()
        {
            Console.WriteLine("히트맵 — 칸의 숫자와 허용 오차가 같은 잣대인지");

            // 0~100 채널. 2 분대의 <b>한 표본만</b> 5 만큼 벌어집니다.
            var a = new StringBuilder("Time,P\n");
            var b = new StringBuilder("Time,P\n");
            for (int i = 0; i < 300; i++)
            {
                string t = "00:" + (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
                double v = i * (100.0 / 299.0);
                a.Append(t).Append(',').Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
                double w = v + (i == 150 ? 5.0 : 0.0);
                b.Append(t).Append(',').Append(w.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("heat_one_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("heat_one_b.csv", b.ToString()), Orientation.Auto);

            var opt = new HeatmapOptions();
            opt.RelativeTolerance = 0.001;      // 0.1% → 값 범위 100 이므로 기준 0.1
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("한 표본만 튀어도 차이로 잡음", r.Rows.Count == 1, "실제 " + r.Rows.Count);
            if (r.Rows.Count != 1) return;

            HeatRow row = r.Rows[0];
            HeatCell cell = row.Cells[2];

            Near("기준", row.Threshold, 0.1, 1e-6);
            Check("넘은 표본은 하나", cell.OverSamples == 1, "실제 " + cell.OverSamples);

            // 구간 전체 평균은 기준보다 작습니다 — 그래서 이걸 적으면 안 됩니다.
            Check("전체 평균은 기준보다 작음", cell.Mean < row.Threshold,
                  "전체 평균 " + cell.Mean + ", 기준 " + row.Threshold);

            // 칸에 적히는 값은 기준보다 큽니다.
            double shown = row.ValueOf(cell);
            Near("칸에 적히는 값", shown, 5, 5e-3);
            Check("빨간 칸의 숫자는 늘 기준보다 큼", shown > row.Threshold,
                  "칸 " + shown + ", 기준 " + row.Threshold);

            // 퍼센트로 봐도 마찬가지여야 합니다 (5 / 100 = 5% > 0.1%).
            Near("칸의 퍼센트", shown / row.Range * 100.0, 5, 0.02);
            Near("기준의 퍼센트", row.ThresholdPercent, 0.1, 1e-6);
            Check("퍼센트로 봐도 칸 > 기준", shown / row.Range * 100.0 > row.ThresholdPercent, null);

            // 정상 칸은 0 입니다.
            Check("정상 칸은 0", row.ValueOf(row.Cells[0]) == 0, "실제 " + row.ValueOf(row.Cells[0]));
        }

        /// <summary>
        /// 이름으로 견주는 줄에는 허용 오차를 매기지 않습니다.
        /// 절대 오차를 1 이상으로 적어 두면, 예전에는 상태 이름이 통째로
        /// 바뀌어도 (차이 1.0 &gt; 기준 2.0 이 거짓이라) 차이로 세지 않았습니다.
        /// </summary>
        private static void HeatmapStateNamesIgnoreAbsoluteTolerance()
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
            opt.AbsoluteTolerance = 2.0;        // 차이 1.0 보다 큽니다
            opt.RelativeTolerance = 0.001;
            HeatmapResult r = HeatmapBuilder.Build(dsA, dsB, opt, null);

            Check("절대 오차를 크게 잡아도 이름 차이는 잡힘", r.Rows.Count == 1, "실제 " + r.Rows.Count);
            if (r.Rows.Count != 1) return;

            HeatRow row = r.Rows[0];
            Check("이름으로 견준 줄", row.ByName, null);
            Check("기준값 없음", row.Threshold == 0, "실제 " + row.Threshold);
            Near("퍼센트의 밑값은 1", row.Range, 1, 1e-9);

            // 2 분대는 전부 달랐으므로 100%.
            Near("다른 표본의 비율", row.ValueOf(row.Cells[2]) * 100.0, 100, 0.01);
            Check("1 분대는 정상", row.ValueOf(row.Cells[1]) == 0, null);
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

            // 가장 크게 벌어진 순간이 큰 쪽이 위입니다. WIDE 는 벌어진 표본이
            // 300 개로 훨씬 많지만, 벌어진 크기는 SPIKE 가 5 배입니다.
            Check("제일 크게 튄 IO 가 맨 위", n1 == "SPIKE,WIDE,SMALL", "실제 " + n1);

            // 차례를 정하는 값 자체가 칸 폭과 무관한지도 직접 봅니다.
            for (int i = 0; i < 3; i++)
            {
                HeatRow x = r1.Rows[i], y = r5.Rows[i];
                Near(x.Name + " 의 최대 차이가 폭과 무관", x.PeakMax, y.PeakMax, 1e-6);
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
        /// 비율 오차의 밑값은 "오르내린 폭" 과 "값의 크기" 중 <b>큰 쪽</b>이어야
        /// 합니다.
        ///
        /// 예전에는 폭만 보고, 폭이 정확히 0 일 때만 크기로 물러섰습니다.
        /// 그래서 6466 과 6467 사이만 오가는 채널은 폭이 1 이라, 허용 오차를
        /// 1% 로 잡아도 기준값이 1 x 1% = 0.01 이 되어 1 만큼의 차이가
        /// 그대로 잡혔습니다.
        /// </summary>
        private static void ToleranceUsesValueSizeNotJustSpan()
        {
            Console.WriteLine("허용 오차 — 밑값은 폭과 크기 중 큰 쪽");

            // NEAR : 6466 과 6467 사이만 오감 (폭 1, 크기 6467)
            // JUMP : 같은 자리에 있다가 6600 으로 튐
            // DIG  : 0/1 디지털 (폭 1, 크기 1)
            var a = new StringBuilder("Time,NEAR,JUMP,DIG\n");
            var b = new StringBuilder("Time,NEAR,JUMP,DIG\n");
            for (int i = 0; i < 200; i++)
            {
                int baseline = i >= 100 ? 6467 : 6466;
                a.Append(i).Append(',').Append(baseline)
                           .Append(',').Append(baseline)
                           .Append(',').Append(i % 2).Append('\n');

                // 이후 로그 쪽 값. 아래에서 쓰는 결과 변수와 이름이 겹치지
                // 않도록 v 를 붙입니다 (같은 메서드 안에서 안쪽 블록과 바깥
                // 블록이 같은 이름을 쓰면 C# 이 CS0136 으로 막습니다).
                int vNear = i >= 100 ? 6466 : 6467;          // 늘 1 만큼 벌어짐
                int vJump = i >= 150 ? 6600 : baseline;      // 크게 튐
                b.Append(i).Append(',').Append(vNear)
                           .Append(',').Append(vJump)
                           .Append(',').Append(1 - (i % 2)).Append('\n');
            }
            LogDataset dsA = Open(WriteCsv("tol_span_a.csv", a.ToString()), Orientation.Auto);
            LogDataset dsB = Open(WriteCsv("tol_span_b.csv", b.ToString()), Orientation.Auto);

            var opt = new DiffOptions();
            opt.RelativeTolerance = ToleranceRule.FromPercent(1.0);   // 1%
            CompareResult r = DiffEngine.Compare(dsA, dsB, opt, null);

            ChannelDiff near = r.Items.Find(d => d.Name == "NEAR");
            ChannelDiff jump = r.Items.Find(d => d.Name == "JUMP");
            ChannelDiff dig = r.Items.Find(d => d.Name == "DIG");
            Check("세 채널 모두 찾음", near != null && jump != null && dig != null, null);
            if (near == null || jump == null || dig == null) return;

            // 폭은 1 이지만 값이 6467 이므로 기준값은 6467 의 1%.
            Near("NEAR 의 기준값은 6467 의 1%", near.Threshold, 64.67, 0.05);
            Check("6467 과 6466 의 차이 1 은 1% 오차 안", !near.Changed,
                  "최대 " + near.MaxAbs.ToString("0.###") + ", 기준 " + near.Threshold.ToString("0.###"));

            // 그렇다고 아무것도 안 잡히면 안 됩니다. 크게 튄 것은 그대로 잡힙니다.
            Check("6600 으로 튄 것은 차이 맞음", jump.Changed,
                  "최대 " + jump.MaxAbs.ToString("0.###") + ", 기준 " + jump.Threshold.ToString("0.###"));

            // 0/1 채널은 폭도 1, 크기도 1 이라 예전과 똑같이 잡힙니다.
            Near("DIG 의 기준값은 1 의 1%", dig.Threshold, 0.01, 1e-9);
            Check("0 과 1 의 차이는 그대로 잡힘", dig.Changed, null);

            Check("차이 난 IO 는 둘", r.ChangedCount == 2, "실제 " + r.ChangedCount);

            // 좁은 폭의 작은 차이까지 봐야 하면 비율을 낮추면 됩니다.
            opt.RelativeTolerance = ToleranceRule.FromPercent(0.001);
            CompareResult r2 = DiffEngine.Compare(dsA, dsB, opt, null);
            ChannelDiff near2 = r2.Items.Find(d => d.Name == "NEAR");
            Check("비율을 낮추면 NEAR 도 잡힘", near2 != null && near2.Changed,
                  near2 != null ? "기준 " + near2.Threshold.ToString("0.######") : "못 찾음");
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
            Near("비율 허용 오차(%)", back.RelativeTolerancePercent, 0.25, 1e-9);
            Check("정렬 기준", back.SortMetric == DiffMetric.SegmentCount, back.SortMetric.ToString());
            Check("그룹 이름", back.Groups.Count == 1 && back.Groups[0].Name == "밸브 묶음", null);
            Check("그룹 구성원을 이름으로 저장", back.Groups[0].Members.Count == 2
                  && back.Groups[0].Members[0] == "밸브 OPEN", null);
        }
    }
}
