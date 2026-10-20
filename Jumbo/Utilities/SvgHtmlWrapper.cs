namespace Jumbo.Utilities;

public static class SvgHtmlWrapper
{
    public static string Wrap(string rawSvg)
    {
        if (string.IsNullOrWhiteSpace(rawSvg)) return string.Empty;
        return $"<html><head><style>body{{margin:0;padding:0;display:flex;justify-content:center;align-items:center;background:transparent;overflow:hidden;height:100vh;width:100vw;}}svg{{max-width:100%;max-height:100%;width:auto;height:auto;}}</style></head><body>{rawSvg}</body></html>";
    }
}
