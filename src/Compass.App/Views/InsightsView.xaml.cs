using System.Windows;
using System.Windows.Controls;
using Compass.App.ViewModels;

namespace Compass.App.Views;

public partial class InsightsView : Page
{
    private readonly InsightsViewModel _vm;

    public InsightsView(InsightsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Compute insights only when the page is actually shown (and re-shown after a
        // mutation marks it stale). Never at startup — the health eval is heavy on large
        // libraries. EnsureLoadedAsync is idempotent + dirty-guarded, so double-calls are safe.
        Loaded += OnShown;
        IsVisibleChanged += OnVisibilityChanged;
    }

    private async void OnShown(object sender, RoutedEventArgs e)
    {
        try { await _vm.EnsureLoadedAsync(); } catch { /* never crash the UI on a refresh */ }
    }

    private async void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            try { await _vm.EnsureLoadedAsync(); } catch { /* never crash the UI on a refresh */ }
        }
    }
}
