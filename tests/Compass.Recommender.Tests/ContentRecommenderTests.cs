using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class ContentRecommenderTests
{
    private static RecommenderOptions Knn(int k = 5) => new()
    {
        K = k, Mode = ScorerMode.NearestNeighbor,
        CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 }
    };

    [Fact]
    public void EmptyLiked_ReturnsNoRecommendations()
    {
        var r = new ContentRecommender();
        var res = r.Recommend(
            Array.Empty<ProfileItem>(),
            new[] { new CandidateItem("c1", FeatureVector.FromKeys(new[] { "genre:rpg" })) },
            Knn());
        res.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public void RanksFeatureOverlapHigher()
    {
        var liked = new[]
        {
            new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" }), Affinity: 5),
        };
        var candidates = new[]
        {
            new CandidateItem("match", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("nomatch", FeatureVector.FromKeys(new[] { "genre:cozy" })),
        };
        var res = new ContentRecommender().Recommend(liked, candidates, Knn());
        res.Recommendations.First().ItemId.Should().Be("match");
        res.Recommendations.First().Score.Should().BeGreaterThan(res.Recommendations.Last().Score);
    }

    [Fact]
    public void KnnPreservesClusters_NotBeigeCentroid()
    {
        // Two distinct taste clusters. A candidate that perfectly matches ONE cluster
        // should beat a candidate that weakly matches the average of both.
        var liked = new[]
        {
            new ProfileItem("cozy", FeatureVector.FromKeys(new[] { "genre:cozy", "genre:farming" }), 5),
            new ProfileItem("rogue", FeatureVector.FromKeys(new[] { "genre:roguelike", "genre:hard" }), 5),
        };
        var clusterMatch = new CandidateItem("clusterMatch",
            FeatureVector.FromKeys(new[] { "genre:roguelike", "genre:hard" }));
        var blandMiddle = new CandidateItem("blandMiddle",
            FeatureVector.FromKeys(new[] { "genre:cozy", "genre:roguelike" }));
        var res = new ContentRecommender().Recommend(liked, new[] { clusterMatch, blandMiddle }, Knn(k: 1));
        res.Recommendations.First().ItemId.Should().Be("clusterMatch");
    }

    [Fact]
    public void Explanation_NamesSharedFeatures_AndNearestLiked()
    {
        // Need a 3rd corpus item so shared features don't appear in ALL items (idf stays > 0).
        var liked = new[]
        {
            new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" }), 5),
        };
        var candidates = new[]
        {
            new CandidateItem("c", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("other", FeatureVector.FromKeys(new[] { "genre:cozy" })),
        };
        var recs = new ContentRecommender().Recommend(liked, candidates, Knn()).Recommendations;
        var rec = recs.Single(r => r.ItemId == "c");
        rec.NearestLikedItemIds.Should().Contain("L1");
        rec.TopFeatures.Select(f => f.FeatureKey).Should().Contain("genre:strategy");
    }

    [Fact]
    public void ZeroFeatureCandidate_ScoresZero_NoNeighbors()
    {
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:x" }), 5) };
        var candidates = new[] { new CandidateItem("empty", new FeatureVector(new Dictionary<string, double>())) };
        var rec = new ContentRecommender().Recommend(liked, candidates, Knn()).Recommendations.Single();
        rec.Score.Should().Be(0);
        rec.NearestLikedItemIds.Should().BeEmpty();
    }

    [Fact]
    public void EmptyCandidates_ReturnsNoRecommendations()
    {
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy" }), 5) };
        var res = new ContentRecommender().Recommend(liked, Array.Empty<CandidateItem>(), Knn());
        res.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public void AffinityZeroLiked_IsExcluded_NoRecommendations()
    {
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy" }), Affinity: 0) };
        var candidates = new[] { new CandidateItem("c1", FeatureVector.FromKeys(new[] { "genre:strategy" })) };
        var res = new ContentRecommender().Recommend(liked, candidates, Knn());
        res.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public void AllLikedFeaturesUbiquitous_NormalizeToEmpty_NoRecommendations()
    {
        // genre:x appears in every corpus item (liked + candidates) → idf=0 → liked vector normalizes to empty → excluded
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:x" }), 5) };
        var candidates = new[] { new CandidateItem("c", FeatureVector.FromKeys(new[] { "genre:x" })) };
        var res = new ContentRecommender().Recommend(liked, candidates, Knn());
        res.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public void EmptyDisliked_MatchesV1Behavior()
    {
        // A second candidate with a different feature ensures genre:strategy is not ubiquitous
        // in the IDF corpus (not in 100% of docs), so its IDF stays positive.
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy" }), 5) };
        var cands = new[]
        {
            new CandidateItem("c",    FeatureVector.FromKeys(new[] { "genre:strategy", "genre:scifi" })),
            new CandidateItem("other", FeatureVector.FromKeys(new[] { "genre:cozy" })),
        };
        var opt = new RecommenderOptions { K = 5, Mode = ScorerMode.NearestNeighbor, CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 } };
        var withNull = new ContentRecommender().Recommend(liked, cands, opt);
        var withEmpty = new ContentRecommender().Recommend(liked, cands, opt, Array.Empty<ProfileItem>());
        withNull.Recommendations.Single(r => r.ItemId == "c").Score
            .Should().Be(withEmpty.Recommendations.Single(r => r.ItemId == "c").Score);
        withNull.Recommendations.Single(r => r.ItemId == "c").PenalizedByItemIds.Should().BeEmpty();
    }

    [Fact]
    public void CandidateSimilarToDisliked_IsPenalized()
    {
        var liked = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:strategy" }), 5) };
        var disliked = new[] { new ProfileItem("D1", FeatureVector.FromKeys(new[] { "genre:horror" }), 5) };
        var cands = new[]
        {
            new CandidateItem("clean",   FeatureVector.FromKeys(new[] { "genre:strategy" })),
            new CandidateItem("tainted", FeatureVector.FromKeys(new[] { "genre:strategy", "genre:horror" })),
        };
        var opt = new RecommenderOptions
        {
            K = 5, Mode = ScorerMode.NearestNeighbor, NegativeWeight = 1.0,
            CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 }
        };
        var res = new ContentRecommender().Recommend(liked, cands, opt, disliked);
        var clean   = res.Recommendations.Single(r => r.ItemId == "clean");
        var tainted = res.Recommendations.Single(r => r.ItemId == "tainted");
        tainted.Score.Should().BeLessThan(clean.Score);
        tainted.PenalizedByItemIds.Should().Contain("D1");
    }

    [Fact]
    public void Penalty_ClampsScoreAtZero()
    {
        // A second candidate with a different feature keeps genre:x out of 100% of corpus docs,
        // so its IDF stays positive and scores are computed (not collapsed to empty).
        var liked    = new[] { new ProfileItem("L1", FeatureVector.FromKeys(new[] { "genre:x" }), 5) };
        var disliked = new[] { new ProfileItem("D1", FeatureVector.FromKeys(new[] { "genre:x" }), 5) };
        var cands    = new[]
        {
            new CandidateItem("c",    FeatureVector.FromKeys(new[] { "genre:x" })),
            new CandidateItem("other", FeatureVector.FromKeys(new[] { "genre:y" })),
        };
        var opt = new RecommenderOptions { K = 5, NegativeWeight = 10.0, CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0 } };
        var rec = new ContentRecommender().Recommend(liked, cands, opt, disliked).Recommendations.Single(r => r.ItemId == "c");
        rec.Score.Should().Be(0);
    }
}
