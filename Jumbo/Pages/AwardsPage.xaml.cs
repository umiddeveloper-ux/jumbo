namespace Jumbo.Pages;

public partial class AwardsPage : ContentPage
{
	private readonly AwardsPageModel _model;

	public AwardsPage(AwardsPageModel model)
	{
		InitializeComponent();
		BindingContext = _model = model;
	}

	// two market cards in a row, whatever the width of the screen (15 on each side, 10 between the cards)
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0) _model.MarketCardWidth = Math.Floor((width - 30 - 6 - 20) / 2);
		if (width > 0) _model.AwardTileWidth = Math.Floor((width - 30 - 6 - 24) / 3);
	}
}
