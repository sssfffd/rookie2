// WPF 스텁 2부 — Media / Input / Controls / Data / Threading.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace System.Windows.Media
{
    public struct Color
    {
        public byte A, R, G, B;
        public static Color FromRgb(byte r, byte g, byte b) { return new Color(); }
        public static Color FromArgb(byte a, byte r, byte g, byte b) { return new Color(); }
    }
    public static class Colors
    {
        public static Color White, Black, Red, Green, Blue, Transparent, Gray, Orange, Yellow;
    }
    public class Freezable { public void Freeze() { } public bool IsFrozen { get { return true; } } }
    public class Brush : Freezable { public double Opacity; }
    public class SolidColorBrush : Brush
    {
        public Color Color;
        public SolidColorBrush() { }
        public SolidColorBrush(Color c) { Color = c; }
    }
    public static class Brushes { public static Brush White, Black, Red, Transparent, Gray; }
    public enum PenLineJoin { Miter, Bevel, Round }
    public enum PenLineCap { Flat, Square, Round, Triangle }
    public class Pen : Freezable
    {
        public Brush Brush; public double Thickness;
        public PenLineJoin LineJoin;
        public PenLineCap StartLineCap, EndLineCap, DashCap;
        public object DashStyle;
        public Pen() { }
        public Pen(Brush b, double t) { Brush = b; Thickness = t; }
    }
    public class Typeface
    {
        public Typeface(string family) { }
    }
    public class FontFamily { public FontFamily(string n) { } }

    public class Geometry : Freezable { }
    public class RectangleGeometry : Geometry
    {
        public RectangleGeometry() { }
        public RectangleGeometry(Rect r) { }
    }
    public class StreamGeometry : Geometry
    {
        public StreamGeometryContext Open() { return new StreamGeometryContext(); }
    }
    public class StreamGeometryContext : IDisposable
    {
        public void BeginFigure(Point start, bool filled, bool closed) { }
        public void LineTo(Point p, bool stroked, bool smooth) { }
        public void PolyLineTo(IList<Point> pts, bool stroked, bool smooth) { }
        public void Dispose() { }
    }

    public class FormattedText
    {
        public FormattedText(string text, CultureInfo c, FlowDirection f, Typeface t, double size, Brush b) { }
        public FormattedText(string text, CultureInfo c, FlowDirection f, Typeface t, double size, Brush b, double pixelsPerDip) { }
        public double Width { get { return 0; } }
        public double Height { get { return 0; } }
        public double MaxTextWidth { get; set; }
        public int MaxLineCount { get; set; }
        public TextTrimming Trimming { get; set; }
        public void SetFontWeight(FontWeight w) { }
        public void SetForegroundBrush(Brush b) { }
    }

    public class DrawingContext : IDisposable
    {
        public void DrawRectangle(Brush b, Pen p, Rect r) { }
        public void DrawLine(Pen p, Point a, Point b) { }
        public void DrawText(FormattedText t, Point at) { }
        public void DrawGeometry(Brush b, Pen p, Geometry g) { }
        public void DrawEllipse(Brush b, Pen p, Point c, double rx, double ry) { }
        public void PushClip(Geometry g) { }
        public void PushOpacity(double o) { }
        public void PushTransform(Transform t) { }
        public void Pop() { }
        public void Close() { }
        public void Dispose() { }
    }

    public class Transform : Freezable { }
    public class TranslateTransform : Transform { public TranslateTransform(double x, double y) { } }
    public class ScaleTransform : Transform { public ScaleTransform(double x, double y) { } }

    public class Visual : DependencyObject { }
    public static class VisualTreeHelper
    {
        public static int GetChildrenCount(DependencyObject d) { return 0; }
        public static DependencyObject GetChild(DependencyObject d, int i) { return null; }
        public static DependencyObject GetParent(DependencyObject d) { return null; }
    }

    public enum EdgeMode { Unspecified, Aliased }
    public static class RenderOptions
    {
        public static void SetEdgeMode(DependencyObject d, EdgeMode m) { }
        public static EdgeMode GetEdgeMode(DependencyObject d) { return EdgeMode.Unspecified; }
    }
    public static class CompositionTarget
    {
        public static event EventHandler Rendering;
    }
    public struct Matrix { public double M11, M12, M21, M22, OffsetX, OffsetY; }
}

namespace System.Windows.Input
{
    public enum Key { None, Escape, Enter, Space, Tab, Delete, Back, Left, Right, Up, Down, Home, End,
        PageUp, PageDown, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, OemPlus, OemMinus,
        Add, Subtract, Multiply, Divide, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9 }
    [Flags]
    public enum ModifierKeys { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }
    public enum MouseButtonState { Released, Pressed }

    public class InputEventArgs : RoutedEventArgs { }
    public class MouseEventArgs : InputEventArgs
    {
        public MouseButtonState LeftButton, RightButton, MiddleButton;
        public Point GetPosition(FrameworkElement rel) { return new Point(); }
    }
    public class MouseButtonEventArgs : MouseEventArgs { public int ClickCount; }
    public class MouseWheelEventArgs : MouseEventArgs { public int Delta; }
    public class KeyEventArgs : InputEventArgs { public Key Key; public Key SystemKey; }
    public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);

    public static class Keyboard
    {
        public static ModifierKeys Modifiers { get { return ModifierKeys.None; } }
        public static bool IsKeyDown(Key k) { return false; }
    }
    public static class Mouse
    {
        public static MouseButtonState LeftButton { get { return MouseButtonState.Released; } }
        public static Point GetPosition(FrameworkElement rel) { return new Point(); }
    }
    public static class Cursors { public static object Arrow, Hand, IBeam, Wait, SizeWE, SizeNS; }

    public static class CommandManager
    {
        public static event EventHandler RequerySuggested;
        public static void InvalidateRequerySuggested() { }
    }
}

namespace System.Windows.Threading
{
    public enum DispatcherPriority { Normal, Background, Render, Input, Loaded, ApplicationIdle, SystemIdle, Send }
    public class Dispatcher
    {
        public object BeginInvoke(Delegate d) { return null; }
        public object BeginInvoke(DispatcherPriority p, Delegate d) { return null; }
        public object BeginInvoke(Delegate d, params object[] args) { return null; }
        public object Invoke(Delegate d) { return null; }
        public bool CheckAccess() { return true; }
    }
    public class DispatcherTimer
    {
        public TimeSpan Interval;
        public bool IsEnabled;
        public event EventHandler Tick;
        public void Start() { }
        public void Stop() { }
    }
}
