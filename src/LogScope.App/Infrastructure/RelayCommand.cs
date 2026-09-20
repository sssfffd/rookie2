using System;
using System.Windows.Input;

namespace LogScope.App.Infrastructure
{
    /// <summary>
    /// 버튼 하나에 메서드 하나를 묶는 가장 단순한 명령.
    ///
    /// 생성자를 <b>하나만</b> 둡니다. 예전에는 Action / Action&lt;object&gt; 짝으로
    /// 여러 개를 뒀는데, 매개변수 목록 없는 익명 메서드(delegate { ... })가
    /// 두 종류 모두로 변환돼서 "모호한 호출" 오류가 났습니다.
    /// 매개변수 없는 메서드를 묶을 때는 아래 For() 를 쓰세요.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _run;
        private readonly Func<object, bool> _can;

        /// <param name="run">실행할 일. null 이면 안 됩니다.</param>
        /// <param name="can">지금 실행할 수 있는지. null 이면 늘 실행 가능.</param>
        public RelayCommand(Action<object> run, Func<object, bool> can)
        {
            if (run == null) throw new ArgumentNullException("run");
            _run = run;
            _can = can;
        }

        /// <summary>매개변수를 받지 않는 메서드를 묶습니다.</summary>
        public static RelayCommand For(Action run)
        {
            if (run == null) throw new ArgumentNullException("run");
            return new RelayCommand(delegate (object ignored) { run(); }, null);
        }

        /// <summary>매개변수를 받지 않는 메서드를, 실행 가능 여부와 함께 묶습니다.</summary>
        public static RelayCommand For(Action run, Func<bool> can)
        {
            if (run == null) throw new ArgumentNullException("run");
            Func<object, bool> wrapped = null;
            if (can != null) wrapped = delegate (object ignored) { return can(); };
            return new RelayCommand(delegate (object ignored) { run(); }, wrapped);
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
