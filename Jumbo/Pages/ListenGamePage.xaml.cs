namespace Jumbo.Pages;

public partial class ListenGamePage : ContentPage
{
    public ListenGamePage(ListenAndFindGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
