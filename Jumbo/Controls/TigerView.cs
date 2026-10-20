namespace Jumbo.Controls;

/// <summary>
/// Jumbo the elephant cub (same character as on the website; the class keeps its old name). One picture, a full figure and a
/// head crop (made from the website's picture); the life is added here: a gentle bob when idle, a sway when waving, a happy jump
/// when cheering, a slow droop when sad. Mood: idle | wave | cheer | sad. Head: only the face (for small spots).
/// </summary>
public class TigerView : ContentView
{
    public static readonly BindableProperty MoodProperty = BindableProperty.Create(
        nameof(Mood), typeof(string), typeof(TigerView), "idle", propertyChanged: (b, _, _) => ((TigerView)b).OnLookChanged());

    public static readonly BindableProperty HeadProperty = BindableProperty.Create(
        nameof(Head), typeof(bool), typeof(TigerView), false, propertyChanged: (b, _, _) => ((TigerView)b).OnLookChanged());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(TigerView), 120d, propertyChanged: (b, _, _) => ((TigerView)b).OnLookChanged());

    public static readonly BindableProperty AnimatedProperty = BindableProperty.Create(
        nameof(Animated), typeof(bool), typeof(TigerView), true, propertyChanged: (b, _, _) => ((TigerView)b).RestartAnimation());

    private readonly Image _image = new() { Aspect = Aspect.AspectFit, InputTransparent = true };
    private CancellationTokenSource? _cts;

    public string Mood { get => (string)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }
    public bool Head { get => (bool)GetValue(HeadProperty); set => SetValue(HeadProperty, value); }
    /// <summary>Width in device-independent units; the height follows the picture's proportions.</summary>
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool Animated { get => (bool)GetValue(AnimatedProperty); set => SetValue(AnimatedProperty, value); }

    public TigerView()
    {
        InputTransparent = true;
        HorizontalOptions = LayoutOptions.Center;
        Content = _image;
        OnLookChanged();
        Loaded += (_, _) => RestartAnimation();
        Unloaded += (_, _) => StopAnimation();
    }

    private void OnLookChanged()
    {
        _image.Source = ImageSource.FromFile(Head ? "jumbo_head.png" : "jumbo_full.png");
        WidthRequest = Size;
        HeightRequest = Head ? Size * 0.72 : Size * 1.3;
        RestartAnimation();
    }

    private void RestartAnimation()
    {
        StopAnimation();
        if (!Animated || Handler is null) return;

        _cts = new CancellationTokenSource();
        _ = RunAsync(Mood, _cts.Token);
    }

    private void StopAnimation()
    {
        _cts?.Cancel();
        _cts = null;
        _image.CancelAnimations();
        _image.TranslationY = 0;
        _image.Rotation = 0;
        _image.Scale = 1;
    }

    private async Task RunAsync(string mood, CancellationToken token)
    {
        try
        {
            var lift = Size * 0.04;
            while (!token.IsCancellationRequested)
            {
                switch (mood)
                {
                    case "cheer":
                        await _image.TranslateToAsync(0, -lift * 2.2, 260, Easing.CubicOut);
                        await _image.TranslateToAsync(0, 0, 260, Easing.CubicIn);
                        break;
                    case "wave":
                        await _image.RotateToAsync(-4, 450, Easing.SinInOut);
                        await _image.RotateToAsync(4, 450, Easing.SinInOut);
                        break;
                    case "sad":
                        await Task.WhenAll(_image.TranslateToAsync(0, lift * 0.8, 1700, Easing.SinInOut), _image.RotateToAsync(-3, 1700, Easing.SinInOut));
                        await Task.WhenAll(_image.TranslateToAsync(0, 0, 1700, Easing.SinInOut), _image.RotateToAsync(0, 1700, Easing.SinInOut));
                        break;
                    default:
                        await _image.TranslateToAsync(0, -lift * 0.7, 1300, Easing.SinInOut);
                        await _image.TranslateToAsync(0, 0, 1300, Easing.SinInOut);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // The view can be torn down in the middle of an animation: nothing to report.
        }
    }
}
