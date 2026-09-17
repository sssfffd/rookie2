using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LogScope.App.Controls;
using LogScope.Core.Io;

namespace LogScope.App.Dialogs
{
    /// <summary>
    /// 오래 걸리는 일을 배경 스레드에서 돌리면서 진행 상황과 취소 버튼을
    /// 보여 줍니다. 읽는 동안 창이 멈춘 것처럼 보이지 않게 하는 것이 목적입니다.
    /// (요구사항 7번)
    /// </summary>
    public sealed class LoadingDialog : Form
    {
        private readonly Label _stage = new Label();
        private readonly Label _detail = new Label();
        private readonly ProgressBar _bar = new ProgressBar();
        private readonly Button _cancel;
        private readonly LoadProgress _progress;
        private readonly Func<LoadProgress, object> _work;

        private Thread _thread;
        private object _result;
        private Exception _error;
        private bool _finished;
        private int _lastTick;

        private LoadingDialog(string title, Func<LoadProgress, object> work)
        {
            _work = work;
            _progress = new LoadProgress(OnReport);

            Theme t = Theme.Current;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ControlBox = false;
            BackColor = t.Window;
            ForeColor = t.Text;
            Font = Theme.Ui;
            ClientSize = new Size(Dpi.S(460), Dpi.S(150));

            _stage.Text = "준비하는 중…";
            _stage.Font = Theme.UiBold;
            _stage.ForeColor = t.Text;
            _stage.AutoEllipsis = true;
            _stage.Bounds = new Rectangle(Dpi.S(18), Dpi.S(18), Dpi.S(424), Dpi.S(22));

            _detail.Text = string.Empty;
            _detail.ForeColor = t.Muted;
            _detail.AutoEllipsis = true;
            _detail.Bounds = new Rectangle(Dpi.S(18), Dpi.S(42), Dpi.S(424), Dpi.S(20));

            _bar.Bounds = new Rectangle(Dpi.S(18), Dpi.S(70), Dpi.S(424), Dpi.S(18));
            _bar.Minimum = 0;
            _bar.Maximum = 1000;
            _bar.Style = ProgressBarStyle.Marquee;
            _bar.MarqueeAnimationSpeed = 30;

            _cancel = Controls_Cancel();
            Controls.Add(_stage);
            Controls.Add(_detail);
            Controls.Add(_bar);
            Controls.Add(_cancel);
        }

        private Button Controls_Cancel()
        {
            Button b = null;
            b = Buttons.Make("취소", delegate
            {
                _progress.Cancel();
                _stage.Text = "취소하는 중…";
                if (b != null) b.Enabled = false;
            });
            b.AutoSize = false;
            b.Size = new Size(Dpi.S(96), Dpi.S(30));
            b.Location = new Point(ClientSize.Width - Dpi.S(96) - Dpi.S(18), Dpi.S(102));
            b.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            return b;
        }

        // 배경 스레드에서 불립니다. 화면 갱신은 UI 스레드로 넘깁니다.
        private void OnReport(string stage, double fraction)
        {
            int now = Environment.TickCount;
            if (now - _lastTick < 60 && fraction < 1.0) return;   // 너무 자주 그리지 않기
            _lastTick = now;
            try
            {
                if (!IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (_finished) return;
                    _detail.Text = stage ?? string.Empty;
                    if (fraction >= 0)
                    {
                        if (_bar.Style != ProgressBarStyle.Continuous)
                        {
                            _bar.Style = ProgressBarStyle.Continuous;
                            _bar.MarqueeAnimationSpeed = 0;
                        }
                        int v = (int)(fraction * 1000);
                        _bar.Value = v < 0 ? 0 : (v > 1000 ? 1000 : v);
                    }
                    else if (_bar.Style != ProgressBarStyle.Marquee)
                    {
                        _bar.Style = ProgressBarStyle.Marquee;
                        _bar.MarqueeAnimationSpeed = 30;
                    }
                });
            }
            catch (InvalidOperationException) { }
            catch (ObjectDisposedException) { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _stage.Text = Text;
            _thread = new Thread(Worker);
            _thread.IsBackground = true;
            _thread.Name = "LogScope 작업";
            _thread.Start();
        }

        private void Worker()
        {
            object r = null;
            Exception err = null;
            try { r = _work(_progress); }
            catch (Exception ex) { err = ex; }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _result = r;
                    _error = err;
                    _finished = true;
                    DialogResult = err == null ? DialogResult.OK : DialogResult.Abort;
                    Close();
                });
            }
            catch (InvalidOperationException) { }
            catch (ObjectDisposedException) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_finished && e.CloseReason == CloseReason.UserClosing)
            {
                _progress.Cancel();
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 일을 시키고 결과를 받습니다. 취소하면 result 가 null 이고
        /// error 는 OperationCanceledException 입니다.
        /// </summary>
        public static bool Run(IWin32Window owner, string title,
                               Func<LoadProgress, object> work,
                               out object result, out Exception error)
        {
            using (var dlg = new LoadingDialog(title, work))
            {
                dlg.ShowDialog(owner);
                result = dlg._result;
                error = dlg._error;
                return error == null;
            }
        }
    }
}
