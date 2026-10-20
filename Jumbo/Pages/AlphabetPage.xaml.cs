using Jumbo.PageModels;

namespace Jumbo.Pages
{
    public partial class AlphabetPage : ContentPage
    {
        public AlphabetPage(AlphabetPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}
