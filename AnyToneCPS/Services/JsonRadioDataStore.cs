using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public sealed class JsonRadioDataStore : IRadioDataStore
{
    public JsonRadioDataStore(string? filePath = null)
    {
        Location = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AnyToneCPS",
            "SE_Field_Comms_D890UV_v1.dat");
    }

    public string DisplayName => "JSON file";
    public string Location { get; }

    public async Task<RadioProjectData?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Location))
        {
            return null;
        }

        await using var stream = File.OpenRead(Location);
        var project = await JsonSerializer.DeserializeAsync<RadioProjectData>(
            stream,
            RadioProjectJsonContext.Default.RadioProjectData,
            cancellationToken);

        if (project is not null)
        {
            DecryptKeysAfterLoad(project);
        }

        return project;
    }

    public async Task SaveAsync(RadioProjectData project, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Location);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var toSave = BuildEncryptedCloneForSave(project);

        await using var stream = File.Create(Location);
        await JsonSerializer.SerializeAsync(
            stream,
            toSave,
            RadioProjectJsonContext.Default.RadioProjectData,
            cancellationToken);
    }

    /// <summary>Decrypts key material in place after a deserialize - shared
    /// by every <see cref="IProjectStorage"/>/<see cref="IRadioDataStore"/>
    /// implementation that reads a <see cref="RadioProjectData"/> from JSON,
    /// so there's one place that knows about <see cref="EncryptionKeyProtector"/>
    /// rather than each caller needing its own copy (a real bug found
    /// 2026-08-16: <c>AvaloniaProjectStorage</c>, used whenever the picked
    /// file has no local filesystem path - the normal case for Android
    /// content:// providers like a cloud-sync app - did its own raw
    /// JsonSerializer call and skipped decryption entirely, since it was
    /// never updated when this feature was added).</summary>
    internal static void DecryptKeysAfterLoad(RadioProjectData project)
    {
        DecryptKeysInPlace(project.EncryptionKeys);
        DecryptKeysInPlace(project.Arc4EncryptionKeys);
        DecryptKeysInPlace(project.AesEncryptionKeys);
    }

    /// <summary>Builds a fresh clone with key material encrypted, ready to
    /// serialize - see <see cref="DecryptKeysAfterLoad"/>'s own doc comment
    /// for why this is shared rather than duplicated per storage
    /// implementation. Encrypts into a clone rather than mutating the
    /// caller's own project - the live app's in-memory entries must keep
    /// holding plain hex, only the on-disk JSON is protected. See
    /// EncryptionKeyProtector's own doc comment for what this does and
    /// doesn't protect against.</summary>
    internal static RadioProjectData BuildEncryptedCloneForSave(RadioProjectData project) => new()
    {
        Channels = project.Channels,
        Zones = project.Zones,
        EncryptionKeys = EncryptKeysToNewList(project.EncryptionKeys),
        Arc4EncryptionKeys = EncryptKeysToNewList(project.Arc4EncryptionKeys),
        AesEncryptionKeys = EncryptKeysToNewList(project.AesEncryptionKeys),
        RadioIds = project.RadioIds,
        Talkgroups = project.Talkgroups,
        ScanLists = project.ScanLists,
        RoamingChannels = project.RoamingChannels,
        RoamingZones = project.RoamingZones,
        ReceiveGroupLists = project.ReceiveGroupLists,
        AutoRepeaterOffsets = project.AutoRepeaterOffsets,
        AnalogAddresses = project.AnalogAddresses,
        GpsRoamingEntries = project.GpsRoamingEntries,
        TalkgroupWhitelist = project.TalkgroupWhitelist,
        DigitalContactWhitelist = project.DigitalContactWhitelist,
        PrefabricatedSms = project.PrefabricatedSms,
        AmAirChannels = project.AmAirChannels,
        AmZones = project.AmZones,
        FmChannels = project.FmChannels,
        MasterId = project.MasterId,
        TalkAliasSettings = project.TalkAliasSettings,
        AlarmSettings = project.AlarmSettings,
        AprsSettings = project.AprsSettings,
        AprsReceiveFilters = project.AprsReceiveFilters,
        OptionalSettings = project.OptionalSettings,
        DigitalContacts = project.DigitalContacts,
        DigitalContactsGenuinelyPopulatedFromRadio = project.DigitalContactsGenuinelyPopulatedFromRadio
    };

    private static List<EncryptionKeyData> EncryptKeysToNewList(List<EncryptionKeyData>? keys)
    {
        if (keys is null)
        {
            return [];
        }

        var result = new List<EncryptionKeyData>(keys.Count);
        foreach (var key in keys)
        {
            result.Add(new EncryptionKeyData
            {
                Number = key.Number,
                EncryptionKey = EncryptionKeyProtector.Encrypt(key.EncryptionKey),
                EncryptionId = key.EncryptionId
            });
        }

        return result;
    }

    private static void DecryptKeysInPlace(List<EncryptionKeyData>? keys)
    {
        if (keys is null)
        {
            return;
        }

        foreach (var key in keys)
        {
            key.EncryptionKey = EncryptionKeyProtector.Decrypt(key.EncryptionKey);
        }
    }
}

public sealed class JsonFileProjectStorage(string path) : IProjectStorage
{
    public string Path { get; } = path;
    public string DisplayLocation => Path;

    public Task<RadioProjectData?> LoadAsync()
    {
        return new JsonRadioDataStore(Path).LoadAsync();
    }

    public Task SaveAsync(RadioProjectData project)
    {
        return new JsonRadioDataStore(Path).SaveAsync(project);
    }
}

public sealed class ProjectStorageSettings
{
    public string Kind { get; set; } = "";
    public string Location { get; set; } = "";
    public string Bookmark { get; set; } = "";
    public string DisplayLocation { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RadioProjectData))]
[JsonSerializable(typeof(ProjectStorageSettings))]
[JsonSerializable(typeof(AppSettingsData))]
[JsonSerializable(typeof(RadioIdData))]
[JsonSerializable(typeof(TalkgroupData))]
[JsonSerializable(typeof(ScanListData))]
[JsonSerializable(typeof(RoamingChannelData))]
[JsonSerializable(typeof(RoamingZoneData))]
[JsonSerializable(typeof(ReceiveGroupListData))]
[JsonSerializable(typeof(AutoRepeaterOffsetData))]
[JsonSerializable(typeof(MasterIdData))]
[JsonSerializable(typeof(TalkAliasSettingsData))]
[JsonSerializable(typeof(AnalogAddressData))]
[JsonSerializable(typeof(GpsRoamingData))]
[JsonSerializable(typeof(TalkgroupWhitelistData))]
[JsonSerializable(typeof(DigitalContactWhitelistData))]
[JsonSerializable(typeof(AprsSettingsData))]
[JsonSerializable(typeof(AprsFixLocationData))]
[JsonSerializable(typeof(AprsDigitalReportData))]
[JsonSerializable(typeof(AprsReceiveFilterData))]
[JsonSerializable(typeof(OptionalSettingsData))]
[JsonSerializable(typeof(AlertToneData))]
[JsonSerializable(typeof(PrefabricatedSmsData))]
[JsonSerializable(typeof(AmAirData))]
[JsonSerializable(typeof(AmZoneData))]
[JsonSerializable(typeof(FmChannelData))]
[JsonSerializable(typeof(AlarmSettingsData))]
[JsonSerializable(typeof(DigitalContactData))]
internal sealed partial class RadioProjectJsonContext : JsonSerializerContext;
