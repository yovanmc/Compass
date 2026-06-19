using Compass.Data.Match;
using FluentAssertions;
using Xunit;

public class StringSimilarityTests
{
    [Fact]
    public void Identical_IsOne()
        => StringSimilarity.TokenSortRatio("doom eternal", "doom eternal").Should().Be(1.0);

    [Fact]
    public void TokenOrderInsensitive()
        => StringSimilarity.TokenSortRatio("hunt wild witcher", "witcher wild hunt")
            .Should().BeApproximately(1.0, 1e-9);

    [Fact]
    public void Different_IsLow()
        => StringSimilarity.TokenSortRatio("celeste", "doom").Should().BeLessThan(0.5);

    [Fact]
    public void NearMiss_IsHigh()
        => StringSimilarity.TokenSortRatio("stardew valley", "stardew vally").Should().BeGreaterThan(0.85);
}
