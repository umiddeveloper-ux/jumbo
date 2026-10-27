using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>A row of the list of the settings: an icon, a title with a short line under it and a chevron. A tap runs the command.</summary>
public class SettingsRow : Border
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(SettingsRow), string.Empty, propertyChanged: (b, _, n) => ((SettingsRow)b)._icon.Text = (string?)n);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(SettingsRow), string.Empty, propertyChanged: (b, _, n) => ((SettingsRow)b)._title.Text = (string?)n);
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(SettingsRow), string.Empty, propertyChanged: (b, _, n) => { var r = (SettingsRow)b; r._sub.Text = (string?)n; r._sub.IsVisible = !string.IsNullOrEmpty((string?)n); });
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SettingsRow));
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SettingsRow));
    public static readonly BindableProperty IsDangerProperty = BindableProperty.Create(
        nameof(IsDanger), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, _) => ((SettingsRow)b).Colour());

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public bool IsDanger { get => (bool)GetValue(IsDangerProperty); set => SetValue(IsDangerProperty, value); }

    private readonly Label _icon = new() { FontSize = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Label _title = new() { FontFamily = "NunitoExtraBold", FontSize = 16, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Label _sub = new() { FontFamily = "NunitoBold", FontSize = 13, IsVisible = false };
    private readonly Label _chevron = new() { Text = "›", FontFamily = "NunitoBlack", FontSize = 26, VerticalOptions = LayoutOptions.Center };
    private readonly Border _iconBox = new() { StrokeThickness = 0, WidthRequest = 42, HeightRequest = 42, StrokeShape = new RoundRectangle { CornerRadius = 14 }, VerticalOptions = LayoutOptions.Center };

    public SettingsRow()
    {
        if (Application.Current?.Resources.TryGetValue("CardStyle", out var style) == true && style is Style s) Style = s;
        StrokeShape = new RoundRectangle { CornerRadius = 18 };
        Padding = new Thickness(14, 12);

        // (a Label inside a fixed-size Border is clipped on Android: the colour box and the icon are layered in a Grid)
        var iconHost = new Grid { WidthRequest = 42, HeightRequest = 42, VerticalOptions = LayoutOptions.Center, Children = { _iconBox, _icon } };
        var text = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Children = { _title, _sub } };
        var grid = new Grid
        {
            ColumnSpacing = 14,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        grid.Add(iconHost, 0);
        grid.Add(text, 1);
        grid.Add(_chevron, 2);
        Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter); };
        GestureRecognizers.Add(tap);

        SemanticProperties.SetDescription(this, null);
        Colour();
        Loaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged += OnTheme; Colour(); };
        Unloaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged -= OnTheme; };
    }

    private void OnTheme(object? sender, AppThemeChangedEventArgs e) => Colour();

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void Colour()
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var muted = Res(dark ? "MutedDark" : "MutedLight");
        _sub.TextColor = muted;
        _chevron.TextColor = muted;
        if (IsDanger)
        {
            _title.TextColor = Res("DangerText");
            _iconBox.BackgroundColor = Res("Danger").WithAlpha(0.14f);
            Stroke = Res("Danger");
        }
        else
        {
            _title.ClearValue(Label.TextColorProperty);       // the default text colour of the theme
            _iconBox.BackgroundColor = Res(dark ? "PrimarySoftDark" : "PrimarySoftLight");
            Stroke = Res(dark ? "BorderDark" : "BorderLight");
        }
    }
}
