using Wpf.Ui.Abstractions;

namespace Compass.App.Navigation;

/// <summary>
/// Resolves navigation pages from the DI container for WPF-UI NavigationView.
/// </summary>
public sealed class PageProvider : INavigationViewPageProvider
{
    private readonly IServiceProvider _services;

    public PageProvider(IServiceProvider services) => _services = services;

    public object? GetPage(Type pageType) => _services.GetService(pageType);
}
