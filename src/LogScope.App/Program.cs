using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LogScope.Core;
using LogScope.Core.Settings;

namespace LogScope.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 예상 못 한 오류로 창이 그냥 사라지지 않게 합니다. 무엇이
            // 잘못됐는지 보여 주고, 다시 열 수 있게 둡니다.
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
            {
                ShowCrash(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                ShowCrash(e.ExceptionObject as Exception);
            };

            // 창을 만들기 전에 화면 배율을 읽어 둡니다. 직접 그리는 컨트롤들이
            // 만들어질 때 Dpi.S() 로 크기를 잡기 때문에, 이 값이 늦게 들어오면
            // 첫 화면이 100% 크기로 잡힙니다.
            ReadStartupDpi();

            AppSettings settings = SettingsStore.Load();
            Theme.Apply(settings.Theme);

            var state = new AppState();
            state.Settings = settings;

            using (var form = new MainForm(state))
            {
                if (args != null && args.Length > 0) form.PendingCommandLine = args;
                Application.Run(form);
            }
        }

        private static void ReadStartupDpi()
        {
            try
            {
                // 화면 DC 의 DPI. app.manifest 로 DPI 인식이 켜져 있으므로
                // 실제 배율이 그대로 들어옵니다.
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX > 0) Dpi.Scale = g.DpiX / 96f;
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException
                                      || e is System.Runtime.InteropServices.ExternalException)
            {
                Dpi.Scale = 1.0f;
            }
        }

        private static void ShowCrash(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("예상하지 못한 오류가 났습니다.");
            sb.AppendLine();
            sb.AppendLine(ex != null ? ex.Message : "(내용 없음)");
            sb.AppendLine();
            sb.AppendLine("버전 " + BuildInfo.Version + " (" + BuildInfo.Commit + ")");
            if (ex != null)
            {
                sb.AppendLine();
                string trace = ex.StackTrace ?? string.Empty;
                sb.AppendLine(trace.Length > 1500 ? trace.Substring(0, 1500) + "…" : trace);
            }
            try
            {
                MessageBox.Show(sb.ToString(), "LogScope 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidOperationException) { }
        }
    }
}
