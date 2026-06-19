using Compass.Core.Config;
using FluentAssertions;
using Xunit;

public class SecretsGuardTests
{
    private static CompassOptions Valid() => new()
    {
        Steam = new() { SteamId64 = "765...", ApiKey = "real" },
        Igdb = new() { ClientId = "id", ClientSecret = "secret" }
    };

    [Fact]
    public void AllPresent_ReturnsNoMissing()
        => SecretsGuard.FindMissing(Valid()).Should().BeEmpty();

    [Fact]
    public void PlaceholderApiKey_IsReportedMissing()
    {
        var o = Valid(); o.Steam.ApiKey = "REPLACE_VIA_USER_SECRETS";
        SecretsGuard.FindMissing(o).Should().Contain("Steam:ApiKey");
    }

    [Fact]
    public void EmptyIgdbClientSecret_IsReportedMissing()
    {
        var o = Valid(); o.Igdb.ClientSecret = "";
        SecretsGuard.FindMissing(o).Should().Contain("Igdb:ClientSecret");
    }
}
