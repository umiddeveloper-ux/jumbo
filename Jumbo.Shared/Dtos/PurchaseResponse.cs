namespace Jumbo.Shared.Dtos;

/// <summary>What a purchase (or putting on / taking off) leaves the learner with.</summary>
public class PurchaseResponse
{
    public int LpPoints { get; set; }
    public int LifeCount { get; set; }
    public int ShieldCount { get; set; }
    public int FreezeCount { get; set; }

    // The thing was put on at once (a worn item is worn as soon as it is bought).
    public bool Equipped { get; set; }
}
