namespace Compass.Recommender;

public interface IRecommender
{
    RankedResult Recommend(
        IReadOnlyList<ProfileItem> liked,
        IReadOnlyList<CandidateItem> candidates,
        RecommenderOptions options,
        IReadOnlyList<ProfileItem>? disliked = null);
}
