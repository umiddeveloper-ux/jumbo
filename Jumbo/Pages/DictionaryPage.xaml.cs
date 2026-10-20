namespace Jumbo.Pages;

public partial class DictionaryPage : ContentPage
{
	public DictionaryPage(DictionaryPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}