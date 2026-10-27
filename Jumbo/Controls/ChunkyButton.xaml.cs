using Jumbo.Services;
using System.Windows.Input;

namespace Jumbo.Controls;

public partial class ChunkyButton : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(ChunkyButton), string.Empty, propertyChanged: (b, _, n) => ((ChunkyButton)b).Caption.Text = (string?)n);

    /// <summary>primary | success | accent | danger | outline</summary>
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(string), typeof(ChunkyButton), "primary", propertyChanged: (b, _, _) => ((ChunkyButton)b).ApplyColors());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ChunkyButton), null, propertyChanged: (b, o, n) => ((ChunkyButton)b).OnCommandChanged(o as ICommand, n as ICommand));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(ChunkyButton));

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(ChunkyButton), 16d, propertyChanged: (b, _, n) => ((ChunkyButton)b).Caption.FontSize = (double)n);

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public event EventHandler? Clicked;

    public ChunkyButton()
    {
        InitializeComponent();
        ApplyColors();
        Loaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged += OnThemeChanged; ThemePalette.Changed += OnPaletteChanged; ApplyColors(); };
        Unloaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged -= OnThemeChanged; ThemePalette.Changed -= OnPaletteChanged; };
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsEnabledProperty.PropertyName) ApplyColors();
        if (propertyName == HeightRequestProperty.PropertyName && HeightRequest > 0) Root.HeightRequest = HeightRequest;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => ApplyColors();

    private void OnPaletteChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ApplyColors);

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void ApplyColors()
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Color face, edge, text;

        if (!IsEnabled)
        {
            face = Res(dark ? "BorderDark" : "BorderLight");
            edge = Res(dark ? "SurfaceDark" : "DisabledTextLight");
            text = Res(dark ? "MutedDark" : "MutedLight");
        }
        else
        {
            (face, edge, text) = Kind switch
            {
                "success" => (Res("Success"), Res("SuccessEdge"), Colors.White),
                "accent" => (Res("Accent"), Res("AccentEdge"), Colors.White),
                "danger" => (Res("Danger"), Res("DangerEdge"), Colors.White),
                "outline" => (Res(dark ? "SurfaceDark" : "SurfaceLight"), Res(dark ? "BorderDark" : "BorderLight"), Res("PrimaryText")),
                _ => (Res("Primary"), Res("PrimaryEdge"), Res("OnPrimary"))
            };
        }

        Face.BackgroundColor = face;
        Edge.BackgroundColor = edge;
        Caption.TextColor = text;
        if (Kind == "outline" && IsEnabled)
        {
            Face.Stroke = new SolidColorBrush(edge);
            Face.StrokeThickness = 2;
        }
        else
        {
            Face.StrokeThickness = 0;
        }
    }

    private void OnCommandChanged(ICommand? oldCommand, ICommand? newCommand)
    {
        if (oldCommand is not null) oldCommand.CanExecuteChanged -= OnCanExecuteChanged;
        if (newCommand is not null) newCommand.CanExecuteChanged += OnCanExecuteChanged;
        OnCanExecuteChanged(null, EventArgs.Empty);
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        if (Command is { } c) IsEnabled = c.CanExecute(CommandParameter);
    }

    private async void OnPressed(object? sender, EventArgs e) => await Face.TranslateToAsync(0, 4, 50);

    private async void OnReleased(object? sender, EventArgs e) => await Face.TranslateToAsync(0, 0, 80);

    private void OnClicked(object? sender, EventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command is { } c && c.CanExecute(CommandParameter)) c.Execute(CommandParameter);
    }
}
