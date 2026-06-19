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
}
