namespace Jumbo.Pages;

public partial class BookZeroPage : ContentPage
{
    public BookZeroPage(BookZeroPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
