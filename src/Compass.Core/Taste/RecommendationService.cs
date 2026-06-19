using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Recommender;

namespace Compass.Core.Taste;

public sealed record GameRecommendation(
    Game Game, double Score,
    IReadOnlyList<string> WhyFeatures,
    IReadOnlyList<string> WhyLikedNames);

public sealed record RecommendationResult(
    IReadOnlyList<GameRecommendation> Recommendations,
    IReadOnlyList<Game> UnscoredBacklog);

public sealed class RecommendationService
{
    private readonly IRecommender _recommender;
    public RecommendationService(IRecommender? recommender = null)
        => _recommender = recommender ?? new ContentRecommender();

    public RecommendationResult Recommend(IReadOnlyList<Game> library, RecommenderConfig cfg)
    {
        var affinity = new AffinityCalculator(cfg.PlayedFloorMinutes, cfg.RecencyWeight);
        var options = new RecommenderOptions
        {
            K = cfg.K,
            Mode = Enum.TryParse<ScorerMode>(cfg.ScorerMode, out var m) ? m : ScorerMode.NearestNeighbor,
            CategoryWeights = cfg.CategoryWeights,
        };

        var liked = new List<ProfileItem>();
        var candidates = new List<CandidateItem>();
        var unscored = new List<Game>();
        var byId = new Dictionary<string, Game>();

        foreach (var g in library)
        {
            var id = g.SteamAppId.ToString();
            byId[id] = g;
            var vec = GameFeatureExtractor.ToVector(g);

            if (affinity.IsPlayed(g.PlaytimeForeverMinutes))
            {
                if (!vec.IsEmpty)
                    liked.Add(new ProfileItem(id, vec,
                        affinity.Affinity(g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes)));
                // played-but-no-features simply don't inform taste; not an error
            }
            else // backlog candidate
            {
                if (vec.IsEmpty) unscored.Add(g);
                else candidates.Add(new CandidateItem(id, vec));
            }
        }

        var ranked = _recommender.Recommend(liked, candidates, options);

        var recs = ranked.Recommendations.Select(r =>
        {
            var game = byId[r.ItemId];
            var whyFeatures = r.TopFeatures.Select(f => Humanize(f.FeatureKey)).ToList();
            var whyNames = r.NearestLikedItemIds
                .Where(byId.ContainsKey).Select(nid => byId[nid].Name).ToList();
            return new GameRecommendation(game, r.Score, whyFeatures, whyNames);
        }).ToList();

        return new RecommendationResult(recs, unscored);
    }

    private static string Humanize(string featureKey)
    {
        var i = featureKey.IndexOf(':');
        return i < 0 ? featureKey : featureKey[(i + 1)..];
    }
}
