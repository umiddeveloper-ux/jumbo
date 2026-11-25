using System.Net.Http.Json;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The pictures of the words as files: the names (the same rule as tools/build-pictures.mjs), the address the clients get, the OmitSvg switch.
public class PictureFilesTests
{
    private static string SvgWithPhoto(byte[] photo, string mime = "jpeg") =>
        $"<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'><g><rect width='100' height='100' fill='#a3a06a'/>" +
        $"<image x='-14.71' y='0' width='129.41' height='100' xlink:href='data:image/{mime};base64,{Convert.ToBase64String(photo)}'/></g></svg>";

    [Fact]
    public void The_name_is_the_first_16_hex_letters_of_the_sha256_of_the_photo()
        // the same value that node computes for these bytes (tools/build-pictures.mjs uses the same rule)
        => Assert.Equal("039058c6f2c0cb49.webp", PictureFiles.FileNameOf(SvgWithPhoto([1, 2, 3])));

    [Fact]
    public void The_same_photo_gives_the_same_name_whatever_the_wrapper()
    {
        byte[] photo = [9, 8, 7, 6];
        var other = SvgWithPhoto(photo).Replace("-14.71", "0").Replace("#a3a06a", "#000000");
        Assert.Equal(PictureFiles.FileNameOf(SvgWithPhoto(photo)), PictureFiles.FileNameOf(other));
        Assert.NotEqual(PictureFiles.FileNameOf(SvgWithPhoto(photo)), PictureFiles.FileNameOf(SvgWithPhoto([9, 8, 7, 5])));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='11'/></svg>")]
    [InlineData("<svg><image href='data:image/jpeg;base64,@@@'/></svg>")]
    public void A_drawing_or_a_broken_photo_has_no_file(string? svg)
        => Assert.Null(PictureFiles.FileNameOf(svg));

    [Fact]
    public void The_address_is_relative_to_the_site_and_the_wrapper_shows_that_file()
    {
        Assert.Equal("/pics/abc.webp", PictureFiles.UrlOf("abc.webp"));
        Assert.Null(PictureFiles.UrlOf(null));
        Assert.Null(PictureFiles.UrlOf(""));

        var svg = PictureFiles.SvgFor("/pics/abc.webp");
        Assert.Contains("href='/pics/abc.webp'", svg);
        Assert.Contains("viewBox='0 0 100 100'", svg);              // the same box as the old svg: the styles that size it keep working
        Assert.DoesNotContain("base64", svg);
        Assert.True(svg.Length < 600);
        Assert.Contains("href='/pics/a&#39;b.webp'", PictureFiles.SvgFor("/pics/a'b.webp"));    // a quote in the address cannot leave the attribute
    }
}

public class PictureApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static string Svg(byte[] photo) =>
        $"<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><image x='-14.71' y='0' width='129.41' height='100' xlink:href='data:image/jpeg;base64,{Convert.ToBase64String(photo)}'/></svg>";

    private static byte[] UniquePhoto() => Guid.NewGuid().ToByteArray();

