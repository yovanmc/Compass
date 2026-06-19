using Compass.Recommender;
using FluentAssertions;
using Xunit;

public class VectorMathTests
{
    [Fact]
    public void L2Normalize_MakesUnitLength()
    {
        var v = new Dictionary<string, double> { ["a"] = 3, ["b"] = 4 };
        var n = VectorMath.L2Normalize(v);
        n["a"].Should().BeApproximately(0.6, 1e-9);
        n["b"].Should().BeApproximately(0.8, 1e-9);
    }

    [Fact]
    public void L2Normalize_ZeroVector_ReturnsEmpty()
        => VectorMath.L2Normalize(new Dictionary<string, double>()).Should().BeEmpty();

    [Fact]
    public void Cosine_OfNormalizedVectors_EqualsDot()
    {
        var a = VectorMath.L2Normalize(new Dictionary<string, double> { ["x"] = 1, ["y"] = 1 });
        var b = VectorMath.L2Normalize(new Dictionary<string, double> { ["x"] = 1 });
        VectorMath.Dot(a, b).Should().BeApproximately(1 / Math.Sqrt(2), 1e-9);
    }
}
