using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compass.Core.Covers;
using Compass.Core.Model;
using Compass.Core.Sync;
using Compass.Core.Taste;
using System.Globalization;

namespace Compass.App.ViewModels;

/// <summary>A grouping of feature items under a category label (e.g. Genres, Themes).</summary>
public sealed record FeatureGroup(string Category, IReadOnlyList<string> Items);

/// <summary>A "top factor" row: humanized feature name + bar fraction (0..1, relative to the strongest factor).</summary>
public sealed record FeatureBar(string Name, double Fraction);

/// <summary>A "more like this" entry: target appId + name + similarity percent.</summary>
public sealed partial class SimilarRow : ObservableObject
{
    public int AppId { get; }
    public string Name { get; }
    public int ScorePercent { get; }

    [ObservableProperty]
    private string? coverPath;

    public SimilarRow(int appId, string name, int scorePercent)
    {
        AppId = appId;
        Name = name;
        ScorePercent = scorePercent;
    }
}

/// <summary>ViewModel for the game detail slide-over panel.</summary>
public sealed partial class DetailViewModel : ObservableObject, IDisposable
{
    private readonly Game _game;
    private readonly GameRecommendation? _rec;
    private readonly ICoverProvider _covers;
    private readonly ISyncStore _store;
    private readonly Action _onChangedAndClose;
    private readonly Action _onLibraryChanged;
    private CancellationTokenSource _coverCts = new();

    // ── Bound properties ──────────────────────────────────────────────────

    public string Name => _game.Name;

    /// Played / Backlog / Unmatched / Hidden — same rule as GameRow.From
    public string StatusLabel => _game.NotInterested ? "Hidden"
        : _game.IgdbId is null ? "Unmatched"
        : _game.PlaytimeForeverMinutes >= 120 ? "Played"
        : "Backlog";

    public string PlaytimeForeverDisplay
    {
        get
        {
            int h = _game.PlaytimeForeverMinutes / 60;
            int m = _game.PlaytimeForeverMinutes % 60;
            if (h > 0 && m > 0) return $"{h}h {m}m";
            if (h > 0)          return $"{h}h";
            return $"{m}m";
        }
    }

    public string Playtime2WeeksDisplay
    {
        get
        {
            if (_game.Playtime2WeeksMinutes == 0) return string.Empty;
            int h = _game.Playtime2WeeksMinutes / 60;
            int m = _game.Playtime2WeeksMinutes % 60;
            if (h > 0 && m > 0) return $"{h}h {m}m recently";
            if (h > 0)          return $"{h}h recently";
            return $"{m}m recently";
        }
    }

    public string MatchLabel => _game.MatchMethod switch
    {
        MatchMethod.AppId => $"Matched by app ID ({_game.MatchConfidence:P0})",
        MatchMethod.Name  => $"Matched by name ({_game.MatchConfidence:P0})",
        _                 => "Unmatched"
    };

    [ObservableProperty]
    private string? coverPath;

    public bool HasScore => _rec is not null;

    public int ScorePercent => _rec is null ? 0 : (int)Math.Round(Math.Clamp(_rec.Score, 0, 1) * 100);

    // ── Feature groups ────────────────────────────────────────────────────

    public IReadOnlyList<FeatureGroup> FeatureGroups { get; }

    // ── Score breakdown (only when HasScore) ─────────────────────────────

    public IReadOnlyList<FeatureBar> TopFeatures
    {
        get
        {
            if (_rec is null || _rec.WhyFeatures.Count == 0) return [];
            double max = _rec.WhyFeatures.Max(w => w.Contribution);
            return _rec.WhyFeatures
                .Select(w => new FeatureBar(w.Name, max > 0 ? Math.Clamp(w.Contribution / max, 0, 1) : 0.0))
                .ToList();
        }
    }
    public IReadOnlyList<string> NearestLoved  => _rec?.WhyLikedNames       ?? [];
    public IReadOnlyList<string> PenalizedBy   => _rec?.WhyPenalizedNames   ?? [];

    // ── More like this ────────────────────────────────────────────────────

    public IReadOnlyList<SimilarRow> Similar { get; private set; } = [];
    public bool HasSimilar => Similar.Count > 0;

