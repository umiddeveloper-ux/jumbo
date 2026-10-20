namespace Jumbo.Pages;

public partial class StepsGamePage : ContentPage
{
    public StepsGamePage(StepsGamePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
