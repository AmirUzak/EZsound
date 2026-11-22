using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EZsound.Converters;

/// <summary>
/// Converts a boolean value to a Visibility value.
/// true → Visible, false → Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // If parameter is "Invert", invert the logic
            if (parameter is string param && param == "Invert")
                return boolValue ? Visibility.Collapsed : Visibility.Visible;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

/// <summary>
/// Converts a boolean (IsMuted) to an opacity value.
/// Muted → 0.5, Unmuted → 1.0.
/// </summary>
public class MuteToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isMuted)
        {
            return isMuted ? 0.5 : 1.0;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
