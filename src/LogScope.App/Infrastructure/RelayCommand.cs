using System;
using System.Windows.Input;

namespace LogScope.App.Infrastructure
{
    /// <summary>버튼 하나에 메서드 하나를 묶는 가장 단순한 명령.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _run;
        private readonly Func<object, bool> _can;

        public RelayCommand(Action run) : this(delegate { run(); }, null) { }

        public RelayCommand(Action run, Func<bool> can)
            : this(delegate { run(); }, can == null ? (Func<object, bool>)null : delegate { return can(); }) { }

        public RelayCommand(Action<object> run, Func<object, bool> can)
        {
            if (run == null) throw new ArgumentNullException("run");
            _run = run;
            _can = can;
        }

        public bool CanExecute(object parameter)
        {
            return _can == null || _can(parameter);
        }

        public void Execute(object parameter)
        {
            _run(parameter);
        }

        /// <summary>
        /// CommandManager 에 얹어 두면 화면에서 뭔가 바뀔 때마다 WPF 가
        /// CanExecute 를 다시 물어봅니다. 따로 알릴 필요가 없어집니다.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
