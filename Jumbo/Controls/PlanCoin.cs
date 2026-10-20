using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>The coin that tells the version of the app: bronze, silver or gold (a star on a round coin).</summary>
public class PlanCoin : Border
{
    public static readonly BindableProperty PlanProperty = BindableProperty.Create(
        nameof(Plan), typeof(string), typeof(PlanCoin), Jumbo.Shared.Plans.Bronze, propertyChanged: (b, _, _) => ((PlanCoin)b).Refresh());
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(PlanCoin), 18d, propertyChanged: (b, _, _) => ((PlanCoin)b).Resize());

    public string Plan { get => (string)GetValue(PlanProperty); set => SetValue(PlanProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private readonly Label _star = new() { Text = "★", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

    public PlanCoin()
    {
        Padding = 0;
        StrokeThickness = 2;
        Content = _star;
        Resize();
        Refresh();
    }

    private void Resize()
    {
        WidthRequest = HeightRequest = Size;
        StrokeShape = new RoundRectangle { CornerRadius = Size / 2 };
        _star.FontSize = Size * 0.62;
    }

    private void Refresh()
    {
        var (fill, edge) = Jumbo.Shared.Plans.Normalize(Plan) switch
        {
            Jumbo.Shared.Plans.Gold => ("#FFC800", "#E5A100"),
            Jumbo.Shared.Plans.Silver => ("#C9D1DB", "#8A94A3"),
            _ => ("#CD7F32", "#8C5520"),
        };
        BackgroundColor = Color.FromArgb(fill);
        Stroke = new SolidColorBrush(Color.FromArgb(edge));
    }
}
