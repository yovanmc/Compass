using Compass.Data.Match;
using FluentAssertions;
using Xunit;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("The Witcher® 3: Wild Hunt", "witcher 3 wild hunt")]
    [InlineData("DOOM™", "doom")]
    [InlineData("Hades II", "hades ii")]
    [InlineData("Stardew Valley - Definitive Edition", "stardew valley")]
    [InlineData("Celeste (Deluxe)", "celeste")]
    public void Normalizes(string input, string expected)
        => NameNormalizer.Normalize(input).Should().Be(expected);
}
