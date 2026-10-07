using System;
using LogScope.Core.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

// ==========================================================
//  DbReadMysql
//
// 읽기 — MySQL 쪽 바이너리와 바깥 프로그램.
//
// 보통은 건드릴 일이 없는 파일입니다. 설비 DB 의 형식이 바뀌지 않는
// 한 그대로 둡니다.
//
//   FrmReader  MySQL 5.x 의 .frm 에서 표 모양 읽기 (바이트 직접)
//   DbTools    mysqldump · ibd2sdi 를 불러 쓰기 (경로를 적어야만 동작)
//
//  한 파일로 모았습니다 (0.73). 전에는 FrmReader · DbTools
//  로 나뉘어 있었습니다 — DB 화면을 고치려면 열 파일을 뒤져야 했습니다.
// ==========================================================

namespace LogScope.Db
{
    // ======================================================
    //  FrmReader
    // ======================================================

    /// <summary>
    /// MySQL 5.x 의 <c>.frm</c> 에서 <b>표의 모양</b>을 읽습니다. 값은 없습니다 —
    /// <c>.frm</c> 에는 정의만 들어 있고 값은 <c>.ibd</c> 에 있습니다.
    ///
    /// <b>왜 이게 필요한가.</b> 폴더에 <c>표.frm</c> + <c>표.ibd</c> 짝이 있으면
    /// MySQL 5.x 입니다. 그 <c>.ibd</c> 안에는 <c>ibd2sdi</c> 가 꺼낼 표
    /// 정의(SDI)가 <b>없습니다</b> — SDI 는 8.0 부터 들어간 것입니다. 그래서 5.x
    /// 폴더에서 바깥 프로그램 없이 표 모양을 아는 길은 이것뿐입니다.
    ///
    /// <b>라이브러리를 더하지 않았습니다.</b> 바이트를 직접 읽습니다. 자리값은
    /// 공개된 형식 설명을 보고 맞췄고(같은 일을 하는 도구들이 있습니다), 남의
    /// 코드를 가져오지는 않았습니다 — 그쪽은 GPL 이고 이 프로그램에 섞을 수
    /// 없습니다.
    ///
    /// <b>못 읽으면 조용히 넘기지 않습니다.</b> 판본이 다르거나 모르는 타입이
    /// 나오면 <see cref="Result.Why"/> 와 <see cref="Result.Note"/> 에 적어
    /// 화면에 내보냅니다. 표가 하나도 안 잡힌 폴더가 "차이 없음" 으로 읽히는
    /// 것이 이 프로그램에서 가장 나쁜 고장입니다.
    /// </summary>
    public static class FrmReader
    {
        /// <summary>이보다 큰 파일은 .frm 이 아니라고 봅니다 (보통 몇 KB 입니다).</summary>
        public const int MaxBytes = 8 * 1024 * 1024;

        /// <summary>표 하나를 읽은 결과.</summary>
        public sealed class Result
        {
            /// <summary>읽은 표. 못 읽었으면 null 입니다.</summary>
            public DbTable Table;

            /// <summary>못 읽은 이유. 읽었으면 빈 글자입니다.</summary>
            public string Why = string.Empty;

            /// <summary>읽었지만 덧붙일 말 (모르는 타입이 있었다 같은 것).</summary>
            public string Note = string.Empty;

            /// <summary>이 .frm 을 만든 MySQL 판 (50619 = 5.6.19). 0 이면 5.0 미만.</summary>
            public int VersionId;

            public bool Ok { get { return Table != null; } }
        }

        public static Result Read(string path)
        {
            var r = new Result();
            byte[] bytes;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) { r.Why = "파일이 없습니다."; return r; }
                if (info.Length < 64) { r.Why = "너무 짧아 .frm 이 아닙니다."; return r; }
                if (info.Length > MaxBytes)
                {
                    r.Why = "너무 큽니다 (" + (info.Length / 1024 / 1024) + "MB). .frm 이 아닌 듯합니다.";
                    return r;
                }
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                r.Why = e.Message;
                return r;
            }

