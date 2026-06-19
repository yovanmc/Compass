namespace Compass.Core.Taste;

public sealed class AffinityCalculator
{
    private readonly int _floor;
    private readonly double _recencyWeight;

    public AffinityCalculator(int playedFloorMinutes, double recencyWeight)
        => (_floor, _recencyWeight) = (playedFloorMinutes, recencyWeight);

    public bool IsPlayed(int foreverMinutes) => foreverMinutes >= _floor;

    public double Affinity(int foreverMinutes, int twoWeekMinutes)
    {
        if (!IsPlayed(foreverMinutes)) return 0;
        var baseAff = Math.Log(1 + foreverMinutes);
        var recency = _recencyWeight * Math.Log(1 + Math.Max(0, twoWeekMinutes));
        return baseAff + recency;
    }
}
