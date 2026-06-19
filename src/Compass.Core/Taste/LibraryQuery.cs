using Compass.Core.Model;

namespace Compass.Core.Taste;

public enum LibraryStatus { All, Played, Backlog, Unmatched, Hidden }
public enum LibrarySort { Score, Playtime, Name, RecentlyPlayed }

public sealed class LibraryFilter
{
    public string? Search { get; init; }
    public LibraryStatus Status { get; init; } = LibraryStatus.All;
    public string? Facet { get; init; }           // a feature key, e.g. "genre:strategy"
    public LibrarySort Sort { get; init; } = LibrarySort.Name;
    public int PlayedFloorMinutes { get; init; } = 120;
}

public static class LibraryQuery
{
    public static IReadOnlyList<Game> Apply(
        IReadOnlyList<Game> library, LibraryFilter f, IReadOnlyDictionary<int, double> scoreByAppId)
    {
        IEnumerable<Game> q = library;

        q = f.Status switch
        {
            LibraryStatus.Hidden    => q.Where(g => g.NotInterested),
            LibraryStatus.Unmatched => q.Where(g => !g.NotInterested && g.IgdbId is null),
            LibraryStatus.Played    => q.Where(g => !g.NotInterested && g.PlaytimeForeverMinutes >= f.PlayedFloorMinutes),
            LibraryStatus.Backlog   => q.Where(g => !g.NotInterested && g.IgdbId is not null && g.PlaytimeForeverMinutes < f.PlayedFloorMinutes),
            _                       => q.Where(g => !g.NotInterested),   // All excludes hidden unless explicitly Hidden
        };

        if (!string.IsNullOrWhiteSpace(f.Search))
            q = q.Where(g => g.Name.Contains(f.Search!, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(f.Facet))
            q = q.Where(g => g.FeatureKeys.Contains(f.Facet!));

        double Score(Game g) => scoreByAppId.TryGetValue(g.SteamAppId, out var s) ? s : 0;

        q = f.Sort switch
        {
            LibrarySort.Score          => q.OrderByDescending(Score).ThenBy(g => g.Name),
            LibrarySort.Playtime       => q.OrderByDescending(g => g.PlaytimeForeverMinutes).ThenBy(g => g.Name),
            LibrarySort.RecentlyPlayed => q.OrderByDescending(g => g.Playtime2WeeksMinutes).ThenBy(g => g.Name),
            _                          => q.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase),
        };

        return q.ToList();
    }
}
