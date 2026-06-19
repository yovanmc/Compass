using System.Globalization;
using Compass.Core.Sync;

namespace Compass.Core.Config;

public sealed class RecommenderSettingsService
{
    private const string P = "Recommender:";
    private readonly ISettingsStore _store;
    public RecommenderSettingsService(ISettingsStore store) => _store = store;

    public RecommenderConfig Load(RecommenderConfig defaults)
    {
        var all = _store.GetAll();
        double D(string k, double dflt) => all.TryGetValue(P+k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : dflt;
        int I(string k, int dflt) => all.TryGetValue(P+k, out var v) && int.TryParse(v, out var i) ? i : dflt;
        bool B(string k, bool dflt) => all.TryGetValue(P+k, out var v) && bool.TryParse(v, out var b) ? b : dflt;
        string S(string k, string dflt) => all.TryGetValue(P+k, out var v) ? v : dflt;

        var weights = new Dictionary<string,double>(defaults.CategoryWeights);
        foreach (var cat in new[]{"genre","theme","mode","keyword"})
            weights[cat] = D($"Weight:{cat}", defaults.CategoryWeights.TryGetValue(cat, out var w) ? w : 1.0);

        return new RecommenderConfig
        {
            PlayedFloorMinutes = I("PlayedFloorMinutes", defaults.PlayedFloorMinutes),
            RecencyWeight = defaults.RecencyWeight,
            K = I("K", defaults.K),
            ScorerMode = S("ScorerMode", defaults.ScorerMode),
            HybridAlpha = D("HybridAlpha", defaults.HybridAlpha),
            NegativeWeight = D("NegativeWeight", defaults.NegativeWeight),
            UseImplicitNegatives = B("UseImplicitNegatives", defaults.UseImplicitNegatives),
            CategoryWeights = weights,
        };
    }

    public void Save(RecommenderConfig c)
    {
        var inv = CultureInfo.InvariantCulture;
        _store.Set(P+"PlayedFloorMinutes", c.PlayedFloorMinutes.ToString(inv));
        _store.Set(P+"K", c.K.ToString(inv));
        _store.Set(P+"ScorerMode", c.ScorerMode);
        _store.Set(P+"HybridAlpha", c.HybridAlpha.ToString(inv));
        _store.Set(P+"NegativeWeight", c.NegativeWeight.ToString(inv));
        _store.Set(P+"UseImplicitNegatives", c.UseImplicitNegatives.ToString());
        foreach (var cat in new[]{"genre","theme","mode","keyword"})
            _store.Set(P+$"Weight:{cat}", (c.CategoryWeights.TryGetValue(cat, out var w) ? w : 1.0).ToString(inv));
    }
}
