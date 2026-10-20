using Jumbo.Controls;
using Jumbo.Services;

namespace Jumbo.Pages;

public partial class BookZeroGamePage : ContentPage
{
    private IReadOnlyList<TrainWagon> _wagons = [];

    public BookZeroGamePage(BookZeroGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;

        model.TrainChanged += wagons =>
        {
            _wagons = wagons;
            MainThread.BeginInvokeOnMainThread(Redraw);
        };
        ThemeService.Changed += Redraw;
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => Redraw();
    }

    private void Redraw() => Train.Show(_wagons, Application.Current?.RequestedTheme == AppTheme.Dark);
}
