using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using LogScope.Common;
using LogScope.Settings;
using LogScope.Shell;
using LogScope.Themes;

namespace LogScope
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
            sb.AppendLine("예상하지 못한 오류가 났습니다. (이 창에서 Ctrl+C 를 누르면 글 전체가 복사됩니다)");
            sb.AppendLine();

            // 오류의 <b>종류</b>와 <b>속 오류</b>까지 적습니다 (1.32). 분석 3 처럼 이름으로 찾아
            // 만든 화면이나 XAML 에서 난 오류는 겉이 TargetInvocationException ·
            // XamlParseException 이라, 겉 글만 적으면 "호출 대상이 예외를 throw했습니다"
            // 한 줄뿐이고 진짜 까닭은 속에 있습니다.
            Exception innermost = ex;
            if (ex == null)
            {
                sb.AppendLine("(내용 없음)");
            }
            else
            {
                int depth = 0;
                for (Exception x = ex; x != null && depth < 6; x = x.InnerException, depth++)
                {
                    sb.AppendLine((depth == 0 ? "" : "└ ") + x.GetType().Name + ": " + x.Message);
                    innermost = x;
                }
            }
            sb.AppendLine();
            sb.AppendLine("버전 " + BuildInfo.Version + " (" + BuildInfo.Commit + ")");

            // 자리는 맨 속 오류의 것이 진짜 난 곳입니다.
            if (innermost != null && innermost.StackTrace != null)
            {
                sb.AppendLine();
                string trace = innermost.StackTrace;
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
