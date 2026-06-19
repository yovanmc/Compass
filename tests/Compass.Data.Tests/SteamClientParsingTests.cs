using Compass.Data.Steam;
using FluentAssertions;
using Xunit;

public class SteamClientParsingTests
{
    private const string Fixture = """
    { "response": { "game_count": 2, "games": [
        { "appid": 220, "name": "Half-Life 2", "playtime_forever": 900, "playtime_2weeks": 30 },
        { "appid": 400, "name": "Portal", "playtime_forever": 0 }
    ]}}
    """;

    [Fact]
    public void Parse_MapsFieldsAndDefaultsTwoWeeks()
    {
        var games = SteamClient.ParseOwnedGames(Fixture);
        games.Should().HaveCount(2);
        var hl2 = games.Single(g => g.AppId == 220);
        hl2.Name.Should().Be("Half-Life 2");
        hl2.PlaytimeForeverMinutes.Should().Be(900);
        hl2.Playtime2WeeksMinutes.Should().Be(30);
        games.Single(g => g.AppId == 400).Playtime2WeeksMinutes.Should().Be(0);
    }

    [Fact]
    public void Parse_MissingGamesKey_ReturnsEmpty()
    {
        var json = """{ "response": {} }""";
        SteamClient.ParseOwnedGames(json).Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingResponseKey_ReturnsEmpty()
    {
        var json = """{}""";
        SteamClient.ParseOwnedGames(json).Should().BeEmpty();
    }
}
