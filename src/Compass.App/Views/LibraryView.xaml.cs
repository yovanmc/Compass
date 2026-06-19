using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Compass.App.ViewModels;

namespace Compass.App.Views;

public partial class LibraryView : Page
{
    private readonly LibraryViewModel _vm;

    public LibraryView(LibraryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    // Toggle buttons act as a two-button radio group for the view mode.
    private void RowViewButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.IsPosterView = false;
        if (sender is ToggleButton tb) tb.IsChecked = true;
    }

    private void PosterViewButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.IsPosterView = true;
        if (sender is ToggleButton tb) tb.IsChecked = true;
    }
}
