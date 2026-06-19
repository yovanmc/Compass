namespace Compass.Core.Sync;

public sealed record IgdbMatch(int SteamAppId, long IgdbId, string Name);
public sealed record IgdbFeature(string Category, long Id, string Name); // category: genre|theme|mode|keyword
public sealed record IgdbGameMetadata(long IgdbId, string Name, IReadOnlyList<IgdbFeature> Features);

public interface IIgdbClient
{
    /// Tier-1 match: Steam appIDs → IGDB game ids via external_games.
    Task<IReadOnlyList<IgdbMatch>> MatchBySteamAppIdsAsync(IReadOnlyList<int> appIds, CancellationToken ct);

    /// Tier-2 fallback: search IGDB games by name (returns candidates to score).
    Task<IReadOnlyList<(long igdbId, string name)>> SearchByNameAsync(string name, CancellationToken ct);

    /// Enrichment: full metadata (genres/themes/modes/keywords) for igdb ids.
    Task<IReadOnlyList<IgdbGameMetadata>> GetMetadataAsync(IReadOnlyList<long> igdbIds, CancellationToken ct);
}
