using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LogScope.Tests
{
    /// <summary>
    /// 테스트용 최소 xlsx 작성기. 읽기 쪽을 진짜 엑셀 파일로 확인하기 위해
    /// 있습니다. 프로그램 본체에는 쓰이지 않습니다.
    /// </summary>
    public static class XlsxWriter
    {
        public static void Write(string path, List<object[]> rows)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Put(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "</Types>");

                Put(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                Put(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"로그\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

                Put(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "</Relationships>");

                Put(zip, "xl/worksheets/sheet1.xml", Sheet(rows));
            }
        }

        private static string Sheet(List<object[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                object[] row = rows[r];
                sb.Append("<row r=\"").Append(r + 1).Append("\">");
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] == null) continue;
                    string refs = Col(c) + (r + 1);
                    if (row[c] is string)
                    {
                        sb.Append("<c r=\"").Append(refs).Append("\" t=\"inlineStr\"><is><t>")
                          .Append(Escape((string)row[c])).Append("</t></is></c>");
                    }
                    else
                    {
                        double d = Convert.ToDouble(row[c], CultureInfo.InvariantCulture);
                        sb.Append("<c r=\"").Append(refs).Append("\"><v>")
                          .Append(d.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string Col(int index)
        {
            string s = string.Empty;
            index++;
            while (index > 0) { int rem = (index - 1) % 26; s = (char)('A' + rem) + s; index = (index - 1) / 26; }
            return s;
        }

        private static string Escape(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void Put(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream s = e.Open())
            using (var w = new StreamWriter(s, new UTF8Encoding(false)))
                w.Write(content);
        }
    }
}
