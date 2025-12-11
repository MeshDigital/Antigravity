using System.Windows.Controls;

namespace SLSKDONET.Views;

public partial class DownloadsPage : Page
{
    public DownloadsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
