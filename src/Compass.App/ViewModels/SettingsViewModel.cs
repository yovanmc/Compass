using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;
using Compass.Core.Sync;

namespace Compass.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly RecommenderSettingsService _settingsSvc;
    private readonly RecommenderConfigState _state;
    private readonly CompassOptions _opts;
    private readonly ISyncStore _store;

    // Guard: prevents OnXxxChanged hooks from firing Save/re-rank during ctor population.
    private bool _loading = true;

    // ── Scorer mode list (static) ─────────────────────────────────────────
    public IReadOnlyList<string> ScorerModes { get; } =
        new[] { "NearestNeighbor", "Centroid", "Hybrid" };

    // ── Bound knobs ───────────────────────────────────────────────────────
    [ObservableProperty]
    private int playedFloorMinutes;

    [ObservableProperty]
    private int k;

    [ObservableProperty]
    private string selectedScorerMode = "NearestNeighbor";

    [ObservableProperty]
    private double hybridAlpha;

    [ObservableProperty]
    private double diversity;

    [ObservableProperty]
    private double weightGenre;

    [ObservableProperty]
    private double weightTheme;

    [ObservableProperty]
    private double weightMode;

    [ObservableProperty]
    private double weightKeyword;

    [ObservableProperty]
    private double negativeWeight;

    [ObservableProperty]
    private bool useImplicitNegatives;

    // Derived — raised explicitly in OnSelectedScorerModeChanged.
    public bool IsHybrid => SelectedScorerMode == "Hybrid";

    // ── Confirm delegate (wired by code-behind to MessageBox) ─────────────
    /// <summary>
    /// Called before any destructive data operation. Return true to proceed.
    /// If null, the operation is aborted (fail-safe: no accidental data loss).
    /// </summary>
    public Func<string, bool>? Confirm { get; set; }

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>Raised after every knob change and after Reset. Shell subscribes to re-rank Recommend + Library.</summary>
    public event Action? ConfigChanged;

    /// <summary>Raised after LoadSampleData or ClearLibrary completes. Shell subscribes to refresh all pages.</summary>
    public event Action? LibraryReplaced;

    // ── Constructor ───────────────────────────────────────────────────────
    public SettingsViewModel(
        RecommenderSettingsService settingsSvc,
        RecommenderConfigState state,
        CompassOptions opts,
        ISyncStore store)
    {
        _settingsSvc = settingsSvc;
        _state       = state;
        _opts        = opts;
        _store       = store;

        // Load layered effective config (SQLite over appsettings defaults).
        var cfg = _settingsSvc.Load(opts.Recommender);
        PopulateFromConfig(cfg);

        _loading = false;
    }

    // ── Property-changed hooks (generated code calls these) ───────────────
    partial void OnPlayedFloorMinutesChanged(int value)   => ApplyAndPersist();
    partial void OnKChanged(int value)                     => ApplyAndPersist();
    partial void OnHybridAlphaChanged(double value)        => ApplyAndPersist();
    partial void OnDiversityChanged(double value)          => ApplyAndPersist();
    partial void OnWeightGenreChanged(double value)        => ApplyAndPersist();
    partial void OnWeightThemeChanged(double value)        => ApplyAndPersist();
    partial void OnWeightModeChanged(double value)         => ApplyAndPersist();
    partial void OnWeightKeywordChanged(double value)      => ApplyAndPersist();
    partial void OnNegativeWeightChanged(double value)     => ApplyAndPersist();
    partial void OnUseImplicitNegativesChanged(bool value) => ApplyAndPersist();

    partial void OnSelectedScorerModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsHybrid));
        ApplyAndPersist();
    }

    // ── Reset command ─────────────────────────────────────────────────────
    [RelayCommand]
    private void Reset()
    {
        _loading = true;
        PopulateFromConfig(_opts.Recommender);
        _loading = false;

        // Single apply after bulk-reset — avoids 10 intermediate saves.
        _settingsSvc.Save(_opts.Recommender);
        _state.Current = BuildConfig();
        ConfigChanged?.Invoke();
    }

    // ── Data commands ─────────────────────────────────────────────────────
    [RelayCommand]
    private void LoadSampleData()
    {
        bool hasData = _store.LoadLibrary().Count > 0;
        if (hasData)
        {
            string msg = "This will replace your current library with the built-in sample data. Your settings will be kept. Proceed?";
            if (!(Confirm?.Invoke(msg) ?? false))
                return;
        }

        // Replace, don't merge: clear first so the sample becomes the entire
        // library — matches the confirm text and the "overwrite a non-empty
        // library" design decision. The provider upserts (it never clears), so
        // without this a real synced library would be merged with, not replaced.
        // Clearing is a harmless no-op when the library is already empty.
        _store.ClearLibrary();
        _store.LoadSampleData(SampleLibrary.Load());
        LibraryReplaced?.Invoke();
    }

    [RelayCommand]
    private void ClearLibrary()
    {
        bool hasData = _store.LoadLibrary().Count > 0;
        if (!hasData)
            return; // nothing to clear

        string msg = "This will remove all library data (games, metadata, features). Your settings will be kept. Proceed?";
        if (!(Confirm?.Invoke(msg) ?? false))
            return;

        _store.ClearLibrary();
        LibraryReplaced?.Invoke();
    }

    // ── Private helpers ───────────────────────────────────────────────────
    private void PopulateFromConfig(RecommenderConfig cfg)
    {
        PlayedFloorMinutes   = cfg.PlayedFloorMinutes;
        K                    = cfg.K;
        SelectedScorerMode   = cfg.ScorerMode;
        HybridAlpha          = cfg.HybridAlpha;
        Diversity            = cfg.Diversity;
        NegativeWeight       = cfg.NegativeWeight;
        UseImplicitNegatives = cfg.UseImplicitNegatives;
        WeightGenre    = cfg.CategoryWeights.TryGetValue("genre",   out var g) ? g : 1.0;
        WeightTheme    = cfg.CategoryWeights.TryGetValue("theme",   out var t) ? t : 1.0;
        WeightMode     = cfg.CategoryWeights.TryGetValue("mode",    out var m) ? m : 1.0;
        WeightKeyword  = cfg.CategoryWeights.TryGetValue("keyword", out var k) ? k : 1.0;
    }

    private RecommenderConfig BuildConfig() => new()
    {
        PlayedFloorMinutes   = PlayedFloorMinutes,
        RecencyWeight        = _opts.Recommender.RecencyWeight,   // not a UI knob
        K                    = K,
        ScorerMode           = SelectedScorerMode,
        HybridAlpha          = HybridAlpha,
        Diversity            = Diversity,
        NegativeWeight       = NegativeWeight,
        UseImplicitNegatives = UseImplicitNegatives,
        CategoryWeights      = new Dictionary<string, double>
        {
            ["genre"]   = WeightGenre,
            ["theme"]   = WeightTheme,
            ["mode"]    = WeightMode,
            ["keyword"] = WeightKeyword,
        },
    };

    private void ApplyAndPersist()
    {
        if (_loading) return;

        var config = BuildConfig();
        _settingsSvc.Save(config);
        _state.Current = config;
        ConfigChanged?.Invoke();
    }
}
