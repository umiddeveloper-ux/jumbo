using Microsoft.Maui.Layouts;
using Microsoft.Maui.Controls.Shapes;
using System.Collections;

namespace Jumbo.Controls;

public record ChipItem(string Key, string Text);

/// <summary>A row of selectable chips (the website's goal / theme / language chips). Two-way <see cref="SelectedKey"/>.</summary>
public class ChipGroup : FlexLayout
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IList), typeof(ChipGroup), null, propertyChanged: (b, _, _) => ((ChipGroup)b).Render());

    public static readonly BindableProperty SelectedKeyProperty = BindableProperty.Create(
        nameof(SelectedKey), typeof(string), typeof(ChipGroup), string.Empty, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((ChipGroup)b).Render());

    public IList? Items { get => (IList?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string SelectedKey { get => (string)GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }

    public ChipGroup()
    {
        Wrap = FlexWrap.Wrap;
        Direction = FlexDirection.Row;
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => Render();
    }

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void Render()
    {
        Children.Clear();
        if (Items is null) return;
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        foreach (var obj in Items)
        {
            if (obj is not ChipItem item) continue;
            bool on = item.Key == SelectedKey;

            var chip = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                StrokeThickness = 2,
                Padding = new Thickness(16, 0),
                HeightRequest = 44,
                Margin = new Thickness(0, 0, 8, 8),
                MinimumWidthRequest = 56,
                Stroke = new SolidColorBrush(on ? Res("Primary") : Res(dark ? "BorderDark" : "BorderLight")),
                BackgroundColor = on ? Res(dark ? "PrimarySoftDark" : "PrimarySoftLight") : Res(dark ? "SurfaceDark" : "SurfaceLight"),
                Content = new Label
                {
                    Text = item.Text,
                    FontFamily = "NunitoExtraBold",
                    FontSize = 15,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = on ? Res("Primary") : Res(dark ? "LightOnDarkBackground" : "DarkOnLightBackground")
                }
            };
            var key = item.Key;
            chip.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SelectedKey = key) });
            Children.Add(chip);
        }
    }
}
