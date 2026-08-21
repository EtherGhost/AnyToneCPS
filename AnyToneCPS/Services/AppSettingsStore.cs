using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public static class AppSettingsStore
{
    public static string SettingsDirectory => Path.Combine(GetApplicationDataDirectory(), "AnyToneCPS");
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static async Task<AppSettingsData> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettingsData();
            }

            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync(
                stream,
                RadioProjectJsonContext.Default.AppSettingsData) ?? new AppSettingsData();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettingsData();
        }
    }

    public static async Task SaveAsync(AppSettingsData settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            await using var stream = File.Create(SettingsPath);
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                RadioProjectJsonContext.Default.AppSettingsData);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string GetApplicationDataDirectory()
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
    }
}
