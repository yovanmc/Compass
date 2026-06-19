namespace Compass.Recommender;

public sealed class FeatureVector
{
    private readonly Dictionary<string, double> _weights;
    public IReadOnlyDictionary<string, double> Weights => _weights;

    public FeatureVector(IEnumerable<KeyValuePair<string, double>> weights)
        => _weights = new Dictionary<string, double>(weights);

    public static FeatureVector FromKeys(IEnumerable<string> keys)
    {
        var d = new Dictionary<string, double>();
        foreach (var k in keys) d[k] = 1.0;
        return new FeatureVector(d);
    }

    public bool IsEmpty => _weights.Count == 0;

    public static string CategoryOf(string featureKey)
    {
        var i = featureKey.IndexOf(':');
        return i < 0 ? "" : featureKey[..i];
    }
}
