namespace Jumbo.Controls;

public partial class TigerCoach : ContentView
{
    public static readonly BindableProperty MoodProperty = BindableProperty.Create(
        nameof(Mood), typeof(string), typeof(TigerCoach), "idle", propertyChanged: (b, _, n) => ((TigerCoach)b).Tiger.Mood = (string)n);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TigerCoach), string.Empty, propertyChanged: (b, _, n) =>
        {
            var c = (TigerCoach)b;
            var text = (string?)n ?? string.Empty;
            if (text.Length > 0) c.Message.Text = text;     // keep the last phrase while the bubble hides
            c.Bubble.IsVisible = text.Length > 0;
        });

    public string Mood { get => (string)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public TigerCoach() => InitializeComponent();
}
