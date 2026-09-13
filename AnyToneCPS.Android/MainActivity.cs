using System.IO;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.SplashScreen;
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
        // Must be installed BEFORE base.OnCreate() (matches the AndroidX
        // SplashScreen migration guide) - held on screen via IsReady until
        // App.axaml.cs's MainViewFactory finishes constructing MainViewModel,
        // not just its own ~1s icon animation (styles.xml's
        // windowSplashScreenAnimationDuration). Without this, the native
        // splash dismissed on its own default timing, well before
        // MainViewModel's real construction cost (including its one-time
        // validator warm-up under NativeAOT - see that method's own doc
        // comment) finished, leaving a blank view on screen for the rest of
        // the wait - real bug found live 2026-09-13.
        // Fully qualified - Activity's own inherited native "SplashScreen"
        // property (Android 13+ SDK, unrelated Window API) shadows the
        // AndroidX.Core.SplashScreen.SplashScreen class this actually needs.
        var splashScreen = AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);
        splashScreen.SetKeepOnScreenCondition(new KeepOnScreenUntilReady());

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

    // SplashScreen.SetKeepOnScreenCondition takes this Java functional
    // interface, not a plain Func<bool> - the .NET binding doesn't expose a
    // delegate overload.
    private sealed class KeepOnScreenUntilReady : Java.Lang.Object, SplashScreen.IKeepOnScreenCondition
    {
        public bool ShouldKeepOnScreen() => !AppStartupSignal.IsReady;
    }
}
