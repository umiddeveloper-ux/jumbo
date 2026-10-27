using Jumbo.PageModels;

namespace Jumbo.Pages
{
    public partial class RatingPage : ContentPage
    {
        public RatingPage(RatingPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}
