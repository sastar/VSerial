using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSerial.UI.Converters;

/// <summary>
/// 布尔值转换为 Visibility，支持 "Inverse" 参数
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool bValue = false;

        if (value is bool b)
        {
            bValue = b;
        }
        else if (value is int intVal)
        {
            bValue = intVal > 0;
        }
        else if (value is long longVal)
        {
            bValue = longVal > 0;
        }
        else if (value is string str)
        {
            bValue = !string.IsNullOrWhiteSpace(str);
        }
        else if (value != null)
        {
            bValue = true;
        }

        if (parameter is string paramStr && string.Equals(paramStr, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            bValue = !bValue;
        }

        return bValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            bool result = vis == Visibility.Visible;
            if (parameter is string paramStr && string.Equals(paramStr, "Inverse", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }
            return result;
        }

        return false;
    }
}
