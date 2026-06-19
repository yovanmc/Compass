// This file is kept for the shared row types used by XAML DataTemplates.
// The logic previously in MainViewModel has been split into ShellViewModel + RecommendViewModel.
using CommunityToolkit.Mvvm.ComponentModel;
using Compass.Core.Model;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

/// <summary>A recommendation card in the Recommend page.</summary>
public sealed partial class RecommendationRow : ObservableObject
{
    public int AppId { get; }
    public string Name { get; }
    public int ScorePercent { get; }
    public string Why { get; }
    public string FeatureLine { get; }

    public RecommendationRow(int appId, string name, int scorePercent, string why, string featureLine)
    {
        AppId       = appId;
        Name        = name;
        ScorePercent = scorePercent;
        Why         = why;
        FeatureLine = featureLine;
    }

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
            r.Game.SteamAppId,
            r.Game.Name,
            (int)Math.Round(Math.Clamp(r.Score, 0, 1) * 100),
            why,
            feats.Length > 0 ? feats : "");
    }
}

/// <summary>A row in the Library page (compact-row and poster-grid views).</summary>
public sealed partial class GameRow : ObservableObject
{
    public int AppId { get; }
    public string Name { get; }
    public int PlaytimeForeverMinutes { get; }
    public double Score { get; }
    public string Status { get; }

    [ObservableProperty]
    private string? coverPath;

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

    public GameRow(int appId, string name, int playtimeForeverMinutes, double score, string status, string? coverPathValue = null)
    {
        AppId = appId;
        Name = name;
        PlaytimeForeverMinutes = playtimeForeverMinutes;
        Score = score;
        Status = status;
        coverPath = coverPathValue;
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
            appId: g.SteamAppId,
            name: g.Name,
            playtimeForeverMinutes: g.PlaytimeForeverMinutes,
            score: score,
            status: status,
            coverPathValue: null);
    }

    /// <summary>
    /// Backward-compatible factory used by RecommendViewModel's Unmatched list
    /// (only <see cref="Name"/> is bound in RecommendView.xaml).
    /// </summary>
    public static GameRow From(Game g) => From(g, score: 0, playedFloorMinutes: 120);
}

/// <summary>An entry in the Library page's genre/theme facet combo.</summary>
public sealed record FacetOption(string Label, string? Key);
