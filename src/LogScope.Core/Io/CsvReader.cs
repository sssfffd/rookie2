using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogScope.Core.Io
{
    /// <summary>
    /// CSV / TSV 를 한 줄씩 흘려보냅니다. 파일 전체를 문자열로 올리지 않고
    /// StreamReader 로 흘려 읽으므로 몇 백 MB 짜리도 열립니다.
    /// 구분자와 인코딩은 앞부분을 보고 스스로 정합니다.
    /// </summary>
    public sealed class CsvReader : IRowSource
    {
        private readonly FileStream _file;
        private readonly StreamReader _reader;
        private readonly char _delim;
        private readonly long _length;
        private readonly List<Cell> _row = new List<Cell>(256);
        private Cell[] _buffer = new Cell[256];
        private long _rows;

        public char Delimiter { get { return _delim; } }
        public Encoding DetectedEncoding { get; private set; }

        public CsvReader(string path)
        {
            _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            _length = _file.Length;

            Encoding enc = SniffEncoding(_file);
            DetectedEncoding = enc;
            _file.Position = 0;
            _reader = new StreamReader(_file, enc, true, 1 << 16);

            _delim = SniffDelimiter(path, _file, enc);
        }

        /// <summary>
        /// BOM 이 있으면 그대로 따르고, 없으면 앞부분이 올바른 UTF-8 인지 봅니다.
        /// 아니면 시스템 기본 코드페이지(한국 윈도우면 CP949)로 읽습니다.
        /// 예전 장비가 뽑은 로그는 대개 이 둘 중 하나입니다.
        /// </summary>
        private static Encoding SniffEncoding(FileStream fs)
        {
            byte[] head = new byte[Math.Min(65536, (int)Math.Min(fs.Length, 65536))];
            int n = fs.Read(head, 0, head.Length);
            if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new UTF8Encoding(true);
            if (n >= 2 && head[0] == 0xFF && head[1] == 0xFE) return Encoding.Unicode;
            if (n >= 2 && head[0] == 0xFE && head[1] == 0xFF) return Encoding.BigEndianUnicode;

            bool ascii = true;
            for (int i = 0; i < n; i++) if (head[i] >= 0x80) { ascii = false; break; }
            if (ascii) return new UTF8Encoding(false);

            try
            {
                // 잘린 마지막 글자 때문에 헛되이 실패하지 않도록 끝을 조금 잘라 냅니다.
                int len = n;
                while (len > 0 && (head[len - 1] & 0xC0) == 0x80) len--;
                if (len > 0) len--;
                new UTF8Encoding(false, true).GetString(head, 0, Math.Max(0, len));
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default;
            }
        }

        private static char SniffDelimiter(string path, FileStream fs, Encoding enc)
        {
            string ext = Path.GetExtension(path);
            if (string.Equals(ext, ".tsv", StringComparison.OrdinalIgnoreCase)) return '\t';

            fs.Position = 0;
            var sr = new StreamReader(fs, enc, true, 1 << 14);
            int comma = 0, tab = 0, semi = 0, pipe = 0, lines = 0;
            string line;
            while (lines < 30 && (line = sr.ReadLine()) != null)
            {
                lines++;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ',') comma++;
                    else if (c == '\t') tab++;
                    else if (c == ';') semi++;
                    else if (c == '|') pipe++;
                }
            }
            fs.Position = 0;

            char best = ','; int bestCount = comma;
            if (tab > bestCount) { best = '\t'; bestCount = tab; }
            if (semi > bestCount) { best = ';'; bestCount = semi; }
            if (pipe > bestCount) { best = '|'; bestCount = pipe; }
            return bestCount == 0 ? ',' : best;
        }

        public double Progress
        {
            get
            {
                if (_length <= 0) return -1;
                try { return Math.Min(1.0, (double)_file.Position / _length); }
                catch (ObjectDisposedException) { return -1; }
            }
        }

        public long RowsRead { get { return _rows; } }

        public bool NextRow(out Cell[] cells, out int count)
        {
            cells = _buffer; count = 0;
            string line = ReadLogicalLine();
            if (line == null) return false;
            _rows++;

            _row.Clear();
            SplitLine(line, _delim, _row);

            if (_row.Count > _buffer.Length)
            {
                int cap = _buffer.Length;
                while (cap < _row.Count) cap *= 2;
                _buffer = new Cell[cap];
            }
            for (int i = 0; i < _row.Count; i++) _buffer[i] = _row[i];
            cells = _buffer;
            count = _row.Count;
            return true;
        }

        // 따옴표 안에 줄바꿈이 들어 있으면 여러 물리 줄이 한 논리 줄입니다.
        private readonly StringBuilder _pending = new StringBuilder();
        private string ReadLogicalLine()
        {
            _pending.Length = 0;
            while (true)
            {
                string line = _reader.ReadLine();
                if (line == null) return _pending.Length > 0 ? _pending.ToString() : null;
                if (_pending.Length > 0) _pending.Append('\n');
                _pending.Append(line);
                if (QuotesBalanced(_pending)) return _pending.ToString();
                if (_pending.Length > (1 << 22)) return _pending.ToString();  // 망가진 따옴표 방어
            }
        }

        private static bool QuotesBalanced(StringBuilder sb)
        {
            int q = 0;
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '"') q++;
            return (q & 1) == 0;
        }

        private static void SplitLine(string line, char delim, List<Cell> outCells)
        {
            int i = 0, n = line.Length;
            var sb = new StringBuilder(32);
            while (true)
            {
                sb.Length = 0;
                bool quoted = false;
                if (i < n && line[i] == '"') { quoted = true; i++; }

                if (quoted)
                {
                    while (i < n)
                    {
                        char c = line[i];
                        if (c == '"')
                        {
                            if (i + 1 < n && line[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                            i++; break;
                        }
                        sb.Append(c); i++;
                    }
                    while (i < n && line[i] != delim) i++;
                }
                else
                {
                    while (i < n && line[i] != delim) { sb.Append(line[i]); i++; }
                }

                outCells.Add(Classify(sb.ToString()));

                if (i >= n) break;
                i++;   // 구분자 건너뛰기
                if (i == n) { outCells.Add(Cell.Empty); break; }   // 줄 끝의 구분자 = 빈 칸 하나
            }
        }

        /// <summary>글자를 보고 빈 칸 / 참거짓 / 숫자 / 글자로 나눕니다.</summary>
        public static Cell Classify(string s)
        {
            if (s == null) return Cell.Empty;
            s = s.Trim();
            if (s.Length == 0) return Cell.Empty;

            bool b;
            if (ValueParse.TryBool(s, out b)) return Cell.FromBool(b);

            double d;
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out d))
                return Cell.FromNumber(d);

            return Cell.FromText(s);
        }

        public void Dispose()
        {
            try { _reader.Dispose(); } catch (ObjectDisposedException) { }
            try { _file.Dispose(); } catch (ObjectDisposedException) { }
        }
    }
}
