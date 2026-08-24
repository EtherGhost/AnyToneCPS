using System.IO;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AnyToneCPS.Android.Services;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using Avalonia;
using Avalonia.Android;

namespace AnyToneCPS.Android;

[Activity(
    Label = "AnyToneCPS.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Diagnostic-only radio protocol trace, same as the Desktop head's
        // own Program.cs - see RadioProtocolLog's own doc comment.
        RadioProtocolLog.Start(Path.Combine(AppSettingsStore.SettingsDirectory, "radio-protocol.log"));

        // Must be set BEFORE base.OnCreate() - that call builds and attaches
        // the Avalonia view tree synchronously, and MainView.axaml.cs reads
        // RadioConnectionProvider.Factory the moment it attaches. Setting it
        // after left it null when the check happened (found live 2026-07-28
        // - the Radio tab reported "not available on this platform" despite
        // this exact assignment existing, just running too late).
        RadioConnectionProvider.Factory = () => new AndroidUsbRadioConnection();
        RadioConnectionProvider.PortLister = AndroidUsbDeviceLister.GetAvailableDevices;

        base.OnCreate(savedInstanceState);
    }
}
