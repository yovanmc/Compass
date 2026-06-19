namespace Compass.Recommender;

public static class VectorMath
{
    public static Dictionary<string, double> L2Normalize(IReadOnlyDictionary<string, double> v)
    {
        double sumSq = 0;
        foreach (var w in v.Values) sumSq += w * w;
        var norm = Math.Sqrt(sumSq);
        var result = new Dictionary<string, double>(v.Count);
        if (norm == 0) return result;
        foreach (var (k, w) in v) result[k] = w / norm;
        return result;
    }

    public static double Dot(IReadOnlyDictionary<string, double> a,
                             IReadOnlyDictionary<string, double> b)
    {
        // iterate the smaller map
        if (a.Count > b.Count) (a, b) = (b, a);
        double sum = 0;
        foreach (var (k, wa) in a)
            if (b.TryGetValue(k, out var wb)) sum += wa * wb;
        return sum;
    }
}
