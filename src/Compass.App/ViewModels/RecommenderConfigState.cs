using Compass.Core.Config;

namespace Compass.App.ViewModels;

/// <summary>
/// Shared mutable wrapper around the effective <see cref="RecommenderConfig"/>.
/// Singletons that need the live config read <see cref="Current"/> at use-time;
/// <see cref="SettingsViewModel"/> writes to it whenever a knob changes.
/// </summary>
public sealed class RecommenderConfigState
{
    public RecommenderConfig Current { get; set; }

    public RecommenderConfigState(RecommenderConfig initial) => Current = initial;
}
