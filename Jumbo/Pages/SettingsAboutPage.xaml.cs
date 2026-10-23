namespace Jumbo.Pages;

public partial class SettingsAboutPage : ContentPage
{
	public SettingsAboutPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
