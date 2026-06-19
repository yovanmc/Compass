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
}
