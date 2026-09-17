using System;
using System.Drawing;
using System.Windows.Forms;

namespace LogScope.App.Controls
{
    /// <summary>
    /// 이 프로그램의 버튼들. 모두 글자를 답니다.
    /// 화살표나 +, x 같은 기호만 있는 버튼은 만들지 않습니다 — 무슨 기능인지
    /// 알 수 없기 때문입니다. 필요하면 "왼쪽으로", "그룹 삭제" 처럼 적습니다.
    /// </summary>
    public static class Buttons
    {
        public static Button Make(string text, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.FlatStyle = FlatStyle.Flat;
            b.Font = Theme.Ui;
            b.Margin = new Padding(Dpi.S(3));
            b.Padding = new Padding(Dpi.S(9), Dpi.S(4), Dpi.S(9), Dpi.S(4));
            b.UseVisualStyleBackColor = false;
            b.TabStop = true;
            Paint(b, false);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static Button MakePrimary(string text, EventHandler onClick)
        {
            Button b = Make(text, onClick);
            Paint(b, true);
            return b;
        }

        public static void Paint(Button b, bool primary)
        {
            Theme t = Theme.Current;
            if (primary)
            {
                b.BackColor = t.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = t.Accent;
            }
            else
            {
                b.BackColor = t.IsDark ? t.PanelAlt : Color.White;
                b.ForeColor = t.Text;
                b.FlatAppearance.BorderColor = t.Border;
            }
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(t.Accent, 0.1f) : t.AccentSoft;
        }

        /// <summary>눌린 상태가 보이는 켜기/끄기 버튼.</summary>
        public sealed class Toggle : Button
        {
            private bool _on;

            public Toggle(string text)
            {
                Text = text;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                FlatStyle = FlatStyle.Flat;
                Font = Theme.Ui;
                Margin = new Padding(Dpi.S(3));
                Padding = new Padding(Dpi.S(9), Dpi.S(4), Dpi.S(9), Dpi.S(4));
                UseVisualStyleBackColor = false;
                FlatAppearance.BorderSize = 1;
                Restyle();
            }

            public event EventHandler CheckedChangedEx;

            public bool On
            {
                get { return _on; }
                set
                {
                    if (_on == value) return;
                    _on = value;
                    Restyle();
                    var h = CheckedChangedEx; if (h != null) h(this, EventArgs.Empty);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                On = !On;
                base.OnClick(e);
            }

            public void Restyle()
            {
                Theme t = Theme.Current;
                BackColor = _on ? t.Accent : (t.IsDark ? t.PanelAlt : Color.White);
                ForeColor = _on ? Color.White : t.Text;
                FlatAppearance.BorderColor = _on ? t.Accent : t.Border;
                FlatAppearance.MouseOverBackColor = _on ? ControlPaint.Light(t.Accent, 0.1f) : t.AccentSoft;
            }
        }

        /// <summary>여러 개 중 하나를 고르는 줄. 각 칸에 글자가 들어갑니다.</summary>
        public sealed class Segmented : FlowLayoutPanel
        {
            private readonly Toggle[] _items;
            private int _index;

            public Segmented(string[] labels, int initial)
            {
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                WrapContents = false;
                FlowDirection = FlowDirection.LeftToRight;
                Margin = new Padding(Dpi.S(3));
                Padding = Padding.Empty;
                BackColor = Color.Transparent;

                _items = new Toggle[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    var b = new Toggle(labels[i]);
                    b.Margin = new Padding(0, 0, Dpi.S(2), 0);
                    int captured = i;
                    b.Click += delegate { Index = captured; };
                    _items[i] = b;
                    Controls.Add(b);
                }
                _index = -1;
                Index = initial;
            }

            public event EventHandler IndexChanged;

            public int Index
            {
                get { return _index; }
                set
                {
                    if (value < 0 || value >= _items.Length) return;
                    _index = value;
                    for (int i = 0; i < _items.Length; i++) _items[i].On = (i == value);
                    var h = IndexChanged; if (h != null) h(this, EventArgs.Empty);
                }
            }

            public void Restyle() { foreach (Toggle t in _items) t.Restyle(); }
        }
    }
}
