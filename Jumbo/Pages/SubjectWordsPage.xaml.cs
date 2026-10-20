namespace Jumbo.Pages;

public partial class SubjectWordsPage : ContentPage
{
	public SubjectWordsPage(SubjectWordsPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
