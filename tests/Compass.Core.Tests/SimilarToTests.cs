using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

/// <summary>
/// Tests for RecommendationService.SimilarTo: per-game nearest-neighbour lookup.
/// </summary>
public class SimilarToTests
{
    // Helpers — IgdbId non-null so the game is matched and gets a vector.
    private static Game G(int id, string name, int mins, bool notInterested, params string[] f) =>
        new() { SteamAppId = id, Name = name, PlaytimeForeverMinutes = mins, FeatureKeys = f, IgdbId = id, NotInterested = notInterested };

    private static Game Played(int id, string name, params string[] f) => G(id, name, 6000, false, f);
    private static Game Backlog(int id, string name, params string[] f) => G(id, name, 0, false, f);
    private static Game Hidden(int id, string name, params string[] f) => G(id, name, 0, true, f);

    private static readonly RecommendationService Svc = new();

    [Fact]
    public void SimilarTo_ReturnsMostSimilarFirst_ExcludesSeed()
    {
        var library = new[]
        {
            Played(1, "Seed RPG",        "genre:rpg", "theme:fantasy"),
            Backlog(2, "Similar RPG",    "genre:rpg", "theme:fantasy"),
            Backlog(3, "Partial RPG",    "genre:rpg"),
            Backlog(4, "Unrelated",      "genre:cozy"),
        };

        var results = Svc.SimilarTo(library, seedAppId: 1, k: 3);

        // Seed must not appear in results
        results.Select(r => r.Game.SteamAppId).Should().NotContain(1);

        // Most-similar (full feature overlap) should rank first
        results.First().Game.SteamAppId.Should().Be(2);

        // Scores must be descending
        results.Select(r => r.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public void SimilarTo_ExcludesNotInterested()
    {
        // Use enough candidates so genre:rpg doesn't get IDF=0 (ubiquitous suppression).
        // The hidden game should never appear; visible candidates that share features should score > 0.
        var library = new[]
        {
            Played(1, "Seed",            "genre:rpg", "theme:fantasy"),
            Hidden(2, "Hidden Twin",     "genre:rpg", "theme:fantasy"),  // must be excluded
            Backlog(3, "Visible RPG",    "genre:rpg", "theme:fantasy"),
            Backlog(4, "Other Genre",    "genre:strategy"),               // no overlap → keeps IDF non-zero for rpg
            Backlog(5, "Another Genre",  "genre:cozy"),
        };

        var results = Svc.SimilarTo(library, seedAppId: 1, k: 5);

        // Hidden game must never appear
        results.Select(r => r.Game.SteamAppId).Should().NotContain(2);

        // Seed must not appear
        results.Select(r => r.Game.SteamAppId).Should().NotContain(1);

        // Game 3 shares both features with seed; it should rank in results
        results.Should().Contain(r => r.Game.SteamAppId == 3);
    }

    [Fact]
    public void SimilarTo_SeedWithEmptyFeatures_ReturnsEmpty()
    {
        var library = new[]
        {
            // Seed has no feature keys → empty vector
            new Game { SteamAppId = 1, Name = "No Meta", PlaytimeForeverMinutes = 6000, FeatureKeys = Array.Empty<string>(), IgdbId = 1 },
            Backlog(2, "Some Game", "genre:rpg"),
        };

        var results = Svc.SimilarTo(library, seedAppId: 1, k: 5);

        results.Should().BeEmpty();
    }

    [Fact]
    public void SimilarTo_RespectsK()
    {
        // Include a non-rpg game so genre:rpg isn't ubiquitous (IDF > 0).
        var library = new[]
        {
            Played(1, "Seed",     "genre:rpg", "theme:fantasy"),
            Backlog(2, "G2",      "genre:rpg", "theme:fantasy"),
            Backlog(3, "G3",      "genre:rpg"),
            Backlog(4, "G4",      "genre:rpg"),
            Backlog(5, "Other",   "genre:cozy"),  // breaks ubiquity
        };

        var results = Svc.SimilarTo(library, seedAppId: 1, k: 2);

        results.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void SimilarTo_UnknownSeed_ReturnsEmpty()
    {
        var library = new[]
        {
            Played(1, "Game", "genre:rpg"),
        };

        var results = Svc.SimilarTo(library, seedAppId: 999, k: 5);

        results.Should().BeEmpty();
    }
}
