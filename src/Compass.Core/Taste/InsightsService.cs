using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Recommender;
using System.Globalization;

namespace Compass.Core.Taste;

public sealed record FeatureWeight(string Name, double Weight);
public sealed record DistributionBucket(string Label, int Count);
public sealed record InfluentialGame(string Name, int Count);
public sealed record LibraryComposition(int Played, int Backlog, int Unmatched, int Hidden);

public sealed record TasteProfile(
    IReadOnlyList<FeatureWeight> TopGenres,
    IReadOnlyList<FeatureWeight> TopThemes,
    IReadOnlyList<DistributionBucket> PlaytimeDistribution,
    LibraryComposition Composition,
    IReadOnlyList<InfluentialGame> MostInfluential);

public sealed record ScorerComparisonRow(
    string Mode, double Delta, double RecallAt10, double IntraListDiversity, int DistinctFeatureCoverage);

public sealed record RecommenderHealth(
    double RecallAt10, double IntraListDiversity, int DistinctFeatureCoverage,
    double SpreadMin, double SpreadMax, double SpreadMean, double SpreadStdev,
    IReadOnlyList<ScorerComparisonRow> Comparison);

public sealed class InsightsService
{
    private const int TopN = 8;
    private const int HealthK = 10;

    public TasteProfile AnalyzeTaste(IReadOnlyList<Game> library, RecommenderConfig cfg)
    {
        var affinity = new AffinityCalculator(cfg.PlayedFloorMinutes, cfg.RecencyWeight);

        var genre = new Dictionary<string, double>();
        var theme = new Dictionary<string, double>();
        int played = 0, backlog = 0, unmatched = 0, hidden = 0;
        var dist = new int[5]; // [unplayed, <2h, 2-10h, 10-50h, 50h+]

        foreach (var g in library)
        {
            if (g.NotInterested) hidden++;
            else if (g.IgdbId is null || g.FeatureKeys.Count == 0) unmatched++;
            else if (affinity.IsPlayed(g.PlaytimeForeverMinutes)) played++;
            else backlog++;

            dist[Bucket(g.PlaytimeForeverMinutes)]++;

            if (g.NotInterested || !affinity.IsPlayed(g.PlaytimeForeverMinutes)) continue;
            var aff = affinity.Affinity(g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes);
            foreach (var key in g.FeatureKeys)
            {
                if (key.StartsWith("genre:", StringComparison.Ordinal)) Add(genre, Humanize(key), aff);
                else if (key.StartsWith("theme:", StringComparison.Ordinal)) Add(theme, Humanize(key), aff);
            }
        }

        var buckets = new[]
        {
            new DistributionBucket("Unplayed", dist[0]),
            new DistributionBucket("<2h", dist[1]),
            new DistributionBucket("2–10h", dist[2]),
            new DistributionBucket("10–50h", dist[3]),
            new DistributionBucket("50h+", dist[4]),
        };

        return new TasteProfile(
            TopWeights(genre), TopWeights(theme), buckets,
            new LibraryComposition(played, backlog, unmatched, hidden),
            MostInfluential(library, cfg));
    }

    public RecommenderHealth ComputeHealth(IReadOnlyList<Game> library, RecommenderConfig cfg)
    {
        var affinity = new AffinityCalculator(cfg.PlayedFloorMinutes, cfg.RecencyWeight);

        var liked = new List<ProfileItem>();
        var candidates = new List<CandidateItem>();
        foreach (var g in library)
        {
            if (g.NotInterested) continue;
            var vec = GameFeatureExtractor.ToVector(g);
            if (vec.IsEmpty) continue;
            var id = g.SteamAppId.ToString();
            if (affinity.IsPlayed(g.PlaytimeForeverMinutes))
                liked.Add(new ProfileItem(id, vec,
                    affinity.Affinity(g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes)));
            else
                candidates.Add(new CandidateItem(id, vec));
        }

        var baseOptions = Options(cfg, cfg.ScorerMode, cfg.Diversity);
        var (recall, diversity, coverage, spread) = Evaluate(liked, candidates, baseOptions);

        var rows = new List<ScorerComparisonRow>();
        var deltas = cfg.Diversity > 0 ? new[] { 0.0, cfg.Diversity } : new[] { 0.0 };
        foreach (var mode in new[] { "NearestNeighbor", "Centroid", "Hybrid" })
            foreach (var delta in deltas)
            {
                var (r, d, c, _) = Evaluate(liked, candidates, Options(cfg, mode, delta));
                rows.Add(new ScorerComparisonRow(mode, delta, r, d, c));
            }

        return new RecommenderHealth(recall, diversity, coverage,
            spread.Min, spread.Max, spread.Mean, spread.Stdev, rows);
    }

    private static RecommenderOptions Options(RecommenderConfig cfg, string mode, double delta) => new()
    {
        K = cfg.K,
        Mode = Enum.TryParse<ScorerMode>(mode, out var m) ? m : ScorerMode.NearestNeighbor,
        CategoryWeights = cfg.CategoryWeights,
        NegativeWeight = cfg.NegativeWeight,
        HybridAlpha = cfg.HybridAlpha,
        Diversity = delta,
    };

    private static (double Recall, double Diversity, int Coverage,
        (double Min, double Max, double Mean, double Stdev) Spread) Evaluate(
        IReadOnlyList<ProfileItem> liked, IReadOnlyList<CandidateItem> candidates, RecommenderOptions options)
    {
        var evaluator = new RecommenderEvaluator();
        double recall = evaluator.LeaveOneOutRecallAtK(liked, candidates, HealthK, options);

        var ranked = new ContentRecommender().Recommend(liked, candidates, options, null);
        var byId = candidates.ToDictionary(c => c.ItemId, c => c.Features);
        var vectors = ranked.Recommendations.Take(HealthK)
            .Where(r => byId.ContainsKey(r.ItemId))
            .Select(r => byId[r.ItemId]).ToList();
        double diversity = evaluator.IntraListDiversity(vectors);
        int coverage = evaluator.DistinctFeatureCoverage(vectors);
        var spread = evaluator.ScoreSpread(ranked.Recommendations.Take(HealthK).Select(r => r.Score).ToList());
        return (recall, diversity, coverage, spread);
    }

    private static IReadOnlyList<InfluentialGame> MostInfluential(IReadOnlyList<Game> library, RecommenderConfig cfg)
    {
        var tally = new Dictionary<string, int>();
        var result = new RecommendationService().Recommend(library, cfg);
        foreach (var rec in result.Recommendations)
            foreach (var name in rec.WhyLikedNames)
                tally[name] = tally.GetValueOrDefault(name) + 1;
        return tally.OrderByDescending(kv => kv.Value).Take(TopN)
            .Select(kv => new InfluentialGame(kv.Key, kv.Value)).ToList();
    }

    private static void Add(Dictionary<string, double> d, string k, double v) =>
        d[k] = d.GetValueOrDefault(k) + v;

    private static IReadOnlyList<FeatureWeight> TopWeights(Dictionary<string, double> d)
        => d.OrderByDescending(kv => kv.Value).Take(TopN)
            .Select(kv => new FeatureWeight(kv.Key, kv.Value)).ToList();

    private static int Bucket(int minutes) => minutes switch
    {
        0 => 0,
        < 120 => 1,
        < 600 => 2,
        < 3000 => 3,
        _ => 4,
    };

    private static string Humanize(string featureKey)
    {
        var i = featureKey.IndexOf(':');
        var raw = i < 0 ? featureKey : featureKey[(i + 1)..];
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.Replace('-', ' '));
    }
}
