using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LogScope.App.Infrastructure
{
    /// <summary>
    /// 값이 바뀌면 화면에 알리는 바탕 클래스.
    /// WPF 바인딩은 INotifyPropertyChanged 를 보고 다시 그립니다.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string name = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>값이 실제로 달라졌을 때만 알립니다. 달라졌으면 true.</summary>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (object.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
