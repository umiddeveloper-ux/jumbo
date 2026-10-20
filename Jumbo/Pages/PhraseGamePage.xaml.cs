namespace Jumbo.Pages;

public partial class PhraseGamePage : ContentPage
{
    public PhraseGamePage(PhraseGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
