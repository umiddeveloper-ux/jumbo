namespace Jumbo.Pages;

public partial class SettingsProfilePage : ContentPage
{
	public SettingsProfilePage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