    private async Task<(SvgImage picture, int subjectId)> NewTopicWithWordAsync(byte[] photo)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var picture = new SvgImage { SVG = Svg(photo) };
        db.SvgImages.Add(picture);
        await db.SaveChangesAsync();
        var subject = new Subject { BookId = 99, SubjectName = "pictures-" + Guid.NewGuid().ToString("N")[..8], SvgImageId = picture.SvgImageId, SequenceNumber = 1 };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        var english = await db.Languages.AsNoTracking().SingleAsync(l => l.ShorName == "en");
        db.Words.Add(new Word { SubjectId = subject.SubjectId, LanguageId = english.LanguageId, SvgImageId = picture.SvgImageId, WordName = "pic", FirstSymbol = "P", WordNumber = 1 });
        await db.SaveChangesAsync();
        return (picture, subject.SubjectId);
    }

    private async Task<int> EnglishIdAsync()
    {
        using var scope = api.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Languages.AsNoTracking().SingleAsync(l => l.ShorName == "en")).LanguageId;
    }

    [Fact]
    public async Task The_backfill_names_a_picture_that_has_a_photo_and_leaves_a_drawing_alone()
    {
        var photo = UniquePhoto();
        int photoId, drawingId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var withPhoto = new SvgImage { SVG = Svg(photo) };
            var drawing = new SvgImage { SVG = "<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='11'/></svg>" };
            db.SvgImages.AddRange(withPhoto, drawing);
            await db.SaveChangesAsync();
            photoId = withPhoto.SvgImageId; drawingId = drawing.SvgImageId;

            await PictureBackfill.RunAsync(db);
            Assert.Equal(0, await PictureBackfill.RunAsync(db));         // nothing is left to do: running it again changes nothing
        }

        using var check = api.Services.CreateScope();
        var rows = await check.ServiceProvider.GetRequiredService<AppDbContext>().SvgImages.AsNoTracking()
            .Where(s => s.SvgImageId == photoId || s.SvgImageId == drawingId).ToDictionaryAsync(s => s.SvgImageId);
        Assert.Equal(PictureFiles.FileNameOf(Svg(photo)), rows[photoId].ImageFile);
        Assert.Null(rows[drawingId].ImageFile);
    }

    [Fact]
    public async Task A_picture_whose_svg_was_changed_gets_a_new_name_when_the_old_one_is_cleared()
    {
        var first = UniquePhoto();
        var second = UniquePhoto();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var picture = new SvgImage { SVG = Svg(first) };
        db.SvgImages.Add(picture);
        await db.SaveChangesAsync();
        await PictureBackfill.RunAsync(db);

        // what tools/import-card-images.mjs does in SQL: a new svg and image_file = NULL
        await db.Database.ExecuteSqlRawAsync("UPDATE svg_images SET svg = {0}, image_file = NULL WHERE svg_image_id = {1}", Svg(second), picture.SvgImageId);
        await PictureBackfill.RunAsync(db);

        var name = await db.SvgImages.AsNoTracking().Where(s => s.SvgImageId == picture.SvgImageId).Select(s => s.ImageFile).SingleAsync();
        Assert.Equal(PictureFiles.FileNameOf(Svg(second)), name);
    }

    [Fact]
    public async Task The_picture_answer_has_the_text_for_old_clients_and_the_address_of_the_file()
    {
        var (picture, _) = await NewTopicWithWordAsync(UniquePhoto());
        using (var scope = api.Services.CreateScope()) await PictureBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var answer = await api.CreateClient().GetFromJsonAsync<SvgImageResponse>($"/api/svg-images/{picture.SvgImageId}");
        Assert.NotNull(answer);
        Assert.Equal(picture.SVG, answer.Svg);                                    // old apps (<= 42) use this and nothing else
        Assert.Equal(PictureFiles.UrlOf(PictureFiles.FileNameOf(picture.SVG)), answer.ImageUrl);
    }

    [Fact]
    public async Task A_picture_without_a_name_has_no_address_and_keeps_its_text()
    {
        int id;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var drawing = new SvgImage { SVG = "<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='11'/></svg>" };
            db.SvgImages.Add(drawing);
            await db.SaveChangesAsync();
            id = drawing.SvgImageId;
        }

        using var omit = api.WithWebHostBuilder(b => b.UseSetting("Pictures:OmitSvg", "true"));
        var answer = await omit.CreateClient().GetFromJsonAsync<SvgImageResponse>($"/api/svg-images/{id}");
        Assert.Null(answer!.ImageUrl);
        Assert.NotEmpty(answer.Svg);                                              // OmitSvg never empties a picture that has no file
    }

    [Fact]
    public async Task OmitSvg_leaves_out_the_text_of_a_picture_that_has_a_file()
    {
        var (picture, _) = await NewTopicWithWordAsync(UniquePhoto());
        using (var scope = api.Services.CreateScope()) await PictureBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        using var omit = api.WithWebHostBuilder(b => b.UseSetting("Pictures:OmitSvg", "true"));
        var answer = await omit.CreateClient().GetFromJsonAsync<SvgImageResponse>($"/api/svg-images/{picture.SvgImageId}");
        Assert.Equal(string.Empty, answer!.Svg);
        Assert.Equal(PictureFiles.UrlOf(PictureFiles.FileNameOf(picture.SVG)), answer.ImageUrl);
    }

    [Fact]
    public async Task The_cards_of_a_topic_carry_the_address_and_OmitSvg_empties_their_text()
    {
        var (picture, subjectId) = await NewTopicWithWordAsync(UniquePhoto());
        using (var scope = api.Services.CreateScope()) await PictureBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var english = await EnglishIdAsync();
        var url = $"/api/words/by-subject/cards?subjectId={subjectId}&languageId={english}&translationLanguageId={english}";

        var cards = await api.CreateClient().GetFromJsonAsync<List<WordCardDto>>(url);
        var card = Assert.Single(cards!);
        Assert.Equal(picture.SVG, card.Svg);
        Assert.Equal(PictureFiles.UrlOf(PictureFiles.FileNameOf(picture.SVG)), card.ImageUrl);

        using var omit = api.WithWebHostBuilder(b => b.UseSetting("Pictures:OmitSvg", "true"));
        var slim = Assert.Single((await omit.CreateClient().GetFromJsonAsync<List<WordCardDto>>(url))!);
        Assert.Equal(string.Empty, slim.Svg);
        Assert.Equal(card.ImageUrl, slim.ImageUrl);
    }
}
