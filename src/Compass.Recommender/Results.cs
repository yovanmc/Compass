namespace Compass.Recommender;

public sealed record FeatureContribution(string FeatureKey, double Contribution);

public sealed record Recommendation(
    string ItemId,
    double Score,
    IReadOnlyList<FeatureContribution> TopFeatures,
    IReadOnlyList<string> NearestLikedItemIds);

public sealed record RankedResult(IReadOnlyList<Recommendation> Recommendations);
