using System.Globalization;
using System.Windows.Data;

namespace Raisin.WPF.Base;

public class LargeNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var number = value switch
        {
            long l => l,
            int i => (long)i,
            double d => (long)Math.Round(d),
            decimal m => (long)Math.Round(m),
            _ => 0L,
        };

        if (number == 0) return "";

        return number switch
        {
            >= 1_000_000_000 or <= -1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 or <= -1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 or <= -1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0"),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
