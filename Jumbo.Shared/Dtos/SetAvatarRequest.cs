namespace Jumbo.Shared.Dtos;

/// <summary>The learner's own avatar: one of the preset pictures (the svg text) or an uploaded photo (a data: url).</summary>
public class SetAvatarRequest
{
    public string AvatarSvg { get; set; } = null!;
}
