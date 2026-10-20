namespace Jumbo.Pages;

public partial class SequenceGamePage : ContentPage
{
    public SequenceGamePage(SequenceGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
