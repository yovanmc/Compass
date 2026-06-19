using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

public class RecommendationServiceTests
{
    private static RecommenderConfig Cfg() => new()
    {
        PlayedFloorMinutes = 120, RecencyWeight = 0.25, K = 5, ScorerMode = "NearestNeighbor",
        CategoryWeights = new() { ["genre"] = 1.0 }
    };

    private static Game Played(int id, string name, int mins, params string[] f) =>
        new() { SteamAppId = id, Name = name, PlaytimeForeverMinutes = mins, FeatureKeys = f, IgdbId = id };

    private static Game Backlog(int id, string name, params string[] f) =>
        new() { SteamAppId = id, Name = name, PlaytimeForeverMinutes = 0, FeatureKeys = f, IgdbId = id };

    [Fact]
    public void RanksBacklogByTaste_WithNames()
    {
        var library = new[]
        {
            Played(1, "Loved Strategy", 6000, "genre:strategy", "theme:sci-fi"),
            Backlog(2, "More Strategy", "genre:strategy", "theme:sci-fi"),
            Backlog(3, "Cozy Thing", "genre:cozy"),
        };
        var svc = new RecommendationService();
        var result = svc.Recommend(library, Cfg());

        result.Recommendations.First().Game.Name.Should().Be("More Strategy");
        result.Recommendations.First().WhyFeatures.Should().Contain(s => s.Contains("strategy"));
        result.Recommendations.First().WhyLikedNames.Should().Contain("Loved Strategy");
    }

    [Fact]
    public void GamesWithoutFeatures_GoToUnscored_NotRecommendations()
    {
        var library = new[]
        {
            Played(1, "Loved", 6000, "genre:strategy"),
            Backlog(2, "No Metadata"),
        };
        var result = new RecommendationService().Recommend(library, Cfg());
        result.Recommendations.Select(r => r.Game.Name).Should().NotContain("No Metadata");
        result.UnscoredBacklog.Select(g => g.Name).Should().Contain("No Metadata");
    }

    [Fact]
    public void PlayedGame_WithNoFeatures_IsExcludedFromBothOutputs()
    {
        var library = new[]
        {
            // Normal played game that informs taste
            Played(1, "Loved Strategy", 6000, "genre:strategy", "theme:sci-fi"),
            // Played game above floor but with no features — must not appear in either output
            Played(2, "Played No Features", 6000 /*, no features */),
            // Backlog candidate with features so the recommender actually runs
            Backlog(3, "Backlog Game", "genre:strategy"),
        };
        var result = new RecommendationService().Recommend(library, Cfg());

        result.Recommendations.Select(r => r.Game.Name).Should().NotContain("Played No Features");
        result.UnscoredBacklog.Select(g => g.Name).Should().NotContain("Played No Features");
    }

    [Fact]
    public void NotInterestedGame_IsExcludedFromCandidates_AndPenalizesSimilar()
    {
        var cfg = Cfg(); cfg.NegativeWeight = 1.0;
        var library = new[]
        {
            Played(1, "Loved Strategy", 6000, "genre:strategy"),
            Backlog(2, "More Strategy", "genre:strategy"),
            new Game { SteamAppId=3, Name="Hated Horror", PlaytimeForeverMinutes=0, FeatureKeys=new[]{"genre:horror"}, IgdbId=3, NotInterested=true },
            Backlog(4, "Scary Strategy", "genre:strategy","genre:horror"),
        };
        var res = new RecommendationService().Recommend(library, cfg);
        res.Recommendations.Select(r => r.Game.Name).Should().NotContain("Hated Horror");   // excluded as candidate
        var more = res.Recommendations.Single(r => r.Game.Name=="More Strategy");
        var scary = res.Recommendations.Single(r => r.Game.Name=="Scary Strategy");
        scary.Score.Should().BeLessThan(more.Score);                                         // penalized
    }

    [Fact]
    public void ImplicitNegatives_OnlyApplyWhenEnabled()
    {
        var library = new[]
        {
            Played(1, "Loved Strategy", 6000, "genre:strategy"),
            // tried-and-dropped horror: 45 min (in [30, floor)) — implicit negative when enabled
            new Game { SteamAppId=3, Name="Dropped Horror", PlaytimeForeverMinutes=45, FeatureKeys=new[]{"genre:horror"}, IgdbId=3 },
            Backlog(4, "Scary Strategy", "genre:strategy","genre:horror"),
        };
        var off = Cfg(); off.NegativeWeight = 1.0; off.UseImplicitNegatives = false;
        var on  = Cfg(); on.NegativeWeight = 1.0; on.UseImplicitNegatives = true;
        var scoreOff = new RecommendationService().Recommend(library, off).Recommendations.Single(r=>r.Game.Name=="Scary Strategy").Score;
        var scoreOn  = new RecommendationService().Recommend(library, on ).Recommendations.Single(r=>r.Game.Name=="Scary Strategy").Score;
        scoreOn.Should().BeLessThan(scoreOff);   // implicit negative drags it down only when enabled
    }
}
