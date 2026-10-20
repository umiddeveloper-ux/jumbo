using Jumbo.PageModels;

namespace Jumbo.Pages;

public partial class RegistrationPage : ContentPage
{
    public RegistrationPage(RegistrationPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
