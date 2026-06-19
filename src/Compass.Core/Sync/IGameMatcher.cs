namespace Compass.Core.Sync;

public sealed record MatchOutcome(int SteamAppId, long? IgdbId, string Name, string Method, double Confidence);

public interface IGameMatcher
{
    Task<IReadOnlyList<MatchOutcome>> MatchAsync(
        IReadOnlyList<(int appId, string name)> games, CancellationToken ct);
}
