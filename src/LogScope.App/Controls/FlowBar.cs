using System;
using System.Drawing;
using System.Windows.Forms;

namespace LogScope.App.Controls
{
    /// <summary>
    /// 버튼과 설명을 담는 가로 줄. 창이 좁아지면 스스로 줄바꿈하고 높이가
    /// 늘어납니다. 그래서 버튼이 영역 밖으로 잘려 나가거나 서로 겹칠 일이
    /// 없습니다. (요구사항 7번)
    /// </summary>
    public sealed class FlowBar : FlowLayoutPanel
    {
        public FlowBar()
        {
            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = true;
            FlowDirection = FlowDirection.LeftToRight;
            Padding = new Padding(Dpi.S(8), Dpi.S(6), Dpi.S(8), Dpi.S(6));
            Margin = Padding.Empty;
            BackColor = Theme.Current.Panel;
            MinimumSize = new Size(0, Dpi.S(10));
        }

        /// <summary>줄 아래에 얇은 경계선을 긋습니다.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Current.Border))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

        public Label AddLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = Theme.Ui;
            l.ForeColor = Theme.Current.Muted;
            l.Margin = new Padding(Dpi.S(8), Dpi.S(9), Dpi.S(3), Dpi.S(3));
            Controls.Add(l);
            return l;
        }

        public void AddGap(int px)
        {
            var p = new Panel();
            p.Width = Dpi.S(px);
            p.Height = 1;
            p.Margin = Padding.Empty;
            Controls.Add(p);
        }

        /// <summary>줄바꿈을 강제합니다.</summary>
        public void AddBreak()
        {
            var p = new Panel();
            p.Width = 1; p.Height = 1;
            p.Margin = Padding.Empty;
            Controls.Add(p);
            SetFlowBreak(p, true);
        }
    }
}
