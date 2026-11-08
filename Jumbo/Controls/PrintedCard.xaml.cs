namespace Jumbo.Controls;

/// <summary>The face of a printed card: level + number on the top, the picture, the wave with the word (see PrintedCard.xaml).</summary>
public partial class PrintedCard : ContentView
{
    public static readonly BindableProperty LevelProperty = BindableProperty.Create(nameof(Level), typeof(string), typeof(PrintedCard), string.Empty,
        propertyChanged: (b, _, n) => ((PrintedCard)b).LevelLabel.Text = (string?)n);
    public static readonly BindableProperty NumberProperty = BindableProperty.Create(nameof(Number), typeof(int), typeof(PrintedCard), 0,
        propertyChanged: (b, _, n) => { var c = (PrintedCard)b; c.NumberLabel.Text = ((int)n).ToString(); c.NumberBadge.IsVisible = (int)n > 0; });
    public static readonly BindableProperty SvgProperty = BindableProperty.Create(nameof(Svg), typeof(string), typeof(PrintedCard), string.Empty,
        propertyChanged: (b, _, n) => ((PrintedCard)b).Picture.Svg = (string?)n ?? string.Empty);
    public static readonly BindableProperty WordProperty = BindableProperty.Create(nameof(Word), typeof(string), typeof(PrintedCard), string.Empty,
        propertyChanged: (b, _, n) => ((PrintedCard)b).WordText.Text = (string?)n ?? string.Empty);

    public string Level { get => (string)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public int Number { get => (int)GetValue(NumberProperty); set => SetValue(NumberProperty, value); }
    public string Svg { get => (string)GetValue(SvgProperty); set => SetValue(SvgProperty, value); }
    public string Word { get => (string)GetValue(WordProperty); set => SetValue(WordProperty, value); }

    public PrintedCard() => InitializeComponent();
}
