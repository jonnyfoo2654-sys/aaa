using System.Windows;
using QTO.Plugin.ViewModels;

namespace QTO.Plugin.Views
{
    public partial class QTOPalette : Window
    {
        public QTOPalette(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
