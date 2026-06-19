using Compass.Core.Sync;
using FluentAssertions;
using Xunit;

namespace Compass.Core.Tests;

public class SampleLibraryTests
{
    [Fact]
    public void Load_ReturnsExpectedCount()
    {
        var games = SampleLibrary.Load();
        // The fixture has 40 entries (37 matched + 3 unmatched).
        games.Should().HaveCount(40);
    }

    [Fact]
    public void Load_PlayedPartition_IsNonEmpty()
    {
        var games = SampleLibrary.Load();
        // Played = playtimeForeverMin >= 120
        var played = games.Where(g => g.PlaytimeForeverMin >= 120).ToList();
        played.Should().NotBeEmpty("there must be played games in the fixture");
        played.Count.Should().BeGreaterThanOrEqualTo(15, "spec requires ~15+ played games");
    }

    [Fact]
    public void Load_BacklogPartition_IsNonEmpty()
    {
        var games = SampleLibrary.Load();
        // Backlog = playtimeForeverMin < 120 AND has features (matched)
        var backlog = games
            .Where(g => g.PlaytimeForeverMin < 120 && g.FeatureKeys.Count > 0)
            .ToList();
        backlog.Should().NotBeEmpty("there must be backlog candidates in the fixture");
    }

    [Fact]
    public void Load_UnmatchedPartition_IsNonEmpty()
    {
        var games = SampleLibrary.Load();
        // Unmatched = igdbName == null (empty feature keys)
        var unmatched = games.Where(g => g.IgdbName == null).ToList();
        unmatched.Should().NotBeEmpty("there must be unmatched games in the fixture");
        unmatched.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Load_NoSingleFeatureAppearsOnEveryGame()
    {
        // This guards the IDF trap: a feature present on every corpus item gets IDF=0
        // and contributes nothing to similarity scoring.
        var games = SampleLibrary.Load();
        int total = games.Count;

        var featureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in games)
            foreach (var key in g.FeatureKeys)
            {
                featureCounts.TryGetValue(key, out var cnt);
                featureCounts[key] = cnt + 1;
            }

        var ubiquitousFeatures = featureCounts
            .Where(kvp => kvp.Value == total)
            .Select(kvp => kvp.Key)
            .ToList();

        ubiquitousFeatures.Should().BeEmpty(
            "no feature key should appear on every game — that kills its IDF weight. " +
            $"Offending keys: {string.Join(", ", ubiquitousFeatures)}");
    }

    [Fact]
    public void ToFixture_LikedAndBacklogPartitionsAreCorrect()
    {
        var (liked, backlog) = SampleLibrary.ToFixture(playedFloorMinutes: 120);

        liked.Should().NotBeEmpty("played games with features should appear in liked");
        backlog.Should().NotBeEmpty("backlog candidates with features should be present");

        // All liked items must have non-empty feature vectors
        liked.Should().AllSatisfy(p => p.Features.IsEmpty.Should().BeFalse());

        // All backlog items must have non-empty feature vectors
        backlog.Should().AllSatisfy(c => c.Features.IsEmpty.Should().BeFalse());

        // Affinities must be positive for all liked items
        liked.Should().AllSatisfy(p => p.Affinity.Should().BeGreaterThan(0.0));
    }

    [Fact]
    public void ToFixture_UnmatchedGamesAreExcluded()
    {
        var games = SampleLibrary.Load();
        var unmatched = games.Where(g => g.FeatureKeys.Count == 0).Select(g => g.AppId.ToString()).ToHashSet();

        var (liked, backlog) = SampleLibrary.ToFixture(120);

        var allIds = liked.Select(p => p.ItemId).Concat(backlog.Select(c => c.ItemId)).ToHashSet();
        allIds.Should().NotIntersectWith(unmatched,
            "unmatched games (no feature keys) must not appear in the fixture");
    }
}
