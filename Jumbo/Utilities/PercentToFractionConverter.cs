using System.Globalization;

namespace Jumbo.Utilities;

/// <summary>0..100 (int) -> 0..1 (double) for ProgressBar.</summary>
public class PercentToFractionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? Math.Clamp(i / 100.0, 0, 1) : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
