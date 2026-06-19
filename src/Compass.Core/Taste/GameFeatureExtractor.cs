using Compass.Core.Model;
using Compass.Recommender;

namespace Compass.Core.Taste;

public static class GameFeatureExtractor
{
    public static FeatureVector ToVector(Game g) => FeatureVector.FromKeys(g.FeatureKeys);
}
