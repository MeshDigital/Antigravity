using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts a value to Visibility: returns Visible when value is NULL, Collapsed when NOT NULL.
/// Used to show DataGrid when no import selected and hide it when one is selected.
/// </summary>
public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
