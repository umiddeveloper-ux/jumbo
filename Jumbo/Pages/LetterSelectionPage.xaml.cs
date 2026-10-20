namespace Jumbo.Pages;

public partial class LetterSelectionPage : ContentPage
{
    public LetterSelectionPage(PageModels.LetterSelectionPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}
