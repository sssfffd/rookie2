using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using LogScope.Core.Compare;
using LogScope.Core.Settings;

namespace LogScope.Core.Ai
{
    /// <summary>
    /// 선택 기능: 파이썬 분석 모듈에 비교 결과를 넘기고 소견을 받아 옵니다.
    ///
    /// 기본은 꺼져 있습니다. 설정 창에서 켜고 파이썬 경로와 스크립트 경로를
    /// 직접 지정해야만 동작합니다. 프로그램에 파이썬을 함께 넣지 않고, 설치
    /// 여부도 확인하지 않습니다 — 없으면 그냥 기능이 없는 상태로 돕니다.
    ///
    /// 보안상 알아 둘 점:
    ///  - 이 기능을 켜면 지정한 파이썬 실행 파일을 자식 프로세스로 띄웁니다.
    ///    경로를 남이 바꿔 쓸 수 있는 폴더에 두지 마세요.
    ///  - 넘기는 것은 채널 이름과 숫자 요약뿐이고, 로그 파일 자체나 경로는
    ///    보내지 않습니다. 네트워크로 내보내는 코드는 여기에 없습니다.
    ///    스크립트가 무엇을 하는지는 그 스크립트를 직접 읽어 확인하세요.
    ///  - 나중에 직접 구현할 자리를 비워 둔 것이고, 지금은 이 한 파일과
    ///    ai/analyze.py 예제만 있습니다. 지우면 나머지는 그대로 돕니다.
    /// </summary>
    public static class AiBridge
    {
        public sealed class Result
        {
            public bool Ok;
            public string Summary = string.Empty;
            public string Error = string.Empty;
        }

        public static bool IsConfigured(AppSettings s)
        {
            return s != null && s.AiEnabled
                && !string.IsNullOrEmpty(s.PythonPath) && File.Exists(s.PythonPath)
                && !string.IsNullOrEmpty(s.AiScriptPath) && File.Exists(s.AiScriptPath);
        }

        /// <summary>
        /// 비교 결과 요약을 넘기고 소견 글을 받습니다. 배경 스레드에서 부르세요.
        /// timeoutMs 안에 끝나지 않으면 자식 프로세스를 끝냅니다.
        /// </summary>
        public static Result Analyze(AppSettings s, CompareResult cmp, int topN, int timeoutMs)
        {
            var r = new Result();
            if (!IsConfigured(s)) { r.Error = "AI 모듈이 설정되어 있지 않습니다."; return r; }
            if (cmp == null) { r.Error = "비교 결과가 없습니다."; return r; }

            string payload = BuildPayload(cmp, topN);

            var psi = new ProcessStartInfo(s.PythonPath);
            psi.Arguments = "\"" + s.AiScriptPath + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            try { psi.WorkingDirectory = Path.GetDirectoryName(s.AiScriptPath); }
            catch (ArgumentException) { }

            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.StandardInput.Write(payload);
                    p.StandardInput.Close();

                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();

                    if (!p.WaitForExit(timeoutMs <= 0 ? 30000 : timeoutMs))
                    {
                        try { p.Kill(); } catch (InvalidOperationException) { }
                        r.Error = "AI 모듈이 응답하지 않아 중단했습니다.";
                        return r;
                    }

                    if (p.ExitCode != 0)
                    {
                        r.Error = "AI 모듈이 오류로 끝났습니다. " + Trim(stderr, 400);
                        return r;
                    }

                    r.Ok = true;
                    r.Summary = ExtractSummary(stdout);
                    return r;
                }
            }
            catch (Exception e) when (e is IOException || e is System.ComponentModel.Win32Exception
                                      || e is InvalidOperationException)
            {
                r.Error = "AI 모듈을 실행하지 못했습니다: " + e.Message;
                return r;
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private static string ExtractSummary(string stdout)
        {
            try
            {
                Dictionary<string, object> d = Json.AsObject(Json.Parse(stdout));
                string text = Json.GetString(d, "summary", null);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch (FormatException) { }
            return Trim(stdout, 8000);
        }

        private static string BuildPayload(CompareResult cmp, int topN)
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["schema"] = "logscope.compare.v1";
            root["beforeChannels"] = (double)cmp.BeforeChannelCount;
            root["afterChannels"] = (double)cmp.AfterChannelCount;
            root["commonChannels"] = (double)cmp.CommonCount;
            root["changedChannels"] = (double)cmp.ChangedCount;

            var only1 = new List<object>();
            foreach (ChannelDiff d in cmp.OnlyBefore) only1.Add(d.Name);
            root["onlyBefore"] = only1;

            var only2 = new List<object>();
            foreach (ChannelDiff d in cmp.OnlyAfter) only2.Add(d.Name);
            root["onlyAfter"] = only2;

            var items = new List<object>();
            int n = 0;
            foreach (ChannelDiff d in cmp.Items)
            {
                if (!d.Changed) continue;
                if (topN > 0 && n++ >= topN) break;
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                o["name"] = d.Name;
                o["maxAbs"] = d.MaxAbs;
                o["meanAbs"] = d.MeanAbs;
                o["rms"] = d.Rms;
                o["area"] = d.Area;
                o["timeRatio"] = d.TimeRatio;
                o["diffSamples"] = (double)d.DiffSamples;
                o["segments"] = (double)d.Segments;
                items.Add(o);
            }
            root["changed"] = items;
            return Json.Write(root);
        }
    }
}
