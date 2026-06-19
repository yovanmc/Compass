using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class MmrDiversityTests
{
    private static RecommenderOptions KnnWithDiversity(double diversity) => new()
    {
        K = 5,
        Mode = ScorerMode.NearestNeighbor,
        Diversity = diversity,
        CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 }
    };

    /// <summary>
    /// With Diversity=0 the returned order must equal pure-relevance descending order.
    /// Back-compat guarantee: δ=0 skips MMR entirely.
    /// </summary>
    [Fact]
    public void DiversityZero_ReproducesPureRelevanceOrder()
    {
        // Liked: strategy+scifi profile.
        // Candidates: varying overlap so scores are clearly ordered.
        // IDF trap guard: "genre:scifi" must not appear in EVERY corpus item,
        // so we include a "genre:cozy" candidate to keep scifi non-ubiquitous.
        var liked = new[]
        {
            new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" }), Affinity: 5),
        };
        var candidates = new[]
        {
            new CandidateItem("high",   FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("medium", FeatureVector.FromKeys(new[] { "genre:scifi" })),
            new CandidateItem("low",    FeatureVector.FromKeys(new[] { "genre:cozy" })),
        };

        var withDiversityZero  = new ContentRecommender().Recommend(liked, candidates, KnnWithDiversity(0.0));
        var withDiversityNone  = new ContentRecommender().Recommend(liked, candidates, new RecommenderOptions
        {
            K = 5, Mode = ScorerMode.NearestNeighbor,
            CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 }
        });

        // Order must match the pure-relevance (no-Diversity) ordering.
        var idsWithZero = withDiversityZero.Recommendations.Select(r => r.ItemId).ToList();
        var idsDefault  = withDiversityNone.Recommendations.Select(r => r.ItemId).ToList();
        idsWithZero.Should().Equal(idsDefault);

        // Sanity: scores are in descending order.
        var scores = withDiversityZero.Recommendations.Select(r => r.Score).ToList();
        scores.Should().BeInDescendingOrder();
    }

    /// <summary>
    /// With Diversity=0.6, a candidate that is distinct from the top-ranked group
    /// must be promoted into the top-2, even if its raw relevance is lower.
    ///
    /// Setup:
    ///   Liked: [genre:scifi]          (strategy is ubiquitous → idf=0 → drops out;
    ///                                   scifi is NOT in the distinct item → idf > 0)
    ///   near1/near2/near3: [genre:strategy, genre:scifi]  — all near-identical after IDF
    ///   distinct:          [genre:strategy, genre:fantasy] — no scifi → low relevance,
    ///                                                        but zero cosine to selected scifi items
    ///
    /// IDF notes:
    ///   "genre:strategy" is in liked + all 4 candidates = 5/5 docs → idf=0 → drops out.
    ///   "genre:scifi"    is in liked + near1/near2/near3 = 4/5 docs → idf > 0.
    ///   "genre:fantasy"  is in distinct only = 1/5 docs → idf > 0.
    ///
    /// After IDF: near items have only "scifi" → cosine(near_i, near_j) ≈ 1.
    ///            distinct has only "fantasy" → cosine(distinct, any_near) = 0.
    ///
    /// MMR (δ=0.6): after selecting near1 (highest relevance),
    ///   near2 MMR ≈ 0.4·high − 0.6·~1.0 → very low
    ///   distinct  ≈ 0.4·low  − 0.6·0    → 0.4·low  (no penalty)
    /// → distinct is promoted to position 2 (top-2).
    /// </summary>
    [Fact]
    public void DiversityHigh_PromotesTheDistinctCandidate()
    {
        var liked = new[]
        {
            new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" }), Affinity: 5),
        };
        var candidates = new[]
        {
            new CandidateItem("near1",   FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("near2",   FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("near3",   FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("distinct", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:fantasy" })),
        };

        var result = new ContentRecommender().Recommend(liked, candidates, KnnWithDiversity(0.6));
        var top2Ids = result.Recommendations.Take(2).Select(r => r.ItemId).ToList();

        top2Ids.Should().Contain("distinct",
            because: "MMR with δ=0.6 should promote the genre:fantasy item that has zero cosine to the selected scifi cluster");
    }
}
