using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>
/// The learner's avatar: a preset picture (svg text) or an uploaded photo (a data: url), and the first letter of the name while there is
/// none. The pictures are rounded squares themselves, so the frame only rounds a photo to the same shape.
/// </summary>
public class AvatarView : Border
{
    public static readonly BindableProperty AvatarProperty = BindableProperty.Create(
        nameof(Avatar), typeof(string), typeof(AvatarView), null, propertyChanged: (b, _, _) => ((AvatarView)b).Refresh());
    public static readonly BindableProperty InitialProperty = BindableProperty.Create(
        nameof(Initial), typeof(string), typeof(AvatarView), string.Empty, propertyChanged: (b, _, _) => ((AvatarView)b).Refresh());
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(AvatarView), 34d, propertyChanged: (b, _, n) => ((AvatarView)b).Resize((double)n));

    public string? Avatar { get => (string?)GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public string Initial { get => (string)GetValue(InitialProperty); set => SetValue(InitialProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private readonly SvgPicture _svg = new() { RenderSize = 160 };
    private readonly Image _photo = new() { Aspect = Aspect.AspectFill, InputTransparent = true };
    private readonly Label _letter = new() { FontFamily = "NunitoBlack", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = Colors.White };

    public AvatarView()
    {
        StrokeThickness = 0;
        Padding = 0;
        BackgroundColor = Colors.Transparent;
        Content = new Grid { Children = { _svg, _photo, _letter } };
        Resize(Size);
        Refresh();
    }

    private void Resize(double size)
    {
        WidthRequest = HeightRequest = size;
        StrokeShape = new RoundRectangle { CornerRadius = size * 0.24 };
        _letter.FontSize = size * 0.5;
    }

    private void Refresh()
    {
        var a = Avatar;
        bool photo = a is { Length: > 0 } && a.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        bool svg = a is { Length: > 0 } && !photo;

        _svg.IsVisible = svg;
        _svg.Svg = svg ? a! : string.Empty;
        _photo.IsVisible = photo;
        _photo.Source = photo ? FromDataUrl(a!) : null;

        _letter.IsVisible = !svg && !photo;
        _letter.Text = string.IsNullOrWhiteSpace(Initial) ? "?" : Initial.Trim()[..1].ToUpperInvariant();
        BackgroundColor = _letter.IsVisible ? (Color)Application.Current!.Resources["Primary"] : Colors.Transparent;
    }

    private static ImageSource? FromDataUrl(string url)
    {
        try
        {
            int comma = url.IndexOf(',');
            if (comma < 0) return null;
            var bytes = Convert.FromBase64String(url[(comma + 1)..]);
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch { return null; }
    }
}
