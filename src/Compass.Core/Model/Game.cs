namespace Compass.Core.Model;

public sealed class Game
{
    public required int SteamAppId { get; init; }
    public required string Name { get; init; }
    public int PlaytimeForeverMinutes { get; init; }
    public int Playtime2WeeksMinutes { get; init; }
    public long? IgdbId { get; init; }
    public MatchMethod MatchMethod { get; init; } = MatchMethod.None;
    public double MatchConfidence { get; init; }
    public IReadOnlyList<string> FeatureKeys { get; init; } = Array.Empty<string>();
    public bool NotInterested { get; init; }
}

public enum MatchMethod { None, AppId, Name }
