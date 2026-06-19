using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

/// <summary>
/// Verifies that GameRecommendation.WhyFeatures carries humanized names AND
/// contribution magnitudes, in the same order the engine emits them.
/// </summary>
public class WhyFeatureTests
{
    private static RecommenderConfig Cfg() => new()
    {
        PlayedFloorMinutes = 120, RecencyWeight = 0.25, K = 5, ScorerMode = "NearestNeighbor",
        CategoryWeights = new() { ["genre"] = 1.0, ["theme"] = 1.0 }
    };

    private static Game Played(int id, string name, int mins, params string[] f) =>
        new() { SteamAppId = id, Name = name, PlaytimeForeverMinutes = mins, FeatureKeys = f, IgdbId = id };

    private static Game Backlog(int id, string name, params string[] f) =>
        new() { SteamAppId = id, Name = name, PlaytimeForeverMinutes = 0, FeatureKeys = f, IgdbId = id };

    [Fact]
    public void WhyFeatures_CarriesHumanizedNames_AndContributions_InDescendingOrder()
    {
        // Seed with two features; the top recommendation should share both.
        var library = new[]
        {
            Played(1, "Loved RPG", 6000, "genre:rpg", "theme:fantasy"),
            Backlog(2, "More RPG",        "genre:rpg", "theme:fantasy"),
            Backlog(3, "Unrelated",       "genre:cozy"),
        };

        var svc = new RecommendationService();
        var result = svc.Recommend(library, Cfg());

        var top = result.Recommendations.First();
        top.Game.Name.Should().Be("More RPG");

        // WhyFeatures must be the new typed list
        top.WhyFeatures.Should().NotBeEmpty();

        // Each entry must have a humanized Name (no "genre:" prefix) and a positive Contribution
        top.WhyFeatures.Should().AllSatisfy(wf =>
        {
            wf.Name.Should().NotContain(":");
            wf.Contribution.Should().BeGreaterThan(0);
        });

        // Contributions must be in descending order (engine emits them that way)
        var contribs = top.WhyFeatures.Select(w => w.Contribution).ToList();
        contribs.Should().BeInDescendingOrder();
    }
}
