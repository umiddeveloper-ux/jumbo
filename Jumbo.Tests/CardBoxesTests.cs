using Jumbo.Shared;

namespace Jumbo.Tests;

// The boxes of printed cards: 60 words = 4 topics. The QR code of a box opens the list of its topics.
public class CardBoxesTests
{
    [Theory]
    [InlineData(20, 5)]
    [InlineData(19, 5)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(0, 0)]
    public void A_book_has_one_box_per_four_topics(int topics, int boxes)
        => Assert.Equal(boxes, CardBoxes.Count(topics));

    [Theory]
    [InlineData(1, 20, 1, 4)]
    [InlineData(2, 20, 5, 8)]
    [InlineData(5, 20, 17, 20)]
    [InlineData(5, 18, 17, 18)]       // the last box of a shorter book holds what is left
    public void A_box_holds_its_four_topics(int box, int topics, int first, int last)
        => Assert.Equal((first, last), CardBoxes.Range(box, topics));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(6, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    public void A_box_that_does_not_exist_has_no_topics(int box, int topics)
        => Assert.Null(CardBoxes.Range(box, topics));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(20, 5)]
    public void A_topic_belongs_to_one_box(int topic, int box)
        => Assert.Equal(box, CardBoxes.BoxOf(topic));

    [Fact]
    public void Every_topic_of_a_book_is_in_exactly_one_box()
    {
        var seen = new List<int>();
        for (var box = 1; box <= CardBoxes.Count(20); box++)
        {
            var (first, last) = CardBoxes.Range(box, 20)!.Value;
            seen.AddRange(Enumerable.Range(first, last - first + 1));
        }
        Assert.Equal(Enumerable.Range(1, 20), seen);
    }
}

public class PlacementLabelTests
{
    [Theory]
    [InlineData("starter", "Pre-A1")]
    [InlineData("a1", "A1")]
    [InlineData("a1plus", "A1+")]
    [InlineData("a2", "A2")]
    [InlineData("b1plus", "B1+")]
    [InlineData("b2plus", "B2+")]
    [InlineData("nonsense", "")]
    [InlineData(null, "")]
    public void A_stage_has_a_short_label(string? stage, string label) => Assert.Equal(label, PlacementScoring.Label(stage));

    [Fact]
    public void Every_stage_the_scoring_gives_has_a_label()
    {
        var all = new[] { new double[] { }, [0.5], [0.7], [0.7, 0.5], [0.8, 0.8], [0.8, 0.8, 0.5], [0.8, 0.8, 0.8], [0.8, 0.8, 0.8, 0.5], [0.8, 0.8, 0.8, 0.8] };
        foreach (var s in all) Assert.NotEmpty(PlacementScoring.Label(PlacementScoring.Evaluate(s).Stage));
    }
}
