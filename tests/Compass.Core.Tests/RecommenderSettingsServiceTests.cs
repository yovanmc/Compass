using Compass.Core.Config;
using Compass.Core.Sync;
using FluentAssertions;
using Xunit;

public class RecommenderSettingsServiceTests
{
    private sealed class FakeStore : ISettingsStore
    {
        public readonly Dictionary<string,string> D = new();
        public string? Get(string k) => D.TryGetValue(k, out var v) ? v : null;
        public void Set(string k, string v) => D[k] = v;
        public IReadOnlyDictionary<string,string> GetAll() => D;
    }

    private static RecommenderConfig Defaults() => new()
    {
        PlayedFloorMinutes=120, RecencyWeight=0.25, K=5, ScorerMode="NearestNeighbor",
        HybridAlpha=0.5, NegativeWeight=0.5, UseImplicitNegatives=false, Diversity=0.3,
        CategoryWeights = new(){ ["genre"]=1.0, ["theme"]=0.9, ["mode"]=0.6, ["keyword"]=0.5 }
    };

    [Fact]
    public void Load_NoOverrides_ReturnsDefaults()
    {
        var svc = new RecommenderSettingsService(new FakeStore());
        svc.Load(Defaults()).K.Should().Be(5);
    }

    [Fact]
    public void Save_ThenLoad_AppliesOverrides()
    {
        var store = new FakeStore();
        var svc = new RecommenderSettingsService(store);
        var changed = Defaults(); changed.K = 9; changed.NegativeWeight = 1.5;
        changed.CategoryWeights["keyword"] = 0.2; changed.UseImplicitNegatives = true;
        svc.Save(changed);
        var loaded = svc.Load(Defaults());
        loaded.K.Should().Be(9);
        loaded.NegativeWeight.Should().Be(1.5);
        loaded.UseImplicitNegatives.Should().BeTrue();
        loaded.CategoryWeights["keyword"].Should().Be(0.2);
        loaded.CategoryWeights["genre"].Should().Be(1.0); // untouched default preserved
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips_Diversity()
    {
        var store = new FakeStore();
        var svc = new RecommenderSettingsService(store);
        var changed = Defaults(); changed.Diversity = 0.7;
        svc.Save(changed);
        var loaded = svc.Load(Defaults());
        loaded.Diversity.Should().Be(0.7);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips_FeedbackWeight()
    {
        var store = new FakeStore();
        var svc = new RecommenderSettingsService(store);
        var changed = Defaults(); changed.FeedbackWeight = 1.75;
        svc.Save(changed);
        var loaded = svc.Load(Defaults());
        loaded.FeedbackWeight.Should().Be(1.75);
    }
}
