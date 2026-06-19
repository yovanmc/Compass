using Compass.Core.Sync;
using FluentAssertions;
using Xunit;

public class FeatureKeyTests
{
    [Theory]
    [InlineData("genre", "Role-playing (RPG)", "genre:role-playing-rpg")]
    [InlineData("theme", "Science fiction", "theme:science-fiction")]
    [InlineData("mode", "Single player", "mode:single-player")]
    public void Build(string cat, string name, string expected)
        => FeatureKey.Build(cat, name).Should().Be(expected);
}
