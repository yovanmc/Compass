// This file is kept for the shared row types used by XAML DataTemplates.
// The logic previously in MainViewModel has been split into ShellViewModel + RecommendViewModel.
using Compass.Core.Model;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

public sealed record RecommendationRow(string Name, int ScorePercent, string Why, string FeatureLine)
{
    public static RecommendationRow From(GameRecommendation r)
    {
        var feats = string.Join(" · ", r.WhyFeatures);
        var likes = r.WhyLikedNames.Count > 0
            ? $"like {string.Join(", ", r.WhyLikedNames)}"
            : "Similar to your taste";
        var why = r.WhyLikedNames.Count > 0 && feats.Length > 0
            ? $"{feats} — {likes}"
            : feats.Length > 0 ? feats : likes;
        return new RecommendationRow(
            r.Game.Name,
            (int)Math.Round(Math.Clamp(r.Score, 0, 1) * 100),
            why,
            feats.Length > 0 ? feats : "");
    }
}

/// <summary>A row in the Library page (compact-row and poster-grid views).</summary>
public sealed record GameRow(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    double Score,
    string Status,
    string? CoverPath)
{
    /// <summary>0-100 integer for binding to ScoreRing.Percent.</summary>
    public int ScorePercent => (int)Math.Round(Math.Clamp(Score, 0, 1) * 100);

    /// <summary>Friendly playtime string, e.g. "12h 30m" or "45m".</summary>
    public string PlaytimeDisplay
    {
        get
        {
            int h = PlaytimeForeverMinutes / 60;
            int m = PlaytimeForeverMinutes % 60;
            if (h > 0 && m > 0) return $"{h}h {m}m";
            if (h > 0)          return $"{h}h";
            return $"{m}m";
        }
    }

    // ── Factories ──────────────────────────────────────────────────────────

    /// <summary>Full factory used by LibraryViewModel.</summary>
    public static GameRow From(Game g, double score, int playedFloorMinutes)
    {
        string status = g.NotInterested
            ? "Hidden"
            : g.IgdbId is null
                ? "Unmatched"
                : g.PlaytimeForeverMinutes >= playedFloorMinutes
                    ? "Played"
                    : "Backlog";

        return new GameRow(
            AppId: g.SteamAppId,
            Name: g.Name,
            PlaytimeForeverMinutes: g.PlaytimeForeverMinutes,
            Score: score,
            Status: status,
            CoverPath: null);
    }

    /// <summary>
    /// Backward-compatible factory used by RecommendViewModel's Unmatched list
    /// (only <see cref="Name"/> is bound in RecommendView.xaml).
    /// </summary>
    public static GameRow From(Game g) => From(g, score: 0, playedFloorMinutes: 120);
}

/// <summary>An entry in the Library page's genre/theme facet combo.</summary>
public sealed record FacetOption(string Label, string? Key);
