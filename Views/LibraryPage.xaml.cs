using System.Windows.Controls;

namespace SLSKDONET.Views
{
    public partial class LibraryPage : Page
    {
        public LibraryPage()
        {
            InitializeComponent();
        }

        private void ToggleImportedBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ImportedExpander.IsExpanded = !ImportedExpander.IsExpanded;
            ToggleImportedBtn.Content = ImportedExpander.IsExpanded ? "Hide Imported" : "Show Imported";
        }
    }
}
