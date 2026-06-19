using System.Windows;
using Compass.App.Navigation;
using Compass.App.ViewModels;
using Compass.App.Views;
using Wpf.Ui.Controls;

namespace Compass.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(ShellViewModel vm, PageProvider pageProvider)
    {
        InitializeComponent();
        DataContext = vm;

        // Wire the DI-backed page provider so NavigationView resolves pages from the container
        NavView.SetPageProviderService(pageProvider);

        // Navigate to Recommend as the initial page once the window is loaded
        Loaded += (_, _) => NavView.Navigate(typeof(RecommendView));
    }

    // Scrim click closes the detail slide-over. (A MouseBinding can't bind the
    // VM command — InputBindings don't inherit DataContext.)
    private void Scrim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        (DataContext as ShellViewModel)?.CloseDetailCommand.Execute(null);
    }
}
