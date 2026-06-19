using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;
using Compass.Core.Sync;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SyncService _sync;
    private readonly ISyncStore _store;
    private readonly RecommendationService _recs;
    private readonly RecommenderConfig _cfg;

    public ObservableCollection<RecommendationRow> Recommendations { get; } = new();
    public ObservableCollection<GameRow> Backlog { get; } = new();
    public ObservableCollection<GameRow> Unmatched { get; } = new();

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<string> MissingSecrets { get; }

    public MainViewModel(SyncService sync, ISyncStore store, RecommendationService recs, CompassOptions opts)
    {
        _sync = sync;
        _store = store;
        _recs = recs;
        _cfg = opts.Recommender;
        MissingSecrets = SecretsGuard.FindMissing(opts);
        RefreshFromStore();
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
            RefreshFromStore();
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

    private void RefreshFromStore()
    {
        var library = _store.LoadLibrary();
        var result = _recs.Recommend(library, _cfg);

        Recommendations.Clear();
        foreach (var r in result.Recommendations.Take(50))
            Recommendations.Add(RecommendationRow.From(r));

        Unmatched.Clear();
        foreach (var g in result.UnscoredBacklog)
            Unmatched.Add(GameRow.From(g));

        // Update status line to show current counts
        var backlogCount = result.Recommendations.Count;
        var unmatchedCount = result.UnscoredBacklog.Count;
        if (library.Count > 0)
            StatusText = $"Backlog ({backlogCount}) · Unmatched ({unmatchedCount})";
    }
}

public sealed record RecommendationRow(string Name, int ScorePercent, string Why, string FeatureLine)
{
    public static RecommendationRow From(GameRecommendation r)
    {
        var feats = string.Join(" · ", r.WhyFeatures);
        var likes = r.WhyLikedNames.Count > 0
            ? $"like {string.Join(", ", r.WhyLikedNames)}"
            : "Similar to your taste";
        var why = r.WhyLikedNames.Count > 0 && feats.Length > 0
            ? $"{feats} — {likes}"
            : feats.Length > 0 ? feats : likes;
        return new RecommendationRow(
            r.Game.Name,
            (int)Math.Round(Math.Clamp(r.Score, 0, 1) * 100),
            why,
            feats.Length > 0 ? feats : "");
    }
}

public sealed record GameRow(string Name)
{
    public static GameRow From(Compass.Core.Model.Game g) => new(g.Name);
}
