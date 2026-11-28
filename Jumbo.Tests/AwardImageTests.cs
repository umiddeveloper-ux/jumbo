using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The picture of an award as a file of the site: only "Book N: Completed" (the awards 1..14 of the seed order) get the frame of the book with its number.
public class AwardImageTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(1, "/frames/award-01.svg")]
    [InlineData(3, "/frames/award-03.svg")]
    [InlineData(10, "/frames/award-10.svg")]
    [InlineData(14, "/frames/award-14.svg")]
    public void A_book_award_has_the_picture_of_its_book(int index, string url)
        => Assert.Equal(url, AwardService.ImageUrlOf(index));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]          // "You started learning"
    [InlineData(15)]         // the levels
    [InlineData(19)]
    [InlineData(100)]
    public void The_other_awards_have_no_picture_file(int index)
        => Assert.Null(AwardService.ImageUrlOf(index));

    [Fact]
    public async Task The_place_of_an_award_is_found_by_its_picture_in_every_language()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();      // never committed: the shared test database stays as it was

        var svgs = new List<SvgImage>();
        for (var i = 0; i < 4; i++) svgs.Add(new SvgImage { SVG = $"<svg id='t{i}'/>" });
        db.SvgImages.AddRange(svgs);
        await db.SaveChangesAsync();

        // the seed order: three rows (en, ru, uz) per award, in one picture
        foreach (var s in svgs)
            foreach (var lang in new[] { 1, 2, 3 })
                db.Awards.Add(new Award { LanguageId = lang, SvgImageId = s.SvgImageId, AwardName = $"t{s.SvgImageId}-{lang}" });
        await db.SaveChangesAsync();

        var map = await new AwardService(db).IndexBySvgAsync();
        var order = map.Where(kv => svgs.Any(s => s.SvgImageId == kv.Key)).OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        Assert.Equal(svgs.Select(s => s.SvgImageId), order);                   // the order of the seed
        Assert.All(svgs, s => Assert.True(map.ContainsKey(s.SvgImageId)));
        // the index is one per picture, not per language row
        Assert.Equal(map.Count, map.Values.Distinct().Count());

        await tx.RollbackAsync();
    }
}
