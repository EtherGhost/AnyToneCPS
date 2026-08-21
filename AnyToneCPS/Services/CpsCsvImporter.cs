using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public sealed class ZoneImportResult
{
    public List<ZoneEntry> Zones { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class CpsCsvImporter
{
    private static readonly string[][] ChannelHeaderSets =
    [
        ["No", "Name", "RX", "TX", "ChannelType"],
        ["No.", "Channel Name", "Receive Frequency", "Transmit Frequency", "Channel Type"]
    ];

    private static readonly string[] ZoneHeaders =
    [
        "No.",
        "Zone Name",
        "Zone Channel Member"
    ];

    public static List<ChannelEntry> ReadChannels(string path)
    {
        return ReadCsvFiles(path, SearchOption.TopDirectoryOnly)
            .SelectMany(file => ReadRows(file, CsvImportKind.Channel))
            .Select(ToChannel)
            .OrderBy(channel => channel.Number)
            .ToList();
    }

    public static List<ChannelEntry> ReadChannels(IEnumerable<string> files)
    {
        return files
            .SelectMany(file => ReadRows(file, CsvImportKind.Channel))
            .Select(ToChannel)
            .OrderBy(channel => channel.Number)
            .ToList();
    }

    public static ZoneImportResult ReadZones(string path, IEnumerable<ChannelEntry> channels)
    {
        var result = new ZoneImportResult();
        var channelLookup = channels
            .GroupBy(channel => channel.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in ReadCsvFiles(path, SearchOption.AllDirectories).SelectMany(file => ReadRows(file, CsvImportKind.Zone)))
        {
            result.Zones.Add(ToZone(row, channelLookup, result.Warnings));
        }

        result.Zones.Sort((left, right) => left.Number.CompareTo(right.Number));
        return result;
    }

    public static ZoneImportResult ReadZones(IEnumerable<string> files, IEnumerable<ChannelEntry> channels)
    {
        var result = new ZoneImportResult();
        var channelLookup = channels
            .GroupBy(channel => channel.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in files.SelectMany(file => ReadRows(file, CsvImportKind.Zone)))
        {
            result.Zones.Add(ToZone(row, channelLookup, result.Warnings));
        }

        result.Zones.Sort((left, right) => left.Number.CompareTo(right.Number));
        return result;
    }

    private static IEnumerable<string> ReadCsvFiles(string path, SearchOption searchOption)
    {
        if (File.Exists(path))
        {
            return [path];
        }

        if (!Directory.Exists(path))
        {
            throw new FileNotFoundException("CSV file or directory was not found.", path);
        }

        return Directory
            .EnumerateFiles(path, "*.csv", searchOption)
            .Where(file =>
            {
                var name = Path.GetFileName(file);
                return !name.StartsWith('_') && !name.StartsWith('~') && new FileInfo(file).Length > 0;
            })
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Dictionary<string, string>> ReadRows(string path, CsvImportKind importKind)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headers = ReadCsvRecord(reader);
        if (headers.Count == 0)
        {
            yield break;
        }

        ValidateHeaders(path, headers, importKind);

        while (!reader.EndOfStream)
        {
            var values = ReadCsvRecord(reader);
            if (values.Count == 0 || values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index].Trim()] = index < values.Count ? values[index].Trim() : "";
            }

            yield return row;
        }
    }

    private static void ValidateHeaders(string path, IReadOnlyCollection<string> headers, CsvImportKind importKind)
    {
        var headerSet = headers
            .Select(header => header.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isValid = importKind switch
        {
            CsvImportKind.Channel => ChannelHeaderSets.Any(requiredHeaders => requiredHeaders.All(headerSet.Contains)),
            CsvImportKind.Zone => ZoneHeaders.All(headerSet.Contains),
            _ => false
        };

        if (!isValid)
        {
            var expected = importKind == CsvImportKind.Channel
                ? "channel CSV columns (No/Name/RX/TX/ChannelType or No./Channel Name/Receive Frequency/Transmit Frequency/Channel Type)"
                : "zone CSV columns (No./Zone Name/Zone Channel Member)";
            throw new FormatException($"{Path.GetFileName(path)} is not a valid {importKind.ToString().ToLowerInvariant()} CSV. Expected {expected}.");
        }
    }

    private static List<string> ReadCsvRecord(TextReader reader)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                if (value.Length > 0 || values.Count > 0)
                {
                    values.Add(value.ToString());
                }

                return values;
            }

            var character = (char)next;
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        value.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    value.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    values.Add(value.ToString());
                    value.Clear();
                    break;
                case '\n':
                    values.Add(value.ToString());
                    return values;
                case '\r':
                    if (reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    values.Add(value.ToString());
                    return values;
                default:
                    value.Append(character);
                    break;
            }
        }
    }

    // TODO: disabled during the Channel canonical-model migration - built
    // against the old string-based ChannelEntry shape (ReceiveFrequency/
    // Contact/etc. no longer exist). Reconnect once CSV import is rewritten
    // against the new typed model (RxFrequencyMHz/ContactIndex/etc.).
    private static ChannelEntry ToChannel(IReadOnlyDictionary<string, string> row) =>
        throw new NotSupportedException("Channel CSV import is disabled during the Channel canonical-model migration.");

    private static ZoneEntry ToZone(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, ChannelEntry> channels,
        ICollection<string> warnings)
    {
        var zone = new ZoneEntry
        {
            Number = ReadInt(row, "No.", "No"),
            Name = Read(row, "Zone Name", "Name"),
            IsHidden = IsTruthy(ReadOrDefault(row, "0", "Zone Hide ", "Zone Hide"))
        };

        foreach (var memberName in SplitPipe(Read(row, "Zone Channel Member")))
        {
            if (channels.TryGetValue(memberName, out var channel))
            {
                zone.Members.Add(channel);
            }
            else
            {
                warnings.Add($"Zone {zone.Number} '{zone.Name}' references missing channel: {memberName}");
            }
        }

        zone.AChannel = FindChannel(row, channels, "A Channel") ?? zone.Members.FirstOrDefault();
        zone.BChannel = FindChannel(row, channels, "B Channel") ?? zone.Members.Skip(1).FirstOrDefault() ?? zone.Members.FirstOrDefault();

        return zone;
    }

    private static ChannelEntry? FindChannel(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, ChannelEntry> channels,
        string field)
    {
        var name = Read(row, field);
        return name.Length > 0 && channels.TryGetValue(name, out var channel)
            ? channel
            : null;
    }

    private static IEnumerable<string> SplitPipe(string value)
    {
        return value
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length > 0);
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        var value = Read(row, keys);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private static string Read(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        return ReadOrDefault(row, "", keys);
    }

    private static string ReadOrDefault(IReadOnlyDictionary<string, string> row, string fallback, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return fallback;
    }

    private static bool IsTruthy(string value)
    {
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private enum CsvImportKind
    {
        Channel,
        Zone
    }
}
