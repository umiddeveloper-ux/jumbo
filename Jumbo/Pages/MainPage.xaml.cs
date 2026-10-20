using Jumbo.PageModels;

namespace Jumbo.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(DashboardPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}
