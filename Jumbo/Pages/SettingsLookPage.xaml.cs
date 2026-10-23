namespace Jumbo.Pages;

public partial class SettingsLookPage : ContentPage
{
	public SettingsLookPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
