using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

public class InsightsServiceTests
{
    private static Game G(int id, string name, int playtime, IReadOnlyList<string> feats,
        bool hide = false) => new()
    {
        SteamAppId = id, Name = name, PlaytimeForeverMinutes = playtime,
        IgdbId = feats.Count > 0 ? id : null, FeatureKeys = feats, NotInterested = hide,
        MatchMethod = feats.Count > 0 ? MatchMethod.AppId : MatchMethod.None,
    };
    private static RecommenderConfig Cfg() => new() { PlayedFloorMinutes = 120, K = 5 };

    [Fact]
    public void AnalyzeTaste_RanksMostPlayedGenresFirst_AndCountsComposition()
    {
        var lib = new[]
        {
            G(1, "Civ", 6000, new[] { "genre:strategy", "theme:historical" }),
            G(2, "Stellaris", 3000, new[] { "genre:strategy", "theme:sci-fi" }),
            G(3, "Witcher", 600, new[] { "genre:rpg", "theme:fantasy" }),
            G(4, "Backlog ShMup", 0, new[] { "genre:shooter" }),
            G(5, "Mystery", 0, Array.Empty<string>()),
            G(6, "Hidden", 5000, new[] { "genre:horror" }, hide: true),
        };
        var taste = new InsightsService().AnalyzeTaste(lib, Cfg());

        taste.TopGenres.First().Name.Should().Be("Strategy");
        taste.Composition.Played.Should().Be(3);
        taste.Composition.Backlog.Should().Be(1);
        taste.Composition.Unmatched.Should().Be(1);
        taste.Composition.Hidden.Should().Be(1);
        taste.PlaytimeDistribution.Sum(b => b.Count).Should().Be(6);
    }

    [Fact]
    public void ComputeHealth_ProducesPositiveRecall_AndOneRowPerScorerMode()
    {
        var sci = new[] { "genre:strategy", "theme:sci-fi" };
        var lib = new[]
        {
            G(1, "Played A", 4000, sci),
            G(2, "Played B", 3000, sci),
            G(3, "Backlog A", 0, sci),
            G(4, "Backlog B", 0, new[] { "genre:strategy", "theme:fantasy" }),
            G(5, "Backlog C", 0, new[] { "genre:puzzle" }),
        };
        var health = new InsightsService().ComputeHealth(lib, new RecommenderConfig
        { PlayedFloorMinutes = 120, K = 5, Diversity = 0.3 });

        health.RecallAt10.Should().BeGreaterThan(0.0);
        health.Comparison.Select(r => r.Mode).Distinct()
            .Should().BeEquivalentTo(new[] { "NearestNeighbor", "Centroid", "Hybrid" });
        health.IntraListDiversity.Should().BeGreaterThanOrEqualTo(0.0);
        health.DistinctFeatureCoverage.Should().BeGreaterThan(0);
    }
}
