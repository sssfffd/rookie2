using System;
using System.IO;
using System.Text;

namespace LogScope.Common
{
    /// <summary>
    /// 글자 파일의 인코딩을 가립니다. <b>이 저장소에서 이 일을 하는 유일한 곳입니다.</b>
    ///
    /// 전에는 세 군데에 따로 있었습니다 — 로그 CSV(CsvReader), DB 덤프
    /// (SqlDumpReader), 손으로 고치는 설정(AppConfig). 그중 <b>덤프 쪽만
    /// 틀렸습니다</b>: BOM 이 없으면 무조건 UTF-8 로 읽어서, CP949(ANSI)로
    /// 저장된 덤프의 한글이 통째로 깨졌습니다. 읽기는 성공하고 값만
    /// 쓰레기가 되므로 아무도 알려 주지 않습니다 (0.79 에서 고쳤습니다).
    ///
    /// 가리는 순서:
    ///   1. BOM 이 있으면 그대로 따릅니다 (UTF-8 · UTF-16 LE/BE)
    ///   2. 앞부분이 전부 ASCII 면 UTF-8 (어느 쪽으로 읽어도 같습니다)
    ///   3. 앞부분이 <b>올바른 UTF-8</b> 이면 UTF-8
    ///   4. 아니면 시스템 기본 코드페이지 (한국 윈도우면 CP949)
    ///
    /// 3 번이 핵심입니다. CP949 바이트열은 거의 언제나 UTF-8 로서 올바르지
    /// 않으므로, "올바른 UTF-8 인가" 한 번으로 둘을 가를 수 있습니다.
    /// </summary>
    public static class TextSniff
    {
        /// <summary>가려 보는 앞부분의 길이. 여기까지만 봅니다.</summary>
        public const int HeadBytes = 65536;

        /// <summary>
        /// 흘려 읽는 파일용. <b>읽은 자리를 되돌려 놓지 않습니다</b> —
        /// 부르는 쪽에서 <c>Position = 0</c> 으로 되돌리세요.
        /// </summary>
        public static Encoding Of(FileStream fs)
        {
            if (fs == null) return new UTF8Encoding(false);

            int want = (int)Math.Min(fs.Length, HeadBytes);
            if (want <= 0) return new UTF8Encoding(false);

            byte[] head = new byte[want];
            int n = fs.Read(head, 0, head.Length);
            return Of(head, n);
        }

        /// <summary>앞부분 바이트로 가립니다. 파일을 통째로 올려 읽는 쪽도 씁니다.</summary>
        public static Encoding Of(byte[] head, int count)
        {
            if (head == null || count <= 0) return new UTF8Encoding(false);
            if (count > head.Length) count = head.Length;

            if (count >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
                return new UTF8Encoding(true);
            if (count >= 2 && head[0] == 0xFF && head[1] == 0xFE) return Encoding.Unicode;
            if (count >= 2 && head[0] == 0xFE && head[1] == 0xFF) return Encoding.BigEndianUnicode;

            bool ascii = true;
            for (int i = 0; i < count; i++) if (head[i] >= 0x80) { ascii = false; break; }
            if (ascii) return new UTF8Encoding(false);

            return LooksUtf8(head, count) ? new UTF8Encoding(false) : Legacy();
        }

        /// <summary>앞부분이 올바른 UTF-8 인가.</summary>
        public static bool LooksUtf8(byte[] head, int count)
        {
            if (head == null || count <= 0) return true;
            if (count > head.Length) count = head.Length;

            // 마지막 글자가 잘려 있을 수 있습니다. 그것 때문에 헛되이
            // 실패하면 CP949 로 잘못 넘어갑니다 — 끝을 조금 잘라 냅니다.
            int len = count;
            while (len > 0 && (head[len - 1] & 0xC0) == 0x80) len--;
            if (len > 0) len--;
            if (len <= 0) return true;

            try
            {
                new UTF8Encoding(false, true).GetString(head, 0, len);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>
        /// CP949. 없는 환경(리눅스의 mono 등)에서는 기본 인코딩으로 물러섭니다 —
        /// 인코딩 하나 때문에 파일을 아예 못 읽으면 안 됩니다.
        /// </summary>
        public static Encoding Legacy()
        {
            try { return Encoding.GetEncoding(949); }
            catch (ArgumentException) { return Encoding.Default; }
            catch (NotSupportedException) { return Encoding.Default; }
        }
    }
}