    /// <summary>Raised when the user clicks a "more like this" entry; the shell re-opens detail for that appId.</summary>
    public event Action<int>? GameChosen;

    [RelayCommand]
    private void OpenSimilar(SimilarRow? row)
    {
        if (row is not null) GameChosen?.Invoke(row.AppId);
    }

    // ── Not-interested toggle ─────────────────────────────────────────────

    [ObservableProperty]
    private bool isNotInterested;

    // ── Feedback (more / less like this) ─────────────────────────────────

    [ObservableProperty]
    private int feedback;

    public bool IsMoreLiked => Feedback > 0;
    public bool IsLessLiked => Feedback < 0;

    partial void OnFeedbackChanged(int value)
    {
        OnPropertyChanged(nameof(IsMoreLiked));
        OnPropertyChanged(nameof(IsLessLiked));
    }

    [RelayCommand]
    private void MoreLikeThis()
    {
        int next = Feedback > 0 ? 0 : 1;           // toggle on/off
        _store.SetFeedback(_game.SteamAppId, next);
        Feedback = next;
        _onLibraryChanged();                        // re-rank pages; keep panel open
    }

    [RelayCommand]
    private void LessLikeThis()
    {
        int next = Feedback < 0 ? 0 : -1;          // toggle on/off
        _store.SetFeedback(_game.SteamAppId, next);
        Feedback = next;
        _onLibraryChanged();
    }

    [RelayCommand]
    private void ToggleNotInterested()
    {
        _store.SetNotInterested(_game.SteamAppId, !IsNotInterested);
        _onChangedAndClose();
    }

    // ── Constructor ───────────────────────────────────────────────────────

    public DetailViewModel(
        Game game,
        GameRecommendation? rec,
        ICoverProvider covers,
        ISyncStore store,
        Action onChangedAndClose,
        Action onLibraryChanged,
        IReadOnlyList<SimilarRow>? similar = null)
    {
        _game = game;
        _rec = rec;
        _covers = covers;
        _store = store;
        _onChangedAndClose = onChangedAndClose;
        _onLibraryChanged = onLibraryChanged;

        Similar = similar ?? [];
        IsNotInterested = game.NotInterested;
        Feedback = game.Feedback;
        FeatureGroups = BuildFeatureGroups(game.FeatureKeys);

        // Fire-and-forget cover load; cancel on dispose.
        _ = LoadCoverAsync(_coverCts.Token);

        foreach (var row in Similar)
            _ = LoadSimilarCoverAsync(row, _coverCts.Token);
    }

    // ── Cover loading ─────────────────────────────────────────────────────

    public async Task LoadCoverAsync(CancellationToken ct)
    {
        // Do NOT ConfigureAwait(false) — must resume on UI thread so the property
        // set marshals correctly.
        CoverPath = await _covers.GetCoverPathAsync(_game.SteamAppId, ct);
    }

    private async Task LoadSimilarCoverAsync(SimilarRow row, CancellationToken ct)
    {
        row.CoverPath = await _covers.GetCoverPathAsync(row.AppId, ct);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _coverCts.Cancel();
        _coverCts.Dispose();
    }

    // ── Feature group builder ─────────────────────────────────────────────

    private static IReadOnlyList<FeatureGroup> BuildFeatureGroups(IReadOnlyList<string> featureKeys)
    {
        var buckets = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["Genres"]     = new(),
            ["Themes"]     = new(),
            ["Game modes"] = new(),
            ["Keywords"]   = new(),
        };

        foreach (var key in featureKeys)
        {
            int colonIdx = key.IndexOf(':');
            if (colonIdx < 0) continue;

            string prefix = key[..colonIdx];
            string raw    = key[(colonIdx + 1)..];
            string label  = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.Replace('-', ' '));

            string? bucket = prefix switch
            {
                "genre"   => "Genres",
                "theme"   => "Themes",
                "mode"    => "Game modes",
                "keyword" => "Keywords",
                _         => null
            };

            if (bucket is not null && buckets.TryGetValue(bucket, out var list))
                list.Add(label);
        }

        var result = new List<FeatureGroup>();
        foreach (var (category, items) in buckets)
        {
            if (items.Count > 0)
                result.Add(new FeatureGroup(category, items));
        }
        return result;
    }
}
