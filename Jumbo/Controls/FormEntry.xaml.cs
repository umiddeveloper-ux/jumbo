namespace Jumbo.Controls;

/// <summary>A labelled, rounded text field (the website's input look): caption above, outlined box, brand-coloured focus ring.</summary>
public partial class FormEntry : ContentView
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(FormEntry), string.Empty, propertyChanged: (b, _, n) => ((FormEntry)b).CaptionLabel.Text = (string?)n);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FormEntry), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => { var e = (FormEntry)b; if (e.Input.Text != (string?)n) e.Input.Text = (string?)n; });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(FormEntry), string.Empty, propertyChanged: (b, _, n) => ((FormEntry)b).Input.Placeholder = (string?)n);

    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
        nameof(IsPassword), typeof(bool), typeof(FormEntry), false, propertyChanged: (b, _, n) =>
        {
            var e = (FormEntry)b;
            e._hidden = (bool)n;                 // a password field starts hidden
            e.Input.IsPassword = (bool)n;
            e.Eye.IsVisible = (bool)n;           // the eye is only for password fields
            e.Eye.Source = "pw_eye.png";
        });

    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(
        nameof(Keyboard), typeof(Keyboard), typeof(FormEntry), Keyboard.Default, propertyChanged: (b, _, n) => ((FormEntry)b).Input.Keyboard = (Keyboard)n);

    public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create(
        nameof(MaxLength), typeof(int), typeof(FormEntry), int.MaxValue, propertyChanged: (b, _, n) => ((FormEntry)b).Input.MaxLength = (int)n);

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }
    public Keyboard Keyboard { get => (Keyboard)GetValue(KeyboardProperty); set => SetValue(KeyboardProperty, value); }
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    public event EventHandler? Completed;

    public FormEntry()
    {
        InitializeComponent();
        Input.TextChanged += (_, e) => { if (Text != e.NewTextValue) Text = e.NewTextValue ?? string.Empty; };
    }

    public new void Focus() => Input.Focus();

    private bool _hidden;

    // The eye: show the password (a crossed eye to hide it again) or hide it.
    private void OnEyeClicked(object? sender, TappedEventArgs e)
    {
        _hidden = !_hidden;
        Input.IsPassword = _hidden;
        Eye.Source = _hidden ? "pw_eye.png" : "pw_eye_off.png";
        // keep the caret at the end after the switch
        Input.CursorPosition = Input.Text?.Length ?? 0;
    }

    private void OnFocused(object? sender, FocusEventArgs e) =>
        Frame.Stroke = new SolidColorBrush((Color)Application.Current!.Resources["Primary"]);

    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Frame.Stroke = new SolidColorBrush((Color)Application.Current!.Resources[dark ? "BorderDark" : "BorderLight"]);
    }

    private void OnCompleted(object? sender, EventArgs e) => Completed?.Invoke(this, EventArgs.Empty);
}
