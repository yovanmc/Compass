using Wpf.Ui.Abstractions;

namespace Compass.App.Navigation;

public sealed class PageProvider : INavigationViewPageProvider
{
    private readonly IServiceProvider _services;

    public PageProvider(IServiceProvider services) => _services = services;

    public object? GetPage(Type pageType) => _services.GetService(pageType);
}
