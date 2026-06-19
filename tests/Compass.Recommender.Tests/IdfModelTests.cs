using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class IdfModelTests
{
    private static RecommenderOptions Opts() => new()
    {
        CategoryWeights = new Dictionary<string, double> { ["genre"] = 1.0, ["keyword"] = 0.5 }
    };

    [Fact]
    public void FeatureOnEveryItem_HasZeroIdf_AndDropsOut()
    {
        // 2 items, both have genre:rpg → idf = log(2/2) = 0
        var corpus = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:rpg", "keyword:dragons" }),
            FeatureVector.FromKeys(new[] { "genre:rpg" }),
        };
        var idf = IdfModel.Fit(corpus, Opts());
        var weighted = idf.Weight(corpus[0]); // before normalization
        weighted.Should().NotContainKey("genre:rpg");     // idf 0 dropped
        weighted.Should().ContainKey("keyword:dragons");  // df 1 of 2 kept
    }

    [Fact]
    public void RareFeature_OutweighsCommon_AfterCategoryAndIdf()
    {
        // genre:common appears in 2 of 3 items → idf = log(3/2) > 0
        // genre:rare appears in 1 of 3 items  → idf = log(3/1) > idf(common)
        // Both are kept (neither is on ALL items), and rare must outweigh common.
        var corpus = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:common", "genre:rare" }),
            FeatureVector.FromKeys(new[] { "genre:common" }),
            FeatureVector.FromKeys(new[] { "genre:other" }),
        };
        var idf = IdfModel.Fit(corpus, Opts());
        var w = idf.Weight(corpus[0]);
        w.Should().ContainKey("genre:rare");
        w.Should().ContainKey("genre:common");
        w["genre:rare"].Should().BeGreaterThan(w["genre:common"]);
    }
}
