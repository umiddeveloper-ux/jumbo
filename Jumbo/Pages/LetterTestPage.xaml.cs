namespace Jumbo.Pages;

public partial class LetterTestPage : ContentPage
{
    public LetterTestPage(LetterTestPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
