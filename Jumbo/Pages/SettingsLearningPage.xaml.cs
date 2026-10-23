namespace Jumbo.Pages;

public partial class SettingsLearningPage : ContentPage
{
	public SettingsLearningPage(SettingsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
