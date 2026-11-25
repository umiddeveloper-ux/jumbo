namespace Jumbo.Pages;

public partial class FindMeGamePage : ContentPage
{
    public FindMeGamePage(FindMeGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    // Six cards: two rows of three. The width of a card follows the width of the page (15 on each side, 4 on each side of a card, and 4 more spare: the flex layout took the exact sum for too much and pushed the first column 4 px out to the left, where it was cut off).
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && BindingContext is FindMeGamePageModel m) m.CardWidth = Math.Floor((width - 30 - 24 - 4) / 3);
    }
}
