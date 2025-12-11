using System.Windows.Controls;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views
{
    public partial class LibraryPage : Page
    {
        public LibraryPage(LibraryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