            return Read(bytes, TableName(path));
        }

        /// <summary>
        /// 파일 이름이 표 이름입니다. MySQL 은 쓸 수 없는 글자를 <c>@00NN</c> 꼴로
        /// 바꿔 저장하므로 되돌립니다 (한글 표 이름이 그렇게 들어갑니다).
        /// </summary>
        public static string TableName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            return Unescape(name);
        }

        /// <summary>
        /// <c>@00NN</c> (16 진 네 자리)를 글자로 되돌립니다. MySQL 의
        /// filename-safe 인코딩입니다. 모르는 꼴은 <b>그대로 둡니다</b> —
        /// 억지로 바꾸면 이름이 달라져 "표가 없어졌다" 가 됩니다.
        /// </summary>
        public static string Unescape(string name)
        {
            if (name.IndexOf('@') < 0) return name;

            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] != '@' || i + 4 >= name.Length) { sb.Append(name[i]); continue; }

                int v = 0;
                bool ok = true;
                for (int k = 1; k <= 4; k++)
                {
                    int d = Hex(name[i + k]);
                    if (d < 0) { ok = false; break; }
                    v = (v << 4) | d;
                }
                if (!ok) { sb.Append(name[i]); continue; }

                sb.Append((char)v);
                i += 4;
            }
            return sb.ToString();
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        // ---------------- 본체 ----------------

        public static Result Read(byte[] b, string tableName)
        {
            var r = new Result();
            if (b == null || b.Length < 64) { r.Why = "너무 짧아 .frm 이 아닙니다."; return r; }

            // 뷰(.frm)는 글자 파일입니다 ("TYPE=VIEW" 로 시작). 표가 아닙니다.
            if (b[0] == 'T' && b.Length > 9 && Ascii(b, 0, 9) == "TYPE=VIEW")
            {
                r.Why = "뷰입니다 (표가 아닙니다).";
                return r;
            }
            if (b[0] != 0xFE || b[1] != 0x01)
            {
                r.Why = "머리 표식이 다릅니다 (FE 01 이 아님). .frm 이 아닌 듯합니다.";
                return r;
            }

            try
            {
                return Parse(b, tableName);
            }
            catch (FormatException e)
            {
                r.Why = e.Message;
                return r;
            }
            catch (Exception e) when (e is IndexOutOfRangeException || e is ArgumentException
                                   || e is OverflowException)
            {
                // 자리값이 어긋나면 여기로 옵니다. 판본이 다른 .frm 일 수 있습니다.
                r.Why = "모양이 어긋납니다 (" + e.GetType().Name + "). 모르는 판본의 .frm 일 수 있습니다.";
                return r;
            }
        }

        private static Result Parse(byte[] b, string tableName)
        {
            var r = new Result();
            r.VersionId = (int)U32(b, 0x33);

            // ---- 조각들의 자리 ----
            int keyOffset = U16(b, 0x06);
            int keyLength = U16(b, 0x0E);
            if (keyLength == 0xFFFF) keyLength = (int)U32(b, 0x2F);

            int namesLenHeader = U16(b, 0x04);
            int formInfo = (int)U32(b, 64 + namesLenHeader);
            Need(b, formInfo, 288, "forminfo");

            const int FormInfoSize = 288;
            int screens = U16(b, formInfo + 260);
            int columnCount = U16(b, formInfo + 258);
            int namesLength = U16(b, formInfo + 268);
            int labelsLength = U16(b, formInfo + 274);
            int commentsLength = U16(b, formInfo + 284);

            if (columnCount <= 0) throw new FormatException("열이 0 개로 적혀 있습니다.");
            if (columnCount > 4096) throw new FormatException("열이 " + columnCount + " 개로 적혀 있습니다 (너무 많습니다).");

            int metaAt = formInfo + FormInfoSize + screens;
            int metaLen = 17 * columnCount;
            Need(b, metaAt, metaLen, "열 정보");

            int namesAt = metaAt + metaLen;
            Need(b, namesAt, namesLength, "열 이름");
            int labelsAt = namesAt + namesLength;
            Need(b, labelsAt, labelsLength, "ENUM/SET 값");

            // ---- 열 이름 ----
            // 앞 한 바이트와 뒤 두 바이트를 버리고 0xFF 로 가릅니다.
            string[] names = SplitNames(b, namesAt, namesLength);
            if (names.Length != columnCount)
            {
                throw new FormatException("열 이름이 " + names.Length + " 개인데 열은 "
                                        + columnCount + " 개로 적혀 있습니다.");
            }

            // ---- ENUM / SET 의 값 목록 ----
            List<string[]> labels = SplitLabels(b, labelsAt, labelsLength);

            // ---- 열 ----
            var table = new DbTable();
            table.Name = tableName;
            table.HasRows = false;          // .frm 에는 값이 없습니다

            var unknown = new List<string>();
            var unknownCharset = new List<int>();

            for (int i = 0; i < columnCount; i++)
            {
                int at = metaAt + 17 * i;
                int length = U16(b, at + 3);
                int flags = U16(b, at + 8);
                int typeCode = b[at + 13];
                int labelId = b[at + 12];
                int charset = (b[at + 11] << 8) + b[at + 14];

                var col = new DbColumn();
                col.Name = names[i];
                col.Ordinal = i;
                col.Nullable = (flags & FlagMaybeNull) != 0;

                // 기본값은 읽지 않습니다 — "모른다" 로 둡니다. 아래 주석 참고.
                col.Default = null;
                col.DefaultKnown = false;

                string[] vals = null;
                if ((typeCode == TypeEnum || typeCode == TypeSet) && labelId >= 1
                    && labelId - 1 < labels.Count)
                {
                    vals = labels[labelId - 1];
                }

                int maxLen = CharsetMaxLen(charset);
                if (maxLen == 0) { unknownCharset.Add(charset); maxLen = 1; }

                col.Type = TypeText(typeCode, length, flags, charset, maxLen, vals, b, at);
                if (col.Type.Length == 0)
                {
                    col.Type = "(모르는 타입 " + typeCode + ")";
                    unknown.Add(col.Name + "=" + typeCode);
                }

                table.Columns.Add(col);
            }

            // ---- 기본 키 ----
            string keyWhy;
            if (keyLength > 0 && Fits(b, keyOffset, keyLength))
            {
                MarkPrimary(b, keyOffset, keyLength, table, out keyWhy);
            }
            else keyWhy = "키 정보 자리가 파일 밖을 가리킵니다.";

            // ---- 덧붙일 말 ----
            var notes = new List<string>();
            if (unknown.Count > 0)
            {
                notes.Add("모르는 타입이 " + unknown.Count + " 곳 있습니다 ("
                        + string.Join(", ", unknown.ToArray()) + "). 그 열은 타입을 견주지 못합니다.");
            }
            if (unknownCharset.Count > 0)
            {
                notes.Add("모르는 문자셋 번호 " + string.Join(", ", Strings(unknownCharset))
                        + " — 그 열의 길이가 글자 수가 아니라 바이트 수로 보일 수 있습니다.");
            }
            if (keyWhy != null && keyWhy.Length > 0) notes.Add(keyWhy);

            r.Note = string.Join(" ", notes.ToArray());
            r.Table = table;
            table.Note = ".frm 에서 모양만 읽었습니다 (값은 .frm 에 없습니다).";
            return r;
        }

        private static string[] Strings(List<int> v)
        {
            var s = new string[v.Count];
            for (int i = 0; i < v.Count; i++) s[i] = v[i].ToString();
            return s;
        }

        // ---------------- 이름과 값 목록 ----------------

        private static string[] SplitNames(byte[] b, int at, int len)
        {
            if (len <= 3) return new string[0];
            // 앞 1 바이트, 뒤 2 바이트를 버립니다.
            var list = new List<string>();
            int start = at + 1;
            int end = at + len - 2;
            int from = start;
            for (int i = start; i < end; i++)
            {
                if (b[i] != 0xFF) continue;
                list.Add(Utf8(b, from, i - from));
                from = i + 1;
            }
            if (from < end) list.Add(Utf8(b, from, end - from));
            return list.ToArray();
        }

        /// <summary>
        /// ENUM/SET 의 값 목록. 0x00 으로 묶음을 가르고, 묶음 안에서 0xFF 로
        /// 값을 가릅니다. 묶음마다 앞뒤 한 바이트를 버립니다.
        /// </summary>
        private static List<string[]> SplitLabels(byte[] b, int at, int len)
        {
            var groups = new List<string[]>();
            if (len <= 1) return groups;

            int end = at + len - 1;       // 마지막 한 바이트는 버립니다
            int from = at;
            for (int i = at; i <= end; i++)
            {
                if (i < end && b[i] != 0x00) continue;
                int gFrom = from, gTo = i;            // [gFrom, gTo)
                if (gTo - gFrom > 2) groups.Add(SplitByFF(b, gFrom + 1, gTo - 1));
                from = i + 1;
            }
            return groups;
        }

        private static string[] SplitByFF(byte[] b, int from, int to)
        {
            var list = new List<string>();
            int start = from;
            for (int i = from; i < to; i++)
            {
                if (b[i] != 0xFF) continue;
                list.Add(Utf8(b, start, i - start));
                start = i + 1;
            }
            if (start < to) list.Add(Utf8(b, start, to - start));
            return list.ToArray();
        }

        // ---------------- 기본 키 ----------------

        private const int BytesPerKey = 8;
        private const int BytesPerKeyPart = 9;
        private const int HaNoSame = 1;

        /// <summary>
        /// 키 조각을 훑어 <c>PRIMARY</c> 의 열에 표시를 답니다.
        ///
        /// 다른 키(UNIQUE · 일반 KEY)는 쓰지 않습니다. 우리가 키를 쓰는 곳은
        /// "행을 어떻게 짝지을까" 한 곳인데, 거기 쓸 수 있는 것은 기본 키뿐입니다.
        /// </summary>
        private static void MarkPrimary(byte[] b, int at, int len, DbTable table, out string why)
        {
            why = string.Empty;
            int p = at;

            int keyCount = b[p++];
            int partCount;
            if (keyCount < 128)
            {
                partCount = b[p++];
                p += 2;
            }
            else
            {
                keyCount = (keyCount & 0x7F) | (b[p++] << 7);
                partCount = U16(b, p);
                p += 2;
            }
            int extraLength = U16(b, p);
            p += 2;

            if (keyCount == 0) { why = "기본 키가 없습니다 (.frm 에 키가 0 개)."; return; }

            int extraAt = p + keyCount * BytesPerKey + partCount * BytesPerKeyPart;
            if (!Fits(b, extraAt, extraLength) || extraAt + extraLength > at + len)
            {
                why = "키 이름 자리가 어긋나 기본 키를 찾지 못했습니다.";
                return;
            }

            string[] keyNames = KeyNames(b, extraAt, extraLength);
            bool found = false;

            for (int k = 0; k < keyCount; k++)
            {
                if (p + BytesPerKey > at + len) { why = "키 정보가 중간에 끊겼습니다."; return; }

                // MySQL 은 flags 를 HA_NOSAME 과 XOR 해서 적습니다. 되돌립니다.
                // (우리는 PRIMARY 만 쓰므로 실제로는 쓰지 않지만, 자리를 맞추려면
                //  읽고 넘어가야 합니다.)
                int unusedFlags = U16(b, p) ^ HaNoSame;
                int parts = b[p + 4];
                p += BytesPerKey;

                bool primary = k < keyNames.Length
                            && string.Equals(keyNames[k], "PRIMARY", StringComparison.Ordinal);

                for (int q = 0; q < parts; q++)
                {
                    if (p + BytesPerKeyPart > at + len) { why = "키 조각이 중간에 끊겼습니다."; return; }
                    int fieldNr = U16(b, p) & 0x3FFF;
                    p += BytesPerKeyPart;

                    if (!primary) continue;
                    int ci = fieldNr - 1;
                    if (ci >= 0 && ci < table.Columns.Count)
                    {
                        table.Columns[ci].IsKey = true;
                        found = true;
                    }
                }

                // 쓰지 않는 값이라고 컴파일러에게 알려 둡니다.
                if (unusedFlags == int.MinValue) why = string.Empty;
            }

            if (!found) why = "기본 키가 없습니다. 행은 .frm 으로 못 읽으니 지금은 모양만 견줍니다.";
        }

        private static string[] KeyNames(byte[] b, int at, int len)
        {
            // 0x00 앞까지가 이름들이고, 0xFF 로 갈라집니다. 빈 것은 버립니다.
            int end = at + len;
            int stop = end;
            for (int i = at; i < end; i++) { if (b[i] == 0x00) { stop = i; break; } }

            string[] raw = SplitByFF(b, at, stop);
            var list = new List<string>();
            for (int i = 0; i < raw.Length; i++) if (raw[i].Length > 0) list.Add(raw[i]);
            return list.ToArray();
        }

        // ---------------- 타입 ----------------

        private const int FlagDecimal = 1;        // 꺼져 있으면 unsigned
        private const int FlagZerofill = 4;
        private const int FlagMaybeNull = 32768;
        private const int NotFixedDec = 31;

        private const int TypeDecimal = 0, TypeTiny = 1, TypeShort = 2, TypeLong = 3;
        private const int TypeFloat = 4, TypeDouble = 5, TypeTimestamp = 7, TypeLongLong = 8;
        private const int TypeInt24 = 9, TypeDate = 10, TypeTime = 11, TypeDateTime = 12;
        private const int TypeYear = 13, TypeNewDate = 14, TypeVarchar = 15, TypeBit = 16;
        private const int TypeTimestamp2 = 17, TypeDateTime2 = 18, TypeTime2 = 19;
        private const int TypeNewDecimal = 246, TypeEnum = 247, TypeSet = 248;
        private const int TypeTinyBlob = 249, TypeMediumBlob = 250, TypeLongBlob = 251;
        private const int TypeBlob = 252, TypeVarString = 253, TypeString = 254;
        private const int TypeGeometry = 255;

        private const int CharsetBinary = 63;
        private const int MaxTimeWidth = 10;
        private const int MaxDateTimeWidth = 19;

        /// <summary>
        /// 타입을 글자로. 모르면 <b>빈 글자</b>를 돌려줍니다 (부르는 쪽이 적습니다).
        ///
        /// <b>NOT NULL 과 AUTO_INCREMENT 는 붙이지 않습니다.</b> NULL 허용은
        /// 따로 들고 있고(<see cref="DbColumn.Nullable"/>), 그걸 타입 글자에도
        /// 넣으면 같은 것을 두 번 견주게 됩니다.
        ///
        /// <b>CHARACTER SET / COLLATE 도 붙이지 않습니다.</b> 덤프(.sql)의
        /// CREATE TABLE 은 열마다 적어 줄 때도 있고 안 적어 줄 때도 있습니다.
        /// 한쪽만 적히면 같은 열이 "타입이 달라졌다" 로 보입니다 — 없는 차이를
        /// 만드는 쪽이 더 나쁩니다.
        /// </summary>
        private static string TypeText(int code, int length, int flags, int charset,
                                       int maxLen, string[] vals, byte[] b, int at)
        {
            bool binary = charset == CharsetBinary;
            int dec = (flags >> 8) & NotFixedDec;

            switch (code)
            {
                case TypeTiny: return Number("tinyint", length, flags);
                case TypeShort: return Number("smallint", length, flags);
                case TypeInt24: return Number("mediumint", length, flags);
                case TypeLong: return Number("int", length, flags);
                case TypeLongLong: return Number("bigint", length, flags);

                case TypeDecimal:
                case TypeNewDecimal:
                    {
                        int prec = length;
                        if (dec > 0) prec--;
                        if (prec > 0) prec--;
                        return "decimal(" + prec + "," + dec + ")";
                    }

                case TypeFloat: return Real("float", length, flags, dec);
                case TypeDouble: return Real("double", length, flags, dec);

                case TypeString: return Chars(binary ? "binary" : "char", length, maxLen);
                case TypeVarchar:
                case TypeVarString: return Chars(binary ? "varbinary" : "varchar", length, maxLen);

                case TypeTinyBlob: return binary ? "tinyblob" : "tinytext";
                case TypeBlob: return binary ? "blob" : "text";
                case TypeMediumBlob: return binary ? "mediumblob" : "mediumtext";
                case TypeLongBlob: return binary ? "longblob" : "longtext";

                case TypeBit: return "bit(" + length + ")";
                case TypeYear: return "year(" + length + ")";
                case TypeDate:
                case TypeNewDate: return "date";

                case TypeTime:
                case TypeTime2: return Fraction("time", length, MaxTimeWidth);
                case TypeDateTime:
                case TypeDateTime2: return Fraction("datetime", length, MaxDateTimeWidth);
                case TypeTimestamp:
                case TypeTimestamp2: return Fraction("timestamp", length, MaxDateTimeWidth);

                case TypeEnum: return Values("enum", vals);
                case TypeSet: return Values("set", vals);

                case TypeGeometry: return Geometry(b[at + 14]);

                default: return string.Empty;
            }
        }

        private static string Number(string name, int length, int flags)
        {
            var sb = new StringBuilder(name);
            if (length > 0) sb.Append('(').Append(length).Append(')');
            if ((flags & FlagDecimal) == 0) sb.Append(" unsigned");
            if ((flags & FlagZerofill) != 0) sb.Append(" zerofill");
            return sb.ToString();
        }

        private static string Real(string name, int length, int flags, int dec)
        {
            var sb = new StringBuilder(name);
            if (dec != NotFixedDec) sb.Append('(').Append(length).Append(',').Append(dec).Append(')');
            if ((flags & FlagDecimal) == 0) sb.Append(" unsigned");
            if ((flags & FlagZerofill) != 0) sb.Append(" zerofill");
            return sb.ToString();
        }

        /// <summary>
        /// 글자 길이. <c>.frm</c> 의 length 는 <b>바이트</b>라서 문자셋의
        /// 글자당 최대 바이트로 나눠야 <c>varchar(64)</c> 가 됩니다. 안 나누면
        /// utf8 열이 <c>varchar(192)</c> 로 보입니다.
        /// </summary>
        private static string Chars(string name, int length, int maxLen)
        {
            int n = maxLen > 0 ? length / maxLen : length;
            return name + "(" + n + ")";
        }

        private static string Fraction(string name, int length, int width)
        {
            int scale = length - width - 1;
            return scale > 0 ? name + "(" + scale + ")" : name;
        }

        private static string Values(string name, string[] vals)
        {
            if (vals == null || vals.Length == 0) return name;
            var sb = new StringBuilder(name);
            sb.Append('(');
            for (int i = 0; i < vals.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('\'').Append(vals[i].Replace("'", "''")).Append('\'');
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static string Geometry(int sub)
        {
            switch (sub)
            {
                case 1: return "point";
                case 2: return "linestring";
                case 3: return "polygon";
                case 4: return "multipoint";
                case 5: return "multilinestring";
                case 6: return "multipolygon";
                case 7: return "geometrycollection";
                default: return "geometry";
            }
        }

        // ---------------- 문자셋 ----------------

        private static Dictionary<int, int> _maxLen;

        /// <summary>
        /// 문자셋 번호 → 글자당 최대 바이트. 모르면 0 을 돌려줍니다 (부르는
        /// 쪽이 1 로 두고 그렇다고 적습니다).
        ///
        /// 적어 두지 않은 번호는 1 입니다. 번호는 MySQL 이 정한 것이라 판본이
        /// 올라가며 늘어나는데, <b>양쪽 .frm 이 같은 서버에서 나왔으면 모르는
        /// 번호여도 양쪽이 똑같이 틀리므로 차이로는 보이지 않습니다.</b>
        /// </summary>
        private static int CharsetMaxLen(int id)
        {
            if (_maxLen == null)
            {
                var m = new Dictionary<int, int>();
                Fill(m, 2, new int[] { 1, 13, 19, 24, 28, 35, 84, 85, 86, 87, 88, 90, 95, 96,
                    128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
                    143, 144, 145, 146, 147, 148, 149, 150, 151, 159 });
                Fill(m, 3, new int[] { 12, 33, 83, 91, 97, 98, 192, 193, 194, 195, 196, 197,
                    198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212,
                    213, 214, 215, 223 });
                Fill(m, 4, new int[] { 45, 46, 54, 55, 56, 60, 61, 62, 101, 102, 103, 104, 105,
                    106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120,
                    121, 122, 123, 124, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170,
                    171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 224, 225,
                    226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240,
                    241, 242, 243, 244, 245, 246, 247 });
                _maxLen = m;
            }

            int v;
            if (_maxLen.TryGetValue(id, out v)) return v;
            // 1 바이트짜리는 적어 두지 않았습니다. 번호가 쓰이는 범위 안이면 1 로 봅니다.
            if (id >= 1 && id <= 255) return 1;
            return 0;
        }

        private static void Fill(Dictionary<int, int> m, int len, int[] ids)
        {
            for (int i = 0; i < ids.Length; i++) m[ids[i]] = len;
        }

        // ---------------- 바이트 읽기 ----------------
        //
        // .frm 의 숫자는 모두 작은 끝 먼저(little-endian)입니다.

        private static int U16(byte[] b, int at)
        {
            Need(b, at, 2, "2 바이트");
            return b[at] | (b[at + 1] << 8);
        }

        private static uint U32(byte[] b, int at)
        {
            Need(b, at, 4, "4 바이트");
            return (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));
        }

        private static bool Fits(byte[] b, int at, int len)
        {
            return at >= 0 && len >= 0 && (long)at + len <= b.Length;
        }

        private static void Need(byte[] b, int at, int len, string what)
        {
            if (!Fits(b, at, len))
                throw new FormatException(what + " 자리(" + at + "+" + len + ")가 파일 밖입니다 ("
                                        + b.Length + " 바이트).");
        }

        private static string Ascii(byte[] b, int at, int len)
        {
            var sb = new StringBuilder(len);
            for (int i = 0; i < len && at + i < b.Length; i++) sb.Append((char)b[at + i]);
            return sb.ToString();
        }

        /// <summary>
        /// 이름은 UTF-8 입니다. 깨진 바이트가 있어도 던지지 않습니다 — 이름 하나
        /// 때문에 표 전체를 못 읽으면 손해가 더 큽니다.
        /// </summary>
        private static string Utf8(byte[] b, int at, int len)
        {
            if (len <= 0 || !Fits(b, at, len)) return string.Empty;
            return new UTF8Encoding(false, false).GetString(b, at, len);
        }
    }

    // ======================================================
    //  DbTools
    // ======================================================

    /// <summary>
    /// <b>선택 기능</b>: 바깥 프로그램을 불러 DB 파일을 글로 바꿔 옵니다.
    /// 기본은 꺼져 있고, 설정 창의 [DB 도구] 에 경로나 명령 줄을 적어야만 돕니다.
    ///
    /// 라이브러리를 더하지 않습니다 — 이미 깔려 있는 MySQL 도구를 자식
    /// 프로세스로 띄우는 것뿐입니다. 파이썬 모듈(AiBridge)과 같은 방식입니다.
    ///
    /// <b>알아 둘 것</b>
    ///  - 적어 둔 실행 파일을 그대로 띄웁니다. 남이 바꿔 쓸 수 있는 폴더를
    ///    가리키지 마세요.
    ///  - <b>mysqldump 는 돌고 있는 서버에서 뽑습니다.</b> 멈춘 데이터
    ///    폴더(.frm/.ibd)만으로는 못 뽑습니다. 그게 MySQL 쪽 사정입니다.
    ///  - ibd2sdi 는 멈춘 .ibd 에서 <b>이름과 타입만</b> 꺼냅니다. 값은 못 꺼냅니다.
    ///  - 접속 정보는 우리가 들고 있지 않습니다. 명령 줄을 그대로 적게 둡니다.
    /// </summary>
    public static class DbTools
    {
        public const int DefaultTimeoutMs = 120000;

        public sealed class Run
        {
            public bool Ok;
            public string Output = string.Empty;
            public string Error = string.Empty;
        }

        /// <summary>
        /// 실행 파일과 인수를 정합니다.
        ///
        ///   <paramref name="exePath"/> 가 적혀 있으면 → 그것이 실행 파일,
        ///     <paramref name="dumpLine"/> 은 <b>인수</b>입니다.
        ///   비어 있으면 → <paramref name="dumpLine"/> 이 <b>명령 줄 전체</b>입니다.
        ///
        /// 둘을 섞어 짐작하지 않습니다. "경로를 적어 두었는가" 하나로 갈립니다 —
        /// 글만 보고 어느 쪽인지 알 수 있어야 합니다.
        /// </summary>
        public static bool Resolve(string exePath, string dumpLine, out string exe, out string args)
        {
            exe = string.Empty; args = string.Empty;

            string path = (exePath ?? string.Empty).Trim();
            string line = (dumpLine ?? string.Empty).Trim();

            // 적어 둔 경로에 따옴표가 붙어 있으면 떼어 줍니다.
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2).Trim();

            if (path.Length > 0)
            {
                exe = path;
                args = line;
                return exe.Length > 0;
            }

            if (line.Length == 0) return false;
            return SplitCommand(line, out exe, out args);
        }

        /// <summary>
        /// 덤프를 받아 파일로 저장합니다. 끝나면 그 파일을
        /// <see cref="SqlDumpReader"/> 로 읽으면 됩니다.
        /// </summary>
        public static Run DumpTo(string exePath, string dumpLine, string outFile, int timeoutMs)
        {
            var r = new Run();

            string exe, args;
            if (!Resolve(exePath, dumpLine, out exe, out args))
            {
                r.Error = "mysqldump 경로나 명령 줄이 비어 있습니다.";
                return r;
            }
            if (!File.Exists(exe))
            {
                r.Error = "실행 파일이 없습니다: " + exe;
                return r;
            }

            try
            {
                using (var p = Start(exe, args))
                using (var w = new StreamWriter(outFile, false, new UTF8Encoding(false)))
                {
                    var err = new StringBuilder();
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null && err.Length < 8000) err.AppendLine(e.Data);
                    };
                    p.BeginErrorReadLine();

                    char[] buf = new char[1 << 16];
                    int n;
                    while ((n = p.StandardOutput.Read(buf, 0, buf.Length)) > 0) w.Write(buf, 0, n);

                    if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs))
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        r.Error = "시간이 너무 걸려 멈췄습니다.";
                        return r;
                    }

                    if (p.ExitCode != 0)
                    {
                        r.Error = "도구가 오류로 끝났습니다 (코드 " + p.ExitCode + ")\n" + err;
                        return r;
                    }
                    r.Ok = true;
                    r.Output = outFile;
                    return r;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is System.ComponentModel.Win32Exception
                                   || e is InvalidOperationException)
            {
                r.Error = e.Message;
                return r;
            }
        }

        /// <summary>
        /// ibd2sdi 로 .ibd 에서 표 정의를 꺼냅니다. <b>이름과 타입만</b>입니다 —
        /// 행은 들어 있지 않으므로 <see cref="DbTable.HasRows"/> 는 거짓입니다.
        /// </summary>
        public static Run ReadIbd(string ibd2sdi, string ibdFile, DbSnapshot into, int timeoutMs)
        {
            var r = new Run();
            if (string.IsNullOrEmpty(ibd2sdi) || !File.Exists(ibd2sdi))
            {
                r.Error = "ibd2sdi 경로가 없습니다.";
                return r;
            }

            string json;
            try
            {
                using (var p = Start(ibd2sdi, "\"" + ibdFile + "\""))
                {
                    json = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs))
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        r.Error = "시간이 너무 걸려 멈췄습니다.";
                        return r;
                    }
                    if (p.ExitCode != 0)
                    {
                        r.Error = "ibd2sdi 가 오류로 끝났습니다 (코드 " + p.ExitCode + ")\n" + err;
                        return r;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is System.ComponentModel.Win32Exception
                                   || e is InvalidOperationException)
            {
                r.Error = e.Message;
                return r;
            }

            int added = TakeSdi(json, into, Path.GetFileName(ibdFile));
            r.Ok = added > 0;
            if (!r.Ok) r.Error = "ibd2sdi 가 낸 글에서 표 정의를 찾지 못했습니다.";
            return r;
        }

        /// <summary>
        /// ibd2sdi 가 낸 JSON 에서 표와 열 이름을 꺼냅니다.
        ///
        /// 이미 있는 Json 읽기를 그대로 씁니다 (설정 파일에 쓰는 것과 같은
        /// 것입니다). 모양이 조금 달라도 "이름" 과 "columns" 만 찾으므로,
        /// MySQL 판이 올라가도 쉽게 깨지지 않습니다.
        /// </summary>
        public static int TakeSdi(string json, DbSnapshot into, string note)
        {
            if (string.IsNullOrEmpty(json) || into == null) return 0;

            object root;
            try { root = Json.Parse(json); }
            catch (FormatException) { return 0; }

            int added = 0;
            foreach (Dictionary<string, object> obj in Objects(root))
            {
                object ddo;
                if (!obj.TryGetValue("dd_object", out ddo)) continue;
                Dictionary<string, object> dd = Json.AsObject(ddo);
                if (dd == null) continue;

                string name = Json.GetString(dd, "name", string.Empty);
                if (name.Length == 0) continue;

                List<object> cols = Json.GetArray(dd, "columns");
                if (cols.Count == 0) continue;

                var t = new DbTable();
                t.Name = name;
                t.HasRows = false;
                t.Note = note + " (ibd2sdi — 값은 읽지 못합니다)";

                foreach (object co in cols)
                {
                    Dictionary<string, object> c = Json.AsObject(co);
                    if (c == null) continue;
                    string cn = Json.GetString(c, "name", string.Empty);
                    if (cn.Length == 0) continue;
                    // InnoDB 가 스스로 넣는 숨은 열은 사용자 열이 아닙니다.
                    if (cn.StartsWith("DB_", StringComparison.Ordinal)) continue;

                    var col = new DbColumn();
                    col.Name = cn;
                    col.Type = Json.GetString(c, "column_type_utf8", string.Empty);
                    col.Nullable = Json.GetBool(c, "is_nullable", true);
                    col.Ordinal = t.Columns.Count;
                    t.Columns.Add(col);
                }

                if (t.Columns.Count == 0) continue;

                DbTable old = into.Find(t.Name);
                if (old != null) into.Tables.Remove(old);
                into.Tables.Add(t);
                added++;
            }
            return added;
        }

        /// <summary>JSON 안의 객체들을 훑습니다 (배열이 겹쳐 있어도).</summary>
        private static IEnumerable<Dictionary<string, object>> Objects(object node)
        {
            var obj = Json.AsObject(node);
            if (obj != null) { yield return obj; }

            var list = node as List<object>;
            if (list == null) yield break;
            for (int i = 0; i < list.Count; i++)
            {
                foreach (Dictionary<string, object> o in Objects(list[i])) yield return o;
            }
        }

        private static Process Start(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            try
            {
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
            }
            catch (ArgumentException) { }
            return Process.Start(psi);
        }

        /// <summary>
        /// 명령 줄을 실행 파일과 인수로 나눕니다. 따옴표로 감싼 경로를 먼저 봅니다 —
        /// 윈도우 경로에는 공백이 흔합니다.
        /// </summary>
        public static bool SplitCommand(string line, out string exe, out string args)
        {
            exe = string.Empty; args = string.Empty;
            if (line == null) return false;

            string s = line.Trim();
            if (s.Length == 0) return false;

            if (s[0] == '"')
            {
                int close = s.IndexOf('"', 1);
                if (close < 0) return false;
                exe = s.Substring(1, close - 1);
                args = s.Substring(close + 1).Trim();
                return exe.Length > 0;
            }

            int sp = s.IndexOf(' ');
            if (sp < 0) { exe = s; return true; }
            exe = s.Substring(0, sp);
            args = s.Substring(sp + 1).Trim();
            return exe.Length > 0;
        }
    }
}
