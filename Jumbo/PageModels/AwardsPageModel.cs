using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.Shared.Dtos;
using Jumbo.Utilities;
using Jumbo.ApiClient;
using Jumbo.Services;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>A card of the market: the picture, what it does, the price and the button (Buy / Put on / Take off).</summary>
public partial class MarketCard : ObservableObject
{
    public int Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SvgHtml { get; init; } = string.Empty;
    public string PriceText { get; init; } = string.Empty;
    public string EffectText { get; init; } = string.Empty;
    public string NoteText { get; init; } = string.Empty;         // "visible on the website" for the things the app does not show
    public bool HasNote => NoteText.Length > 0;
    public string ButtonText { get; init; } = string.Empty;
    public bool CanTap { get; init; }
    public bool IsWorn { get; init; }
    public bool IsOwned { get; init; }
    public string ButtonKind => IsWorn || !CanTap ? "outline" : "primary";
}

public partial class AwardsPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly LivesService _livesService;

    private bool _isNavigatedTo;
    private bool _dataLoaded = false;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccessMarket))]
    private int _userLp;

    // The width of an award tile: three in a row (the page sets it from its own width).
    [ObservableProperty]
    private double _awardTileWidth = 100;

    // The width of a market card: two in a row (the page sets it from its own width).
    [ObservableProperty]
    private double _marketCardWidth = 150;

    // The market is open to everybody now: the prices start at 50 LP.
    public bool CanAccessMarket => true;

    [ObservableProperty]
    private List<MarketCard> _marketCards = [];

    // What the learner has in stock: shields and freezes.
    [ObservableProperty]
    private string _inventoryText = string.Empty;

    // Используем ObservableCollection для динамического обновления интерфейса
    [ObservableProperty]
    private List<AwardItem> _awards = [];

    [ObservableProperty]
    private List<MarketItem> _marketItems = [];

    public AwardsPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler, LivesService livesService)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _livesService = livesService;
    }

    private async Task LoadData()
    {
        try
        {
            IsBusy = true;

            var user = await _apiClient.GetCurrentUserAsync();
            UserLp = user?.LpPoints ?? 0;
            InventoryText = BuildInventory(user?.ShieldCount ?? 0, user?.FreezeCount ?? 0);

            Awards = await _apiClient.GetAwardsAsync(UserLanguagePrefs.AppLangId);
            MarketItems = await _apiClient.GetMarketItemsAsync(UserLanguagePrefs.AppLangId);
            MarketCards = MarketItems.Select(ToCard).ToList();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CheckIfLevelAchieved(string userLevel, string awardLevel)
    {
        // Простая иерархия уровней для сравнения
        var levels = new List<string> { "A0", "A1", "A2", "B1", "B2", "C1", "C2" };
        int userIndex = levels.IndexOf(userLevel);
        int awardIndex = levels.IndexOf(awardLevel);
        return userIndex >= awardIndex && awardIndex != -1;
    }

    private static string BuildInventory(int shields, int freezes) =>
        string.Join("   ", new[] { shields > 0 ? $"🛡️ {shields}" : null, freezes > 0 ? $"❄️ {freezes}" : null }.Where(x => x is not null));

    private MarketCard ToCard(MarketItem m)
    {
        bool webOnly = m.Kind is "theme";                                     // colour themes are drawn by the website only
        bool note = m.Kind is "frame" or "avatar" or "badge" or "theme" or "pet";
        string button = m.Equipped ? LocalizationManager.Get("Market_Unequip")
            : m.Owned ? LocalizationManager.Get("Market_Equip")
            : UserLp < m.Price ? LocalizationManager.Get("Market_NotEnough")
            : LocalizationManager.Get("Market_Buy");
        return new MarketCard
        {
            Id = m.Id,
            Kind = m.Kind,
            Name = m.Name,
            SvgHtml = SvgHtmlWrapper.Wrap(m.SvgImage),
            PriceText = $"💎 {m.Price} LP",
            EffectText = LocalizationManager.Get($"Market_Eff_{m.Kind}"),
            NoteText = note ? LocalizationManager.Get("Market_WebNote") : string.Empty,
            ButtonText = button,
            IsOwned = m.Owned,
            IsWorn = m.Equipped,
            CanTap = !webOnly && (m.Equipped || m.Owned || m.CanBuy)
        };
    }

    // Buy, put on or take off, then read everything again: LP, lives, shields, the shop.
    [RelayCommand]
    private async Task Act(MarketCard? card)
    {
        if (card is null || !card.CanTap || IsBusy) return;
        try
        {
            IsBusy = true;
            if (card.IsWorn) await _apiClient.UnequipAsync(card.Kind);
            else if (card.IsOwned) await _apiClient.EquipAsync(card.Id);
            else
            {
                await _apiClient.PurchaseAsync(card.Id);
                await AppShell.DisplayToastAsync(LocalizationManager.Format("Awards_Bought", card.Name));
            }
            await _livesService.InitAsync();
            await LoadData();
        }
        catch (Exception ex)
        {
            var key = $"Market_Err_{ex.Message}";
            var text = LocalizationManager.Get(key);
            await Shell.Current.DisplayAlertAsync(string.Empty, text == key ? ex.Message : text, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadData();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void NavigatedTo() => _isNavigatedTo = true;

    [RelayCommand]
    private void NavigatedFrom() => _isNavigatedTo = false;

    [RelayCommand]
    private async Task Appearing()
    {
        if (!_dataLoaded)
        {
            _dataLoaded = true;

            // Даем MAUI 100 миллисекунд, чтобы отрисовать скелет страницы и WebView,
            // после чего гарантированно наполняем списки данными
            await Task.Delay(100);
            await LoadData();
        }
        else if (!_isNavigatedTo)
        {
            await Refresh();
        }
    }
}

