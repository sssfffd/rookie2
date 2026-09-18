using System;
using System.ComponentModel;
using System.Windows;

namespace LogScope.App.Views
{
    /// <summary>
    /// 오래 걸리는 일이 도는 동안 띄우는 창. 진행 상황과 취소 버튼이 있습니다.
    /// Report() 는 배경 스레드에서 불리므로 안에서 UI 스레드로 넘깁니다.
    /// </summary>
    public partial class LoadingWindow : Window
    {
        private bool _finished;
        private int _lastTick;

        /// <summary>취소 버튼을 눌렀을 때.</summary>
        public event Action CancelRequested;

        public LoadingWindow(string title)
        {
            InitializeComponent();
            Title = title;
            StageText.Text = title;
        }

        /// <summary>배경 스레드에서 불립니다. fraction 이 음수면 "진행률 모름".</summary>
        public void Report(string stage, double fraction)
        {
            // 너무 자주 그리면 오히려 느려집니다. 60ms 에 한 번만 올립니다.
            int now = Environment.TickCount;
            if (now - _lastTick < 60 && fraction < 1.0) return;
            _lastTick = now;

            try
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    if (_finished) return;
                    DetailText.Text = stage ?? string.Empty;
                    if (fraction >= 0)
                    {
                        Bar.IsIndeterminate = false;
                        double v = fraction * 1000.0;
                        Bar.Value = v < 0 ? 0 : (v > 1000 ? 1000 : v);
                    }
                    else Bar.IsIndeterminate = true;
                });
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>UI 스레드에서 불러야 합니다.</summary>
        public void Finish()
        {
            _finished = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            StageText.Text = "취소하는 중…";
            Action h = CancelRequested;
            if (h != null) h();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 일이 끝나기 전에는 창이 닫히지 않습니다. 대신 취소로 받습니다.
            if (!_finished)
            {
                e.Cancel = true;
                OnCancel(this, null);
                return;
            }
            base.OnClosing(e);
        }
    }
}
