using Compass.Core.Sync;
using Compass.Recommender;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Compass.Core.Tests;

/// <summary>
/// Quality-floor evaluation suite that exercises RecommenderEvaluator against
/// the baked-in sample library fixture. Floors are set a few points below the
/// observed values (documented in comments) so the suite catches regressions
/// without being brittle to minor floating-point drift.
/// </summary>
public class SampleDataEvalTests
{
    private readonly ITestOutputHelper _output;

    public SampleDataEvalTests(ITestOutputHelper output) => _output = output;

    // ── Fixture ────────────────────────────────────────────────────────────────

    private const int PlayedFloor = 120; // minutes — same as app default

    /// <summary>
    /// How many top recommendations to use for intra-list diversity comparison.
    /// </summary>
    private const int TopN = 10;

    // ── Leave-One-Out Recall@10 ────────────────────────────────────────────────

    /// <summary>
    /// For each played game, hold it out and verify the recommender surfaces it in
    /// the top-10 when it competes against the full backlog. Measures whether the
    /// engine recognises the owner's taste profile against a realistic candidate pool.
    ///
    /// Observed value: 0.8750 (14/16 played items retrieved in top-10 with shared pool).
    /// Floor set at 0.60 — comfortably below observed to allow fixture tweaks while
    /// still catching a broken IDF model or scoring regression.
    /// </summary>
    [Fact]
    public void LeaveOneOutRecallAt10_MeetsQualityFloor()
    {
        var (liked, backlog) = SampleLibrary.ToFixture(PlayedFloor);
        liked.Should().NotBeEmpty("fixture must have played games for this test to be meaningful");
        backlog.Should().NotBeEmpty("fixture must have backlog candidates");

        var eval = new RecommenderEvaluator();
        var options = new RecommenderOptions { K = 5 }; // default K=5 for scoring; k=10 for recall window

        double recall = eval.LeaveOneOutRecallAtK(liked, backlog, k: 10, options);

        _output.WriteLine($"LeaveOneOutRecallAt10: {recall:F4} " +
                          $"(liked={liked.Count}, backlog={backlog.Count})");

        // Observed: 0.8750 (14/16). Floor: 0.60.
        recall.Should().BeGreaterThanOrEqualTo(0.60,
            $"recall@10={recall:F4} is below the 0.60 quality floor " +
            "(observed ~0.875 on the baked-in fixture)");
    }

    // ── Intra-List Diversity: Diversity=0 vs Diversity=0.5 ───────────────────

    /// <summary>
    /// Verifies that MMR re-ranking (Diversity=0.5) produces intra-list diversity
    /// that is ≥ the baseline (Diversity=0 / pure relevance). The ≥ check is intentional:
    /// if the backlog is already highly diverse, MMR may not be able to improve it further,
    /// but it must never make it worse.
    ///
    /// Observed values (top-10 recommendations):
    ///   Diversity=0.0  → 0.5783
    ///   Diversity=0.5  → 0.6440
    /// Both floors set conservatively at 0.40.
    /// </summary>
    [Fact]
    public void IntraListDiversity_IncreasesOrHoldsWithMmr()
    {
        var (liked, backlog) = SampleLibrary.ToFixture(PlayedFloor);

        var recommender = new ContentRecommender();
        var eval = new RecommenderEvaluator();

        var baseOptions = new RecommenderOptions { K = 5, Diversity = 0.0 };
        var mmrOptions  = new RecommenderOptions { K = 5, Diversity = 0.5 };

        var baseResult = recommender.Recommend(liked, backlog, baseOptions);
        var mmrResult  = recommender.Recommend(liked, backlog, mmrOptions);

        // Extract feature vectors for the top-N recommendations.
        var baseVectors = baseResult.Recommendations
            .Take(TopN)
            .Select(r => backlog.First(c => c.ItemId == r.ItemId).Features)
            .ToList();

        var mmrVectors = mmrResult.Recommendations
            .Take(TopN)
            .Select(r => backlog.First(c => c.ItemId == r.ItemId).Features)
            .ToList();

        double baseDiversity = eval.IntraListDiversity(baseVectors);
        double mmrDiversity  = eval.IntraListDiversity(mmrVectors);

        _output.WriteLine($"IntraListDiversity  base (δ=0.0): {baseDiversity:F4}");
        _output.WriteLine($"IntraListDiversity  mmr  (δ=0.5): {mmrDiversity:F4}");

        // Both must clear the absolute floor.
        // Observed: base 0.5783, mmr 0.6440. Floor: 0.40.
        baseDiversity.Should().BeGreaterThanOrEqualTo(0.40,
            $"base diversity {baseDiversity:F4} is below floor 0.40 " +
            "(observed 0.5783; indicates the fixture is too homogeneous)");

        mmrDiversity.Should().BeGreaterThanOrEqualTo(0.40,
            $"mmr diversity {mmrDiversity:F4} is below floor 0.40 " +
            "(observed 0.6440)");

        // MMR must not make diversity worse.
        mmrDiversity.Should().BeGreaterThanOrEqualTo(baseDiversity,
            $"MMR (δ=0.5) diversity {mmrDiversity:F4} < base {baseDiversity:F4}; " +
            "re-ranking should never reduce intra-list variety");
    }
}
