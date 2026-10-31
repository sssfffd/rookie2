using System.Windows;
using System.Windows.Controls;

namespace LogScope.Analysis1.Judge
{
    /// <summary>
    /// 오차 한 줄을 눌렀을 때 뜨는 작은 창 (1.56) — 허용할지 고릅니다.
    ///
    /// XAML 없이 코드로 만듭니다. 고를 것이 셋 ~ 넷이라 예/아니오 창으로는 모자라고,
    /// 창 하나를 위해 XAML 파일을 따로 두면 이 저장소의 검사(리눅스)가 모양을 못 봅니다.
    /// </summary>
    public static class AllowPrompt
    {
        public static AllowChoice Ask(Window owner, JudgeItem item, int sameIoOpen, string sizeText)
        {
            if (item == null) return AllowChoice.Cancel;

            var win = new Window();
            win.Title = JudgeTexts.PromptTitle;
            win.Owner = owner;
            win.WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            win.SizeToContent = SizeToContent.WidthAndHeight;
            win.ResizeMode = ResizeMode.NoResize;
            win.ShowInTaskbar = false;
            win.MaxWidth = 720;
            if (owner != null)
            {
                win.Background = owner.Background;
                win.Foreground = owner.Foreground;
                win.FontFamily = owner.FontFamily;
                win.FontSize = owner.FontSize;
            }

            var root = new StackPanel();
            root.Margin = new Thickness(18, 14, 18, 14);

            var what = new TextBlock();
            what.Text = item.What;
            what.FontWeight = FontWeights.SemiBold;
            what.TextWrapping = TextWrapping.Wrap;
            root.Children.Add(what);

            var detail = new TextBlock();
            detail.Text = item.Detail;
            detail.Opacity = 0.75;
            detail.TextWrapping = TextWrapping.Wrap;
            detail.Margin = new Thickness(0, 4, 0, 12);
            root.Children.Add(detail);

            RadioButton first, none = null, once = null, always = null, ioOnce = null, unallow = null, keep = null;

            if (item.IsAllowed)
            {
                unallow = Radio(JudgeTexts.ChoiceUnallow, root);
                keep = Radio(JudgeTexts.ChoiceKeep, root);
                first = unallow;
            }
            else
            {
                none = Radio(JudgeTexts.ChoiceNone, root);
                once = Radio(JudgeTexts.ChoiceOnce, root);
                if (item.CanAllowAlways)
                {
                    always = Radio(JudgeTexts.ChoiceAlways
                        + (string.IsNullOrEmpty(sizeText) ? string.Empty : " (" + sizeText + ")"), root);
                }
                else
                {
                    var no = new TextBlock();
                    no.Text = JudgeTexts.AlwaysNotHere;
                    no.Opacity = 0.75;
                    no.Margin = new Thickness(20, 2, 0, 4);
                    root.Children.Add(no);
                }
                if (item.Kind == JudgeKind.Value && sameIoOpen > 1)
                    ioOnce = Radio(item.Io + " — " + JudgeTexts.ChoiceIoOnce + " (" + sameIoOpen + " 개)", root);
                first = once;
            }
            first.IsChecked = true;

            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 14, 0, 0);
            var ok = new Button();
            ok.Content = JudgeTexts.Ok;
            ok.IsDefault = true;
            ok.MinWidth = 80;
            ok.Margin = new Thickness(0, 0, 8, 0);
            ok.Click += delegate { win.DialogResult = true; };
            var cancel = new Button();
            cancel.Content = JudgeTexts.Cancel;
            cancel.IsCancel = true;
            cancel.MinWidth = 80;
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            win.Content = root;
            if (win.ShowDialog() != true) return AllowChoice.Cancel;

            if (Checked(unallow)) return AllowChoice.Unallow;
            if (Checked(keep) || Checked(none)) return AllowChoice.Cancel;
            if (Checked(once)) return AllowChoice.Once;
            if (Checked(always)) return AllowChoice.Always;
            if (Checked(ioOnce)) return AllowChoice.IoOnce;
            return AllowChoice.Cancel;
        }

        private static RadioButton Radio(string text, Panel parent)
        {
            var r = new RadioButton();
            r.GroupName = "allow";
            var tb = new TextBlock();
            tb.Text = text;
            tb.TextWrapping = TextWrapping.Wrap;
            r.Content = tb;
            r.Margin = new Thickness(0, 3, 0, 3);
            parent.Children.Add(r);
            return r;
        }

        private static bool Checked(RadioButton r)
        {
            return r != null && r.IsChecked == true;
        }
    }
}
