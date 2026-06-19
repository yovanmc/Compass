using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;
using Compass.Core.Sync;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

public sealed partial class RecommendViewModel : ObservableObject
{
    private readonly ISyncStore _store;
    private readonly RecommendationService _recs;
    private readonly RecommenderConfig _cfg;

    public ObservableCollection<RecommendationRow> Recommendations { get; } = new();
    public ObservableCollection<GameRow> Unmatched { get; } = new();

    [ObservableProperty]
    private bool isEmpty = true;

    /// <summary>Raised when a recommendation card is clicked. Carries the SteamAppId.</summary>
    public event Action<int>? GameChosen;

    [RelayCommand]
    private void OpenDetail(RecommendationRow row)
        => GameChosen?.Invoke(row.AppId);

    public RecommendViewModel(ISyncStore store, RecommendationService recs, CompassOptions opts)
    {
        _store = store;
        _recs = recs;
        _cfg = opts.Recommender;
        RefreshFromStore();
    }

    public void RefreshFromStore()
    {
        var library = _store.LoadLibrary();
        var result = _recs.Recommend(library, _cfg);

        Recommendations.Clear();
        foreach (var r in result.Recommendations.Take(50))
            Recommendations.Add(RecommendationRow.From(r));

        Unmatched.Clear();
        foreach (var g in result.UnscoredBacklog)
            Unmatched.Add(GameRow.From(g));

        IsEmpty = Recommendations.Count == 0;
    }
}
