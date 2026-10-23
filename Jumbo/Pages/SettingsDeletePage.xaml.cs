namespace Jumbo.Pages;

public partial class SettingsDeletePage : ContentPage
{
	public SettingsDeletePage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
