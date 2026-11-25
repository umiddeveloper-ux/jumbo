using Jumbo.Api.Data;
using Jumbo.Shared;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Gives every picture that has a photo inside its svg the name of its file (svg_images.image_file, see PictureFiles). Runs at API start, after
// the books are seeded: only rows without a name are looked at, in small batches, so a start with nothing to do reads almost nothing, and a
// row without a photo (a drawing) or with a broken one just stays empty. A picture whose svg was changed by hand must have image_file set
// to NULL; the next start computes it again.
public static class PictureBackfill
{
    private const int Batch = 100;

    public static async Task<int> RunAsync(AppDbContext db, ILogger? log = null)
    {
        var done = 0;
        var after = 0;
        while (true)
        {
            var rows = await db.SvgImages
                .Where(s => s.ImageFile == null && s.SvgImageId > after && EF.Functions.Like(s.SVG, "%data:image/%"))
                .OrderBy(s => s.SvgImageId)
                .Take(Batch)
                .ToListAsync();
            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                row.ImageFile = PictureFiles.FileNameOf(row.SVG);
                if (row.ImageFile is not null) done++;
            }
            after = rows[^1].SvgImageId;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();                 // the svg texts are big: do not keep the batch in memory
        }

        if (done > 0) log?.LogInformation("Pictures: {Count} picture file names filled in", done);
        return done;
    }
}
