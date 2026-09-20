// WPF 를 흉내 낸 최소 스텁. 리눅스에는 WPF 가 없어서, App 프로젝트의 C# 을
// 타입 검사라도 해 보려고 만든 <b>검사 전용</b> 파일입니다. 저장소에는
// 들어가지 않습니다. 동작을 흉내 내지 않고 시그니처만 맞춥니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace System.Windows
{
    public struct Point { public double X, Y; public Point(double x, double y) { X = x; Y = y; } }
    public struct Size { public double Width, Height; public Size(double w, double h) { Width = w; Height = h; } }
    public struct Vector { public double X, Y; public Vector(double x, double y) { X = x; Y = y; } }
    public struct Thickness
    {
        public double Left, Top, Right, Bottom;
        public Thickness(double u) { Left = Top = Right = Bottom = u; }
        public Thickness(double l, double t, double r, double b) { Left = l; Top = t; Right = r; Bottom = b; }
    }
    public struct Rect
    {
        public double X, Y, Width, Height;
        public Rect(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }
        public Rect(Point a, Point b) { X = 0; Y = 0; Width = 0; Height = 0; }
        public Rect(Point a, Size s) { X = 0; Y = 0; Width = 0; Height = 0; }
        public double Left { get { return X; } }
        public double Top { get { return Y; } }
        public double Right { get { return X + Width; } }
        public double Bottom { get { return Y + Height; } }
        public bool Contains(Point p) { return false; }
        public static Rect Empty { get { return new Rect(); } }
        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }
    }

    public enum Visibility { Visible, Hidden, Collapsed }
    public enum HorizontalAlignment { Left, Center, Right, Stretch }
    public enum VerticalAlignment { Top, Center, Bottom, Stretch }
    public enum TextWrapping { NoWrap, Wrap, WrapWithOverflow }
    public enum TextTrimming { None, CharacterEllipsis, WordEllipsis }
    public enum FlowDirection { LeftToRight, RightToLeft }
    public enum WindowState { Normal, Minimized, Maximized }
    public enum WindowStartupLocation { Manual, CenterScreen, CenterOwner }
    public enum SizeToContent { Manual, Width, Height, WidthAndHeight }
    public enum ResizeMode { NoResize, CanMinimize, CanResize, CanResizeWithGrip }
    public enum MessageBoxButton { OK, OKCancel, YesNoCancel, YesNo }
    public enum MessageBoxImage { None, Error, Question, Warning, Information }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No }

    public struct FontWeight { }
    public static class FontWeights
    {
        public static FontWeight Normal, Bold, SemiBold, Light, Medium;
    }
    public static class FontStyles { public static object Normal, Italic; }
    public static class FontStretches { public static object Normal; }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class ThemeInfoAttribute : Attribute
    {
        public ThemeInfoAttribute(ResourceDictionaryLocation t, ResourceDictionaryLocation g) { }
    }
    public enum ResourceDictionaryLocation { None, SourceAssembly, ExternalAssembly }

    public static class SystemParameters
    {
        public static double MinimumHorizontalDragDistance { get { return 4; } }
        public static double MinimumVerticalDragDistance { get { return 4; } }
        public static double PrimaryScreenWidth { get { return 1920; } }
        public static double PrimaryScreenHeight { get { return 1080; } }
    }

    public class DependencyObject
    {
        public object GetValue(DependencyProperty p) { return null; }
        public void SetValue(DependencyProperty p, object v) { }
    }

    public class DependencyProperty
    {
        public static DependencyProperty Register(string name, Type t, Type owner) { return null; }
        public static DependencyProperty Register(string name, Type t, Type owner, PropertyMetadata m) { return null; }
    }

    public class PropertyMetadata
    {
        public PropertyMetadata() { }
        public PropertyMetadata(object d) { }
        public PropertyMetadata(object d, PropertyChangedCallback cb) { }
    }
    public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);

    [Flags]
    public enum FrameworkPropertyMetadataOptions
    {
        None = 0, AffectsRender = 1, AffectsMeasure = 2, AffectsArrange = 4, BindsTwoWayByDefault = 8,
    }
    public class FrameworkPropertyMetadata : PropertyMetadata
    {
        public FrameworkPropertyMetadata() { }
        public FrameworkPropertyMetadata(object d) : base(d) { }
        public FrameworkPropertyMetadata(object d, FrameworkPropertyMetadataOptions o) : base(d) { }
        public FrameworkPropertyMetadata(object d, FrameworkPropertyMetadataOptions o, PropertyChangedCallback cb) : base(d) { }
    }

    public struct DependencyPropertyChangedEventArgs
    {
        public object NewValue, OldValue;
        public DependencyProperty Property;
    }
    public delegate void DependencyPropertyChangedEventHandler(object sender, DependencyPropertyChangedEventArgs e);

    public class RoutedEventArgs : EventArgs
    {
        public object OriginalSource, Source;
        public bool Handled;
    }
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);

    public class ResourceDictionary : IEnumerable
    {
        public Collection<ResourceDictionary> MergedDictionaries = new Collection<ResourceDictionary>();
        public object Source;
        public object this[object key] { get { return null; } set { } }
        public bool Contains(object key) { return false; }
        public IEnumerator GetEnumerator() { return null; }
    }
    public class Collection<T> : List<T> { }

    public class FrameworkElement : System.Windows.Media.Visual
    {
        public double ActualWidth { get { return 0; } }
        public double ActualHeight { get { return 0; } }
        public double Width, Height, MinWidth, MinHeight, MaxWidth, MaxHeight;
        public Thickness Margin;
        public bool Focusable, IsEnabled, IsLoaded, IsVisible, ClipToBounds, SnapsToDevicePixels;
        public object Tag, DataContext, ToolTip, Style, Cursor;
        public string Name;
        public Visibility Visibility;
        public HorizontalAlignment HorizontalAlignment;
        public VerticalAlignment VerticalAlignment;
        public ResourceDictionary Resources = new ResourceDictionary();
        public event DependencyPropertyChangedEventHandler DataContextChanged;
        public event RoutedEventHandler Loaded;
        public event EventHandler SizeChanged;

        public System.Windows.Threading.Dispatcher Dispatcher { get { return null; } }
        public void InvalidateVisual() { }
        public void InvalidateMeasure() { }
        public bool Focus() { return true; }
        public bool CaptureMouse() { return true; }
        public void ReleaseMouseCapture() { }
        public object FindResource(object key) { return null; }
        public object TryFindResource(object key) { return null; }
        protected virtual void OnRender(System.Windows.Media.DrawingContext dc) { }
        protected virtual void OnMouseMove(System.Windows.Input.MouseEventArgs e) { }
        protected virtual void OnMouseLeave(System.Windows.Input.MouseEventArgs e) { }
        protected virtual void OnMouseEnter(System.Windows.Input.MouseEventArgs e) { }
        protected virtual void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e) { }
        protected virtual void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e) { }
        protected virtual void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) { }
        protected virtual void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) { }
        protected virtual void OnMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e) { }
        protected virtual void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e) { }
        protected virtual void OnKeyDown(System.Windows.Input.KeyEventArgs e) { }
        protected virtual void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e) { }
        protected virtual void OnKeyUp(System.Windows.Input.KeyEventArgs e) { }
        protected virtual void OnTextInput(object e) { }
        protected virtual void OnDragEnter(DragEventArgs e) { }
        protected virtual void OnDragOver(DragEventArgs e) { }
        protected virtual void OnDragLeave(DragEventArgs e) { }
        protected virtual void OnDrop(DragEventArgs e) { }
        protected virtual Size MeasureOverride(Size available) { return new Size(); }
        protected virtual Size ArrangeOverride(Size final) { return new Size(); }
    }

    public class Window : FrameworkElement
    {
        public string Title;
        public Window Owner;
        public WindowState WindowState;
        public WindowStartupLocation WindowStartupLocation;
        public SizeToContent SizeToContent;
        public ResizeMode ResizeMode;
        public bool ShowInTaskbar, Topmost;
        public bool? DialogResult;
        public object Content, Icon;
        public double Left, Top;
        public event EventHandler Activated;
        public event EventHandler Closed;
        public event System.ComponentModel.CancelEventHandler Closing;

        public void Show() { }
        public bool? ShowDialog() { return true; }
        public void Close() { }
        public void Hide() { }
        public void DragMove() { }
        public static Window GetWindow(DependencyObject d) { return null; }
        protected virtual void OnClosing(System.ComponentModel.CancelEventArgs e) { }
        protected virtual void OnClosed(EventArgs e) { }
        protected virtual void OnSourceInitialized(EventArgs e) { }
    }

    public partial class Application
    {
        public static Application Current { get { return null; } }
        public ResourceDictionary Resources = new ResourceDictionary();
        public Window MainWindow;
        public string StartupUri;
        public event EventHandler Startup;
        public int Run() { return 0; }
        public int Run(Window w) { return 0; }
        public void Shutdown() { }
        protected virtual void OnStartup(StartupEventArgs e) { }
        protected virtual void OnExit(ExitEventArgs e) { }
        public static void LoadComponent(object c, Uri u) { }
    }
    public class StartupEventArgs : EventArgs { public string[] Args; }
    public class ExitEventArgs : EventArgs { public int ApplicationExitCode; }

    public static class MessageBox
    {
        public static MessageBoxResult Show(string text) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string text, string caption) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string t, string c, MessageBoxButton b) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(string t, string c, MessageBoxButton b, MessageBoxImage i) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(Window o, string t) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(Window o, string t, string c) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(Window o, string t, string c, MessageBoxButton b) { return MessageBoxResult.OK; }
        public static MessageBoxResult Show(Window o, string t, string c, MessageBoxButton b, MessageBoxImage i) { return MessageBoxResult.OK; }
    }

    public class DataObject
    {
        public DataObject() { }
        public DataObject(string format, object data) { }
        public DataObject(object data) { }
        public object GetData(string format) { return null; }
        public bool GetDataPresent(string format) { return false; }
        public void SetData(string format, object data) { }
    }
    [Flags]
    public enum DragDropEffects { None = 0, Copy = 1, Move = 2, Link = 4, All = 7 }
    public class DragEventArgs : RoutedEventArgs
    {
        public DataObject Data;
        public DragDropEffects Effects, AllowedEffects;
        public Point GetPosition(FrameworkElement rel) { return new Point(); }
    }
    public delegate void DragEventHandler(object sender, DragEventArgs e);
    public static class DragDrop
    {
        public static DragDropEffects DoDragDrop(DependencyObject src, object data, DragDropEffects allowed) { return DragDropEffects.None; }
    }
}
