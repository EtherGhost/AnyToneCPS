using System;
using System.IO;
using Avalonia;
using AnyToneCPS.Desktop.Services;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;

namespace AnyToneCPS.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Diagnostic-only radio protocol trace, opt-in via RadioProtocolLog
        // being a no-op until Start() is called - see its own doc comment
        // for why this exists. Truncated on every startup so there's never
        // ambiguity about which run a log belongs to.
        RadioProtocolLog.Start(Path.Combine(AppSettingsStore.SettingsDirectory, "radio-protocol.log"));

        // Only the Desktop head can talk to the radio (System.IO.Ports isn't
        // available on Android/iOS/Browser). Register the factory here so the
        // shared project's MainWindow can pick up a real connection without
        // ever referencing this Desktop-only project.
        RadioConnectionProvider.Factory = () => new SerialRadioConnection();
        RadioConnectionProvider.PortLister = SerialPortLister.GetAvailablePorts;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}