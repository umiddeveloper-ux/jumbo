using System.Globalization;

namespace Jumbo.Utilities;

public class RepetitionLevelToColorConverter : IValueConverter
{
    public static Color GetColor(int level) => level switch
    {
        0 => Color.FromArgb("#FFDDDD"),
        1 => Color.FromArgb("#FFF8CC"),
        2 => Color.FromArgb("#E4E4E4"),
        >= 3 => Color.FromArgb("#CCEDD4"),
        _ => Color.FromArgb("#F0F0F0")
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        GetColor(value is int i ? i : -1);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
