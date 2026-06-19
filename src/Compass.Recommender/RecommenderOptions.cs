namespace Compass.Recommender;

public enum ScorerMode { NearestNeighbor, Centroid, Hybrid }

public sealed class RecommenderOptions
{
    public int K { get; init; } = 5;
    public ScorerMode Mode { get; init; } = ScorerMode.NearestNeighbor;
    public double HybridAlpha { get; init; } = 0.5;
    public IReadOnlyDictionary<string, double> CategoryWeights { get; init; }
        = new Dictionary<string, double>();
    public double DefaultCategoryWeight { get; init; } = 1.0;
    public int MaxExplanationFeatures { get; init; } = 3;
    public int MaxExplanationNeighbors { get; init; } = 3;

    public double WeightFor(string category)
        => CategoryWeights.TryGetValue(category, out var w) ? w : DefaultCategoryWeight;
}
