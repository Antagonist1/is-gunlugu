using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IsGunlugu;

/// <summary>True hides the element but keeps its space, so note rows stay aligned with the others.</summary>
public sealed class TrueToHiddenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Hidden : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True removes the element, the opposite of BooleanToVisibilityConverter.</summary>
public sealed class TrueToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
