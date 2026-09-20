using System;
using Avalonia.Controls;

namespace AnyToneCPS.Views;

// Shared by every "open this in a browser" link (About's Source, Settings'
// Report an Issue/Releases) - Avalonia's TopLevel.Launcher already handles
// the platform difference (xdg-open/shell open on Desktop, an Intent on
// Android, window.open on Browser), so this just needs the control that
// was clicked to find its own TopLevel.
internal static class UrlLauncher
{
    public static void Open(string url, Control sender)
    {
        _ = TopLevel.GetTopLevel(sender)?.Launcher.LaunchUriAsync(new Uri(url));
    }
}
