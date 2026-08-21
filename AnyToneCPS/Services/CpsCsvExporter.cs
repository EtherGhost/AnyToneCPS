using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public static class CpsCsvExporter
{
    public static readonly Encoding AnyToneCsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // TODO: disabled during the Channel canonical-model migration - built
    // against the old string-based ChannelEntry shape (ReceiveFrequency/
    // Contact/etc. no longer exist). Reconnect once CSV export is rewritten
    // against the new typed model (RxFrequencyMHz/ContactIndex/etc.).
    public static string BuildChannelCsv(IEnumerable<ChannelEntry> channels) =>
        throw new NotSupportedException("Channel CSV export is disabled during the Channel canonical-model migration.");

    // TODO: disabled alongside BuildChannelCsv - also reads now-removed
    // ChannelEntry string fields (ReceiveFrequency/TransmitFrequency) via
    // zone.Members/AChannel/BChannel.
    public static string BuildZoneCsv(IEnumerable<ZoneEntry> zones) =>
        throw new NotSupportedException("Zone CSV export is disabled during the Channel canonical-model migration.");

    public static IReadOnlyList<string> WriteExports(
        string directory,
        IEnumerable<ChannelEntry> channels,
        IEnumerable<ZoneEntry> zones)
    {
        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var channelPath = GetAvailablePath(directory, $"channellist_{stamp}", ".CSV");
        var zonePath = GetAvailablePath(directory, $"zonelist_{stamp}", ".CSV");

        File.WriteAllText(
            channelPath,
            BuildChannelCsv(channels),
            AnyToneCsvEncoding);

        File.WriteAllText(
            zonePath,
            BuildZoneCsv(zones),
            AnyToneCsvEncoding);

        return [channelPath, zonePath];
    }

    private static string GetAvailablePath(string directory, string fileNameWithoutExtension, string extension)
    {
        var path = Path.Combine(directory, $"{fileNameWithoutExtension}{extension}");
        if (!File.Exists(path))
        {
            return path;
        }

        for (var index = 2; ; index++)
        {
            path = Path.Combine(directory, $"{fileNameWithoutExtension}_{index}{extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }
}
