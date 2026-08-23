using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using AnyToneCPS.Services.Radio.Codecs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// Radio connection / "Read From Radio" concerns, split out of MainViewModel.cs
/// to keep that file from growing further. Write-to-radio concerns live in
/// the separate <c>MainViewModel.RadioWrite.cs</c> - deliberately kept
/// narrow (a single channel's write-safe fields only) rather than mixed in
/// here.
/// </summary>
public partial class MainViewModel
{
    private Func<IRadioConnection>? _radioConnectionFactory;
    private Func<IReadOnlyList<string>>? _portLister;

    /// <summary>Raw codeplug snapshot from the last successful Read From
    /// Radio or Write - reused as the base for the NEXT write instead of
    /// re-capturing fresh every time (see
    /// <c>MainViewModel.RadioWrite.cs</c>'s <c>WriteChangesToRadioAsync</c>).
    /// Matches the vendor CPS's own behavior: `Device::writeOtherData()`
    /// never re-reads from the radio before writing, it just serializes
    /// whatever's already in its in-memory model. Null means "no write is
    /// possible yet" - cleared whenever the loaded project changes out from
    /// under it (<see cref="NewProject"/>/<see cref="LoadProject"/>) since a
    /// different project's channels no longer correspond to these cached
    /// bytes.</summary>
    internal RadioCodeplugRawSnapshot? _cachedRadioSnapshot;

    /// <summary>True only once a read this session has actually included
    /// Digital Contacts (`includeDigitalContacts: true` in
    /// <see cref="ApplyRadioReadResult"/>) - NOT reset by a later ordinary
    /// read that skips them (see that method's own doc comment on the
    /// 2026-08-16 fix), but IS reset alongside <see cref="_cachedRadioSnapshot"/>
    /// whenever the loaded project changes out from under it
    /// (<see cref="NewProject"/>/<see cref="LoadProject"/>), since a
    /// project file's own Digital Contacts (if any) were never genuinely
    /// read from a connected radio. Gates whether the write-time "Include
    /// Digital Contact List" option can be turned on at all -
    /// <see cref="DigitalContactWriter"/> always rewrites the whole
    /// contact stream from whatever's in memory, so writing it without
    /// this being true would silently replace the radio's real contact
    /// database with an incomplete one.</summary>
    private bool _digitalContactsGenuinelyPopulatedFromRadio;

    internal bool CanIncludeDigitalContactsInWrite => _digitalContactsGenuinelyPopulatedFromRadio;

    public ObservableCollection<string> AvailablePorts { get; } = [];
    public ObservableCollection<string> RadioReadWarnings { get; } = [];

    [ObservableProperty] private string? _selectedPort;
    [ObservableProperty] private bool _isReadingFromRadio;
    [ObservableProperty] private string _radioReadStatusText = "";
    [ObservableProperty] private int _radioReadProgressCurrent;
    [ObservableProperty] private int _radioReadProgressTotal;
    [ObservableProperty] private string? _radioIdentitySummary;

    /// <summary>Device Information + Local Information (factory/dealer
    /// metadata) - never saved to the project file, never written back,
    /// only ever populated by a successful Read From Radio and cleared on
    /// any failed one (same "can't get the info if not connected" lifetime
    /// as <see cref="RadioIdentitySummary"/> itself). See LocalInfoCodec's
    /// doc comment for the byte layout and encoding.</summary>
    [ObservableProperty] private string? _deviceModel;
    [ObservableProperty] private string? _deviceVersion;
    [ObservableProperty] private LocalInfoCodec.DecodedLocalInfo? _deviceLocalInfo;

    // Fixed per the D890UV's own standard band plan - matches the exact
    // numbers already used elsewhere in this app as validation limits
    // (OptionalSettingsEntry's VFO Scan/Auto Repeater VHF/UHF band-limit
    // messages), not read from the radio itself (frequency_mode, decoded
    // by the reference project's ExpertOptions class, selects among ~15
    // possible band combinations for special-order units, but a standard
    // D890UV is always this one - MODE 00000 in the reference's own
    // Constants::AT_OPTIONS list).
    public string DeviceBands => "VHF 136-174 MHz, UHF 400-480 MHz";

    public bool HasDeviceLocalInfo => DeviceLocalInfo is not null;

    /// <summary>Off by default, matching the vendor CPS's own "Digital
    /// Contact List" checkbox in its read/write options dialog (also
    /// unchecked by default there) - reading this list can be slow/unbounded
    /// if a large DMR-ID database has been imported, unlike every other
    /// entity here which reads unconditionally. See <see cref="DigitalContactCodec"/>.</summary>
    [ObservableProperty] private bool _includeDigitalContactList;

