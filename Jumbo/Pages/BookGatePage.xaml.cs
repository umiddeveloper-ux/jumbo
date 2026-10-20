namespace Jumbo.Pages;

public partial class BookGatePage : ContentPage
{
    public BookGatePage(BookGatePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
