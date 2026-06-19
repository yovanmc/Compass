namespace Compass.Recommender;

public sealed class IdfModel
{
    private readonly IReadOnlyDictionary<string, double> _idf;
    private readonly RecommenderOptions _options;

    private IdfModel(IReadOnlyDictionary<string, double> idf, RecommenderOptions options)
        => (_idf, _options) = (idf, options);

    public static IdfModel Fit(IReadOnlyCollection<FeatureVector> corpus, RecommenderOptions options)
    {
        int n = corpus.Count;
        var df = new Dictionary<string, int>();
        foreach (var v in corpus)
            foreach (var key in v.Weights.Keys)
                df[key] = df.TryGetValue(key, out var c) ? c + 1 : 1;

        var idf = new Dictionary<string, double>(df.Count);
        foreach (var (key, d) in df)
            idf[key] = n == 0 ? 0 : Math.Log((double)n / d); // df on all → 0
        return new IdfModel(idf, options);
    }

    /// Apply category-weight × idf × rawWeight (NOT normalized).
    public Dictionary<string, double> Weight(FeatureVector v)
    {
        var result = new Dictionary<string, double>(v.Weights.Count);
        foreach (var (key, raw) in v.Weights)
        {
            if (!_idf.TryGetValue(key, out var idf) || idf == 0) continue;
            var cat = FeatureVector.CategoryOf(key);
            var w = _options.WeightFor(cat) * idf * raw;
            if (w != 0) result[key] = w;
        }
        return result;
    }

    /// Weighted + L2-normalized (ready for cosine).
    public Dictionary<string, double> WeightNormalized(FeatureVector v)
        => VectorMath.L2Normalize(Weight(v));
}
