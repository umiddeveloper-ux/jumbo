namespace Jumbo.Shared.Entities;

public class SvgImage
{
    public int SvgImageId { get; set; }

    public string SVG { get; set; } = null!;

    // The name of the picture file made from the photo inside the svg (PictureFiles.FileNameOf), e.g. "0f3a9c21b7d4e8a6.webp".
    // Null: not computed yet (the API fills it at start) or the svg has no photo. A change of SVG by hand (SQL) must set it to NULL.
    public string? ImageFile { get; set; }
}
