namespace Jumbo.Shared.Dtos;

public class MarketItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SvgImage { get; set; } = string.Empty;
    public int Price { get; set; }
    public bool CanBuy { get; set; }

    // life | refill | shield | freeze | frame | avatar | badge | theme | pet
    public string Kind { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    // Things that are worn are bought once (Owned) and then put on (Equipped); things that are used up can be bought again.
    public bool Owned { get; set; }
    public bool Equipped { get; set; }
    public bool Consumable => Kind is "life" or "refill" or "shield" or "freeze";
}
