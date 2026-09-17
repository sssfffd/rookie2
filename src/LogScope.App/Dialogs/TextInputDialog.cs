using System;
using System.Drawing;
using System.Windows.Forms;
using LogScope.App.Controls;

namespace LogScope.App.Dialogs
{
    /// <summary>글자 한 줄을 받는 작은 창. 그룹 이름 바꾸기 등에 씁니다.</summary>
    public sealed class TextInputDialog : Form
    {
        private readonly TextBox _box = new TextBox();

        private TextInputDialog(string title, string prompt, string initial)
        {
            Theme t = Theme.Current;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = t.Window; ForeColor = t.Text; Font = Theme.Ui;
            ClientSize = new Size(Dpi.S(400), Dpi.S(140));

            var label = new Label();
            label.Text = prompt;
            label.AutoSize = false;
            label.Bounds = new Rectangle(Dpi.S(16), Dpi.S(16), Dpi.S(368), Dpi.S(20));
            label.ForeColor = t.Muted;

            _box.Text = initial ?? string.Empty;
            _box.Bounds = new Rectangle(Dpi.S(16), Dpi.S(40), Dpi.S(368), Dpi.S(26));
            _box.BackColor = t.IsDark ? t.PanelAlt : Color.White;
            _box.ForeColor = t.Text;
            _box.BorderStyle = BorderStyle.FixedSingle;
            _box.SelectAll();

            Button ok = Buttons.MakePrimary("확인", null);
            ok.AutoSize = false;
            ok.Size = new Size(Dpi.S(92), Dpi.S(30));
            ok.Location = new Point(Dpi.S(196), Dpi.S(88));
            ok.DialogResult = DialogResult.OK;

            Button cancel = Buttons.Make("취소", null);
            cancel.AutoSize = false;
            cancel.Size = new Size(Dpi.S(92), Dpi.S(30));
            cancel.Location = new Point(Dpi.S(294), Dpi.S(88));
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(label);
            Controls.Add(_box);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static bool Ask(IWin32Window owner, string title, string prompt,
                              string initial, out string value)
        {
            using (var d = new TextInputDialog(title, prompt, initial))
            {
                bool ok = d.ShowDialog(owner) == DialogResult.OK;
                value = ok ? d._box.Text.Trim() : null;
                return ok && !string.IsNullOrEmpty(value);
            }
        }
    }
}
