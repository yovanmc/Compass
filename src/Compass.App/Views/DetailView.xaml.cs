using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Compass.App.ViewModels;

namespace Compass.App.Views;

/// <summary>
/// Detail slide-over panel. DataContext (DetailViewModel) is supplied by the shell binding.
/// </summary>
public partial class DetailView : UserControl
{
    public DetailView()
    {
        InitializeComponent();
    }

    private void Similar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe
            && fe.DataContext is SimilarRow row
            && DataContext is DetailViewModel vm)
        {
            vm.OpenSimilarCommand.Execute(row);
        }
    }
}
