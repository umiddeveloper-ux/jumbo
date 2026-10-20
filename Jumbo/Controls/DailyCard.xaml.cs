using Jumbo.Services;
using Jumbo.Shared.Dtos;

namespace Jumbo.Controls;

public partial class DailyCard : ContentView
{
    public static readonly BindableProperty DataProperty = BindableProperty.Create(
        nameof(Data), typeof(DailyProgressDto), typeof(DailyCard), null, propertyChanged: (b, _, _) => ((DailyCard)b).Render());

    private readonly RingDrawable _ring = new();

    public DailyProgressDto? Data { get => (DailyProgressDto?)GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public DailyCard()
    {
        InitializeComponent();
        Ring.Drawable = _ring;
        IsVisible = false;
        Loaded += (_, _) =>
        {
            LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
            if (Application.Current is { } app) app.RequestedThemeChanged += OnThemeChanged;
        };
        Unloaded += (_, _) =>
        {
            LocalizationManager.Instance.PropertyChanged -= OnLanguageChanged;
            if (Application.Current is { } app) app.RequestedThemeChanged -= OnThemeChanged;
        };
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Render();

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Render();

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void Render()
    {
        var d = Data;
        IsVisible = d is not null;
        if (d is null) return;

        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        double pct = d.GoalWords > 0 ? Math.Min(1.0, (double)d.TodayWords / d.GoalWords) : 0;

        _ring.Progress = pct;
        _ring.TrackColor = Res(dark ? "BorderDark" : "BorderLight");
        _ring.FillColor = d.GoalMetToday ? Res("Success") : Res("Primary");
        Ring.Invalidate();

        RingLabel.Text = d.GoalMetToday ? "✓" : d.TodayWords.ToString();
        RingLabel.TextColor = d.GoalMetToday ? Res(dark ? "SuccessTextDark" : "SuccessText") : Res(dark ? "LightOnDarkBackground" : "DarkOnLightBackground");

        ProgressLabel.Text = LocalizationManager.Format("Daily_Progress", Math.Min(d.TodayWords, d.GoalWords), d.GoalWords);

        if (d.GoalMetToday)
        {
            MessageLabel.Text = LocalizationManager.Get("Daily_GoalDone");
            MessageLabel.TextColor = Res(dark ? "SuccessTextDark" : "SuccessText");
        }
        else if (d.StreakAtRisk)
        {
            MessageLabel.Text = LocalizationManager.Get("Daily_AtRisk");
            MessageLabel.TextColor = Res("Accent");
        }
        else if (d.TodayWords == 0)
        {
            MessageLabel.Text = LocalizationManager.Get("Daily_StartPrompt");
            MessageLabel.TextColor = Res("Primary");
        }
        else
        {
            MessageLabel.Text = string.Empty;
        }

        StreakLabel.Text = d.Streak.ToString();
        StreakLabel.TextColor = d.Streak > 0 ? Res("Accent") : Res(dark ? "MutedDark" : "MutedLight");
        Flame.Opacity = d.Streak > 0 ? 1 : 0.35;

        RenderWeek(d, dark);
    }

    private void RenderWeek(DailyProgressDto d, bool dark)
    {
        WeekGrid.Children.Clear();
        WeekGrid.ColumnDefinitions.Clear();
        for (int i = 0; i < d.Week.Count; i++)
        {
            var day = d.Week[i];
            bool today = i == d.Week.Count - 1;
            WeekGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var dot = new Border
            {
                WidthRequest = 28,
                HeightRequest = 28,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                StrokeThickness = today && !day.GoalMet ? 2 : 0,
                Stroke = new SolidColorBrush(Res("Primary")),
                BackgroundColor = day.GoalMet ? Res("Success") : Res(dark ? "BorderDark" : "BorderLight"),
                HorizontalOptions = LayoutOptions.Center,
                Padding = 0,
                Content = new Label
                {
                    Text = day.GoalMet ? "✓" : string.Empty,
                    TextColor = Colors.White,
                    FontFamily = "NunitoBlack",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var name = new Label
            {
                Text = LocalizationManager.Get($"Day_{day.Date.DayOfWeek}"),
                FontSize = 11,
                FontFamily = today ? "NunitoExtraBold" : "NunitoBold",
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = today ? Res("Primary") : Res(dark ? "MutedDark" : "MutedLight")
            };
            var cell = new VerticalStackLayout { Spacing = 3, Children = { dot, name } };
            Grid.SetColumn(cell, i);
            WeekGrid.Children.Add(cell);
        }
    }

    private sealed class RingDrawable : IDrawable
    {
        public double Progress { get; set; }
        public Color TrackColor { get; set; } = Colors.LightGray;
        public Color FillColor { get; set; } = Colors.DodgerBlue;

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float stroke = 8f;
            var r = new RectF(rect.X + stroke / 2, rect.Y + stroke / 2, rect.Width - stroke, rect.Height - stroke);
            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;

            canvas.StrokeColor = TrackColor;
            canvas.DrawEllipse(r);

            if (Progress <= 0) return;
            canvas.StrokeColor = FillColor;
            if (Progress >= 0.999) { canvas.DrawEllipse(r); return; }

            // Start at 12 o'clock and go clockwise; MAUI measures angles counter-clockwise from 3 o'clock.
            float start = 90f;
            float end = 90f - (float)(Progress * 360.0);
            canvas.DrawArc(r, start, end, true, false);
        }
    }
}
