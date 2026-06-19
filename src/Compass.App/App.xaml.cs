using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using Compass.App.Navigation;
using Compass.App.ViewModels;
using Compass.App.Views;
using Compass.Core.Config;
using Compass.Core.Covers;
using Compass.Core.Sync;
using Compass.Core.Taste;
using Compass.Data.Covers;
using Compass.Data.Db;
using Compass.Data.Igdb;
using Compass.Data.Match;
using Compass.Data.Steam;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Compass.App;

public partial class App : Application
{
    private IServiceProvider _services = default!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply dark theme + ice-cyan accent BEFORE first window renders (Mica OFF = None backdrop).
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.None, false);
        // Ice-cyan #4FC3F7 as accent.
        var iceCyan = Color.FromRgb(0x4F, 0xC3, 0xF7);
        ApplicationAccentColorManager.Apply(iceCyan, ApplicationTheme.Dark, false, false);

        // Optional --db <path> override (for testing / seeded screenshots).
        string dbPath = CompassDb.DefaultDbPath();
        var args = e.Args;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--db", StringComparison.OrdinalIgnoreCase))
            {
                dbPath = args[i + 1];
                break;
            }
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<App>(optional: true)
            .Build();

        var options = new CompassOptions();
        config.Bind(options);

        var sc = new ServiceCollection();
        sc.AddSingleton(options);
        sc.AddSingleton(new CompassDb(dbPath));
        sc.AddHttpClient();

        sc.AddSingleton<ISteamClient>(sp => new SteamClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            options.Steam.ApiKey, options.Steam.SteamId64));

        sc.AddSingleton(sp => new TwitchTokenProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            options.Igdb.ClientId, options.Igdb.ClientSecret));

        sc.AddSingleton<IIgdbClient>(sp => new IgdbClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<TwitchTokenProvider>(), options.Igdb.ClientId));

        sc.AddSingleton<IGameMatcher>(sp => new GameMatcher(
            sp.GetRequiredService<IIgdbClient>(), nameConfidenceThreshold: 0.85));

        sc.AddSingleton<ISyncStore>(sp => new SqliteSyncStore(sp.GetRequiredService<CompassDb>()));

        // Settings persistence infrastructure
        sc.AddSingleton<ISettingsStore>(sp => new SettingsRepository(sp.GetRequiredService<CompassDb>()));
        sc.AddSingleton(sp => new RecommenderSettingsService(sp.GetRequiredService<ISettingsStore>()));
        sc.AddSingleton(sp => new RecommenderConfigState(
            sp.GetRequiredService<RecommenderSettingsService>().Load(options.Recommender)));

        sc.AddSingleton(sp => new SyncService(
            sp.GetRequiredService<ISteamClient>(),
            sp.GetRequiredService<IIgdbClient>(),
            sp.GetRequiredService<IGameMatcher>(),
            sp.GetRequiredService<ISyncStore>()));

        sc.AddSingleton(new RecommendationService());
        sc.AddSingleton(new InsightsService());

        // Cover art infrastructure
        sc.AddSingleton<ICoverDownloader>(sp =>
            new HttpCoverDownloader(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        sc.AddSingleton<ICoverProvider>(sp =>
            new SteamCoverProvider(
                sp.GetRequiredService<ICoverDownloader>(),
                SteamCoverProvider.DefaultCacheDir()));

        sc.AddSingleton<RecommendViewModel>();
        sc.AddSingleton<LibraryViewModel>();
        sc.AddSingleton<DetailViewModelFactory>();
        sc.AddSingleton<SettingsViewModel>();
        sc.AddSingleton<InsightsViewModel>();
        sc.AddSingleton<ShellViewModel>();

        // Pages — registered so PageProvider can resolve them from DI
        sc.AddTransient<RecommendView>();
        sc.AddTransient<LibraryView>();
        sc.AddTransient<SettingsView>();
        sc.AddTransient<InsightsView>();

        // Navigation page provider (feeds DI instances to WPF-UI NavigationView)
        sc.AddSingleton<PageProvider>(sp => new PageProvider(sp));

        sc.AddSingleton<MainWindow>();

        _services = sc.BuildServiceProvider();

        _services.GetRequiredService<CompassDb>().Initialize();
        _services.GetRequiredService<MainWindow>().Show();
    }
}
