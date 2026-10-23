namespace Jumbo.Pages;

public partial class SettingsTeamPage : ContentPage
{
	public SettingsTeamPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
