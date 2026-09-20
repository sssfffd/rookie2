using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 설정 파일을 읽고 씁니다.
    ///
    /// 저장 위치는 두 곳을 봅니다.
    ///  1) 실행 파일 옆의 LogScope.settings.json — USB 나 공유 폴더에 복사해
    ///     들고 다닐 때 설정도 같이 따라옵니다. 쓸 수 있으면 여기를 씁니다.
    ///  2) 쓸 수 없으면(Program Files 등) %APPDATA%\LogScope\settings.json
    ///
    /// 쓸 때는 임시 파일에 먼저 쓰고 바꿔치기합니다. 저장 중에 프로그램이
    /// 죽어도 기존 설정이 반쪽짜리로 남지 않습니다.
    /// </summary>
    public static class SettingsStore
    {
        public const string FileName = "LogScope.settings.json";

        private static string _resolved;

        public static string ResolvePath()
        {
            if (_resolved != null) return _resolved;

            string beside = null;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    beside = Path.Combine(dir, FileName);
                    if (CanWriteTo(dir)) { _resolved = beside; return _resolved; }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
            }

            try
            {
                string appdata = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogScope");
                Directory.CreateDirectory(appdata);
                _resolved = Path.Combine(appdata, "settings.json");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _resolved = beside ?? FileName;
            }
            return _resolved;
        }

        private static bool CanWriteTo(string dir)
        {
            string probe = Path.Combine(dir, ".logscope-write-test");
            try
            {
                using (var fs = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                    fs.WriteByte(0);
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static AppSettings Load()
        {
            string path = ResolvePath();
            try
            {
                if (!File.Exists(path)) return new AppSettings();
                string text = File.ReadAllText(path, Encoding.UTF8);
                return AppSettings.FromJson(Json.Parse(text));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException)
            {
                // 설정이 깨졌다고 프로그램이 안 열리면 안 됩니다. 기본값으로 갑니다.
                return new AppSettings();
            }
        }

        /// <summary>저장에 성공하면 true. 실패해도 예외를 던지지 않습니다.</summary>
        public static bool Save(AppSettings settings)
        {
            if (settings == null) return false;
            string path = ResolvePath();
            string temp = path + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(temp, Json.Write(settings.ToJson()), new UTF8Encoding(false));

                if (File.Exists(path))
                {
                    // Replace 는 같은 볼륨에서만 됩니다. 안 되면 지우고 옮깁니다.
                    try { File.Replace(temp, path, null); return true; }
                    catch (Exception e) when (e is IOException || e is PlatformNotSupportedException)
                    {
                        File.Delete(path);
                    }
                }
                File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
                return false;
            }
        }
    }
}
