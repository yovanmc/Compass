using Compass.Core.Model;
using Compass.Core.Taste;
using FluentAssertions;
using Xunit;

public class LibraryQueryTests
{
    private static Game G(int id, string name, int forever, bool hidden=false, long? igdb=1, params string[] f) =>
        new() { SteamAppId=id, Name=name, PlaytimeForeverMinutes=forever, IgdbId=igdb, NotInterested=hidden, FeatureKeys=f };

    private static IReadOnlyList<Game> Lib() => new[]
    {
        G(1,"Alpha",6000, f:"genre:strategy"),         // played
        G(2,"Bravo",0,    f:"genre:cozy"),             // backlog
        G(3,"Charlie",0,  igdb:null),                  // unmatched (no igdb)
        G(4,"Delta",0,    hidden:true, f:"genre:rpg"), // hidden
    };

    [Fact]
    public void Search_MatchesNameSubstring_CaseInsensitive()
        => LibraryQuery.Apply(Lib(), new LibraryFilter { Search="alp" }, new Dictionary<int,double>())
            .Select(g=>g.Name).Should().BeEquivalentTo(new[]{"Alpha"});

    [Fact]
    public void Status_BacklogExcludesPlayedHiddenUnmatched()
        => LibraryQuery.Apply(Lib(), new LibraryFilter { Status=LibraryStatus.Backlog, PlayedFloorMinutes=120 }, new Dictionary<int,double>())
            .Select(g=>g.Name).Should().BeEquivalentTo(new[]{"Bravo"});

    [Fact]
    public void Status_Hidden_ReturnsOnlyHidden()
        => LibraryQuery.Apply(Lib(), new LibraryFilter { Status=LibraryStatus.Hidden }, new Dictionary<int,double>())
            .Select(g=>g.Name).Should().BeEquivalentTo(new[]{"Delta"});

    [Fact]
    public void Facet_FiltersByFeatureKey()
        => LibraryQuery.Apply(Lib(), new LibraryFilter { Facet="genre:cozy" }, new Dictionary<int,double>())
            .Select(g=>g.Name).Should().BeEquivalentTo(new[]{"Bravo"});

    [Fact]
    public void Sort_ByScoreDescending()
    {
        var scores = new Dictionary<int,double>{ [2]=0.9, [3]=0.1 };
        LibraryQuery.Apply(Lib(), new LibraryFilter { Sort=LibrarySort.Score }, scores)
            .Select(g=>g.SteamAppId).First().Should().Be(2);
    }

    [Fact]
    public void Sort_ByPlaytimeDescending_ThenName()
        => LibraryQuery.Apply(Lib(), new LibraryFilter { Sort=LibrarySort.Playtime }, new Dictionary<int,double>())
            .First().Name.Should().Be("Alpha");
}
