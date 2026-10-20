namespace JumboWeb.Services;

public static class LetterColors
{
    public static string BgColor(int repetitionLevel) => repetitionLevel switch
    {
        3 => "#4CAF50",
        2 => "#8BC34A",
        1 => "#CDDC39",
        0 => "#FF9800",
        _ => "#9E9E9E"
    };

    public static string TextColor(int repetitionLevel) => repetitionLevel >= 0 ? "#FFFFFF" : "#CCCCCC";
}
