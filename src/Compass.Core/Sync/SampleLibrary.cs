using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Compass.Core.Taste;
using Compass.Recommender;

namespace Compass.Core.Sync;

/// <summary>
/// Represents one game entry in the baked-in sample library.
/// </summary>
public sealed record SampleGame(
    int AppId,
    string Name,
    int PlaytimeForeverMin,
    int Playtime2WeeksMin,
    string? IgdbName,
    IReadOnlyList<string> FeatureKeys);

/// <summary>
/// Loads the baked-in ~40-game sample library from the embedded JSON resource.
/// </summary>
public static class SampleLibrary
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Ensure nullable string properties deserialize correctly
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Deserializes the embedded sample-library.json and returns all sample games.
    /// </summary>
    public static IReadOnlyList<SampleGame> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        // The resource name follows the default namespace + folder + file naming.
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("sample-library.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Embedded resource 'sample-library.json' not found. " +
                "Ensure it is included as <EmbeddedResource> in Compass.Core.csproj.");

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");

        var games = JsonSerializer.Deserialize<SampleGame[]>(stream, _jsonOptions)
            ?? throw new InvalidOperationException("Deserialized sample library was null.");

        return games;
    }

    /// <summary>
    /// Converts the sample library into a (liked, backlog) fixture suitable for
    /// RecommenderEvaluator. Uses the same AffinityCalculator logic as RecommendationService.
    /// <para>
    /// Played (playtimeForeverMin &gt;= floor, non-empty features) → ProfileItem with
    /// affinity computed by AffinityCalculator(floor, recencyWeight=0).
    /// </para>
    /// <para>
    /// Backlog (playtimeForeverMin &lt; floor, non-empty features) → CandidateItem.
    /// </para>
    /// <para>
    /// Unmatched (empty featureKeys) → excluded from both lists.
    /// </para>
    /// </summary>
    public static (IReadOnlyList<ProfileItem> Liked, IReadOnlyList<CandidateItem> Backlog)
        ToFixture(int playedFloorMinutes)
    {
        // recencyWeight=0: ignore two-week recency signal so fixture affinity is
        // determined purely by total playtime — mirrors the default config used in tests.
        var affinity = new AffinityCalculator(playedFloorMinutes, recencyWeight: 0.0);

        var games = Load();
        var liked = new List<ProfileItem>();
        var backlog = new List<CandidateItem>();

        foreach (var g in games)
        {
            // Games with no features are unmatched; skip — they can't contribute
            // to the recommender profile and would make zero-vector candidates.
            if (g.FeatureKeys.Count == 0)
                continue;

            var vec = FeatureVector.FromKeys(g.FeatureKeys);
            var id = g.AppId.ToString();

            if (affinity.IsPlayed(g.PlaytimeForeverMin))
            {
                var aff = affinity.Affinity(g.PlaytimeForeverMin, g.Playtime2WeeksMin);
                liked.Add(new ProfileItem(id, vec, aff));
            }
            else
            {
                backlog.Add(new CandidateItem(id, vec));
            }
        }

        return (liked, backlog);
    }
}
