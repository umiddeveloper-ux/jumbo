namespace Jumbo.Shared.Entities;

public class Market
{
    public int MarketId { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SvgImageId { get; set; }

    public int Price { get; set; }

    // What the item is and what it does: life | refill | shield | freeze | frame | avatar | badge | theme | pet | hidden (not on sale yet).
    // Key names the thing for the clients ("frame_gold", "theme_mint"); it is the same in every language.
    public string Kind { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
}
