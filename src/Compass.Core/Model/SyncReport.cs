namespace Compass.Core.Model;
public sealed record SyncReport(int Owned, int Matched, int Unmatched, int Enriched, DateTimeOffset At);
