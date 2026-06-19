namespace Compass.Core.Sync;

public sealed record OwnedGame(int AppId, string Name, int PlaytimeForeverMinutes, int Playtime2WeeksMinutes);

public interface ISteamClient
{
    Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct);
}
