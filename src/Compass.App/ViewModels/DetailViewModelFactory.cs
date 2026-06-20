using Compass.Core.Config;
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

        // Find this game's score/breakdown. Use a relevance-only pass (diversity OFF): MMR only
        // reorders results and, on a large library, shortlists candidates to the strongest few
        // hundred — it never changes an individual game's score. δ=0 therefore guarantees every
        // backlog game has its score available here, and it's the cheaper path.
        var cfg = _state.Current;
        var relevanceCfg = new RecommenderConfig
        {
            PlayedFloorMinutes   = cfg.PlayedFloorMinutes,
            RecencyWeight        = cfg.RecencyWeight,
            K                    = cfg.K,
            ScorerMode           = cfg.ScorerMode,
            CategoryWeights      = cfg.CategoryWeights,
            NegativeWeight       = cfg.NegativeWeight,
            UseImplicitNegatives = cfg.UseImplicitNegatives,
            HybridAlpha          = cfg.HybridAlpha,
            Diversity            = 0,
            FeedbackWeight       = cfg.FeedbackWeight,
        };
        var result = _recs.Recommend(library, relevanceCfg);
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
