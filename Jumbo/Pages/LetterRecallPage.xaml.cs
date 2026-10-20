namespace Jumbo.Pages;

public partial class LetterRecallPage : ContentPage
{
    public LetterRecallPage(LetterRecallPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
