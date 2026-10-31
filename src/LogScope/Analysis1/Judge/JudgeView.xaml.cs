using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LogScope.Analysis1.Judge
{
    /// <summary>
    /// 분석 1 오차 목록 화면 (1.56). 줄을 누르면 허용 고르는 창, [지우기] 는 규칙 하나를 지웁니다.
    /// 허용을 바꾸면 <see cref="JudgeVm.Changed"/> 가 울리고, 창(ShellWindow)이 설정을 저장하고 메인 칸을 다시 그립니다.
    /// </summary>
    public partial class JudgeView : UserControl
    {
        public JudgeView()
        {
            InitializeComponent();
        }

        private JudgeVm Vm { get { return DataContext as JudgeVm; } }

        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            var row = fe != null ? fe.DataContext as JudgeRowVm : null;
            JudgeVm vm = Vm;
            if (row == null || vm == null) return;
            // 스크롤 막대를 누른 것은 줄이 아닙니다 (DataContext 가 JudgeVm).
            e.Handled = true;

            JudgeItem it = row.Item;
            string size = SizeText(vm, it);
            AllowChoice c = AllowPrompt.Ask(Window.GetWindow(this), it, vm.OpenCountOf(it.Io), size);
            vm.Apply(it, c);
        }

        private static string SizeText(JudgeVm vm, JudgeItem it)
        {
            JudgeResult r = vm.Result;
            if (r == null) return string.Empty;
            if (it.Kind == JudgeKind.Value)
                return r.Duration(it.Duration) + " · " + LogScope.Common.NumberText.Plain(it.MaxExcess);
            if (it.Kind == JudgeKind.StepLength)
                return "±" + r.Duration(System.Math.Abs(it.MaxExcess));
            return string.Empty;
        }

        private void OnRemoveRule(object sender, RoutedEventArgs e)
        {
            var b = sender as FrameworkElement;
            JudgeVm vm = Vm;
            if (b == null || vm == null || !(b.Tag is int)) return;
            vm.RemoveRule((int)b.Tag);
        }
    }
}
