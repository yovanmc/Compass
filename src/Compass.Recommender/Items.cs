namespace Compass.Recommender;

public sealed record ProfileItem(string ItemId, FeatureVector Features, double Affinity);
public sealed record CandidateItem(string ItemId, FeatureVector Features);
