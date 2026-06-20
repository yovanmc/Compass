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

    // Insights is computed LAZILY, off the UI thread, only when the page is shown.
    // ComputeHealth runs a leave-one-out evaluation that is cheap on a tiny sample
    // library but O(played² × candidates) on a real one — minutes on ~1000+ games.
    // It must NEVER run during construction (that would block app startup) or on the
    // UI thread (that would freeze the window). _dirty drives recompute-on-next-show.
    private bool _dirty = true;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private bool isLoading = true;

    /// Three mutually-exclusive view states: loading spinner, empty onboarding, or content.
    public bool ShowEmpty   => IsEmpty && !IsLoading;
    public bool ShowContent => !IsEmpty && !IsLoading && Taste is not null;

    partial void OnIsEmptyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowContent));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowContent));
    }

    [ObservableProperty]
    private TasteProfile? taste;

    partial void OnTasteChanged(TasteProfile? value) => OnPropertyChanged(nameof(ShowContent));

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
        // No eager compute: the page loads on demand via EnsureLoadedAsync() when shown.
    }

    /// <summary>Marks the cached insights stale; the next time the page is shown it recomputes.</summary>
    public void RefreshFromStore() => _dirty = true;

    /// <summary>
    /// Recomputes taste + health on a background thread if the data is stale, surfacing an
    /// IsLoading state meanwhile. Idempotent — no-ops when already current. The view calls this
    /// when the Insights page becomes visible.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (!_dirty) return;
        _dirty = false;

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        // LoadLibrary is a cheap SQLite read (kept on the UI thread, as the other page VMs do).
        var lib = _store.LoadLibrary();
        if (lib.Count == 0)
        {
            Taste = null;
            Health = null;
            GenreBars = [];
            ThemeBars = [];
            PlaytimeBars = [];
            IsEmpty = true;
            IsLoading = false;
            return;
        }

        IsEmpty = false;
        IsLoading = true;
        var cfg = _state.Current;

        try
        {
            // The expensive part (AnalyzeTaste + ComputeHealth) runs off the UI thread.
            var (taste, health) = await Task.Run(
                () => (_insights.AnalyzeTaste(lib, cfg), _insights.ComputeHealth(lib, cfg)),
                cts.Token);

            if (cts.IsCancellationRequested) return;   // a newer load superseded this one

            Taste = taste;
            Health = health;
            GenreBars = BuildWeightBars(taste.TopGenres);
            ThemeBars = BuildWeightBars(taste.TopThemes);
            PlaytimeBars = BuildCountBars(taste.PlaytimeDistribution);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer EnsureLoadedAsync(); leave state to that newer load
        }
        finally
        {
            if (_loadCts == cts) IsLoading = false;
        }
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
