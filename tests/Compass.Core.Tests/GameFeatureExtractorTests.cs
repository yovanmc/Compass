using Compass.Core.Model;
using Compass.Core.Taste;
using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class GameFeatureExtractorTests
{
    [Fact]
    public void BuildsNamespacedVectorFromGame()
    {
        var g = new Game { SteamAppId = 1, Name = "X",
            FeatureKeys = new[] { "genre:strategy", "theme:sci-fi" } };
        var v = GameFeatureExtractor.ToVector(g);
        v.Weights.Keys.Should().BeEquivalentTo(new[] { "genre:strategy", "theme:sci-fi" });
    }

    [Fact]
    public void NoFeatures_ProducesEmptyVector()
        => GameFeatureExtractor.ToVector(new Game { SteamAppId = 1, Name = "X" })
            .IsEmpty.Should().BeTrue();
}
