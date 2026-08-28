using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
/// Radio WRITE concerns - deliberately narrow on WHICH fields can be edited:
/// only the ones <see cref="ChannelCodec.Encode"/> exposes as safe (Name,
/// RX/TX frequency, CTCSS/DCS mode, Squelch Mode, Optional Signal,
/// Busy-Lock/TX-Permit, Contact/Talk Group, Radio ID, Receive Group List,
/// PTT ID, Channel Type, Transmit Power, Bandwidth, Talk Around, Call
/// Confirmation, PTT Prohibit, Reverse, RX/TX Color Code, Work Alone, Slot
/// Suit, Slot, SMS Confirmation, AES/ARC4/Digital Key, Auto Scan, Scramble
/// Type, Custom Scrambler Frequency, Correct Frequency Hz, Custom CTCSS,
/// DMR Mode).
/// But NOT narrow on what gets written to the radio -
/// see <see cref="RadioCodeplugWriter"/>'s doc comment: a narrow single-
/// channel write was found (2026-07-18, twice independently across this
/// project) to silently erase neighboring flash sharing the same physical
/// erase block, so every write patches whichever channels changed
/// (<see cref="RadioCodeplugPatcher"/>) into a full codeplug snapshot and
/// writes everything back together (<see cref="RadioCodeplugWriter"/>) -
/// matching the vendor CPS's own proven-safe behavior.
///
/// The snapshot itself is NOT re-captured before every write -
/// <see cref="MainViewModel.Radio._cachedRadioSnapshot"/> (populated by the
/// last successful Read From Radio or Write) is reused directly, matching
/// the vendor CPS's own `Device::writeOtherData()`, which never re-reads
/// before writing either. If no read has happened yet this session,
/// <c>WriteChangesToRadioAsync</c> captures one itself right before
/// patching (<see cref="Services.Radio.RadioCodeplugRawSnapshotReader.Capture"/>)
/// WITHOUT applying it to the live ViewModel - decided 2026-08-16,
/// replacing an earlier hard requirement that a full Read From Radio (which
/// overwrites the live view with the radio's own data) happen first, which
/// destroyed any codeplug prepared in the app before ever reading. Either
/// way, a write can only ever be as fresh as its baseline; something else
/// changing the radio in between (front panel, another program) would be
/// silently overwritten by the next write, same as it would be in the
/// vendor CPS.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty] private bool _isWritingToRadio;
    [ObservableProperty] private string _radioWriteStatusText = "";
    public ObservableCollection<string> RadioWriteWarnings { get; } = [];

    /// <summary>Copies the status text plus every warning line as one block
    /// of text - each warning renders as its own SelectableTextBlock (so a
    /// mismatch list with hundreds of lines stays readable), which limits
    /// plain text selection to one line at a time. This is the only way to
    /// get the whole message out in one action.</summary>
    [RelayCommand]
    private async Task CopyRadioWriteWarningsAsync()
    {
        var text = string.Join(Environment.NewLine, new[] { RadioWriteStatusText }.Concat(RadioWriteWarnings));
        await _storagePicker.CopyToClipboardAsync(text);
    }

    partial void OnIsWritingToRadioChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusyOverlayVisible));
        OnPropertyChanged(nameof(BusyOverlayMessage));
    }

    /// <summary>Maximum number of per-channel lines shown in the write
    /// confirmation dialog before truncating to a "+N more" summary - a
    /// batch of many edited channels shouldn't produce an unreadable wall
    /// of text.</summary>
    private const int MaxWriteSummaryLines = 15;

    /// <summary>Radio indices (0-based) the user deleted from <see cref="Channels"/>
    /// this session but hasn't written to the radio yet. Deleting a channel
    /// only removes its <see cref="ChannelEntry"/> from the in-memory list -
    /// there is no object left to carry an <c>IsXxxPendingRadioWrite</c>
    /// flag, so without this separate set, a delete would neither enable
    /// Write-to-Radio nor actually reach the radio (found live 2026-07-19:
    /// the deleted channel silently reappeared after the next Read From
    /// Radio). Cleared (per-index) on a successful write. If a channel's
    /// Number is reused before the next write (a new/duplicated channel, or
    /// a direct edit), <see cref="WriteChangesToRadioAsync"/> excludes that
    /// index from the delete set at write time - the reused channel's own
    /// field patch correctly overwrites the slot instead of it being
    /// blanked.</summary>
    private readonly HashSet<int> _pendingDeleteRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// zones (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteZoneRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// scan lists (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteScanListRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Radio IDs (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteRadioIdIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Talkgroups (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteTalkgroupIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Receive Group Lists (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteReceiveGroupListIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Roaming Channels (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteRoamingChannelIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Roaming Zones (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteRoamingZoneIndices = [];

    /// <summary>Remembered write-dialog choice - see RadioIncludeOptionsRequest's
    /// own doc comment for why this is independent from
    /// MainViewModel.Radio.cs's own IncludeDigitalContactList/
    /// IncludeEncryptionKeysList (those are the READ-side remembered
    /// choice). Both default false, matching the vendor CPS's own default.
    /// Bindable (not a plain private field) so Mobile's own write-side
    /// "include options" popup - added 2026-08-16 alongside actually
    /// enabling Write to Radio on Mobile - has something to bind its own
    /// checkboxes to; Desktop's modal dialog sets these through
    /// <see cref="RadioIncludeOptionsRequest"/> instead (see
    /// WriteChangesToRadioAsync's own use of both), so this property is
    /// read there but not written from that path.</summary>
    [ObservableProperty] private bool _writeIncludeDigitalContactList;
    [ObservableProperty] private bool _writeIncludeEncryptionKeys;

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// AM Air channels (0-based radio indices). The mandatory VFO row
    /// (<see cref="AmAirCodec.VfoIndex"/>) can never appear here - see
    /// <see cref="CanRemoveSelectedAmAir"/>.</summary>
    private readonly HashSet<int> _pendingDeleteAmAirRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// AM Zones (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteAmZoneRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Prefabricated SMS (0-based slot ids).</summary>
    private readonly HashSet<int> _pendingDeletePrefabricatedSmsIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// FM broadcast channels (0-based radio indices). The always-present
    /// "home" slot (<see cref="FmChannelCodec.HomeIndex"/>) can never appear
    /// here - see <see cref="CanRemoveSelectedFmChannel"/>.</summary>
    private readonly HashSet<int> _pendingDeleteFmChannelRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Auto Repeater Offsets (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteAutoRepeaterOffsetIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Analog Address Book entries (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteAnalogAddressRadioIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// QDC 1200 ID table entries (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteQdc1200IdIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// 5Tone ID table entries (0-based radio indices). A deleted row whose
    /// own Number fell within D890UvMemoryMap.FiveToneInfoIdSlotCount also
    /// gets its own Information ID slot cleared at write time (see
    /// WriteChangesToRadioAsync) - no separate tracking set needed for
    /// that, it's derived from this same delete set.</summary>
    private readonly HashSet<int> _pendingDeleteFiveToneIdIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// 2Tone Encode table entries (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteTwoToneEncodeIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// 2Tone Decode table entries (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteTwoToneDecodeIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// Analog Quick Call slots (0-based radio indices). No equivalent set
    /// exists for Hot Key itself - its 18 rows are a fixed named list, never
    /// added/removed (see HotKeyEntry's class doc comment).</summary>
    private readonly HashSet<int> _pendingDeleteAnalogQuickCallIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// State Information slots (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteStateInformationIndices = [];

    /// <summary>Same purpose as <see cref="_pendingDeleteRadioIndices"/>, for
    /// QDC Address Book entries (0-based radio indices).</summary>
    private readonly HashSet<int> _pendingDeleteQdcAddressIndices = [];

    // Always available once connected, matching the vendor CPS's own
    // "Write to Radio" behavior - decided 2026-08-16, replacing the long
    // dirty-flag check this method used to have (every HasAnySafeFieldDirty/
    // HasAnyPendingRadioWrite/pending-delete/synced-count check across every
    // entity). Removing that gate doesn't change WHAT gets patched - every
    // BuildXValues/Encode/ApplyXPatch call below is a pure function of live
    // model state, so a field that hasn't actually changed just gets the
    // same bytes written back (a genuine no-op RMW round trip, not a blind
    // rewrite). A genuine Read From Radio is no longer required first
    // either (also decided 2026-08-16) - WriteChangesToRadioAsync captures
    // its own RMW baseline directly from the radio if `_cachedRadioSnapshot`
    // is still null, without touching the live ViewModel, so a codeplug
    // prepared before ever reading isn't destroyed by a mandatory read.
    private bool CanWriteChangesToRadio() =>
        !IsReadingFromRadio
        && !IsWritingToRadio
        && !IsVerifyingRoundtrip
        && !HasBlockingValidationErrors
        && _radioConnectionFactory is not null
        && !string.IsNullOrWhiteSpace(SelectedPort);

    // Deliberately checks HasAnyPendingRadioWrite (independent of the file-
    // save dirty tracking used for the bold "unsaved" field highlighting) -
    // saving the project must NOT make Write-to-Radio forget a pending edit
    // it hasn't actually sent to the radio yet. See ChannelEntry's
    // _radioSyncSnapshot doc comment.
    private static bool HasAnySafeFieldDirty(ChannelEntry channel) => channel.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(ZoneEntry zone) => zone.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(ScanListEntry scanList) => scanList.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(RadioIdEntry radioId) => radioId.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(TalkgroupEntry talkgroup) => talkgroup.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(ReceiveGroupListEntry receiveGroupList) => receiveGroupList.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(RoamingChannelEntry roamingChannel) => roamingChannel.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(RoamingZoneEntry roamingZone) => roamingZone.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(TalkgroupWhitelistEntry entry) => entry.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(DigitalContactWhitelistEntry entry) => entry.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(AmAirEntry amAir) => amAir.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(AmZoneEntry amZone) => amZone.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(PrefabricatedSmsEntry sms) => sms.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(FmChannelEntry fmChannel) => fmChannel.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(AutoRepeaterOffsetEntry autoRepeaterOffset) => autoRepeaterOffset.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(AnalogAddressEntry analogAddress) => analogAddress.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(GpsRoamingEntry gpsRoaming) => gpsRoaming.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(Qdc1200IdEntry qdc1200Id) => qdc1200Id.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(AnalogQuickCallEntry analogQuickCall) => analogQuickCall.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(StateInformationEntry stateInformation) => stateInformation.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(HotKeyEntry hotKey) => hotKey.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(QdcAddressEntry qdcAddress) => qdcAddress.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(EncryptionKeyEntry key) => key.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(FiveToneIdEntry fiveToneId) => fiveToneId.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(TwoToneEncodeEntry twoToneEncodeEntry) => twoToneEncodeEntry.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(TwoToneDecodeEntry twoToneDecodeEntry) => twoToneDecodeEntry.HasAnyPendingRadioWrite;

    private static bool HasAnySafeFieldDirty(DtmfEncodeEntry dtmfEncodeEntry) => dtmfEncodeEntry.HasAnyPendingRadioWrite;

    /// <summary>
    /// Writes every channel with a pending write-safe field change to the
    /// radio in a SINGLE full-codeplug round trip, rather than one round
    /// trip per channel - each round trip already reads/rewrites the whole
    /// known codeplug regardless of how much changed (see class doc
    /// comment), so batching N edits together costs the same as writing
    /// just one, instead of N times as much.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanWriteChangesToRadio))]
    private async Task WriteChangesToRadioAsync()
    {
        if (_radioConnectionFactory is null || string.IsNullOrWhiteSpace(SelectedPort))
        {
            return;
        }

        var dirtyChannels = Channels.Where(HasAnySafeFieldDirty).ToList();
        // A channel's Number may have been reused (Add/DuplicateChannel) since
        // it was deleted - that slot gets a real field patch below instead,
        // not blanked.
        var deleteIndices = _pendingDeleteRadioIndices.Except(Channels.Select(c => c.Number - 1)).ToList();
        var dirtyZones = Zones.Where(HasAnySafeFieldDirty).ToList();
        var deleteZoneIndices = _pendingDeleteZoneRadioIndices.Except(Zones.Select(z => z.Number - 1)).ToList();
        var dirtyScanLists = ScanLists.Where(HasAnySafeFieldDirty).ToList();
        var deleteScanListIndices = _pendingDeleteScanListRadioIndices.Except(ScanLists.Select(s => s.Number - 1)).ToList();
        var dirtyAmAir = AmAirChannels.Where(HasAnySafeFieldDirty).ToList();
        var deleteAmAirIndices = _pendingDeleteAmAirRadioIndices.Except(AmAirChannels.Select(a => a.Number - 1)).ToList();
        var dirtyAmZones = AmZones.Where(HasAnySafeFieldDirty).ToList();
        var deleteAmZoneIndices = _pendingDeleteAmZoneRadioIndices.Except(AmZones.Select(z => z.Number - 1)).ToList();
        var dirtyPrefabricatedSms = PrefabricatedSmsMessages.Where(HasAnySafeFieldDirty).ToList();
        var deletePrefabricatedSmsIndices = _pendingDeletePrefabricatedSmsIndices.Except(PrefabricatedSmsMessages.Select(s => s.Number - 1)).ToList();
        var dirtyFmChannels = FmChannels.Where(HasAnySafeFieldDirty).ToList();
        var deleteFmChannelIndices = _pendingDeleteFmChannelRadioIndices.Except(FmChannels.Select(f => f.Number - 1)).ToList();
        var dirtyAutoRepeaterOffsets = AutoRepeaterOffsets.Where(HasAnySafeFieldDirty).ToList();
        var deleteAutoRepeaterOffsetIndices = _pendingDeleteAutoRepeaterOffsetIndices.Except(AutoRepeaterOffsets.Select(a => a.Number - 1)).ToList();
        var dirtyAnalogAddresses = AnalogAddresses.Where(HasAnySafeFieldDirty).ToList();
        var dirtyGpsRoaming = GpsRoamingEntries.Where(HasAnySafeFieldDirty).ToList();
        var deleteAnalogAddressIndices = _pendingDeleteAnalogAddressRadioIndices.Except(AnalogAddresses.Select(a => a.Number - 1)).ToList();
        var dirtyQdc1200Ids = Qdc1200Ids.Where(HasAnySafeFieldDirty).ToList();
        var deleteQdc1200IdIndices = _pendingDeleteQdc1200IdIndices.Except(Qdc1200Ids.Select(q => q.Number - 1)).ToList();
        var qdc1200SettingsDirty = Qdc1200Settings.HasAnyPendingRadioWrite;
        var dirtyAnalogQuickCalls = AnalogQuickCalls.Where(HasAnySafeFieldDirty).ToList();
        var deleteAnalogQuickCallIndices = _pendingDeleteAnalogQuickCallIndices.Except(AnalogQuickCalls.Select(a => a.Number - 1)).ToList();
        var dirtyStateInformation = StateInformationEntries.Where(HasAnySafeFieldDirty).ToList();
        var deleteStateInformationIndices = _pendingDeleteStateInformationIndices.Except(StateInformationEntries.Select(s => s.Number - 1)).ToList();
        // Hot Key's 18 rows are fixed/never removed (see HotKeyEntry's class
        // doc comment) - no delete set, and the radio index is the row's own
        // position in HotKeys rather than a Number-1 lookup.
        var dirtyHotKeys = HotKeys.Select((hotKey, radioIndex) => (HotKey: hotKey, RadioIndex: radioIndex)).Where(e => HasAnySafeFieldDirty(e.HotKey)).ToList();
        var dirtyQdcAddresses = QdcAddresses.Where(HasAnySafeFieldDirty).ToList();
        var deleteQdcAddressIndices = _pendingDeleteQdcAddressIndices.Except(QdcAddresses.Select(a => a.Number - 1)).ToList();
        var dirtyFiveToneIds = FiveToneIds.Where(HasAnySafeFieldDirty).ToList();
        var deleteFiveToneIdIndices = _pendingDeleteFiveToneIdIndices.Except(FiveToneIds.Select(f => f.Number - 1)).ToList();
        var fiveToneSettingsDirty = FiveToneSettings.HasAnyPendingRadioWrite;
        var dirtyTwoToneEncodeEntries = TwoToneEncodeEntries.Where(HasAnySafeFieldDirty).ToList();
        var deleteTwoToneEncodeIndices = _pendingDeleteTwoToneEncodeIndices.Except(TwoToneEncodeEntries.Select(e => e.Number - 1)).ToList();
        var dirtyTwoToneDecodeEntries = TwoToneDecodeEntries.Where(HasAnySafeFieldDirty).ToList();
        var deleteTwoToneDecodeIndices = _pendingDeleteTwoToneDecodeIndices.Except(TwoToneDecodeEntries.Select(e => e.Number - 1)).ToList();
        var twoToneEncodeSettingsDirty = TwoToneEncodeSettings.HasAnyPendingRadioWrite;
        var dirtyDtmfEncodeEntries = DtmfEncodeEntries.Where(HasAnySafeFieldDirty).ToList();
        var dtmfSettingsDirty = DtmfSettings.HasAnyPendingRadioWrite;
        var dirtyRadioIds = RadioIds.Where(HasAnySafeFieldDirty).ToList();
        var deleteRadioIdIndices = _pendingDeleteRadioIdIndices.Except(RadioIds.Select(r => r.Number - 1)).ToList();
        var masterIdDirty = MasterId.HasAnyPendingRadioWrite;
        var dirtyTalkgroups = Talkgroups.Where(HasAnySafeFieldDirty).ToList();
        var deleteTalkgroupIndices = _pendingDeleteTalkgroupIndices.Except(Talkgroups.Select(t => t.Number - 1)).ToList();
        var dirtyReceiveGroupLists = ReceiveGroupLists.Where(HasAnySafeFieldDirty).ToList();
        var deleteReceiveGroupListIndices = _pendingDeleteReceiveGroupListIndices.Except(ReceiveGroupLists.Select(g => g.Number - 1)).ToList();
        var dirtyRoamingChannels = RoamingChannels.Where(HasAnySafeFieldDirty).ToList();
        var deleteRoamingChannelIndices = _pendingDeleteRoamingChannelIndices.Except(RoamingChannels.Select(r => r.Number - 1)).ToList();
        var dirtyRoamingZones = RoamingZones.Where(HasAnySafeFieldDirty).ToList();
        var deleteRoamingZoneIndices = _pendingDeleteRoamingZoneIndices.Except(RoamingZones.Select(z => z.Number - 1)).ToList();
        // Whole-list rewrite, not per-record patches (see
        // TalkgroupWhitelistCodec's own doc comment) - a pure removal
        // leaves no per-entry HasAnyPendingRadioWrite trace, so the count
        // comparison against the last-synced count is what catches it.
        var talkgroupWhitelistDirty = TalkgroupWhitelist.Any(HasAnySafeFieldDirty) || TalkgroupWhitelist.Count != _talkgroupWhitelistSyncedCount;
        var digitalContactWhitelistDirty = DigitalContactWhitelist.Any(HasAnySafeFieldDirty) || DigitalContactWhitelist.Count != _digitalContactWhitelistSyncedCount;
        var dirtyDigitalCodes = EncryptionKeys.Where(HasAnySafeFieldDirty).ToList();
        var dirtyArc4Keys = Arc4EncryptionKeys.Where(HasAnySafeFieldDirty).ToList();
        var dirtyAesKeys = AesEncryptionKeys.Where(HasAnySafeFieldDirty).ToList();
        var optionalSettingsDirty = OptionalSettings.HasAnyPendingRadioWrite
            || OptionalSettings.CallPermitTones.Any(t => t.HasAnyPendingRadioWrite)
            || OptionalSettings.MatchEndTones.Any(t => t.HasAnyPendingRadioWrite)
            || OptionalSettings.CallResetTones.Any(t => t.HasAnyPendingRadioWrite)
            || OptionalSettings.UnMatchEndTones.Any(t => t.HasAnyPendingRadioWrite)
            || OptionalSettings.CallAllTones.Any(t => t.HasAnyPendingRadioWrite);
        var alarmSettingsDirty = AlarmSettings.HasAnyPendingRadioWrite;
        var talkAliasSettingsDirty = TalkAliasSettings.HasAnyPendingRadioWrite;
        var aprsSettingsDirty = AprsSettings.HasAnyPendingRadioWrite;
        // No "nothing to write" early return anymore - see
        // CanWriteChangesToRadio's own doc comment (2026-08-16). Every
        // dirtyXxx/xxxDirty flag computed above is still used below to
        // decide what actually gets patched; an entity that isn't dirty
        // just doesn't get a patch call, so this write becomes a genuine
        // no-op RMW round trip when truly nothing has changed, matching
        // the vendor CPS rather than refusing to run at all.

        var patches = new List<(ChannelEntry Channel, int RadioIndex, ChannelCodec.ChannelFieldPatch Patch)>();
        var summaryLines = new List<string>();
        foreach (var channel in dirtyChannels)
        {
            // Defense in depth against a real incident: VFO A/B (radio
            // indices 4000/4001) used to leak into the regular Channels
            // list from a read, and Number-based "next channel" allocation
            // could then compute an out-of-range/reserved radio index. The
            // read side is now fixed (RadioCodeplugReader.ReadChannels
            // filters them out) and Add/DuplicateChannel now cap Number,
            // but refuse here too rather than trust every caller forever.
            var radioIndex = channel.Number - 1;
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.MaxRegularChannelCount)
            {
                RadioWriteStatusText = $"Channel {channel.Number} ('{channel.Name}'): channel number is outside the radio's valid range (1-{D890UvMemoryMap.MaxRegularChannelCount}) - refusing to write.";
                return;
            }

            var (patch, channelSummaryLines, error) = BuildSafeFieldPatch(channel);
            if (error is not null)
            {
                // Fail fast, before touching the radio at all - one bad
                // value anywhere in the batch shouldn't risk a partial write.
                RadioWriteStatusText = $"Channel {channel.Number} ('{channel.Name}'): {error}";
                return;
            }

            patches.Add((channel, radioIndex, patch));
            summaryLines.Add($"Channel {channel.Number} ('{channel.Name}'): {string.Join(", ", channelSummaryLines)}");
        }

        foreach (var radioIndex in deleteIndices)
        {
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.MaxRegularChannelCount)
            {
                RadioWriteStatusText = $"Channel {radioIndex + 1}: channel number is outside the radio's valid range (1-{D890UvMemoryMap.MaxRegularChannelCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Channel {radioIndex + 1}: deleted");
        }

        var zonePatches = new List<(ZoneEntry Zone, int RadioIndex, ZoneCodec.ZoneFieldPatch Patch)>();
        foreach (var zone in dirtyZones)
        {
            var radioIndex = zone.Number - 1;
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.ZoneSlotCount)
            {
                RadioWriteStatusText = $"Zone {zone.Number} ('{zone.Name}'): zone number is outside the radio's valid range (1-{D890UvMemoryMap.ZoneSlotCount}) - refusing to write.";
                return;
            }

            var (patch, zoneSummaryLines) = BuildSafeZoneFieldPatch(zone);
            zonePatches.Add((zone, radioIndex, patch));
            summaryLines.Add($"Zone {zone.Number} ('{zone.Name}'): {string.Join(", ", zoneSummaryLines)}");
        }

        foreach (var radioIndex in deleteZoneIndices)
        {
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.ZoneSlotCount)
            {
                RadioWriteStatusText = $"Zone {radioIndex + 1}: zone number is outside the radio's valid range (1-{D890UvMemoryMap.ZoneSlotCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Zone {radioIndex + 1}: deleted");
        }

        var scanListValues = new List<(ScanListEntry ScanList, int RadioIndex, ScanListCodec.DecodedScanList Values)>();
        foreach (var scanList in dirtyScanLists)
        {
            var radioIndex = scanList.Number - 1;
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.ScanListSlotCount)
            {
                RadioWriteStatusText = $"Scan List {scanList.Number} ('{scanList.Name}'): scan list number is outside the radio's valid range (1-{D890UvMemoryMap.ScanListSlotCount}) - refusing to write.";
                return;
            }

            var (values, scanListSummaryLines) = BuildSafeScanListValues(scanList, radioIndex);
            scanListValues.Add((scanList, radioIndex, values));
            summaryLines.Add($"Scan List {scanList.Number} ('{scanList.Name}'): {string.Join(", ", scanListSummaryLines)}");
        }

        foreach (var radioIndex in deleteScanListIndices)
        {
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.ScanListSlotCount)
            {
                RadioWriteStatusText = $"Scan List {radioIndex + 1}: scan list number is outside the radio's valid range (1-{D890UvMemoryMap.ScanListSlotCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Scan List {radioIndex + 1}: deleted");
        }

        var amAirValues = new List<(AmAirEntry AmAir, int RadioIndex, AmAirCodec.DecodedAmAir Values)>();
        foreach (var amAir in dirtyAmAir)
        {
            var radioIndex = amAir.Number - 1;
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.AmAirSlotCount)
            {
                RadioWriteStatusText = $"AM Air {amAir.Number} ('{amAir.Name}'): channel number is outside the radio's valid range (1-{D890UvMemoryMap.AmAirSlotCount}) - refusing to write.";
                return;
            }

            var (values, amAirSummaryLines) = BuildSafeAmAirValues(amAir, radioIndex);
            amAirValues.Add((amAir, radioIndex, values));
            summaryLines.Add($"AM Air {amAir.Number} ('{amAir.Name}'): {string.Join(", ", amAirSummaryLines)}");
        }

        foreach (var radioIndex in deleteAmAirIndices)
        {
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.AmAirSlotCount)
            {
                RadioWriteStatusText = $"AM Air {radioIndex + 1}: channel number is outside the radio's valid range (1-{D890UvMemoryMap.AmAirSlotCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"AM Air {radioIndex + 1}: deleted");
        }

        var amZoneValues = new List<(AmZoneEntry AmZone, int RadioIndex, AmZoneCodec.DecodedAmZone Values)>();
        foreach (var amZone in dirtyAmZones)
        {
            var radioIndex = amZone.Number - 1;
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.AmZoneCount)
            {
                RadioWriteStatusText = $"AM Zone {amZone.Number} ('{amZone.Name}'): zone number is outside the radio's valid range (1-{D890UvMemoryMap.AmZoneCount}) - refusing to write.";
                return;
            }

            // The scan-channel bitmask can only reference AM Air radio
            // indexes 0-127 (AmZoneCodec.ScanChannelBitCount) - the
            // "available" list already restricts selection to this range,
            // but fail fast here too rather than let AmZoneCodec.
            // EncodeScanChannelBitmask throw mid-write.
            var outOfRangeScanChannel = amZone.ScanChannelMembers.FirstOrDefault(c => c.Number - 1 >= AmZoneCodec.ScanChannelBitCount);
            if (outOfRangeScanChannel is not null)
            {
                RadioWriteStatusText = $"AM Zone {amZone.Number} ('{amZone.Name}'): scan channel member '{outOfRangeScanChannel.Name}' is outside the scan list's valid range (AM Air 1-{AmZoneCodec.ScanChannelBitCount}) - refusing to write.";
                return;
            }

            var (values, amZoneSummaryLines) = BuildSafeAmZoneValues(amZone, radioIndex);
            amZoneValues.Add((amZone, radioIndex, values));
            summaryLines.Add($"AM Zone {amZone.Number} ('{amZone.Name}'): {string.Join(", ", amZoneSummaryLines)}");
        }

        foreach (var radioIndex in deleteAmZoneIndices)
        {
            if (radioIndex < 0 || radioIndex >= D890UvMemoryMap.AmZoneCount)
            {
                RadioWriteStatusText = $"AM Zone {radioIndex + 1}: zone number is outside the radio's valid range (1-{D890UvMemoryMap.AmZoneCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"AM Zone {radioIndex + 1}: deleted");
        }

        foreach (var sms in dirtyPrefabricatedSms)
        {
            var slotId = sms.Number - 1;
            if (slotId < 0 || slotId >= PrefabricatedSmsCodec.SlotCount)
            {
                RadioWriteStatusText = $"Prefabricated SMS {sms.Number}: slot number is outside the radio's valid range (1-{PrefabricatedSmsCodec.SlotCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Prefabricated SMS {sms.Number}: text = '{sms.Text}'");
        }

        foreach (var slotId in deletePrefabricatedSmsIndices)
        {
            if (slotId < 0 || slotId >= PrefabricatedSmsCodec.SlotCount)
            {
                RadioWriteStatusText = $"Prefabricated SMS {slotId + 1}: slot number is outside the radio's valid range (1-{PrefabricatedSmsCodec.SlotCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Prefabricated SMS {slotId + 1}: deleted");
        }

        // The used-slot chain is one shared structure across every message
        // (see PrefabricatedSmsCodec's doc comment) - any add/edit/delete
        // requires rewriting the WHOLE chain from current state, not just
        // the touched slot(s). allActiveSlotIds reflects the post-edit
        // target state directly from the live collection (already excludes
        // anything removed locally via RemovePrefabricatedSms).
        var prefabricatedSmsChainChanged = dirtyPrefabricatedSms.Count > 0 || deletePrefabricatedSmsIndices.Count > 0;
        var allActiveSlotIds = PrefabricatedSmsMessages.Select(s => s.Number - 1).OrderBy(id => id).ToList();

        var fmChannelValues = new List<(FmChannelEntry FmChannel, int RadioIndex, FmChannelCodec.DecodedFmChannel Values)>();
        foreach (var fmChannel in dirtyFmChannels)
        {
            var radioIndex = fmChannel.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.FmChannelMax)
            {
                RadioWriteStatusText = $"FM Channel {fmChannel.Number} ('{fmChannel.Name}'): channel number is outside the radio's valid range (1-{CodeplugLimits.FmChannelMax}) - refusing to write.";
                return;
            }

            var (values, fmChannelSummaryLines) = BuildSafeFmChannelValues(fmChannel, radioIndex);
            fmChannelValues.Add((fmChannel, radioIndex, values));
            summaryLines.Add($"FM Channel {fmChannel.Number} ('{fmChannel.Name}'): {string.Join(", ", fmChannelSummaryLines)}");
        }

        foreach (var radioIndex in deleteFmChannelIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.FmChannelMax)
            {
                RadioWriteStatusText = $"FM Channel {radioIndex + 1}: channel number is outside the radio's valid range (1-{CodeplugLimits.FmChannelMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"FM Channel {radioIndex + 1}: deleted");
        }

        foreach (var autoRepeaterOffset in dirtyAutoRepeaterOffsets)
        {
            var radioIndex = autoRepeaterOffset.Number - 1;
            if (radioIndex < 0 || radioIndex >= AutoRepeaterOffsetCodec.EntryCount)
            {
                RadioWriteStatusText = $"Auto Repeater Offset {autoRepeaterOffset.Number}: slot number is outside the radio's valid range (1-{AutoRepeaterOffsetCodec.EntryCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Auto Repeater Offset {autoRepeaterOffset.Number}: {autoRepeaterOffset.OffsetFrequencyMhz} MHz");
        }

        var gpsRoamingValues = new List<(GpsRoamingEntry GpsRoaming, int RadioIndex, GpsRoamingCodec.DecodedGpsRoaming Values)>();
        foreach (var gpsRoaming in dirtyGpsRoaming)
        {
            var radioIndex = gpsRoaming.Number - 1;
            if (radioIndex < 0 || radioIndex >= GpsRoamingCodec.EntryCount)
            {
                RadioWriteStatusText = $"GPS Roaming {gpsRoaming.Number}: slot number is outside the radio's valid range (1-{GpsRoamingCodec.EntryCount}) - refusing to write.";
                return;
            }

            var values = new GpsRoamingCodec.DecodedGpsRoaming(radioIndex)
            {
                Enabled = gpsRoaming.Enabled,
                ZoneIndex = gpsRoaming.ZoneIndex,
                LatDegree = gpsRoaming.LatDegree,
                LatMinute = gpsRoaming.LatMinute,
                LatMinuteDecimal = gpsRoaming.LatMinuteDecimal,
                NorthSouth = gpsRoaming.NorthSouth,
                LongDegree = gpsRoaming.LongDegree,
                LongMinute = gpsRoaming.LongMinute,
                LongMinuteDecimal = gpsRoaming.LongMinuteDecimal,
                EastWest = gpsRoaming.EastWest,
                Radius = gpsRoaming.Radius
            };
            gpsRoamingValues.Add((gpsRoaming, radioIndex, values));
            summaryLines.Add($"GPS Roaming {gpsRoaming.Number}: {(gpsRoaming.Enabled ? "On" : "Off")}, Zone = '{gpsRoaming.ZoneDisplayName}'");
        }

        foreach (var radioIndex in deleteAutoRepeaterOffsetIndices)
        {
            if (radioIndex < 0 || radioIndex >= AutoRepeaterOffsetCodec.EntryCount)
            {
                RadioWriteStatusText = $"Auto Repeater Offset {radioIndex + 1}: slot number is outside the radio's valid range (1-{AutoRepeaterOffsetCodec.EntryCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Auto Repeater Offset {radioIndex + 1}: deleted");
        }

        var analogAddressValues = new List<(AnalogAddressEntry AnalogAddress, int RadioIndex, AnalogAddressCodec.DecodedAnalogAddress Values)>();
        foreach (var analogAddress in dirtyAnalogAddresses)
        {
            var radioIndex = analogAddress.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.AnalogAddressMax)
            {
                RadioWriteStatusText = $"Analog Address {analogAddress.Number} ('{analogAddress.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.AnalogAddressMax}) - refusing to write.";
                return;
            }

            var (values, analogAddressSummaryLines) = BuildSafeAnalogAddressValues(analogAddress, radioIndex);
            analogAddressValues.Add((analogAddress, radioIndex, values));
            summaryLines.Add($"Analog Address {analogAddress.Number} ('{analogAddress.Name}'): {string.Join(", ", analogAddressSummaryLines)}");
        }

        foreach (var radioIndex in deleteAnalogAddressIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.AnalogAddressMax)
            {
                RadioWriteStatusText = $"Analog Address {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.AnalogAddressMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Analog Address {radioIndex + 1}: deleted");
        }

        var qdc1200IdValues = new List<(Qdc1200IdEntry Qdc1200Id, int RadioIndex, Qdc1200IdCodec.DecodedQdc1200Id Values)>();
        foreach (var qdc1200Id in dirtyQdc1200Ids)
        {
            var radioIndex = qdc1200Id.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.Qdc1200IdMax)
            {
                RadioWriteStatusText = $"QDC 1200 ID {qdc1200Id.Number} ('{qdc1200Id.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.Qdc1200IdMax}) - refusing to write.";
                return;
            }

            var (values, qdc1200IdSummaryLines) = BuildSafeQdc1200IdValues(qdc1200Id, radioIndex);
            qdc1200IdValues.Add((qdc1200Id, radioIndex, values));
            summaryLines.Add($"QDC 1200 ID {qdc1200Id.Number} ('{qdc1200Id.Name}'): {string.Join(", ", qdc1200IdSummaryLines)}");
        }

        foreach (var radioIndex in deleteQdc1200IdIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.Qdc1200IdMax)
            {
                RadioWriteStatusText = $"QDC 1200 ID {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.Qdc1200IdMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"QDC 1200 ID {radioIndex + 1}: deleted");
        }

        Qdc1200SettingsCodec.DecodedQdc1200Settings? qdc1200SettingsValues = null;
        if (qdc1200SettingsDirty)
        {
            qdc1200SettingsValues = new Qdc1200SettingsCodec.DecodedQdc1200Settings
            {
                SideTone = Qdc1200Settings.SideTone,
                RemotelyKillAllow = Qdc1200Settings.RemotelyKillAllow,
                RemotelyMonitorAllow = Qdc1200Settings.RemotelyMonitorAllow,
                Pretime = Qdc1200Settings.Pretime,
                AutoResetTime = Qdc1200Settings.AutoResetTime,
                SelfIdPrivateCall = Qdc1200Settings.SelfIdPrivateCall,
                SelfIdGroupCall = Qdc1200Settings.SelfIdGroupCall,
                MaxAckWaitTime = Qdc1200Settings.MaxAckWaitTime,
                ResendCode = Qdc1200Settings.ResendCode,
                RemoteListeningDuration = Qdc1200Settings.RemoteListeningDuration
            };
            summaryLines.Add("QDC 1200 Settings: will be written");
        }

        var analogQuickCallValues = new List<(AnalogQuickCallEntry AnalogQuickCall, int RadioIndex, AnalogQuickCallCodec.DecodedAnalogQuickCall Values)>();
        foreach (var analogQuickCall in dirtyAnalogQuickCalls)
        {
            var radioIndex = analogQuickCall.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.AnalogQuickCallMax)
            {
                RadioWriteStatusText = $"Analog Quick Call {analogQuickCall.Number}: number is outside the radio's valid range (1-{CodeplugLimits.AnalogQuickCallMax}) - refusing to write.";
                return;
            }

            var values = new AnalogQuickCallCodec.DecodedAnalogQuickCall(radioIndex)
            {
                OperationType = analogQuickCall.OperationType,
                CallId = analogQuickCall.CallId
            };
            analogQuickCallValues.Add((analogQuickCall, radioIndex, values));
            summaryLines.Add($"Analog Quick Call {analogQuickCall.Number}: Operation Type = {analogQuickCall.OperationTypeText}");
        }

        foreach (var radioIndex in deleteAnalogQuickCallIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.AnalogQuickCallMax)
            {
                RadioWriteStatusText = $"Analog Quick Call {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.AnalogQuickCallMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Analog Quick Call {radioIndex + 1}: deleted");
        }

        var stateInformationValues = new List<(StateInformationEntry StateInformation, int RadioIndex, string Content)>();
        foreach (var stateInformation in dirtyStateInformation)
        {
            var radioIndex = stateInformation.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.StateInformationMax)
            {
                RadioWriteStatusText = $"State Information {stateInformation.Number}: number is outside the radio's valid range (1-{CodeplugLimits.StateInformationMax}) - refusing to write.";
                return;
            }

            stateInformationValues.Add((stateInformation, radioIndex, stateInformation.Content));
            summaryLines.Add($"State Information {stateInformation.Number}: Content = '{stateInformation.Content}'");
        }

        foreach (var radioIndex in deleteStateInformationIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.StateInformationMax)
            {
                RadioWriteStatusText = $"State Information {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.StateInformationMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"State Information {radioIndex + 1}: deleted");
        }

        var hotKeyValues = new List<(HotKeyEntry HotKey, int RadioIndex, HotKeyCodec.DecodedHotKey Values)>();
        foreach (var (hotKey, radioIndex) in dirtyHotKeys)
        {
            var values = new HotKeyCodec.DecodedHotKey(radioIndex)
            {
                Mode = hotKey.Mode,
                Menu = hotKey.Menu,
                CallType = hotKey.CallType,
                DigiCallType = hotKey.DigiCallType,
                CallObject = hotKey.CallObject,
                Content = hotKey.Content
            };
            hotKeyValues.Add((hotKey, radioIndex, values));
            summaryLines.Add($"Hot Key '{hotKey.Key}': {hotKey.ModeText}");
        }

        var qdcAddressValues = new List<(QdcAddressEntry QdcAddress, int RadioIndex, QdcAddressCodec.DecodedQdcAddress Values)>();
        foreach (var qdcAddress in dirtyQdcAddresses)
        {
            var radioIndex = qdcAddress.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.QdcAddressMax)
            {
                RadioWriteStatusText = $"QDC Address {qdcAddress.Number} ('{qdcAddress.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.QdcAddressMax}) - refusing to write.";
                return;
            }

            var values = new QdcAddressCodec.DecodedQdcAddress(radioIndex)
            {
                Type = qdcAddress.Type,
                CallType = qdcAddress.CallType,
                Ack = qdcAddress.Ack,
                GroupCallId = qdcAddress.GroupCallId,
                PrivateCallId = qdcAddress.PrivateCallId,
                Name = qdcAddress.Name
            };
            qdcAddressValues.Add((qdcAddress, radioIndex, values));
            summaryLines.Add($"QDC Address {qdcAddress.Number} ('{qdcAddress.Name}'): Call Type = {qdcAddress.CallTypeText}");
        }

        foreach (var radioIndex in deleteQdcAddressIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.QdcAddressMax)
            {
                RadioWriteStatusText = $"QDC Address {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.QdcAddressMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"QDC Address {radioIndex + 1}: deleted");
        }

        var fiveToneIdValues = new List<(FiveToneIdEntry FiveToneId, int RadioIndex, FiveToneIdCodec.DecodedFiveToneId Values)>();
        // Function Option/Function Decoding Response/Information ID/
        // Function Name live on FiveToneIdEntry too (see that class's own
        // doc comment) but are encoded to a COMPLETELY SEPARATE address (a
        // small 16-slot array, see D890UvMemoryMap.FiveToneInfoIdData) -
        // only rows whose own Number falls within that slot count get one
        // of these; every other dirty row still gets its own ID-table
        // patch above regardless.
        var fiveToneInfoIdSlotValues = new List<(int SlotIndex, FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot Values)>();
        foreach (var fiveToneId in dirtyFiveToneIds)
        {
            var radioIndex = fiveToneId.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.FiveToneIdMax)
            {
                RadioWriteStatusText = $"5Tone ID {fiveToneId.Number} ('{fiveToneId.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.FiveToneIdMax}) - refusing to write.";
                return;
            }

            var (values, fiveToneIdSummaryLines) = BuildSafeFiveToneIdValues(fiveToneId, radioIndex);
            fiveToneIdValues.Add((fiveToneId, radioIndex, values));
            summaryLines.Add($"5Tone ID {fiveToneId.Number} ('{fiveToneId.Name}'): {string.Join(", ", fiveToneIdSummaryLines)}");

            if (fiveToneId.Number >= 1 && fiveToneId.Number <= D890UvMemoryMap.FiveToneInfoIdSlotCount)
            {
                var slotValues = new FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot
                {
                    FunctionOption = fiveToneId.FunctionOption,
                    FunctionDecodingResponse = fiveToneId.FunctionDecodingResponse,
                    InformationId = fiveToneId.InformationId,
                    FunctionName = fiveToneId.FunctionName
                };
                fiveToneInfoIdSlotValues.Add((fiveToneId.Number - 1, slotValues));
                summaryLines.Add($"5Tone Information ID {fiveToneId.Number}: Function Option = {fiveToneId.FunctionOptionText}");
            }
        }

        var fiveToneInfoIdSlotClears = new List<int>();
        foreach (var radioIndex in deleteFiveToneIdIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.FiveToneIdMax)
            {
                RadioWriteStatusText = $"5Tone ID {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.FiveToneIdMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"5Tone ID {radioIndex + 1}: deleted");

            if (radioIndex + 1 <= D890UvMemoryMap.FiveToneInfoIdSlotCount)
            {
                fiveToneInfoIdSlotClears.Add(radioIndex);
            }
        }

        FiveToneSettingsCodec.DecodedFiveToneSettings? fiveToneSettingsValues = null;
        FiveToneSettingsCodec.DecodedFiveToneBotEot? fiveToneBotValues = null;
        FiveToneSettingsCodec.DecodedFiveToneBotEot? fiveToneEotValues = null;
        if (fiveToneSettingsDirty)
        {
            fiveToneSettingsValues = new FiveToneSettingsCodec.DecodedFiveToneSettings
            {
                SelfId = FiveToneSettings.SelfId,
                DecodeStandard = FiveToneSettings.DecodeStandard,
                DecodingResponse = FiveToneSettings.DecodingResponse,
                DecodeTimeMs = FiveToneSettings.DecodeTimeMs,
                DecUnit1 = FiveToneSettings.DecUnit1,
                DecUnit2 = FiveToneSettings.DecUnit2,
                DecUnit3 = FiveToneSettings.DecUnit3,
                DecUnit4 = FiveToneSettings.DecUnit4,
                DecUnit5 = FiveToneSettings.DecUnit5,
                DecUnit6 = FiveToneSettings.DecUnit6,
                DecUnit7 = FiveToneSettings.DecUnit7,
                DispAnyId = FiveToneSettings.DispAnyId,
                Pretime = FiveToneSettings.Pretime,
                AutoResetTime = FiveToneSettings.AutoResetTime,
                TimeLapseAfterEncode = FiveToneSettings.TimeLapseAfterEncode,
                PttIdPauseTime = FiveToneSettings.PttIdPauseTime,
                FirstToneLength = FiveToneSettings.FirstToneLength,
                StopTimeLength = FiveToneSettings.StopTimeLength,
                FirstToneLengthAfterStop = FiveToneSettings.FirstToneLengthAfterStop,
                SideTone = FiveToneSettings.SideTone
            };
            fiveToneBotValues = new FiveToneSettingsCodec.DecodedFiveToneBotEot
            {
                Standard = FiveToneSettings.BotStandard,
                TimeOfEncodeTone = (byte)FiveToneSettings.BotTimeOfEncodeTone,
                EncodeId = FiveToneSettings.BotEncodeId,
                SpecialCall = ToFiveToneSpecialCallCodecValues(FiveToneSettings.BotSpecialCall)
            };
            fiveToneEotValues = new FiveToneSettingsCodec.DecodedFiveToneBotEot
            {
                Standard = FiveToneSettings.EotStandard,
                TimeOfEncodeTone = (byte)FiveToneSettings.EotTimeOfEncodeTone,
                EncodeId = FiveToneSettings.EotEncodeId,
                SpecialCall = ToFiveToneSpecialCallCodecValues(FiveToneSettings.EotSpecialCall)
            };
            summaryLines.Add("5Tone Settings (incl. BOT/EOT): will be written");
        }

        var twoToneEncodeValues = new List<(TwoToneEncodeEntry Entry, int RadioIndex, TwoToneEncodeCodec.DecodedTwoToneEncode Values)>();
        foreach (var entry in dirtyTwoToneEncodeEntries)
        {
            var radioIndex = entry.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TwoToneEncodeMax)
            {
                RadioWriteStatusText = $"2Tone Encode {entry.Number} ('{entry.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.TwoToneEncodeMax}) - refusing to write.";
                return;
            }

            var values = new TwoToneEncodeCodec.DecodedTwoToneEncode(radioIndex)
            {
                FirstToneFrequencyHz = entry.FirstToneFrequencyHz,
                SecondToneFrequencyHz = entry.SecondToneFrequencyHz,
                Name = entry.Name
            };
            twoToneEncodeValues.Add((entry, radioIndex, values));
            summaryLines.Add($"2Tone Encode {entry.Number} ('{entry.Name}'): {entry.FirstToneFrequencyHz:0.0}/{entry.SecondToneFrequencyHz:0.0} Hz");
        }

        foreach (var radioIndex in deleteTwoToneEncodeIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TwoToneEncodeMax)
            {
                RadioWriteStatusText = $"2Tone Encode {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.TwoToneEncodeMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"2Tone Encode {radioIndex + 1}: deleted");
        }

        var twoToneDecodeValues = new List<(TwoToneDecodeEntry Entry, int RadioIndex, TwoToneDecodeCodec.DecodedTwoToneDecode Values)>();
        foreach (var entry in dirtyTwoToneDecodeEntries)
        {
            var radioIndex = entry.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TwoToneDecodeMax)
            {
                RadioWriteStatusText = $"2Tone Decode {entry.Number} ('{entry.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.TwoToneDecodeMax}) - refusing to write.";
                return;
            }

            var values = new TwoToneDecodeCodec.DecodedTwoToneDecode(radioIndex)
            {
                FirstToneFrequencyHz = entry.FirstToneFrequencyHz,
                SecondToneFrequencyHz = entry.SecondToneFrequencyHz,
                DecodingResponse = entry.DecodingResponse,
                Name = entry.Name
            };
            twoToneDecodeValues.Add((entry, radioIndex, values));
            summaryLines.Add($"2Tone Decode {entry.Number} ('{entry.Name}'): {entry.FirstToneFrequencyHz:0.0}/{entry.SecondToneFrequencyHz:0.0} Hz, {entry.DecodingResponseText}");
        }

        foreach (var radioIndex in deleteTwoToneDecodeIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TwoToneDecodeMax)
            {
                RadioWriteStatusText = $"2Tone Decode {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.TwoToneDecodeMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"2Tone Decode {radioIndex + 1}: deleted");
        }

        TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings? twoToneEncodeSettingsValues = null;
        if (twoToneEncodeSettingsDirty)
        {
            twoToneEncodeSettingsValues = new TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings(
                FirstToneDurationSeconds: TwoToneEncodeSettings.FirstToneDurationSeconds,
                SecondToneDurationSeconds: TwoToneEncodeSettings.SecondToneDurationSeconds,
                LongToneDurationSeconds: TwoToneEncodeSettings.LongToneDurationSeconds,
                GapTimeMs: TwoToneEncodeSettings.GapTimeMs,
                AutoResetTimeSeconds: TwoToneEncodeSettings.AutoResetTimeSeconds,
                SideTone: TwoToneEncodeSettings.SideTone);
            summaryLines.Add("2Tone Encode Settings: will be written");
        }

        var dtmfEncodeValues = new List<(DtmfEncodeEntry Entry, int RadioIndex, string Code)>();
        foreach (var entry in dirtyDtmfEncodeEntries)
        {
            var radioIndex = entry.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.DtmfEncodeSlotCount)
            {
                RadioWriteStatusText = $"DTMF Encode {entry.Number}: number is outside the radio's valid range (1-{CodeplugLimits.DtmfEncodeSlotCount}) - refusing to write.";
                return;
            }

            dtmfEncodeValues.Add((entry, radioIndex, entry.Code));
            summaryLines.Add($"DTMF Encode M{entry.Number}: '{entry.Code}'");
        }

        DtmfSettingsCodec.DecodedDtmfSettings? dtmfSettingsValues = null;
        int? dtmfTransmittingTimeIndex = null;
        if (dtmfSettingsDirty)
        {
            dtmfSettingsValues = new DtmfSettingsCodec.DecodedDtmfSettings
            {
                IntervalCharacter = DtmfSettings.IntervalCharacter,
                GroupCode = DtmfSettings.GroupCode,
                DecodingResponse = DtmfSettings.DecodingResponse,
                PretimeMs = DtmfSettings.PretimeMs,
                FirstDigitTimeMs = DtmfSettings.FirstDigitTimeMs,
                AutoResetTimeSeconds = DtmfSettings.AutoResetTimeSeconds,
                SelfId = DtmfSettings.SelfId,
                TimeLapseAfterEncodeMs = DtmfSettings.TimeLapseAfterEncodeMs,
                PttIdPauseTimeSeconds = DtmfSettings.PttIdPauseTimeSeconds,
                PttId = DtmfSettings.PttId,
                DCodePauseSeconds = DtmfSettings.DCodePauseSeconds,
                SideTone = DtmfSettings.SideTone
            };
            var transmittingTimeIndex = DtmfSettingsEntry.TransmittingTimeMsOptions.ToList().IndexOf(DtmfSettings.TransmittingTimeMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
            dtmfTransmittingTimeIndex = transmittingTimeIndex >= 0 ? transmittingTimeIndex : 0;
            summaryLines.Add("DTMF Settings (incl. BOT/EOT/Remotely Kill/Stun): will be written");
        }

        var radioIdValues = new List<(RadioIdEntry RadioId, int RadioIndex, RadioIdCodec.DecodedRadioId Values)>();
        foreach (var radioId in dirtyRadioIds)
        {
            var radioIndex = radioId.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RadioIdListMax)
            {
                RadioWriteStatusText = $"Radio ID {radioId.Number} ('{radioId.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.RadioIdListMax}) - refusing to write.";
                return;
            }

            var values = new RadioIdCodec.DecodedRadioId(radioIndex) { DmrId = radioId.DmrId, Name = radioId.Name };
            radioIdValues.Add((radioId, radioIndex, values));
            summaryLines.Add($"Radio ID {radioId.Number} ('{radioId.Name}'): DMR ID = {radioId.DmrId}");
        }

        foreach (var radioIndex in deleteRadioIdIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RadioIdListMax)
            {
                RadioWriteStatusText = $"Radio ID {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.RadioIdListMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Radio ID {radioIndex + 1}: deleted");
        }

        MasterIdCodec.DecodedMasterId? masterIdValues = null;
        if (masterIdDirty)
        {
            masterIdValues = new MasterIdCodec.DecodedMasterId { DmrId = MasterId.DmrId, Used = MasterId.Used, Name = MasterId.Name };
            summaryLines.Add("Master ID: will be written");
        }

        var talkgroupValues = new List<(TalkgroupEntry Talkgroup, int RadioIndex, TalkgroupCodec.DecodedTalkgroup Values)>();
        foreach (var talkgroup in dirtyTalkgroups)
        {
            var radioIndex = talkgroup.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TalkgroupListMax)
            {
                RadioWriteStatusText = $"Talkgroup {talkgroup.Number} ('{talkgroup.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.TalkgroupListMax}) - refusing to write.";
                return;
            }

            var values = new TalkgroupCodec.DecodedTalkgroup(radioIndex) { DmrId = talkgroup.DmrId, Name = talkgroup.Name, CallType = talkgroup.CallType, CallAlert = talkgroup.CallAlert };
            talkgroupValues.Add((talkgroup, radioIndex, values));
            summaryLines.Add($"Talkgroup {talkgroup.Number} ('{talkgroup.Name}'): DMR ID = {talkgroup.DmrId}, Call Type = {talkgroup.CallType}");
        }

        foreach (var radioIndex in deleteTalkgroupIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.TalkgroupListMax)
            {
                RadioWriteStatusText = $"Talkgroup {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.TalkgroupListMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Talkgroup {radioIndex + 1}: deleted");
        }

        var receiveGroupListValues = new List<(ReceiveGroupListEntry ReceiveGroupList, int RadioIndex, ReceiveGroupListCodec.DecodedReceiveGroupList Values)>();
        foreach (var receiveGroupList in dirtyReceiveGroupLists)
        {
            var radioIndex = receiveGroupList.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.ReceiveGroupListMax)
            {
                RadioWriteStatusText = $"Receive Group List {receiveGroupList.Number} ('{receiveGroupList.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.ReceiveGroupListMax}) - refusing to write.";
                return;
            }

            var values = new ReceiveGroupListCodec.DecodedReceiveGroupList(radioIndex) { Name = receiveGroupList.Name, TalkgroupIndexes = receiveGroupList.TalkgroupIndexes.ToList() };
            receiveGroupListValues.Add((receiveGroupList, radioIndex, values));
            summaryLines.Add($"Receive Group List {receiveGroupList.Number} ('{receiveGroupList.Name}'): {receiveGroupList.TalkgroupIndexes.Count} member talkgroup(s)");
        }

        foreach (var radioIndex in deleteReceiveGroupListIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.ReceiveGroupListMax)
            {
                RadioWriteStatusText = $"Receive Group List {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.ReceiveGroupListMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Receive Group List {radioIndex + 1}: deleted");
        }

        var roamingChannelValues = new List<(RoamingChannelEntry RoamingChannel, int RadioIndex, RoamingChannelCodec.DecodedRoamingChannel Values)>();
        foreach (var roamingChannel in dirtyRoamingChannels)
        {
            var radioIndex = roamingChannel.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RoamingChannelMax)
            {
                RadioWriteStatusText = $"Roaming Channel {roamingChannel.Number} ('{roamingChannel.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.RoamingChannelMax}) - refusing to write.";
                return;
            }

            var values = new RoamingChannelCodec.DecodedRoamingChannel(radioIndex)
            {
                RxFrequencyMhz = roamingChannel.RxFrequencyMhz,
                TxFrequencyMhz = roamingChannel.TxFrequencyMhz,
                ColorCode = roamingChannel.ColorCode,
                Slot = roamingChannel.Slot,
                Name = roamingChannel.Name
            };
            roamingChannelValues.Add((roamingChannel, radioIndex, values));
            summaryLines.Add($"Roaming Channel {roamingChannel.Number} ('{roamingChannel.Name}'): RX = {roamingChannel.RxFrequencyMhz:0.00000} MHz, TX = {roamingChannel.TxFrequencyMhz:0.00000} MHz");
        }

        foreach (var radioIndex in deleteRoamingChannelIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RoamingChannelMax)
            {
                RadioWriteStatusText = $"Roaming Channel {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.RoamingChannelMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Roaming Channel {radioIndex + 1}: deleted");
        }

        var roamingZoneValues = new List<(RoamingZoneEntry RoamingZone, int RadioIndex, RoamingZoneCodec.DecodedRoamingZone Values)>();
        foreach (var roamingZone in dirtyRoamingZones)
        {
            var radioIndex = roamingZone.Number - 1;
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RoamingZoneMax)
            {
                RadioWriteStatusText = $"Roaming Zone {roamingZone.Number} ('{roamingZone.Name}'): number is outside the radio's valid range (1-{CodeplugLimits.RoamingZoneMax}) - refusing to write.";
                return;
            }

            var values = new RoamingZoneCodec.DecodedRoamingZone(radioIndex)
            {
                Name = roamingZone.Name,
                RoamingChannelIndexes = roamingZone.Members.Select(m => m.Number - 1).ToList()
            };
            roamingZoneValues.Add((roamingZone, radioIndex, values));
            summaryLines.Add($"Roaming Zone {roamingZone.Number} ('{roamingZone.Name}'): {roamingZone.Members.Count} member channel(s)");
        }

        foreach (var radioIndex in deleteRoamingZoneIndices)
        {
            if (radioIndex < 0 || radioIndex >= CodeplugLimits.RoamingZoneMax)
            {
                RadioWriteStatusText = $"Roaming Zone {radioIndex + 1}: number is outside the radio's valid range (1-{CodeplugLimits.RoamingZoneMax}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Roaming Zone {radioIndex + 1}: deleted");
        }

        foreach (var key in dirtyDigitalCodes)
        {
            if (key.Number < 1 || key.Number > CodeplugLimits.BasicEncryptionCodeCount)
            {
                RadioWriteStatusText = $"Digital Encryption Code {key.Number}: slot number is outside the radio's valid range (1-{CodeplugLimits.BasicEncryptionCodeCount}) - refusing to write.";
                return;
            }

            summaryLines.Add($"Digital Encryption Code {key.Number}: code = '{key.EncryptionId}'");
        }

        foreach (var key in dirtyArc4Keys)
        {
            if (key.Number < 1 || key.Number > CodeplugLimits.Arc4EncryptionKeyCount)
            {
                RadioWriteStatusText = $"ARC4 Key {key.Number}: slot number is outside the radio's valid range (1-{CodeplugLimits.Arc4EncryptionKeyCount}) - refusing to write.";
                return;
            }

            summaryLines.Add(key.EncryptionKey == "Off"
                ? $"ARC4 Key {key.Number}: cleared"
                : $"ARC4 Key {key.Number}: key = '{key.EncryptionKey}'");
        }

        foreach (var key in dirtyAesKeys)
        {
            if (key.Number < 1 || key.Number > CodeplugLimits.AesEncryptionKeyCount)
            {
                RadioWriteStatusText = $"AES Key {key.Number}: slot number is outside the radio's valid range (1-{CodeplugLimits.AesEncryptionKeyCount}) - refusing to write.";
                return;
            }

            summaryLines.Add(key.EncryptionId == "Off"
                ? $"AES Key {key.Number}: cleared"
                : $"AES Key {key.Number}: key = '{key.EncryptionId}'");
        }

        var optionalSettingsPatch = optionalSettingsDirty ? BuildSafeOptionalSettingsPatch(OptionalSettings, summaryLines) : null;
        var alarmSettingsValues = alarmSettingsDirty ? BuildAlarmSettingsValues(AlarmSettings, summaryLines) : null;
        var aprsSettingsValues = aprsSettingsDirty ? BuildAprsSettingsValues(AprsSettings, summaryLines) : null;

        TalkAliasSettingsCodec.DecodedTalkAliasSettings? talkAliasSettingsValues = null;
        if (talkAliasSettingsDirty)
        {
            talkAliasSettingsValues = new TalkAliasSettingsCodec.DecodedTalkAliasSettings
            {
                DisplayPriority = TalkAliasSettings.DisplayPriority,
                DataFormat = TalkAliasSettings.DataFormat
            };
            summaryLines.Add($"Talk Alias Settings: Display Priority = '{TalkAliasSettings.DisplayPriorityText}', Data Format = '{TalkAliasSettings.DataFormatText}'");
        }

        List<DigitalContactCodec.DecodedDigitalContact>? digitalContactsToWrite = null;
        if (_digitalContactsDirty)
        {
            digitalContactsToWrite = DigitalContacts.Select(ToDecodedDigitalContact).ToList();
            summaryLines.Add($"Digital Contacts: {digitalContactsToWrite.Count} contact(s) will be written (whole list rewritten together)");
        }

        // Whole-region rewrite (see TalkgroupWhitelistCodec.EncodeAll's own
        // doc comment) - Id is ignored by EncodeAll (list position wins),
        // so it doesn't matter what's passed here.
        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist>? talkgroupWhitelistValues = null;
        if (talkgroupWhitelistDirty)
        {
            if (TalkgroupWhitelist.Count > CodeplugLimits.WhitelistSlotMax)
            {
                RadioWriteStatusText = $"Talkgroup Whitelist has {TalkgroupWhitelist.Count} entries, more than the radio's {CodeplugLimits.WhitelistSlotMax}-entry cap - refusing to write.";
                return;
            }

            talkgroupWhitelistValues = TalkgroupWhitelist.Select(e => new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = e.DmrId, CallType = e.CallType }).ToList();
            summaryLines.Add($"Talkgroup Whitelist: {talkgroupWhitelistValues.Count} entries (whole list rewritten together)");
        }

        List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist>? digitalContactWhitelistValues = null;
        if (digitalContactWhitelistDirty)
        {
            if (DigitalContactWhitelist.Count > CodeplugLimits.WhitelistSlotMax)
            {
                RadioWriteStatusText = $"Digital Contact Whitelist has {DigitalContactWhitelist.Count} entries, more than the radio's {CodeplugLimits.WhitelistSlotMax}-entry cap - refusing to write.";
                return;
            }

            digitalContactWhitelistValues = DigitalContactWhitelist.Select(e => new TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist(0) { DmrId = e.DmrId, CallType = e.CallType }).ToList();
            summaryLines.Add($"Digital Contact Whitelist: {digitalContactWhitelistValues.Count} entries (whole list rewritten together)");
        }

        var displayedLines = summaryLines.Take(MaxWriteSummaryLines).ToList();
        if (summaryLines.Count > MaxWriteSummaryLines)
        {
            displayedLines.Add($"... and {summaryLines.Count - MaxWriteSummaryLines} more");
        }

        var summary = $"{dirtyChannels.Count} channel(s) will be written, {deleteIndices.Count} deleted, {dirtyZones.Count} zone(s) will be written, {deleteZoneIndices.Count} deleted, {dirtyScanLists.Count} scan list(s) will be written, {deleteScanListIndices.Count} deleted, {dirtyAmAir.Count} AM Air channel(s) will be written, {deleteAmAirIndices.Count} deleted, {dirtyAmZones.Count} AM Zone(s) will be written, {deleteAmZoneIndices.Count} deleted, {dirtyPrefabricatedSms.Count} prefabricated SMS will be written, {deletePrefabricatedSmsIndices.Count} deleted, {dirtyFmChannels.Count} FM channel(s) will be written, {deleteFmChannelIndices.Count} deleted, {dirtyAutoRepeaterOffsets.Count} Auto Repeater Offset(s) will be written, {deleteAutoRepeaterOffsetIndices.Count} deleted, {dirtyAnalogAddresses.Count} Analog Address(es) will be written, {deleteAnalogAddressIndices.Count} deleted, {dirtyQdc1200Ids.Count} QDC 1200 ID(s) will be written, {deleteQdc1200IdIndices.Count} deleted, QDC 1200 Settings {(qdc1200SettingsDirty ? "will be written" : "unchanged")}, {dirtyAnalogQuickCalls.Count} Analog Quick Call(s) will be written, {deleteAnalogQuickCallIndices.Count} deleted, {dirtyStateInformation.Count} State Information slot(s) will be written, {deleteStateInformationIndices.Count} deleted, {dirtyHotKeys.Count} Hot Key(s) will be written, {dirtyQdcAddresses.Count} QDC Address(es) will be written, {deleteQdcAddressIndices.Count} deleted, {dirtyFiveToneIds.Count} 5Tone ID(s) will be written, {deleteFiveToneIdIndices.Count} deleted, 5Tone Settings {(fiveToneSettingsDirty ? "will be written" : "unchanged")}, {dirtyTwoToneEncodeEntries.Count} 2Tone Encode entr{(dirtyTwoToneEncodeEntries.Count == 1 ? "y" : "ies")} will be written, {deleteTwoToneEncodeIndices.Count} deleted, {dirtyTwoToneDecodeEntries.Count} 2Tone Decode entr{(dirtyTwoToneDecodeEntries.Count == 1 ? "y" : "ies")} will be written, {deleteTwoToneDecodeIndices.Count} deleted, 2Tone Encode Settings {(twoToneEncodeSettingsDirty ? "will be written" : "unchanged")}, {dirtyDtmfEncodeEntries.Count} DTMF Encode entr{(dirtyDtmfEncodeEntries.Count == 1 ? "y" : "ies")} will be written, DTMF Settings {(dtmfSettingsDirty ? "will be written" : "unchanged")}, {dirtyRadioIds.Count} Radio ID(s) will be written, {deleteRadioIdIndices.Count} deleted, Master ID {(masterIdDirty ? "will be written" : "unchanged")}, {dirtyTalkgroups.Count} Talkgroup(s) will be written, {deleteTalkgroupIndices.Count} deleted, {dirtyReceiveGroupLists.Count} Receive Group List(s) will be written, {deleteReceiveGroupListIndices.Count} deleted, {dirtyRoamingChannels.Count} Roaming Channel(s) will be written, {deleteRoamingChannelIndices.Count} deleted, {dirtyRoamingZones.Count} Roaming Zone(s) will be written, {deleteRoamingZoneIndices.Count} deleted, {dirtyDigitalCodes.Count} digital code(s),{dirtyArc4Keys.Count} ARC4 key(s), {dirtyAesKeys.Count} AES key(s), Power-on settings {(optionalSettingsDirty ? "will be written" : "unchanged")}, Alarm Settings {(alarmSettingsDirty ? "will be written" : "unchanged")}, Talk Alias Settings {(talkAliasSettingsDirty ? "will be written" : "unchanged")}, Talkgroup Whitelist {(talkgroupWhitelistDirty ? "will be written" : "unchanged")}, Digital Contact Whitelist {(digitalContactWhitelistDirty ? "will be written" : "unchanged")}:\n" + string.Join("\n", displayedLines);
        // Safety-critical, added 2026-07-30 - see
        // OptionalSettingsEntry.IsVoxOn's doc comment for the hazard. Put
        // right in the confirmation dialog itself so it's seen at the exact
        // moment the radio is about to be actively connected to, not just
        // shown somewhere the user might not be looking.
        if (OptionalSettings.IsVoxOn)
        {
            summary = "WARNING: VOX is on. The radio can start transmitting on its own while connected for programming, which can damage the PC. Consider turning VOX off before continuing.\n\n" + summary;
        }

        var writeOptions = new RadioIncludeOptionsRequest
        {
            // Forced false when not genuinely populated, regardless of the
            // remembered choice - see CanIncludeDigitalContactsInWrite's own
            // doc comment. The dialog also disables the checkbox for this
            // same reason; this is defense in depth, not the only gate.
            IncludeDigitalContactList = WriteIncludeDigitalContactList && CanIncludeDigitalContactsInWrite,
            IncludeEncryptionKeys = WriteIncludeEncryptionKeys,
            DigitalContactListAvailableToInclude = CanIncludeDigitalContactsInWrite,
            DigitalContactCount = DigitalContacts.Count
        };
        if (!await _storagePicker.ConfirmWriteToRadioAsync(summary, writeOptions))
        {
            RadioWriteStatusText = "Write cancelled";
            return;
        }

        WriteIncludeDigitalContactList = writeOptions.IncludeDigitalContactList && CanIncludeDigitalContactsInWrite;
        WriteIncludeEncryptionKeys = writeOptions.IncludeEncryptionKeys;

        // Leaving a checkbox unchecked here does NOT discard the pending
        // edits (_digitalContactsDirty/the encryption key HasAnyPendingRadioWrite
        // flags are left alone) - it just excludes them from THIS write, by
        // emptying the lists the patch/mark-synced loops below iterate.
        // See RadioIncludeOptionsRequest's own doc comment.
        if (!writeOptions.IncludeDigitalContactList)
        {
            digitalContactsToWrite = null;
        }

        if (!writeOptions.IncludeEncryptionKeys)
        {
            dirtyDigitalCodes = [];
            dirtyArc4Keys = [];
            dirtyAesKeys = [];
        }

        IsWritingToRadio = true;
        RadioWriteWarnings.Clear();
        RadioWriteStatusText = "Writing...";
        // A stale "Read complete: ..."/"Read failed: ..." from a previous
        // read must not keep showing next to this new write's own status.
        RadioReadStatusText = "";
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        ReadFromRadioCommand.NotifyCanExecuteChanged();
        VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();

        try
        {
            var portName = SelectedPort;
            var baseSnapshot = _cachedRadioSnapshot;
            IProgress<string> progress = new Progress<string>(message => RadioWriteStatusText = message);
            var (result, patchedSnapshot) = await Task.Run(() =>
            {
                var connection = _radioConnectionFactory();

                // The baseline capture and every AddMissingXxx top-up below
                // each normally open and close their own session - and every
                // real close makes the radio physically reboot (see
                // RadioWriteVerification's own doc comment). Batched through
                // one wrapper so a write that needs several different entity
                // types topped up (a few edited zones, a new scan list, a
                // new radio ID, say) reboots the radio once here, not once
                // per entity type - found live 2026-08-24 rebooting 3-4+
                // times back to back before a write even started. See
                // BatchedRadioConnection's own doc comment. FinishAndClose
                // (which does the real, single close/reboot) runs even on an
                // exception, so a failure partway through this sequence
                // still lets the radio recover instead of leaving the port
                // open.
                var readConnection = new BatchedRadioConnection(connection);
                RadioCodeplugRawSnapshot snapshot;
                try
                {
                    if (baseSnapshot is null)
                    {
                        // No Read From Radio has happened yet this session - capture
                        // a baseline directly, WITHOUT calling ApplyRadioReadResult,
                        // so any codeplug already prepared in the live ViewModel
                        // (channels/zones/etc. added or edited before ever reading)
                        // is left completely untouched. Decided 2026-08-16: requiring
                        // a full Read (which does call ApplyRadioReadResult and
                        // overwrites the live view with the radio's own data) before
                        // every write destroyed exactly that kind of prepared-but-
                        // never-read work for no reason - RMW only needs a raw byte
                        // baseline to patch against, not a loaded view.
                        progress.Report("No baseline read yet - capturing one from the radio before writing...");
                        baseSnapshot = RadioCodeplugRawSnapshotReader.Capture(readConnection, portName, progress: progress);
                    }

                    // Extend the cached snapshot with any brand-new channels/
                    // zones it doesn't cover yet - a small, targeted read, NOT a
                    // full re-capture (see AddMissingChannels/AddMissingZones's
                    // doc comments). Returns the same snapshot unchanged if
                    // everything dirty is already covered, which is the common
                    // case.
                    progress.Report("Checking for any new channels not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingChannels(baseSnapshot, readConnection, portName, patches.Select(entry => entry.RadioIndex).Concat(deleteIndices));
                    progress.Report("Checking for any new zones not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingZones(snapshot, readConnection, portName, zonePatches.Select(entry => entry.RadioIndex).Concat(deleteZoneIndices));
                    progress.Report("Checking for any new scan lists not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingScanLists(snapshot, readConnection, portName, scanListValues.Select(entry => entry.RadioIndex).Concat(deleteScanListIndices));
                    progress.Report("Checking for any new AM Air channels not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingAmAir(snapshot, readConnection, portName, amAirValues.Select(entry => entry.RadioIndex).Concat(deleteAmAirIndices));
                    progress.Report("Checking for any new AM Zones not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingAmZones(snapshot, readConnection, portName, amZoneValues.Select(entry => entry.RadioIndex).Concat(deleteAmZoneIndices));
                    progress.Report("Checking for any new prefabricated SMS not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingPrefabricatedSms(snapshot, readConnection, portName, allActiveSlotIds.Count, dirtyPrefabricatedSms.Select(entry => entry.Number - 1).Concat(deletePrefabricatedSmsIndices));
                    progress.Report("Checking for any new FM channels not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingFmChannels(snapshot, readConnection, portName, fmChannelValues.Select(entry => entry.RadioIndex).Concat(deleteFmChannelIndices));
                    progress.Report("Checking for any new Analog Addresses not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingAnalogAddresses(snapshot, readConnection, portName, analogAddressValues.Select(entry => entry.RadioIndex).Concat(deleteAnalogAddressIndices));
                    progress.Report("Checking for any new Radio IDs not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingRadioIds(snapshot, readConnection, portName, radioIdValues.Select(entry => entry.RadioIndex).Concat(deleteRadioIdIndices));
                    progress.Report("Checking for any new Talkgroups not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingTalkgroups(snapshot, readConnection, portName, talkgroupValues.Select(entry => entry.RadioIndex).Concat(deleteTalkgroupIndices));
                    progress.Report("Checking for any new Receive Group Lists not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingReceiveGroupLists(snapshot, readConnection, portName, receiveGroupListValues.Select(entry => entry.RadioIndex).Concat(deleteReceiveGroupListIndices));
                    progress.Report("Checking for any new Roaming Channels not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingRoamingChannels(snapshot, readConnection, portName, roamingChannelValues.Select(entry => entry.RadioIndex).Concat(deleteRoamingChannelIndices));
                    progress.Report("Checking for any new Roaming Zones not yet cached...");
                    snapshot = RadioCodeplugRawSnapshotReader.AddMissingRoamingZones(snapshot, readConnection, portName, roamingZoneValues.Select(entry => entry.RadioIndex).Concat(deleteRoamingZoneIndices));
                }
                finally
                {
                    readConnection.FinishAndClose();
                }

                var patched = patches.Aggregate(snapshot, (snap, entry) => RadioCodeplugPatcher.ApplyChannelPatch(snap, entry.RadioIndex, entry.Patch));
                patched = deleteIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyChannelDelete);
                patched = zonePatches.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyZonePatch(snap, entry.RadioIndex, entry.Patch));
                patched = deleteZoneIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyZoneDelete);
                patched = scanListValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyScanListPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteScanListIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyScanListDelete);
                patched = amAirValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyAmAirPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteAmAirIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyAmAirDelete);
                patched = amZoneValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyAmZonePatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteAmZoneIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyAmZoneDelete);
                patched = dirtyPrefabricatedSms.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyPrefabricatedSmsTextPatch(snap, entry.Number - 1, entry.Text));
                patched = deletePrefabricatedSmsIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyPrefabricatedSmsDelete);
                if (prefabricatedSmsChainChanged)
                {
                    patched = RadioCodeplugPatcher.ApplyPrefabricatedSmsSetChain(patched, allActiveSlotIds);
                }
                patched = fmChannelValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyFmChannelPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteFmChannelIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyFmChannelDelete);
                // No "AddMissing" step needed for Auto Repeater Offsets,
                // same reason as encryption keys below - the whole 250-slot
                // region is always captured in full unconditionally (no
                // presence bitmap to gate a partial capture on).
                patched = dirtyAutoRepeaterOffsets.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyAutoRepeaterOffsetPatch(snap, entry.Number - 1, entry.OffsetFrequencyMhz));
                patched = deleteAutoRepeaterOffsetIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyAutoRepeaterOffsetDelete);
                patched = gpsRoamingValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyGpsRoamingPatch(snap, entry.RadioIndex, entry.Values));
                patched = analogAddressValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyAnalogAddressPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteAnalogAddressIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyAnalogAddressDelete);
                // No "AddMissing" step needed for the QDC 1200 ID table
                // either, same reason as Auto Repeater Offset above - the
                // whole 100-slot region is always captured in full
                // unconditionally (no bitmap/presence list found in either
                // live capture, see Qdc1200IdCodec's own doc comment).
                patched = qdc1200IdValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyQdc1200IdPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteQdc1200IdIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyQdc1200IdDelete);
                if (qdc1200SettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyQdc1200SettingsPatch(patched, qdc1200SettingsValues);
                }

                // No "AddMissing" step needed for Analog Quick Call/State
                // Information/Hot Key either - all three flat regions are
                // always captured in full unconditionally, same reasoning
                // as Auto Repeater Offset/QDC 1200 ID above.
                patched = analogQuickCallValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyAnalogQuickCallPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteAnalogQuickCallIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyAnalogQuickCallDelete);
                patched = stateInformationValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyStateInformationPatch(snap, entry.RadioIndex, entry.Content));
                patched = deleteStateInformationIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyStateInformationDelete);
                patched = hotKeyValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyHotKeyPatch(snap, entry.RadioIndex, entry.Values));
                patched = qdcAddressValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyQdcAddressPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteQdcAddressIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyQdcAddressDelete);

                // No "AddMissing" step needed for 5Tone IDs or Information
                // ID slots either - both regions are always captured in
                // full unconditionally, same reasoning as Auto Repeater
                // Offset/QDC 1200 ID above.
                patched = fiveToneIdValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyFiveToneIdPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteFiveToneIdIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyFiveToneIdDelete);
                patched = fiveToneInfoIdSlotValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyFiveToneInfoIdSlotPatch(snap, entry.SlotIndex, entry.Values));
                patched = fiveToneInfoIdSlotClears.Aggregate(patched, RadioCodeplugPatcher.ApplyFiveToneInfoIdSlotClear);
                if (fiveToneSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyFiveToneSettingsPatch(patched, fiveToneSettingsValues);
                }

                if (fiveToneBotValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyFiveToneBotPatch(patched, fiveToneBotValues);
                }

                if (fiveToneEotValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyFiveToneEotPatch(patched, fiveToneEotValues);
                }

                // No "AddMissing" step needed for 2Tone Encode/Decode either
                // - both regions are always captured in full unconditionally,
                // same reasoning as 5Tone above.
                patched = twoToneEncodeValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyTwoToneEncodePatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteTwoToneEncodeIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyTwoToneEncodeDelete);
                patched = twoToneDecodeValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyTwoToneDecodePatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteTwoToneDecodeIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyTwoToneDecodeDelete);
                if (twoToneEncodeSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyTwoToneEncodeSettingsPatch(patched, twoToneEncodeSettingsValues);
                }

                // No "AddMissing" step needed for DTMF Encode either - no
                // presence bitmap at all (fixed set, blank = all-0xFF), so
                // every one of the 16 slots is always addressable.
                patched = dtmfEncodeValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyDtmfEncodePatch(snap, entry.RadioIndex, entry.Code));
                if (dtmfSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyDtmfSettingsPatch(patched, dtmfSettingsValues);
                    patched = RadioCodeplugPatcher.ApplyDtmfBotPatch(patched, DtmfSettings.PttIdStartingBot);
                    patched = RadioCodeplugPatcher.ApplyDtmfEotPatch(patched, DtmfSettings.PttIdEndingEot);
                    patched = RadioCodeplugPatcher.ApplyDtmfRemotelyKillPatch(patched, DtmfSettings.RemotelyKill);
                    patched = RadioCodeplugPatcher.ApplyDtmfRemotelyStunPatch(patched, DtmfSettings.RemotelyStun);
                }

                if (dtmfTransmittingTimeIndex is { } transmittingTimeIndex)
                {
                    patched = RadioCodeplugPatcher.ApplyDtmfTransmittingTimePatch(patched, transmittingTimeIndex);
                }

                patched = radioIdValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyRadioIdPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteRadioIdIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyRadioIdDelete);
                if (masterIdValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyMasterIdPatch(patched, masterIdValues);
                }

                patched = talkgroupValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyTalkgroupPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteTalkgroupIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyTalkgroupDelete);
                patched = receiveGroupListValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyReceiveGroupListPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteReceiveGroupListIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyReceiveGroupListDelete);
                patched = roamingChannelValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyRoamingChannelPatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteRoamingChannelIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyRoamingChannelDelete);
                patched = roamingZoneValues.Aggregate(patched, (snap, entry) => RadioCodeplugPatcher.ApplyRoamingZonePatch(snap, entry.RadioIndex, entry.Values));
                patched = deleteRoamingZoneIndices.Aggregate(patched, RadioCodeplugPatcher.ApplyRoamingZoneDelete);

                if (talkgroupWhitelistValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyTalkgroupWhitelistPatch(patched, talkgroupWhitelistValues);
                }

                if (digitalContactWhitelistValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyDigitalContactWhitelistPatch(patched, digitalContactWhitelistValues);
                }

                // No "AddMissing" step needed for encryption keys, unlike
                // Channels/Zones/ScanLists/AM Air/AM Zones/Prefabricated SMS
                // above - RadioCodeplugRawSnapshot.
                // Capture always captures the full AES/ARC4/Basic regions
                // unconditionally (they're small, flat, bitmap-free tables),
                // regardless of IncludeEncryptionKeysList.
                patched = dirtyDigitalCodes.Aggregate(patched, (snap, key) => RadioCodeplugPatcher.ApplyBasicCodePatch(snap, key.Number, key.EncryptionId));
                patched = dirtyArc4Keys.Aggregate(patched, (snap, key) => key.EncryptionKey == "Off"
                    ? RadioCodeplugPatcher.ApplyArc4KeyClearPatch(snap, key.Number)
                    : RadioCodeplugPatcher.ApplyArc4KeyPatch(snap, key.Number, key.EncryptionKey));
                patched = dirtyAesKeys.Aggregate(patched, (snap, key) => key.EncryptionId == "Off"
                    ? RadioCodeplugPatcher.ApplyAesKeyClearPatch(snap, key.Number)
                    : RadioCodeplugPatcher.ApplyAesKeyPatch(snap, key.Number, key.EncryptionId));
                if (optionalSettingsPatch is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyOptionalSettingsPatch(patched, optionalSettingsPatch);
                }

                if (talkAliasSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyTalkAliasSettingsPatch(patched, talkAliasSettingsValues);
                }

                if (alarmSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyAlarmSettingsPatch(patched, alarmSettingsValues);
                }

                if (aprsSettingsValues is not null)
                {
                    patched = RadioCodeplugPatcher.ApplyAprsSettingsPatch(patched, aprsSettingsValues);
                }

                if (digitalContactsToWrite is not null)
                {
                    progress.Report("Writing Digital Contacts (whole list)...");
                    try
                    {
                        DigitalContactWriter.Write(connection, digitalContactsToWrite);
                    }
                    catch (Exception ex) when (ex is RadioWriteFailedException or RadioReadVerificationFailedException)
                    {
                        // Same "convert to a failed result, don't throw"
                        // handling RadioCodeplugWriter.Write does internally
                        // for the main snapshot write below - this call sits
                        // outside that method's own try/catch since it's a
                        // separate write against a region the snapshot
                        // pipeline doesn't cover (see DigitalContactWriter's
                        // own doc comment).
                        return (new CodeplugWriteResult { Success = false, Error = ex.Message }, patched);
                    }
                }

                var writeResult = RadioCodeplugWriter.Write(connection, portName, patched, progress);
                return (writeResult, patched);
            });

            if (!result.Success)
            {
                RadioWriteStatusText = AppendVoxHint($"Write failed: {result.Error}");
                return;
            }

            if (result.Mismatches.Count > 0)
            {
                RadioWriteStatusText = $"Write verification FAILED: {result.Mismatches.Count} byte(s) did not match after read-back. The radio's actual state is uncertain - re-read before trying again.";
                foreach (var (address, offset) in result.Mismatches)
                {
                    RadioWriteWarnings.Add($"Region 0x{address:X7} offset 0x{offset:X} did not verify against the intended write.");
                }

                // Don't trust the cache going forward - the radio's actual
                // state is now uncertain, so force a fresh Read From Radio
                // before any further write is allowed.
                _cachedRadioSnapshot = null;
                return;
            }

            // The write's own verification just confirmed patchedSnapshot's
            // bytes match the radio exactly - reuse it as the base for the
            // NEXT write, so a chain of writes never needs to re-read.
            _cachedRadioSnapshot = patchedSnapshot;
            if (digitalContactsToWrite is not null)
            {
                _digitalContactsDirty = false;
            }

            RadioWriteStatusText = $"Write verified: {dirtyChannels.Count} channel(s) written, {deleteIndices.Count} deleted, {dirtyZones.Count} zone(s) written, {deleteZoneIndices.Count} deleted, {dirtyScanLists.Count} scan list(s) written, {deleteScanListIndices.Count} deleted, {dirtyAmAir.Count} AM Air channel(s) written, {deleteAmAirIndices.Count} deleted, {dirtyAmZones.Count} AM Zone(s) written, {deleteAmZoneIndices.Count} deleted, {dirtyPrefabricatedSms.Count} prefabricated SMS written, {deletePrefabricatedSmsIndices.Count} deleted, {dirtyFmChannels.Count} FM channel(s) written, {deleteFmChannelIndices.Count} deleted, {dirtyAutoRepeaterOffsets.Count} Auto Repeater Offset(s) written, {deleteAutoRepeaterOffsetIndices.Count} deleted, {dirtyAnalogAddresses.Count} Analog Address(es) written, {deleteAnalogAddressIndices.Count} deleted, {dirtyQdc1200Ids.Count} QDC 1200 ID(s) written, {deleteQdc1200IdIndices.Count} deleted, QDC 1200 Settings {(qdc1200SettingsDirty ? "written" : "unchanged")}, {dirtyAnalogQuickCalls.Count} Analog Quick Call(s) written, {deleteAnalogQuickCallIndices.Count} deleted, {dirtyStateInformation.Count} State Information slot(s) written, {deleteStateInformationIndices.Count} deleted, {dirtyHotKeys.Count} Hot Key(s) written, {dirtyQdcAddresses.Count} QDC Address(es) written, {deleteQdcAddressIndices.Count} deleted, {dirtyFiveToneIds.Count} 5Tone ID(s) written, {deleteFiveToneIdIndices.Count} deleted, 5Tone Settings {(fiveToneSettingsDirty ? "written" : "unchanged")}, {dirtyTwoToneEncodeEntries.Count} 2Tone Encode entr{(dirtyTwoToneEncodeEntries.Count == 1 ? "y" : "ies")} written, {deleteTwoToneEncodeIndices.Count} deleted, {dirtyTwoToneDecodeEntries.Count} 2Tone Decode entr{(dirtyTwoToneDecodeEntries.Count == 1 ? "y" : "ies")} written, {deleteTwoToneDecodeIndices.Count} deleted, 2Tone Encode Settings {(twoToneEncodeSettingsDirty ? "written" : "unchanged")}, {dirtyDtmfEncodeEntries.Count} DTMF Encode entr{(dirtyDtmfEncodeEntries.Count == 1 ? "y" : "ies")} written, DTMF Settings {(dtmfSettingsDirty ? "written" : "unchanged")}, {dirtyRadioIds.Count} Radio ID(s) written, {deleteRadioIdIndices.Count} deleted, Master ID {(masterIdDirty ? "written" : "unchanged")}, {dirtyTalkgroups.Count} Talkgroup(s) written, {deleteTalkgroupIndices.Count} deleted, {dirtyReceiveGroupLists.Count} Receive Group List(s) written, {deleteReceiveGroupListIndices.Count} deleted, {dirtyRoamingChannels.Count} Roaming Channel(s) written, {deleteRoamingChannelIndices.Count} deleted, {dirtyRoamingZones.Count} Roaming Zone(s) written, {deleteRoamingZoneIndices.Count} deleted, {dirtyDigitalCodes.Count} digital code(s) written,{dirtyArc4Keys.Count} ARC4 key(s) written, {dirtyAesKeys.Count} AES key(s) written, Power-on settings {(optionalSettingsDirty ? "written" : "unchanged")}, Alarm Settings {(alarmSettingsDirty ? "written" : "unchanged")}, Talk Alias Settings {(talkAliasSettingsValues is not null ? "written" : "unchanged")}, Talkgroup Whitelist {(talkgroupWhitelistValues is not null ? $"written ({talkgroupWhitelistValues.Count} total)" : "unchanged")}, Digital Contact Whitelist {(digitalContactWhitelistValues is not null ? $"written ({digitalContactWhitelistValues.Count} total)" : "unchanged")}, Digital Contacts {(digitalContactsToWrite is not null ? $"written ({digitalContactsToWrite.Count} total)" : "unchanged")}, the whole codeplug's read-back matches exactly.";
            foreach (var radioIndex in deleteIndices)
            {
                _pendingDeleteRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteZoneIndices)
            {
                _pendingDeleteZoneRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteScanListIndices)
            {
                _pendingDeleteScanListRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteAmAirIndices)
            {
                _pendingDeleteAmAirRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteAmZoneIndices)
            {
                _pendingDeleteAmZoneRadioIndices.Remove(radioIndex);
            }

            foreach (var slotId in deletePrefabricatedSmsIndices)
            {
                _pendingDeletePrefabricatedSmsIndices.Remove(slotId);
            }

            foreach (var radioIndex in deleteFmChannelIndices)
            {
                _pendingDeleteFmChannelRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteAutoRepeaterOffsetIndices)
            {
                _pendingDeleteAutoRepeaterOffsetIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteAnalogAddressIndices)
            {
                _pendingDeleteAnalogAddressRadioIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteQdc1200IdIndices)
            {
                _pendingDeleteQdc1200IdIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteAnalogQuickCallIndices)
            {
                _pendingDeleteAnalogQuickCallIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteStateInformationIndices)
            {
                _pendingDeleteStateInformationIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteQdcAddressIndices)
            {
                _pendingDeleteQdcAddressIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteFiveToneIdIndices)
            {
                _pendingDeleteFiveToneIdIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteTwoToneEncodeIndices)
            {
                _pendingDeleteTwoToneEncodeIndices.Remove(radioIndex);
            }

            foreach (var radioIndex in deleteTwoToneDecodeIndices)
            {
                _pendingDeleteTwoToneDecodeIndices.Remove(radioIndex);
            }

            foreach (var channel in dirtyChannels)
            {
                // Radio-write baseline only - deliberately NOT MarkClean(),
                // so a Save that happened (or happens later) never affects
                // what Write-to-Radio thinks is pending, and vice versa.
                channel.MarkRadioSynced();
            }

            foreach (var zone in dirtyZones)
            {
                zone.MarkRadioSynced();
            }

            foreach (var scanList in dirtyScanLists)
            {
                scanList.MarkRadioSynced();
            }

            foreach (var amAir in dirtyAmAir)
            {
                amAir.MarkRadioSynced();
            }

            foreach (var amZone in dirtyAmZones)
            {
                amZone.MarkRadioSynced();
            }

            foreach (var sms in dirtyPrefabricatedSms)
            {
                sms.MarkRadioSynced();
            }

            foreach (var fmChannel in dirtyFmChannels)
            {
                fmChannel.MarkRadioSynced();
            }

            foreach (var autoRepeaterOffset in dirtyAutoRepeaterOffsets)
            {
                autoRepeaterOffset.MarkRadioSynced();
            }

            foreach (var analogAddress in dirtyAnalogAddresses)
            {
                analogAddress.MarkRadioSynced();
            }

            foreach (var gpsRoaming in dirtyGpsRoaming)
            {
                gpsRoaming.MarkRadioSynced();
            }

            foreach (var qdc1200Id in dirtyQdc1200Ids)
            {
                qdc1200Id.MarkRadioSynced();
            }

            if (qdc1200SettingsDirty)
            {
                Qdc1200Settings.MarkRadioSynced();
            }

            foreach (var analogQuickCall in dirtyAnalogQuickCalls)
            {
                analogQuickCall.MarkRadioSynced();
            }

            foreach (var stateInformation in dirtyStateInformation)
            {
                stateInformation.MarkRadioSynced();
            }

            foreach (var (hotKey, _) in dirtyHotKeys)
            {
                hotKey.MarkRadioSynced();
            }

            foreach (var qdcAddress in dirtyQdcAddresses)
            {
                qdcAddress.MarkRadioSynced();
            }

            foreach (var fiveToneId in dirtyFiveToneIds)
            {
                fiveToneId.MarkRadioSynced();
            }

            if (fiveToneSettingsDirty)
            {
                FiveToneSettings.MarkRadioSynced();
            }

            foreach (var entry in dirtyTwoToneEncodeEntries)
            {
                entry.MarkRadioSynced();
            }

            foreach (var entry in dirtyTwoToneDecodeEntries)
            {
                entry.MarkRadioSynced();
            }

            if (twoToneEncodeSettingsDirty)
            {
                TwoToneEncodeSettings.MarkRadioSynced();
            }

            foreach (var entry in dirtyDtmfEncodeEntries)
            {
                entry.MarkRadioSynced();
            }

            if (dtmfSettingsDirty)
            {
                DtmfSettings.MarkRadioSynced();
            }

            foreach (var radioId in dirtyRadioIds)
            {
                radioId.MarkRadioSynced();
            }

            foreach (var radioIndex in deleteRadioIdIndices)
            {
                _pendingDeleteRadioIdIndices.Remove(radioIndex);
            }

            if (masterIdDirty)
            {
                MasterId.MarkRadioSynced();
            }

            foreach (var talkgroup in dirtyTalkgroups)
            {
                talkgroup.MarkRadioSynced();
            }

            foreach (var radioIndex in deleteTalkgroupIndices)
            {
                _pendingDeleteTalkgroupIndices.Remove(radioIndex);
            }

            foreach (var receiveGroupList in dirtyReceiveGroupLists)
            {
                receiveGroupList.MarkRadioSynced();
            }

            foreach (var radioIndex in deleteReceiveGroupListIndices)
            {
                _pendingDeleteReceiveGroupListIndices.Remove(radioIndex);
            }

            foreach (var roamingChannel in dirtyRoamingChannels)
            {
                roamingChannel.MarkRadioSynced();
            }

            foreach (var radioIndex in deleteRoamingChannelIndices)
            {
                _pendingDeleteRoamingChannelIndices.Remove(radioIndex);
            }

            foreach (var roamingZone in dirtyRoamingZones)
            {
                roamingZone.MarkRadioSynced();
            }

            foreach (var radioIndex in deleteRoamingZoneIndices)
            {
                _pendingDeleteRoamingZoneIndices.Remove(radioIndex);
            }

            // Whole-list rewrite (see TalkgroupWhitelistCodec's own doc
            // comment) - unlike the per-record patches above, EVERY current
            // entry was just rewritten, not only the ones that were
            // individually dirty, so every entry (and the count itself,
            // covering a pure removal with no other edits) gets marked
            // synced.
            if (talkgroupWhitelistDirty)
            {
                foreach (var entry in TalkgroupWhitelist)
                {
                    entry.MarkRadioSynced();
                }

                _talkgroupWhitelistSyncedCount = TalkgroupWhitelist.Count;
            }

            if (digitalContactWhitelistDirty)
            {
                foreach (var entry in DigitalContactWhitelist)
                {
                    entry.MarkRadioSynced();
                }

                _digitalContactWhitelistSyncedCount = DigitalContactWhitelist.Count;
            }

            foreach (var key in dirtyDigitalCodes)
            {
                key.MarkRadioSynced();
            }

            foreach (var key in dirtyArc4Keys)
            {
                key.MarkRadioSynced();
            }

            foreach (var key in dirtyAesKeys)
            {
                key.MarkRadioSynced();
            }

            if (alarmSettingsDirty)
            {
                AlarmSettings.MarkRadioSynced();
            }

            if (aprsSettingsDirty)
            {
                AprsSettings.MarkRadioSynced();
            }

            if (talkAliasSettingsDirty)
            {
                TalkAliasSettings.MarkRadioSynced();
            }

            if (optionalSettingsDirty)
            {
                OptionalSettings.MarkRadioSynced();
                foreach (var tone in OptionalSettings.CallPermitTones.Concat(OptionalSettings.MatchEndTones).Concat(OptionalSettings.CallResetTones)
                             .Concat(OptionalSettings.UnMatchEndTones).Concat(OptionalSettings.CallAllTones))
                {
                    tone.MarkRadioSynced();
                }
            }

            WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            RadioWriteStatusText = AppendVoxHint($"Write failed: {exception.Message}");
        }
        finally
        {
            IsWritingToRadio = false;
            WriteChangesToRadioCommand.NotifyCanExecuteChanged();
            ReadFromRadioCommand.NotifyCanExecuteChanged();
            VerifyReadSaveRoundtripCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Builds a zone's write-safe patch directly from its already-
    /// typed fields - mirrors <see cref="BuildSafeFieldPatch"/>, but every
    /// field is unconditionally safe to patch (no bit-sharing risk - see
    /// ZoneCodec.ZoneFieldPatch's doc comment), so there's no per-field
    /// error slot to thread through.</summary>
    private static (ZoneCodec.ZoneFieldPatch Patch, List<string> SummaryLines) BuildSafeZoneFieldPatch(ZoneEntry zone)
    {
        var summary = new List<string>();

        string? name = null;
        if (zone.IsNamePendingRadioWrite)
        {
            name = zone.Name;
            summary.Add($"Name = '{name}'");
        }

        IReadOnlyList<ushort>? channelMembers = null;
        if (zone.IsMembersPendingRadioWrite)
        {
            channelMembers = zone.Members.Select(c => (ushort)(c.Number - 1)).ToList();
            summary.Add($"Members = {channelMembers.Count} channel(s)");
        }

        // AChannelIndex/BChannelIndex encode a 0-based POSITION within this
        // zone's own Members list, NOT the channel's global radio index
        // (Number - 1) - corrected 2026-08-01, see RadioReadMapper.MapZones'
        // matching doc comment for the live differential test that found
        // this. The original 2026-07-19 write confirmation used a test zone
        // where every member's channel number happened to equal its
        // position + 1, so the old (wrong) Number-1 encoding coincidentally
        // produced the right bytes there - it silently breaks for any zone
        // whose members aren't numbered in exact position order, which is
        // most real zones. zone.Members.IndexOf is safe here for the same
        // reason the old code assumed AChannel is always a real value: see
        // this method's other doc comment on ReassignZoneChannels below -
        // AChannel/BChannel, when set, are always one of zone.Members.
        // The 0xFFFF ("no channel") fallback below is defensive only, not a
        // real case: confirmed 2026-07-19 (directly observed in the real
        // vendor CPS) that A/B can never actually be cleared by a user -
        // ReassignZoneChannels guarantees AChannel is always set the moment
        // a zone has >=1 member, and a zone with 0 members doesn't persist
        // at all (see that method's doc comment), so this method never
        // actually sees a null AChannel for any zone still in the Zones
        // collection.
        ushort? aChannelIndex = null;
        if (zone.IsAChannelPendingRadioWrite)
        {
            aChannelIndex = zone.AChannel is { } aChannel ? (ushort)zone.Members.IndexOf(aChannel) : (ushort)0xFFFF;
            summary.Add($"A Channel = '{zone.AChannel?.Name ?? "(none)"}'");
        }

        ushort? bChannelIndex = null;
        if (zone.IsBChannelPendingRadioWrite)
        {
            bChannelIndex = zone.BChannel is { } bChannel ? (ushort)zone.Members.IndexOf(bChannel) : (ushort)0xFFFF;
            summary.Add($"B Channel = '{zone.BChannel?.Name ?? "(none)"}'");
        }

        bool? isHidden = null;
        if (zone.IsHiddenPendingRadioWrite)
        {
            isHidden = zone.IsHidden;
            summary.Add($"Hide = {(zone.IsHidden ? "On" : "Off")}");
        }

        var patch = new ZoneCodec.ZoneFieldPatch
        {
            Name = name,
            ChannelMembers = channelMembers,
            AChannelIndex = aChannelIndex,
            BChannelIndex = bChannelIndex,
            IsHidden = isHidden
        };

        return (patch, summary);
    }

    /// <summary>Builds the full target state for a scan list's radio record
    /// directly from its already-typed fields. Unlike Zone/Channel, this
    /// doesn't build a nullable-per-field patch - ScanListCodec.Encode
    /// always re-encodes every field unconditionally (see its doc comment
    /// for why that's safe here), so this just captures the live values;
    /// the per-field IsXxxPendingRadioWrite checks below are only used to
    /// build the human-readable summary, not to decide what gets
    /// written.</summary>
    private static (ScanListCodec.DecodedScanList Values, List<string> SummaryLines) BuildSafeScanListValues(ScanListEntry scanList, int radioIndex)
    {
        var summary = new List<string>();

        if (scanList.IsNamePendingRadioWrite)
        {
            summary.Add($"Name = '{scanList.Name}'");
        }

        if (scanList.IsPriorityChannelSelectPendingRadioWrite)
        {
            summary.Add($"Priority Channel Select = '{scanList.PriorityChannelSelectText}'");
        }

        if (scanList.IsPriorityChannel1PendingRadioWrite)
        {
            summary.Add($"Priority Channel 1 = '{scanList.PriorityChannel1?.Name ?? "None"}'");
        }

        if (scanList.IsPriorityChannel2PendingRadioWrite)
        {
            summary.Add($"Priority Channel 2 = '{scanList.PriorityChannel2?.Name ?? "None"}'");
        }

        if (scanList.IsLookbackTimeAPendingRadioWrite)
        {
            summary.Add($"Lookback Time A = {scanList.LookbackTimeA}");
        }

        if (scanList.IsLookbackTimeBPendingRadioWrite)
        {
            summary.Add($"Lookback Time B = {scanList.LookbackTimeB}");
        }

        if (scanList.IsDropoutDelayTimePendingRadioWrite)
        {
            summary.Add($"Dropout Delay Time = {scanList.DropoutDelayTime}");
        }

        if (scanList.IsDwellTimePendingRadioWrite)
        {
            summary.Add($"Dwell Time = {scanList.DwellTime}");
        }

        if (scanList.IsRevertChannelPendingRadioWrite)
        {
            summary.Add($"Revert Channel = '{scanList.RevertChannelText}'");
        }

        if (scanList.IsMembersPendingRadioWrite)
        {
            summary.Add($"Members = {scanList.Members.Count} channel(s)");
        }

        var values = new ScanListCodec.DecodedScanList(radioIndex)
        {
            PriorityChannelSelect = scanList.PriorityChannelSelect,
            // Raw wire value is the 1-based channel number itself (see
            // ScanListCodec.Decode's doc comment) - do NOT subtract 1 here,
            // unlike ChannelMemberIndexes below which does want a 0-based
            // radio index.
            PriorityChannel1 = scanList.PriorityChannel1 is { } p1 ? p1.Number : null,
            PriorityChannel2 = scanList.PriorityChannel2 is { } p2 ? p2.Number : null,
            LookbackTimeA = scanList.LookbackTimeA,
            LookbackTimeB = scanList.LookbackTimeB,
            DropoutDelayTime = scanList.DropoutDelayTime,
            DwellTime = scanList.DwellTime,
            Name = scanList.Name,
            ChannelMemberIndexes = scanList.Members.Select(c => c.Number - 1).ToList(),
            RevertChannel = scanList.RevertChannel
        };

        return (values, summary);
    }

    /// <summary>Builds the full target state for an AM Air channel's radio
    /// record - same "always re-encode every field" reasoning as
    /// <see cref="BuildSafeScanListValues"/> (Frequency/Name don't share
    /// bits, confirmed by AmAirCodec.Encode's doc comment).</summary>
    private static (AmAirCodec.DecodedAmAir Values, List<string> SummaryLines) BuildSafeAmAirValues(AmAirEntry amAir, int radioIndex)
    {
        var summary = new List<string>();

        if (amAir.IsFrequencyMhzPendingRadioWrite)
        {
            summary.Add($"Frequency = {amAir.FrequencyMhz} MHz");
        }

        if (amAir.IsNamePendingRadioWrite)
        {
            summary.Add($"Name = '{amAir.Name}'");
        }

        var values = new AmAirCodec.DecodedAmAir(radioIndex)
        {
            FrequencyMHz = amAir.FrequencyMhz,
            Name = amAir.Name
        };

        return (values, summary);
    }

    /// <summary>Builds the full target state for an Analog Address Book
    /// entry's radio record - same "always re-encode every field"
    /// reasoning as <see cref="BuildSafeAmAirValues"/> (Address Number/Name
    /// don't share bits, confirmed by AnalogAddressCodec.Encode's doc
    /// comment).</summary>
    private static (AnalogAddressCodec.DecodedAnalogAddress Values, List<string> SummaryLines) BuildSafeAnalogAddressValues(AnalogAddressEntry analogAddress, int radioIndex)
    {
        var summary = new List<string>();

        if (analogAddress.IsAddressNumberPendingRadioWrite)
        {
            summary.Add($"Address Number = {analogAddress.AddressNumber}");
        }

        if (analogAddress.IsNamePendingRadioWrite)
        {
            summary.Add($"Name = '{analogAddress.Name}'");
        }

        var values = new AnalogAddressCodec.DecodedAnalogAddress(radioIndex)
        {
            Number = analogAddress.AddressNumber,
            Name = analogAddress.Name
        };

        return (values, summary);
    }

    /// <summary>Builds the full target state for a QDC 1200 ID table entry's
    /// radio record. Qdc1200IdEntry uses a single aggregate
    /// HasAnyPendingRadioWrite flag rather than per-field IsXPendingRadioWrite
    /// booleans (see its own class doc comment - no per-field UI indicator to
    /// drive), so unlike BuildSafeAnalogAddressValues the summary line here
    /// always lists the full current state rather than just what changed.</summary>
    private static (Qdc1200IdCodec.DecodedQdc1200Id Values, List<string> SummaryLines) BuildSafeQdc1200IdValues(Qdc1200IdEntry qdc1200Id, int radioIndex)
    {
        var summary = new List<string>
        {
            $"Call Type = {qdc1200Id.CallTypeText}",
            $"Type = {(qdc1200Id.IsTypeEnabled ? qdc1200Id.TypeText : "(n/a)")}",
            $"Name = '{qdc1200Id.Name}'"
        };

        var values = new Qdc1200IdCodec.DecodedQdc1200Id(radioIndex)
        {
            Type = qdc1200Id.Type,
            CallType = qdc1200Id.CallType,
            NeedToAnswer = qdc1200Id.NeedToAnswer,
            GroupCallId = qdc1200Id.GroupCallId,
            PrivateCallId = qdc1200Id.PrivateCallId,
            Name = qdc1200Id.Name
        };

        return (values, summary);
    }

    /// <summary>Builds the full target state for a 5Tone ID table row -
    /// always re-encodes every confirmed field (Standard/Time Of Encode
    /// Tone/Name/Special Call/Encode ID), same "no bit-sharing" reasoning
    /// as every other always-re-encode codec in this app.</summary>
    private static (FiveToneIdCodec.DecodedFiveToneId Values, List<string> SummaryLines) BuildSafeFiveToneIdValues(FiveToneIdEntry fiveToneId, int radioIndex)
    {
        var summary = new List<string>
        {
            $"Standard = {fiveToneId.StandardText}",
            $"Time Of Encode Tone = {fiveToneId.TimeOfEncodeTone}",
            $"Name = '{fiveToneId.Name}'"
        };

        var values = new FiveToneIdCodec.DecodedFiveToneId(radioIndex)
        {
            Standard = fiveToneId.Standard,
            TimeOfEncodeTone = (byte)fiveToneId.TimeOfEncodeTone,
            Name = fiveToneId.Name,
            EncodeId = fiveToneId.EncodeId,
            SpecialCall = ToFiveToneSpecialCallCodecValues(fiveToneId.SpecialCall)
        };

        return (values, summary);
    }

    /// <summary>Translates the Models-layer FiveToneSpecialCallEntry (byte
    /// CallingType constants, IntervalCharacter as an index into its own
    /// option list) into the Services/Radio/Codecs-layer
    /// FiveToneSpecialCallCodecValues the codecs actually take - kept as a
    /// translation at this boundary rather than referencing Models types
    /// from the Codecs namespace, same layering FiveToneIdCodec's own doc
    /// comment already establishes.</summary>
    private static FiveToneSpecialCallCodecValues ToFiveToneSpecialCallCodecValues(FiveToneSpecialCallEntry specialCall)
    {
        var callingType = specialCall.CallingType switch
        {
            FiveToneSpecialCallEntry.CallingTypeSendMessage => FiveToneCallingType.SendMessage,
            FiveToneSpecialCallEntry.CallingTypePttId => FiveToneCallingType.PttId,
            _ => FiveToneCallingType.Ani
        };
        var intervalSuffix = specialCall.IsAni && specialCall.IntervalCharacter != 0
            ? FiveToneSpecialCallEntry.IntervalCharacterOptions[specialCall.IntervalCharacter]
            : "";

        return new FiveToneSpecialCallCodecValues(callingType, specialCall.OtherSideId, specialCall.Message, intervalSuffix) { IsConfigured = specialCall.IsConfigured };
    }

    /// <summary>Builds the full target state for an AM Zone's radio record(s) -
    /// same "always re-encode every field" reasoning as
    /// <see cref="BuildSafeScanListValues"/>. AChannel/ScanChannelMembers
    /// live outside the main 0x80-byte record (see AmZoneCodec's doc
    /// comment) but are still always re-encoded here for the same
    /// no-bit-sharing reason.</summary>
    private static (AmZoneCodec.DecodedAmZone Values, List<string> SummaryLines) BuildSafeAmZoneValues(AmZoneEntry amZone, int radioIndex)
    {
        var summary = new List<string>();

        if (amZone.IsNamePendingRadioWrite)
        {
            summary.Add($"Name = '{amZone.Name}'");
        }

        if (amZone.IsAChannelPendingRadioWrite)
        {
            summary.Add($"A Channel = '{amZone.AChannel?.Name ?? "(none)"}'");
        }

        if (amZone.IsMembersPendingRadioWrite)
        {
            summary.Add($"Members = {amZone.Members.Count} channel(s)");
        }

        if (amZone.IsScanChannelMembersPendingRadioWrite)
        {
            summary.Add($"Scan Channel Members = {amZone.ScanChannelMembers.Count} channel(s)");
        }

        var values = new AmZoneCodec.DecodedAmZone(radioIndex)
        {
            Name = amZone.Name,
            // AChannel is effectively mandatory (ReassignAmZoneChannel keeps
            // it populated whenever Members is non-empty, and an empty-
            // Members zone is auto-removed - see RemoveAmZoneMembers) - the
            // 0xFFFF fallback here is defensive only, matching ZoneCodec's
            // own AChannel/BChannel reasoning.
            AChannelIndex = amZone.AChannel is { } aChannel ? aChannel.Number - 1 : 0xFFFF,
            MemberChannelIndexes = amZone.Members.Select(c => c.Number - 1).ToList(),
            ScanChannelIndexes = amZone.ScanChannelMembers.Select(c => c.Number - 1).ToList()
        };

        return (values, summary);
    }

    /// <summary>Builds the full target state for an FM broadcast channel's
    /// radio record - same "always re-encode every field" reasoning as
    /// <see cref="BuildSafeAmAirValues"/> (Frequency/Name don't share bits,
    /// confirmed by FmChannelCodec.Encode's doc comment; ScanAdd lives in a
    /// separate bitmap bit, patched independently by
    /// RadioCodeplugPatcher.ApplyFmChannelPatch).</summary>
    private static (FmChannelCodec.DecodedFmChannel Values, List<string> SummaryLines) BuildSafeFmChannelValues(FmChannelEntry fmChannel, int radioIndex)
    {
        var summary = new List<string>();

        if (fmChannel.IsFrequencyMhzPendingRadioWrite)
        {
            summary.Add($"Frequency = {fmChannel.FrequencyMhz} MHz");
        }

        if (fmChannel.IsNamePendingRadioWrite)
        {
            summary.Add($"Name = '{fmChannel.Name}'");
        }

        if (fmChannel.IsScanAddPendingRadioWrite)
        {
            summary.Add($"Scan Add = {(fmChannel.ScanAdd ? "On" : "Off")}");
        }

        var values = new FmChannelCodec.DecodedFmChannel(radioIndex)
        {
            FrequencyMHz = fmChannel.FrequencyMhz,
            Name = fmChannel.Name,
            ScanAdd = fmChannel.ScanAdd
        };

        return (values, summary);
    }

    /// <summary>Builds the write-safe patch directly from <paramref name="channel"/>'s
    /// already-typed, already-valid fields - no string parsing needed
    /// anymore (the canonical model can't hold an invalid value in the
    /// first place; see <c>Models/ChannelEntry.cs</c>'s doc comment). The
    /// `Error` return slot is kept for signature compatibility with the
    /// caller's fail-fast-on-any-error loop, but nothing here can actually
    /// produce one today.</summary>
    private static (ChannelCodec.ChannelFieldPatch Patch, List<string> SummaryLines, string? Error) BuildSafeFieldPatch(ChannelEntry channel)
    {
        var summary = new List<string>();

        double? rxFrequency = null;
        double? offsetMhz = null;
        byte? offsetDirection = null;
        if (channel.IsReceiveFrequencyPendingRadioWrite || channel.IsTransmitFrequencyPendingRadioWrite)
        {
            rxFrequency = channel.RxFrequencyMHz;
            offsetMhz = channel.OffsetMHz;
            offsetDirection = channel.OffsetDirection;
            summary.Add($"RX {channel.RxFrequencyMHz:F5} MHz, TX {channel.ComputeTransmitFrequencyMHz():F5} MHz");
        }

        string? name = null;
        if (channel.IsNamePendingRadioWrite)
        {
            name = channel.Name;
            summary.Add($"Name = '{name}'");
        }

        byte? ctcssDcsEncode = null;
        if (channel.IsCtcssEncodePendingRadioWrite)
        {
            ctcssDcsEncode = channel.CtcssDcsEncode;
            summary.Add($"CTCSS/DCS Encode = '{channel.CtcssEncodeSelection}'");
        }

        byte? ctcssDcsDecode = null;
        if (channel.IsCtcssDecodePendingRadioWrite)
        {
            ctcssDcsDecode = channel.CtcssDcsDecode;
            summary.Add($"CTCSS/DCS Decode = '{channel.CtcssDecodeSelection}'");
        }

        byte? squelchMode = null;
        if (channel.IsSquelchModePendingRadioWrite)
        {
            squelchMode = channel.SquelchMode;
            summary.Add($"Squelch Mode = '{channel.SquelchModeSelection}'");
        }

        byte? optionalSignal = null;
        if (channel.IsOptionalSignalPendingRadioWrite)
        {
            optionalSignal = channel.OptionalSignal;
            summary.Add($"Optional Signal = '{channel.OptionalSignalSelection}'");
        }

        byte? busyLock = null;
        if (channel.IsBusyLockTxPermitPendingRadioWrite)
        {
            busyLock = channel.BusyLock;
            summary.Add($"Busy-Lock/TX-Permit = '{channel.BusyLockTxPermitSelection}'");
        }

        ushort? contactIndex = null;
        if (channel.IsContactPendingRadioWrite)
        {
            contactIndex = channel.ContactIndex;
            summary.Add($"Contact/Talk Group = '{channel.ContactDisplayName}'");
        }

        byte? radioIdIndex = null;
        if (channel.IsRadioIdPendingRadioWrite)
        {
            radioIdIndex = (byte)channel.RadioIdIndex;
            summary.Add($"Radio ID = '{channel.RadioIdDisplayName}'");
        }

        byte? receiveGroupCallListIndex = null;
        if (channel.IsReceiveGroupListPendingRadioWrite)
        {
            receiveGroupCallListIndex = (byte)channel.ReceiveGroupListIndex;
            summary.Add($"Receive Group List = '{channel.ReceiveGroupListDisplayName}'");
        }

        byte? pttId = null;
        if (channel.IsPttIdPendingRadioWrite)
        {
            pttId = channel.PttId;
            summary.Add($"PTT ID = '{channel.PttIdSelection}'");
        }

        byte? channelType = null;
        if (channel.IsChannelTypePendingRadioWrite)
        {
            channelType = channel.ChannelType;
            summary.Add($"Channel Type = '{channel.ChannelTypeSelection}'");
        }

        byte? transmitPower = null;
        if (channel.IsTransmitPowerPendingRadioWrite)
        {
            transmitPower = channel.TransmitPower;
            summary.Add($"Transmit Power = '{channel.TransmitPowerSelection}'");
        }

        byte? bandwidth = null;
        if (channel.IsBandwidthPendingRadioWrite)
        {
            bandwidth = channel.Bandwidth;
            summary.Add($"Bandwidth = '{channel.BandwidthSelection}'");
        }

        bool? talkAround = null;
        if (channel.IsTalkAroundPendingRadioWrite)
        {
            talkAround = channel.TalkAround;
            summary.Add($"Talk Around = {(channel.TalkAround ? "On" : "Off")}");
        }

        bool? callConfirmation = null;
        if (channel.IsCallConfirmationPendingRadioWrite)
        {
            callConfirmation = channel.CallConfirmation;
            summary.Add($"Call Confirmation = {(channel.CallConfirmation ? "On" : "Off")}");
        }

        bool? pttProhibit = null;
        if (channel.IsPttProhibitPendingRadioWrite)
        {
            pttProhibit = channel.PttProhibit;
            summary.Add($"PTT Prohibit = {(channel.PttProhibit ? "On" : "Off")}");
        }

        bool? reverse = null;
        if (channel.IsReversePendingRadioWrite)
        {
            reverse = channel.Reverse;
            summary.Add($"Reverse = {(channel.Reverse ? "On" : "Off")}");
        }

        byte? rxColorCode = null;
        if (channel.IsColorCodePendingRadioWrite)
        {
            rxColorCode = channel.ColorCode;
            summary.Add($"RX Color Code = {channel.ColorCode}");
        }

        byte? txColorCode = null;
        if (channel.IsTxColorCodePendingRadioWrite)
        {
            txColorCode = channel.TxColorCode;
            summary.Add($"TX Color Code = {channel.TxColorCode}");
        }

        bool? workAlone = null;
        if (channel.IsWorkAlonePendingRadioWrite)
        {
            workAlone = channel.WorkAlone;
            summary.Add($"Work Alone = {(channel.WorkAlone ? "On" : "Off")}");
        }

        bool? slotSuit = null;
        if (channel.IsSlotSuitPendingRadioWrite)
        {
            slotSuit = channel.SlotSuit;
            summary.Add($"Slot Suit = {(channel.SlotSuit ? "On" : "Off")}");
        }

        bool? repeaterSlot2 = null;
        if (channel.IsRepeaterSlotPendingRadioWrite)
        {
            repeaterSlot2 = channel.RepeaterSlot2;
            summary.Add($"Slot = '{channel.RepeaterSlotText}'");
        }

        bool? smsConfirmation = null;
        if (channel.IsSmsConfirmationPendingRadioWrite)
        {
            smsConfirmation = channel.SmsConfirmation;
            summary.Add($"SMS Confirmation = {(channel.SmsConfirmation ? "On" : "Off")}");
        }

        byte? aesEncryptionIndex = null;
        if (channel.IsAesEncryptionPendingRadioWrite)
        {
            aesEncryptionIndex = channel.AesEncryptionIndex;
            summary.Add($"AES Key = '{channel.AesDigitalEncryptionText}'");
        }

        byte? arc4EncryptionKeyIndex = null;
        if (channel.IsArc4EncryptionPendingRadioWrite)
        {
            arc4EncryptionKeyIndex = channel.Arc4EncryptionKeyIndex;
            summary.Add($"ARC4 Key = '{channel.Arc4EncryptionText}'");
        }

        bool? autoScan = null;
        if (channel.IsAutoScanPendingRadioWrite)
        {
            autoScan = channel.AutoScan;
            summary.Add($"Auto Scan = {(channel.AutoScan ? "On" : "Off")}");
        }

        byte? scrambleMode = null;
        if (channel.IsScramblePendingRadioWrite)
        {
            scrambleMode = (byte)channel.ScrambleMode;
            summary.Add($"Scramble Type = '{channel.ScrambleModeSelection}'");
        }

        byte? customScrambleFrequencyIndex = null;
        if (channel.IsScrambleFrequencyPendingRadioWrite)
        {
            customScrambleFrequencyIndex = (byte)channel.CustomScrambleFrequencyIndex;
            summary.Add($"Custom Scrambler Frequency = '{channel.CustomScramblerSelection}'");
        }

        byte? digitalEncryptionIndex = null;
        if (channel.IsDigitalEncryptionPendingRadioWrite)
        {
            digitalEncryptionIndex = channel.DigitalEncryptionIndex;
            summary.Add($"Digital Key = '{channel.DigitalEncryptionText}'");
        }

        byte? correctFrequencyHz = null;
        if (channel.IsCorrectFrequencyHzPendingRadioWrite)
        {
            correctFrequencyHz = channel.CorrectFrequencyHz;
            summary.Add($"Correct Frequency = {channel.CorrectFrequencyHzText} Hz");
        }

        ushort? customCtcss = null;
        if (channel.IsCustomCtcssPendingRadioWrite)
        {
            customCtcss = channel.CustomCtcss;
            summary.Add($"Custom CTCSS = {channel.CustomCtcssText} Hz");
        }

        byte? ctcssEncodeTone = null;
        ushort? dcsEncodeTone = null;
        if (channel.IsCtcssEncodeTonePendingRadioWrite || channel.IsDcsEncodeTonePendingRadioWrite)
        {
            ctcssEncodeTone = channel.CtcssEncodeTone;
            dcsEncodeTone = channel.DcsEncodeTone;
            summary.Add($"Encode Tone = '{channel.EncodeToneSelection}'");
        }

        byte? ctcssDecodeTone = null;
        ushort? dcsDecodeTone = null;
        if (channel.IsCtcssDecodeTonePendingRadioWrite || channel.IsDcsDecodeTonePendingRadioWrite)
        {
            ctcssDecodeTone = channel.CtcssDecodeTone;
            dcsDecodeTone = channel.DcsDecodeTone;
            summary.Add($"Decode Tone = '{channel.DecodeToneSelection}'");
        }

        byte? dmrModeDcdm = null;
        bool? dmrMode = null;
        if (channel.IsDmrModeDcdmPendingRadioWrite || channel.IsDmrModePendingRadioWrite)
        {
            dmrModeDcdm = channel.IsDmrModeDcdmPendingRadioWrite ? channel.DmrModeDcdm : null;
            dmrMode = channel.IsDmrModePendingRadioWrite ? channel.DmrMode : null;
            summary.Add($"DMR Mode = '{channel.DmrModeSelection}'");
        }

        bool? dmrCrcIgnore = null;
        if (channel.IsDmrCrcIgnorePendingRadioWrite)
        {
            dmrCrcIgnore = channel.DmrCrcIgnore;
            summary.Add($"DMR CRC Ignore = {(channel.DmrCrcIgnore ? "On" : "Off")}");
        }

        bool? sendTalkerAlias = null;
        if (channel.IsSendTalkerAliasPendingRadioWrite)
        {
            sendTalkerAlias = channel.SendTalkerAlias;
            summary.Add($"Send Talker Alias = {(channel.SendTalkerAlias ? "On" : "Off")}");
        }

        bool? smsForbid = null;
        if (channel.IsSmsForbidPendingRadioWrite)
        {
            smsForbid = channel.SmsForbid;
            summary.Add($"SMS Forbid = {(channel.SmsForbid ? "On" : "Off")}");
        }

        bool? dataAckDisable = null;
        if (channel.IsDataAckDisablePendingRadioWrite)
        {
            dataAckDisable = channel.DataAckDisable;
            summary.Add($"Data ACK Disable = {(channel.DataAckDisable ? "On" : "Off")}");
        }

        bool? excludeChannelRoaming = null;
        if (channel.IsExcludeChannelRoamingPendingRadioWrite)
        {
            excludeChannelRoaming = channel.ExcludeChannelRoaming;
            summary.Add($"Exclude Channel From Roaming = {(channel.ExcludeChannelRoaming ? "On" : "Off")}");
        }

        bool? aesRandomKey = null;
        if (channel.IsAesRandomKeyPendingRadioWrite)
        {
            aesRandomKey = channel.AesRandomKey;
            summary.Add($"AES Random Key = {(channel.AesRandomKey ? "On" : "Off")}");
        }

        bool? aesMultipleKey = null;
        if (channel.IsAesMultipleKeyPendingRadioWrite)
        {
            aesMultipleKey = channel.AesMultipleKey;
            summary.Add($"AES Multiple Key = {(channel.AesMultipleKey ? "On" : "Off")}");
        }

        bool? aprsRx = null;
        if (channel.IsAprsRxPendingRadioWrite)
        {
            aprsRx = channel.AprsRx;
            summary.Add($"APRS RX = {(channel.AprsRx ? "On" : "Off")}");
        }

        byte? dtmfIdIndex = null;
        if (channel.IsDtmfIdIndexPendingRadioWrite)
        {
            dtmfIdIndex = channel.DtmfIdIndex;
            summary.Add($"DTMF ID = '{channel.DtmfIdSelection}'");
        }

        byte? tone2IdIndex = null;
        if (channel.IsTone2IdIndexPendingRadioWrite)
        {
            tone2IdIndex = channel.Tone2IdIndex;
            summary.Add($"2Tone ID = '{channel.Tone2IdSelection}'");
        }

        byte? tone5IdIndex = null;
        if (channel.IsTone5IdIndexPendingRadioWrite)
        {
            tone5IdIndex = channel.Tone5IdIndex;
            summary.Add($"5Tone ID = '{channel.Tone5IdSelection}'");
        }

        byte? tone2Decode = null;
        if (channel.IsTone2DecodePendingRadioWrite)
        {
            tone2Decode = channel.Tone2Decode;
            summary.Add($"2Tone Decode = '{channel.Tone2DecodeSelection}'");
        }

        byte? r5ToneBot = null;
        if (channel.IsR5ToneBotPendingRadioWrite)
        {
            r5ToneBot = channel.R5ToneBot;
            summary.Add($"5Tone Bot = '{channel.R5ToneBotSelection}'");
        }

        byte? r5ToneEot = null;
        if (channel.IsR5ToneEotPendingRadioWrite)
        {
            r5ToneEot = channel.R5ToneEot;
            summary.Add($"5Tone Eot = '{channel.R5ToneEotSelection}'");
        }

        byte? qdcIdIndex = null;
        if (channel.IsQdcIdIndexPendingRadioWrite)
        {
            qdcIdIndex = channel.QdcIdIndex;
            summary.Add($"QDC1200 ID = '{channel.QdcIdSelection}'");
        }

        bool? extendEncryption = null;
        if (channel.IsExtendEncryptionPendingRadioWrite)
        {
            extendEncryption = channel.ExtendEncryption;
            summary.Add($"Extended Encryption = '{channel.ExtendEncryptionSelection}'");
        }

        bool? txInterrupt = null;
        if (channel.IsTxInterruptPendingRadioWrite)
        {
            txInterrupt = channel.TxInterrupt;
            summary.Add($"TX Interrupt = '{channel.TxInterruptSelection}'");
        }

        bool? idleTx = null;
        if (channel.IsIdleTxPendingRadioWrite)
        {
            idleTx = channel.IdleTx;
            summary.Add($"Idle TX = {(channel.IdleTx ? "On" : "Off")}");
        }

        bool? ranging = null;
        if (channel.IsRangingPendingRadioWrite)
        {
            ranging = channel.Ranging;
            summary.Add($"Ranging = {(channel.Ranging ? "On" : "Off")}");
        }

        var patch = new ChannelCodec.ChannelFieldPatch
        {
            Name = name,
            RxFrequencyMHz = rxFrequency,
            OffsetMHz = offsetMhz,
            OffsetDirection = offsetDirection,
            CtcssDcsEncode = ctcssDcsEncode,
            CtcssDcsDecode = ctcssDcsDecode,
            SquelchMode = squelchMode,
            OptionalSignal = optionalSignal,
            BusyLock = busyLock,
            ContactIndex = contactIndex,
            RadioIdIndex = radioIdIndex,
            ReceiveGroupCallListIndex = receiveGroupCallListIndex,
            PttId = pttId,
            ChannelType = channelType,
            TransmitPower = transmitPower,
            Bandwidth = bandwidth,
            TalkAround = talkAround,
            CallConfirmation = callConfirmation,
            PttProhibit = pttProhibit,
            Reverse = reverse,
            RxColorCode = rxColorCode,
            TxColorCode = txColorCode,
            WorkAlone = workAlone,
            SlotSuit = slotSuit,
            RepeaterSlot2 = repeaterSlot2,
            SmsConfirmation = smsConfirmation,
            AesEncryptionIndex = aesEncryptionIndex,
            Arc4EncryptionKeyIndex = arc4EncryptionKeyIndex,
            AutoScan = autoScan,
            ScrambleMode = scrambleMode,
            CustomScrambleFrequencyIndex = customScrambleFrequencyIndex,
            DigitalEncryptionIndex = digitalEncryptionIndex,
            CorrectFrequencyHz = correctFrequencyHz,
            CustomCtcss = customCtcss,
            CtcssEncodeTone = ctcssEncodeTone,
            CtcssDecodeTone = ctcssDecodeTone,
            DcsEncodeTone = dcsEncodeTone,
            DcsDecodeTone = dcsDecodeTone,
            DmrModeDcdm = dmrModeDcdm,
            DmrMode = dmrMode,
            DmrCrcIgnore = dmrCrcIgnore,
            SendTalkerAlias = sendTalkerAlias,
            SmsForbid = smsForbid,
            DataAckDisable = dataAckDisable,
            ExcludeChannelRoaming = excludeChannelRoaming,
            AesRandomKey = aesRandomKey,
            AesMultipleKey = aesMultipleKey,
            AprsRx = aprsRx,
            DtmfIdIndex = dtmfIdIndex,
            Tone2IdIndex = tone2IdIndex,
            Tone5IdIndex = tone5IdIndex,
            Tone2Decode = tone2Decode,
            R5ToneBot = r5ToneBot,
            R5ToneEot = r5ToneEot,
            QdcIdIndex = qdcIdIndex,
            ExtendEncryption = extendEncryption,
            TxInterrupt = txInterrupt,
            IdleTx = idleTx,
            Ranging = ranging
        };

        return (patch, summary, null);
    }

    /// <summary>Builds the full Alarm Settings record directly from
    /// <paramref name="alarmSettings"/>'s already-typed fields - unlike
    /// OptionalSettings' per-field nullable patch, this always re-encodes
    /// every field it owns (a plain values object, same shape as
    /// AmZone/FmChannel/AutoRepeaterOffset's own write path), since Alarm
    /// Settings is a small, fully-decoded single record with no unconfirmed
    /// bytes mixed in among the fields this app models - the RMW encode
    /// functions still only ever touch the specific offsets they own,
    /// leaving every other byte in each of the 3 regions untouched (see
    /// AlarmSettingsCodec.EncodeD3483000's doc comment). QdcCallType is
    /// deliberately excluded - it has no byte address at all.</summary>
    private static DigitalContactCodec.DecodedDigitalContact ToDecodedDigitalContact(DigitalContactEntry entry) => new(entry.Index)
    {
        CallType = (byte)entry.CallType,
        CallAlert = entry.CallAlert,
        IsFriend = entry.IsFriend,
        RadioId = entry.RadioId,
        Name = entry.Name,
        City = entry.City,
        Callsign = entry.Callsign,
        State = entry.State,
        Country = entry.Country,
        Remarks = entry.Remarks
    };

    /// <summary>Every field written here matches an offset confirmed by a
    /// live differential write 2026-08-15 (see Capture_Findings.md) except:
    /// Filters (deliberately excluded, meaning unconfirmed - not part of
    /// <see cref="AprsSettingsCodec.DecodedAprsSettings"/>'s writable
    /// fields at all), and Fix4-8's Ew / Digital Report slots 2-8's
    /// Channel/CallType/Slot (extended from a strongly-confirmed pattern,
    /// not independently live-tested for every slot - see
    /// AprsSettingsCodec.Encode's own doc comment).</summary>
    private static AprsSettingsCodec.DecodedAprsSettings BuildAprsSettingsValues(AprsSettingsEntry aprsSettings, List<string> summaryLines)
    {
        summaryLines.Add("APRS Settings: updated");

        return new AprsSettingsCodec.DecodedAprsSettings
        {
            TxFreq1MHz = aprsSettings.TxFreq1Mhz,
            TxDelay = aprsSettings.TxDelay,
            SendSubtone = aprsSettings.SendSubtone,
            Ctcss = aprsSettings.Ctcss,
            Dcs = aprsSettings.Dcs,
            ManualTxInterval = aprsSettings.ManualTxInterval,
            AutoTxInterval = aprsSettings.AutoTxInterval,
            TxTone = aprsSettings.TxTone,
            FixedLocationBeacon = aprsSettings.FixedLocationBeacon,

            Fix1Lat = aprsSettings.Fix1Lat,
            Fix1Ns = aprsSettings.Fix1Ns,
            Fix1Lng = aprsSettings.Fix1Lng,
            Fix1Ew = aprsSettings.Fix1Ew,

            ToCall = aprsSettings.ToCall,
            ToCallSsid = aprsSettings.ToCallSsid,
            YourCall = aprsSettings.YourCall,
            YourCallSsid = aprsSettings.YourCallSsid,
            DigipeaterPath = aprsSettings.DigipeaterPath,

            AprsSymbol = aprsSettings.AprsSymbol,
            MapIcon = aprsSettings.MapIcon,
            TxPower = aprsSettings.TxPower,
            PrewaveTime = aprsSettings.PrewaveTime,

            RoamingSupport = aprsSettings.RoamingSupport,
            RepeaterActivationDelay = aprsSettings.RepeaterActivationDelay,
            DisTime = aprsSettings.DisTime,
            Altitude = aprsSettings.Altitude,
            AnalogTxMode = aprsSettings.AnalogTxMode,
            PassAll = aprsSettings.PassAll,

            TxFreq2MHz = aprsSettings.TxFreq2Mhz,
            TxFreq3MHz = aprsSettings.TxFreq3Mhz,
            TxFreq4MHz = aprsSettings.TxFreq4Mhz,
            TxFreq5MHz = aprsSettings.TxFreq5Mhz,
            TxFreq6MHz = aprsSettings.TxFreq6Mhz,
            TxFreq7MHz = aprsSettings.TxFreq7Mhz,
            TxFreq8MHz = aprsSettings.TxFreq8Mhz,

            SendingText = aprsSettings.SendingText,

            FixLocations = aprsSettings.AdditionalFixLocations.Select(fix => new AprsSettingsCodec.DecodedFixLocation(fix.Number)
            {
                Lat = fix.Lat,
                Ns = fix.Ns,
                Lng = fix.Lng,
                Ew = fix.Ew
            }).ToList(),

            DigitalReports = aprsSettings.DigitalReports.Select(report => new AprsSettingsCodec.DecodedDigitalReport(report.Number)
            {
                Channel = report.Channel,
                TalkgroupId = report.TalkgroupId,
                CallType = report.CallType,
                Slot = report.Slot
            }).ToList()
        };
    }

    private static AlarmSettingsCodec.DecodedAlarmSettings BuildAlarmSettingsValues(AlarmSettingsEntry alarmSettings, List<string> summaryLines)
    {
        summaryLines.Add("Alarm Settings: updated");

        return new AlarmSettingsCodec.DecodedAlarmSettings
        {
            AnalogEmergencyAlarm = alarmSettings.AnalogEmergencyAlarm,
            AnalogEniType = alarmSettings.AnalogEniType,
            AnalogEmergencyId = alarmSettings.AnalogEmergencyId,
            AnalogAlarmTime = alarmSettings.AnalogAlarmTime,
            AnalogTxDuration = alarmSettings.AnalogTxDuration,
            AnalogRxDuration = alarmSettings.AnalogRxDuration,
            AnalogEmergencyChannel = (ushort)alarmSettings.AnalogEmergencyChannel,
            AnalogEniSend = alarmSettings.AnalogEniSend,
            AnalogEmergencyCycle = alarmSettings.AnalogEmergencyCycle,

            DigitalEmergencyAlarm = alarmSettings.DigitalEmergencyAlarm,
            DigitalAlarmTime = alarmSettings.DigitalAlarmTime,
            DigitalTxDuration = alarmSettings.DigitalTxDuration,
            DigitalRxDuration = alarmSettings.DigitalRxDuration,
            DigitalEmergencyChannel = alarmSettings.DigitalEmergencyChannel,
            DigitalEmergencyCycle = alarmSettings.DigitalEmergencyCycle,
            DigitalEniSend = alarmSettings.DigitalEniSend,
            DigitalCallType = alarmSettings.DigitalCallType,
            DigitalTgDmrId = alarmSettings.DigitalTgDmrId,

            ReceiveAlarm = alarmSettings.ReceiveAlarm,
            ManDown = alarmSettings.ManDown,
            ManDownDelay = alarmSettings.ManDownDelay,

            WorkAloneResponseTime = alarmSettings.WorkAloneResponseTime,
            WorkAloneWarningTime = alarmSettings.WorkAloneWarningTime,
            WorkAloneResponse = alarmSettings.WorkAloneResponse,

            QdcGroupId = alarmSettings.QdcGroupId,
            QdcPrivateId = alarmSettings.QdcPrivateId
        };
    }

    /// <summary>Builds the write-safe patch for the Power-on tab directly
    /// from <paramref name="settings"/>'s already-typed fields - appends
    /// human-readable summary lines to <paramref name="summaryLines"/>
    /// rather than returning its own list, since (unlike Channel/Zone/Scan
    /// List) there's only ever one OptionalSettings instance, not a
    /// per-item loop.</summary>
    private static OptionalSettingsCodec.PowerOnFieldPatch BuildSafeOptionalSettingsPatch(OptionalSettingsEntry settings, List<string> summaryLines)
    {
        byte? powerOnInterface = null;
        if (settings.IsPowerOnInterfacePendingRadioWrite)
        {
            powerOnInterface = settings.PowerOnInterface;
            summaryLines.Add($"Power-on Interface = '{settings.PowerOnInterfaceText}'");
        }

        string? displayLine1 = null;
        if (settings.IsPowerOnDisplayLine1PendingRadioWrite)
        {
            displayLine1 = settings.PowerOnDisplayLine1;
            summaryLines.Add($"Power-on Display Line 1 = '{displayLine1}'");
        }

        string? displayLine2 = null;
        if (settings.IsPowerOnDisplayLine2PendingRadioWrite)
        {
            displayLine2 = settings.PowerOnDisplayLine2;
            summaryLines.Add($"Power-on Display Line 2 = '{displayLine2}'");
        }

        byte? powerOnPassword = null;
        if (settings.IsPowerOnPasswordPendingRadioWrite)
        {
            powerOnPassword = settings.PowerOnPassword;
            summaryLines.Add($"Power-on Password = '{settings.PowerOnPasswordText}'");
        }

        string? passwordChar = null;
        if (settings.IsPowerOnPasswordCharPendingRadioWrite)
        {
            passwordChar = settings.PowerOnPasswordChar;
            summaryLines.Add($"Power-on Password Char = '{passwordChar}'");
        }

        byte? defaultStartupChannel = null;
        if (settings.IsDefaultStartupChannelPendingRadioWrite)
        {
            defaultStartupChannel = settings.DefaultStartupChannel;
            summaryLines.Add($"Default Startup Channel = '{settings.DefaultStartupChannelText}'");
        }

        byte? startupZoneA = null;
        if (settings.IsStartupZoneAPendingRadioWrite)
        {
            startupZoneA = settings.StartupZoneA;
            summaryLines.Add($"Startup Zone A = {settings.StartupZoneA}");
        }

        byte? startupChannelA = null;
        if (settings.IsStartupChannelAPendingRadioWrite)
        {
            startupChannelA = settings.StartupChannelA;
            summaryLines.Add($"Startup Channel A = {settings.StartupChannelA}");
        }

        byte? startupZoneB = null;
        if (settings.IsStartupZoneBPendingRadioWrite)
        {
            startupZoneB = settings.StartupZoneB;
            summaryLines.Add($"Startup Zone B = {settings.StartupZoneB}");
        }

        byte? startupChannelB = null;
        if (settings.IsStartupChannelBPendingRadioWrite)
        {
            startupChannelB = settings.StartupChannelB;
            summaryLines.Add($"Startup Channel B = {settings.StartupChannelB}");
        }

        byte? startupReset = null;
        if (settings.IsStartupResetPendingRadioWrite)
        {
            startupReset = settings.StartupReset;
            summaryLines.Add($"Startup Reset = '{settings.StartupResetText}'");
        }

        byte? smsAlert = null;
        if (settings.IsSmsAlertPendingRadioWrite)
        {
            smsAlert = settings.SmsAlert;
            summaryLines.Add($"SMS Alert = '{settings.SmsAlertText}'");
        }

        byte? callAlert = null;
        if (settings.IsCallAlertPendingRadioWrite)
        {
            callAlert = settings.CallAlert;
            summaryLines.Add($"Call Alert = '{settings.CallAlertText}'");
        }

        byte? digiCallResetTone = null;
        if (settings.IsDigiCallResetTonePendingRadioWrite)
        {
            digiCallResetTone = settings.DigiCallResetTone;
            summaryLines.Add($"Digi Call Reset Tone = '{settings.DigiCallResetToneText}'");
        }

        byte? talkPermit = null;
        if (settings.IsTalkPermitPendingRadioWrite)
        {
            talkPermit = settings.TalkPermit;
            summaryLines.Add($"Talk Permit = '{settings.TalkPermitText}'");
        }

        byte? keyTone = null;
        if (settings.IsKeyTonePendingRadioWrite)
        {
            keyTone = settings.KeyTone;
            summaryLines.Add($"Key Tone = '{settings.KeyToneText}'");
        }

        byte? digiIdleChannelTone = null;
        if (settings.IsDigiIdleChannelTonePendingRadioWrite)
        {
            digiIdleChannelTone = settings.DigiIdleChannelTone;
            summaryLines.Add($"Digi Idle Channel Tone = '{settings.DigiIdleChannelToneText}'");
        }

        byte? startupSound = null;
        if (settings.IsStartupSoundPendingRadioWrite)
        {
            startupSound = settings.StartupSound;
            summaryLines.Add($"Startup Sound = '{settings.StartupSoundText}'");
        }

        byte? analogIdleChannelTone = null;
        if (settings.IsAnalogIdleChannelTonePendingRadioWrite)
        {
            analogIdleChannelTone = settings.AnalogIdleChannelTone;
            summaryLines.Add($"Ana Idle Channel Tone = '{settings.AnalogIdleChannelToneText}'");
        }

        IReadOnlyList<(ushort Frequency, ushort Period)>? callPermitTones = null;
        if (settings.CallPermitTones.Any(t => t.HasAnyPendingRadioWrite))
        {
            callPermitTones = settings.CallPermitTones.Select(t => ((ushort)t.Frequency, (ushort)t.Period)).ToList();
            summaryLines.Add("Call Permit Tone matrix updated");
        }

        IReadOnlyList<(ushort Frequency, ushort Period)>? matchEndTones = null;
        if (settings.MatchEndTones.Any(t => t.HasAnyPendingRadioWrite))
        {
            matchEndTones = settings.MatchEndTones.Select(t => ((ushort)t.Frequency, (ushort)t.Period)).ToList();
            summaryLines.Add("Match End Tone matrix updated");
        }

        IReadOnlyList<(ushort Frequency, ushort Period)>? callResetTones = null;
        if (settings.CallResetTones.Any(t => t.HasAnyPendingRadioWrite))
        {
            callResetTones = settings.CallResetTones.Select(t => ((ushort)t.Frequency, (ushort)t.Period)).ToList();
            summaryLines.Add("Call Reset Tone matrix updated");
        }

        IReadOnlyList<(ushort Frequency, ushort Period)>? unMatchEndTones = null;
        if (settings.UnMatchEndTones.Any(t => t.HasAnyPendingRadioWrite))
        {
            unMatchEndTones = settings.UnMatchEndTones.Select(t => ((ushort)t.Frequency, (ushort)t.Period)).ToList();
            summaryLines.Add("UnMatch End Tone matrix updated");
        }

        IReadOnlyList<(ushort Frequency, ushort Period)>? callAllTones = null;
        if (settings.CallAllTones.Any(t => t.HasAnyPendingRadioWrite))
        {
            callAllTones = settings.CallAllTones.Select(t => ((ushort)t.Frequency, (ushort)t.Period)).ToList();
            summaryLines.Add("All Call End Tone matrix updated");
        }

        byte? autoShutdown = null;
        if (settings.IsAutoShutdownPendingRadioWrite)
        {
            autoShutdown = settings.AutoShutdown;
            summaryLines.Add($"Auto Shutdown = '{settings.AutoShutdownText}'");
        }

        byte? powerSave = null;
        if (settings.IsPowerSavePendingRadioWrite)
        {
            powerSave = settings.PowerSave;
            summaryLines.Add($"Power Save = '{settings.PowerSaveText}'");
        }

        byte? autoShutdownType = null;
        if (settings.IsAutoShutdownTypePendingRadioWrite)
        {
            autoShutdownType = settings.AutoShutdownType;
            summaryLines.Add($"Auto Shutdown Type = '{settings.AutoShutdownTypeText}'");
        }

        byte? brightness = null;
        if (settings.IsBrightnessPendingRadioWrite)
        {
            brightness = settings.Brightness;
            summaryLines.Add($"Brightness = '{settings.BrightnessText}'");
        }

        byte? autoBacklightDuration = null;
        if (settings.IsAutoBacklightDurationPendingRadioWrite)
        {
            autoBacklightDuration = settings.AutoBacklightDuration;
            summaryLines.Add($"AutoBacklightDuration = '{settings.AutoBacklightDurationText}'");
        }

        byte? backlightTxDelay = null;
        if (settings.IsBacklightTxDelayPendingRadioWrite)
        {
            backlightTxDelay = settings.BacklightTxDelay;
            summaryLines.Add($"BacklightTxDelay = '{settings.BacklightTxDelayText}'");
        }

        byte? menuExitTime = null;
        if (settings.IsMenuExitTimePendingRadioWrite)
        {
            menuExitTime = settings.MenuExitTime;
            summaryLines.Add($"MenuExitTime = '{settings.MenuExitTimeText}'");
        }

        byte? timeDisplay = null;
        if (settings.IsTimeDisplayPendingRadioWrite)
        {
            timeDisplay = settings.TimeDisplay;
            summaryLines.Add($"TimeDisplay = '{settings.TimeDisplayText}'");
        }

        byte? lastCaller = null;
        if (settings.IsLastCallerPendingRadioWrite)
        {
            lastCaller = settings.LastCaller;
            summaryLines.Add($"LastCaller = '{settings.LastCallerText}'");
        }

        byte? callDisplayMode = null;
        if (settings.IsCallDisplayModePendingRadioWrite)
        {
            callDisplayMode = settings.CallDisplayMode;
            summaryLines.Add($"CallDisplayMode = '{settings.CallDisplayModeText}'");
        }

        byte? callsignDisplayColor = null;
        if (settings.IsCallsignDisplayColorPendingRadioWrite)
        {
            callsignDisplayColor = settings.CallsignDisplayColor;
            summaryLines.Add($"CallsignDisplayColor = '{settings.CallsignDisplayColorText}'");
        }

        byte? callEndPromptBox = null;
        if (settings.IsCallEndPromptBoxPendingRadioWrite)
        {
            callEndPromptBox = settings.CallEndPromptBox;
            summaryLines.Add($"CallEndPromptBox = '{settings.CallEndPromptBoxText}'");
        }

        byte? displayChannelNumber = null;
        if (settings.IsDisplayChannelNumberPendingRadioWrite)
        {
            displayChannelNumber = settings.DisplayChannelNumber;
            summaryLines.Add($"DisplayChannelNumber = '{settings.DisplayChannelNumberText}'");
        }

        byte? displayCurrentContact = null;
        if (settings.IsDisplayCurrentContactPendingRadioWrite)
        {
            displayCurrentContact = settings.DisplayCurrentContact;
            summaryLines.Add($"DisplayCurrentContact = '{settings.DisplayCurrentContactText}'");
        }

        byte? standbyCharColor = null;
        if (settings.IsStandbyCharColorPendingRadioWrite)
        {
            standbyCharColor = settings.StandbyCharColor;
            summaryLines.Add($"StandbyCharColor = '{settings.StandbyCharColorText}'");
        }

        byte? standbyBkPicture = null;
        if (settings.IsStandbyBkPicturePendingRadioWrite)
        {
            standbyBkPicture = settings.StandbyBkPicture;
            summaryLines.Add($"StandbyBkPicture = '{settings.StandbyBkPictureText}'");
        }

        byte? showLastCallOnLaunch = null;
        if (settings.IsShowLastCallOnLaunchPendingRadioWrite)
        {
            showLastCallOnLaunch = settings.ShowLastCallOnLaunch;
            summaryLines.Add($"ShowLastCallOnLaunch = '{settings.ShowLastCallOnLaunchText}'");
        }

        byte? separateDisplay = null;
        if (settings.IsSeparateDisplayPendingRadioWrite)
        {
            separateDisplay = settings.SeparateDisplay;
            summaryLines.Add($"SeparateDisplay = '{settings.SeparateDisplayText}'");
        }

        byte? chSwitchingKeepsCaller = null;
        if (settings.IsChSwitchingKeepsCallerPendingRadioWrite)
        {
            chSwitchingKeepsCaller = settings.ChSwitchingKeepsCaller;
            summaryLines.Add($"ChSwitchingKeepsCaller = '{settings.ChSwitchingKeepsCallerText}'");
        }

        byte? backlightRxDelay = null;
        if (settings.IsBacklightRxDelayPendingRadioWrite)
        {
            backlightRxDelay = settings.BacklightRxDelay;
            summaryLines.Add($"BacklightRxDelay = '{settings.BacklightRxDelayText}'");
        }

        byte? channelNameColorA = null;
        if (settings.IsChannelNameColorAPendingRadioWrite)
        {
            channelNameColorA = settings.ChannelNameColorA;
            summaryLines.Add($"ChannelNameColorA = '{settings.ChannelNameColorAText}'");
        }

        byte? channelNameColorB = null;
        if (settings.IsChannelNameColorBPendingRadioWrite)
        {
            channelNameColorB = settings.ChannelNameColorB;
            summaryLines.Add($"ChannelNameColorB = '{settings.ChannelNameColorBText}'");
        }

        byte? zoneNameColorA = null;
        if (settings.IsZoneNameColorAPendingRadioWrite)
        {
            zoneNameColorA = settings.ZoneNameColorA;
            summaryLines.Add($"ZoneNameColorA = '{settings.ZoneNameColorAText}'");
        }

        byte? zoneNameColorB = null;
        if (settings.IsZoneNameColorBPendingRadioWrite)
        {
            zoneNameColorB = settings.ZoneNameColorB;
            summaryLines.Add($"ZoneNameColorB = '{settings.ZoneNameColorBText}'");
        }

        bool? displayChannelType = null;
        if (settings.IsDisplayChannelTypePendingRadioWrite)
        {
            displayChannelType = settings.DisplayChannelType;
            summaryLines.Add($"DisplayChannelType = {(settings.DisplayChannelType ? "On" : "Off")}");
        }

        bool? displayTimeSlot = null;
        if (settings.IsDisplayTimeSlotPendingRadioWrite)
        {
            displayTimeSlot = settings.DisplayTimeSlot;
            summaryLines.Add($"DisplayTimeSlot = {(settings.DisplayTimeSlot ? "On" : "Off")}");
        }

        bool? displayColorCode = null;
        if (settings.IsDisplayColorCodePendingRadioWrite)
        {
            displayColorCode = settings.DisplayColorCode;
            summaryLines.Add($"DisplayColorCode = {(settings.DisplayColorCode ? "On" : "Off")}");
        }

        byte? dateDisplayFormat = null;
        if (settings.IsDateDisplayFormatPendingRadioWrite)
        {
            dateDisplayFormat = settings.DateDisplayFormat;
            summaryLines.Add($"DateDisplayFormat = '{settings.DateDisplayFormatText}'");
        }

        byte? volumeBar = null;
        if (settings.IsVolumeBarPendingRadioWrite)
        {
            volumeBar = settings.VolumeBar;
            summaryLines.Add($"VolumeBar = '{settings.VolumeBarText}'");
        }

        byte? nightMode = null;
        if (settings.IsNightModePendingRadioWrite)
        {
            nightMode = settings.NightMode;
            summaryLines.Add($"NightMode = '{settings.NightModeText}'");
        }

        byte? displayMode = null;
        if (settings.IsDisplayModePendingRadioWrite)
        {
            displayMode = settings.DisplayMode;
            summaryLines.Add($"DisplayMode = '{settings.DisplayModeText}'");
        }

        byte? vfMrA = null;
        if (settings.IsVfMrAPendingRadioWrite)
        {
            vfMrA = settings.VfMrA;
            summaryLines.Add($"VfMrA = '{settings.VfMrAText}'");
        }

        byte? memZoneA = null;
        if (settings.IsMemZoneAPendingRadioWrite)
        {
            memZoneA = settings.MemZoneA;
            summaryLines.Add($"MemZoneA = {settings.MemZoneA}");
        }

        byte? vfMrB = null;
        if (settings.IsVfMrBPendingRadioWrite)
        {
            vfMrB = settings.VfMrB;
            summaryLines.Add($"VfMrB = '{settings.VfMrBText}'");
        }

        byte? memZoneB = null;
        if (settings.IsMemZoneBPendingRadioWrite)
        {
            memZoneB = settings.MemZoneB;
            summaryLines.Add($"MemZoneB = {settings.MemZoneB}");
        }

        byte? mainChannelSet = null;
        if (settings.IsMainChannelSetPendingRadioWrite)
        {
            mainChannelSet = settings.MainChannelSet;
            summaryLines.Add($"MainChannelSet = '{settings.MainChannelSetText}'");
        }

        byte? subChannelMode = null;
        if (settings.IsSubChannelModePendingRadioWrite)
        {
            subChannelMode = settings.SubChannelMode;
            summaryLines.Add($"SubChannelMode = '{settings.SubChannelModeText}'");
        }

        byte? workingMode = null;
        if (settings.IsWorkingModePendingRadioWrite)
        {
            workingMode = settings.WorkingMode;
            summaryLines.Add($"WorkingMode = '{settings.WorkingModeText}'");
        }

        byte? voxLevel = null;
        if (settings.IsVoxLevelPendingRadioWrite)
        {
            voxLevel = settings.VoxLevel;
            summaryLines.Add($"Vox On/Off = '{settings.VoxLevelText}'");
        }

        byte? voxDelay = null;
        if (settings.IsVoxDelayPendingRadioWrite)
        {
            voxDelay = settings.VoxDelay;
            summaryLines.Add($"Vox Delay = '{settings.VoxDelayText}'");
        }

        byte? voxDetection = null;
        if (settings.IsVoxDetectionPendingRadioWrite)
        {
            voxDetection = settings.VoxDetection;
            summaryLines.Add($"Vox Detection = '{settings.VoxDetectionText}'");
        }

        byte? steTypeOfCtcss = null;
        if (settings.IsSteTypeOfCtcssPendingRadioWrite)
        {
            steTypeOfCtcss = settings.SteTypeOfCtcss;
            summaryLines.Add($"STE CTCSS Type = '{settings.SteTypeOfCtcssText}'");
        }

        byte? steWhenNoSignal = null;
        if (settings.IsSteWhenNoSignalPendingRadioWrite)
        {
            steWhenNoSignal = settings.SteWhenNoSignal;
            summaryLines.Add($"STE No Signal = '{settings.SteWhenNoSignalText}'");
        }

        byte? steTime = null;
        if (settings.IsSteTimePendingRadioWrite)
        {
            steTime = settings.SteTime;
            summaryLines.Add($"STE Time = '{settings.SteTimeText}'");
        }

        byte? amFmFunction = null;
        if (settings.IsAmFmFunctionPendingRadioWrite)
        {
            amFmFunction = settings.AmFmFunction;
            summaryLines.Add($"AM/FM Function = '{settings.AmFmFunctionText}'");
        }

        byte? fmVfoMem = null;
        if (settings.IsFmVfoMemPendingRadioWrite)
        {
            fmVfoMem = settings.FmVfoMem;
            summaryLines.Add($"FM VFO/MEM = '{settings.FmVfoMemText}'");
        }

        byte? fmWorkChannel = null;
        if (settings.IsFmWorkChannelPendingRadioWrite)
        {
            fmWorkChannel = settings.FmWorkChannel;
            summaryLines.Add($"FM Work Channel = index {settings.FmWorkChannel}");
        }

        byte? fmMonitor = null;
        if (settings.IsFmMonitorPendingRadioWrite)
        {
            fmMonitor = settings.FmMonitor;
            summaryLines.Add($"FM Monitor = '{settings.FmMonitorText}'");
        }

        byte? amVfoMem = null;
        if (settings.IsAmVfoMemPendingRadioWrite)
        {
            amVfoMem = settings.AmVfoMem;
            summaryLines.Add($"AM VFO/MEM = '{settings.AmVfoMemText}'");
        }

        byte? amOffset = null;
        if (settings.IsAmOffsetPendingRadioWrite)
        {
            amOffset = settings.AmOffset;
            summaryLines.Add($"AM Offset = '{settings.AmOffsetText}'");
        }

        byte? amSqlLevel = null;
        if (settings.IsAmSqlLevelPendingRadioWrite)
        {
            amSqlLevel = settings.AmSqlLevel;
            summaryLines.Add($"AM SQL Level = '{settings.AmSqlLevelText}'");
        }

        byte? frequencyStep = null;
        if (settings.IsFrequencyStepPendingRadioWrite)
        {
            frequencyStep = settings.FrequencyStep;
            summaryLines.Add($"Frequency Step = '{settings.FrequencyStepText}'");
        }

        byte? keyLock = null;
        if (settings.IsKeyLockPendingRadioWrite)
        {
            keyLock = settings.KeyLock;
            summaryLines.Add($"Key Lock = '{settings.KeyLockText}'");
        }

        byte? pf1ShortKey = null;
        if (settings.IsPf1ShortKeyPendingRadioWrite)
        {
            pf1ShortKey = settings.Pf1ShortKey;
            summaryLines.Add($"PF1 Short Key = '{settings.Pf1ShortKeyText}'");
        }

        byte? pf2ShortKey = null;
        if (settings.IsPf2ShortKeyPendingRadioWrite)
        {
            pf2ShortKey = settings.Pf2ShortKey;
            summaryLines.Add($"PF2 Short Key = '{settings.Pf2ShortKeyText}'");
        }

        byte? pf3ShortKey = null;
        if (settings.IsPf3ShortKeyPendingRadioWrite)
        {
            pf3ShortKey = settings.Pf3ShortKey;
            summaryLines.Add($"PF3 Short Key = '{settings.Pf3ShortKeyText}'");
        }

        byte? p1ShortKey = null;
        if (settings.IsP1ShortKeyPendingRadioWrite)
        {
            p1ShortKey = settings.P1ShortKey;
            summaryLines.Add($"P1 Short Key = '{settings.P1ShortKeyText}'");
        }

        byte? p2ShortKey = null;
        if (settings.IsP2ShortKeyPendingRadioWrite)
        {
            p2ShortKey = settings.P2ShortKey;
            summaryLines.Add($"P2 Short Key = '{settings.P2ShortKeyText}'");
        }

        byte? pf1LongKey = null;
        if (settings.IsPf1LongKeyPendingRadioWrite)
        {
            pf1LongKey = settings.Pf1LongKey;
            summaryLines.Add($"PF1 Long Key = '{settings.Pf1LongKeyText}'");
        }

        byte? pf2LongKey = null;
        if (settings.IsPf2LongKeyPendingRadioWrite)
        {
            pf2LongKey = settings.Pf2LongKey;
            summaryLines.Add($"PF2 Long Key = '{settings.Pf2LongKeyText}'");
        }

        byte? pf3LongKey = null;
        if (settings.IsPf3LongKeyPendingRadioWrite)
        {
            pf3LongKey = settings.Pf3LongKey;
            summaryLines.Add($"PF3 Long Key = '{settings.Pf3LongKeyText}'");
        }

        byte? p1LongKey = null;
        if (settings.IsP1LongKeyPendingRadioWrite)
        {
            p1LongKey = settings.P1LongKey;
            summaryLines.Add($"P1 Long Key = '{settings.P1LongKeyText}'");
        }

        byte? p2LongKey = null;
        if (settings.IsP2LongKeyPendingRadioWrite)
        {
            p2LongKey = settings.P2LongKey;
            summaryLines.Add($"P2 Long Key = '{settings.P2LongKeyText}'");
        }

        byte? longKeyTime = null;
        if (settings.IsLongKeyTimePendingRadioWrite)
        {
            longKeyTime = settings.LongKeyTime;
            summaryLines.Add($"Long Key Time = '{settings.LongKeyTimeText}'");
        }

        bool? knobLock = null;
        if (settings.IsKnobLockPendingRadioWrite)
        {
            knobLock = settings.KnobLock;
            summaryLines.Add($"Knob Lock = {settings.KnobLock}");
        }

        bool? keyboardLock = null;
        if (settings.IsKeyboardLockPendingRadioWrite)
        {
            keyboardLock = settings.KeyboardLock;
            summaryLines.Add($"Keyboard Lock = {settings.KeyboardLock}");
        }

        bool? sideKeyLock = null;
        if (settings.IsSideKeyLockPendingRadioWrite)
        {
            sideKeyLock = settings.SideKeyLock;
            summaryLines.Add($"Side Key Lock = {settings.SideKeyLock}");
        }

        bool? forcedKeyLock = null;
        if (settings.IsForcedKeyLockPendingRadioWrite)
        {
            forcedKeyLock = settings.ForcedKeyLock;
            summaryLines.Add($"Forced Key Lock = {settings.ForcedKeyLock}");
        }

        byte? addressBookSentWithCode = null;
        if (settings.IsAddressBookSentWithCodePendingRadioWrite)
        {
            addressBookSentWithCode = settings.AddressBookSentWithCode;
            summaryLines.Add($"Address Book Sent With Own Code = '{settings.AddressBookSentWithCodeText}'");
        }

        byte? tot = null;
        if (settings.IsTotPendingRadioWrite)
        {
            tot = settings.Tot;
            summaryLines.Add($"TOT = '{settings.TotText}'");
        }

        byte? language = null;
        if (settings.IsLanguagePendingRadioWrite)
        {
            language = settings.Language;
            summaryLines.Add($"Language = '{settings.LanguageText}'");
        }

        byte? generalFrequencyStep = null;
        if (settings.IsGeneralFrequencyStepPendingRadioWrite)
        {
            generalFrequencyStep = settings.GeneralFrequencyStep;
            summaryLines.Add($"Frequency Step (Other) = '{settings.GeneralFrequencyStepText}'");
        }

        byte? sqlLevelA = null;
        if (settings.IsSqlLevelAPendingRadioWrite)
        {
            sqlLevelA = settings.SqlLevelA;
            summaryLines.Add($"SQL Level A = '{settings.SqlLevelAText}'");
        }

        byte? sqlLevelB = null;
        if (settings.IsSqlLevelBPendingRadioWrite)
        {
            sqlLevelB = settings.SqlLevelB;
            summaryLines.Add($"SQL Level B = '{settings.SqlLevelBText}'");
        }

        byte? tbst = null;
        if (settings.IsTbstPendingRadioWrite)
        {
            tbst = settings.Tbst;
            summaryLines.Add($"TBST = '{settings.TbstText}'");
        }

        byte? analogCallHoldTime = null;
        if (settings.IsAnalogCallHoldTimePendingRadioWrite)
        {
            analogCallHoldTime = settings.AnalogCallHoldTime;
            summaryLines.Add($"Analog Call Hold Time = '{settings.AnalogCallHoldTimeText}'");
        }

        byte? callChannelMaintained = null;
        if (settings.IsCallChannelMaintainedPendingRadioWrite)
        {
            callChannelMaintained = settings.CallChannelMaintained;
            summaryLines.Add($"Call Channel Is Maintained = '{settings.CallChannelMaintainedText}'");
        }

        byte? priorityZoneA = null;
        if (settings.IsPriorityZoneAPendingRadioWrite)
        {
            priorityZoneA = settings.PriorityZoneA;
            summaryLines.Add($"Priority Zone A = {settings.PriorityZoneA}");
        }

        byte? priorityZoneB = null;
        if (settings.IsPriorityZoneBPendingRadioWrite)
        {
            priorityZoneB = settings.PriorityZoneB;
            summaryLines.Add($"Priority Zone B = {settings.PriorityZoneB}");
        }

        byte? muteTiming = null;
        if (settings.IsMuteTimingPendingRadioWrite)
        {
            muteTiming = settings.MuteTiming;
            summaryLines.Add($"Mute Timing = '{settings.MuteTimingText}'");
        }

        byte? encryptionType = null;
        if (settings.IsEncryptionTypePendingRadioWrite)
        {
            encryptionType = settings.EncryptionType;
            summaryLines.Add($"Encryption Type = '{settings.EncryptionTypeText}'");
        }

        byte? totPredict = null;
        if (settings.IsTotPredictPendingRadioWrite)
        {
            totPredict = settings.TotPredict;
            summaryLines.Add($"TOT Predict = '{settings.TotPredictText}'");
        }

        byte? txPowerAgc = null;
        if (settings.IsTxPowerAgcPendingRadioWrite)
        {
            txPowerAgc = settings.TxPowerAgc;
            summaryLines.Add($"TxPow AGC = '{settings.TxPowerAgcText}'");
        }

        byte? noaaMoni = null;
        if (settings.IsNoaaMoniPendingRadioWrite)
        {
            noaaMoni = settings.NoaaMoni;
            summaryLines.Add($"NOAA Moni = '{settings.NoaaMoniText}'");
        }

        byte? noaaScan = null;
        if (settings.IsNoaaScanPendingRadioWrite)
        {
            noaaScan = settings.NoaaScan;
            summaryLines.Add($"NOAA Scan = '{settings.NoaaScanText}'");
        }

        byte? noaa = null;
        if (settings.IsNoaaPendingRadioWrite)
        {
            noaa = settings.Noaa;
            summaryLines.Add($"NOAA Alert = '{settings.NoaaText}'");
        }

        byte? noaaChannel = null;
        if (settings.IsNoaaChannelPendingRadioWrite)
        {
            noaaChannel = settings.NoaaChannel;
            summaryLines.Add($"NOAA Channel = '{settings.NoaaChannelText}'");
        }

        byte? groupCallHoldTime = null;
        if (settings.IsGroupCallHoldTimePendingRadioWrite)
        {
            groupCallHoldTime = settings.GroupCallHoldTime;
            summaryLines.Add($"Group Call Hold Time = '{settings.GroupCallHoldTimeText}'");
        }

        byte? privateCallHoldTime = null;
        if (settings.IsPrivateCallHoldTimePendingRadioWrite)
        {
            privateCallHoldTime = settings.PrivateCallHoldTime;
            summaryLines.Add($"Private Call Hold Time = '{settings.PrivateCallHoldTimeText}'");
        }

        byte? manualDialGroupCallHoldTime = null;
        if (settings.IsManualDialGroupCallHoldTimePendingRadioWrite)
        {
            manualDialGroupCallHoldTime = settings.ManualDialGroupCallHoldTime;
            summaryLines.Add($"Manual Dial - Group TG Hold Time = '{settings.ManualDialGroupCallHoldTimeText}'");
        }

        byte? manualDialPrivateCallHoldTime = null;
        if (settings.IsManualDialPrivateCallHoldTimePendingRadioWrite)
        {
            manualDialPrivateCallHoldTime = settings.ManualDialPrivateCallHoldTime;
            summaryLines.Add($"Manual Dial - Private TG Hold Time = '{settings.ManualDialPrivateCallHoldTimeText}'");
        }

        byte? voiceHeaderRepetitions = null;
        if (settings.IsVoiceHeaderRepetitionsPendingRadioWrite)
        {
            voiceHeaderRepetitions = settings.VoiceHeaderRepetitions;
            summaryLines.Add($"Voice Header Repetitions = '{settings.VoiceHeaderRepetitionsText}'");
        }

        byte? txPreambleDuration = null;
        if (settings.IsTxPreambleDurationPendingRadioWrite)
        {
            txPreambleDuration = settings.TxPreambleDuration;
            summaryLines.Add($"TX Preamble Duration = '{settings.TxPreambleDurationText}'");
        }

        byte? filterOwnId = null;
        if (settings.IsFilterOwnIdPendingRadioWrite)
        {
            filterOwnId = settings.FilterOwnId;
            summaryLines.Add($"Filter Own ID In Miss Call = '{settings.FilterOwnIdText}'");
        }

        byte? digitalRemoteKill = null;
        if (settings.IsDigitalRemoteKillPendingRadioWrite)
        {
            digitalRemoteKill = settings.DigitalRemoteKill;
            summaryLines.Add($"Digital Remote Kill = '{settings.DigitalRemoteKillText}'");
        }

        byte? digitalMonitor = null;
        if (settings.IsDigitalMonitorPendingRadioWrite)
        {
            digitalMonitor = settings.DigitalMonitor;
            summaryLines.Add($"Digital Monitor = '{settings.DigitalMonitorText}'");
        }

        byte? digitalMonitorCc = null;
        if (settings.IsDigitalMonitorCcPendingRadioWrite)
        {
            digitalMonitorCc = settings.DigitalMonitorCc;
            summaryLines.Add($"Digital Monitor CC = '{settings.DigitalMonitorCcText}'");
        }

        byte? digitalMonitorId = null;
        if (settings.IsDigitalMonitorIdPendingRadioWrite)
        {
            digitalMonitorId = settings.DigitalMonitorId;
            summaryLines.Add($"Digital Monitor ID = '{settings.DigitalMonitorIdText}'");
        }

        byte? monitorSlotHold = null;
        if (settings.IsMonitorSlotHoldPendingRadioWrite)
        {
            monitorSlotHold = settings.MonitorSlotHold;
            summaryLines.Add($"Monitor Hold Slot = '{settings.MonitorSlotHoldText}'");
        }

        byte? remoteMonitor = null;
        if (settings.IsRemoteMonitorPendingRadioWrite)
        {
            remoteMonitor = settings.RemoteMonitor;
            summaryLines.Add($"Remote Monitor = '{settings.RemoteMonitorText}'");
        }

        byte? smsFormat = null;
        if (settings.IsSmsFormatPendingRadioWrite)
        {
            smsFormat = settings.SmsFormat;
            summaryLines.Add($"SMS Format = '{settings.SmsFormatText}'");
        }

        byte? resetDigitalProtocol = null;
        if (settings.IsResetDigitalProtocolPendingRadioWrite)
        {
            resetDigitalProtocol = settings.ResetDigitalProtocol;
            summaryLines.Add($"Reset Digital Protocol = '{settings.ResetDigitalProtocolText}'");
        }

        byte? gpsPositioning = null;
        if (settings.IsGpsPositioningPendingRadioWrite)
        {
            gpsPositioning = settings.GpsPositioning;
            summaryLines.Add($"GPS Positioning = '{settings.GpsPositioningText}'");
        }

        byte? timeZone = null;
        if (settings.IsTimeZonePendingRadioWrite)
        {
            timeZone = settings.TimeZone;
            summaryLines.Add($"Time Zone = '{settings.TimeZoneText}'");
        }

        byte? gpsMode = null;
        if (settings.IsGpsModePendingRadioWrite)
        {
            gpsMode = settings.GpsMode;
            summaryLines.Add($"GPS Mode = '{settings.GpsModeText}'");
        }

        byte? vfoScanType = null;
        if (settings.IsVfoScanTypePendingRadioWrite)
        {
            vfoScanType = settings.VfoScanType;
            summaryLines.Add($"VFO Scan Type = '{settings.VfoScanTypeText}'");
        }

        int? vfoScanStartFreqUhf = null;
        if (settings.IsVfoScanStartFreqUhfPendingRadioWrite)
        {
            vfoScanStartFreqUhf = settings.VfoScanStartFreqUhf;
            summaryLines.Add($"VFO Scan Start Freq (UHF) = '{settings.VfoScanStartFreqUhfText}'");
        }

        int? vfoScanEndFreqUhf = null;
        if (settings.IsVfoScanEndFreqUhfPendingRadioWrite)
        {
            vfoScanEndFreqUhf = settings.VfoScanEndFreqUhf;
            summaryLines.Add($"VFO Scan End Freq (UHF) = '{settings.VfoScanEndFreqUhfText}'");
        }

        int? vfoScanStartFreqVhf = null;
        if (settings.IsVfoScanStartFreqVhfPendingRadioWrite)
        {
            vfoScanStartFreqVhf = settings.VfoScanStartFreqVhf;
            summaryLines.Add($"VFO Scan Start Freq (VHF) = '{settings.VfoScanStartFreqVhfText}'");
        }

        int? vfoScanEndFreqVhf = null;
        if (settings.IsVfoScanEndFreqVhfPendingRadioWrite)
        {
            vfoScanEndFreqVhf = settings.VfoScanEndFreqVhf;
            summaryLines.Add($"VFO Scan End Freq (VHF) = '{settings.VfoScanEndFreqVhfText}'");
        }

        byte? autoRepeaterA = null;
        if (settings.IsAutoRepeaterAPendingRadioWrite)
        {
            autoRepeaterA = settings.AutoRepeaterA;
            summaryLines.Add($"Auto Repeater A = '{settings.AutoRepeaterAText}'");
        }

        byte? autoRepeaterB = null;
        if (settings.IsAutoRepeaterBPendingRadioWrite)
        {
            autoRepeaterB = settings.AutoRepeaterB;
            summaryLines.Add($"Auto Repeater B = '{settings.AutoRepeaterBText}'");
        }

        byte? autoRepeater1Uhf = null;
        if (settings.IsAutoRepeater1UhfPendingRadioWrite)
        {
            autoRepeater1Uhf = settings.AutoRepeater1Uhf;
            summaryLines.Add($"Auto Repeater1 (UHF) = '{settings.AutoRepeater1UhfText}'");
        }

        byte? autoRepeater1Vhf = null;
        if (settings.IsAutoRepeater1VhfPendingRadioWrite)
        {
            autoRepeater1Vhf = settings.AutoRepeater1Vhf;
            summaryLines.Add($"Auto Repeater1 (VHF) = '{settings.AutoRepeater1VhfText}'");
        }

        byte? autoRepeater2Uhf = null;
        if (settings.IsAutoRepeater2UhfPendingRadioWrite)
        {
            autoRepeater2Uhf = settings.AutoRepeater2Uhf;
            summaryLines.Add($"Auto Repeater2 (UHF) = '{settings.AutoRepeater2UhfText}'");
        }

        byte? autoRepeater2Vhf = null;
        if (settings.IsAutoRepeater2VhfPendingRadioWrite)
        {
            autoRepeater2Vhf = settings.AutoRepeater2Vhf;
            summaryLines.Add($"Auto Repeater2 (VHF) = '{settings.AutoRepeater2VhfText}'");
        }

        byte? repeaterCheck = null;
        if (settings.IsRepeaterCheckPendingRadioWrite)
        {
            repeaterCheck = settings.RepeaterCheck;
            summaryLines.Add($"Repeater Check = '{settings.RepeaterCheckText}'");
        }

        byte? repeaterCheckInterval = null;
        if (settings.IsRepeaterCheckIntervalPendingRadioWrite)
        {
            repeaterCheckInterval = settings.RepeaterCheckInterval;
            summaryLines.Add($"Repeater Check Interval[s] = '{settings.RepeaterCheckIntervalText}'");
        }

        byte? repeaterCheckReconnections = null;
        if (settings.IsRepeaterCheckReconnectionsPendingRadioWrite)
        {
            repeaterCheckReconnections = settings.RepeaterCheckReconnections;
            summaryLines.Add($"Repeater Check Reconnections = '{settings.RepeaterCheckReconnectionsText}'");
        }

        byte? repeaterOutOfRangeNotify = null;
        if (settings.IsRepeaterOutOfRangeNotifyPendingRadioWrite)
        {
            repeaterOutOfRangeNotify = settings.RepeaterOutOfRangeNotify;
            summaryLines.Add($"Repeater Out Of Range Notify = '{settings.RepeaterOutOfRangeNotifyText}'");
        }

        byte? outOfRangeNotify = null;
        if (settings.IsOutOfRangeNotifyPendingRadioWrite)
        {
            outOfRangeNotify = settings.OutOfRangeNotify;
            summaryLines.Add($"Out Of Range Notify (Times) = '{settings.OutOfRangeNotifyText}'");
        }

        byte? autoRoaming = null;
        if (settings.IsAutoRoamingPendingRadioWrite)
        {
            autoRoaming = settings.AutoRoaming;
            summaryLines.Add($"Auto Roaming = '{settings.AutoRoamingText}'");
        }

        byte? autoRoamingStartCondition = null;
        if (settings.IsAutoRoamingStartConditionPendingRadioWrite)
        {
            autoRoamingStartCondition = settings.AutoRoamingStartCondition;
            summaryLines.Add($"Auto Roaming Start Condition = '{settings.AutoRoamingStartConditionText}'");
        }

        byte? autoRoamingFixedTime = null;
        if (settings.IsAutoRoamingFixedTimePendingRadioWrite)
        {
            autoRoamingFixedTime = settings.AutoRoamingFixedTime;
            summaryLines.Add($"Auto Roaming at Fixed Time[m] = '{settings.AutoRoamingFixedTimeText}'");
        }

        byte? roamingEffectWaitTime = null;
        if (settings.IsRoamingEffectWaitTimePendingRadioWrite)
        {
            roamingEffectWaitTime = settings.RoamingEffectWaitTime;
            summaryLines.Add($"Roaming Effect Wait Time[s] = '{settings.RoamingEffectWaitTimeText}'");
        }

        int? autoRepeater1MinFreqVhf = null;
        if (settings.IsAutoRepeater1MinFreqVhfPendingRadioWrite)
        {
            autoRepeater1MinFreqVhf = settings.AutoRepeater1MinFreqVhf;
            summaryLines.Add($"Min Freq Of Auto Repeater1(VHF) = '{settings.AutoRepeater1MinFreqVhfText}'");
        }

        int? autoRepeater1MaxFreqVhf = null;
        if (settings.IsAutoRepeater1MaxFreqVhfPendingRadioWrite)
        {
            autoRepeater1MaxFreqVhf = settings.AutoRepeater1MaxFreqVhf;
            summaryLines.Add($"Max Freq Of Auto Repeater1(VHF) = '{settings.AutoRepeater1MaxFreqVhfText}'");
        }

        int? autoRepeater1MinFreqUhf = null;
        if (settings.IsAutoRepeater1MinFreqUhfPendingRadioWrite)
        {
            autoRepeater1MinFreqUhf = settings.AutoRepeater1MinFreqUhf;
            summaryLines.Add($"Min Freq Of Auto Repeater1(UHF) = '{settings.AutoRepeater1MinFreqUhfText}'");
        }

        int? autoRepeater1MaxFreqUhf = null;
        if (settings.IsAutoRepeater1MaxFreqUhfPendingRadioWrite)
        {
            autoRepeater1MaxFreqUhf = settings.AutoRepeater1MaxFreqUhf;
            summaryLines.Add($"Max Freq Of Auto Repeater1(UHF) = '{settings.AutoRepeater1MaxFreqUhfText}'");
        }

        int? autoRepeater2MinFreqVhf = null;
        if (settings.IsAutoRepeater2MinFreqVhfPendingRadioWrite)
        {
            autoRepeater2MinFreqVhf = settings.AutoRepeater2MinFreqVhf;
            summaryLines.Add($"Min Freq Of Auto Repeater2(VHF) = '{settings.AutoRepeater2MinFreqVhfText}'");
        }

        int? autoRepeater2MaxFreqVhf = null;
        if (settings.IsAutoRepeater2MaxFreqVhfPendingRadioWrite)
        {
            autoRepeater2MaxFreqVhf = settings.AutoRepeater2MaxFreqVhf;
            summaryLines.Add($"Max Freq Of Auto Repeater2(VHF) = '{settings.AutoRepeater2MaxFreqVhfText}'");
        }

        int? autoRepeater2MinFreqUhf = null;
        if (settings.IsAutoRepeater2MinFreqUhfPendingRadioWrite)
        {
            autoRepeater2MinFreqUhf = settings.AutoRepeater2MinFreqUhf;
            summaryLines.Add($"Min Freq Of Auto Repeater2(UHF) = '{settings.AutoRepeater2MinFreqUhfText}'");
        }

        int? autoRepeater2MaxFreqUhf = null;
        if (settings.IsAutoRepeater2MaxFreqUhfPendingRadioWrite)
        {
            autoRepeater2MaxFreqUhf = settings.AutoRepeater2MaxFreqUhf;
            summaryLines.Add($"Max Freq Of Auto Repeater2(UHF) = '{settings.AutoRepeater2MaxFreqUhfText}'");
        }

        byte? repeaterMode = null;
        if (settings.IsRepeaterModePendingRadioWrite)
        {
            repeaterMode = settings.RepeaterMode;
            summaryLines.Add($"Repeater Mode = '{settings.RepeaterModeText}'");
        }

        byte? repCcLimit = null;
        if (settings.IsRepCcLimitPendingRadioWrite)
        {
            repCcLimit = settings.RepCcLimit;
            summaryLines.Add($"Rep CC Limit = '{settings.RepCcLimitText}'");
        }

        byte? repSlotA = null;
        if (settings.IsRepSlotAPendingRadioWrite)
        {
            repSlotA = settings.RepSlotA;
            summaryLines.Add($"Rep Slot PathA = '{settings.RepSlotAText}'");
        }

        byte? repSlotB = null;
        if (settings.IsRepSlotBPendingRadioWrite)
        {
            repSlotB = settings.RepSlotB;
            summaryLines.Add($"Rep Slot PathB = '{settings.RepSlotBText}'");
        }

        byte? repeaterWhitelist = null;
        if (settings.IsRepeaterWhitelistPendingRadioWrite)
        {
            repeaterWhitelist = settings.RepeaterWhitelist;
            summaryLines.Add($"Repeater Whitelist = '{settings.RepeaterWhitelistText}'");
        }

        byte? recordFunction = null;
        if (settings.IsRecordFunctionPendingRadioWrite)
        {
            recordFunction = settings.RecordFunction;
            summaryLines.Add($"Record Function = '{settings.RecordFunctionText}'");
        }

        byte? recordDelay = null;
        if (settings.IsRecordDelayPendingRadioWrite)
        {
            recordDelay = settings.RecordDelay;
            summaryLines.Add($"Record Delay = '{settings.RecordDelayText}'");
        }

        byte? maxVolume = null;
        if (settings.IsMaxVolumePendingRadioWrite)
        {
            maxVolume = settings.MaxVolume;
            summaryLines.Add($"Maximum Volume = '{settings.MaxVolumeText}'");
        }

        byte? powerOnVolumeType = null;
        if (settings.IsPowerOnVolumeTypePendingRadioWrite)
        {
            powerOnVolumeType = settings.PowerOnVolumeType;
            summaryLines.Add($"Power On Volume Type = '{settings.PowerOnVolumeTypeText}'");
        }

        byte? powerOnVolume = null;
        if (settings.IsPowerOnVolumePendingRadioWrite)
        {
            powerOnVolume = settings.PowerOnVolume;
            summaryLines.Add($"Power On Volume = '{settings.PowerOnVolumeText}'");
        }

        byte? maxHeadphoneVolume = null;
        if (settings.IsMaxHeadphoneVolumePendingRadioWrite)
        {
            maxHeadphoneVolume = settings.MaxHeadphoneVolume;
            summaryLines.Add($"Max Headphone Volume = '{settings.MaxHeadphoneVolumeText}'");
        }

        byte? digiMicGain = null;
        if (settings.IsDigiMicGainPendingRadioWrite)
        {
            digiMicGain = settings.DigiMicGain;
            summaryLines.Add($"DMR Mic Gain = '{settings.DigiMicGainText}'");
        }

        byte? enhancedSoundQuality = null;
        if (settings.IsEnhancedSoundQualityPendingRadioWrite)
        {
            enhancedSoundQuality = settings.EnhancedSoundQuality;
            summaryLines.Add($"Enhanced Sound Quality = '{settings.EnhancedSoundQualityText}'");
        }

        byte? analogMicGain = null;
        if (settings.IsAnalogMicGainPendingRadioWrite)
        {
            analogMicGain = settings.AnalogMicGain;
            summaryLines.Add($"Ana Mic Gain = '{settings.AnalogMicGainText}'");
        }

        byte? rxAgc = null;
        if (settings.IsRxAgcPendingRadioWrite)
        {
            rxAgc = settings.RxAgc;
            summaryLines.Add($"Rx AGC = '{settings.RxAgcText}'");
        }

        byte? nxMicGain = null;
        if (settings.IsNxMicGainPendingRadioWrite)
        {
            nxMicGain = settings.NxMicGain;
            summaryLines.Add($"NX Mic Gain = '{settings.NxMicGainText}'");
        }

        byte? subSpkInTx = null;
        if (settings.IsSubSpkInTxPendingRadioWrite)
        {
            subSpkInTx = settings.SubSpkInTx;
            summaryLines.Add($"Sub SpkInTx = '{settings.SubSpkInTxText}'");
        }

        byte? rxNoiseReduction = null;
        if (settings.IsRxNoiseReductionPendingRadioWrite)
        {
            rxNoiseReduction = settings.RxNoiseReduction;
            summaryLines.Add($"RX Noise Reduction = '{settings.RxNoiseReductionText}'");
        }

        byte? txNoiseReduction = null;
        if (settings.IsTxNoiseReductionPendingRadioWrite)
        {
            txNoiseReduction = settings.TxNoiseReduction;
            summaryLines.Add($"TX Noise Reduction = '{settings.TxNoiseReductionText}'");
        }

        byte? satLocation = null;
        if (settings.IsSatLocationPendingRadioWrite)
        {
            satLocation = settings.SatLocation;
            summaryLines.Add($"Sat Location = '{settings.SatLocationText}'");
        }

        byte? satTxPower = null;
        if (settings.IsSatTxPowerPendingRadioWrite)
        {
            satTxPower = settings.SatTxPower;
            summaryLines.Add($"Sat TX Power = '{settings.SatTxPowerText}'");
        }

        byte? satAnaSql = null;
        if (settings.IsSatAnaSqlPendingRadioWrite)
        {
            satAnaSql = settings.SatAnaSql;
            summaryLines.Add($"Sat Ana SQL = '{settings.SatAnaSqlText}'");
        }

        byte? satAosLimit = null;
        if (settings.IsSatAosLimitPendingRadioWrite)
        {
            satAosLimit = settings.SatAosLimit;
            summaryLines.Add($"Sat AOS Limit = '{settings.SatAosLimitText}'");
        }

        byte? roamingZone = null;
        if (settings.IsRoamingZonePendingRadioWrite)
        {
            roamingZone = settings.RoamingZone;
            summaryLines.Add($"Roaming Zone = {settings.RoamingZone}");
        }

        return new OptionalSettingsCodec.PowerOnFieldPatch
        {
            PowerOnInterface = powerOnInterface,
            PowerOnDisplayLine1 = displayLine1,
            PowerOnDisplayLine2 = displayLine2,
            PowerOnPassword = powerOnPassword,
            PowerOnPasswordChar = passwordChar,
            DefaultStartupChannel = defaultStartupChannel,
            StartupZoneA = startupZoneA,
            StartupChannelA = startupChannelA,
            StartupZoneB = startupZoneB,
            StartupChannelB = startupChannelB,
            StartupReset = startupReset,
            SmsAlert = smsAlert,
            CallAlert = callAlert,
            DigiCallResetTone = digiCallResetTone,
            TalkPermit = talkPermit,
            KeyTone = keyTone,
            DigiIdleChannelTone = digiIdleChannelTone,
            StartupSound = startupSound,
            AnalogIdleChannelTone = analogIdleChannelTone,
            CallPermitTones = callPermitTones,
            MatchEndTones = matchEndTones,
            CallResetTones = callResetTones,
            UnMatchEndTones = unMatchEndTones,
            CallAllTones = callAllTones,
            AutoShutdown = autoShutdown,
            PowerSave = powerSave,
            AutoShutdownType = autoShutdownType,
            Brightness = brightness,
            AutoBacklightDuration = autoBacklightDuration,
            BacklightTxDelay = backlightTxDelay,
            MenuExitTime = menuExitTime,
            TimeDisplay = timeDisplay,
            LastCaller = lastCaller,
            CallDisplayMode = callDisplayMode,
            CallsignDisplayColor = callsignDisplayColor,
            CallEndPromptBox = callEndPromptBox,
            DisplayChannelNumber = displayChannelNumber,
            DisplayCurrentContact = displayCurrentContact,
            StandbyCharColor = standbyCharColor,
            StandbyBkPicture = standbyBkPicture,
            ShowLastCallOnLaunch = showLastCallOnLaunch,
            SeparateDisplay = separateDisplay,
            ChSwitchingKeepsCaller = chSwitchingKeepsCaller,
            BacklightRxDelay = backlightRxDelay,
            ChannelNameColorA = channelNameColorA,
            ChannelNameColorB = channelNameColorB,
            ZoneNameColorA = zoneNameColorA,
            ZoneNameColorB = zoneNameColorB,
            DisplayChannelType = displayChannelType,
            DisplayTimeSlot = displayTimeSlot,
            DisplayColorCode = displayColorCode,
            DateDisplayFormat = dateDisplayFormat,
            VolumeBar = volumeBar,
            NightMode = nightMode,
            DisplayMode = displayMode,
            VfMrA = vfMrA,
            MemZoneA = memZoneA,
            VfMrB = vfMrB,
            MemZoneB = memZoneB,
            MainChannelSet = mainChannelSet,
            SubChannelMode = subChannelMode,
            WorkingMode = workingMode,
            VoxLevel = voxLevel,
            VoxDelay = voxDelay,
            VoxDetection = voxDetection,
            SteTypeOfCtcss = steTypeOfCtcss,
            SteWhenNoSignal = steWhenNoSignal,
            SteTime = steTime,
            AmFmFunction = amFmFunction,
            FmVfoMem = fmVfoMem,
            FmWorkChannel = fmWorkChannel,
            FmMonitor = fmMonitor,
            AmVfoMem = amVfoMem,
            AmOffset = amOffset,
            AmSqlLevel = amSqlLevel,
            FrequencyStep = frequencyStep,
            KeyLock = keyLock,
            Pf1ShortKey = pf1ShortKey,
            Pf2ShortKey = pf2ShortKey,
            Pf3ShortKey = pf3ShortKey,
            P1ShortKey = p1ShortKey,
            P2ShortKey = p2ShortKey,
            Pf1LongKey = pf1LongKey,
            Pf2LongKey = pf2LongKey,
            Pf3LongKey = pf3LongKey,
            P1LongKey = p1LongKey,
            P2LongKey = p2LongKey,
            LongKeyTime = longKeyTime,
            KnobLock = knobLock,
            KeyboardLock = keyboardLock,
            SideKeyLock = sideKeyLock,
            ForcedKeyLock = forcedKeyLock,
            AddressBookSentWithCode = addressBookSentWithCode,
            Tot = tot,
            Language = language,
            GeneralFrequencyStep = generalFrequencyStep,
            SqlLevelA = sqlLevelA,
            SqlLevelB = sqlLevelB,
            Tbst = tbst,
            AnalogCallHoldTime = analogCallHoldTime,
            CallChannelMaintained = callChannelMaintained,
            PriorityZoneA = priorityZoneA,
            PriorityZoneB = priorityZoneB,
            MuteTiming = muteTiming,
            EncryptionType = encryptionType,
            TotPredict = totPredict,
            TxPowerAgc = txPowerAgc,
            NoaaMoni = noaaMoni,
            NoaaScan = noaaScan,
            Noaa = noaa,
            NoaaChannel = noaaChannel,
            GroupCallHoldTime = groupCallHoldTime,
            PrivateCallHoldTime = privateCallHoldTime,
            ManualDialGroupCallHoldTime = manualDialGroupCallHoldTime,
            ManualDialPrivateCallHoldTime = manualDialPrivateCallHoldTime,
            VoiceHeaderRepetitions = voiceHeaderRepetitions,
            TxPreambleDuration = txPreambleDuration,
            FilterOwnId = filterOwnId,
            DigitalRemoteKill = digitalRemoteKill,
            DigitalMonitor = digitalMonitor,
            DigitalMonitorCc = digitalMonitorCc,
            DigitalMonitorId = digitalMonitorId,
            MonitorSlotHold = monitorSlotHold,
            RemoteMonitor = remoteMonitor,
            SmsFormat = smsFormat,
            ResetDigitalProtocol = resetDigitalProtocol,
            GpsPositioning = gpsPositioning,
            TimeZone = timeZone,
            GpsMode = gpsMode,
            VfoScanType = vfoScanType,
            VfoScanStartFreqUhf = vfoScanStartFreqUhf,
            VfoScanEndFreqUhf = vfoScanEndFreqUhf,
            VfoScanStartFreqVhf = vfoScanStartFreqVhf,
            VfoScanEndFreqVhf = vfoScanEndFreqVhf,
            AutoRepeaterA = autoRepeaterA,
            AutoRepeaterB = autoRepeaterB,
            AutoRepeater1Uhf = autoRepeater1Uhf,
            AutoRepeater1Vhf = autoRepeater1Vhf,
            AutoRepeater2Uhf = autoRepeater2Uhf,
            AutoRepeater2Vhf = autoRepeater2Vhf,
            RepeaterCheck = repeaterCheck,
            RepeaterCheckInterval = repeaterCheckInterval,
            RepeaterCheckReconnections = repeaterCheckReconnections,
            RepeaterOutOfRangeNotify = repeaterOutOfRangeNotify,
            OutOfRangeNotify = outOfRangeNotify,
            AutoRoaming = autoRoaming,
            AutoRoamingStartCondition = autoRoamingStartCondition,
            AutoRoamingFixedTime = autoRoamingFixedTime,
            RoamingEffectWaitTime = roamingEffectWaitTime,
            AutoRepeater1MinFreqVhf = autoRepeater1MinFreqVhf,
            AutoRepeater1MaxFreqVhf = autoRepeater1MaxFreqVhf,
            AutoRepeater1MinFreqUhf = autoRepeater1MinFreqUhf,
            AutoRepeater1MaxFreqUhf = autoRepeater1MaxFreqUhf,
            AutoRepeater2MinFreqVhf = autoRepeater2MinFreqVhf,
            AutoRepeater2MaxFreqVhf = autoRepeater2MaxFreqVhf,
            AutoRepeater2MinFreqUhf = autoRepeater2MinFreqUhf,
            AutoRepeater2MaxFreqUhf = autoRepeater2MaxFreqUhf,
            RepeaterMode = repeaterMode,
            RepCcLimit = repCcLimit,
            RepSlotA = repSlotA,
            RepSlotB = repSlotB,
            RepeaterWhitelist = repeaterWhitelist,
            RecordFunction = recordFunction,
            RecordDelay = recordDelay,
            MaxVolume = maxVolume,
            PowerOnVolumeType = powerOnVolumeType,
            PowerOnVolume = powerOnVolume,
            MaxHeadphoneVolume = maxHeadphoneVolume,
            DigiMicGain = digiMicGain,
            EnhancedSoundQuality = enhancedSoundQuality,
            AnalogMicGain = analogMicGain,
            RxAgc = rxAgc,
            NxMicGain = nxMicGain,
            SubSpkInTx = subSpkInTx,
            RxNoiseReduction = rxNoiseReduction,
            TxNoiseReduction = txNoiseReduction,
            SatLocation = satLocation,
            SatTxPower = satTxPower,
            SatAnaSql = satAnaSql,
            SatAosLimit = satAosLimit,
            RoamingZone = roamingZone
        };
    }
}
