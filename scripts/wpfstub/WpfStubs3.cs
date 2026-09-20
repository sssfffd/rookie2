// WPF 스텁 3부 — Controls / Data / Markup / Interop / Win32 대화상자.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace System.Windows.Controls
{
    public class Control : FrameworkElement
    {
        public Brush Background, Foreground, BorderBrush;
        public Thickness Padding, BorderThickness;
        public double FontSize;
        public FontFamily FontFamily;
        public FontWeight FontWeight;
        public object Template;
        public HorizontalAlignment HorizontalContentAlignment;
        public VerticalAlignment VerticalContentAlignment;
    }
    public class ContentControl : Control { public object Content; }
    public class UserControl : ContentControl { }
    public class Panel : FrameworkElement { public UIElementCollection Children = new UIElementCollection(); public Brush Background; }
    public class UIElementCollection : List<FrameworkElement> { }
    public class Grid : Panel { }
    public class StackPanel : Panel { }
    public class WrapPanel : Panel { }
    public class DockPanel : Panel { }
    public class Canvas : Panel { }
    public class Border : FrameworkElement { public Brush Background, BorderBrush; public Thickness BorderThickness, Padding; public object Child; }
    public class TextBlock : FrameworkElement
    {
        public string Text;
        public Brush Foreground, Background;
        public double FontSize;
        public TextWrapping TextWrapping;
        public TextTrimming TextTrimming;
    }
    public class TextBox : Control
    {
        public string Text;
        public int CaretIndex, SelectionStart, SelectionLength;
        public bool IsReadOnly, AcceptsReturn;
        public event EventHandler TextChanged;
        public void SelectAll() { }
    }
    public class Button : ContentControl { public event RoutedEventHandler Click; }
    public class ScrollBar : Control
    {
        public double Minimum, Maximum, Value, ViewportSize, SmallChange, LargeChange;
        public object Orientation;
    }
    public class ScrollViewer : ContentControl
    {
        public double HorizontalOffset, VerticalOffset, ScrollableWidth, ScrollableHeight;
        public void ScrollToVerticalOffset(double v) { }
    }
    public class ItemsControl : Control
    {
        public IEnumerable ItemsSource;
        public ItemCollection Items = new ItemCollection();
        public object ItemTemplate, ItemContainerStyle;
    }
    public class ItemCollection : IEnumerable
    {
        public int Count { get { return 0; } }
        public object this[int i] { get { return null; } }
        public int IndexOf(object o) { return -1; }
        public IEnumerator GetEnumerator() { return null; }
    }
    public class Selector : ItemsControl
    {
        public object SelectedItem;
        public int SelectedIndex;
        public event SelectionChangedEventHandler SelectionChanged;
    }
    public class ListBox : Selector
    {
        public IList SelectedItems { get { return null; } }
        public object SelectionMode;
        public void ScrollIntoView(object item) { }
        public void UnselectAll() { }
    }
    public class ListView : ListBox { public object View; }
    public class ListBoxItem : ContentControl { public bool IsSelected; }
    public class ListViewItem : ListBoxItem { }
    public class ComboBox : Selector { public bool IsEditable, IsDropDownOpen; public string Text; }
    public class ComboBoxItem : ContentControl { }
    public class CheckBox : ContentControl
    {
        public bool? IsChecked;
        public event RoutedEventHandler Checked, Unchecked, Click;
    }
    public class RadioButton : CheckBox { public string GroupName; }
    public class TabControl : Selector { }
    public class TabItem : ContentControl { public object Header; }
    public class GridView { public GridViewColumnCollection Columns = new GridViewColumnCollection(); }
    public class GridViewColumn { public object Header, HeaderTemplate, DisplayMemberBinding; public double Width; }
    public class GridViewColumnCollection : List<GridViewColumn> { }
    public class GridViewColumnHeader : ButtonBase { public object Column; }
    public class ButtonBase : ContentControl { public event RoutedEventHandler Click; }
    public class Image : FrameworkElement { public object Source, Stretch; }
    public class ProgressBar : Control { public double Minimum, Maximum, Value; public bool IsIndeterminate; }
    public class Label : ContentControl { }
    public class Separator : Control { }
    public class Menu : ItemsControl { }
    public class MenuItem : ItemsControl { public object Header; public event RoutedEventHandler Click; }
    public class ToolTip : ContentControl { }
    public class SelectionChangedEventArgs : RoutedEventArgs
    {
        public IList AddedItems { get { return null; } }
        public IList RemovedItems { get { return null; } }
    }
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);
}

namespace System.Windows.Controls.Primitives
{
    public class ToggleButton : System.Windows.Controls.ButtonBase
    {
        public bool? IsChecked;
        public event RoutedEventHandler Checked, Unchecked;
    }
    public class RangeBase : System.Windows.Controls.Control
    {
        public double Minimum, Maximum, Value;
    }
}

namespace System.Windows.Data
{
    public interface IValueConverter
    {
        object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
    }
    public class Binding
    {
        public Binding() { }
        public Binding(string path) { }
        public object Source, Converter, ConverterParameter;
        public string Path, StringFormat;
    }
}

namespace System.Windows.Markup
{
    public interface IComponentConnector { void Connect(int id, object target); void InitializeComponent(); }
}

namespace System.Windows.Interop
{
    public class HwndSource { public IntPtr Handle; public CompositionTargetInfo CompositionTarget; }
    public class CompositionTargetInfo { public System.Windows.Media.Matrix TransformToDevice; }
}

namespace System.Windows
{
    public class PresentationSource
    {
        public System.Windows.Interop.CompositionTargetInfo CompositionTarget;
        public static PresentationSource FromVisual(System.Windows.Media.Visual v) { return null; }
    }
}

namespace Microsoft.Win32
{
    public class FileDialog
    {
        public string FileName, InitialDirectory, Filter, Title, DefaultExt;
        public string[] FileNames { get { return new string[0]; } }
        public bool Multiselect, CheckFileExists, AddExtension;
        public int FilterIndex;
        public bool? ShowDialog() { return true; }
        public bool? ShowDialog(System.Windows.Window owner) { return true; }
    }
    public class OpenFileDialog : FileDialog { }
    public class SaveFileDialog : FileDialog { public bool OverwritePrompt; }
}
