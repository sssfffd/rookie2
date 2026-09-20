using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using LogScope.App.Services;
using LogScope.App.Themes;
using LogScope.App.Views;
using LogScope.Core;
using LogScope.Core.Settings;

namespace LogScope.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 예상 못 한 오류로 창이 그냥 사라지지 않게 합니다.
            DispatcherUnhandledException += OnUnhandled;
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs args)
            {
                Show(args.ExceptionObject as Exception);
            };

            AppSettings settings = SettingsStore.Load();
            ThemeManager.Apply(settings.Theme);

            var state = new AppState();
            state.Settings = settings;

            var window = new ShellWindow(state, e.Args);
            MainWindow = window;
            window.Show();
        }

        private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Show(e.Exception);
            e.Handled = true;
        }

        private static void Show(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("예상하지 못한 오류가 났습니다.");
            sb.AppendLine();
            sb.AppendLine(ex != null ? ex.Message : "(내용 없음)");
            sb.AppendLine();
            sb.AppendLine("버전 " + BuildInfo.Version + " (" + BuildInfo.Commit + ")");
            if (ex != null && ex.StackTrace != null)
            {
                sb.AppendLine();
                string trace = ex.StackTrace;
                sb.AppendLine(trace.Length > 1500 ? trace.Substring(0, 1500) + "…" : trace);
            }
            try
            {
                MessageBox.Show(sb.ToString(), "LogScope 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException) { }
        }
    }
}
