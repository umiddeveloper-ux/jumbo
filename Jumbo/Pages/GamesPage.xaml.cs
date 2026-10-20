using Jumbo.PageModels;

namespace Jumbo.Pages;

public partial class GamesPage : ContentPage
{
    public GamesPage(GamesPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    private readonly GamesPageModel _model;

    // three cards in a row, whatever the width of the screen (16 on each side, 8 between the cards)
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0) _model.CardWidth = Math.Floor((width - 32 - 24) / 3);
    }
}
