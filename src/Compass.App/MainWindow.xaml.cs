using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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

        NavView.SetPageProviderService(pageProvider);

        Loaded += (_, _) =>
        {
            // WPF-UI's NavigationView hosts pages in a DynamicScrollViewer that measures at INFINITE
            // height, which defeats list virtualization (multi-second freeze on a large library).
            // Disabling it bounds each page to the viewport. Do it before the first navigation.
            DisableNavContentScroll();
            NavView.Navigate(typeof(RecommendView));
            // Re-apply after navigation in case the content host was built lazily.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(DisableNavContentScroll));
        };
    }

    private void DisableNavContentScroll()
    {
        var presenter = FindDescendant<NavigationViewContentPresenter>(NavView);
        var sv = presenter is null ? null : FindDescendant<DynamicScrollViewer>(presenter);
        if (sv is not null)
        {
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var deeper = FindDescendant<T>(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    // Scrim click closes the detail slide-over. (A MouseBinding can't bind the
    // VM command — InputBindings don't inherit DataContext.)
    private void Scrim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        (DataContext as ShellViewModel)?.CloseDetailCommand.Execute(null);
    }
}
