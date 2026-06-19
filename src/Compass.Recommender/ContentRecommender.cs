namespace Compass.Recommender;

public sealed class ContentRecommender : IRecommender
{
    public RankedResult Recommend(
        IReadOnlyList<ProfileItem> liked,
        IReadOnlyList<CandidateItem> candidates,
        RecommenderOptions options,
        IReadOnlyList<ProfileItem>? disliked = null)
    {
        disliked ??= Array.Empty<ProfileItem>();

        if (liked.Count == 0 || candidates.Count == 0)
            return new RankedResult(Array.Empty<Recommendation>());

        // Fit IDF over the combined corpus (liked + disliked + candidates) so weights
        // reflect the whole library including items the user doesn't want.
        var corpus = new List<FeatureVector>(liked.Count + disliked.Count + candidates.Count);
        corpus.AddRange(liked.Select(l => l.Features));
        corpus.AddRange(disliked.Select(d => d.Features));
        corpus.AddRange(candidates.Select(c => c.Features));
        var idf = IdfModel.Fit(corpus, options);

        // Pre-weight liked vectors; drop any that normalize to empty (no usable features).
        var likedVecs = new List<(string id, double affinity, Dictionary<string, double> vec)>();
        foreach (var l in liked)
        {
            var v = idf.WeightNormalized(l.Features);
            if (v.Count > 0 && l.Affinity > 0) likedVecs.Add((l.ItemId, l.Affinity, v));
        }
        if (likedVecs.Count == 0)
            return new RankedResult(Array.Empty<Recommendation>());

        // Pre-weight disliked vectors.
        var dislikedVecs = new List<(string id, double affinity, Dictionary<string, double> vec)>();
        foreach (var d in disliked)
        {
            var v = idf.WeightNormalized(d.Features);
            if (v.Count > 0 && d.Affinity > 0) dislikedVecs.Add((d.ItemId, d.Affinity, v));
        }

        // Centroid (affinity-weighted, normalized) — used for hybrid + feature attribution.
        var centroidRaw = new Dictionary<string, double>();
        foreach (var (_, aff, vec) in likedVecs)
            foreach (var (k, w) in vec)
                centroidRaw[k] = centroidRaw.TryGetValue(k, out var p) ? p + aff * w : aff * w;
        var centroid = VectorMath.L2Normalize(centroidRaw);

        // Build scored candidates; track the normalized vector alongside each for MMR.
        var scored = new List<(Recommendation rec, Dictionary<string, double> normVec)>(candidates.Count);
        foreach (var c in candidates)
        {
            var cv = idf.WeightNormalized(c.Features);
            if (cv.Count == 0)
            {
                scored.Add((new Recommendation(c.ItemId, 0,
                    Array.Empty<FeatureContribution>(), Array.Empty<string>(), Array.Empty<string>()),
                    cv));
                continue;
            }

            // Positive kNN (affinity-weighted, preserves v1 numbers exactly).
            var (knn, neighbors) = AffinityKnn(cv, likedVecs, options.K, options.MaxExplanationNeighbors);

            double centroidSim = VectorMath.Dot(cv, centroid);
            double positiveScore = options.Mode switch
            {
                ScorerMode.Centroid => centroidSim,
                ScorerMode.Hybrid => options.HybridAlpha * centroidSim + (1 - options.HybridAlpha) * knn,
                _ => knn
            };

            // Negative penalty (same affinity-weighted top-k routine against disliked).
            double negSim = 0;
            List<string> penalizedBy = new();
            if (dislikedVecs.Count > 0 && options.NegativeWeight > 0)
            {
                (negSim, penalizedBy) = AffinityKnn(cv, dislikedVecs, options.K, options.MaxExplanationNeighbors);
                if (negSim <= 0) penalizedBy = new List<string>();
            }

            double finalScore = Math.Max(0, positiveScore - options.NegativeWeight * negSim);

            // Feature attribution via centroid contribution.
            var contributions = cv
                .Select(kv => new FeatureContribution(kv.Key,
                    kv.Value * (centroid.TryGetValue(kv.Key, out var cw) ? cw : 0)))
                .Where(fc => fc.Contribution > 0)
                .OrderByDescending(fc => fc.Contribution)
                .Take(options.MaxExplanationFeatures)
                .ToList();

            scored.Add((new Recommendation(c.ItemId, finalScore, contributions, neighbors, penalizedBy), cv));
        }

        // Sort by descending relevance score (pure-relevance order).
        scored.Sort((a, b) => b.rec.Score.CompareTo(a.rec.Score));

        // MMR re-rank: if δ=0 skip entirely (exact back-compat).
        if (options.Diversity <= 0)
            return new RankedResult(scored.Select(s => s.rec).ToList());

        // Greedy MMR: repeatedly pick argmax of (1−δ)·relevance(d) − δ·maxCosine(d, selected).
        // Vectors are already L2-normalized, so dot product == cosine similarity.
        double delta = options.Diversity;
        var remaining = new List<(Recommendation rec, Dictionary<string, double> normVec)>(scored);
        var selected = new List<Recommendation>(remaining.Count);
        // Track the normalized vectors of selected items separately for O(1) inner-loop access.
        var selectedVecs = new List<Dictionary<string, double>>(remaining.Count);

        while (remaining.Count > 0)
        {
            if (selected.Count == 0)
            {
                // First pick is always the highest-relevance item (already at index 0).
                selected.Add(remaining[0].rec);
                selectedVecs.Add(remaining[0].normVec);
                remaining.RemoveAt(0);
                continue;
            }

            int bestIdx = 0;
            double bestMmr = double.NegativeInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                var (rec, normVec) = remaining[i];
                double maxSim = 0;
                foreach (var selVec in selectedVecs)
                {
                    double sim = VectorMath.Dot(normVec, selVec);
                    if (sim > maxSim) maxSim = sim;
                }
                double mmr = (1 - delta) * rec.Score - delta * maxSim;
                if (mmr > bestMmr)
                {
                    bestMmr = mmr;
                    bestIdx = i;
                }
            }

            selected.Add(remaining[bestIdx].rec);
            selectedVecs.Add(remaining[bestIdx].normVec);
            remaining.RemoveAt(bestIdx);
        }

        return new RankedResult(selected);
    }

    // Affinity-weighted kNN similarity. Returns (score, neighborIds).
    // Used for both positive and negative profiles so the math is identical.
    private static (double score, List<string> neighborIds) AffinityKnn(
        Dictionary<string, double> cv,
        List<(string id, double affinity, Dictionary<string, double> vec)> profile,
        int k,
        int maxNeighbors)
    {
        var sims = profile
            .Select(p => (p.id, p.affinity, sim: VectorMath.Dot(cv, p.vec)))
            .OrderByDescending(t => t.sim)
            .ToList();

        var topK = sims.Take(k).Where(t => t.sim > 0).ToList();

        double score = 0;
        if (topK.Count > 0)
        {
            double num = topK.Sum(t => t.affinity * t.sim);
            double den = topK.Sum(t => t.affinity);
            score = den > 0 ? num / den : 0;
        }

        return (score, topK.Take(maxNeighbors).Select(t => t.id).ToList());
    }
}
