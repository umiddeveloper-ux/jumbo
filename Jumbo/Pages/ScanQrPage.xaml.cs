using Jumbo.PageModels;
using ZXing.Net.Maui;

namespace Jumbo.Pages;

public partial class ScanQrPage : ContentPage
{
    private readonly ScanQrPageModel _model;

    public ScanQrPage(ScanQrPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
        _model = model;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        MainThread.BeginInvokeOnMainThread(async () => await _model.HandleScannedCodeAsync(value));
    }
}
