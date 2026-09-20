using System.IO;
using System.Windows;
using LogScope.App.ViewModels;
using LogScope.Core.Settings;

namespace LogScope.App.Views
{
    /// <summary>
    /// 설정 창. 오른쪽 위 [설정] 버튼으로 엽니다.
    /// 값은 바인딩으로 AppSettings 에 바로 들어가고, 창을 닫을 때 저장합니다.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsVm _vm;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _vm = new SettingsVm(settings);
            DataContext = _vm;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnForgetFiles(object sender, RoutedEventArgs e)
        {
            _vm.ForgetFiles();
        }

        private void OnResetTolerance(object sender, RoutedEventArgs e)
        {
            _vm.ResetTolerance();
        }

        // ---- 폴더 고르기 ----
        //
        // WPF 에는 폴더 고르는 창이 없습니다. .NET Framework 에 원래 들어 있는
        // System.Windows.Forms.FolderBrowserDialog 를 이 한 곳에서만 씁니다.
        // 바깥에서 받아 온 라이브러리가 아니라 프레임워크의 일부입니다.

        private void OnPickBeforeFolder(object sender, RoutedEventArgs e)
        {
            string picked = PickFolder("이전 로그를 찾을 기본 폴더", _vm.BeforeFolder);
            if (picked != null) _vm.BeforeFolder = picked;
        }

        private void OnPickAfterFolder(object sender, RoutedEventArgs e)
        {
            string picked = PickFolder("이후 로그를 찾을 기본 폴더", _vm.AfterFolder);
            if (picked != null) _vm.AfterFolder = picked;
        }

        private static string PickFolder(string description, string start)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = description;
                dlg.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) dlg.SelectedPath = start;
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
            }
        }

        // ---- 파일 고르기 ----

        private void OnPickPython(object sender, RoutedEventArgs e)
        {
            string picked = PickFile("실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*", _vm.PythonPath);
            if (picked != null) _vm.PythonPath = picked;
        }

        private void OnPickScript(object sender, RoutedEventArgs e)
        {
            string picked = PickFile("파이썬 (*.py)|*.py|모든 파일 (*.*)|*.*", _vm.AiScriptPath);
            if (picked != null) _vm.AiScriptPath = picked;
        }

        private string PickFile(string filter, string start)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = filter;
            dlg.CheckFileExists = true;
            if (!string.IsNullOrEmpty(start) && File.Exists(start)) dlg.FileName = start;
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        }
    }
}
