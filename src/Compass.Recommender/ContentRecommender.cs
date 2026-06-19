namespace Compass.Recommender;

public sealed class ContentRecommender : IRecommender
{
    public RankedResult Recommend(
        IReadOnlyList<ProfileItem> liked,
        IReadOnlyList<CandidateItem> candidates,
        RecommenderOptions options)
    {
        if (liked.Count == 0 || candidates.Count == 0)
            return new RankedResult(Array.Empty<Recommendation>());

        // Fit IDF over the combined corpus so weights reflect the whole library.
        var corpus = new List<FeatureVector>(liked.Count + candidates.Count);
        corpus.AddRange(liked.Select(l => l.Features));
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

        // Centroid (affinity-weighted, normalized) — used for hybrid + feature attribution.
        var centroidRaw = new Dictionary<string, double>();
        foreach (var (_, aff, vec) in likedVecs)
            foreach (var (k, w) in vec)
                centroidRaw[k] = centroidRaw.TryGetValue(k, out var p) ? p + aff * w : aff * w;
        var centroid = VectorMath.L2Normalize(centroidRaw);

        var recs = new List<Recommendation>(candidates.Count);
        foreach (var c in candidates)
        {
            var cv = idf.WeightNormalized(c.Features);
            if (cv.Count == 0)
            {
                recs.Add(new Recommendation(c.ItemId, 0,
                    Array.Empty<FeatureContribution>(), Array.Empty<string>()));
                continue;
            }

            // similarity to each liked item
            var sims = likedVecs
                .Select(l => (l.id, l.affinity, sim: VectorMath.Dot(cv, l.vec)))
                .OrderByDescending(t => t.sim)
                .ToList();

            var topK = sims.Take(options.K).Where(t => t.sim > 0).ToList();

            double knn = 0;
            if (topK.Count > 0)
            {
                double num = topK.Sum(t => t.affinity * t.sim);
                double den = topK.Sum(t => t.affinity);
                knn = den > 0 ? num / den : 0;
            }

            double centroidSim = VectorMath.Dot(cv, centroid);
            double score = options.Mode switch
            {
                ScorerMode.Centroid => centroidSim,
                ScorerMode.Hybrid => options.HybridAlpha * centroidSim + (1 - options.HybridAlpha) * knn,
                _ => knn
            };

            // Feature attribution via centroid contribution.
            var contributions = cv
                .Select(kv => new FeatureContribution(kv.Key,
                    kv.Value * (centroid.TryGetValue(kv.Key, out var cw) ? cw : 0)))
                .Where(fc => fc.Contribution > 0)
                .OrderByDescending(fc => fc.Contribution)
                .Take(options.MaxExplanationFeatures)
                .ToList();

            var neighbors = topK
                .Take(options.MaxExplanationNeighbors)
                .Select(t => t.id)
                .ToList();

            recs.Add(new Recommendation(c.ItemId, score, contributions, neighbors));
        }

        recs.Sort((a, b) => b.Score.CompareTo(a.Score));
        return new RankedResult(recs);
    }
}
