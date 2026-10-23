namespace Jumbo.Pages;

public partial class SettingsBotPage : ContentPage
{
	public SettingsBotPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
