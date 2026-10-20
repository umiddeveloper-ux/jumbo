namespace Jumbo.Pages;

public partial class SubjectsPage : ContentPage
{
	public SubjectsPage(SubjectsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}