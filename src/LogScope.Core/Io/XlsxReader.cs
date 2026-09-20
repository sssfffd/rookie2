using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace LogScope.Core.Io
{
    /// <summary>
    /// .xlsx / .xlsm 를 한 줄씩 흘려보냅니다.
    ///
    /// 바깥 라이브러리를 하나도 쓰지 않습니다. xlsx 는 zip 안에 든 XML 이고,
    /// zip 은 System.IO.Compression, XML 은 System.Xml 로 .NET 에 이미 있습니다.
    /// (C++ 때는 zip 해제를 직접 구현해야 했던 부분입니다.)
    ///
    /// 시트 XML 은 XmlReader 로 앞에서부터 흘려 읽으므로, 시트가 몇 백 MB 라도
    /// 메모리에 올라가는 것은 지금 읽는 한 줄과 공유 문자열 표뿐입니다.
    /// </summary>
    public sealed class XlsxReader : IRowSource
    {
        private readonly ZipArchive _zip;
        private readonly FileStream _file;
        private readonly Stream _sheetStream;
        private readonly XmlReader _xml;

        private string[] _shared = new string[0];
        private bool[] _dateStyle = new bool[0];

        private Cell[] _buffer = new Cell[256];
        private long _rows;
        private int _totalRows;          // dimension 에서 읽은 예상 줄 수. 없으면 0.
        private int _maxWidth;           // 지금까지 본 가장 넓은 줄. 버퍼를 지울 범위.
        private bool _atEnd;

        public string SheetName { get; private set; }
        public List<string> SheetNames { get; private set; }

        public XlsxReader(string path, string wantedSheet, long maxUncompressedBytes)
        {
            _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            try
            {
                _zip = new ZipArchive(_file, ZipArchiveMode.Read, false);
            }
            catch (InvalidDataException e)
            {
                _file.Dispose();
                throw new InvalidDataException(
                    "엑셀 파일로 열리지 않습니다. .xls (예전 형식) 이면 엑셀에서 .xlsx 로 저장한 뒤 다시 열어 주세요.", e);
            }

            if (maxUncompressedBytes > 0)
            {
                long total = 0;
                foreach (ZipArchiveEntry e in _zip.Entries) total += e.Length;
                if (total > maxUncompressedBytes)
                {
                    Dispose();
                    throw new InvalidDataException("압축을 푼 크기가 설정한 상한을 넘습니다.");
                }
            }

            LoadSharedStrings();
            LoadStyles();

            string sheetPath = ResolveSheet(wantedSheet);
            ZipArchiveEntry sheet = FindEntry(sheetPath);
            if (sheet == null)
            {
                Dispose();
                throw new InvalidDataException("시트를 찾지 못했습니다: " + sheetPath);
            }

            _sheetStream = sheet.Open();
            _xml = XmlReader.Create(_sheetStream, SafeSettings());
            SeekToSheetData();
        }

        /// <summary>
        /// 바깥에서 온 XML 을 읽을 때의 안전 설정. DTD 를 끄면 외부 실체 참조
        /// (XXE) 와 실체 폭탄을 막을 수 있습니다.
        /// </summary>
        private static XmlReaderSettings SafeSettings()
        {
            var s = new XmlReaderSettings();
            s.DtdProcessing = DtdProcessing.Prohibit;
            s.XmlResolver = null;
            s.IgnoreComments = true;
            s.IgnoreProcessingInstructions = true;
            s.IgnoreWhitespace = true;
            s.CloseInput = true;
            return s;
        }

        private ZipArchiveEntry FindEntry(string name)
        {
            foreach (ZipArchiveEntry e in _zip.Entries)
                if (string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        // ---- 공유 문자열 --------------------------------------------------
        private void LoadSharedStrings()
        {
            ZipArchiveEntry e = FindEntry("xl/sharedStrings.xml");
            if (e == null) return;

            var list = new List<string>(4096);
            using (Stream s = e.Open())
            using (XmlReader r = XmlReader.Create(s, SafeSettings()))
            {
                var sb = new System.Text.StringBuilder();
                bool inItem = false, skipRuby = false;

                // ReadElementContentAsString 은 </t> 다음 노드까지 읽어 버립니다.
                // 그 다음 노드가 </si> 인 경우가 흔하므로, 한 번 더 Read() 하면
                // 문자열이 통째로 사라집니다. advance 로 그걸 막습니다.
                bool advance = true;
                while (true)
                {
                    if (advance) { if (!r.Read()) break; }
                    advance = true;

                    if (r.NodeType == XmlNodeType.Element)
                    {
                        if (r.LocalName == "si") { inItem = true; sb.Length = 0; skipRuby = false; }
                        else if (inItem && (r.LocalName == "rPh" || r.LocalName == "phoneticPr")) skipRuby = true;
                        else if (inItem && r.LocalName == "t" && !skipRuby && !r.IsEmptyElement)
                        {
                            sb.Append(r.ReadElementContentAsString());
                            advance = false;
                        }
                    }
                    else if (r.NodeType == XmlNodeType.EndElement)
                    {
                        if (r.LocalName == "si") { list.Add(sb.ToString()); inItem = false; }
                        else if (r.LocalName == "rPh") skipRuby = false;
                    }
                }
            }
            _shared = list.ToArray();
        }

        // ---- 서식 (어느 칸이 날짜인지) -------------------------------------
        private void LoadStyles()
        {
            ZipArchiveEntry e = FindEntry("xl/styles.xml");
            if (e == null) return;

            var custom = new Dictionary<int, string>();
            var xfDate = new List<bool>(64);

            using (Stream s = e.Open())
            using (XmlReader r = XmlReader.Create(s, SafeSettings()))
            {
                bool inCellXfs = false;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element)
                    {
                        if (r.LocalName == "numFmt")
                        {
                            string id = r.GetAttribute("numFmtId");
                            string code = r.GetAttribute("formatCode");
                            int idv;
                            if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out idv))
                                custom[idv] = code ?? string.Empty;
                        }
                        else if (r.LocalName == "cellXfs") inCellXfs = true;
                        else if (inCellXfs && r.LocalName == "xf")
                        {
                            string id = r.GetAttribute("numFmtId");
                            int idv;
                            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out idv)) idv = 0;
                            string code;
                            if (!custom.TryGetValue(idv, out code)) code = null;
                            xfDate.Add(ValueParse.IsDateFormat(idv, code));
                        }
                    }
                    else if (r.NodeType == XmlNodeType.EndElement && r.LocalName == "cellXfs") inCellXfs = false;
                }
            }
            _dateStyle = xfDate.ToArray();
        }

        // ---- 시트 고르기 ---------------------------------------------------
        private string ResolveSheet(string wantedSheet)
        {
            SheetNames = new List<string>();
            var relTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ZipArchiveEntry rels = FindEntry("xl/_rels/workbook.xml.rels");
            if (rels != null)
            {
                using (Stream s = rels.Open())
                using (XmlReader r = XmlReader.Create(s, SafeSettings()))
                {
                    while (r.Read())
                    {
                        if (r.NodeType != XmlNodeType.Element || r.LocalName != "Relationship") continue;
                        string id = r.GetAttribute("Id");
                        string target = r.GetAttribute("Target");
                        if (id != null && target != null) relTarget[id] = target;
                    }
                }
            }

            var order = new List<string>();   // 통합 문서에 적힌 순서대로의 시트 경로
            ZipArchiveEntry wb = FindEntry("xl/workbook.xml");
            if (wb != null)
            {
                using (Stream s = wb.Open())
                using (XmlReader r = XmlReader.Create(s, SafeSettings()))
                {
                    while (r.Read())
                    {
                        if (r.NodeType != XmlNodeType.Element || r.LocalName != "sheet") continue;
                        string name = r.GetAttribute("name") ?? ("Sheet" + (SheetNames.Count + 1));
                        string rid = r.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                        if (rid == null) rid = r.GetAttribute("r:id");
                        string target;
                        if (rid == null || !relTarget.TryGetValue(rid, out target)) target = null;
                        SheetNames.Add(name);
                        order.Add(target == null ? null : NormalizeTarget(target));
                    }
                }
            }

            int pick = 0;
            if (!string.IsNullOrEmpty(wantedSheet))
            {
                int found = SheetNames.FindIndex(n => string.Equals(n, wantedSheet, StringComparison.OrdinalIgnoreCase));
                if (found >= 0) pick = found;
            }

            if (pick < order.Count && order[pick] != null)
            {
                SheetName = SheetNames[pick];
                return order[pick];
            }

            SheetName = SheetNames.Count > pick ? SheetNames[pick] : "Sheet1";
            return "xl/worksheets/sheet1.xml";
        }

        private static string NormalizeTarget(string target)
        {
            if (target.StartsWith("/", StringComparison.Ordinal)) return target.Substring(1);
            if (target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return target;
            return "xl/" + target;
        }

        // ---- 줄 읽기 -------------------------------------------------------
        private void SeekToSheetData()
        {
            while (_xml.Read())
            {
                if (_xml.NodeType != XmlNodeType.Element) continue;
                if (_xml.LocalName == "dimension")
                {
                    string refs = _xml.GetAttribute("ref");
                    _totalRows = RowsInDimension(refs);
                }
                else if (_xml.LocalName == "sheetData")
                {
                    if (_xml.IsEmptyElement) _atEnd = true;
                    return;
                }
            }
            _atEnd = true;
        }

        private static int RowsInDimension(string refs)
        {
            if (string.IsNullOrEmpty(refs)) return 0;
            int colon = refs.IndexOf(':');
            if (colon < 0) return 0;
            int row = RowOfRef(refs.Substring(colon + 1));
            return row > 0 ? row : 0;
        }

        private static int RowOfRef(string cellRef)
        {
            int i = 0;
            while (i < cellRef.Length && !char.IsDigit(cellRef[i])) i++;
            int v = 0;
            for (; i < cellRef.Length && char.IsDigit(cellRef[i]); i++) v = v * 10 + (cellRef[i] - '0');
            return v;
        }

        /// <summary>"BC" -> 54 (0 기반).</summary>
        public static int ColumnOfRef(string cellRef)
        {
            int v = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = cellRef[i];
                if (c >= 'A' && c <= 'Z') v = v * 26 + (c - 'A' + 1);
                else if (c >= 'a' && c <= 'z') v = v * 26 + (c - 'a' + 1);
                else break;
            }
            return v - 1;
        }

        public double Progress
        {
            get
            {
                if (_totalRows > 0) return Math.Min(1.0, (double)_rows / _totalRows);
                return -1;
            }
        }

        public long RowsRead { get { return _rows; } }

        /// <summary>
        /// 다음 줄. 엑셀은 빈 줄과 빈 칸을 아예 적지 않으므로, r 속성에 적힌
        /// 자리대로 되돌려 놓아야 열 번호가 어긋나지 않습니다.
        /// 완전히 빈 줄은 건너뛰지 않고 빈 줄 그대로 돌려줍니다 — 그래야
        /// 0 이 이어지는 구간이나 시간만 적힌 줄이 사라지지 않습니다.
        /// </summary>
        public bool NextRow(out Cell[] cells, out int count)
        {
            cells = _buffer; count = 0;
            if (_atEnd) return false;

            while (_xml.Read())
            {
                if (_xml.NodeType == XmlNodeType.EndElement && _xml.LocalName == "sheetData") { _atEnd = true; return false; }
                if (_xml.NodeType != XmlNodeType.Element || _xml.LocalName != "row") continue;

                _rows++;
                // 엑셀은 빈 칸을 아예 적지 않습니다. 버퍼를 다시 쓰므로 지난 줄의
                // 값이 남아 있으면 안 됩니다.
                if (_maxWidth > 0) Array.Clear(_buffer, 0, Math.Min(_maxWidth, _buffer.Length));
                bool empty = _xml.IsEmptyElement;
                int width = 0;
                if (!empty)
                {
                    // <row> 안의 <c> 들을 읽습니다.
                    int depth = _xml.Depth;
                    while (_xml.Read())
                    {
                        if (_xml.NodeType == XmlNodeType.EndElement && _xml.Depth == depth && _xml.LocalName == "row") break;
                        if (_xml.NodeType != XmlNodeType.Element || _xml.LocalName != "c") continue;

                        string r = _xml.GetAttribute("r");
                        string t = _xml.GetAttribute("t");
                        string sAttr = _xml.GetAttribute("s");
                        int col = string.IsNullOrEmpty(r) ? width : ColumnOfRef(r);
                        if (col < 0) col = width;

                        Cell cell = ReadCellValue(t, sAttr);
                        Put(col, cell);
                        if (col + 1 > width) width = col + 1;
                    }
                }
                if (width > _maxWidth) _maxWidth = width;
                cells = _buffer;
                count = width;
                return true;
            }
            _atEnd = true;
            return false;
        }

        private void Put(int col, Cell c)
        {
            if (col >= _buffer.Length)
            {
                int cap = _buffer.Length;
                while (cap <= col) cap *= 2;
                Cell[] nb = new Cell[cap];
                Array.Copy(_buffer, nb, _buffer.Length);
                _buffer = nb;
            }
            _buffer[col] = c;
        }

        /// <summary>&lt;c&gt; 하나를 읽습니다. 읽고 나면 리더는 &lt;/c&gt; 위에 있습니다.</summary>
        private Cell ReadCellValue(string t, string styleIndex)
        {
            if (_xml.IsEmptyElement) return Cell.Empty;

            int depth = _xml.Depth;
            string text = null;
            bool inline = false;
            var sb = new System.Text.StringBuilder();

            bool advance = true;
            while (true)
            {
                if (advance) { if (!_xml.Read()) break; }
                advance = true;

                if (_xml.NodeType == XmlNodeType.EndElement && _xml.Depth == depth && _xml.LocalName == "c") break;
                if (_xml.NodeType == XmlNodeType.Element)
                {
                    if (_xml.LocalName == "v" && !_xml.IsEmptyElement)
                    {
                        text = _xml.ReadElementContentAsString();
                        advance = false;
                    }
                    else if (_xml.LocalName == "is") inline = true;
                    else if (inline && _xml.LocalName == "t" && !_xml.IsEmptyElement)
                    {
                        sb.Append(_xml.ReadElementContentAsString());
                        advance = false;
                    }
                }
            }
            if (inline) text = sb.ToString();

            if (text == null || text.Length == 0) return Cell.Empty;

            switch (t)
            {
                case "s":
                    {
                        int idx;
                        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx)
                            && idx >= 0 && idx < _shared.Length)
                            return Cell.FromText(_shared[idx].Trim());
                        return Cell.Empty;
                    }
                case "b":
                    return Cell.FromBool(text != "0");
                case "e":
                    return Cell.Empty;          // #N/A 같은 오류 칸은 빈 칸으로 봅니다.
                case "str":
                case "inlineStr":
                    return ClassifyText(text);
                default:
                    {
                        double d;
                        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                            return ClassifyText(text);
                        if (IsDateStyle(styleIndex)) return Cell.FromDateSerial(d);
                        return Cell.FromNumber(d);
                    }
            }
        }

        private static Cell ClassifyText(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return Cell.Empty;
            bool b;
            if (ValueParse.TryBool(s, out b)) return Cell.FromBool(b);
            return Cell.FromText(s);
        }

        private bool IsDateStyle(string styleIndex)
        {
            if (string.IsNullOrEmpty(styleIndex)) return false;
            int i;
            if (!int.TryParse(styleIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return false;
            return i >= 0 && i < _dateStyle.Length && _dateStyle[i];
        }

        public void Dispose()
        {
            try { if (_xml != null) _xml.Dispose(); } catch (ObjectDisposedException) { }
            try { if (_sheetStream != null) _sheetStream.Dispose(); } catch (ObjectDisposedException) { }
            try { if (_zip != null) _zip.Dispose(); } catch (ObjectDisposedException) { }
            try { if (_file != null) _file.Dispose(); } catch (ObjectDisposedException) { }
        }

        /// <summary>파일을 열지 않고 시트 이름만 훑어봅니다.</summary>
        public static List<string> ListSheets(string path)
        {
            var names = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (!string.Equals(e.FullName, "xl/workbook.xml", StringComparison.OrdinalIgnoreCase)) continue;
                    using (Stream s = e.Open())
                    using (XmlReader r = XmlReader.Create(s, SafeSettings()))
                    {
                        while (r.Read())
                            if (r.NodeType == XmlNodeType.Element && r.LocalName == "sheet")
                                names.Add(r.GetAttribute("name") ?? ("Sheet" + (names.Count + 1)));
                    }
                    break;
                }
            }
            return names;
        }
    }
}
