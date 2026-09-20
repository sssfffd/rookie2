using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LogScope.Core.Model;

namespace LogScope.Core.Io
{
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
}
