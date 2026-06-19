namespace Compass.Recommender;

/// <summary>
/// Pure, BCL-only offline metrics for a content-based recommender.
/// No domain knowledge; operates on the engine's own types.
/// </summary>
public sealed class RecommenderEvaluator
{
    // ── IntraListDiversity ───────────────────────────────────────────────────

    /// <summary>
    /// Average pairwise (1 − cosine) over the plain L2-normalized feature vectors.
    /// Uses raw feature weights (no IDF), making it an independent variety measure.
    /// Returns 0 for fewer than 2 items.
    /// </summary>
    public double IntraListDiversity(IReadOnlyList<FeatureVector> vectors)
    {
        if (vectors.Count < 2)
            return 0.0;

        // L2-normalize each vector once over its raw Weights.
        var normed = new Dictionary<string, double>[vectors.Count];
        for (int i = 0; i < vectors.Count; i++)
            normed[i] = PlainL2Normalize(vectors[i].Weights);

        double totalDiversity = 0.0;
        int pairs = 0;

        for (int i = 0; i < normed.Length - 1; i++)
        {
            for (int j = i + 1; j < normed.Length; j++)
            {
                double cosine = VectorMath.Dot(normed[i], normed[j]);
                totalDiversity += 1.0 - cosine;
                pairs++;
            }
        }

        return pairs == 0 ? 0.0 : totalDiversity / pairs;
    }

    // ── DistinctFeatureCoverage ──────────────────────────────────────────────

    /// <summary>
    /// Count of distinct feature-dimension keys with non-zero weight across all vectors.
    /// </summary>
    public int DistinctFeatureCoverage(IReadOnlyList<FeatureVector> vectors)
    {
        var keys = new HashSet<string>();
        foreach (var v in vectors)
            foreach (var (k, w) in v.Weights)
                if (w != 0.0) keys.Add(k);
        return keys.Count;
    }

    // ── ScoreSpread ──────────────────────────────────────────────────────────

    /// <summary>
    /// Population statistics (min, max, mean, stdev) over a list of scores.
    /// Returns all-zero for an empty list; stdev = 0 for a single item.
    /// </summary>
    public (double Min, double Max, double Mean, double Stdev) ScoreSpread(
        IReadOnlyList<double> scores)
    {
        if (scores.Count == 0)
            return (0.0, 0.0, 0.0, 0.0);

        double min = scores[0];
        double max = scores[0];
        double sum = 0.0;

        foreach (var s in scores)
        {
            if (s < min) min = s;
            if (s > max) max = s;
            sum += s;
        }

        double mean = sum / scores.Count;

        if (scores.Count == 1)
            return (min, max, mean, 0.0);

        double sumSqDev = 0.0;
        foreach (var s in scores)
        {
            double dev = s - mean;
            sumSqDev += dev * dev;
        }

        double stdev = Math.Sqrt(sumSqDev / scores.Count); // population stdev
        return (min, max, mean, stdev);
    }

    // ── LeaveOneOutRecallAtK — isolated-candidate overload ───────────────────

    /// <summary>
    /// For each liked item with a non-empty vector, hold it out, present it as the
    /// sole candidate, and check whether it appears in the top-k returned ranking.
    /// Returns hits / evaluated; 0 if nothing was evaluated.
    /// </summary>
    public double LeaveOneOutRecallAtK(
        IReadOnlyList<ProfileItem> liked,
        int k,
        RecommenderOptions options)
        => LeaveOneOutRecallAtK(liked, Array.Empty<CandidateItem>(), k, options);

    // ── LeaveOneOutRecallAtK — shared-pool overload ──────────────────────────

    /// <summary>
    /// Same as the isolated overload but the held-out item competes against
    /// <paramref name="sharedPool"/> ∪ { heldOut } (realistic backlog scenario).
    /// </summary>
    public double LeaveOneOutRecallAtK(
        IReadOnlyList<ProfileItem> liked,
        IReadOnlyList<CandidateItem> sharedPool,
        int k,
        RecommenderOptions options)
    {
        if (liked.Count == 0)
            return 0.0;

        var recommender = new ContentRecommender();
        int hits = 0;
        int evaluated = 0;

        for (int i = 0; i < liked.Count; i++)
        {
            var heldOut = liked[i];

            // Skip items with empty feature vectors — they can never be retrieved.
            if (heldOut.Features.IsEmpty)
                continue;

            // Build liked' = all liked items except the held-out one.
            var likedPrime = new List<ProfileItem>(liked.Count - 1);
            for (int j = 0; j < liked.Count; j++)
                if (j != i) likedPrime.Add(liked[j]);

            // candidates = sharedPool ∪ { heldOut as a CandidateItem }
            var candidates = new List<CandidateItem>(sharedPool.Count + 1);
            candidates.AddRange(sharedPool);
            candidates.Add(new CandidateItem(heldOut.ItemId, heldOut.Features));

            var result = recommender.Recommend(likedPrime, candidates, options, null);

            // Check whether the held-out id appears in the top-k of the ranking
            // with a positive score (score=0 means the engine found no similarity —
            // the item only ranks "first" because it's the only candidate).
            var topK = result.Recommendations
                .Take(k)
                .Where(r => r.Score > 0);
            if (topK.Any(r => r.ItemId == heldOut.ItemId))
                hits++;

            evaluated++;
        }

        return evaluated == 0 ? 0.0 : (double)hits / evaluated;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// L2-normalizes raw feature weights, independent of the engine's IDF model.
    /// Returns an empty dictionary for a zero vector.
    /// </summary>
    private static Dictionary<string, double> PlainL2Normalize(
        IReadOnlyDictionary<string, double> weights)
    {
        double sumSq = 0.0;
        foreach (var w in weights.Values) sumSq += w * w;
        double norm = Math.Sqrt(sumSq);

        var result = new Dictionary<string, double>(weights.Count);
        if (norm == 0.0) return result;

        foreach (var (k, w) in weights)
            result[k] = w / norm;

        return result;
    }
}
