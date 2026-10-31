using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using LogScope.Common;

namespace LogScope.Settings
{
    /// <summary>
    /// 설정 창. 오른쪽 위 [설정] 버튼으로 엽니다.
    /// 값은 바인딩으로 AppSettings 에 바로 들어가고, 창을 닫을 때 저장합니다.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsVm _vm;

        public SettingsWindow(AppSettings settings) : this(settings, null) { }

        /// <param name="ioNames">
        /// 열어 둔 로그의 IO 이름. IO 별 허용 오차를 걸 때 고르라고 내어
        /// 줍니다. 로그를 안 열었으면 없어도 됩니다 — 손으로 적을 수 있습니다.
        /// </param>
        public SettingsWindow(AppSettings settings, IEnumerable<string> ioNames)
        {
            InitializeComponent();
            _vm = new SettingsVm(settings, ioNames);
            DataContext = _vm;

            // LLM 서버 상태 줄 (1.51) — 확인은 배경 스레드라 화면 스레드로 넘겨 고칩니다.
            LlmServer.Changed += OnLlmServerChanged;
            ShowLlmStatus();
        }

        // ---- LLM 서버 (1.51) ----

        private void OnCheckLlm(object sender, RoutedEventArgs e)
        {
            LlmServer.CheckAsync(_vm.LlmServer);
        }

        private void OnLlmServerChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(ShowLlmStatus));
        }

        private void ShowLlmStatus()
        {
            LlmServerState st = LlmServer.State;
            string word;
            string brush;
            switch (st)
            {
                case LlmServerState.Ok: word = "연결됨"; brush = "Brush.Good"; break;
                case LlmServerState.Fail: word = "연결 안 됨"; brush = "Brush.Bad"; break;
                case LlmServerState.Checking: word = "확인 중"; brush = "Brush.Warn"; break;
                default: word = "아직 확인 안 함"; brush = "Brush.Muted"; break;
            }
            string d = LlmServer.Detail;
            LlmStatus.Text = d.Length > 0 && st != LlmServerState.Checking ? word + " — " + d : word;
            LlmDot.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, brush);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 닫을 때 빈 줄을 버립니다. 이름을 안 고르고 닫은 줄이 남아 있으면
        /// 다음에 열었을 때 왜 있는지 알 수 없는 줄이 됩니다.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            LlmServer.Changed -= OnLlmServerChanged;
            _vm.PruneTolerances();
            base.OnClosed(e);
        }

        private void OnAddTolerance(object sender, RoutedEventArgs e)
        {
            _vm.AddTolerance();
        }

        /// <summary>[허용한 오차] 의 [지우기] (1.59).</summary>
        private void OnRemoveAllowRule(object sender, RoutedEventArgs e)
        {
            var b = sender as FrameworkElement;
            if (b != null) _vm.RemoveAllowRule(b.Tag as AllowRuleRowVm);
        }

        private void OnRemoveTolerance(object sender, RoutedEventArgs e)
        {
            var b = sender as FrameworkElement;
            if (b != null) _vm.RemoveTolerance(b.Tag as ToleranceRowVm);
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

        private void OnPickMysqlDump(object sender, RoutedEventArgs e)
        {
            string picked = PickFile("실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*", _vm.MysqlDumpPath);
            if (picked != null) _vm.MysqlDumpPath = picked;
        }

        private void OnPickIbd2Sdi(object sender, RoutedEventArgs e)
        {
            string picked = PickFile("실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*", _vm.Ibd2SdiPath);
            if (picked != null) _vm.Ibd2SdiPath = picked;
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
