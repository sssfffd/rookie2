using System;
namespace System.Windows.Threading
{
    public class DispatcherUnhandledExceptionEventArgs : EventArgs
    {
        public Exception Exception;
        public bool Handled;
    }
    public delegate void DispatcherUnhandledExceptionEventHandler(object s, DispatcherUnhandledExceptionEventArgs e);
}
namespace System.Windows
{
    public partial class Application
    {
        public event System.Windows.Threading.DispatcherUnhandledExceptionEventHandler DispatcherUnhandledException;
        public System.Windows.Threading.Dispatcher Dispatcher { get { return null; } }
    }
}
