using Android.Content.Res;
using Google.Android.Material.BottomNavigation;
using Jumbo.Services;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using IShellContext = Microsoft.Maui.Controls.Platform.Compatibility.IShellContext;

namespace Jumbo.Platforms.Android;

/// <summary>
/// The bottom tabs: the pill under the active tab takes the soft colour of the level of the page (red, yellow, green, violet, else orange)
/// and follows the theme chosen in the app. The colour of Material 3 comes from the Android resources, which follow the theme of the
/// phone and not the moon button of the app, so it is set here at run time.
/// </summary>
public class JumboShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new JumboBottomTracker(this, shellItem);

    private sealed class JumboBottomTracker : ShellBottomNavViewAppearanceTracker
    {
        private WeakReference<BottomNavigationView>? _view;

        public JumboBottomTracker(IShellContext context, ShellItem item) : base(context, item)
        {
            ThemePalette.Changed += OnPaletteChanged;
        }

        public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            base.SetAppearance(bottomView, appearance);
            _view = new WeakReference<BottomNavigationView>(bottomView);
            Apply(bottomView);
        }

        private void OnPaletteChanged(object? sender, EventArgs e)
        {
            if (_view is not null && _view.TryGetTarget(out var v)) v.Post(() => Apply(v));
        }

        private static void Apply(BottomNavigationView v)
            => v.ItemActiveIndicatorColor = ColorStateList.ValueOf(ThemePalette.PrimarySoft.ToPlatform());

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemePalette.Changed -= OnPaletteChanged;
            base.Dispose(disposing);
        }
    }
}
