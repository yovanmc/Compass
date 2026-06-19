using Compass.Recommender;
using FluentAssertions;

public class RecommenderEvaluatorTests
{
    // ────────────────────────────────────────────────────────────────────────────
    // IntraListDiversity
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IntraListDiversity_TwoOrthogonalVectors_ReturnsOne()
    {
        // genre:strategy and genre:puzzle share no dimensions.
        // Both are unit vectors after L2 norm → cosine = 0 → diversity = 1 − 0 = 1.0.
        var vectors = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:strategy" }),
            FeatureVector.FromKeys(new[] { "genre:puzzle" }),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.IntraListDiversity(vectors).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void IntraListDiversity_TwoIdenticalVectors_ReturnsZero()
    {
        // Cosine of identical unit vectors = 1 → diversity = 1 − 1 = 0.
        var vectors = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:strategy" }),
            FeatureVector.FromKeys(new[] { "genre:strategy" }),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.IntraListDiversity(vectors).Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void IntraListDiversity_EmptyList_ReturnsZero()
    {
        var evaluator = new RecommenderEvaluator();
        evaluator.IntraListDiversity(Array.Empty<FeatureVector>()).Should().Be(0.0);
    }

    [Fact]
    public void IntraListDiversity_SingleItem_ReturnsZero()
    {
        var vectors = new[] { FeatureVector.FromKeys(new[] { "genre:rpg" }) };
        var evaluator = new RecommenderEvaluator();
        evaluator.IntraListDiversity(vectors).Should().Be(0.0);
    }

    [Fact]
    public void IntraListDiversity_ThreeItems_AveragesPairwiseDiversity()
    {
        // A=[strategy], B=[puzzle], C=[strategy].
        // Pairs: (A,B)→1.0, (A,C)→0.0, (B,C)→1.0  → average = 2/3.
        var a = FeatureVector.FromKeys(new[] { "genre:strategy" });
        var b = FeatureVector.FromKeys(new[] { "genre:puzzle" });
        var c = FeatureVector.FromKeys(new[] { "genre:strategy" });
        var evaluator = new RecommenderEvaluator();
        evaluator.IntraListDiversity(new[] { a, b, c })
            .Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // DistinctFeatureCoverage
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DistinctFeatureCoverage_TwoDifferentSingleKeyVectors_ReturnsTwo()
    {
        var vectors = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:strategy" }),
            FeatureVector.FromKeys(new[] { "genre:puzzle" }),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.DistinctFeatureCoverage(vectors).Should().Be(2);
    }

    [Fact]
    public void DistinctFeatureCoverage_OverlappingKeys_CountsUnion()
    {
        // {strategy} ∪ {strategy, rpg} = 2 distinct keys.
        var vectors = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:strategy" }),
            FeatureVector.FromKeys(new[] { "genre:strategy", "genre:rpg" }),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.DistinctFeatureCoverage(vectors).Should().Be(2);
    }

    [Fact]
    public void DistinctFeatureCoverage_EmptyList_ReturnsZero()
    {
        var evaluator = new RecommenderEvaluator();
        evaluator.DistinctFeatureCoverage(Array.Empty<FeatureVector>()).Should().Be(0);
    }

    [Fact]
    public void DistinctFeatureCoverage_ThreeVectors_CountsAllDistinctKeys()
    {
        // {a} ∪ {b} ∪ {a, c} = {a, b, c} = 3 keys.
        var vectors = new[]
        {
            FeatureVector.FromKeys(new[] { "genre:a" }),
            FeatureVector.FromKeys(new[] { "genre:b" }),
            FeatureVector.FromKeys(new[] { "genre:a", "genre:c" }),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.DistinctFeatureCoverage(vectors).Should().Be(3);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // ScoreSpread
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreSpread_KnownList_ReturnsCorrectStats()
    {
        // scores = [1, 3, 5]; mean = 3; stdev = sqrt(((−2)²+0²+(+2)²)/3) = sqrt(8/3)
        var scores = new[] { 1.0, 3.0, 5.0 };
        var evaluator = new RecommenderEvaluator();
        var spread = evaluator.ScoreSpread(scores);

        spread.Min.Should().BeApproximately(1.0, 1e-9);
        spread.Max.Should().BeApproximately(5.0, 1e-9);
        spread.Mean.Should().BeApproximately(3.0, 1e-9);
        spread.Stdev.Should().BeApproximately(Math.Sqrt(8.0 / 3.0), 1e-9);
    }

    [Fact]
    public void ScoreSpread_SingleScore_StdevIsZero()
    {
        var evaluator = new RecommenderEvaluator();
        var spread = evaluator.ScoreSpread(new[] { 7.0 });

        spread.Min.Should().Be(7.0);
        spread.Max.Should().Be(7.0);
        spread.Mean.Should().Be(7.0);
        spread.Stdev.Should().Be(0.0);
    }

    [Fact]
    public void ScoreSpread_EmptyList_ReturnsAllZero()
    {
        var evaluator = new RecommenderEvaluator();
        var spread = evaluator.ScoreSpread(Array.Empty<double>());

        spread.Min.Should().Be(0.0);
        spread.Max.Should().Be(0.0);
        spread.Mean.Should().Be(0.0);
        spread.Stdev.Should().Be(0.0);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // LeaveOneOutRecallAtK — isolated-candidate overload
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LeaveOneOutRecallAtK_ObviousTwin_RecallIsGreaterThanZero()
    {
        // Three liked items.  "A" and "B" share "genre:rpg"; "C" is unrelated.
        // When A is held out:  liked' = {B(rpg+action), C(strategy)}, candidate = A(rpg+fantasy).
        //   "genre:rpg" is in B and A → IDF > 0 → B overlaps with A → score > 0 → hit.
        // When B is held out:  liked' = {A(rpg+fantasy), C(strategy)}, candidate = B(rpg+action).
        //   Same rpg overlap → hit.
        // When C is held out:  liked' = {A, B}, candidate = C(strategy) — no overlap → miss.
        // recall@1 = 2 hits / 3 evaluated > 0.
        var liked = new[]
        {
            new ProfileItem("A", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:fantasy" }), Affinity: 5),
            new ProfileItem("B", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:action" }), Affinity: 5),
            new ProfileItem("C", FeatureVector.FromKeys(new[] { "genre:strategy" }), Affinity: 5),
        };
        var opts = new RecommenderOptions { K = 5 };
        var evaluator = new RecommenderEvaluator();
        evaluator.LeaveOneOutRecallAtK(liked, k: 1, opts).Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void LeaveOneOutRecallAtK_NoOverlap_RecallIsZero()
    {
        // All three items have completely different features — no item can retrieve another.
        var liked = new[]
        {
            new ProfileItem("X", FeatureVector.FromKeys(new[] { "genre:a" }), Affinity: 5),
            new ProfileItem("Y", FeatureVector.FromKeys(new[] { "genre:b" }), Affinity: 5),
            new ProfileItem("Z", FeatureVector.FromKeys(new[] { "genre:c" }), Affinity: 5),
        };
        var opts = new RecommenderOptions { K = 5 };
        var evaluator = new RecommenderEvaluator();
        evaluator.LeaveOneOutRecallAtK(liked, k: 1, opts).Should().Be(0.0);
    }

    [Fact]
    public void LeaveOneOutRecallAtK_EmptyLiked_ReturnsZero()
    {
        var evaluator = new RecommenderEvaluator();
        evaluator.LeaveOneOutRecallAtK(
            Array.Empty<ProfileItem>(), k: 3, new RecommenderOptions())
            .Should().Be(0.0);
    }

    [Fact]
    public void LeaveOneOutRecallAtK_SingleLikedItem_ReturnsZero()
    {
        // Only one item: hold it out → liked' is empty → recommender returns nothing → no hits.
        var liked = new[]
        {
            new ProfileItem("solo", FeatureVector.FromKeys(new[] { "genre:rpg" }), Affinity: 5),
        };
        var evaluator = new RecommenderEvaluator();
        evaluator.LeaveOneOutRecallAtK(liked, k: 1, new RecommenderOptions()).Should().Be(0.0);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // LeaveOneOutRecallAtK — shared-pool overload
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LeaveOneOutRecallAtK_SharedPool_TwinInTopK_RecallIsGreaterThanZero()
    {
        // Liked: two items that share "genre:rpg".
        // Pool: a clearly unrelated item "decoy" (genre:cooking).
        // When A is held out: liked' = {B}, candidates = {A(rpg+fantasy)} ∪ {decoy(cooking)}.
        //   A shares rpg with B → score > 0; decoy has no overlap → score = 0.
        //   A ranks #1 → hit.
        // When B is held out: liked' = {A}, candidates = {B(rpg+action)} ∪ {decoy}.
        //   Same reasoning → hit.
        // recall@1 = 2/2 = 1.0.
        var liked = new[]
        {
            new ProfileItem("A", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:fantasy" }), Affinity: 5),
            new ProfileItem("B", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:action"  }), Affinity: 5),
        };
        var pool = new[]
        {
            new CandidateItem("decoy", FeatureVector.FromKeys(new[] { "genre:cooking" })),
        };
        var opts = new RecommenderOptions { K = 5 };
        var evaluator = new RecommenderEvaluator();
        evaluator.LeaveOneOutRecallAtK(liked, pool, k: 1, opts).Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void LeaveOneOutRecallAtK_SharedPool_EmptyPool_BehavesSameAsIsolatedOverload()
    {
        // With an empty shared pool the two overloads should agree.
        var liked = new[]
        {
            new ProfileItem("A", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:fantasy" }), Affinity: 5),
            new ProfileItem("B", FeatureVector.FromKeys(new[] { "genre:rpg", "genre:action" }), Affinity: 5),
            new ProfileItem("C", FeatureVector.FromKeys(new[] { "genre:strategy" }), Affinity: 5),
        };
        var opts = new RecommenderOptions { K = 5 };
        var evaluator = new RecommenderEvaluator();
        var isolated = evaluator.LeaveOneOutRecallAtK(liked, k: 1, opts);
        var withPool  = evaluator.LeaveOneOutRecallAtK(liked, Array.Empty<CandidateItem>(), k: 1, opts);
        withPool.Should().BeApproximately(isolated, 1e-9);
    }
}
