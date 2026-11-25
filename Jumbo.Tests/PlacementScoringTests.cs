using Jumbo.Shared;

namespace Jumbo.Tests;

// The level test result: which stage and which book the learner is sent to.
public class PlacementScoringTests
{
    [Theory]
    [InlineData(new double[] { }, "starter", "A1", 1)]
    [InlineData(new[] { 0.39 }, "starter", "A1", 1)]
    [InlineData(new[] { 0.40 }, "a1", "A1", 1)]
    [InlineData(new[] { 0.69 }, "a1", "A1", 1)]
    [InlineData(new[] { 0.70 }, "a1plus", "A1", 3)]
    [InlineData(new[] { 0.70, 0.39 }, "a1plus", "A1", 3)]
    [InlineData(new[] { 0.70, 0.40 }, "a2", "A2", 4)]
    [InlineData(new[] { 0.90, 0.70 }, "a2plus", "A2", 6)]
    [InlineData(new[] { 0.90, 0.70, 0.30 }, "a2plus", "A2", 6)]
    [InlineData(new[] { 0.90, 0.80, 0.45 }, "b1", "B1", 7)]
    [InlineData(new[] { 0.90, 0.80, 0.70 }, "b1plus", "B1", 9)]
    [InlineData(new[] { 0.90, 0.80, 0.70, 0.40 }, "b2", "B2", 10)]
    [InlineData(new[] { 0.90, 0.80, 0.70, 0.70 }, "b2plus", "B2", 10)]
    public void Evaluate_gives_the_stage_and_the_book(double[] shares, string stage, string level, int book)
    {
        var result = PlacementScoring.Evaluate(shares);
        Assert.Equal(new PlacementResult(stage, level, book), result);
    }

    [Fact]
    public void A_weak_first_block_ends_the_test_even_when_later_blocks_are_listed()
    {
        Assert.Equal("starter", PlacementScoring.Evaluate([0.1, 1.0, 1.0, 1.0]).Stage);
    }

    [Theory]
    [InlineData(0.69, false)]
    [InlineData(0.70, true)]
    [InlineData(1.0, true)]
    public void The_next_block_follows_only_from_70_percent(double share, bool goesOn)
        => Assert.Equal(goesOn, PlacementScoring.GoesOn(share));

    [Fact]
    public void Every_stage_points_to_an_existing_book()
    {
        // books 1-10 exist; no result may send a learner to a book that is not there
        var stages = new[] { new double[] { }, [0.5], [0.7], [0.7, 0.5], [0.8, 0.8], [0.8, 0.8, 0.5], [0.8, 0.8, 0.8], [0.8, 0.8, 0.8, 0.5], [0.8, 0.8, 0.8, 0.8] };
        foreach (var s in stages)
            Assert.InRange(PlacementScoring.Evaluate(s).RecommendedBook, 1, 10);
    }
}
