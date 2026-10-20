using Jumbo.Services;
using System.Globalization;

namespace Jumbo.Utilities;

/// <summary>Binding value -> localized text with that value inserted: ConverterParameter = resource key ("Letter_Heading" = "Letter “{0}”").</summary>
public class TrFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter is string key ? LocalizationManager.Format(key, value ?? string.Empty) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
