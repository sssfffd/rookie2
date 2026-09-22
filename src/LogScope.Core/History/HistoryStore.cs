using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LogScope.Core.Settings;

namespace LogScope.Core.History
{
    /// <summary>
    /// 저장해 둔 분석 결과를 읽고 씁니다.
    ///
    /// <b>기록 하나에 파일 하나</b>입니다. 한 파일에 몰아 담으면 저장하다
    /// 죽었을 때 지난 기록까지 통째로 날아가고, 지우는 것도 파일 전체를
    /// 다시 써야 합니다. 파일을 나눠 두면 하나가 깨져도 나머지는 그대로 읽힙니다.
    ///
    /// 파일 이름은 저장 시각입니다 (<c>20260922-143207-881.json</c>). 이름만
    /// 봐도 차례가 서고, 같은 초에 두 번 눌러도 밀리초까지 있어 안 겹칩니다.
    /// </summary>
    public static class HistoryStore
    {
        public const string FolderName = "history";

        /// <summary>목록에 들고 있을 최대 개수. 넘으면 오래된 것부터 지웁니다.</summary>
        public const int MaxKeep = 200;

        /// <summary>설정 파일 옆의 history 폴더.</summary>
        public static string ResolveFolder()
        {
            string dir;
            try { dir = Path.GetDirectoryName(SettingsStore.ResolvePath()); }
            catch (ArgumentException) { dir = null; }
            if (string.IsNullOrEmpty(dir)) dir = ".";
            return Path.Combine(dir, FolderName);
        }

        private static string NameFor(DateTime t)
        {
            return t.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".json";
        }

        /// <summary>
        /// 한 건을 저장합니다. 저장된 파일 이름(Id)을 돌려주고, 못 쓰면
        /// problem 에 이유를 담고 빈 글자를 돌려줍니다.
        ///
        /// 설정 저장과 같은 방식입니다 — 임시 파일에 먼저 쓰고 바꿔치기해서,
        /// 쓰는 중에 죽어도 반쪽짜리 파일이 남지 않습니다.
        /// </summary>
        public static string Save(string folder, AnalysisRecord record, out string problem)
        {
            problem = string.Empty;
            if (record == null) { problem = "저장할 것이 없습니다."; return string.Empty; }

            if (record.SavedAt == DateTime.MinValue) record.SavedAt = DateTime.Now;

            try
            {
                Directory.CreateDirectory(folder);

                // 같은 밀리초에 두 번 저장하는 일은 거의 없지만, 그때 조용히
                // 덮어쓰면 한 건이 사라집니다. 비어 있는 이름을 찾습니다.
                DateTime t = record.SavedAt;
                string path = Path.Combine(folder, NameFor(t));
                for (int i = 0; i < 1000 && File.Exists(path); i++)
                {
                    t = t.AddMilliseconds(1);
                    path = Path.Combine(folder, NameFor(t));
                }
                record.SavedAt = t;

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Json.Write(record.ToJson()), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);

                record.Id = Path.GetFileName(path);
                return record.Id;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is NotSupportedException || e is ArgumentException)
            {
                problem = e.Message;
                return string.Empty;
            }
        }

        /// <summary>
        /// 저장된 것을 모두 읽습니다. <b>최근 것이 앞</b>입니다.
        /// 읽다 깨진 파일이 나오면 그것만 건너뜁니다 — 하나 때문에 나머지를
        /// 못 보면 안 됩니다.
        /// </summary>
        public static List<AnalysisRecord> Load(string folder)
        {
            var list = new List<AnalysisRecord>();
            string[] files;
            try
            {
                if (!Directory.Exists(folder)) return list;
                files = Directory.GetFiles(folder, "*.json");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return list;
            }

            Array.Sort(files, StringComparer.Ordinal);

            for (int i = files.Length - 1; i >= 0; i--)   // 이름이 곧 시각이라 뒤에서부터가 최근
            {
                AnalysisRecord r;
                try { r = AnalysisRecord.FromJson(Json.Parse(File.ReadAllText(files[i]))); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                       || e is FormatException)
                {
                    continue;
                }
                if (r == null) continue;
                r.Id = Path.GetFileName(files[i]);
                list.Add(r);
                if (list.Count >= MaxKeep) break;
            }
            return list;
        }

        /// <summary>한 건을 지웁니다. 없으면 아무 일도 안 일어납니다.</summary>
        public static bool Delete(string folder, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            // 파일 이름만 받습니다. 경로가 섞여 들어오면 엉뚱한 곳을 지울 수 있습니다.
            if (id.IndexOf(Path.DirectorySeparatorChar) >= 0
             || id.IndexOf(Path.AltDirectorySeparatorChar) >= 0
             || id.IndexOf("..", StringComparison.Ordinal) >= 0) return false;

            try
            {
                string path = Path.Combine(folder, id);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                   || e is ArgumentException || e is NotSupportedException)
            {
                return false;
            }
        }
    }
}
