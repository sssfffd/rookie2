using System;
using System.IO;
using System.Text;
using System.Windows;

namespace LogScope.App.Common
{
    /// <summary>
    /// 만들어 준 글을 <b>보여 주는</b> 창. 복사와 파일 저장이 함께 있습니다.
    ///
    /// 분석 2 의 [SQL 문 출력] 이 이 창을 띄웁니다. 전에는 화면 아래에 글
    /// 칸이 붙어 있었는데(0.79 까지), 목록이 쓸 높이를 늘 반 가져갔습니다.
    /// 볼 때만 띄우는 창이 나았습니다.
    ///
    /// 글을 만드는 쪽과 보여 주는 쪽을 나눠 두었으므로, 다른 화면에서도
    /// 그대로 쓸 수 있습니다 (그래서 Common 에 있습니다).
    /// </summary>
    public partial class TextViewWindow : Window
    {
        private readonly string _suggestName;
        private readonly string _filter;

        public TextViewWindow(string title, string head, string body,
                              string suggestName, string filter)
        {
            InitializeComponent();

            Title = title ?? "글 보기";
            HeadText.Text = head ?? string.Empty;
            Body.Text = body ?? string.Empty;
            _suggestName = suggestName ?? "글.txt";
            _filter = filter ?? "글 (*.txt)|*.txt|모든 파일 (*.*)|*.*";
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(Body.Text);
            }
            catch (Exception ex)
            {
                // 클립보드는 다른 프로그램이 잡고 있으면 실패합니다.
                // 창을 닫지 않고 그렇다고만 말합니다.
                MessageBox.Show(this, "복사하지 못했습니다.\n\n" + ex.Message,
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = Title;
            dlg.Filter = _filter;
            dlg.FileName = _suggestName;
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                // BOM 을 붙입니다. mysql 클라이언트가 한글을 UTF-8 로 읽게
                // 하는 가장 확실한 방법입니다.
                File.WriteAllText(dlg.FileName, Body.Text, new UTF8Encoding(true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                    || ex is NotSupportedException || ex is ArgumentException)
            {
                MessageBox.Show(this, "저장하지 못했습니다.\n\n" + ex.Message,
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
