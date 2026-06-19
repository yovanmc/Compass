using Compass.Core.Config;
using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

public class FeedbackWiringTests
{
    private static Game G(int id, string name, int playtime, IReadOnlyList<string> feats,
        int feedback = 0, bool hide = false, long? igdb = 1) => new()
    {
        SteamAppId = id, Name = name, PlaytimeForeverMinutes = playtime,
        IgdbId = feats.Count > 0 ? igdb : null, FeatureKeys = feats,
        Feedback = feedback, NotInterested = hide,
        MatchMethod = feats.Count > 0 ? MatchMethod.AppId : MatchMethod.None,
    };

    private static RecommenderConfig Cfg(double feedbackWeight = 1.0, double negativeWeight = 0.5) => new()
    { PlayedFloorMinutes = 120, K = 5, FeedbackWeight = feedbackWeight, Diversity = 0.0, NegativeWeight = negativeWeight };

    private static int RankOf(RecommendationResult r, int appId) =>
        r.Recommendations.Select((x, i) => (x, i)).First(t => t.x.Game.SteamAppId == appId).i;

    [Fact]
    public void PlusOne_BacklogGame_RaisesSimilarCandidates_AndStaysACandidate()
    {
        var strat = new[] { "genre:strategy", "theme:sci-fi" };
        var lib = new[]
        {
            G(1, "Played RPG", 600, new[] { "genre:rpg", "theme:fantasy" }),
            G(2, "Strategy A", 0, strat),
            G(3, "Puzzle B", 0, new[] { "genre:puzzle" }),
            G(4, "Strategy Seed", 0, strat, feedback: 1),
        };
        var svc = new RecommendationService();
        var r = svc.Recommend(lib, Cfg());

        r.Recommendations.Should().Contain(x => x.Game.SteamAppId == 4);
        RankOf(r, 2).Should().BeLessThan(RankOf(r, 3));
    }

    [Fact]
    public void MinusOne_BacklogGame_StaysACandidate_ButLowersSimilar()
    {
        // Played anchor is strategy; a puzzle game gives IDF contrast so strategy features score > 0.
        // NegativeWeight > 0 so the disliked signal from -1 feedback penalizes similar candidates.
        var strat = new[] { "genre:strategy", "theme:sci-fi" };
        var lib = new[]
        {
            G(1, "Played Strategy", 600, strat),
            G(2, "Strategy A", 0, strat),
            G(3, "Puzzle Outsider", 0, new[] { "genre:puzzle" }),
            G(4, "Strategy Seed", 0, strat, feedback: -1),
        };
        var svc = new RecommendationService();
        var withFb = svc.Recommend(lib, Cfg());
        var noFb = svc.Recommend(lib.Select(g => g.SteamAppId == 4
            ? G(4, "Strategy Seed", 0, strat) : g).ToArray(), Cfg());

        withFb.Recommendations.Should().Contain(x => x.Game.SteamAppId == 4);
        withFb.Recommendations.First(x => x.Game.SteamAppId == 2).Score
            .Should().BeLessThan(noFb.Recommendations.First(x => x.Game.SteamAppId == 2).Score);
    }

    [Fact]
    public void NotInterested_Wins_OverStaleFeedback()
    {
        var strat = new[] { "genre:strategy" };
        var lib = new[]
        {
            G(1, "Played", 600, strat),
            G(2, "Hidden but +1", 0, strat, feedback: 1, hide: true),
        };
        var svc = new RecommendationService();
        svc.Recommend(lib, Cfg()).Recommendations
            .Should().NotContain(x => x.Game.SteamAppId == 2);
    }
}
