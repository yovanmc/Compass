using Compass.Core.Covers;
using Compass.Core.Sync;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

/// <summary>
/// Creates a <see cref="DetailViewModel"/> for a specific game.
/// Runs a fresh recommendation pass so the detail panel always reflects current store state.
/// </summary>
public sealed class DetailViewModelFactory
{
    private readonly ISyncStore _store;
    private readonly RecommendationService _recs;
    private readonly RecommenderConfigState _state;
    private readonly ICoverProvider _covers;

    public DetailViewModelFactory(
        ISyncStore store,
        RecommendationService recs,
        RecommenderConfigState state,
        ICoverProvider covers)
    {
        _store  = store;
        _recs   = recs;
        _state  = state;
        _covers = covers;
    }

    public DetailViewModel Create(int appId, Action onChangedAndClose, Action onLibraryChanged)
    {
        var library = _store.LoadLibrary();
        var game = library.FirstOrDefault(g => g.SteamAppId == appId)
            ?? throw new InvalidOperationException($"Game with appId {appId} not found in library.");

        // Run recommendation to find this game's score/breakdown, if it is a backlog candidate.
        var result = _recs.Recommend(library, _state.Current);
        var rec = result.Recommendations.FirstOrDefault(r => r.Game.SteamAppId == appId);

        var similar = _recs.SimilarTo(library, appId, 6)
            .Select(r => new SimilarRow(
                r.Game.SteamAppId,
                r.Game.Name,
                (int)Math.Round(Math.Clamp(r.Score, 0, 1) * 100)))
            .ToList();

        var vm = new DetailViewModel(game, rec, _covers, _store, onChangedAndClose, onLibraryChanged, similar);
        return vm;
    }
}
