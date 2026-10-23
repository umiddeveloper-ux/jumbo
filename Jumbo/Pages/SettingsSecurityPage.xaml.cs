namespace Jumbo.Pages;

public partial class SettingsSecurityPage : ContentPage
{
	public SettingsSecurityPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
