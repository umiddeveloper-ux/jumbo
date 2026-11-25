namespace Jumbo.Shared.Dtos;

public class SvgImageResponse
{
    public string Svg { get; set; } = string.Empty;

    // The picture as a file (e.g. "/pics/0f3a9c21b7d4e8a6.webp", relative to the site), when it exists. Old clients ignore it and use Svg.
    // When the server setting Pictures:OmitSvg is on, Svg is empty for a picture that has a file.
    public string? ImageUrl { get; set; }
}
