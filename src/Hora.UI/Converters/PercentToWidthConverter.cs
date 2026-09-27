using System.Globalization;
using System.Windows.Data;

namespace Hora.UI.Converters;

public sealed class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return Math.Clamp(percent / 100.0 * 220.0, 4.0, 220.0);
        }

        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
