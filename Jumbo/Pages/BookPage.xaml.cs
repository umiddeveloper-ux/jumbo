using Jumbo.Controls;
using Jumbo.PageModels;
using Jumbo.Services;

namespace Jumbo.Pages;

public partial class BookPage : ContentPage
{
    private readonly BookPageModel _model;
    private IReadOnlyList<SnakeNode> _nodes = [];
    private string _bubble = string.Empty;

    public BookPage(BookPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;

        _model.PathChanged += OnPathChanged;
        _model.FocusCurrent += OnFocusCurrent;
        Snake.NodeTapped += index => _model.SelectNodeCommand.Execute(index);
        ThemeService.Changed += Redraw;
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => Redraw();
    }

    // A second after the road is shown it scrolls by itself (smoothly) to the topic that has to be passed: the learner sees it move.
    private int _focusTicket;

    private async void OnFocusCurrent(int index)
    {
        int ticket = ++_focusTicket;
        try
        {
            await Task.Delay(1000);
            if (ticket != _focusTicket) return;                       // a newer load has come
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                double y = Snake.Y + Snake.CenterY(index) - PageScroll.Height * 0.36;
                double max = Math.Max(0, PageScroll.ContentSize.Height - PageScroll.Height);
                await PageScroll.ScrollToAsync(0, Math.Clamp(y, 0, max), true);
            });
        }
        catch { /* the page was left in the meantime */ }
    }

    private void OnPathChanged(IReadOnlyList<SnakeNode> nodes, string bubble)
    {
        _nodes = nodes;
        _bubble = bubble;
        MainThread.BeginInvokeOnMainThread(Redraw);
    }

    private void Redraw() =>
        Snake.Show(_nodes, _bubble, Application.Current?.RequestedTheme == AppTheme.Dark);
}
