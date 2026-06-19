using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;
using Compass.Core.Sync;

namespace Compass.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly SyncService _sync;
    private readonly CompassOptions _opts;

    public RecommendViewModel Recommend { get; }

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<string> MissingSecrets { get; }
    public bool HasMissingSecrets => MissingSecrets.Count > 0;

    public ShellViewModel(SyncService sync, RecommendViewModel recommend, CompassOptions opts)
    {
        _sync = sync;
        _opts = opts;
        Recommend = recommend;
        MissingSecrets = SecretsGuard.FindMissing(opts);

        // Seed the status line with initial counts if data is already in store
        if (Recommend.Recommendations.Count > 0 || Recommend.Unmatched.Count > 0)
            StatusText = $"Backlog ({Recommend.Recommendations.Count}) · Unmatched ({Recommend.Unmatched.Count})";
    }

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
