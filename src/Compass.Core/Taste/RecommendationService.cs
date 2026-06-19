using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Recommender;

namespace Compass.Core.Taste;

/// <summary>A single feature contributing to a recommendation, with its humanized name and engine magnitude.</summary>
public sealed record WhyFeature(string Name, double Contribution);

public sealed record GameRecommendation(
    Game Game, double Score,
    IReadOnlyList<WhyFeature> WhyFeatures,
    IReadOnlyList<string> WhyLikedNames,
    IReadOnlyList<string> WhyPenalizedNames);

public sealed record RecommendationResult(
    IReadOnlyList<GameRecommendation> Recommendations,
    IReadOnlyList<Game> UnscoredBacklog);

public sealed class RecommendationService
{
    public const int SampledThresholdMinutes = 30;

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
            NegativeWeight = cfg.NegativeWeight,
            HybridAlpha = cfg.HybridAlpha,
            Diversity = cfg.Diversity,
        };

        var liked = new List<ProfileItem>();
        var disliked = new List<ProfileItem>();
        var candidates = new List<CandidateItem>();
        var unscored = new List<Game>();
        var byId = new Dictionary<string, Game>();

        foreach (var g in library)
        {
            var id = g.SteamAppId.ToString();
            byId[id] = g;
            var vec = GameFeatureExtractor.ToVector(g);

            if (g.NotInterested)
            {
                // Exclude from candidates entirely; build as disliked signal if it has features
                if (!vec.IsEmpty)
                {
                    var w = Math.Log(1 + Math.Max(g.PlaytimeForeverMinutes, cfg.PlayedFloorMinutes));
                    disliked.Add(new ProfileItem(id, vec, w));
                }
                continue;
            }

            if (affinity.IsPlayed(g.PlaytimeForeverMinutes))
            {
                if (!vec.IsEmpty)
                    liked.Add(new ProfileItem(id, vec,
                        affinity.Affinity(g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes)));
                // played-but-no-features simply don't inform taste; not an error
            }
            else // backlog candidate
            {
                // Check for implicit negatives: tried-and-dropped games
                if (cfg.UseImplicitNegatives
                    && g.PlaytimeForeverMinutes >= SampledThresholdMinutes
                    && g.PlaytimeForeverMinutes < cfg.PlayedFloorMinutes
                    && !vec.IsEmpty)
                {
                    var w = 0.5 * Math.Log(1 + g.PlaytimeForeverMinutes);
                    disliked.Add(new ProfileItem(id, vec, w));
                    // Still exclude from candidates (treat as tried-and-dropped, not unscored backlog)
                    continue;
                }

                if (vec.IsEmpty) unscored.Add(g);
                else candidates.Add(new CandidateItem(id, vec));
            }
        }

        var ranked = _recommender.Recommend(liked, candidates, options, disliked);

        var recs = ranked.Recommendations
            .Select(r => MapRecommendation(r, byId))
            .ToList();

        return new RecommendationResult(recs, unscored);
    }

    /// <summary>
    /// Returns up to <paramref name="k"/> games most similar to the seed game (by content features),
    /// excluding the seed itself and any game marked NotInterested. Games without features are skipped.
    /// Returns an empty list if the seed is not found or has no features.
    /// </summary>
    public IReadOnlyList<GameRecommendation> SimilarTo(IReadOnlyList<Game> library, int seedAppId, int k)
    {
        var seed = library.FirstOrDefault(g => g.SteamAppId == seedAppId);
        if (seed is null) return [];

        var seedVec = GameFeatureExtractor.ToVector(seed);
        if (seedVec.IsEmpty) return [];

        var byId = new Dictionary<string, Game>();
        var liked = new List<ProfileItem>
        {
            new(seedAppId.ToString(), seedVec, 1.0)
        };

        var candidates = new List<CandidateItem>();
        foreach (var g in library)
        {
            if (g.SteamAppId == seedAppId) continue;   // exclude seed
            if (g.NotInterested) continue;              // exclude not-interested

            var vec = GameFeatureExtractor.ToVector(g);
            if (vec.IsEmpty) continue;                  // exclude no-feature games

            var id = g.SteamAppId.ToString();
            byId[id] = g;
            candidates.Add(new CandidateItem(id, vec));
        }

        if (candidates.Count == 0) return [];

        var options = new RecommenderOptions { K = k };  // Diversity = 0 (default) — pure similarity
        var ranked = _recommender.Recommend(liked, candidates, options, null);

        return ranked.Recommendations
            .Where(r => r.Score > 0)
            .Take(k)
            .Select(r => MapRecommendation(r, byId))
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static GameRecommendation MapRecommendation(Recommendation r, Dictionary<string, Game> byId)
    {
        var game = byId[r.ItemId];
        var whyFeatures = r.TopFeatures
            .Select(f => new WhyFeature(Humanize(f.FeatureKey), f.Contribution))
            .ToList();
        var whyNames = r.NearestLikedItemIds
            .Where(byId.ContainsKey).Select(nid => byId[nid].Name).ToList();
        var whyPenalized = r.PenalizedByItemIds
            .Where(byId.ContainsKey).Select(nid => byId[nid].Name).ToList();
        return new GameRecommendation(game, r.Score, whyFeatures, whyNames, whyPenalized);
    }

    private static string Humanize(string featureKey)
    {
        var i = featureKey.IndexOf(':');
        return i < 0 ? featureKey : featureKey[(i + 1)..];
    }
}
