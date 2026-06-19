using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;
using Compass.Core.Sync;

namespace Compass.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly SyncService _sync;
    private readonly CompassOptions _opts;
    private readonly DetailViewModelFactory _detailFactory;

    public RecommendViewModel Recommend { get; }
    public LibraryViewModel Library { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private DetailViewModel? activeDetail;

    public bool IsDetailOpen => ActiveDetail is not null;

    partial void OnActiveDetailChanged(DetailViewModel? value)
        => OnPropertyChanged(nameof(IsDetailOpen));

    // Detach the replaced panel's re-open handler so its event lifecycle is
    // symmetric with the subscribe in OnGameChosen (no dangling subscriptions).
    partial void OnActiveDetailChanging(DetailViewModel? oldValue, DetailViewModel? newValue)
    {
        if (oldValue is not null) oldValue.GameChosen -= OnGameChosen;
    }

    public IReadOnlyList<string> MissingSecrets { get; }
    public bool HasMissingSecrets => MissingSecrets.Count > 0;

    public ShellViewModel(
        SyncService sync,
        RecommendViewModel recommend,
        LibraryViewModel library,
        SettingsViewModel settings,
        CompassOptions opts,
        DetailViewModelFactory detailFactory)
    {
        _sync          = sync;
        _opts          = opts;
        _detailFactory = detailFactory;
        Recommend      = recommend;
        Library        = library;
        Settings       = settings;
        MissingSecrets = SecretsGuard.FindMissing(opts);

        // Subscribe to game-chosen events from both pages
        Recommend.GameChosen += OnGameChosen;
        Library.GameChosen   += OnGameChosen;

        // Re-rank both pages whenever a Settings knob changes
        Settings.ConfigChanged += () =>
        {
            Recommend.RefreshFromStore();
            Library.RefreshFromStore();
        };

        // Refresh both pages after Load sample data / Clear library
        Settings.LibraryReplaced += () =>
        {
            Recommend.RefreshFromStore();
            Library.RefreshFromStore();
        };

        // Seed the status line with initial counts if data is already in store
        if (Recommend.Recommendations.Count > 0 || Recommend.Unmatched.Count > 0)
            StatusText = $"Backlog ({Recommend.Recommendations.Count}) · Unmatched ({Recommend.Unmatched.Count})";
    }

    private void OnGameChosen(int appId)
    {
        var vm = _detailFactory.Create(appId, onChangedAndClose: () =>
        {
            Recommend.RefreshFromStore();
            Library.RefreshFromStore();
            ActiveDetail = null;
        });
        vm.GameChosen += OnGameChosen;   // "more like this" re-opens detail for the next game
        ActiveDetail = vm;
    }

    [RelayCommand]
    private void CloseDetail() => ActiveDetail = null;

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (MissingSecrets.Count > 0)
        {
            StatusText = "Add API keys first — see README.";
            return;
        }
        IsBusy = true;
        try
        {
            var progress = new Progress<string>(s =>
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusText = s));
            var report = await Task.Run(() => _sync.SyncAsync(CancellationToken.None, progress));
            StatusText = $"{report.Owned} games · {report.Matched} matched · {report.Unmatched} unmatched";

            // Refresh all page VMs after sync
            Recommend.RefreshFromStore();
            Library.RefreshFromStore();
        }
        catch (Exception ex)
        {
            StatusText = $"Sync failed: {ex.Message} (cached data kept).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
