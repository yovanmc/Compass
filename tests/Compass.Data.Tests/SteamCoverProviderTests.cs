using Compass.Data.Covers;
using FluentAssertions;

public class SteamCoverProviderTests : IDisposable
{
    private readonly string _dir;

    public SteamCoverProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"compass-covers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private sealed class FakeDownloader : ICoverDownloader
    {
        public HashSet<string> Available = new();
        public List<string> Calls = new();

        public Task<byte[]?> TryDownloadAsync(string url, CancellationToken ct)
        {
            Calls.Add(url);
            return Task.FromResult(Available.Contains(url) ? new byte[] { 1, 2, 3 } : (byte[]?)null);
        }
    }

    [Fact]
    public async Task PrefersPortrait_ThenCachesAndReuses()
    {
        var dl = new FakeDownloader();
        var provider = new SteamCoverProvider(dl, _dir);
        var portrait = SteamCoverProvider.PortraitUrl(10);
        dl.Available.Add(portrait);

        var path1 = await provider.GetCoverPathAsync(10, default);
        path1.Should().NotBeNull();
        File.Exists(path1!).Should().BeTrue();

        dl.Calls.Clear();
        var path2 = await provider.GetCoverPathAsync(10, default);   // cached → no new download
        path2.Should().Be(path1);
        dl.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task FallsBackToLandscape_WhenPortraitMissing()
    {
        var dl = new FakeDownloader();
        var provider = new SteamCoverProvider(dl, _dir);
        dl.Available.Add(SteamCoverProvider.LandscapeUrl(20));   // only landscape exists

        var path = await provider.GetCoverPathAsync(20, default);
        path.Should().NotBeNull();
        dl.Calls.Should().ContainInOrder(SteamCoverProvider.PortraitUrl(20), SteamCoverProvider.LandscapeUrl(20));
    }

    [Fact]
    public async Task ReturnsNull_WhenNothingAvailable()
        => (await new SteamCoverProvider(new FakeDownloader(), _dir).GetCoverPathAsync(30, default)).Should().BeNull();
}
