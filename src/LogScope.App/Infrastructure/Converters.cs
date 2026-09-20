using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogScope.App.Infrastructure
{
    /// <summary>참이면 보이고 거짓이면 자리까지 없앱니다.</summary>
    public sealed class BoolToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool on = value is bool && (bool)value;
            if (parameter as string == "invert") on = !on;
            return on ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    /// <summary>개수가 0 일 때만 보입니다. "없음" 안내 글에 씁니다.</summary>
    public sealed class ZeroToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int n = 0;
            if (value is int) n = (int)value;
            else if (value != null) int.TryParse(value.ToString(), out n);
            return n == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>글자가 비어 있지 않을 때만 보입니다.</summary>
    public sealed class TextToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