    /// <summary>Off by default. A real USB capture (2026-07-19) of the
    /// vendor CPS confirmed it never reads encryption key material back
    /// from the radio at all - write-only there. Our own read is genuine
    /// (real key bytes, not placeholders) but slow: one 16-byte serial
    /// round-trip per block, ~1024 of them for the AES key table alone.
    /// Since there's no vendor behavior to match and no other entity this
    /// slow, make it opt-in like <see cref="IncludeDigitalContactList"/>
    /// rather than always paying that cost.</summary>
    [ObservableProperty] private bool _includeEncryptionKeysList;

    public bool IsRadioAvailableOnThisPlatform => _radioConnectionFactory is not null;
    public bool HasRadioIdentitySummary => !string.IsNullOrEmpty(RadioIdentitySummary);
    public bool HasRadioReadWarnings => RadioReadWarnings.Count > 0;

    public string RadioReadProgressPercentText => RadioReadProgressTotal <= 0
        ? ""
        : $"{RadioReadProgressCurrent * 100 / RadioReadProgressTotal}%";

    partial void OnRadioIdentitySummaryChanged(string? value) => OnPropertyChanged(nameof(HasRadioIdentitySummary));
    partial void OnDeviceLocalInfoChanged(LocalInfoCodec.DecodedLocalInfo? value) => OnPropertyChanged(nameof(HasDeviceLocalInfo));
    partial void OnRadioReadProgressCurrentChanged(int value) => OnPropertyChanged(nameof(RadioReadProgressPercentText));
    partial void OnRadioReadProgressTotalChanged(int value) => OnPropertyChanged(nameof(RadioReadProgressPercentText));

    partial void OnIsReadingFromRadioChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusyOverlayVisible));
        OnPropertyChanged(nameof(BusyOverlayMessage));
    }

    /// <summary>Called once from platform-specific startup code (currently
    /// only the Desktop head - see AnyToneCPS.Desktop/Program.cs and
    /// Views/MainWindow.axaml.cs) once a real IRadioConnection is available.
    /// On platforms that never call this (Android/iOS/Browser), the Radio
    /// tab stays visible but reports itself unavailable.</summary>
    public void SetRadioServices(Func<IRadioConnection> connectionFactory, Func<IReadOnlyList<string>>? portLister)
    {
        _radioConnectionFactory = connectionFactory;
        _portLister = portLister;
        OnPropertyChanged(nameof(IsRadioAvailableOnThisPlatform));
        RefreshRadioPorts();

        // The very first scan can race the OS/udev still enumerating the
        // USB serial device right after app launch - this is why "Read from
        // Radio" was sometimes disabled at startup with a restart fixing it
        // (nothing to do with the radio itself rebooting). Retry a few times
        // with a short delay if the first scan came back empty, rather than
        // requiring the user to notice and click "Refresh" themselves.
        if (AvailablePorts.Count == 0)
        {
            _ = RetryPortScanAsync();
        }
    }

    private async Task RetryPortScanAsync()
    {
        for (var attempt = 0; attempt < 5 && AvailablePorts.Count == 0; attempt++)
        {
            await Task.Delay(1000);
            RefreshRadioPorts();
        }
    }

    [RelayCommand]
    private void RefreshRadioPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in _portLister?.Invoke() ?? [])
        {
            AvailablePorts.Add(port);
        }

        if (SelectedPort is null || !AvailablePorts.Contains(SelectedPort))
        {
            SelectedPort = AvailablePorts.FirstOrDefault();
        }

        ReadFromRadioCommand.NotifyCanExecuteChanged();
        // Found live 2026-07-19: this command's CanExecute is never
        // re-evaluated anywhere else either (not here, not in
        // SetRadioServices, not in OnIsVerifyingRoundtripChanged despite
        // that handler refreshing every other radio command) - it was
        // permanently stuck at its initial (false) construction-time
        // evaluation, i.e. always disabled regardless of actual state.
        VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();
        // Found live 2026-08-24 on Android: CanWriteChangesToRadio depends
        // on the exact same _radioConnectionFactory/SelectedPort state as
        // CanReadFromRadio, but was missing from this list - the first USB
        // scan on Android often comes back empty right after launch (see
        // RetryPortScanAsync's own doc comment), so Write to Radio stayed
        // permanently disabled even after a later retry found the port and
        // set SelectedPort, because nothing ever told the command to
        // re-check.
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanReadFromRadio() =>
        !IsReadingFromRadio
        && !IsWritingToRadio
        && !IsVerifyingRoundtrip
        && _radioConnectionFactory is not null
        && !string.IsNullOrWhiteSpace(SelectedPort);

    [RelayCommand(CanExecute = nameof(CanReadFromRadio))]
    private async Task ReadFromRadioAsync()
    {
        if (_radioConnectionFactory is null || string.IsNullOrWhiteSpace(SelectedPort))
        {
            return;
        }

        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            RadioReadStatusText = "Read cancelled";
            return;
        }

        var readOptions = new RadioIncludeOptionsRequest
        {
            IncludeDigitalContactList = IncludeDigitalContactList,
            IncludeEncryptionKeys = IncludeEncryptionKeysList
        };
        if (!await _storagePicker.ShowReadOptionsDialogAsync(readOptions))
        {
            RadioReadStatusText = "Read cancelled";
            return;
        }

        IncludeDigitalContactList = readOptions.IncludeDigitalContactList;
        IncludeEncryptionKeysList = readOptions.IncludeEncryptionKeys;

        IsReadingFromRadio = true;
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();
        RadioReadWarnings.Clear();
        // Safety-critical, added 2026-07-30 - see
        // OptionalSettingsEntry.IsVoxOn's doc comment for the hazard. Checks
        // the CURRENTLY loaded settings (from a prior read or project), not
        // the radio's actual live state - a best-effort heads-up, not a
        // guarantee, since the radio may have been reconfigured since.
        if (OptionalSettings.IsVoxOn)
        {
            RadioReadWarnings.Add("WARNING: VOX is on in the currently loaded settings. If that matches the radio, it can start transmitting on its own while connected for programming, which can damage the PC.");
        }

        RadioReadProgressCurrent = 0;
        RadioReadProgressTotal = 0;
        RadioReadStatusText = "Connecting...";
        // A stale "Write verified: ..."/"Write failed: ..." from a previous
        // write must not keep showing next to this new read's own status.
        RadioWriteStatusText = "";

        // IProgress<T>-typed (not `var`) - Progress<T> implements
        // IProgress<T>.Report explicitly, so calling .Report(...) directly
        // below (rather than only ever handing this to a method that takes
        // it as IProgress<T>) needs the interface-typed reference.
        IProgress<RadioReadProgress> progress = new Progress<RadioReadProgress>(p =>
        {
            RadioReadStatusText = p.Message;
            RadioReadProgressCurrent = p.Current;
            RadioReadProgressTotal = p.Total;
        });

        try
        {
            var portName = SelectedPort;
            var includeDigitalContacts = IncludeDigitalContactList;
            var includeEncryptionKeys = IncludeEncryptionKeysList;
            var (result, rawSnapshot, rawSnapshotError) = await Task.Run(() =>
            {
                var connection = _radioConnectionFactory();
                var warnings = new List<string>();
                void OnWarning(string message) => warnings.Add(message);

                connection.Warning += OnWarning;
                try
                {
                    // One open session for both the decoded read AND the raw
                    // snapshot capture (needed for a later Write) - these
                    // used to be two separate RadioCodeplugReader.Read /
                    // RadioCodeplugRawSnapshotReader.Capture calls, each
                    // opening and closing its own session. Since the radio
                    // reboots/re-enumerates its USB after EVERY session
                    // close, that cost a full extra reboot-and-reopen wait
                    // for no reason - both walks read the exact same set of
                    // addresses anyway (see
                    // RadioCodeplugReader.ReadFromOpenConnection's doc
                    // comment). Fixed 2026-08-01.
                    if (!RadioWriteVerification.TryOpenInitial(connection, portName, new Progress<string>(message => progress.Report(new RadioReadProgress(message, 0, 1))), out var openError))
                    {
                        return (new RadioCodeplugReadResult { Success = false, Error = $"Could not open port '{portName}': {openError}", Warnings = warnings }, (RadioCodeplugRawSnapshot?)null, (string?)null);
                    }

                    progress.Report(new RadioReadProgress("Identifying radio...", 0, 1));
                    var identity = connection.Identify();
                    if (!identity.IsRecognizedD890UV)
                    {
                        return (new RadioCodeplugReadResult
                        {
                            Success = false,
                            Error = $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.",
                            Identity = identity,
                            Warnings = warnings
                        }, (RadioCodeplugRawSnapshot?)null, (string?)null);
                    }

                    var readResult = RadioCodeplugReader.ReadFromOpenConnection(connection, identity, warnings, progress, includeDigitalContacts, includeEncryptionKeys);
                    if (!readResult.Success)
                    {
                        return (readResult, (RadioCodeplugRawSnapshot?)null, (string?)null);
                    }

                    // Best-effort, same as before the merge: a transient
                    // failure here shouldn't throw away an otherwise-
                    // successful decoded read.
                    try
                    {
                        var snapshot = RadioCodeplugRawSnapshotReader.CaptureFromOpenConnection(connection);
                        return (readResult, snapshot, (string?)null);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return (readResult, (RadioCodeplugRawSnapshot?)null, ex.Message);
                    }
                }
                finally
                {
                    connection.Warning -= OnWarning;
                    connection.Close();
                }
            });

            foreach (var warning in result.Warnings)
            {
                RadioReadWarnings.Add(warning);
            }

            if (!result.Success)
            {
                RadioReadStatusText = AppendVoxHint(result.Error ?? "Read failed");
                RadioIdentitySummary = result.Identity is { } badIdentity
                    ? $"Radio reported model='{badIdentity.Model}' version='{badIdentity.Version}'"
                    : null;
                DeviceModel = null;
                DeviceVersion = null;
                DeviceLocalInfo = null;
                return;
            }

            RadioIdentitySummary = $"D890UV, firmware {result.Identity!.Version}";
            DeviceModel = "D890UV";
            DeviceVersion = result.Identity.Version;
            DeviceLocalInfo = result.LocalInfo;
            ApplyRadioReadResult(result, includeDigitalContacts, includeEncryptionKeys);
            _cachedRadioSnapshot = rawSnapshot;
            RadioReadStatusText = rawSnapshot is not null
                ? $"Read complete: {result.Channels.Count(c => !c.IsBlank)} channels, {result.Zones.Count} zones"
                : $"Read complete: {result.Channels.Count(c => !c.IsBlank)} channels, {result.Zones.Count} zones (write support unavailable this session: {rawSnapshotError})";
            WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            RadioReadStatusText = AppendVoxHint($"Read failed: {exception.Message}");
        }
        finally
        {
            IsReadingFromRadio = false;
            ReadFromRadioCommand.NotifyCanExecuteChanged();
            WriteChangesToRadioCommand.NotifyCanExecuteChanged();
            VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();
        }
    }

    internal void ApplyRadioReadResult(RadioCodeplugReadResult result, bool includeDigitalContacts, bool includeEncryptionKeys)
    {
        var talkgroupNames = RadioReadMapper.BuildTalkgroupNameLookup(result);
        var radioIdNames = RadioReadMapper.BuildRadioIdNameLookup(result);
        var receiveGroupNames = RadioReadMapper.BuildReceiveGroupNameLookup(result);

        var channels = RadioReadMapper.MapChannels(result);
        foreach (var channel in channels)
        {
            channel.ContactDisplayName = RadioReadMapper.ResolveContactName(channel, talkgroupNames);
            channel.RadioIdDisplayName = RadioReadMapper.ResolveRadioIdName(channel, radioIdNames);
            channel.ReceiveGroupListDisplayName = RadioReadMapper.ResolveReceiveGroupListName(channel, receiveGroupNames);
        }

        var channelsByRadioIndex = result.Channels
            .Where(c => !c.IsBlank)
            .Zip(channels, (decoded, entry) => (decoded.Index, entry))
            .ToDictionary(pair => pair.Index, pair => pair.entry);

        var zones = RadioReadMapper.MapZones(result, channelsByRadioIndex);
        var radioIds = RadioReadMapper.MapRadioIds(result);
        var talkgroups = RadioReadMapper.MapTalkgroups(result);
        var scanLists = RadioReadMapper.MapScanLists(result, channelsByRadioIndex);
        var roamingChannels = RadioReadMapper.MapRoamingChannels(result);
        // Same shape as channelsByRadioIndex above - MapRoamingChannels
        // filters/orders the same way, so zipping against the raw decoded
        // list aligns 1:1.
        var roamingChannelsByRadioIndex = result.RoamingChannels
            .Where(r => r.RxFrequencyMhz > 0 || !string.IsNullOrWhiteSpace(r.Name))
            .Zip(roamingChannels, (decoded, entry) => (decoded.Index, entry))
            .ToDictionary(pair => pair.Index, pair => pair.entry);
        var roamingZones = RadioReadMapper.MapRoamingZones(result, roamingChannelsByRadioIndex);
        var receiveGroupLists = RadioReadMapper.MapReceiveGroupLists(result);
        var autoRepeaterOffsets = RadioReadMapper.MapAutoRepeaterOffsets(result);
        var analogAddresses = RadioReadMapper.MapAnalogAddresses(result);
        var gpsRoamingEntries = RadioReadMapper.MapGpsRoaming(result);
        var zoneNames = RadioReadMapper.BuildZoneNameLookup(result);
        foreach (var gpsRoaming in gpsRoamingEntries)
        {
            gpsRoaming.ZoneDisplayName = RadioReadMapper.ResolveZoneName(gpsRoaming.ZoneIndex, zoneNames);
        }
        var talkgroupWhitelist = RadioReadMapper.MapTalkgroupWhitelist(result);
        var digitalContactWhitelist = RadioReadMapper.MapDigitalContactWhitelist(result);
        var aprsReceiveFilters = RadioReadMapper.MapAprsReceiveFilters(result);
        var prefabricatedSms = RadioReadMapper.MapPrefabricatedSms(result);
        var amAirChannels = RadioReadMapper.MapAmAir(result);
        // Same shape as channelsByRadioIndex above - MapAmAir filters the
        // VFO slot and blanks in the same order MapAmAir itself does, so
        // zipping against the raw decoded list aligns 1:1.
        var amAirChannelsByRadioIndex = result.AmAirChannels
            .Where(a => a.Index != AmAirCodec.VfoIndex && a.FrequencyMHz > 0)
            .Zip(amAirChannels, (decoded, entry) => (decoded.Index, entry))
            .ToDictionary(pair => pair.Index, pair => pair.entry);
        var amZones = RadioReadMapper.MapAmZones(result, amAirChannelsByRadioIndex);
        var fmChannels = RadioReadMapper.MapFmChannels(result);
        var digitalContacts = RadioReadMapper.MapDigitalContacts(result);
        var analogQuickCalls = RadioReadMapper.MapAnalogQuickCalls(result);
        var stateInformationEntries = RadioReadMapper.MapStateInformation(result);
        var hotKeys = RadioReadMapper.MapHotKeys(result);
        var qdc1200Ids = RadioReadMapper.MapQdc1200Ids(result);
        var qdcAddresses = RadioReadMapper.MapQdcAddresses(result);
        var fiveToneIds = RadioReadMapper.MapFiveToneIds(result);
        var twoToneEncodeEntries = RadioReadMapper.MapTwoToneEncodeEntries(result);
        var twoToneDecodeEntries = RadioReadMapper.MapTwoToneDecodeEntries(result);
        var dtmfEncodeEntries = RadioReadMapper.MapDtmfEncodeEntries(result);

        ReplaceChannels(channels);
        RebindZonesToChannels();
        ReplaceZones(zones);

        ReplaceCollection(RadioIds, radioIds);
        ReplaceCollection(Talkgroups, talkgroups);
        ReplaceCollection(ScanLists, scanLists);
        ReplaceCollection(RoamingChannels, roamingChannels);
        ReplaceCollection(RoamingZones, roamingZones);
        ReplaceCollection(ReceiveGroupLists, receiveGroupLists);
        ReplaceCollection(AutoRepeaterOffsets, autoRepeaterOffsets);
        ReplaceCollection(AnalogAddresses, analogAddresses);
        ReplaceCollection(GpsRoamingEntries, gpsRoamingEntries);
        // ReadGpsRoaming always decodes all 32 fixed slots unconditionally,
        // so this is a defense-in-depth no-op in practice - see
        // EnsureGpsRoamingSlotsPresent's own doc comment.
        EnsureGpsRoamingSlotsPresent();
        ReplaceCollection(TalkgroupWhitelist, talkgroupWhitelist);
        ReplaceCollection(DigitalContactWhitelist, digitalContactWhitelist);
        ReplaceCollection(PrefabricatedSmsMessages, prefabricatedSms);
        ReplaceCollection(AmAirChannels, amAirChannels);
        ReplaceCollection(AmZones, amZones);
        ReplaceCollection(FmChannels, fmChannels);
        ReplaceCollection(AprsReceiveFilters, aprsReceiveFilters);

        // Digital Contacts are opt-in (IncludeDigitalContactList, off by
        // default) - same reasoning as the Encryption Keys fix right below,
        // and the same bug: a SKIPPED read must leave DigitalContacts
        // completely untouched rather than wiping it to empty. CONFIRMED
        // 2026-08-16: the old unconditional ReplaceCollection was silently
        // discarding real contact data an earlier read this session had
        // already loaded, on every subsequent non-contacts read - this is
        // the exact same bug class already fixed for Encryption Keys on
        // 2026-07-20, just never applied here too. See
        // ReadFromRadioSkippingDigitalContactsLeavesAnEarlierReadListUntouched
        // in the test project for the regression test.
        if (includeDigitalContacts)
        {
            ReplaceCollection(DigitalContacts, digitalContacts);
            // Freshly-read data exactly matches the radio - see
            // _digitalContactsDirty's own doc comment for why this must NOT
            // default to "pending write" the way other entities' snapshots do.
            _digitalContactsDirty = false;
            _digitalContactsGenuinelyPopulatedFromRadio = true;
            OnPropertyChanged(nameof(CanIncludeDigitalContactsInWrite));
        }
        ReplaceCollection(AnalogQuickCalls, analogQuickCalls);
        ReplaceCollection(StateInformationEntries, stateInformationEntries);
        ReplaceCollection(HotKeys, hotKeys);
        ReplaceCollection(Qdc1200Ids, qdc1200Ids);
        ReplaceCollection(QdcAddresses, qdcAddresses);
        ReplaceCollection(FiveToneIds, fiveToneIds);
        ReplaceCollection(TwoToneEncodeEntries, twoToneEncodeEntries);
        ReplaceCollection(TwoToneDecodeEntries, twoToneDecodeEntries);

        RadioReadMapper.ApplyQdc1200Settings(result, Qdc1200Settings);
        RadioReadMapper.ApplyFiveToneSettings(result, FiveToneSettings);
        RadioReadMapper.ApplyTwoToneEncodeSettings(result, TwoToneEncodeSettings);

        // DtmfSettings applied BEFORE the M1-M16 Code values below,
        // deliberately - setting Self ID/Interval Character here fires
        // MainViewModel.Dtmf.cs's own recompose-on-change subscription for
        // any already-configured slot, which would otherwise overwrite the
        // read's own real Code values applied next. The M1-M16 update
        // always wins, matching what's actually on the radio.
        RadioReadMapper.ApplyDtmfSettings(result, DtmfSettings);

        // DTMF's M1-M16 is a fixed set (see EnsureDtmfEncodeSlotsPresent) -
        // deliberately NOT ReplaceCollection, which would drop the
        // per-entry PropertyChanged subscription wired at slot-creation
        // time (needed for Other Side ID -> compose-on-change, both
        // Desktop popup and Mobile inline editing). Update Code in place
        // on the existing, already-wired entries instead.
        EnsureDtmfEncodeSlotsPresent();
        foreach (var value in dtmfEncodeEntries)
        {
            var target = DtmfEncodeEntries.FirstOrDefault(e => e.Number == value.Number);
            if (target is not null)
            {
                target.Code = value.Code;
            }
        }
        RefreshFilteredDigitalContacts();
        NotifyAllEntityCounts();

        // Encryption keys are opt-in (IncludeEncryptionKeysList, off by
        // default - see its doc comment) - unlike every other entity here,
        // a SKIPPED read must leave EncryptionKeys/Arc4EncryptionKeys/
        // AesEncryptionKeys completely untouched rather than wiping them to
        // empty-then-backfilled-defaults. Found 2026-07-20 while adding
        // radio-write support for these lists: the old unconditional
        // ReplaceCollection+EnsureEncryptionKeySlotsPresent below silently
        // reset any keys already in memory (from an earlier opt-in read,
        // Generate, or manual edit this session) back to "Off" on every
        // subsequent channel-only read - harmless before write support
        // existed, but would have meant a later Write blasting "Off" over
        // real on-radio keys that were simply never re-read. See
        // EncryptionKeyEntry's class doc comment for the full reasoning.
        if (includeEncryptionKeys)
        {
            var aesEncryptionKeys = RadioReadMapper.MapAesEncryptionKeys(result);
            var arc4EncryptionKeys = RadioReadMapper.MapArc4EncryptionKeys(result);
            var basicEncryptionCodes = RadioReadMapper.MapBasicEncryptionCodes(result);

            ReplaceCollection(EncryptionKeys, basicEncryptionCodes);
            ReplaceCollection(Arc4EncryptionKeys, arc4EncryptionKeys);
            ReplaceCollection(AesEncryptionKeys, aesEncryptionKeys);
            // Backfill every slot the read didn't find a real key for back
            // to its default "Off" state, matching the vendor CPS always
            // showing every slot.
            EnsureEncryptionKeySlotsPresent();
            OnPropertyChanged(nameof(DigitalEncryptionKeyOptions));
            OnPropertyChanged(nameof(AesEncryptionKeyOptions));
            OnPropertyChanged(nameof(Arc4EncryptionKeyOptions));

            // A genuine read confirms these ARE the radio's current values -
            // same reasoning as the Channel/Zone/ScanList sync loops below,
            // just gated on the opt-in flag.
            foreach (var key in EncryptionKeys)
            {
                key.MarkRadioSynced();
            }

            foreach (var key in Arc4EncryptionKeys)
            {
                key.MarkRadioSynced();
            }

            foreach (var key in AesEncryptionKeys)
            {
                key.MarkRadioSynced();
            }
        }

        RadioReadMapper.ApplyMasterId(result, MasterId);
        RadioReadMapper.ApplyTalkAliasSettings(result, TalkAliasSettings);
        TalkAliasSettings.MarkRadioSynced();
        RadioReadMapper.ApplyAlarmSettings(result, AlarmSettings);
        AlarmSettings.MarkRadioSynced();
        RadioReadMapper.ApplyAprsSettings(result, AprsSettings);
        // A genuine read confirms these ARE the radio's current values -
        // same reasoning as TalkAliasSettings/AlarmSettings right above.
        // Missing until 2026-08-16 (found while testing the always-allow-
        // write change) - without this, AprsSettings.HasAnyPendingRadioWrite
        // was permanently stuck true after every read, regardless of
        // whether the user changed anything. Cascades to
        // AdditionalFixLocations/DigitalReports - see
        // AprsSettingsEntry.MarkRadioSynced's own doc comment.
        AprsSettings.MarkRadioSynced();
        RadioReadMapper.ApplyOptionalSettings(result, OptionalSettings);
        // A genuine read confirms the Power-on/Alert Tone tabs' fields ARE
        // the radio's current values - same reasoning as the Channel/Zone/
        // ScanList sync loops below. Marks all 25 AlertTones entries synced
        // (all 5 categories are write-safe now, but this call is harmless
        // either way since a read reflects the true radio state regardless
        // of which ones have a write path).
        OptionalSettings.MarkRadioSynced();
        foreach (var tone in OptionalSettings.AlertTones)
        {
            tone.MarkRadioSynced();
        }

        SelectedChannel = Channels.FirstOrDefault();
        SelectedZone = Zones.FirstOrDefault();
        AvailableZoneChannel = Channels.FirstOrDefault();
        _currentProjectStorage = null;
        CurrentProjectLocation = "";

        // Reset per-field dirty snapshots (freshly-read values shouldn't show
        // as individually edited), but the read itself IS unsaved data - no
        // project file reflects it yet - so Save/Save As must stay enabled,
        // not follow MarkProjectClean's usual "nothing to save" meaning.
        MarkProjectClean();
        _projectStructureDirty = true;
        NotifyDirtyStateChanged();

        // Independently establish the radio-write baseline: these values
        // ARE what the radio currently has, so nothing is "pending write"
        // right after a read - separate from the file-save tracking above,
        // see ChannelEntry's _radioSyncSnapshot doc comment.
        foreach (var channel in Channels)
        {
            channel.MarkRadioSynced();
        }

        foreach (var zone in Zones)
        {
            zone.MarkRadioSynced();
        }

        foreach (var scanList in ScanLists)
        {
            scanList.MarkRadioSynced();
        }

        foreach (var amAir in AmAirChannels)
        {
            amAir.MarkRadioSynced();
        }

        foreach (var amZone in AmZones)
        {
            amZone.MarkRadioSynced();
        }

        foreach (var sms in PrefabricatedSmsMessages)
        {
            sms.MarkRadioSynced();
        }

        foreach (var fmChannel in FmChannels)
        {
            fmChannel.MarkRadioSynced();
        }

        foreach (var autoRepeaterOffset in AutoRepeaterOffsets)
        {
            autoRepeaterOffset.MarkRadioSynced();
        }

        foreach (var analogAddress in AnalogAddresses)
        {
            analogAddress.MarkRadioSynced();
        }

        foreach (var gpsRoaming in GpsRoamingEntries)
        {
            gpsRoaming.MarkRadioSynced();
        }

        foreach (var qdc1200Id in Qdc1200Ids)
        {
            qdc1200Id.MarkRadioSynced();
        }

        Qdc1200Settings.MarkRadioSynced();

        foreach (var analogQuickCall in AnalogQuickCalls)
        {
            analogQuickCall.MarkRadioSynced();
        }

        foreach (var stateInformation in StateInformationEntries)
        {
            stateInformation.MarkRadioSynced();
        }

        foreach (var hotKey in HotKeys)
        {
            hotKey.MarkRadioSynced();
        }

        foreach (var qdcAddress in QdcAddresses)
        {
            qdcAddress.MarkRadioSynced();
        }

        foreach (var fiveToneId in FiveToneIds)
        {
            fiveToneId.MarkRadioSynced();
        }

        FiveToneSettings.MarkRadioSynced();

        foreach (var twoToneEncodeEntry in TwoToneEncodeEntries)
        {
            twoToneEncodeEntry.MarkRadioSynced();
        }

        foreach (var twoToneDecodeEntry in TwoToneDecodeEntries)
        {
            twoToneDecodeEntry.MarkRadioSynced();
        }

        TwoToneEncodeSettings.MarkRadioSynced();

        foreach (var dtmfEncodeEntry in DtmfEncodeEntries)
        {
            dtmfEncodeEntry.MarkRadioSynced();
        }

        DtmfSettings.MarkRadioSynced();

        foreach (var radioId in RadioIds)
        {
            radioId.MarkRadioSynced();
        }

        MasterId.MarkRadioSynced();

        foreach (var talkgroup in Talkgroups)
        {
            talkgroup.MarkRadioSynced();
        }

        foreach (var roamingChannel in RoamingChannels)
        {
            roamingChannel.MarkRadioSynced();
        }

        foreach (var roamingZone in RoamingZones)
        {
            roamingZone.MarkRadioSynced();
        }

        foreach (var talkgroupWhitelistEntry in TalkgroupWhitelist)
        {
            talkgroupWhitelistEntry.MarkRadioSynced();
        }

        _talkgroupWhitelistSyncedCount = TalkgroupWhitelist.Count;

        foreach (var digitalContactWhitelistEntry in DigitalContactWhitelist)
        {
            digitalContactWhitelistEntry.MarkRadioSynced();
        }

        _digitalContactWhitelistSyncedCount = DigitalContactWhitelist.Count;

        // Any pending channel/zone/scan list/AM Air/AM Zone/prefabricated
        // SMS/FM channel/Auto Repeater Offset/Analog Address/QDC 1200 ID/
        // Analog Quick Call/State Information/QDC Address deletion was
        // based on a now-stale view of the radio - this fresh read is the
        // new baseline, so a deletion the user asked for before this read
        // but never wrote must not resurface as a surprise deletion after
        // some later, unrelated write. See _pendingDeleteRadioIndices's
        // doc comment.
        _pendingDeleteRadioIndices.Clear();
        _pendingDeleteZoneRadioIndices.Clear();
        _pendingDeleteScanListRadioIndices.Clear();
        _pendingDeleteAmAirRadioIndices.Clear();
        _pendingDeleteAmZoneRadioIndices.Clear();
        _pendingDeletePrefabricatedSmsIndices.Clear();
        _pendingDeleteFmChannelRadioIndices.Clear();
        _pendingDeleteAutoRepeaterOffsetIndices.Clear();
        _pendingDeleteAnalogAddressRadioIndices.Clear();
        _pendingDeleteQdc1200IdIndices.Clear();
        _pendingDeleteAnalogQuickCallIndices.Clear();
        _pendingDeleteStateInformationIndices.Clear();
        _pendingDeleteQdcAddressIndices.Clear();
        _pendingDeleteFiveToneIdIndices.Clear();
        _pendingDeleteTwoToneEncodeIndices.Clear();
        _pendingDeleteTwoToneDecodeIndices.Clear();
        _pendingDeleteRadioIdIndices.Clear();
        _pendingDeleteTalkgroupIndices.Clear();
        _pendingDeleteRoamingChannelIndices.Clear();
        _pendingDeleteRoamingZoneIndices.Clear();

        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        RefreshValidationAndPreview();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    /// <summary>Appends a VOX-aware hint to a connection-failure message -
    /// safety-critical, added 2026-07-30, see
    /// OptionalSettingsEntry.IsVoxOn's doc comment for the hazard. Checks
    /// the CURRENTLY loaded settings (from a prior read or project), not
    /// the radio's actual live state - a best-effort heads-up, not a
    /// guarantee, since the radio may have been reconfigured since. Shared
    /// with MainViewModel.RadioWrite.cs's own write-failure handling.</summary>
    private string AppendVoxHint(string message) =>
        OptionalSettings.IsVoxOn
            ? $"{message} (VOX is on in the currently loaded settings - if that matches the radio, it may be transmitting and disrupting the connection. Consider turning VOX off and retrying.)"
            : message;
}
