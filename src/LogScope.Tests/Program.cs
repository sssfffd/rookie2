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
                HeatmapBuckets();
                HeatmapRelativeTolerance();
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

            // 값이 한 자리에 머문 채널은 범위가 0 이라, 값의 크기를 대신 씁니다.
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

            // 칸에 적히는 값은 "그 구간의 절대 차이 평균" 입니다.
            // 합계로 하면 구간이 길수록 무조건 커지고, 부호를 살리면
            // +1/-1 진동이 0 으로 지워집니다.
            Near("2분대 평균 차이", row.Cells[2].Mean, 10, 1e-3);
            Near("1분대 평균 차이", row.Cells[1].Mean, 0, 1e-6);
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
