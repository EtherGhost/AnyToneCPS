using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// Verifies that the CURRENTLY SAVED project file matches what's actually
/// on the radio right now - a permanent tool (promoted 2026-07-20 from a
/// temporary diagnostic added 2026-07-19 to validate the Channel canonical-
/// model migration). Requires the project to already have been saved
/// somewhere (<see cref="MainViewModel._currentProjectStorage"/> non-null) -
/// this deliberately reads the SAVED file back from disk rather than
/// diffing the live in-memory Channels/Zones/etc., so an unsaved edit
/// correctly shows up as a "mismatch" (it isn't in the saved file yet).
///
/// Flow: load the saved project file into a throwaway scratch copy (doesn't
/// disturb what's on screen), do a fresh independent Read From Radio into
/// another throwaway copy, then diff every plain (reflection-visible)
/// property plus the reference-typed membership fields (Zone Members/A/B
/// Channel, Scan List Members/Priority Channel 1/2 - compared by Channel
/// Number, not object identity, since the two reads produce entirely
/// separate ChannelEntry instances) for every entity type that has a real
/// radio write path: Channels, Zones, Scan Lists, and the 3 encryption key
/// lists. Every other entity type has no write path yet (see the "not yet
/// writable" banners on their own tabs), so there's nothing to verify there.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty] private bool _isVerifyingRoundtrip;
    [ObservableProperty] private string _verifyRoundtripStatusText = "";
    public ObservableCollection<string> VerifyRoundtripMismatches { get; } = [];

    partial void OnIsVerifyingRoundtripChanged(bool value)
    {
        ReadFromRadioCommand.NotifyCanExecuteChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();
    }

    private bool CanVerifyReadSaveRoundtrip() =>
        !IsReadingFromRadio
        && !IsWritingToRadio
        && !IsVerifyingRoundtrip
        && _radioConnectionFactory is not null
        && !string.IsNullOrWhiteSpace(SelectedPort)
        && _currentProjectStorage is not null;

    [RelayCommand(CanExecute = nameof(CanVerifyReadSaveRoundtrip))]
    private async Task VerifyReadSaveRoundtripAsync()
    {
        if (_radioConnectionFactory is null || string.IsNullOrWhiteSpace(SelectedPort) || _currentProjectStorage is null)
        {
            return;
        }

        IsVerifyingRoundtrip = true;
        VerifyRoundtripMismatches.Clear();
        VerifyRoundtripStatusText = "Loading the saved project file...";

        try
        {
            var savedData = await LoadProjectDataOnBackgroundAsync(_currentProjectStorage);
            if (savedData is null)
            {
                VerifyRoundtripStatusText = "Verify failed: could not load the saved project file.";
                return;
            }

            var savedChannels = new List<ChannelEntry>();
            var savedZones = new List<ZoneEntry>();
            var savedScanLists = new List<ScanListEntry>();
            var savedEncryptionKeys = new List<EncryptionKeyEntry>();
            var savedArc4EncryptionKeys = new List<EncryptionKeyEntry>();
            var savedAesEncryptionKeys = new List<EncryptionKeyEntry>();
            RadioProjectMapper.LoadInto(
                savedData, savedChannels, savedZones,
                savedEncryptionKeys, savedArc4EncryptionKeys, savedAesEncryptionKeys,
                scanLists: savedScanLists);

            VerifyRoundtripStatusText = "Reading from radio again for comparison (this does not touch the currently loaded data)...";
            var portName = SelectedPort;
            var includeDigitalContacts = IncludeDigitalContactList;
            var includeEncryptionKeys = IncludeEncryptionKeysList;
            var readResult = await Task.Run(() =>
            {
                var connection = _radioConnectionFactory();
                return RadioCodeplugReader.Read(connection, portName, null, includeDigitalContacts, includeEncryptionKeys);
            });

            if (!readResult.Success)
            {
                VerifyRoundtripStatusText = $"Verify failed: re-read from radio failed ({readResult.Error}).";
                return;
            }

            var freshChannels = RadioReadMapper.MapChannels(readResult);
            var talkgroupNames = RadioReadMapper.BuildTalkgroupNameLookup(readResult);
            var radioIdNames = RadioReadMapper.BuildRadioIdNameLookup(readResult);
            var receiveGroupNames = RadioReadMapper.BuildReceiveGroupNameLookup(readResult);
            foreach (var channel in freshChannels)
            {
                channel.ContactDisplayName = RadioReadMapper.ResolveContactName(channel, talkgroupNames);
                channel.RadioIdDisplayName = RadioReadMapper.ResolveRadioIdName(channel, radioIdNames);
                channel.ReceiveGroupListDisplayName = RadioReadMapper.ResolveReceiveGroupListName(channel, receiveGroupNames);
            }

            var freshChannelsByRadioIndex = readResult.Channels
                .Where(c => !c.IsBlank)
                .Zip(freshChannels, (decoded, entry) => (decoded.Index, entry))
                .ToDictionary(pair => pair.Index, pair => pair.entry);
            var freshZones = RadioReadMapper.MapZones(readResult, freshChannelsByRadioIndex);
            var freshScanLists = RadioReadMapper.MapScanLists(readResult, freshChannelsByRadioIndex);

            var mismatches = new List<string>();
            mismatches.AddRange(CompareEntities(savedChannels, freshChannels, "Channel", c => c.Number, c => c.Name));
            mismatches.AddRange(CompareEntities(savedZones, freshZones, "Zone", z => z.Number, z => z.Name, ZoneReferenceFieldsExcluded));
            mismatches.AddRange(CompareReferenceCollection(savedZones, freshZones, "Zone", z => z.Number, z => z.Name, "Members", z => z.Members.Select(c => c.Number)));
            mismatches.AddRange(CompareReferenceField(savedZones, freshZones, "Zone", z => z.Number, z => z.Name, "A Channel", z => z.AChannel?.Number));
            mismatches.AddRange(CompareReferenceField(savedZones, freshZones, "Zone", z => z.Number, z => z.Name, "B Channel", z => z.BChannel?.Number));
            mismatches.AddRange(CompareEntities(savedScanLists, freshScanLists, "Scan List", s => s.Number, s => s.Name, ScanListReferenceFieldsExcluded));
            mismatches.AddRange(CompareReferenceCollection(savedScanLists, freshScanLists, "Scan List", s => s.Number, s => s.Name, "Members", s => s.Members.Select(c => c.Number)));
            mismatches.AddRange(CompareReferenceField(savedScanLists, freshScanLists, "Scan List", s => s.Number, s => s.Name, "Priority Channel 1", s => s.PriorityChannel1?.Number));
            mismatches.AddRange(CompareReferenceField(savedScanLists, freshScanLists, "Scan List", s => s.Number, s => s.Name, "Priority Channel 2", s => s.PriorityChannel2?.Number));

            if (includeEncryptionKeys)
            {
                var freshEncryptionKeys = RadioReadMapper.MapBasicEncryptionCodes(readResult);
                var freshArc4EncryptionKeys = RadioReadMapper.MapArc4EncryptionKeys(readResult);
                var freshAesEncryptionKeys = RadioReadMapper.MapAesEncryptionKeys(readResult);
                mismatches.AddRange(CompareEntities(savedEncryptionKeys, freshEncryptionKeys, "Digital Encryption Code", k => k.Number, k => k.EncryptionId));
                mismatches.AddRange(CompareEntities(savedArc4EncryptionKeys, freshArc4EncryptionKeys, "ARC4 Key", k => k.Number, k => k.EncryptionKey));
                mismatches.AddRange(CompareEntities(savedAesEncryptionKeys, freshAesEncryptionKeys, "AES Key", k => k.Number, k => k.EncryptionId));
            }
            else
            {
                mismatches.Add("Encryption keys not compared - check 'Include Encryption Keys' on the Radio tab and re-run to verify them too.");
            }

            foreach (var mismatch in mismatches)
            {
                VerifyRoundtripMismatches.Add(mismatch);
            }

            VerifyRoundtripStatusText = mismatches.Count == 0
                ? "Verify: the saved project file matches the radio exactly."
                : $"Verify: {mismatches.Count} mismatch(es) found between the saved project file and the radio - see below.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            VerifyRoundtripStatusText = $"Verify failed: {exception.Message}";
        }
        finally
        {
            IsVerifyingRoundtrip = false;
        }
    }

    /// <summary>Zone's own reference-typed fields need Number-based
    /// comparison (see <see cref="CompareReferenceField"/>), not plain
    /// reflection Equals - two independent reads produce entirely separate
    /// ChannelEntry instances, so reference equality would always report a
    /// false mismatch.</summary>
    private static readonly HashSet<string> ZoneReferenceFieldsExcluded = ["Members", "AChannel", "BChannel"];

    private static readonly HashSet<string> ScanListReferenceFieldsExcluded = ["Members", "PriorityChannel1", "PriorityChannel2"];

    /// <summary>Reflection-based diff over every read/write public property
    /// on <typeparamref name="T"/> (i.e. real data fields - computed/
    /// derived properties are getter-only and skipped automatically), so
    /// this doesn't need hand-maintaining a field list as each entity
    /// changes. <paramref name="excludedProperties"/> skips reference-typed
    /// fields that need their own Number-based comparison instead - see
    /// <see cref="CompareReferenceField"/>/<see cref="CompareReferenceCollection"/>.</summary>
    private static List<string> CompareEntities<T>(
        IReadOnlyList<T> saved,
        IReadOnlyList<T> fresh,
        string entityLabel,
        Func<T, int> numberSelector,
        Func<T, string> nameSelector,
        IReadOnlySet<string>? excludedProperties = null)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .Where(p => excludedProperties is null || !excludedProperties.Contains(p.Name))
            .ToList();

        var freshByNumber = fresh.ToDictionary(numberSelector);
        var savedByNumber = saved.ToDictionary(numberSelector);
        var mismatches = new List<string>();

        foreach (var savedItem in saved)
        {
            var number = numberSelector(savedItem);
            if (!freshByNumber.TryGetValue(number, out var freshItem))
            {
                mismatches.Add($"{entityLabel} {number} ('{nameSelector(savedItem)}'): present in saved file, missing from fresh radio read");
                continue;
            }

            foreach (var property in properties)
            {
                var savedValue = property.GetValue(savedItem);
                var freshValue = property.GetValue(freshItem);
                if (!Equals(savedValue, freshValue))
                {
                    mismatches.Add($"{entityLabel} {number} ('{nameSelector(savedItem)}'): {property.Name} = '{savedValue}' (saved) vs '{freshValue}' (fresh radio read)");
                }
            }
        }

        foreach (var freshItem in fresh)
        {
            var number = numberSelector(freshItem);
            if (!savedByNumber.ContainsKey(number))
            {
                mismatches.Add($"{entityLabel} {number} ('{nameSelector(freshItem)}'): present in fresh radio read, missing from saved file");
            }
        }

        return mismatches;
    }

    /// <summary>Compares a single reference-typed field (e.g. Zone.AChannel)
    /// by the referenced channel's Number, not object identity.</summary>
    private static List<string> CompareReferenceField<T>(
        IReadOnlyList<T> saved,
        IReadOnlyList<T> fresh,
        string entityLabel,
        Func<T, int> numberSelector,
        Func<T, string> nameSelector,
        string fieldLabel,
        Func<T, int?> referenceNumberSelector)
    {
        var freshByNumber = fresh.ToDictionary(numberSelector);
        var mismatches = new List<string>();
        foreach (var savedItem in saved)
        {
            var number = numberSelector(savedItem);
            if (!freshByNumber.TryGetValue(number, out var freshItem))
            {
                continue;
            }

            var savedRef = referenceNumberSelector(savedItem);
            var freshRef = referenceNumberSelector(freshItem);
            if (savedRef != freshRef)
            {
                mismatches.Add($"{entityLabel} {number} ('{nameSelector(savedItem)}'): {fieldLabel} = '{savedRef}' (saved) vs '{freshRef}' (fresh radio read)");
            }
        }

        return mismatches;
    }

    /// <summary>Compares a reference-typed collection (e.g. Zone.Members) by
    /// the referenced channels' Numbers, in order.</summary>
    private static List<string> CompareReferenceCollection<T>(
        IReadOnlyList<T> saved,
        IReadOnlyList<T> fresh,
        string entityLabel,
        Func<T, int> numberSelector,
        Func<T, string> nameSelector,
        string fieldLabel,
        Func<T, IEnumerable<int>> membersSelector)
    {
        var freshByNumber = fresh.ToDictionary(numberSelector);
        var mismatches = new List<string>();
        foreach (var savedItem in saved)
        {
            var number = numberSelector(savedItem);
            if (!freshByNumber.TryGetValue(number, out var freshItem))
            {
                continue;
            }

            var savedMembers = membersSelector(savedItem).ToList();
            var freshMembers = membersSelector(freshItem).ToList();
            if (!savedMembers.SequenceEqual(freshMembers))
            {
                mismatches.Add($"{entityLabel} {number} ('{nameSelector(savedItem)}'): {fieldLabel} = [{string.Join(",", savedMembers)}] (saved) vs [{string.Join(",", freshMembers)}] (fresh radio read)");
            }
        }

        return mismatches;
    }
}
