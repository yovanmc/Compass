using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

public class AffinityCalculatorTests
{
    private static AffinityCalculator Calc() => new(playedFloorMinutes: 120, recencyWeight: 0.25);

    [Fact]
    public void BelowFloor_IsZero()
        => Calc().Affinity(foreverMinutes: 60, twoWeekMinutes: 0).Should().Be(0);

    [Fact]
    public void AtFloor_IsPositive()
        => Calc().Affinity(120, 0).Should().BeGreaterThan(0);

    [Fact]
    public void MoreHours_MoreAffinity_ButSublinear()
    {
        var c = Calc();
        var a100 = c.Affinity(100 * 60, 0);
        var a1000 = c.Affinity(1000 * 60, 0);
        a1000.Should().BeGreaterThan(a100);
        (a1000 / a100).Should().BeLessThan(10); // log keeps a 10x-hours game from being 10x weight
    }

    [Fact]
    public void RecentPlay_AddsBoost()
    {
        var c = Calc();
        c.Affinity(600, 600).Should().BeGreaterThan(c.Affinity(600, 0));
    }

    [Fact]
    public void IsPlayed_TracksFloor()
    {
        var c = Calc();
        c.IsPlayed(119).Should().BeFalse();
        c.IsPlayed(120).Should().BeTrue();
    }
}
