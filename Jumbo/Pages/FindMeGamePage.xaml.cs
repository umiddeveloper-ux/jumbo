namespace Jumbo.Pages;

public partial class FindMeGamePage : ContentPage
{
    public FindMeGamePage(FindMeGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    // Six cards: two rows of three. The width of a card follows the width of the page (15 on each side, 8 between the cards).
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && BindingContext is FindMeGamePageModel m) m.CardWidth = Math.Floor((width - 30 - 24) / 3);
    }
}
