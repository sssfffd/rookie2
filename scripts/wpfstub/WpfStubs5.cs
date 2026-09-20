using System;
namespace System.Windows.Forms
{
    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }
    public class FolderBrowserDialog : IDisposable
    {
        public string Description, SelectedPath;
        public bool ShowNewFolderButton;
        public DialogResult ShowDialog() { return DialogResult.OK; }
        public void Dispose() { }
    }
}
