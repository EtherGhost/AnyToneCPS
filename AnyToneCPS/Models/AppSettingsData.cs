namespace AnyToneCPS.Models;

public sealed class AppSettingsData
{
    public string ThemeMode { get; set; } = "Dark";
    public string ExportDirectory { get; set; } = "";

    // "Don't show again" for the startup VOX safety warning (see
    // MainViewModel's ShowVoxStartupWarning) - added 2026-07-30.
    public bool SuppressVoxStartupWarning { get; set; }
}
