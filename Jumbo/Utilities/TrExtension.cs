using Jumbo.Services;

namespace Jumbo.Utilities;

/// <summary>XAML markup extension: <c>Text="{u:Tr Key=Reg_Next}"</c> (live-updates when the app language changes).</summary>
[ContentProperty(nameof(Key))]
public class TrExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay, source: LocalizationManager.Instance);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ((IMarkupExtension<BindingBase>)this).ProvideValue(serviceProvider);
}
