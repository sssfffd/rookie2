using System;
using System.Collections.Generic;
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
        /// <summary>
        /// 그룹이 groups.txt 로 정해졌는지. 화면이 "여기서 고친 것은 다시 켜면
        /// 되돌아갑니다" 를 적는 데 씁니다 — 말없이 되돌아가면 고친 사람은
        /// 저장이 고장 난 줄 압니다.
        /// </summary>
        public static bool GroupsFromFile { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 예상 못 한 오류로 창이 그냥 사라지지 않게 합니다.
            DispatcherUnhandledException += OnUnhandled;
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs args)
            {
                Show(args.ExceptionObject as Exception);
            };

            // 손으로 고치는 설정 한 파일 (실행 파일 옆의 config.txt).
            // 없으면 기본값이라 여기서 따로 막을 것이 없습니다.
            AppConfig.Current = AppConfig.Load(AppConfig.ResolvePath());

            AppSettings settings = SettingsStore.Load();

            // 손으로 고치는 그룹 파일 (실행 파일 옆의 groups.txt).
            //
            // 그룹을 하나라도 정해 두면 그 파일이 주인입니다 — 설정 파일에
            // 저장된 그룹을 덮습니다. 전부 주석이면 아무것도 하지 않습니다
            // (설명만 적힌 빈 틀이 그룹을 지워 버리면 안 됩니다).
            List<GroupDef> fromFile = GroupFile.Load(GroupFile.ResolvePath());
            if (fromFile.Count > 0)
            {
                settings.Groups = fromFile;
                GroupsFromFile = true;
            }

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
                MessageBox.Show(sb.ToString(), AppConfig.Current.Name + " 오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException) { }
        }
    }
}
