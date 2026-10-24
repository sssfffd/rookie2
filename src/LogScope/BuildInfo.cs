using System;
using System.IO;

namespace LogScope
{
    /// <summary>
    /// 버전 · 커밋 · 빌드 시각 (1.25).
    ///
    /// <b>저장소에 들어 있는 평범한 코드 파일</b>입니다. 1.24 까지는 빌드 도중에
    /// BuildInfo.g.cs 를 만들어 끼워 넣었는데, WPF 가 XAML 을 컴파일하며 따로 도는
    /// 임시 빌드 · VS 의 IntelliSense 빌드 같은 자리에서 그 파일이 빠져 "BuildInfo 가
    /// 없다" 가 났습니다. 이제 값은 <b>프로그램 안에 넣은 글 파일</b>(리소스)에서
    /// 실행할 때 읽습니다 — 빌드 순서가 어떻든 이 클래스는 늘 있습니다.
    ///
    ///   버전     저장소 루트 version.txt — csproj 가 그 파일을 그대로 리소스로 넣음
    ///   커밋 등  tools/BuildInfo.targets 가 빌드할 때 obj 에 써서 리소스로 넣음
    ///            (git 이 없거나 실패하면 "unknown" — 빌드는 멈추지 않습니다)
    ///
    /// 버전은 여전히 version.txt 한 곳에만 있습니다.
    /// </summary>
    public static class BuildInfo
    {
        public static readonly string Version;
        public static readonly string Commit;
        public static readonly bool Modified;
        public static readonly string BuiltAt;

        static BuildInfo()
        {
            Version = FirstLine(Read("LogScope.version.txt"), "0.00");

            // "커밋|수정됨(true/false)|빌드 시각" 한 줄
            string[] p = FirstLine(Read("LogScope.BuildStamp.txt"), string.Empty).Split('|');
            Commit = p.Length > 0 && p[0].Trim().Length > 0 ? p[0].Trim() : "unknown";
            Modified = p.Length > 1 && p[1].Trim() == "true";
            BuiltAt = p.Length > 2 ? p[2].Trim() : string.Empty;
        }

        private static string Read(string name)
        {
            try
            {
                using (Stream s = typeof(BuildInfo).Assembly.GetManifestResourceStream(name))
                {
                    if (s == null) return string.Empty;
                    using (var r = new StreamReader(s)) return r.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string FirstLine(string text, string fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string line = text.Replace("\r", string.Empty).Split('\n')[0].Trim().TrimStart('\uFEFF');
            return line.Length > 0 ? line : fallback;
        }
    }
}
