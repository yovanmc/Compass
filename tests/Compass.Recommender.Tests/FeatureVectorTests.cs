using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class FeatureVectorTests
{
    [Fact]
    public void FromKeys_AssignsRawWeightOne_AndDedups()
    {
        var v = FeatureVector.FromKeys(new[] { "genre:strategy", "genre:strategy", "theme:sci-fi" });
        v.Weights.Should().HaveCount(2);
        v.Weights["genre:strategy"].Should().Be(1.0);
        v.Weights["theme:sci-fi"].Should().Be(1.0);
    }

    [Fact]
    public void CategoryOf_ReturnsPrefixBeforeColon()
        => FeatureVector.CategoryOf("mode:single-player").Should().Be("mode");

    [Fact]
    public void CategoryOf_NoColon_ReturnsEmpty()
        => FeatureVector.CategoryOf("weird").Should().Be("");
}
