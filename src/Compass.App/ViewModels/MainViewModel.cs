// This file is kept for the shared row types used by XAML DataTemplates.
// The logic previously in MainViewModel has been split into ShellViewModel + RecommendViewModel.
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

public sealed record GameRow(string Name)
{
    public static GameRow From(Compass.Core.Model.Game g) => new(g.Name);
}
