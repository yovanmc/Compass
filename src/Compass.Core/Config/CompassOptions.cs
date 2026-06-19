namespace Compass.Core.Config;

public sealed class CompassOptions
{
    public SteamOptions Steam { get; set; } = new();
    public IgdbOptions Igdb { get; set; } = new();
    public RecommenderConfig Recommender { get; set; } = new();
}

public sealed class SteamOptions
{
    public string SteamId64 { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed class IgdbOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public sealed class RecommenderConfig
{
    public int PlayedFloorMinutes { get; set; } = 120;
    public double RecencyWeight { get; set; } = 0.25;
    public int K { get; set; } = 5;
    public string ScorerMode { get; set; } = "NearestNeighbor";
    public Dictionary<string, double> CategoryWeights { get; set; } = new();
    public double NegativeWeight { get; set; } = 0.0;
    public bool UseImplicitNegatives { get; set; } = false;
    public double HybridAlpha { get; set; } = 0.5;
    public double Diversity { get; set; } = 0.3;
    public double FeedbackWeight { get; set; } = 1.0;
}
