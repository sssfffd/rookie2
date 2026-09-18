using System;
using System.Windows;

namespace LogScope.App.Themes
{
    /// <summary>
    /// 테마 갈아 끼우기.
    ///
    /// WinForms 였다면 프로그램을 다시 켜야 했지만, WPF 는 자원 사전만
    /// 바꿔 끼우면 화면이 바로 따라옵니다. 브러시를 DynamicResource 로
    /// 가져다 쓰기 때문입니다.
    /// </summary>
    public static class ThemeManager
    {
        private const string LightUri = "pack://application:,,,/Themes/Light.xaml";
        private const string DarkUri = "pack://application:,,,/Themes/Dark.xaml";

        /// <summary>그래프를 직접 그릴 때 쓰는 색. 테마를 바꾸면 같이 바뀝니다.</summary>
        public static Palette Palette = Palette.Light();

        public static string Current { get; private set; }

        /// <summary>테마가 바뀌면 불립니다. 직접 그리는 화면은 여기서 다시 그립니다.</summary>
        public static event EventHandler Changed;

        public static bool IsDark
        {
            get { return string.Equals(Current, "dark", StringComparison.OrdinalIgnoreCase); }
        }

        public static void Apply(string name)
        {
            bool dark = string.Equals(name, "dark", StringComparison.OrdinalIgnoreCase);
            Current = dark ? "dark" : "light";
            Palette = dark ? Palette.Dark() : Palette.Light();

            Application app = Application.Current;
            if (app != null)
            {
                var dict = new ResourceDictionary();
                dict.Source = new Uri(dark ? DarkUri : LightUri, UriKind.Absolute);

                // 색 사전은 항상 0 번 자리에 둡니다. 1 번은 컨트롤 모양(Controls.xaml)
                // 인데, 그쪽은 색 사전을 참조하므로 순서가 바뀌면 안 됩니다.
                if (app.Resources.MergedDictionaries.Count == 0)
                    app.Resources.MergedDictionaries.Add(dict);
                else
                    app.Resources.MergedDictionaries[0] = dict;
            }

            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }
    }
}
