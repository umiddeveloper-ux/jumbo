using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Resources.Strings;

namespace Jumbo.PageModels;

public partial class ScanQrPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private bool _processing;

    [ObservableProperty]
    private bool _isBusy;

    public ScanQrPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    public async Task HandleScannedCodeAsync(string code)
    {
        if (_processing || IsBusy) return;
        if (!Guid.TryParse(code, out var sessionId)) return;

        _processing = true;
        IsBusy = true;
        try
        {
            await _apiClient.ConfirmQrLoginAsync(sessionId);
            await AppShell.DisplaySnackbarAsync(AppResources.QrConfirmedMessage);
            await Task.Delay(1000);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
            _processing = false;
        }
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync("..");
}
