namespace Compass.Core.Covers;

public interface ICoverProvider
{
    /// Returns a local file path to a cover image, or null if none could be obtained.
    Task<string?> GetCoverPathAsync(int appId, CancellationToken ct);
}
