using System;
using System.Threading;
using System.Windows;
using LogScope.App.Views;
using LogScope.Core.Io;

namespace LogScope.App.Services
{
    /// <summary>
    /// 오래 걸리는 일을 배경 스레드에서 돌리고, 그동안 진행 창을 띄웁니다.
    /// 읽는 중에 창이 멈춘 것처럼 보이지 않게 하는 것이 목적입니다. (요구사항 7번)
    /// </summary>
    public static class BackgroundJob
    {
        /// <summary>
        /// work 를 배경 스레드에서 돌립니다. 끝날 때까지 진행 창이 떠 있습니다.
        /// 취소하면 error 에 OperationCanceledException 이 들어옵니다.
        /// </summary>
        public static bool Run(Window owner, string title,
                               Func<LoadProgress, object> work,
                               out object result, out Exception error)
        {
            object captured = null;
            Exception failure = null;

            var dlg = new LoadingWindow(title);
            if (owner != null && owner.IsLoaded && owner.IsVisible) dlg.Owner = owner;

            var progress = new LoadProgress(dlg.Report);
            dlg.CancelRequested += progress.Cancel;

            dlg.Loaded += delegate
            {
                var thread = new Thread(delegate ()
                {
                    try { captured = work(progress); }
                    catch (Exception ex) { failure = ex; }

                    // 창을 닫는 일은 UI 스레드에서만 할 수 있습니다.
                    dlg.Dispatcher.BeginInvoke((Action)dlg.Finish);
                });
                thread.IsBackground = true;
                thread.Name = "LogScope 작업";
                thread.Start();
            };

            dlg.ShowDialog();

            result = captured;
            error = failure;
            return failure == null;
        }
    }
}
