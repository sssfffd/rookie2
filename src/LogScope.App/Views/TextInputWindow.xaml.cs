using System.Windows;

namespace LogScope.App.Views
{
    /// <summary>글자 한 줄을 받는 작은 창. 그룹 이름 바꾸기 등에 씁니다.</summary>
    public partial class TextInputWindow : Window
    {
        private TextInputWindow(string title, string prompt, string initial)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            Input.Text = initial ?? string.Empty;
            Loaded += delegate { Input.Focus(); Input.SelectAll(); };
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public static bool Ask(Window owner, string title, string prompt, string initial, out string value)
        {
            var w = new TextInputWindow(title, prompt, initial);
            if (owner != null && owner.IsVisible) w.Owner = owner;
            bool ok = w.ShowDialog() == true;
            value = ok ? w.Input.Text.Trim() : null;
            return ok && !string.IsNullOrEmpty(value);
        }
    }
}
