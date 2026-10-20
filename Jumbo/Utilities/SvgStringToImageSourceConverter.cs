using System.Globalization;


namespace Jumbo.Utilities
{
    public class SvgStringToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string svgText && !string.IsNullOrWhiteSpace(svgText))
            {
                // Корректно экранируем спецсимволы и переводим в Base64
                var plainBytes = System.Text.Encoding.UTF8.GetBytes(svgText);
                var base64String = System.Convert.ToBase64String(plainBytes);

                // Формируем data-url, который встроенный ImageSource отлично считывает на всех платформах
                return ImageSource.FromUri(new Uri($"data:image/svg+xml;base64,{base64String}"));
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
