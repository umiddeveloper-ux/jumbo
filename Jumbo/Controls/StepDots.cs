using Jumbo.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>The wizard's step indicator: numbered dots (✓ when done) with a label under each, like the website's.</summary>
public class StepDots : Grid
{
    public static readonly BindableProperty StepProperty = BindableProperty.Create(
        nameof(Step), typeof(int), typeof(StepDots), 1, propertyChanged: (b, _, _) => ((StepDots)b).Render());

    /// <summary>Resource keys of the labels, one per step.</summary>
    public static readonly BindableProperty LabelKeysProperty = BindableProperty.Create(
        nameof(LabelKeys), typeof(string), typeof(StepDots), "Reg_Step1,Reg_Step2,Reg_Step3", propertyChanged: (b, _, _) => ((StepDots)b).Render());

    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string LabelKeys { get => (string)GetValue(LabelKeysProperty); set => SetValue(LabelKeysProperty, value); }

    public StepDots()
    {
        Loaded += (_, _) => { LocalizationManager.Instance.PropertyChanged += OnChanged; Render(); };
        Unloaded += (_, _) => LocalizationManager.Instance.PropertyChanged -= OnChanged;
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => Render();
    }

    private void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(Render);

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void Render()
    {
        var keys = LabelKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        Children.Clear();
        ColumnDefinitions.Clear();
        ColumnSpacing = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            int n = i + 1;
            bool done = n < Step, active = n == Step;
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var dot = new Border
            {
                WidthRequest = 32,
                HeightRequest = 32,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                StrokeThickness = active ? 3 : 0,
                Stroke = new SolidColorBrush(Res("PrimaryEdgeLight")),
                Padding = 0,
                HorizontalOptions = LayoutOptions.Center,
                BackgroundColor = done ? Res("Success") : active ? Res("Primary") : Res(dark ? "BorderDark" : "BorderLight"),
                Content = new Label
                {
                    Text = done ? "✓" : n.ToString(),
                    TextColor = done || active ? Colors.White : Res(dark ? "MutedDark" : "MutedLight"),
                    FontFamily = "NunitoBlack",
                    FontSize = 15,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var label = new Label
            {
                Text = LocalizationManager.Get(keys[i]),
                FontFamily = active ? "NunitoExtraBold" : "NunitoBold",
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = active ? Res("Primary") : Res(dark ? "MutedDark" : "MutedLight")
            };
            var cell = new VerticalStackLayout { Spacing = 4, Children = { dot, label } };
            Grid.SetColumn((BindableObject)cell, i);
            Children.Add(cell);
        }
    }
}
