using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Compass.Core.Sync;
using Compass.Core.Taste;

namespace Compass.App.ViewModels;

/// <summary>A single bar-chart row: label, normalised fraction [0..1], and display value text.</summary>
public sealed record BarRow(string Label, double Fraction, string Value);

public sealed partial class InsightsViewModel : ObservableObject
{
    private readonly ISyncStore _store;
    private readonly InsightsService _insights;
    private readonly RecommenderConfigState _state;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private TasteProfile? taste;

    [ObservableProperty]
    private RecommenderHealth? health;

    [ObservableProperty]
    private IReadOnlyList<BarRow> genreBars = [];

    [ObservableProperty]
    private IReadOnlyList<BarRow> themeBars = [];

    [ObservableProperty]
    private IReadOnlyList<BarRow> playtimeBars = [];

    public InsightsViewModel(ISyncStore store, InsightsService insights, RecommenderConfigState state)
    {
        _store = store;
        _insights = insights;
        _state = state;
        Recompute();
    }

    public void RefreshFromStore() => Recompute();

    private void Recompute()
    {
        var lib = _store.LoadLibrary();
        IsEmpty = lib.Count == 0;

        if (IsEmpty)
        {
            Taste = null;
            Health = null;
            GenreBars = [];
            ThemeBars = [];
            PlaytimeBars = [];
            return;
        }

        Taste = _insights.AnalyzeTaste(lib, _state.Current);
        Health = _insights.ComputeHealth(lib, _state.Current);

        GenreBars = BuildWeightBars(Taste.TopGenres);
        ThemeBars = BuildWeightBars(Taste.TopThemes);
        PlaytimeBars = BuildCountBars(Taste.PlaytimeDistribution);
    }

    private static IReadOnlyList<BarRow> BuildWeightBars(IReadOnlyList<FeatureWeight> weights)
    {
        if (weights.Count == 0) return [];
        double max = weights.Max(w => w.Weight);
        return weights.Select(w => new BarRow(
            w.Name,
            max > 0 ? w.Weight / max : 0,
            w.Weight.ToString("0.0", CultureInfo.InvariantCulture)
        )).ToList();
    }

    private static IReadOnlyList<BarRow> BuildCountBars(IReadOnlyList<DistributionBucket> buckets)
    {
        if (buckets.Count == 0) return [];
        int max = buckets.Max(b => b.Count);
        return buckets.Select(b => new BarRow(
            b.Label,
            max > 0 ? (double)b.Count / max : 0,
            b.Count.ToString()
        )).ToList();
    }
}
