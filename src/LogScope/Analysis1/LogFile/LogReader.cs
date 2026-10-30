using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;
using LogScope.Common;

namespace LogScope.Analysis1.LogFile
{
    // 1.50: 로그 열기 — LogReader · OpenOptions.
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>로그 파일을 여는 입구. 확장자로 CSV / XLSX 를 고릅니다.</summary>
    public static class LogReader
    {
        public static readonly string[] SupportedExtensions =
            new[] { ".xlsx", ".xlsm", ".csv", ".tsv", ".txt" };

        public static string FileDialogFilter
        {
            get
            {
                return "로그 파일 (*.xlsx;*.xlsm;*.csv;*.tsv;*.txt)|*.xlsx;*.xlsm;*.csv;*.tsv;*.txt"
                     + "|엑셀 (*.xlsx;*.xlsm)|*.xlsx;*.xlsm"
                     + "|CSV / 텍스트 (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt"
                     + "|모든 파일 (*.*)|*.*";
            }
        }

        public static bool IsSupported(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            foreach (string e in SupportedExtensions)
                if (string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// 파일을 열어 데이터셋을 만듭니다. 오래 걸릴 수 있으니 배경 스레드에서
        /// 부르고, prog 로 진행 상황을 받아 창에 표시하세요.
        /// 취소하면 OperationCanceledException 이 납니다.
        /// </summary>
        public static LogDataset Open(string path, OpenOptions options, LoadProgress prog)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("경로가 비어 있습니다.", "path");
            if (!File.Exists(path)) throw new FileNotFoundException("파일이 없습니다: " + path, path);

            var sw = Stopwatch.StartNew();
            if (options == null) options = new OpenOptions();

            var info = new FileInfo(path);
            if (prog != null) prog.Report("파일을 여는 중…", 0);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            IRowSource src;
            if (ext == ".xlsx" || ext == ".xlsm")
                src = new XlsxReader(path, options.SheetName, options.MaxUncompressedBytes);
            else
                src = new CsvReader(path);

            LogDataset ds;
            try
            {
                ds = TableBuilder.Build(src, options, prog, path);
            }
            finally
            {
                src.Dispose();
            }

            // 단계를 찾습니다. 넣는 자리는 StepRead 한 곳입니다 —
            // 히트맵도 그래프도 ds.Steps 만 보므로 여기만 채우면 됩니다.
            StepRead.Fill(ds, options);
            if (ds.StepNote.Length > 0) ds.AddNote(ds.StepNote);

            ds.SourceBytes = info.Length;
            ds.LoadSeconds = sw.Elapsed.TotalSeconds;

            long cells = (long)ds.ChannelCount * ds.SampleCount;
            if (options.MaxCells > 0 && cells > options.MaxCells)
                ds.AddNote("칸 수가 설정한 상한을 넘었습니다.");

            if (prog != null) prog.Report("완료", 1);
            return ds;
        }

        /// <summary>엑셀 파일 안의 시트 이름들. CSV 면 빈 목록.</summary>
        public static List<string> ListSheets(string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            if (ext == ".xlsx" || ext == ".xlsm")
            {
                try { return XlsxReader.ListSheets(path); }
                catch (IOException) { }
                catch (InvalidDataException) { }
            }
            return new List<string>();
        }
    }

    // ======================================================================
    //  OpenOptions.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 로그를 열 때의 선택지.
    ///
    /// 상한값은 "파일 크기 상한은 없게" 라는 요구에 맞춰 기본이 모두 무제한입니다.
    /// 다만 남이 준 파일을 열 때를 위해 걸 수 있는 자리는 남겨 뒀습니다.
    /// 0 이하이면 검사하지 않습니다.
    /// </summary>
    public sealed class OpenOptions
    {
        /// <summary>Auto 면 배치를 스스로 찾습니다. Rows/Cols 면 지정한 대로 읽습니다.</summary>
        public Orientation Orientation = Orientation.Auto;

        /// <summary>배치 판별에 쓸 앞쪽 줄 수. 머리말이 길면 늘리세요.</summary>
        public int ProbeRows = 200;

        /// <summary>0 이하 = 무제한.</summary>
        public int MaxChannels = 0;
        public int MaxSamples = 0;
        public long MaxCells = 0;

        /// <summary>xlsx 압축을 푼 총량 상한. 0 이하 = 무제한.</summary>
        public long MaxUncompressedBytes = 0;

        /// <summary>상태 채널 하나가 가질 수 있는 서로 다른 문자열 수. 0 이하 = 무제한.</summary>
        public int MaxStateValues = 4096;

        /// <summary>읽을 시트 이름. 비우면 첫 번째 시트.</summary>
        public string SheetName = null;

        /// <summary>
        /// <b>단계를 담은 IO 이름.</b> 비우면 단계를 찾지 않습니다.
        ///
        /// 이 이름의 IO 값이 바뀌는 자리가 단계 경계가 됩니다. 글자
        /// 채널이면 그 글자가 단계 이름이고, 숫자면 값이 그대로 이름입니다.
        /// 로그 모양이 달라 이걸로 안 되면 <c>StepRead.Fill</c> 에 직접
        /// 넣는 자리가 있습니다.
        /// </summary>
        public string StepIo = string.Empty;

        public OpenOptions Clone()
        {
            return (OpenOptions)MemberwiseClone();
        }
    }
}
