namespace Jumbo.Pages;

public partial class SubjectDetailPage : ContentPage
{
	public SubjectDetailPage(SubjectDetailPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}

	// The video stops when the learner leaves the page, and the player lets go of the device's media resources.
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		try { Player.Stop(); } catch { }
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		try { Player.Pause(); } catch { }
	}
}
