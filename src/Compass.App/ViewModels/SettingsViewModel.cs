using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Config;

namespace Compass.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly RecommenderSettingsService _settingsSvc;
    private readonly RecommenderConfigState _state;
    private readonly CompassOptions _opts;

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

    // ── Event for shell re-rank ───────────────────────────────────────────
    /// <summary>Raised after every knob change and after Reset. Shell subscribes to re-rank Recommend + Library.</summary>
    public event Action? ConfigChanged;

    // ── Constructor ───────────────────────────────────────────────────────
    public SettingsViewModel(
        RecommenderSettingsService settingsSvc,
        RecommenderConfigState state,
        CompassOptions opts)
    {
        _settingsSvc = settingsSvc;
        _state       = state;
        _opts        = opts;

        // Load layered effective config (SQLite over appsettings defaults).
        var cfg = _settingsSvc.Load(opts.Recommender);
        PopulateFromConfig(cfg);

        _loading = false;
    }

    // ── Property-changed hooks (generated code calls these) ───────────────
    partial void OnPlayedFloorMinutesChanged(int value)   => ApplyAndPersist();
    partial void OnKChanged(int value)                     => ApplyAndPersist();
    partial void OnHybridAlphaChanged(double value)        => ApplyAndPersist();
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

    // ── Private helpers ───────────────────────────────────────────────────
    private void PopulateFromConfig(RecommenderConfig cfg)
    {
        PlayedFloorMinutes   = cfg.PlayedFloorMinutes;
        K                    = cfg.K;
        SelectedScorerMode   = cfg.ScorerMode;
        HybridAlpha          = cfg.HybridAlpha;
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
