using Compass.Core.Sync;
using Compass.Data.Match;
using FluentAssertions;
using Xunit;

public class GameMatcherTests
{
    private sealed class FakeIgdb : IIgdbClient
    {
        public Dictionary<int, IgdbMatch> AppIdMap = new();
        public Dictionary<string, List<(long, string)>> NameSearch = new();

        public Task<IReadOnlyList<IgdbMatch>> MatchBySteamAppIdsAsync(
            IReadOnlyList<int> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<IgdbMatch>>(
                ids.Where(AppIdMap.ContainsKey).Select(i => AppIdMap[i]).ToList());

        public Task<IReadOnlyList<(long, string)>> SearchByNameAsync(string name, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(long, string)>>(
                NameSearch.TryGetValue(name, out var v) ? v : new List<(long, string)>());

        public Task<IReadOnlyList<IgdbGameMetadata>> GetMetadataAsync(
            IReadOnlyList<long> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<IgdbGameMetadata>>(new List<IgdbGameMetadata>());
    }

    [Fact]
    public async Task Tier1_AppIdMatch_WinsWithConfidence1()
    {
        var fake = new FakeIgdb { AppIdMap = { [10] = new IgdbMatch(10, 555, "Doom") } };
        var matcher = new GameMatcher(fake, nameConfidenceThreshold: 0.85);
        var results = await matcher.MatchAsync(new[] { (10, "Doom") }, default);
        results.Single().IgdbId.Should().Be(555);
        results.Single().Method.Should().Be("appid");
        results.Single().Confidence.Should().Be(1.0);
    }

    [Fact]
    public async Task Tier2_NameMatch_AboveThreshold()
    {
        var fake = new FakeIgdb();
        fake.NameSearch["Stardew Valley"] = new() { (777, "Stardew Valley") };
        var matcher = new GameMatcher(fake, 0.85);
        var r = (await matcher.MatchAsync(new[] { (11, "Stardew Valley") }, default)).Single();
        r.IgdbId.Should().Be(777);
        r.Method.Should().Be("name");
        r.Confidence.Should().BeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public async Task Tier3_NoConfidentMatch_IsUnmatched()
    {
        var fake = new FakeIgdb();
        fake.NameSearch["Obscure Thing"] = new() { (1, "Totally Different Game") };
        var matcher = new GameMatcher(fake, 0.85);
        var r = (await matcher.MatchAsync(new[] { (12, "Obscure Thing") }, default)).Single();
        r.IgdbId.Should().BeNull();
        r.Method.Should().Be("none");
    }

    [Fact]
    public async Task Tier1_DuplicateAppIdRows_DoNotThrow_AndYieldOneMatch()
    {
        // IGDB external_games can return multiple rows for the same appid (e.g. regional entries).
        // GameMatcher must handle this without throwing a duplicate-key exception.
        var fake = new FakeIgdb();
        // Inject two IgdbMatch rows for the same Steam appId 10
        // We override MatchBySteamAppIdsAsync to return duplicates
        var fakeWithDups = new DupFakeIgdb();
        var matcher = new GameMatcher(fakeWithDups, nameConfidenceThreshold: 0.85);
        var results = await matcher.MatchAsync(new[] { (10, "Doom") }, default);
        // Should not throw, should yield exactly one appid match
        results.Should().HaveCount(1);
        results.Single().IgdbId.Should().NotBeNull();
        results.Single().Method.Should().Be("appid");
    }

    /// <summary>
    /// Fake that returns two IgdbMatch rows for the same Steam appId to test dedup.
    /// </summary>
    private sealed class DupFakeIgdb : IIgdbClient
    {
        public Task<IReadOnlyList<IgdbMatch>> MatchBySteamAppIdsAsync(
            IReadOnlyList<int> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<IgdbMatch>>(new List<IgdbMatch>
            {
                new IgdbMatch(10, 555, "Doom"),
                new IgdbMatch(10, 556, "Doom (Regional)"), // duplicate appid, different igdb row
            });

        public Task<IReadOnlyList<(long, string)>> SearchByNameAsync(string name, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(long, string)>>(new List<(long, string)>());

        public Task<IReadOnlyList<IgdbGameMetadata>> GetMetadataAsync(
            IReadOnlyList<long> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<IgdbGameMetadata>>(new List<IgdbGameMetadata>());
    }
}
