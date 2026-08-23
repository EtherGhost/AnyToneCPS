using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using AnyToneCPS.Services.Radio.Codecs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static readonly Regex FrequencyPattern = new(@"^\d{3}\.\d{5}$", RegexOptions.Compiled);
    private IStoragePickerService _storagePicker = new NullStoragePickerService();
    private IProjectStorage? _currentProjectStorage;
    private bool _hasAttemptedAutoLoad;
    private bool _projectStructureDirty;
    private bool _suppressEditorRefresh;

    public ObservableCollection<ChannelEntry> Channels { get; } = [];
    // The Channels ListBox's full multi-selection (Desktop: Ctrl/Shift-click,
    // Mobile: long-press to enter selection mode then tap to toggle - see
    // SetSelectedChannels's doc comment) - drives bulk Copy/Delete and hides
    // ChannelDetailView's single-channel editor while more than one is
    // selected. SelectedChannel keeps meaning "the primary/last-clicked one"
    // (unchanged), same relationship as SelectedZoneMembers/SelectedZoneMember.
    public ObservableCollection<ChannelEntry> SelectedChannels { get; } = [];
    public ObservableCollection<ZoneEntry> Zones { get; } = [];
    public ObservableCollection<string> ValidationMessages { get; } = [];
    public ObservableCollection<ChannelEntry> AvailableZoneChannels { get; } = [];
    public ObservableCollection<ChannelEntry> SelectedAvailableZoneChannels { get; } = [];
    public ObservableCollection<ChannelEntry> SelectedZoneMembers { get; } = [];
    public ObservableCollection<RoamingChannelEntry> AvailableRoamingZoneChannels { get; } = [];
    public ObservableCollection<RoamingChannelEntry> SelectedAvailableRoamingZoneChannels { get; } = [];
    public ObservableCollection<RoamingChannelEntry> SelectedRoamingZoneMembers { get; } = [];
    public ObservableCollection<ChannelEntry> AvailableScanListChannels { get; } = [];
    public ObservableCollection<ChannelEntry> SelectedAvailableScanListChannels { get; } = [];
    public ObservableCollection<ChannelEntry> SelectedScanListMemberChannels { get; } = [];
    public ObservableCollection<AmAirEntry> AvailableAmZoneChannels { get; } = [];
    public ObservableCollection<AmAirEntry> SelectedAvailableAmZoneChannels { get; } = [];
    public ObservableCollection<AmAirEntry> SelectedAmZoneMembers { get; } = [];
    public ObservableCollection<AmAirEntry> AvailableAmZoneScanChannels { get; } = [];
    public ObservableCollection<AmAirEntry> SelectedAvailableAmZoneScanChannels { get; } = [];
    public ObservableCollection<AmAirEntry> SelectedAmZoneScanChannelMembers { get; } = [];
    public ObservableCollection<EncryptionKeyEntry> EncryptionKeys { get; } = [];
    public ObservableCollection<EncryptionKeyEntry> Arc4EncryptionKeys { get; } = [];
    public ObservableCollection<EncryptionKeyEntry> AesEncryptionKeys { get; } = [];
    // The 3 lists above always hold every slot (1..32/34/255, "Off" for
    // unset - see EnsureEncryptionKeySlotsPresent's own doc comment for
    // why). These 3 are the actual ListBox sources on both platforms -
    // occupied slots only, kept in sync by SyncEncryptionKeyVisibility -
    // so the UI looks and behaves like every other list in the app without
    // changing the underlying always-every-slot data model or write path.
    public ObservableCollection<EncryptionKeyEntry> VisibleEncryptionKeys { get; } = [];
    public ObservableCollection<EncryptionKeyEntry> VisibleArc4EncryptionKeys { get; } = [];
    public ObservableCollection<EncryptionKeyEntry> VisibleAesEncryptionKeys { get; } = [];
    public ObservableCollection<CsvPreviewRow> ChannelPreviewRows { get; } = [];
    public ObservableCollection<CsvPreviewRow> ZonePreviewRows { get; } = [];
    public ObservableCollection<string> ChannelPreviewHeaders { get; } = [];
    public ObservableCollection<string> ZonePreviewHeaders { get; } = [];

    public IReadOnlyList<string> ChannelTypes { get; } = ["A-Analog", "D-Digital", "A+D TX A", "D+A TX D"];
    // "Mid" (not "Middle") - confirmed exact vendor CPS label, english.ini id
    // 20058, see Docs/AnyTone_D890UV/field_options.json. Was wrong before
    // an audit against that file.
    public IReadOnlyList<string> TransmitPowers { get; } = ["Low", "Mid", "High", "Turbo"];
    public IReadOnlyList<string> Bandwidths { get; } = ["12.5K", "25K"];
    public IReadOnlyList<string> EncryptionModes { get; } = ["Off", "Digital", "AES", "ARC4"];
    public IReadOnlyList<string> RepeaterSlots { get; } = ["1", "2"];
    public IReadOnlyList<string> DigitalEncryptionKeyOptions => ["Off", .. EncryptionKeys.Select(key => key.Number.ToString(CultureInfo.InvariantCulture))];
    // Contact/RadioId/ScanList/ReceiveGroupList are stored on ChannelEntry as
    // raw 0-based radio indexes now (not names) - these Options lists back
    // the channel detail form's dropdowns, and the SelectedChannelXxxName
    // pass-through properties below translate a selected name back to the
    // matching index (name -> index resolution needs the live
    // Talkgroups/RadioIds/ScanLists/ReceiveGroupLists collections, which only
    // this ViewModel has access to - see ChannelEntry's class doc comment).
    public IReadOnlyList<string> ContactOptions => Talkgroups.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
    public IReadOnlyList<string> RadioIdOptions => RadioIds.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
    public IReadOnlyList<string> ScanListOptions => ["None", .. ScanLists.Select(s => s.Name).Where(n => !string.IsNullOrWhiteSpace(n))];
    public IReadOnlyList<string> ReceiveGroupListOptions => ["None", .. ReceiveGroupLists.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n))];

    /// <summary>Same name->index indirection as SelectedChannelContactName
    /// above (Zone lives on the global Zones list, not something
    /// GpsRoamingEntry can resolve on its own) - "Off" is the confirmed
    /// 255 sentinel, matching the vendor CPS's own text for an unset Zone.</summary>
    public IReadOnlyList<string> GpsRoamingZoneOptions => ["Off", .. Zones.Select(z => z.Name).Where(n => !string.IsNullOrWhiteSpace(n))];

    public string SelectedGpsRoamingZoneName
    {
        get => SelectedGpsRoaming?.ZoneDisplayName ?? "";
        set
        {
            if (SelectedGpsRoaming is null)
            {
                return;
            }

            if (value == "Off")
            {
                SelectedGpsRoaming.ZoneIndex = 255;
                SelectedGpsRoaming.ZoneDisplayName = "Off";
                OnPropertyChanged(nameof(SelectedGpsRoamingZoneName));
                return;
            }

            var match = Zones.FirstOrDefault(z => z.Name == value);
            if (match is null)
            {
                return;
            }

            SelectedGpsRoaming.ZoneIndex = match.Number - 1;
            SelectedGpsRoaming.ZoneDisplayName = match.Name;
            OnPropertyChanged(nameof(SelectedGpsRoamingZoneName));
        }
    }

    // OptionalSettings' Startup Zone/Channel A/B fields (Optional Settings
    // tab, "Power-on") - same reasoning as ContactOptions etc. above:
    // OptionalSettingsEntry is a raw-byte model with no access to the live
    // Zones collection, so the byte<->name resolution lives here. Startup
    // Channel A/B is restricted to the zone referenced by Startup Zone A/B
    // (a cascading picker, like Priority Channel 1/2 on Scan List), so its
    // Options list and resolved name both depend on the CURRENT Startup
    // Zone A/B value - see OnOptionalSettingsPropertyChanged for the
    // notification wiring this needs.
    public IReadOnlyList<string> OptionalSettingsZoneOptions => Zones.Select(z => z.DisplayLabel).ToList();

    public string OptionalSettingsStartupZoneAName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneA)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.StartupZoneA = (byte)(zone.Number - 1);
            }
        }
    }

    public string OptionalSettingsStartupZoneBName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneB)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.StartupZoneB = (byte)(zone.Number - 1);
            }
        }
    }

    public IReadOnlyList<string> OptionalSettingsStartupChannelAOptions =>
        Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneA)?.Members.Select(c => c.DisplayLabel).ToList() ?? [];

    // Confirmed 2026-07-20 via a live radio read cross-checked against the
    // real project's zone membership: this byte is the channel's POSITION
    // within the referenced zone's Members list, not a global channel
    // index - "PMR01" (Channel Number 600) can't fit in a byte as a global
    // index, but position 2 within Zone "CALL"'s members matches exactly.
    public string OptionalSettingsStartupChannelAName
    {
        get
        {
            var zone = Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneA);
            return zone?.Members.ElementAtOrDefault(OptionalSettings.StartupChannelA)?.DisplayLabel ?? "";
        }
        set
        {
            var zone = Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneA);
            var index = zone?.Members.ToList().FindIndex(c => c.DisplayLabel == value) ?? -1;
            if (index >= 0)
            {
                OptionalSettings.StartupChannelA = (byte)index;
            }
        }
    }

    public IReadOnlyList<string> OptionalSettingsStartupChannelBOptions =>
        Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneB)?.Members.Select(c => c.DisplayLabel).ToList() ?? [];

    public string OptionalSettingsStartupChannelBName
    {
        get
        {
            var zone = Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneB);
            return zone?.Members.ElementAtOrDefault(OptionalSettings.StartupChannelB)?.DisplayLabel ?? "";
        }
        set
        {
            var zone = Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.StartupZoneB);
            var index = zone?.Members.ToList().FindIndex(c => c.DisplayLabel == value) ?? -1;
            if (index >= 0)
            {
                OptionalSettings.StartupChannelB = (byte)index;
            }
        }
    }

    // Work Mode tab's Mem Zone A/B - a plain zone reference, no cascading
    // channel picker (unlike Startup Zone/Channel A/B above), confirmed via
    // the reference project's own optional_settings_dialog.cpp
    // (memZoneACmbx/memZoneBCmbx are populated with zone_names only).
    public string OptionalSettingsMemZoneAName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.MemZoneA)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.MemZoneA = (byte)(zone.Number - 1);
            }
        }
    }

    public string OptionalSettingsMemZoneBName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.MemZoneB)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.MemZoneB = (byte)(zone.Number - 1);
            }
        }
    }

    // Auto Repeater tab's Roaming Zone - a plain reference into RoamingZones
    // (a distinct entity/list from the regular Zones above), same Number-1
    // convention (RadioReadMapper.MapRoamingZones: Number = Index + 1).
    // Real offset (0xdb) confirmed 2026-07-28 via a live differential write -
    // the reference project's claimed 0xd5 is genuinely AddressBookSentWithCode,
    // not a real collision.
    public IReadOnlyList<string> OptionalSettingsRoamingZoneOptions => RoamingZones.Select(z => z.DisplayLabel).ToList();

    public string OptionalSettingsRoamingZoneName
    {
        get => RoamingZones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.RoamingZone)?.DisplayLabel ?? "";
        set
        {
            var zone = RoamingZones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.RoamingZone = (byte)(zone.Number - 1);
            }
        }
    }

    // Other tab's Priority Zone A/B - a plain zone reference like Work
    // Mode's Mem Zone A/B above, not a raw index display (was a TextBox
    // showing the raw byte before real zone pickers were added).
    public string OptionalSettingsPriorityZoneAName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.PriorityZoneA)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.PriorityZoneA = (byte)(zone.Number - 1);
            }
        }
    }

    public string OptionalSettingsPriorityZoneBName
    {
        get => Zones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.PriorityZoneB)?.DisplayLabel ?? "";
        set
        {
            var zone = Zones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.PriorityZoneB = (byte)(zone.Number - 1);
            }
        }
    }

    // AM/FM tab's FM Work Channel - added 2026-07-29 as a proper picker
    // instead of a raw-byte TextBox, same "resolve via the live collection,
    // OptionalSettingsEntry itself has no access to it" reason as the Zone
    // pickers above. Mapping confirmed 2026-07-29 via a live differential
    // write: the raw byte is a plain zero-based index (Number - 1), same
    // convention as every other picker here - now included in the radio-
    // write patch (OptionalSettingsCodec, offset 0x1d).
    //
    // The always-present "home"/VFO slot (FmChannelCodec.HomeIndex) is
    // excluded from FmChannels entirely by RadioReadMapper.MapFmChannels -
    // it's a different concept (the frequency used in FM VFO mode) from
    // "which memory channel is active," and previously showed up as a
    // confusing duplicate "FM CH 1" entry in this picker before that
    // exclusion moved upstream - see FmChannelEntry's doc comment.
    public IReadOnlyList<string> OptionalSettingsFmChannelOptions =>
        FmChannels.Select(c => c.DisplayLabel).ToList();

    public string OptionalSettingsFmWorkChannelName
    {
        get => FmChannels.FirstOrDefault(c => c.Number - 1 == OptionalSettings.FmWorkChannel)?.DisplayLabel ?? "";
        set
        {
            var channel = FmChannels.FirstOrDefault(c => c.DisplayLabel == value);
            if (channel is not null)
            {
                OptionalSettings.FmWorkChannel = (byte)(channel.Number - 1);
            }
        }
    }

    // AM Work Zone - kept as a disabled picker (IsEnabled="False" in XAML),
    // not removed, even though a 2026-07-29 live differential write
    // conclusively found it doesn't persist as an independent D890UV radio
    // setting (selecting a different work zone in the vendor CPS only
    // changes AM Offset's own byte, nothing else - see
    // OptionalSettingsCodec's doc comment). The control still exists in the
    // vendor CPS's own UI and may be real on other AnyTone models that
    // could become future projects here - disabling preserves the UI
    // groundwork instead of having to rebuild it from scratch later, same
    // precedent as the Vox/BT tab's disabled Bluetooth field group.
    public IReadOnlyList<string> OptionalSettingsAmZoneOptions => AmZones.Select(z => z.DisplayLabel).ToList();

    public string OptionalSettingsAmWorkZoneName
    {
        get => AmZones.FirstOrDefault(z => z.Number - 1 == OptionalSettings.AmWorkZone)?.DisplayLabel ?? "";
        set
        {
            var zone = AmZones.FirstOrDefault(z => z.DisplayLabel == value);
            if (zone is not null)
            {
                OptionalSettings.AmWorkZone = (byte)(zone.Number - 1);
            }
        }
    }

    // Analog Alarm's "Emergency ID" - AlarmSettingsEntry itself has no
    // access to the Channel-level DTMF/5Tone ID lists (DtmfIds/Tone5Ids,
    // both defined here on MainViewModel), same "resolve via the live
    // collection" reason as the FM Work Channel/AM Work Zone pickers above.
    // The reference project's own alert_settings_dialog.cpp shows Emergency
    // ID is only meaningful when ENI Type is DTMF or 5Tone - for None or
    // QDC1200 there's nothing to list (QDC1200 uses its own separate
    // Kind/Group ID/Private ID fields instead). NOT independently confirmed
    // against real hardware - inferred from the reference source only.
    //
    // KNOWN LIMITATION, deliberately deferred 2026-08-04: this shows
    // all 16 DtmfIds/Tone5Ids slots, but the real vendor
    // CPS only lists as many as are actually configured (only 2 real
    // 5Tone entries existed at test time, this showed 16). See DtmfIds' own doc comment above
    // for the full explanation - same underlying gap, deferred the same way.
    public IReadOnlyList<string> AlarmSettingsAnalogEmergencyIdOptions => AlarmSettings.AnalogEniType switch
    {
        1 => DtmfIds,
        2 => Tone5Ids,
        _ => []
    };

    public string AlarmSettingsAnalogEmergencyIdSelection
    {
        get
        {
            var options = AlarmSettingsAnalogEmergencyIdOptions;
            return AlarmSettings.AnalogEmergencyId < options.Count ? options[AlarmSettings.AnalogEmergencyId] : "";
        }
        set
        {
            var index = AlarmSettingsAnalogEmergencyIdOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                AlarmSettings.AnalogEmergencyId = (byte)index;
            }
        }
    }

    // Analog Alarm's "Emergency Channel" - filtered to analog channels only
    // (matches the reference project's own channel_type==0 filter for this
    // exact picker), same resolve-via-live-collection pattern as the FM
    // Work Channel/AM Work Zone pickers above.
    public IReadOnlyList<string> AlarmSettingsAnalogEmergencyChannelOptions =>
        Channels.Where(c => c.IsAnalog && c.RxFrequencyMHz > 0).Select(c => c.DisplayLabel).ToList();

    public string AlarmSettingsAnalogEmergencyChannelSelection
    {
        get => Channels.FirstOrDefault(c => c.Number - 1 == AlarmSettings.AnalogEmergencyChannel)?.DisplayLabel ?? "";
        set
        {
            var channel = Channels.FirstOrDefault(c => c.DisplayLabel == value);
            if (channel is not null)
            {
                AlarmSettings.AnalogEmergencyChannel = channel.Number - 1;
            }
        }
    }

    // Digital Alarm's "Emergency Channel" - filtered to digital channels
    // only, same reasoning as Analog's own Emergency Channel above. Capped
    // at Number<=65536 - AlarmSettingsEntry.DigitalEmergencyChannel is a
    // ushort (0-65535), confirmed by a live USB write capture 2026-08-04
    // (see AlarmSettingsCodec.Decode's doc comment on this field) - no real
    // codeplug gets anywhere near that many channels, so this is a
    // theoretical ceiling, not a practical constraint.
    public IReadOnlyList<string> AlarmSettingsDigitalEmergencyChannelOptions =>
        Channels.Where(c => !c.IsAnalog && c.RxFrequencyMHz > 0 && c.Number <= 65536).Select(c => c.DisplayLabel).ToList();

    public string AlarmSettingsDigitalEmergencyChannelSelection
    {
        get => Channels.FirstOrDefault(c => c.Number - 1 == AlarmSettings.DigitalEmergencyChannel)?.DisplayLabel ?? "";
        set
        {
            var channel = Channels.FirstOrDefault(c => c.DisplayLabel == value);
            if (channel is not null)
            {
                AlarmSettings.DigitalEmergencyChannel = (ushort)(channel.Number - 1);
            }
        }
    }

    // Constant, per-list option sets for the Scan List editor's 8 ComboBoxes
    // - declared as static lists on ScanListEntry itself (see its doc
    // comments for the vendor-CPS-confirmed source of each), exposed here
    // since Avalonia XAML compiled bindings resolve instance members, not
    // static ones, off a DataContext.
    public IReadOnlyList<string> ScanListPriorityChannelSelectOptions => ScanListEntry.PriorityChannelSelectOptions;
    public IReadOnlyList<string> ScanListRevertChannelOptions => ScanListEntry.RevertChannelOptions;
    public IReadOnlyList<string> ScanListLookBackTimeOptions => ScanListEntry.LookBackTimeOptions;
    public IReadOnlyList<string> ScanListDropoutDelayDwellTimeOptions => ScanListEntry.DropoutDelayDwellTimeOptions;

    public string SelectedChannelContactName
    {
        get => SelectedChannel?.ContactDisplayName ?? "";
        set
        {
            if (SelectedChannel is null)
            {
                return;
            }

            var match = Talkgroups.FirstOrDefault(t => t.Name == value);
            if (match is null)
            {
                return;
            }

            SelectedChannel.ContactIndex = (ushort)(match.Number - 1);
            SelectedChannel.ContactDisplayName = match.Name;
            OnPropertyChanged(nameof(SelectedChannelContactName));
        }
    }

    public string SelectedChannelRadioIdName
    {
        get => SelectedChannel?.RadioIdDisplayName ?? "";
        set
        {
            if (SelectedChannel is null)
            {
                return;
            }

            var match = RadioIds.FirstOrDefault(r => r.Name == value);
            if (match is null)
            {
                return;
            }

            SelectedChannel.RadioIdIndex = (ushort)(match.Number - 1);
            SelectedChannel.RadioIdDisplayName = match.Name;
            OnPropertyChanged(nameof(SelectedChannelRadioIdName));
        }
    }

    /// <summary>Scan List membership is NOT a per-channel field on the real
    /// radio - like Zone membership, it's stored as a list of channel
    /// references on the Scan List's own record (confirmed 2026-07-19; see
    /// ScanListEntry.Members's doc comment). This property is a convenience
    /// for the channel editor: it resolves/edits whichever scan list (if
    /// any) currently contains this channel, rather than reading/writing a
    /// field that lives on ChannelEntry itself.
    ///
    /// Deliberately does NOT remove the channel from every OTHER scan list
    /// before adding it to the chosen one (unlike this property's original,
    /// single-list-only implementation) - the real vendor CPS's own Scan
    /// List editor lets a channel belong to multiple scan lists at once (its
    /// "available channels" list only excludes channels already in THIS
    /// list, not any other), confirmed 2026-07-19 by reading the reference
    /// project's own scan_list_edit_dialog.cpp. A channel already in
    /// several lists just shows the first one found here - a known,
    /// accepted simplification of this convenience property, not a
    /// correctness issue for the underlying data (Views/MainView.axaml's
    /// own Scan List editor is the real, full multi-membership picker).</summary>
    public string SelectedChannelScanListName
    {
        get
        {
            if (SelectedChannel is null)
            {
                return "None";
            }

            var containingList = ScanLists.FirstOrDefault(s => s.Members.Contains(SelectedChannel));
            return containingList?.Name ?? "None";
        }
        set
        {
            if (SelectedChannel is null)
            {
                return;
            }

            if (value == "None")
            {
                // Explicit "un-assign" action via this picker - still
                // removes from every list (unlike picking a real list name,
                // which only adds), since there's no other way to express
                // "take this channel out of scan lists entirely" here.
                foreach (var scanList in ScanLists)
                {
                    scanList.Members.Remove(SelectedChannel);
                }
            }
            else
            {
                var match = ScanLists.FirstOrDefault(s => s.Name == value);
                if (match is not null && !match.Members.Contains(SelectedChannel))
                {
                    match.Members.Add(SelectedChannel);
                }
            }

            OnPropertyChanged(nameof(SelectedChannelScanListName));
        }
    }

    public string SelectedChannelReceiveGroupListName
    {
        get => SelectedChannel?.ReceiveGroupListDisplayName ?? "None";
        set
        {
            if (SelectedChannel is null)
            {
                return;
            }

            if (value == "None")
            {
                // 255 (0xFF), not 0, is the radio's real "no receive group
                // list" sentinel - confirmed live 2026-07-19 (see ValidateChannels).
                SelectedChannel.ReceiveGroupListIndex = 255;
                SelectedChannel.ReceiveGroupListDisplayName = "None";
                OnPropertyChanged(nameof(SelectedChannelReceiveGroupListName));
                return;
            }

            var match = ReceiveGroupLists.FirstOrDefault(g => g.Name == value);
            if (match is null)
            {
                return;
            }

            SelectedChannel.ReceiveGroupListIndex = (ushort)(match.Number - 1);
            SelectedChannel.ReceiveGroupListDisplayName = match.Name;
            OnPropertyChanged(nameof(SelectedChannelReceiveGroupListName));
        }
    }
    public IReadOnlyList<string> AesEncryptionKeyOptions => ["Off", .. AesEncryptionKeys.Select(key => key.Number.ToString(CultureInfo.InvariantCulture))];
    public IReadOnlyList<string> Arc4EncryptionKeyOptions => ["Off", .. Arc4EncryptionKeys.Select(key => key.Number.ToString(CultureInfo.InvariantCulture))];
    public IReadOnlyList<string> OnOffValues { get; } = ["Off", "On", "0", "1"];
    public IReadOnlyList<string> ContactCallTypes { get; } = ["Group Call", "Private Call", "All Call"];
    // CORRECTED 2026-07-31 - see ChannelCodec.BusyLockToString's doc comment:
    // analog and digital genuinely have different raw-value mappings, not a
    // shared 4-value enum with only index 0 relabeled (that 2026-07-17
    // finding was wrong). Digital's own 4-item list is unchanged.
    private static readonly IReadOnlyList<string> DigitalTxPermitValues = ["Always", "Channel Free", "Different Color Code", "Same Color Code"];
    private static readonly IReadOnlyList<string> AnalogBusyLockValues = ["Off", "Different CDT", "Channel Free"];
    // Drives ChannelDetailView's own "hide the editor while a bulk selection
    // is active" behavior - editing one field of several selected channels
    // at once is ambiguous, so the detail form only shows for 0 or 1.
    public bool IsSingleChannelSelected => SelectedChannels.Count <= 1;
    public string MultiChannelSelectionSummary => $"{SelectedChannels.Count} channels selected - Copy/Del act on all of them";
    public IReadOnlyList<string> BusyLockTxPermitValues => SelectedChannel?.IsDigital == true ? DigitalTxPermitValues : AnalogBusyLockValues;
    // "Busy Lock/TX Permit" was a static combined label - confirmed
    // 2026-07-31 the real vendor CPS shows a single type-dependent header
    // instead ("Busy Lock" for analog, "TX Permit" for digital), matching
    // the reference source's own analog/digital label-swap behavior
    // (channel_edit_dialog.cpp's setModeFormVisibility, though it uses "TX
    // Present" - the real vendor CPS's own wording, "TX Permit", wins).
    public string BusyLockTxPermitHeaderText => SelectedChannel?.IsDigital == true ? "TX Permit" : "Busy Lock";
    // Was missing 2 valid vendor combos before an audit against
    // Docs/AnyTone_D890UV/field_options.json (english.ini ids 20068-20072).
    // Full 5-item list is still the underlying raw 0-4 space
    // (ChannelCodec.SquelchModeToString) - kept here for whichever channel
    // type the real vendor CPS turns out to offer it all on (see
    // AnalogSquelchModes below for the analog-only restriction).
    public IReadOnlyList<string> SquelchModes { get; } = ["Carrier", "CTCSS/DCS", "Optional Signal", "CTC/DCS&Optional Signal", "CTC/DCS|Optional Signal"];
    // Confirmed 2026-07-31 against the real vendor CPS: analog
    // channels only ever offer "Carrier" here - the other 4 combos are not
    // selectable for analog. Digital's own Squelch Mode exposure is a
    // separate, not-yet-started task (raw storage/encoding is unaffected).
    public IReadOnlyList<string> AnalogSquelchModes { get; } = ["Carrier"];
    // Confirmed 2026-07-17 via a live differential test against real
    // hardware: 0=Off, 1=CTCSS, 2=DCS (see ChannelCodec.CtcssDcsModeToString).
    public IReadOnlyList<string> CtcssDcsModes { get; } = ["Off", "CTCSS", "DCS"];
    // RX/TX Color Code is a confirmed 0-15 field (write-safe since the
    // 2026-07-19 live differential test) - was a free-text box, real
    // vendor CPS uses a bounded dropdown.
    public IReadOnlyList<string> ColorCodeOptions { get; } = Enumerable.Range(0, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    // KNOWN LIMITATION, deliberately deferred 2026-08-04 (see task
    // "Revisit fixed 16-item DTMF/5Tone/2Tone/QDC ID lists once their
    // settings views exist"): DtmfIds/Tone2Ids/Tone5Ids/QdcIds/
    // Tone2DecodeOptions below always show all 16 slots, but the real
    // vendor CPS only shows as many as are actually configured in the
    // separate DTMF/5Tone/2Tone/QDC "settings" screens - confirmed live for
    // AlarmSettingsAnalogEmergencyIdOptions (2 real 5Tone
    // entries configured, this app showed 16). This app has never
    // reverse-engineered those settings tables (where the real configured
    // codes and their count actually live), only the 0-15 index reference
    // used here and by AlarmSettingsAnalogEmergencyIdOptions. Deliberately
    // left as-is rather than a piecemeal guess-fix - better to
    // wait until those settings views get built as their own entities,
    // since the real list/count will already be modeled then.
    //
    // Confirmed write-safe 2026-08-01 via a live differential test - a
    // 0-based index into the real vendor CPS's M1-M16 DTMF ID list, only
    // meaningful for analog channels with Optional Signal set to DTMF.
    public IReadOnlyList<string> DtmfIds { get; } = Enumerable.Range(1, 16).Select(i => $"M{i}").ToList();
    // Confirmed write-safe 2026-08-01 via a live differential test - a
    // 0-based index into the real vendor CPS's 2Tone settings list, only
    // meaningful for analog channels with Optional Signal set to 2Tone.
    public IReadOnlyList<string> Tone2Ids { get; } = Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    // Confirmed write-safe 2026-08-01 via a live differential test - a
    // 0-based index into the real vendor CPS's 5Tone settings list, only
    // meaningful for analog channels with Optional Signal set to 5Tone.
    public IReadOnlyList<string> Tone5Ids { get; } = Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    // Confirmed write-safe 2026-08-01 via a live differential test - a
    // 0-based index into the real vendor CPS's QDC1200 ID list (byte 0x42,
    // previously unclaimed), only meaningful for analog channels with
    // Optional Signal set to QDC1200.
    public IReadOnlyList<string> QdcIds { get; } = Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    // Confirmed write-safe 2026-08-01 via a live differential test (raw
    // 0/1 only - only 2 real 2Tone settings entries existed at test time,
    // so only 2 items showed in the vendor CPS dropdown). Treated as the
    // same 16-slot list as Tone2Ids/Tone5Ids rather
    // than a hardcoded 2-item field, since the vendor CPS almost certainly
    // just shows as many items as are configured, the same way this app's
    // own Contact/Radio ID/Scan List comboboxes work - not independently
    // confirmed beyond raw 0/1. Only meaningful for analog channels with
    // Optional Signal set to 2Tone.
    public IReadOnlyList<string> Tone2DecodeOptions { get; } = Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    // Confirmed write-safe 2026-08-01/2026-08-02 via live differential
    // tests - "1"/"2" are raw 0/1, "Customize" is raw 100 (a sentinel, no
    // extra custom-value field exists for it in the real vendor CPS UI).
    // Only meaningful for analog channels with Optional Signal set to
    // 5Tone.
    public IReadOnlyList<string> R5ToneOptions { get; } = ["1", "2", "Customize"];
    // Confirmed write-safe 2026-08-01 via a live differential test -
    // false=AES, true=ARC4 (byte 0x3b bit 5), independent of which key
    // index is populated. Digital-only, gated the same way as the AES
    // Key/ARC4 Key comboboxes (Optional Settings' Encryption Type must be
    // AES/ARC4).
    public IReadOnlyList<string> ExtendEncryptionOptions { get; } = ["AES", "ARC4"];
    // Confirmed write-safe 2026-08-01 via a live differential test - byte
    // 0x3b bit 7, false=Off, true=Low priority. Real vendor CPS also has
    // "High priority", but that write attempt failed with a communication
    // error before completing, so its encoding is unconfirmed. Listed here
    // for visibility/parity with the real vendor CPS, but blocked from
    // being selected via a ComboBox.Styles/DisabledWhenEqualsConverter
    // setup in the view (task #31) rather than guessed at or omitted.
    public IReadOnlyList<string> TxInterruptOptions { get; } = ["Off", "Low priority", "High priority"];
    // CORRECTED 2026-08-01: an earlier audit removed QDC1200 from this list,
    // reasoning that the on-wire field was only 2 bits (4 possible values).
    // That was the same class of mistake as the original SquelchMode bug -
    // a live differential test (real vendor CPS, channel AV00, Optional
    // Signal set to QDC1200) found it's actually a 3-bit field (bits 6-4 of
    // byte 0x1a), with QDC1200 as raw 4. See ChannelCodec.Decode's
    // OptionalSignal comment.
    public IReadOnlyList<string> OptionalSignals { get; } = ["Off", "DTMF", "2Tone", "5Tone", "QDC1200"];
    public IReadOnlyList<string> PttIds { get; } = ["Off", "Start", "End", "Start&End"];
    // CORRECTED 2026-07-31: the real vendor CPS "DMR Mode" dropdown has 4
    // items, not 3 - DMO/Simplex and Repeater are genuinely distinct
    // selections (a separate bit, ChannelEntry.DmrMode) layered on top of
    // the 3-value DCDM submode (ChannelEntry.DmrModeDcdm) - see
    // ChannelCodec.DmrModeSelectionToString's doc comment.
    public IReadOnlyList<string> DmrModes { get; } = ["DMO/simplex", "Repeater", "DCDM Double Slot", "DCDM TS Split"];
    public IReadOnlyList<string> ScrambleValues { get; } = ChannelEntry.ScrambleModeLabels;
    public IReadOnlyList<string> CustomScramblerValues { get; } = ChannelEntry.CustomScramblerLabels;
    public IReadOnlyList<string> ThemeModes { get; } = ["Dark", "Light", "System"];

    [ObservableProperty] private ChannelEntry? _selectedChannel;
    [ObservableProperty] private ZoneEntry? _selectedZone;
    [ObservableProperty] private EncryptionKeyEntry? _selectedEncryptionKey;
    [ObservableProperty] private EncryptionKeyEntry? _selectedArc4EncryptionKey;
    [ObservableProperty] private EncryptionKeyEntry? _selectedAesEncryptionKey;
    [ObservableProperty] private ChannelEntry? _availableZoneChannel;
    [ObservableProperty] private ChannelEntry? _selectedZoneMember;
    [ObservableProperty] private RoamingChannelEntry? _selectedRoamingZoneMember;
    [ObservableProperty] private string _currentProjectLocation = "";
    [ObservableProperty] private string _exportDirectory = GetDefaultExportDirectory();
    [ObservableProperty] private string _lastChannelImportLocation = "";
    [ObservableProperty] private string _lastZoneImportLocation = "";
    [ObservableProperty] private string _channelPreview = "";
    [ObservableProperty] private string _zonePreview = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isLoadingProject;
    [ObservableProperty] private bool _isSavingProject;

    /// <summary>Drives a single blocking overlay (MainView.axaml/
    /// MobileMainView.axaml, added 2026-08-07) shared by every operation
    /// slow enough to need one: project load/save and radio read/write.
    /// Read/write already had their own tab-scoped progress UI
    /// (<see cref="IsReadingFromRadio"/>/<see cref="IsWritingToRadio"/>) -
    /// this doesn't replace that, it just makes "something is happening"
    /// visible from any tab, and blocks input while a write is in
    /// flight instead of letting the user edit fields mid-write.</summary>
    public bool IsBusyOverlayVisible => IsLoadingProject || IsSavingProject || IsReadingFromRadio || IsWritingToRadio;

    /// <summary>Prefers the live, detailed progress text (RadioReadStatusText/
    /// RadioWriteStatusText - already updated throughout the operation via
    /// each one's own IProgress&lt;string&gt; callback, e.g. "Writing...
    /// (243/399 regions)") over a generic static string, falling back to the
    /// static string only before the first progress message arrives. Real
    /// gap found live 2026-08-17: the toolbar's own status text already
    /// showed this detail nicely, but this overlay - the one actually
    /// blocking input and telling the user something's happening - showed
    /// only "Writing to radio...", regardless of platform.</summary>
    public string BusyOverlayMessage =>
        IsLoadingProject ? "Loading project..."
        : IsSavingProject ? "Saving project..."
        : IsReadingFromRadio ? (string.IsNullOrEmpty(RadioReadStatusText) ? "Reading from radio..." : RadioReadStatusText)
        : IsWritingToRadio ? (string.IsNullOrEmpty(RadioWriteStatusText) ? "Writing to radio..." : RadioWriteStatusText)
        : "";

    partial void OnRadioReadStatusTextChanged(string value) => OnPropertyChanged(nameof(BusyOverlayMessage));
    partial void OnRadioWriteStatusTextChanged(string value) => OnPropertyChanged(nameof(BusyOverlayMessage));

    partial void OnIsLoadingProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusyOverlayVisible));
        OnPropertyChanged(nameof(BusyOverlayMessage));
    }

    partial void OnIsSavingProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusyOverlayVisible));
        OnPropertyChanged(nameof(BusyOverlayMessage));
    }

    /// <summary>Which of the 18 Optional Settings sub-tabs (Radio/Power-on/
    /// Alert Tone/.../Satellite) is showing, independent of
    /// <see cref="SelectedTabIndex"/> itself (which just selects the
    /// "Radio" top-level view, index 25). Added 2026-07-28 alongside
    /// MobileNavigationSections' "Radio Settings" leaves - Desktop's
    /// TabControl already had its own implicit selection with no VM-visible
    /// index, so the nav tree couldn't jump directly to a sub-tab before
    /// this; now both the tree AND the TabControl's own strip drive the
    /// same value, matching Channels/Zones' single-source-of-truth pattern.</summary>
    [ObservableProperty] private int _selectedOptionalSettingsSubTabIndex;
    [ObservableProperty] private NavigationTreeNode? _selectedNavigationNode;
    [ObservableProperty] private string _selectedThemeMode = "Dark";

    /// <summary>Drives the auto-dismissing startup safety popup reminding
    /// the user to turn VOX off before connecting the radio (see
    /// OptionalSettingsEntry.IsVoxOn's doc comment for the hazard) - shown
    /// once per app launch, unless <see cref="SuppressVoxStartupWarning"/>
    /// is set. Added 2026-07-30.</summary>
    [ObservableProperty] private bool _showVoxStartupWarning;
    [ObservableProperty] private bool _suppressVoxStartupWarning;

    public int ChannelCount => Channels.Count;
    public int ZoneCount => Zones.Count;
    public string DataStoreDescription => string.IsNullOrWhiteSpace(CurrentProjectLocation)
        ? "No codeplug file selected"
        : $"Codeplug: {CurrentProjectLocation}";
    public string ValidationSummary => ValidationMessages.Count == 0
        ? "OK"
        : $"{ValidationMessages.Count} issue(s)";

    /// <summary>Messages prefixed "Warning:" are informational (e.g. a zone's
    /// A/B channel not currently being a member - reflects live front-panel
    /// state, not a real invariant); anything else is a real constraint
    /// violation (out-of-range/malformed value) that could corrupt data on
    /// save or crash/misbehave on write - those block Save/Save As/Write to
    /// Radio rather than just being shown.</summary>
    public bool HasBlockingValidationErrors => ValidationMessages.Any(message => !message.StartsWith("Warning:", StringComparison.Ordinal));
    public bool IsDirty => _projectStructureDirty
        || Channels.Any(channel => channel.IsDirty)
        || Zones.Any(zone => zone.IsDirty)
        || ScanLists.Any(scanList => scanList.IsDirty);
    public string DirtyIndicator => IsDirty ? "Unsaved changes" : "Saved";
    public string BuildDescription { get; } = GetBuildDescription();
    public string AppVersion { get; } = GetAppVersion();
    public string BuildMode { get; } = GetBuildMode();
    public string NativeAotDescription => BuildMode.Equals("NativeAOT", StringComparison.OrdinalIgnoreCase)
        ? "NativeAOT"
        : "Inte NativeAOT";
    public string SettingsFileLocation => AppSettingsStore.SettingsPath;
    public string AppDataLocation => AppSettingsStore.SettingsDirectory;
    public bool IsChannelsViewSelected => SelectedTabIndex == 0;
    public bool IsZonesViewSelected => SelectedTabIndex == 1;
    // Split 2026-08-15 from a single TabIndex 2 view with an internal
    // Digital/ARC4/AES TabControl into 3 independent top-level views, same
    // "all tabs their own views" reasoning as the 22-entity split below -
    // each of the 3 encryption key kinds gets its own nav tree leaf now
    // instead of being hidden inside an in-page tab strip.
    public bool IsDigitalKeysViewSelected => SelectedTabIndex == 40;
    public bool IsArc4KeysViewSelected => SelectedTabIndex == 41;
    public bool IsAesKeysViewSelected => SelectedTabIndex == 42;
    public bool IsAnyKeysViewSelected => IsDigitalKeysViewSelected || IsArc4KeysViewSelected || IsAesKeysViewSelected;
    // The 22 entities below used to live as TabItems inside one shared
    // "Codeplug lists" TabControl (SelectedTabIndex == 3). Split 2026-07-19
    // into independent top-level views, matching Channels/Zones' own
    // "every entity gets its own top-level view" convention - each now gets its own
    // SelectedTabIndex slot and top-level content Grid, exactly matching
    // how Channels/Zones already work. Order matches the former TabControl's
    // child order exactly, so NavigationTree's grouping is unaffected.
    public bool IsRadioIdListViewSelected => SelectedTabIndex == 3;
    public bool IsTalkgroupsViewSelected => SelectedTabIndex == 4;
    public bool IsScanListsViewSelected => SelectedTabIndex == 5;
    public bool IsRoamingChannelsViewSelected => SelectedTabIndex == 6;
    public bool IsRoamingZonesViewSelected => SelectedTabIndex == 7;
    public bool IsReceiveGroupListsViewSelected => SelectedTabIndex == 8;
    public bool IsAutoRepeaterOffsetsViewSelected => SelectedTabIndex == 9;
    public bool IsAnalogAddressBookViewSelected => SelectedTabIndex == 10;
    public bool IsGpsRoamingViewSelected => SelectedTabIndex == 11;
    public bool IsTalkgroupWhitelistViewSelected => SelectedTabIndex == 12;
    public bool IsDigitalContactWhitelistViewSelected => SelectedTabIndex == 13;
    public bool IsDigitalContactsViewSelected => SelectedTabIndex == 14;
    public bool IsPrefabricatedSmsViewSelected => SelectedTabIndex == 15;
    public bool IsAmAirBandViewSelected => SelectedTabIndex == 16;
    public bool IsAmZoneViewSelected => SelectedTabIndex == 17;
    public bool IsFmBroadcastViewSelected => SelectedTabIndex == 18;
    public bool IsMasterIdViewSelected => SelectedTabIndex == 19;
    public bool IsTalkAliasSettingsViewSelected => SelectedTabIndex == 20;
    public bool IsAlarmSettingsViewSelected => SelectedTabIndex == 21;
    public bool IsAprsSettingsViewSelected => SelectedTabIndex == 22;
    public bool IsAprsFiltersViewSelected => SelectedTabIndex == 23;
    public bool IsRadioViewSelected => SelectedTabIndex == 25;
    public bool IsAnalogQuickCallViewSelected => SelectedTabIndex == 29;
    public bool IsStateInformationViewSelected => SelectedTabIndex == 30;
    public bool IsHotKeyViewSelected => SelectedTabIndex == 31;
    public bool IsQdc1200DecodeViewSelected => SelectedTabIndex == 32;
    public bool IsQdc1200EncodeViewSelected => SelectedTabIndex == 33;
    public bool IsQdcAddressBookViewSelected => SelectedTabIndex == 34;
    public bool IsFiveToneViewSelected => SelectedTabIndex == 35;
    public bool IsTwoToneEncodeViewSelected => SelectedTabIndex == 36;
    public bool IsTwoToneDecodeViewSelected => SelectedTabIndex == 37;
    public bool IsDtmfViewSelected => SelectedTabIndex == 38;
    // The 19 Optional Settings sub-tabs' own selection state, independent of
    // SelectedTabIndex (see SelectedOptionalSettingsSubTabIndex's doc
    // comment). Order matches MainView.axaml's Radio TabControl's TabItem
    // order exactly (index 0 = the TabControl's own first "Radio" tab).
    public bool IsOptionalSettingsRadioSubTabSelected => SelectedOptionalSettingsSubTabIndex == 0;
    public bool IsOptionalSettingsPowerOnSubTabSelected => SelectedOptionalSettingsSubTabIndex == 1;
    // Renamed from "Alert Zone" 2026-07-28 - "Zone" was a misnomer (this tab
    // has never had anything to do with Zones); the vendor CPS's own field
    // names are all "... Tone" (Call Permit Tone, Match End Tone, Call Reset
    // Tone). Its content was also merged with the former separate "Alert
    // Tone1" tab (SMS/Call/Talk-Permit tones + all 5 CallPermit/MatchEnd/
    // CallReset/UnMatchEnd/CallAll tone-matrix groups, one place instead of
    // two) - removing that tab shifted every subsequent SubTabIndex down by
    // one (GPS/Ranging 13->12 ... Satellite 18->17).
    public bool IsOptionalSettingsAlertToneSubTabSelected => SelectedOptionalSettingsSubTabIndex == 2;
    public bool IsOptionalSettingsPowerSaveSubTabSelected => SelectedOptionalSettingsSubTabIndex == 3;
    public bool IsOptionalSettingsDisplaySubTabSelected => SelectedOptionalSettingsSubTabIndex == 4;
    public bool IsOptionalSettingsWorkModeSubTabSelected => SelectedOptionalSettingsSubTabIndex == 5;
    public bool IsOptionalSettingsVoxBtSubTabSelected => SelectedOptionalSettingsSubTabIndex == 6;
    public bool IsOptionalSettingsSteSubTabSelected => SelectedOptionalSettingsSubTabIndex == 7;
    public bool IsOptionalSettingsAmFmSubTabSelected => SelectedOptionalSettingsSubTabIndex == 8;
    public bool IsOptionalSettingsKeyFunctionSubTabSelected => SelectedOptionalSettingsSubTabIndex == 9;
    public bool IsOptionalSettingsOtherSubTabSelected => SelectedOptionalSettingsSubTabIndex == 10;
    public bool IsOptionalSettingsDigitalFuncSubTabSelected => SelectedOptionalSettingsSubTabIndex == 11;
    public bool IsOptionalSettingsGpsRangingSubTabSelected => SelectedOptionalSettingsSubTabIndex == 12;
    public bool IsOptionalSettingsVfoScanSubTabSelected => SelectedOptionalSettingsSubTabIndex == 13;
    public bool IsOptionalSettingsAutoRepeaterSubTabSelected => SelectedOptionalSettingsSubTabIndex == 14;
    public bool IsOptionalSettingsRecordSubTabSelected => SelectedOptionalSettingsSubTabIndex == 15;
    public bool IsOptionalSettingsVolumeAudioSubTabSelected => SelectedOptionalSettingsSubTabIndex == 16;
    public bool IsOptionalSettingsSatelliteSubTabSelected => SelectedOptionalSettingsSubTabIndex == 17;
    // Split 2026-07-18 from one combined "Import/Export" section into two -
    // a shared page made the app feel CSV-first rather than radio-first, so
    // CSV import and export are now separate destinations rather than one
    // shared page.
    public bool IsImportsViewSelected => SelectedTabIndex == 26;
    public bool IsExportsViewSelected => SelectedTabIndex == 27;
    public bool IsSettingsViewSelected => SelectedTabIndex == 28;
    public bool IsDevOptionsViewSelected => SelectedTabIndex == 39;
    public bool IsAboutViewSelected => SelectedTabIndex == 43;
    public IReadOnlyList<ChannelEntry> SelectedZoneMemberOptions => SelectedZone?.Members.ToList() ?? [];
    public IReadOnlyList<AmAirEntry> SelectedAmZoneMemberOptions => SelectedAmZone?.Members.ToList() ?? [];

    /// <summary>ItemsSource for the Priority Channel 1/2 ComboBoxes - "None"
    /// plus SelectedScanList's own Members only (see ScanListEntry.
    /// PriorityChannel1Text's doc comment). Mirrors SelectedZoneMemberOptions'
    /// pattern (a MainViewModel-level wrapper, manually notified on the
    /// relevant change points, rather than binding XAML directly to
    /// SelectedScanList.Members).</summary>
    public IReadOnlyList<string> SelectedScanListMemberOptions => new[] { "None" }.Concat(SelectedScanList?.Members.Select(m => m.DisplayLabel) ?? []).ToList();

    /// <summary>
    /// Tree navigation shown in the sidebar, added 2026-07-18 to replace the
    /// flat 7-item list (one of which, "Codeplug", hid all 22 entity types
    /// behind a single unlabeled tab strip - a temporary/CSV-tool feel that
    /// didn't fit a real CPS replacement), then updated 2026-07-19 once the "Codeplug
    /// lists" TabControl itself was removed - every entity leaf now carries
    /// its own independent <see cref="SelectedTabIndex"/> value (matching a
    /// dedicated top-level content Grid), same as Channels/Zones. Regrouped
    /// 2026-08-14: Roaming split out of "Common"/"Analog" into its own
    /// category, the previously ungrouped QDC/5Tone/2Tone/DTMF entries
    /// (plus "Analog Quick Call", moved out of "Hot Keys") gathered under a
    /// new "Signaling" category, "Common"/"Advanced" renamed to "Channels &
    /// Zones"/"Alerts", and "Radio"/"Encryption Keys" moved in from
    /// top-level siblings of "D890UV" to children of it, since they're
    /// radio-specific data rather than app-level. If this app ever supports
    /// a second radio model, "D890UV" becomes the first of several
    /// radio-root nodes rather than a hardcoded label.
    /// </summary>
    public IReadOnlyList<NavigationTreeNode> NavigationTree { get; } =
    [
        new NavigationTreeNode("D890UV", Children:
        [
            new NavigationTreeNode("Channels & Zones", Children:
            [
                new NavigationTreeNode("Channels", TabIndex: 0),
                new NavigationTreeNode("Zones", TabIndex: 1),
                new NavigationTreeNode("Scan Lists", TabIndex: 5),
                new NavigationTreeNode("Receive Group Lists", TabIndex: 8),
                new NavigationTreeNode("Auto Repeater Offsets", TabIndex: 9)
            ]),
            new NavigationTreeNode("Roaming", Children:
            [
                new NavigationTreeNode("Roaming Channels", TabIndex: 6),
                new NavigationTreeNode("Roaming Zones", TabIndex: 7),
                new NavigationTreeNode("GPS Roaming", TabIndex: 11)
            ]),
            new NavigationTreeNode("DMR", Children:
            [
                new NavigationTreeNode("Radio ID List", TabIndex: 3),
                new NavigationTreeNode("Talkgroups", TabIndex: 4),
                new NavigationTreeNode("Digital Contacts", TabIndex: 14),
                new NavigationTreeNode("Digital Contact Whitelist", TabIndex: 13),
                new NavigationTreeNode("Talkgroup Whitelist", TabIndex: 12),
                new NavigationTreeNode("Master ID", TabIndex: 19),
                new NavigationTreeNode("Prefabricated SMS", TabIndex: 15)
            ]),
            new NavigationTreeNode("Analog", Children:
            [
                new NavigationTreeNode("Analog Address Book", TabIndex: 10),
                new NavigationTreeNode("AM Air Band", TabIndex: 16),
                new NavigationTreeNode("AM Zone", TabIndex: 17),
                new NavigationTreeNode("FM Broadcast", TabIndex: 18)
            ]),
            new NavigationTreeNode("APRS", Children:
            [
                new NavigationTreeNode("APRS Settings", TabIndex: 22),
                new NavigationTreeNode("APRS Filters", TabIndex: 23)
            ]),
            new NavigationTreeNode("Signaling", Children:
            [
                new NavigationTreeNode("QDC 1200 Settings", Children:
                [
                    new NavigationTreeNode("Decode", TabIndex: 32),
                    new NavigationTreeNode("Encode", TabIndex: 33)
                ]),
                new NavigationTreeNode("QDC Address Book", TabIndex: 34),
                new NavigationTreeNode("5Tone Settings", TabIndex: 35),
                new NavigationTreeNode("2Tone Settings", Children:
                [
                    new NavigationTreeNode("Encode", TabIndex: 36),
                    new NavigationTreeNode("Decode", TabIndex: 37)
                ]),
                new NavigationTreeNode("DTMF Settings", TabIndex: 38),
                new NavigationTreeNode("Analog Quick Call", TabIndex: 29)
            ]),
            new NavigationTreeNode("Hot Keys", Children:
            [
                new NavigationTreeNode("State Information", TabIndex: 30),
                new NavigationTreeNode("Hot Keys", TabIndex: 31)
            ]),
            new NavigationTreeNode("Alerts", Children:
            [
                new NavigationTreeNode("Talk Alias Settings", TabIndex: 20),
                new NavigationTreeNode("Alarm Settings", TabIndex: 21)
            ]),
            // The 19 tabs inside the "Radio" view's own TabControl (see
            // SelectedOptionalSettingsSubTabIndex's doc comment) - added
            // 2026-07-28 so both the tree AND mobile's nav (which has no
            // in-page tab strip at all) can jump directly to one instead of
            // clicking "Radio" then hunting across a wide tab strip. Every
            // leaf sets TabIndex 25 (selects "Radio") plus its own
            // SubTabIndex - "Radio" itself (the base read/write tab, index
            // 0) is reachable via the "Radio" leaf just below, not repeated
            // here.
            new NavigationTreeNode("Radio Settings", Children:
            [
                new NavigationTreeNode("Power-on", TabIndex: 25, SubTabIndex: 1),
                new NavigationTreeNode("Alert Tone", TabIndex: 25, SubTabIndex: 2),
                new NavigationTreeNode("Power Save", TabIndex: 25, SubTabIndex: 3),
                new NavigationTreeNode("Display", TabIndex: 25, SubTabIndex: 4),
                new NavigationTreeNode("Work Mode", TabIndex: 25, SubTabIndex: 5),
                new NavigationTreeNode("Vox/BT", TabIndex: 25, SubTabIndex: 6),
                new NavigationTreeNode("STE", TabIndex: 25, SubTabIndex: 7),
                new NavigationTreeNode("AM/FM", TabIndex: 25, SubTabIndex: 8),
                new NavigationTreeNode("Key Function", TabIndex: 25, SubTabIndex: 9),
                new NavigationTreeNode("Other", TabIndex: 25, SubTabIndex: 10),
                new NavigationTreeNode("Digital Func", TabIndex: 25, SubTabIndex: 11),
                new NavigationTreeNode("GPS/Ranging", TabIndex: 25, SubTabIndex: 12),
                new NavigationTreeNode("VFO Scan", TabIndex: 25, SubTabIndex: 13),
                new NavigationTreeNode("Auto repeater", TabIndex: 25, SubTabIndex: 14),
                new NavigationTreeNode("Record", TabIndex: 25, SubTabIndex: 15),
                new NavigationTreeNode("Volume/Audio", TabIndex: 25, SubTabIndex: 16),
                new NavigationTreeNode("Satellite", TabIndex: 25, SubTabIndex: 17)
            ]),
            new NavigationTreeNode("Radio", TabIndex: 25, SubTabIndex: 0),
            new NavigationTreeNode("Encryption Keys", Children:
            [
                new NavigationTreeNode("Digital", TabIndex: 40),
                new NavigationTreeNode("ARC4", TabIndex: 41),
                new NavigationTreeNode("AES", TabIndex: 42)
            ])
        ]),
        new NavigationTreeNode("Imports", TabIndex: 26) { IsEnabled = false, DisabledReason = "CSV import is disabled during the Channel canonical-model migration - not yet available in this version." },
        new NavigationTreeNode("Exports", TabIndex: 27) { IsEnabled = false, DisabledReason = "CSV export is disabled during the Channel canonical-model migration - not yet available in this version." },
        new NavigationTreeNode("Settings", TabIndex: 28),
        new NavigationTreeNode("Dev Options", TabIndex: 39) { IsVisible = false },
        new NavigationTreeNode("About", TabIndex: 43)
    ];

    /// <summary>Mobile's nav flyout (MobileMainView.axaml, added 2026-07-28)
    /// renders <see cref="NavigationTree"/> as Expander sections instead of
    /// a TreeView - a flat scrolling ListBox of all 27 entities was the
    /// original mobile nav, replaced for the same "hard to scan" reason the
    /// desktop sidebar's old flat list was. Flattens away the single
    /// "D890UV" wrapper node so its real categories (Channels & Zones/
    /// Roaming/DMR/Analog/APRS/Signaling/Hot Keys/Alerts) become top-level
    /// sections directly, saving a tap on the common case of exactly one
    /// radio root - if a second radio model is ever added, this flatten
    /// becomes lossy (both radios' categories would appear side by side
    /// with no grouping) and should be revisited alongside NavigationTree's
    /// own doc comment on the same topic.</summary>
    public IReadOnlyList<NavigationTreeNode> MobileNavigationSections =>
        NavigationTree.SelectMany(node => node.HasChildren ? node.Children : [node]).ToList();

    partial void OnSelectedNavigationNodeChanged(NavigationTreeNode? value)
    {
        if (value?.IsEnabled == false)
        {
            return;
        }

        if (value?.TabIndex is { } tabIndex)
        {
            SelectedTabIndex = tabIndex;
        }

        if (value?.SubTabIndex is { } subTabIndex)
        {
            SelectedOptionalSettingsSubTabIndex = subTabIndex;
        }
    }

    public MainViewModel()
    {
        Channels.CollectionChanged += OnChannelsChanged;
        Zones.CollectionChanged += OnZonesChanged;
        ScanLists.CollectionChanged += OnScanListsChanged;
        EncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;
        Arc4EncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;
        AesEncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;
        // DMR-ID-bearing entities added 2026-08-08 alongside DmrIdText's
        // ObservableValidator conversion - without per-item forwarding,
        // editing an existing row's DmrIdText updates that field's own red
        // border (Avalonia's INotifyDataErrorInfo binding is automatic) but
        // never reaches ValidationMessages/HasBlockingValidationErrors,
        // which only refresh via RefreshValidation - same gap Channel/Zone/
        // ScanList already closed with their own AttachXxxHandlers.
        Talkgroups.CollectionChanged += OnTalkgroupsChanged;
        RadioIds.CollectionChanged += OnRadioIdsChanged;
        TalkgroupWhitelist.CollectionChanged += OnTalkgroupWhitelistChanged;
        DigitalContactWhitelist.CollectionChanged += OnDigitalContactWhitelistChanged;
        // AprsSettings.DigitalReports is a fixed 8-slot list (never added/
        // removed after AprsSettingsEntry's own constructor populates it),
        // so it's attached directly here rather than via CollectionChanged -
        // same reasoning as FiveToneSettings.BotSpecialCall/EotSpecialCall
        // below.
        foreach (var report in AprsSettings.DigitalReports)
        {
            report.PropertyChanged += OnEditorPropertyChanged;
        }
        RadioReadWarnings.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRadioReadWarnings));
        WireCoreEntityOptionNotifications();
        WireHotKeyNotifications();
        WireQdc1200Notifications();
        WireQdcAddressBookNotifications();
        WireFiveToneNotifications();
        WireTwoToneNotifications();
        WireDtmfNotifications();

        SeedData();
        SelectedChannel = Channels.FirstOrDefault();
        SelectedZone = Zones.FirstOrDefault();
        AvailableZoneChannel = Channels.FirstOrDefault();

        MarkProjectClean();
        RefreshValidationAndPreview("Ready");
        _ = LoadAppSettingsAsync();
    }

    public async void SetStoragePicker(IStoragePickerService storagePicker)
    {
        _storagePicker = storagePicker;
        if (!_hasAttemptedAutoLoad)
        {
            _hasAttemptedAutoLoad = true;
            await LoadRememberedProjectAsync();
        }
    }

    /// <summary>Called by the Channels ListBox's own SelectionChanged handler
    /// on both platforms (Desktop: Ctrl/Shift-click via SelectionMode=Multiple;
    /// Mobile: tap-to-toggle once long-press has entered selection mode) -
    /// keeps <see cref="SelectedChannels"/> in sync with whatever the ListBox
    /// itself reports as selected, the same one-way view-to-VM pattern every
    /// other multi-select list in this app already uses (see
    /// SetSelectedAvailableZoneChannels above).</summary>
    public void SetSelectedChannels(IEnumerable<ChannelEntry> channels)
    {
        ReplaceSelection(SelectedChannels, channels);
        DuplicateChannelCommand.NotifyCanExecuteChanged();
        RemoveChannelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSingleChannelSelected));
        OnPropertyChanged(nameof(MultiChannelSelectionSummary));
    }

    public void SetSelectedAvailableZoneChannels(IEnumerable<ChannelEntry> channels)
    {
        ReplaceSelection(SelectedAvailableZoneChannels, channels);
        AddZoneMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedZoneMembers(IEnumerable<ChannelEntry> channels)
    {
        ReplaceSelection(SelectedZoneMembers, channels);
        SelectedZoneMember = SelectedZoneMembers.FirstOrDefault();
        RemoveZoneMembersCommand.NotifyCanExecuteChanged();
        MoveZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveZoneMemberDownCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAvailableRoamingZoneChannels(IEnumerable<RoamingChannelEntry> channels)
    {
        ReplaceSelection(SelectedAvailableRoamingZoneChannels, channels);
        AddRoamingZoneMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedRoamingZoneMembers(IEnumerable<RoamingChannelEntry> channels)
    {
        ReplaceSelection(SelectedRoamingZoneMembers, channels);
        SelectedRoamingZoneMember = SelectedRoamingZoneMembers.FirstOrDefault();
        RemoveRoamingZoneMembersCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberDownCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAvailableScanListChannels(IEnumerable<ChannelEntry> channels)
    {
        ReplaceSelection(SelectedAvailableScanListChannels, channels);
        AddScanListMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedScanListMemberChannels(IEnumerable<ChannelEntry> channels)
    {
        ReplaceSelection(SelectedScanListMemberChannels, channels);
        RemoveScanListMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAvailableAmZoneChannels(IEnumerable<AmAirEntry> channels)
    {
        ReplaceSelection(SelectedAvailableAmZoneChannels, channels);
        AddAmZoneMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAmZoneMembers(IEnumerable<AmAirEntry> channels)
    {
        ReplaceSelection(SelectedAmZoneMembers, channels);
        RemoveAmZoneMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAvailableAmZoneScanChannels(IEnumerable<AmAirEntry> channels)
    {
        ReplaceSelection(SelectedAvailableAmZoneScanChannels, channels);
        AddAmZoneScanChannelMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedAmZoneScanChannelMembers(IEnumerable<AmAirEntry> channels)
    {
        ReplaceSelection(SelectedAmZoneScanChannelMembers, channels);
        RemoveAmZoneScanChannelMembersCommand.NotifyCanExecuteChanged();
    }

    public async Task<bool> ConfirmCanDiscardUnsavedChangesAsync()
    {
        return !IsDirty || await _storagePicker.ConfirmDiscardUnsavedChangesAsync();
    }

    private async Task LoadRememberedProjectAsync()
    {
        try
        {
            var projectStorage = await OpenRememberedProjectOnBackgroundAsync();
            if (projectStorage is null)
            {
                return;
            }

            var data = await LoadProjectDataOnBackgroundAsync(projectStorage);
            if (data is null)
            {
                StatusMessage = "Remembered codeplug not found";
                return;
            }

            RadioProjectMapper.LoadInto(
                data, Channels, Zones, EncryptionKeys, Arc4EncryptionKeys, AesEncryptionKeys,
                RadioIds, Talkgroups, ScanLists, RoamingChannels, RoamingZones, ReceiveGroupLists, AutoRepeaterOffsets,
                MasterId, TalkAliasSettings, AnalogAddresses, GpsRoamingEntries, TalkgroupWhitelist, PrefabricatedSmsMessages, AmAirChannels, AmZones, FmChannels, AlarmSettings, DigitalContactWhitelist, AprsSettings, AprsReceiveFilters, OptionalSettings, DigitalContacts);
            EnsureEncryptionKeySlotsPresent();
            EnsureGpsRoamingSlotsPresent();
            EnsureHotKeySlotsPresent();
            EnsureDtmfEncodeSlotsPresent();
            NotifyAllEntityCounts();
            // RadioProjectMapper.LoadInto mutates DigitalContacts directly,
            // not through any of the paths that already call this
            // (Read From Radio, add/remove) - without it, the list view
            // (bound to FilteredDigitalContacts, not DigitalContacts
            // itself) stays stuck showing whatever it had before this load,
            // real bug found live 2026-08-16 (contacts only "appeared"
            // after toggling the Friends Only filter, which happens to
            // trigger this same refresh for an unrelated reason).
            RefreshFilteredDigitalContacts();
            SelectedChannel = Channels.FirstOrDefault();
            SelectedZone = Zones.FirstOrDefault();
            AvailableZoneChannel = Channels.FirstOrDefault();
            _currentProjectStorage = projectStorage;
            CurrentProjectLocation = projectStorage.DisplayLocation;
            // See LoadProject's identical line for why this is restored from
            // the file rather than always reset - this is the startup
            // auto-load path, same DigitalContactsGenuinelyPopulatedFromRadio
            // provenance concern applies here too.
            _digitalContactsGenuinelyPopulatedFromRadio = data.DigitalContactsGenuinelyPopulatedFromRadio;
            OnPropertyChanged(nameof(CanIncludeDigitalContactsInWrite));
            MarkProjectClean();
            RefreshValidationAndPreview();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Auto-load failed: {exception.Message}";
        }
    }

    public void MarkProjectClean()
    {
        _projectStructureDirty = false;
        _suppressEditorRefresh = true;
        try
        {
            foreach (var channel in Channels)
            {
                channel.MarkClean();
            }

            foreach (var zone in Zones)
            {
                zone.MarkClean();
            }

            foreach (var scanList in ScanLists)
            {
                scanList.MarkClean();
            }

            foreach (var amAir in AmAirChannels)
            {
                amAir.MarkClean();
            }

            foreach (var amZone in AmZones)
            {
                amZone.MarkClean();
            }

            foreach (var sms in PrefabricatedSmsMessages)
            {
                sms.MarkClean();
            }

            foreach (var fmChannel in FmChannels)
            {
                fmChannel.MarkClean();
            }

            foreach (var autoRepeaterOffset in AutoRepeaterOffsets)
            {
                autoRepeaterOffset.MarkClean();
            }
        }
        finally
        {
            _suppressEditorRefresh = false;
        }

        OnPropertyChanged(nameof(DigitalEncryptionKeyOptions));
        OnPropertyChanged(nameof(AesEncryptionKeyOptions));
        OnPropertyChanged(nameof(Arc4EncryptionKeyOptions));
        NotifyDirtyStateChanged();
    }

    [RelayCommand]
    private async Task LoadProject()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "Open cancelled";
            return;
        }

        var projectStorage = await _storagePicker.PickOpenProjectAsync();
        if (projectStorage is null)
        {
            StatusMessage = "Open cancelled";
            return;
        }

        IsLoadingProject = true;
        try
        {
            var data = await LoadProjectDataOnBackgroundAsync(projectStorage);
            if (data is null)
            {
                StatusMessage = "No saved project found";
                return;
            }

            RadioProjectMapper.LoadInto(
                data, Channels, Zones, EncryptionKeys, Arc4EncryptionKeys, AesEncryptionKeys,
                RadioIds, Talkgroups, ScanLists, RoamingChannels, RoamingZones, ReceiveGroupLists, AutoRepeaterOffsets,
                MasterId, TalkAliasSettings, AnalogAddresses, GpsRoamingEntries, TalkgroupWhitelist, PrefabricatedSmsMessages, AmAirChannels, AmZones, FmChannels, AlarmSettings, DigitalContactWhitelist, AprsSettings, AprsReceiveFilters, OptionalSettings, DigitalContacts);
            EnsureEncryptionKeySlotsPresent();
            EnsureGpsRoamingSlotsPresent();
            EnsureHotKeySlotsPresent();
            EnsureDtmfEncodeSlotsPresent();
            NotifyAllEntityCounts();
            // RadioProjectMapper.LoadInto mutates DigitalContacts directly,
            // not through any of the paths that already call this
            // (Read From Radio, add/remove) - without it, the list view
            // (bound to FilteredDigitalContacts, not DigitalContacts
            // itself) stays stuck showing whatever it had before this load,
            // real bug found live 2026-08-16 (contacts only "appeared"
            // after toggling the Friends Only filter, which happens to
            // trigger this same refresh for an unrelated reason).
            RefreshFilteredDigitalContacts();
            SelectedChannel = Channels.FirstOrDefault();
            SelectedZone = Zones.FirstOrDefault();
            AvailableZoneChannel = Channels.FirstOrDefault();
            _currentProjectStorage = projectStorage;
            CurrentProjectLocation = projectStorage.DisplayLocation;
            await _storagePicker.RememberProjectAsync(projectStorage);
            // This project's channels no longer correspond to whatever raw
            // bytes an earlier Read From Radio may have cached - a write
            // must re-read before it can safely patch anything again.
            _cachedRadioSnapshot = null;
            // Restored from the file rather than always reset to false - a
            // Digital Contact List that really did come from a genuine Read
            // From Radio (on this device or another) stays write-eligible
            // after a save/load round trip. See RadioProjectData.
            // DigitalContactsGenuinelyPopulatedFromRadio's own doc comment
            // (real bug found 2026-08-16: loading a project on Android
            // always disabled the write-side checkbox, even for a list that
            // traced back to a genuine Desktop read, since this flag was
            // never saved anywhere before).
            _digitalContactsGenuinelyPopulatedFromRadio = data.DigitalContactsGenuinelyPopulatedFromRadio;
            OnPropertyChanged(nameof(CanIncludeDigitalContactsInWrite));
            WriteChangesToRadioCommand.NotifyCanExecuteChanged();
            MarkProjectClean();
            RefreshValidationAndPreview();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            StatusMessage = $"Open failed: {exception.Message}";
        }
        finally
        {
            IsLoadingProject = false;
        }
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "New cancelled";
            return;
        }

        DetachChannelHandlers(Channels);
        DetachZoneHandlers(Zones);
        Channels.Clear();
        Zones.Clear();
        EncryptionKeys.Clear();
        Arc4EncryptionKeys.Clear();
        AesEncryptionKeys.Clear();

        SeedData();
        NotifyAllEntityCounts();
        SelectedChannel = Channels.FirstOrDefault();
        SelectedZone = Zones.FirstOrDefault();
        AvailableZoneChannel = Channels.FirstOrDefault();
        _currentProjectStorage = null;
        CurrentProjectLocation = "";
        await _storagePicker.ForgetRememberedProjectAsync();
        // See LoadProject's identical comment - a fresh project has no
        // corresponding raw radio bytes cached.
        _cachedRadioSnapshot = null;
        _digitalContactsGenuinelyPopulatedFromRadio = false;
        OnPropertyChanged(nameof(CanIncludeDigitalContactsInWrite));
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        MarkProjectClean();
        RefreshValidationAndPreview("New codeplug");
    }

    private static void SortByNumber<T>(ObservableCollection<T> list, Func<T, int> numberSelector)
    {
        var sorted = list.OrderBy(numberSelector).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var currentIndex = list.IndexOf(sorted[i]);
            if (currentIndex != i)
            {
                list.Move(currentIndex, i);
            }
        }
    }

    /// <summary>Restores ascending Number/Index order before every Save -
    /// list position otherwise just reflects insertion order (append on
    /// Add, append on Duplicate), which drifts out of numeric order the
    /// moment an entry's own "No" field is hand-edited to a different
    /// value. Uses ObservableCollection.Move rather than a Clear+rebuild,
    /// so bound SelectedXxx references stay valid.</summary>
    internal void ReorderListsByNumber()
    {
        SortByNumber(Channels, c => c.Number);
        SortByNumber(Zones, z => z.Number);
        SortByNumber(ScanLists, s => s.Number);
        SortByNumber(ReceiveGroupLists, r => r.Number);
        SortByNumber(RadioIds, r => r.Number);
        SortByNumber(Talkgroups, t => t.Number);
        SortByNumber(RoamingChannels, r => r.Number);
        SortByNumber(RoamingZones, r => r.Number);
        SortByNumber(AutoRepeaterOffsets, a => a.Number);
        SortByNumber(AnalogAddresses, a => a.Number);
        SortByNumber(GpsRoamingEntries, g => g.Number);
        SortByNumber(TalkgroupWhitelist, t => t.Number);
        SortByNumber(DigitalContactWhitelist, d => d.Number);
        SortByNumber(PrefabricatedSmsMessages, p => p.Number);
        SortByNumber(AmAirChannels, a => a.Number);
        SortByNumber(AmZones, a => a.Number);
        SortByNumber(FmChannels, f => f.Number);
        SortByNumber(DigitalContacts, d => d.Index);
        SortByNumber(StateInformationEntries, s => s.Number);
        SortByNumber(AnalogQuickCalls, a => a.Number);
        SortByNumber(Qdc1200Ids, q => q.Number);
        SortByNumber(QdcAddresses, q => q.Number);
        SortByNumber(FiveToneIds, f => f.Number);
        SortByNumber(TwoToneEncodeEntries, t => t.Number);
        SortByNumber(TwoToneDecodeEntries, t => t.Number);

        RefreshFilteredDigitalContacts();
    }

    /// <summary>Simplex (OffsetDirection == 0) ignores OffsetMHz when
    /// computing TX (see ChannelEntry.ComputeTransmitFrequencyMHz), so a
    /// stale value left over from before an RX edit is invisible in the UI
    /// - confirmed 2026-08-23 against a real saved project file (11
    /// channels carrying a pre-edit OffsetMHz that no longer matched RX).
    /// ChannelEntry's own OnRxFrequencyMHzChanged/OnOffsetDirectionChanged
    /// hooks now keep this in sync going forward during live editing; this
    /// is the defense-in-depth pass at the save boundary that also heals
    /// any channel that went stale before that fix existed, or by some
    /// future path that doesn't go through those hooks.</summary>
    internal void NormalizeSimplexChannelOffsets()
    {
        foreach (var channel in Channels)
        {
            if (channel.OffsetDirection == 0 && channel.OffsetMHz != channel.RxFrequencyMHz)
            {
                channel.OffsetMHz = channel.RxFrequencyMHz;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProject()
    {
        var projectStorage = _currentProjectStorage;
        if (projectStorage is null)
        {
            projectStorage = await _storagePicker.PickSaveProjectAsync("SE_Field_Comms_D890UV_v1.dat");
            if (projectStorage is null)
            {
                StatusMessage = "Save cancelled";
                return;
            }

            if (!await _storagePicker.ConfirmOverwriteAsync(projectStorage))
            {
                StatusMessage = "Save cancelled";
                return;
            }
        }

        NormalizeSimplexChannelOffsets();
        ReorderListsByNumber();
        IsSavingProject = true;
        try
        {
            var data = RadioProjectMapper.ToData(
                Channels, Zones, EncryptionKeys, Arc4EncryptionKeys, AesEncryptionKeys,
                RadioIds, Talkgroups, ScanLists, RoamingChannels, RoamingZones, ReceiveGroupLists, AutoRepeaterOffsets,
                MasterId, TalkAliasSettings, AnalogAddresses, GpsRoamingEntries, TalkgroupWhitelist, PrefabricatedSmsMessages, AmAirChannels, AmZones, FmChannels, AlarmSettings, DigitalContactWhitelist, AprsSettings, AprsReceiveFilters, OptionalSettings, DigitalContacts,
                digitalContactsGenuinelyPopulatedFromRadio: _digitalContactsGenuinelyPopulatedFromRadio);
            await SaveProjectDataOnBackgroundAsync(projectStorage, data);
            _currentProjectStorage = projectStorage;
            CurrentProjectLocation = projectStorage.DisplayLocation;
            await _storagePicker.RememberProjectAsync(projectStorage);
            MarkProjectClean();
            StatusMessage = $"Codeplug saved: {projectStorage.DisplayLocation}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Save failed: {exception.Message}";
        }
        finally
        {
            IsSavingProject = false;
        }
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        var suggestedName = string.IsNullOrWhiteSpace(CurrentProjectLocation)
            ? "SE_Field_Comms_D890UV_v1.dat"
            : Path.GetFileName(CurrentProjectLocation);
        var projectStorage = await _storagePicker.PickSaveProjectAsync(suggestedName);
        if (projectStorage is null)
        {
            StatusMessage = "Save cancelled";
            return;
        }

        if (!await _storagePicker.ConfirmOverwriteAsync(projectStorage))
        {
            StatusMessage = "Save cancelled";
            return;
        }

        NormalizeSimplexChannelOffsets();
        ReorderListsByNumber();
        IsSavingProject = true;
        try
        {
            var data = RadioProjectMapper.ToData(
                Channels, Zones, EncryptionKeys, Arc4EncryptionKeys, AesEncryptionKeys,
                RadioIds, Talkgroups, ScanLists, RoamingChannels, RoamingZones, ReceiveGroupLists, AutoRepeaterOffsets,
                MasterId, TalkAliasSettings, AnalogAddresses, GpsRoamingEntries, TalkgroupWhitelist, PrefabricatedSmsMessages, AmAirChannels, AmZones, FmChannels, AlarmSettings, DigitalContactWhitelist, AprsSettings, AprsReceiveFilters, OptionalSettings, DigitalContacts,
                digitalContactsGenuinelyPopulatedFromRadio: _digitalContactsGenuinelyPopulatedFromRadio);
            await SaveProjectDataOnBackgroundAsync(projectStorage, data);
            _currentProjectStorage = projectStorage;
            CurrentProjectLocation = projectStorage.DisplayLocation;
            await _storagePicker.RememberProjectAsync(projectStorage);
            MarkProjectClean();
            StatusMessage = $"Codeplug saved: {projectStorage.DisplayLocation}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Save failed: {exception.Message}";
        }
        finally
        {
            IsSavingProject = false;
        }
    }

    [RelayCommand]
    private void AddChannel()
    {
        var nextNumber = Channels.Count == 0 ? 1 : Channels.Max(channel => channel.Number) + 1;
        if (nextNumber > D890UvMemoryMap.MaxRegularChannelCount)
        {
            StatusMessage = $"Cannot add channel: the radio only has {D890UvMemoryMap.MaxRegularChannelCount} channel slots.";
            return;
        }

        var channel = new ChannelEntry
        {
            Number = nextNumber,
            Name = $"CHANNEL {nextNumber:000}",
            RxFrequencyMHz = 145.5,
            OffsetMHz = 0,
            OffsetDirection = 0,
            ChannelType = 0, // A-Analog
            TransmitPower = 2, // High
            Bandwidth = 0 // 12.5K
        };

        Channels.Add(channel);
        SelectedChannel = channel;
        RefreshValidationAndPreview("Channel added");
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedEncryptionKey))]
    private async Task RemoveEncryptionKey()
    {
        if (await ClearEncryptionKeyAsync(
            SelectedEncryptionKey,
            "0000",
            (key, value) => key.EncryptionId = value,
            "Digital",
            channel => channel.UsesDigitalEncryption ? channel.DigitalEncryptionText : "Off",
            channel => channel.DigitalEncryptionIndex = 0,
            "Digital Encryption"))
        {
            SelectedEncryptionKey = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedArc4EncryptionKey))]
    private async Task RemoveArc4EncryptionKey()
    {
        if (await ClearEncryptionKeyAsync(
            SelectedArc4EncryptionKey,
            "Off",
            (key, value) => key.EncryptionKey = value,
            "ARC4",
            channel => channel.UsesArc4Encryption ? channel.Arc4EncryptionText : "Off",
            channel => channel.Arc4EncryptionKeyIndex = 0,
            "ARC4"))
        {
            SelectedArc4EncryptionKey = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAesEncryptionKey))]
    private async Task RemoveAesEncryptionKey()
    {
        if (await ClearEncryptionKeyAsync(
            SelectedAesEncryptionKey,
            "Off",
            (key, value) => key.EncryptionId = value,
            "AES",
            channel => channel.UsesAesEncryption ? channel.AesDigitalEncryptionText : "Off",
            channel => channel.AesEncryptionIndex = 0,
            "AES Digital Encryption"))
        {
            SelectedAesEncryptionKey = null;
        }
    }

    // Fills exactly the first still-empty slot (by Number order) with a
    // random value and selects it - "Add" for a fixed-slot list means
    // "reveal the next one", not "append a new row" (see
    // EnsureEncryptionKeySlotsPresent's doc comment: Number is a real radio
    // slot address, not a display-order convenience, so every slot 1..N
    // already exists internally either way). Replaced the old "Generate N
    // at once" behavior 2026-08-15 to match every other list's single "+"
    // button, now that occupied-only filtering (VisibleEncryptionKeys etc.)
    // makes a fixed-slot list look and behave like a normal one.
    [RelayCommand]
    private void AddDigitalEncryptionKey()
    {
        var added = AddEncryptionKey(
            EncryptionKeys,
            "0000",
            key => key.EncryptionId,
            (key, value) => key.EncryptionId = value,
            _ => RandomNumberGenerator.GetInt32(0, 10000).ToString("0000", CultureInfo.InvariantCulture),
            "Digital");

        if (added is not null)
        {
            SelectedEncryptionKey = added;
        }
    }

    [RelayCommand]
    private void AddArc4EncryptionKey()
    {
        var added = AddEncryptionKey(
            Arc4EncryptionKeys,
            "Off",
            key => key.EncryptionKey,
            (key, value) => key.EncryptionKey = value,
            _ => GenerateHex(5)(0),
            "ARC4");

        if (added is not null)
        {
            SelectedArc4EncryptionKey = added;
        }
    }

    [RelayCommand]
    private void AddAesEncryptionKey()
    {
        var added = AddEncryptionKey(
            AesEncryptionKeys,
            "Off",
            key => key.EncryptionId,
            (key, value) => key.EncryptionId = value,
            _ => GenerateHex(32)(0),
            "AES");

        if (added is not null)
        {
            SelectedAesEncryptionKey = added;
        }
    }

    // Replaces the currently selected key's value with a fresh random one,
    // in place - for rotating an already-assigned key without clearing the
    // slot (which would also drop every channel's reference to it, unlike
    // this). Reuses the exact same random-value generators Add already
    // uses for a freshly revealed slot, and only touches the field that's
    // actually confirmed to have a real radio address for each key type
    // (Digital/AES: EncryptionId; ARC4: EncryptionKey - see
    // KeysDetailView's own field layout for which column is which).
    [RelayCommand(CanExecute = nameof(CanRemoveSelectedEncryptionKey))]
    private void RegenerateDigitalEncryptionKey()
    {
        if (SelectedEncryptionKey is { } key)
        {
            key.EncryptionId = RandomNumberGenerator.GetInt32(0, 10000).ToString("0000", CultureInfo.InvariantCulture);
            RefreshValidationAndPreview($"Digital key {key.Number} randomized");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedArc4EncryptionKey))]
    private void RegenerateArc4EncryptionKey()
    {
        if (SelectedArc4EncryptionKey is { } key)
        {
            key.EncryptionKey = GenerateHex(5)(0);
            RefreshValidationAndPreview($"ARC4 key {key.Number} randomized");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAesEncryptionKey))]
    private void RegenerateAesEncryptionKey()
    {
        if (SelectedAesEncryptionKey is { } key)
        {
            key.EncryptionId = GenerateHex(32)(0);
            RefreshValidationAndPreview($"AES key {key.Number} randomized");
        }
    }

    // Bulk-aware: with 2+ channels selected (Desktop Ctrl/Shift-click, Mobile
    // long-press then tap-to-toggle - see SelectedChannels's doc comment)
    // duplicates every selected channel; otherwise falls back to the single
    // SelectedChannel, unchanged from before multi-select existed.
    [RelayCommand(CanExecute = nameof(CanUseSelectedChannel))]
    private void DuplicateChannel()
    {
        List<ChannelEntry> sources;
        if (SelectedChannels.Count > 1)
        {
            sources = SelectedChannels.ToList();
        }
        else if (SelectedChannel is { } single)
        {
            sources = [single];
        }
        else
        {
            sources = [];
        }

        if (sources.Count == 0)
        {
            return;
        }

        var duplicates = new List<ChannelEntry>();
        foreach (var source in sources)
        {
            var nextNumber = Channels.Count == 0 ? 1 : Channels.Max(channel => channel.Number) + 1;
            if (nextNumber > D890UvMemoryMap.MaxRegularChannelCount)
            {
                StatusMessage = $"Stopped duplicating: the radio only has {D890UvMemoryMap.MaxRegularChannelCount} channel slots.";
                break;
            }

            var channel = source.Clone();
            channel.Number = nextNumber;
            channel.Name = $"{source.Name} COPY";

            Channels.Add(channel);
            duplicates.Add(channel);

            // Scan List membership lives on the ScanListEntry side (see
            // SelectedChannelScanListName's doc comment) - duplicate it here too
            // so a duplicated channel keeps the same scan list(s) as its source.
            foreach (var scanList in ScanLists.Where(s => s.Members.Contains(source)))
            {
                scanList.Members.Add(channel);
            }
        }

        if (duplicates.Count == 0)
        {
            return;
        }

        // Setting SelectedChannel alone is enough - both platforms' Channels
        // ListBox reacts to that (clearing any prior multi-selection down to
        // just this one item) and its own SelectionChanged handler updates
        // SelectedChannels to match, same as every other single-item
        // selection change already did before bulk selection existed.
        SelectedChannel = duplicates[^1];
        RefreshValidationAndPreview(duplicates.Count == 1 ? "Channel duplicated" : $"{duplicates.Count} channels duplicated");
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    // Bulk-aware: with 2+ channels selected (Desktop Ctrl/Shift-click, Mobile
    // long-press then tap-to-toggle - see SelectedChannels's doc comment)
    // removes every selected channel; otherwise falls back to the single
    // SelectedChannel, unchanged from before multi-select existed.
    [RelayCommand(CanExecute = nameof(CanUseSelectedChannel))]
    private void RemoveChannel()
    {
        List<ChannelEntry> targets;
        if (SelectedChannels.Count > 1)
        {
            targets = SelectedChannels.ToList();
        }
        else if (SelectedChannel is { } single)
        {
            targets = [single];
        }
        else
        {
            targets = [];
        }

        if (targets.Count == 0)
        {
            return;
        }

        // A zone/scan list can only end up "emptied" once no matter how many
        // of its members are removed in this pass - a HashSet (reference
        // equality, ZoneEntry has none of its own) keeps a zone that already
        // hit zero members from being queued for removal a second time as a
        // later target's cleanup pass finds it still at zero.
        var emptiedZones = new HashSet<ZoneEntry>();
        foreach (var removed in targets)
        {
            RemoveChannelCleanupOnly(removed, emptiedZones);
        }

        foreach (var zone in emptiedZones)
        {
            RemoveZoneInternal(zone);
        }

        SelectedChannel = Channels.FirstOrDefault();
        AvailableZoneChannel = Channels.FirstOrDefault();
        RefreshValidationAndPreview(targets.Count == 1
            ? emptiedZones.Count == 0
                ? "Channel removed"
                : $"Channel removed - {emptiedZones.Count} zone(s) had no channels left, so they were removed too"
            : emptiedZones.Count == 0
                ? $"{targets.Count} channels removed"
                : $"{targets.Count} channels removed - {emptiedZones.Count} zone(s) had no channels left, so they were removed too");
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The per-channel half of <see cref="RemoveChannel"/> - zone/scan
    /// list membership cleanup, the actual <see cref="Channels"/> removal, and
    /// queuing the radio-side delete. Split out so a bulk removal can run this
    /// once per selected channel and only reassign SelectedChannel/refresh/
    /// notify dirty once at the end, instead of per channel.</summary>
    private void RemoveChannelCleanupOnly(ChannelEntry removed, HashSet<ZoneEntry> emptiedZones)
    {
        // A zone that loses its last member is removed entirely, matching
        // confirmed real vendor CPS behavior (2026-07-19: a zone with zero
        // channels does not persist - it deletes itself).
        foreach (var zone in Zones)
        {
            while (zone.Members.Remove(removed))
            {
            }

            if (zone.Members.Count == 0)
            {
                emptiedZones.Add(zone);
            }
            else
            {
                ReassignZoneChannels(zone);
            }
        }

        // Scan List membership lives on the ScanListEntry side (see
        // SelectedChannelScanListName's doc comment) - a deleted channel
        // must not keep dangling membership (or a dangling Priority
        // Channel 1/2 reference) behind.
        var removedIndex = removed.Number - 1;
        foreach (var scanList in ScanLists)
        {
            scanList.Members.Remove(removed);

            if (ReferenceEquals(scanList.PriorityChannel1, removed))
            {
                scanList.PriorityChannel1 = null;
            }

            if (ReferenceEquals(scanList.PriorityChannel2, removed))
            {
                scanList.PriorityChannel2 = null;
            }
        }

        Channels.Remove(removed);
        // See _pendingDeleteRadioIndices's doc comment - without this, a
        // delete never actually reaches the radio (it just vanishes from
        // the in-memory list, then silently reappears on the next Read
        // From Radio).
        _pendingDeleteRadioIndices.Add(removedIndex);
    }

    [RelayCommand]
    private void AddZone()
    {
        if (Zones.Count >= CodeplugLimits.ZoneListMax)
        {
            StatusMessage = $"Cannot add zone: AnyTone lists support max {CodeplugLimits.ZoneListMax} zones.";
            return;
        }

        var nextNumber = Zones.Count == 0 ? 1 : Zones.Max(zone => zone.Number) + 1;
        var zone = new ZoneEntry
        {
            Number = nextNumber,
            Name = $"Zone {nextNumber:00}"
        };

        Zones.Add(zone);
        SelectedZone = zone;
        RefreshValidationAndPreview("Zone added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedZone))]
    private void RemoveZone()
    {
        if (SelectedZone is null)
        {
            return;
        }

        RemoveZoneInternal(SelectedZone);
        RefreshValidationAndPreview("Zone removed");
    }

    /// <summary>Shared by every path that can remove a zone - explicit
    /// (<see cref="RemoveZone"/>) or automatic (a zone losing its last
    /// member - see <see cref="ReassignZoneChannels"/>'s doc comment).
    /// Deliberately does NOT call <see cref="RefreshValidationAndPreview"/>
    /// itself, so callers that remove several zones in one pass (e.g.
    /// <see cref="RemoveChannel"/>) aren't forced into a per-zone status
    /// message.</summary>
    private void RemoveZoneInternal(ZoneEntry zone)
    {
        var removedIndex = zone.Number - 1;
        Zones.Remove(zone);
        // See _pendingDeleteZoneRadioIndices's doc comment - without this, a
        // delete never actually reaches the radio (it just vanishes from
        // the in-memory list, then silently reappears on the next Read
        // From Radio) - the exact same gap channel deletion had before it
        // was fixed.
        _pendingDeleteZoneRadioIndices.Add(removedIndex);
        if (ReferenceEquals(SelectedZone, zone))
        {
            SelectedZone = Zones.FirstOrDefault();
        }

        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Same purpose as <see cref="RemoveZoneInternal"/>, for AM
    /// Zone. Deliberately does NOT track a pending-radio-delete index (unlike
    /// RemoveZoneInternal) - AM Zone has no radio-write support yet.</summary>
    private void RemoveAmZoneInternal(AmZoneEntry amZone)
    {
        var removedIndex = amZone.Number - 1;
        AmZones.Remove(amZone);
        // See _pendingDeleteAmZoneRadioIndices's doc comment - without this,
        // a delete never actually reaches the radio (the same gap Channel/
        // Zone/Scan List/AM Air deletion had before each was fixed).
        _pendingDeleteAmZoneRadioIndices.Add(removedIndex);
        if (ReferenceEquals(SelectedAmZone, amZone))
        {
            SelectedAmZone = AmZones.FirstOrDefault();
        }

        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Assigns A/B-Channel purely as a side effect of membership, matching
    /// confirmed real vendor CPS behavior (2026-07-19, directly observed):
    /// the first channel ever added to a zone becomes A, the second becomes
    /// B; with only one member, B stays unset; A/B cannot be cleared
    /// directly by the user, only reassigned when the channel they point at
    /// stops being a member. Only touches a slot that's actually missing or
    /// dangling (still <c>??=</c>-like in spirit) - reordering existing
    /// members never retroactively changes an already-valid A/B.
    /// </summary>
    private static void ReassignZoneChannels(ZoneEntry zone)
    {
        if (zone.Members.Count == 0)
        {
            // Handled by the caller (RemoveZoneInternal) - a zone never
            // persists with zero members, so there's nothing to assign.
            return;
        }

        if (zone.AChannel is null || !zone.Members.Contains(zone.AChannel))
        {
            zone.AChannel = zone.Members[0];
        }

        if (zone.Members.Count == 1)
        {
            zone.BChannel = null;
        }
        else if (zone.BChannel is null || !zone.Members.Contains(zone.BChannel) || zone.BChannel == zone.AChannel)
        {
            zone.BChannel = zone.Members.FirstOrDefault(m => m != zone.AChannel);
        }
    }

    /// <summary>Same purpose as <see cref="ReassignZoneChannels"/>, for AM
    /// Zone's single A Channel (no B Channel equivalent).</summary>
    private static void ReassignAmZoneChannel(AmZoneEntry amZone)
    {
        if (amZone.Members.Count == 0)
        {
            // Handled by the caller (RemoveAmZoneInternal) - an AM Zone
            // never persists with zero members, so there's nothing to assign.
            return;
        }

        if (amZone.AChannel is null || !amZone.Members.Contains(amZone.AChannel))
        {
            amZone.AChannel = amZone.Members[0];
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddZoneMembers))]
    private void AddZoneMembers()
    {
        if (SelectedZone is null || SelectedAvailableZoneChannels.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (var channel in SelectedAvailableZoneChannels.ToList())
        {
            if (!SelectedZone.Members.Contains(channel))
            {
                SelectedZone.Members.Add(channel);
                added++;
            }
        }

        ReassignZoneChannels(SelectedZone);
        SetSelectedAvailableZoneChannels([]);
        RefreshAvailableZoneChannels();
        RefreshValidationAndPreview(added == 1 ? "Zone member added" : $"{added} zone members added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedZoneMembers))]
    private void RemoveZoneMembers()
    {
        if (SelectedZone is null || SelectedZoneMembers.Count == 0)
        {
            return;
        }

        var zone = SelectedZone;
        var removedChannels = SelectedZoneMembers.ToHashSet();
        foreach (var removed in removedChannels)
        {
            zone.Members.Remove(removed);
        }

        SetSelectedZoneMembers([]);

        if (zone.Members.Count == 0)
        {
            // See ReassignZoneChannels's doc comment - a zone with no
            // members left doesn't persist.
            RemoveZoneInternal(zone);
            RefreshAvailableZoneChannels();
            RefreshValidationAndPreview(removedChannels.Count == 1 ? "Zone member removed - zone had no channels left, so it was removed too" : $"{removedChannels.Count} zone members removed - zone had no channels left, so it was removed too");
            return;
        }

        ReassignZoneChannels(zone);
        RefreshAvailableZoneChannels();
        RefreshValidationAndPreview(removedChannels.Count == 1 ? "Zone member removed" : $"{removedChannels.Count} zone members removed");
    }

    [RelayCommand(CanExecute = nameof(CanAddRoamingZoneMembers))]
    private void AddRoamingZoneMembers()
    {
        if (SelectedRoamingZone is null || SelectedAvailableRoamingZoneChannels.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (var channel in SelectedAvailableRoamingZoneChannels.ToList())
        {
            if (SelectedRoamingZone.Members.Count >= CodeplugLimits.RoamingZoneMemberMax)
            {
                break;
            }

            if (!SelectedRoamingZone.Members.Contains(channel))
            {
                SelectedRoamingZone.Members.Add(channel);
                added++;
            }
        }

        SetSelectedAvailableRoamingZoneChannels([]);
        RefreshAvailableRoamingZoneChannels();
        RefreshValidationAndPreview(added == 1 ? "Roaming zone member added" : $"{added} roaming zone members added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedRoamingZoneMembers))]
    private void RemoveRoamingZoneMembers()
    {
        if (SelectedRoamingZone is null || SelectedRoamingZoneMembers.Count == 0)
        {
            return;
        }

        var removedChannels = SelectedRoamingZoneMembers.ToHashSet();
        foreach (var removed in removedChannels)
        {
            SelectedRoamingZone.Members.Remove(removed);
        }

        SetSelectedRoamingZoneMembers([]);
        RefreshAvailableRoamingZoneChannels();
        RefreshValidationAndPreview(removedChannels.Count == 1 ? "Roaming zone member removed" : $"{removedChannels.Count} roaming zone members removed");
    }

    [RelayCommand(CanExecute = nameof(CanAddAmZoneMembers))]
    private void AddAmZoneMembers()
    {
        if (SelectedAmZone is null || SelectedAvailableAmZoneChannels.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (var channel in SelectedAvailableAmZoneChannels.ToList())
        {
            if (!SelectedAmZone.Members.Contains(channel))
            {
                SelectedAmZone.Members.Add(channel);
                added++;
            }
        }

        ReassignAmZoneChannel(SelectedAmZone);
        SetSelectedAvailableAmZoneChannels([]);
        RefreshAvailableAmZoneChannels();
        RefreshValidationAndPreview(added == 1 ? "AM Zone member added" : $"{added} AM Zone members added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedAmZoneMembers))]
    private void RemoveAmZoneMembers()
    {
        if (SelectedAmZone is null || SelectedAmZoneMembers.Count == 0)
        {
            return;
        }

        var amZone = SelectedAmZone;
        var removedChannels = SelectedAmZoneMembers.ToHashSet();
        foreach (var removed in removedChannels)
        {
            amZone.Members.Remove(removed);
        }

        SetSelectedAmZoneMembers([]);

        if (amZone.Members.Count == 0)
        {
            // See ReassignAmZoneChannel's doc comment - an AM Zone with no
            // members left doesn't persist.
            RemoveAmZoneInternal(amZone);
            RefreshAvailableAmZoneChannels();
            RefreshValidationAndPreview(removedChannels.Count == 1 ? "AM Zone member removed - zone had no channels left, so it was removed too" : $"{removedChannels.Count} AM Zone members removed - zone had no channels left, so it was removed too");
            return;
        }

        ReassignAmZoneChannel(amZone);
        RefreshAvailableAmZoneChannels();
        RefreshValidationAndPreview(removedChannels.Count == 1 ? "AM Zone member removed" : $"{removedChannels.Count} AM Zone members removed");
    }

    [RelayCommand(CanExecute = nameof(CanAddAmZoneScanChannelMembers))]
    private void AddAmZoneScanChannelMembers()
    {
        if (SelectedAmZone is null || SelectedAvailableAmZoneScanChannels.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (var channel in SelectedAvailableAmZoneScanChannels.ToList())
        {
            if (!SelectedAmZone.ScanChannelMembers.Contains(channel))
            {
                SelectedAmZone.ScanChannelMembers.Add(channel);
                added++;
            }
        }

        SetSelectedAvailableAmZoneScanChannels([]);
        RefreshAvailableAmZoneScanChannels();
        RefreshValidationAndPreview(added == 1 ? "AM Zone scan channel member added" : $"{added} AM Zone scan channel members added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedAmZoneScanChannelMembers))]
    private void RemoveAmZoneScanChannelMembers()
    {
        if (SelectedAmZone is null || SelectedAmZoneScanChannelMembers.Count == 0)
        {
            return;
        }

        var amZone = SelectedAmZone;
        var removedChannels = SelectedAmZoneScanChannelMembers.ToHashSet();
        foreach (var removed in removedChannels)
        {
            amZone.ScanChannelMembers.Remove(removed);
        }

        SetSelectedAmZoneScanChannelMembers([]);
        // Unlike the regular Members list, an empty ScanChannelMembers list
        // is a completely normal state (it's the default for every zone
        // until the user opts in) - no auto-delete here.
        RefreshAvailableAmZoneScanChannels();
        RefreshValidationAndPreview(removedChannels.Count == 1 ? "AM Zone scan channel member removed" : $"{removedChannels.Count} AM Zone scan channel members removed");
    }

    [RelayCommand(CanExecute = nameof(CanAddScanListMembers))]
    private void AddScanListMembers()
    {
        if (SelectedScanList is null || SelectedAvailableScanListChannels.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (var channel in SelectedAvailableScanListChannels.ToList())
        {
            if (!SelectedScanList.Members.Contains(channel))
            {
                SelectedScanList.Members.Add(channel);
                added++;
            }
        }

        SetSelectedAvailableScanListChannels([]);
        RefreshAvailableScanListChannels();
        RefreshValidationAndPreview(added == 1 ? "Scan list member added" : $"{added} scan list members added");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedScanListMembers))]
    private void RemoveScanListMembers()
    {
        if (SelectedScanList is null || SelectedScanListMemberChannels.Count == 0)
        {
            return;
        }

        var scanList = SelectedScanList;
        var removedChannels = SelectedScanListMemberChannels.ToHashSet();
        foreach (var removed in removedChannels)
        {
            scanList.Members.Remove(removed);

            if (scanList.PriorityChannel1 == removed)
            {
                scanList.PriorityChannel1 = null;
            }

            if (scanList.PriorityChannel2 == removed)
            {
                scanList.PriorityChannel2 = null;
            }
        }

        SetSelectedScanListMemberChannels([]);
        RefreshAvailableScanListChannels();
        RefreshValidationAndPreview(removedChannels.Count == 1 ? "Scan list member removed" : $"{removedChannels.Count} scan list members removed");
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedZoneMember))]
    private void MoveZoneMemberUp()
    {
        MoveZoneMember(-1);
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedZoneMember))]
    private void MoveZoneMemberDown()
    {
        MoveZoneMember(1);
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedRoamingZoneMember))]
    private void MoveRoamingZoneMemberUp()
    {
        MoveRoamingZoneMember(-1);
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedRoamingZoneMember))]
    private void MoveRoamingZoneMemberDown()
    {
        MoveRoamingZoneMember(1);
    }

    [RelayCommand]
    private void RefreshExport()
    {
        RefreshValidationAndPreview("Preview refreshed");
    }

    [RelayCommand]
    private async Task ImportChannels()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "Channel import cancelled";
            return;
        }

        var files = await _storagePicker.PickCsvFilesAsync("Import channel CSV files");
        if (files.Count == 0)
        {
            StatusMessage = "Channel import cancelled";
            return;
        }

        try
        {
            var importedChannels = CpsCsvImporter.ReadChannels(files);
            ReplaceChannels(importedChannels);
            RebindZonesToChannels();
            SelectedChannel = Channels.FirstOrDefault();
            AvailableZoneChannel = Channels.FirstOrDefault();
            LastChannelImportLocation = GetDisplayLocation(files);
            RefreshValidationAndPreview($"Imported {importedChannels.Count} channels");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            StatusMessage = $"Channel import failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportChannelFolder()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "Channel import cancelled";
            return;
        }

        var folder = await _storagePicker.PickFolderAsync("Import channel CSV folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Channel import cancelled";
            return;
        }

        try
        {
            var importedChannels = CpsCsvImporter.ReadChannels(folder);
            ReplaceChannels(importedChannels);
            RebindZonesToChannels();
            SelectedChannel = Channels.FirstOrDefault();
            AvailableZoneChannel = Channels.FirstOrDefault();
            LastChannelImportLocation = folder;
            RefreshValidationAndPreview($"Imported {importedChannels.Count} channels");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            StatusMessage = $"Channel import failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportZones()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "Zone import cancelled";
            return;
        }

        var files = await _storagePicker.PickCsvFilesAsync("Import zone CSV files");
        if (files.Count == 0)
        {
            StatusMessage = "Zone import cancelled";
            return;
        }

        try
        {
            var result = CpsCsvImporter.ReadZones(files, Channels);
            ReplaceZones(result.Zones);
            SelectedZone = Zones.FirstOrDefault();
            LastZoneImportLocation = GetDisplayLocation(files);
            RefreshValidationAndPreview($"Imported {result.Zones.Count} zones");

            foreach (var warning in result.Warnings)
            {
                ValidationMessages.Add(warning);
            }

            OnPropertyChanged(nameof(ValidationSummary));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            StatusMessage = $"Zone import failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportZoneFolder()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync())
        {
            StatusMessage = "Zone import cancelled";
            return;
        }

        var folder = await _storagePicker.PickFolderAsync("Import zone CSV folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Zone import cancelled";
            return;
        }

        try
        {
            var result = CpsCsvImporter.ReadZones(folder, Channels);
            ReplaceZones(result.Zones);
            SelectedZone = Zones.FirstOrDefault();
            LastZoneImportLocation = folder;
            RefreshValidationAndPreview($"Imported {result.Zones.Count} zones");

            foreach (var warning in result.Warnings)
            {
                ValidationMessages.Add(warning);
            }

            OnPropertyChanged(nameof(ValidationSummary));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            StatusMessage = $"Zone import failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveExport()
    {
        RefreshValidationAndPreview("Preview refreshed");

        if (ValidationMessages.Any(message => !message.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Fix validation issues before export";
            return;
        }

        if (string.IsNullOrWhiteSpace(ExportDirectory))
        {
            var folder = await _storagePicker.PickFolderAsync("Export CSV files");
            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusMessage = "Export cancelled";
                return;
            }

            ExportDirectory = folder;
        }

        var files = CpsCsvExporter.WriteExports(ExportDirectory, Channels, Zones);
        StatusMessage = $"Exported {files.Count} files to {ExportDirectory}";
    }

    [RelayCommand]
    private async Task ChooseExportDirectory()
    {
        var folder = await _storagePicker.PickFolderAsync("Choose export folder");
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Export folder selection cancelled";
            return;
        }

        ExportDirectory = folder;
        await SaveAppSettingsAsync();
        StatusMessage = $"Export folder: {folder}";
    }

    partial void OnSelectedChannelChanged(ChannelEntry? value)
    {
        DuplicateChannelCommand.NotifyCanExecuteChanged();
        RemoveChannelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BusyLockTxPermitValues));
        OnPropertyChanged(nameof(BusyLockTxPermitHeaderText));
        OnPropertyChanged(nameof(SelectedChannelContactName));
        OnPropertyChanged(nameof(SelectedChannelRadioIdName));
        OnPropertyChanged(nameof(SelectedChannelScanListName));
        OnPropertyChanged(nameof(SelectedChannelReceiveGroupListName));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsChannelsViewSelected));
        OnPropertyChanged(nameof(IsZonesViewSelected));
        OnPropertyChanged(nameof(IsDigitalKeysViewSelected));
        OnPropertyChanged(nameof(IsArc4KeysViewSelected));
        OnPropertyChanged(nameof(IsAesKeysViewSelected));
        OnPropertyChanged(nameof(IsAnyKeysViewSelected));
        OnPropertyChanged(nameof(IsRadioIdListViewSelected));
        OnPropertyChanged(nameof(IsTalkgroupsViewSelected));
        OnPropertyChanged(nameof(IsScanListsViewSelected));
        OnPropertyChanged(nameof(IsRoamingChannelsViewSelected));
        OnPropertyChanged(nameof(IsRoamingZonesViewSelected));
        OnPropertyChanged(nameof(IsReceiveGroupListsViewSelected));
        OnPropertyChanged(nameof(IsAutoRepeaterOffsetsViewSelected));
        OnPropertyChanged(nameof(IsAnalogAddressBookViewSelected));
        OnPropertyChanged(nameof(IsGpsRoamingViewSelected));
        OnPropertyChanged(nameof(IsTalkgroupWhitelistViewSelected));
        OnPropertyChanged(nameof(IsDigitalContactWhitelistViewSelected));
        OnPropertyChanged(nameof(IsDigitalContactsViewSelected));
        OnPropertyChanged(nameof(IsPrefabricatedSmsViewSelected));
        OnPropertyChanged(nameof(IsAmAirBandViewSelected));
        OnPropertyChanged(nameof(IsAmZoneViewSelected));
        OnPropertyChanged(nameof(IsFmBroadcastViewSelected));
        OnPropertyChanged(nameof(IsMasterIdViewSelected));
        OnPropertyChanged(nameof(IsTalkAliasSettingsViewSelected));
        OnPropertyChanged(nameof(IsAlarmSettingsViewSelected));
        OnPropertyChanged(nameof(IsAprsSettingsViewSelected));
        OnPropertyChanged(nameof(IsAprsFiltersViewSelected));
        OnPropertyChanged(nameof(IsRadioViewSelected));
        OnPropertyChanged(nameof(IsImportsViewSelected));
        OnPropertyChanged(nameof(IsExportsViewSelected));
        OnPropertyChanged(nameof(IsSettingsViewSelected));
        OnPropertyChanged(nameof(IsAnalogQuickCallViewSelected));
        OnPropertyChanged(nameof(IsStateInformationViewSelected));
        OnPropertyChanged(nameof(IsHotKeyViewSelected));
        OnPropertyChanged(nameof(IsQdc1200DecodeViewSelected));
        OnPropertyChanged(nameof(IsQdc1200EncodeViewSelected));
        OnPropertyChanged(nameof(IsQdcAddressBookViewSelected));
        OnPropertyChanged(nameof(IsFiveToneViewSelected));
        OnPropertyChanged(nameof(IsTwoToneEncodeViewSelected));
        OnPropertyChanged(nameof(IsTwoToneDecodeViewSelected));
        OnPropertyChanged(nameof(IsDtmfViewSelected));
        OnPropertyChanged(nameof(IsDevOptionsViewSelected));
        OnPropertyChanged(nameof(IsAboutViewSelected));
    }

    partial void OnSelectedOptionalSettingsSubTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsOptionalSettingsRadioSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsPowerOnSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsAlertToneSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsPowerSaveSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsDisplaySubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsWorkModeSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsVoxBtSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsSteSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsAmFmSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsKeyFunctionSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsOtherSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsDigitalFuncSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsGpsRangingSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsVfoScanSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsAutoRepeaterSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsRecordSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsVolumeAudioSubTabSelected));
        OnPropertyChanged(nameof(IsOptionalSettingsSatelliteSubTabSelected));
    }

    partial void OnSelectedThemeModeChanged(string value)
    {
        ApplyThemeMode(value);
        _ = SaveAppSettingsAsync();
    }

    partial void OnSelectedZoneChanged(ZoneEntry? value)
    {
        SelectedZoneMember = value?.Members.FirstOrDefault();
        RemoveZoneCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableZoneChannels([]);
        SetSelectedZoneMembers([]);
        RefreshAvailableZoneChannels();
        OnPropertyChanged(nameof(SelectedZoneMemberOptions));
        AddZoneMembersCommand.NotifyCanExecuteChanged();
        RemoveZoneMembersCommand.NotifyCanExecuteChanged();
        MoveZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveZoneMemberDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnAvailableZoneChannelChanged(ChannelEntry? value)
    {
        AddZoneMembersCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedZoneMemberChanged(ChannelEntry? value)
    {
        RemoveZoneMembersCommand.NotifyCanExecuteChanged();
        MoveZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveZoneMemberDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEncryptionKeyChanged(EncryptionKeyEntry? value)
    {
        RemoveEncryptionKeyCommand.NotifyCanExecuteChanged();
        RegenerateDigitalEncryptionKeyCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedArc4EncryptionKeyChanged(EncryptionKeyEntry? value)
    {
        RemoveArc4EncryptionKeyCommand.NotifyCanExecuteChanged();
        RegenerateArc4EncryptionKeyCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAesEncryptionKeyChanged(EncryptionKeyEntry? value)
    {
        RemoveAesEncryptionKeyCommand.NotifyCanExecuteChanged();
        RegenerateAesEncryptionKeyCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentProjectLocationChanged(string value)
    {
        OnPropertyChanged(nameof(DataStoreDescription));
    }

    private bool CanUseSelectedChannel() => SelectedChannel is not null || SelectedChannels.Count > 0;
    private bool CanUseSelectedZone() => SelectedZone is not null;
    private bool CanSaveProject() => IsDirty && !HasBlockingValidationErrors;

    private static string GetDefaultExportDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "AnyToneCPS", "Exports");
    }

    private Task<IProjectStorage?> OpenRememberedProjectOnBackgroundAsync()
    {
        return Task.Run(async () => await _storagePicker.OpenRememberedProjectAsync());
    }

    private static Task<RadioProjectData?> LoadProjectDataOnBackgroundAsync(IProjectStorage projectStorage)
    {
        return Task.Run(async () => await projectStorage.LoadAsync());
    }

    private static Task SaveProjectDataOnBackgroundAsync(IProjectStorage projectStorage, RadioProjectData data)
    {
        return Task.Run(async () => await projectStorage.SaveAsync(data));
    }

    private async Task LoadAppSettingsAsync()
    {
        var settings = await AppSettingsStore.LoadAsync();
        if (!string.IsNullOrWhiteSpace(settings.ExportDirectory))
        {
            ExportDirectory = settings.ExportDirectory;
        }

        SelectedThemeMode = ThemeModes.Contains(settings.ThemeMode) ? settings.ThemeMode : "Dark";
        ApplyThemeMode(SelectedThemeMode);

        SuppressVoxStartupWarning = settings.SuppressVoxStartupWarning;
        if (!SuppressVoxStartupWarning)
        {
            ShowVoxStartupWarning = true;
            DispatcherTimer.RunOnce(() => ShowVoxStartupWarning = false, TimeSpan.FromSeconds(8));
        }
    }

    [RelayCommand]
    private void DismissVoxStartupWarning() => ShowVoxStartupWarning = false;

    partial void OnSuppressVoxStartupWarningChanged(bool value) => _ = SaveAppSettingsAsync();

    private Task SaveAppSettingsAsync()
    {
        return AppSettingsStore.SaveAsync(new AppSettingsData
        {
            SuppressVoxStartupWarning = SuppressVoxStartupWarning,
            ThemeMode = SelectedThemeMode,
            ExportDirectory = ExportDirectory
        });
    }

    private static void ApplyThemeMode(string themeMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = themeMode switch
        {
            "Light" => ThemeVariant.Light,
            "System" => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }

    private static string GetBuildDescription()
    {
        return $"Version {GetAppVersion()} - {GetBuildMode()}";
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(MainViewModel).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString(3) ?? "unknown";
        }

        return version;
    }

    private static string GetBuildMode()
    {
        // Was Assembly.GetEntryAssembly() ?? typeof(MainViewModel).Assembly -
        // correct on Desktop (GetEntryAssembly() returns AnyToneCPS.Desktop.dll,
        // which carries this attribute per its own csproj) but wrong on
        // Android: there's no managed "Main" entry point the runtime
        // recognizes there, so GetEntryAssembly() returns null and this fell
        // back to the SHARED AnyToneCPS.dll - which never carries the
        // attribute, since it's only set per-head (AnyToneCPS.Android.csproj/
        // AnyToneCPS.Desktop.csproj), not the shared project. Found live
        // 2026-07-28: a real NativeAOT Android build still showed "Inte
        // NativeAOT". Searching every loaded assembly finds it regardless of
        // which one the runtime considers "entry" on a given platform.
        var buildMode = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            .FirstOrDefault(attribute => attribute.Key == "AnyToneCPS.BuildMode")
            ?.Value;

        if (string.IsNullOrWhiteSpace(buildMode))
        {
            buildMode = RuntimeFeature.IsDynamicCodeSupported
                ? "runtime"
                : "AOT runtime";
        }

        return buildMode;
    }

    private bool CanAddZoneMembers() => SelectedZone is not null && SelectedAvailableZoneChannels.Count > 0;
    private bool CanAddScanListMembers() => SelectedScanList is not null && SelectedAvailableScanListChannels.Count > 0;
    private bool CanUseSelectedScanListMembers() => SelectedScanList is not null && SelectedScanListMemberChannels.Count > 0;
    private bool CanUseSelectedZoneMember() => SelectedZone is not null && SelectedZoneMember is not null;
    private bool CanUseSelectedZoneMembers() => SelectedZone is not null && SelectedZoneMembers.Count > 0;
    private bool CanAddRoamingZoneMembers() => SelectedRoamingZone is not null && SelectedAvailableRoamingZoneChannels.Count > 0;
    private bool CanUseSelectedRoamingZoneMember() => SelectedRoamingZone is not null && SelectedRoamingZoneMember is not null;
    private bool CanUseSelectedRoamingZoneMembers() => SelectedRoamingZone is not null && SelectedRoamingZoneMembers.Count > 0;

    private bool CanAddAmZoneMembers() => SelectedAmZone is not null && SelectedAvailableAmZoneChannels.Count > 0;
    private bool CanUseSelectedAmZoneMembers() => SelectedAmZone is not null && SelectedAmZoneMembers.Count > 0;
    private bool CanAddAmZoneScanChannelMembers() => SelectedAmZone is not null && SelectedAvailableAmZoneScanChannels.Count > 0;
    private bool CanUseSelectedAmZoneScanChannelMembers() => SelectedAmZone is not null && SelectedAmZoneScanChannelMembers.Count > 0;
    private bool CanRemoveSelectedEncryptionKey() => SelectedEncryptionKey is not null;
    private bool CanRemoveSelectedArc4EncryptionKey() => SelectedArc4EncryptionKey is not null;
    private bool CanRemoveSelectedAesEncryptionKey() => SelectedAesEncryptionKey is not null;

    private void MoveZoneMember(int offset)
    {
        if (SelectedZone is null || SelectedZoneMember is null)
        {
            return;
        }

        var currentIndex = SelectedZone.Members.IndexOf(SelectedZoneMember);
        var nextIndex = currentIndex + offset;

        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= SelectedZone.Members.Count)
        {
            return;
        }

        SelectedZone.Members.Move(currentIndex, nextIndex);
        RefreshValidationAndPreview("Zone order changed");
    }

    private void MoveRoamingZoneMember(int offset)
    {
        if (SelectedRoamingZone is null || SelectedRoamingZoneMember is null)
        {
            return;
        }

        var currentIndex = SelectedRoamingZone.Members.IndexOf(SelectedRoamingZoneMember);
        var nextIndex = currentIndex + offset;

        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= SelectedRoamingZone.Members.Count)
        {
            return;
        }

        SelectedRoamingZone.Members.Move(currentIndex, nextIndex);
        RefreshValidationAndPreview("Roaming zone order changed");
    }

    // 2026-07-18: reworked to match the real vendor CPS, which always shows
    // every slot (1-32/34/255) rather than a variable-length list you add
    // to - see SeedEncryptionKeySlots. "Remove" now means "clear this
    // slot's real value back to its default" (the row itself always
    // exists), and "Generate" now means "randomize the first N slots that
    // are still at their default", not "create N new rows".
    /// <summary>Returns true if the slot was actually cleared - false if
    /// there was nothing selected, or the user declined to clear it after
    /// being warned it's used by channel(s). Callers use this to decide
    /// whether to also drop the selection (see the 3 RemoveXEncryptionKey
    /// commands above) - declining leaves the row selected so the user's
    /// still looking at what they said no to.</summary>
    private async Task<bool> ClearEncryptionKeyAsync(
        EncryptionKeyEntry? selectedKey,
        string offValue,
        Action<EncryptionKeyEntry, string> setRealValue,
        string encryptionMode,
        Func<ChannelEntry, string> channelKeySelector,
        Action<ChannelEntry> clearChannelEncryption,
        string field)
    {
        if (selectedKey is null)
        {
            return false;
        }

        var keyNumber = selectedKey.Number.ToString(CultureInfo.InvariantCulture);
        var usedBy = Channels
            .Where(channel => channel.EncryptionMode.Equals(encryptionMode, StringComparison.OrdinalIgnoreCase)
                && channelKeySelector(channel).Equals(keyNumber, StringComparison.OrdinalIgnoreCase))
            .OrderBy(channel => channel.Number)
            .ToList();

        if (usedBy.Count > 0)
        {
            var channelList = string.Join(Environment.NewLine, usedBy.Select(channel => $"- {channel.DisplayLabel}"));
            var choice = await _storagePicker.ConfirmRemoveUsedEncryptionKeyAsync(
                $"{field} key {keyNumber} is used by:{Environment.NewLine}{channelList}{Environment.NewLine}{Environment.NewLine}Clear the key from those channels and disable encryption?");

            if (choice != UsedEncryptionKeyRemovalChoice.RemoveReferences)
            {
                StatusMessage = $"{field} key {keyNumber} is used by {usedBy.Count} channel(s)";
                return false;
            }

            foreach (var channel in usedBy)
            {
                clearChannelEncryption(channel);
            }
        }

        setRealValue(selectedKey, offValue);
        RefreshValidationAndPreview($"{field} key {keyNumber} cleared");
        return true;
    }

    /// <summary>Fills the first still-empty slot (by Number order) with a
    /// random value - see the 3 AddXEncryptionKey commands' own doc comment
    /// for why "Add" means this instead of appending a new row. Returns the
    /// filled entry so the caller can select it, or null if every slot is
    /// already occupied.</summary>
    private EncryptionKeyEntry? AddEncryptionKey(
        ObservableCollection<EncryptionKeyEntry> keys,
        string offValue,
        Func<EncryptionKeyEntry, string> getRealValue,
        Action<EncryptionKeyEntry, string> setRealValue,
        Func<int, string> randomValueFactory,
        string field)
    {
        var candidate = keys
            .Where(key => getRealValue(key) == offValue)
            .OrderBy(key => key.Number)
            .FirstOrDefault();

        if (candidate is null)
        {
            StatusMessage = $"No empty {field} slots left";
            return null;
        }

        setRealValue(candidate, randomValueFactory(candidate.Number));
        RefreshValidationAndPreview($"{field} key {candidate.Number} added");
        return candidate;
    }

    private static Func<int, string> GenerateHex(int byteCount)
    {
        return _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount));
    }

    private void ReplaceChannels(IEnumerable<ChannelEntry> channels)
    {
        DetachChannelHandlers(Channels);
        Channels.Clear();

        foreach (var channel in channels)
        {
            Channels.Add(channel);
        }
    }

    private void ReplaceZones(IEnumerable<ZoneEntry> zones)
    {
        DetachZoneHandlers(Zones);
        Zones.Clear();

        foreach (var zone in zones)
        {
            Zones.Add(zone);
        }
    }

    private void RebindZonesToChannels()
    {
        var channelLookup = Channels
            .GroupBy(channel => channel.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var zone in Zones)
        {
            var memberNames = zone.Members.Select(channel => channel.Name).ToList();
            zone.Members.Clear();

            foreach (var memberName in memberNames)
            {
                if (channelLookup.TryGetValue(memberName, out var channel))
                {
                    zone.Members.Add(channel);
                }
            }

            zone.AChannel = zone.AChannel is { } aChannel && channelLookup.TryGetValue(aChannel.Name, out var reboundAChannel)
                ? reboundAChannel
                : zone.Members.FirstOrDefault();

            zone.BChannel = zone.BChannel is { } bChannel && channelLookup.TryGetValue(bChannel.Name, out var reboundBChannel)
                ? reboundBChannel
                : zone.Members.Skip(1).FirstOrDefault() ?? zone.Members.FirstOrDefault();
        }
    }

    private void RefreshAvailableZoneChannels()
    {
        AvailableZoneChannels.Clear();

        if (SelectedZone is null)
        {
            return;
        }

        foreach (var channel in Channels.Where(channel => !SelectedZone.Members.Contains(channel)))
        {
            AvailableZoneChannels.Add(channel);
        }
    }

    private void RefreshAvailableRoamingZoneChannels()
    {
        AvailableRoamingZoneChannels.Clear();

        if (SelectedRoamingZone is null)
        {
            return;
        }

        foreach (var channel in RoamingChannels.Where(channel => !SelectedRoamingZone.Members.Contains(channel)))
        {
            AvailableRoamingZoneChannels.Add(channel);
        }
    }

    private void RefreshAvailableAmZoneChannels()
    {
        AvailableAmZoneChannels.Clear();

        if (SelectedAmZone is null)
        {
            return;
        }

        foreach (var channel in AmAirChannels.Where(channel => !SelectedAmZone.Members.Contains(channel)))
        {
            AvailableAmZoneChannels.Add(channel);
        }
    }

    /// <summary>Restricted to AM Air Number 1-128 (radio index 0-127) - the
    /// scan-channel bitmask physically cannot reference anything higher, see
    /// AmZoneCodec.ScanChannelBitCount's doc comment.</summary>
    private void RefreshAvailableAmZoneScanChannels()
    {
        AvailableAmZoneScanChannels.Clear();

        if (SelectedAmZone is null)
        {
            return;
        }

        foreach (var channel in AmAirChannels.Where(channel => channel.Number <= AmZoneCodec.ScanChannelBitCount && !SelectedAmZone.ScanChannelMembers.Contains(channel)))
        {
            AvailableAmZoneScanChannels.Add(channel);
        }
    }

    private void RefreshAvailableScanListChannels()
    {
        AvailableScanListChannels.Clear();

        if (SelectedScanList is null)
        {
            return;
        }

        // Unlike Zone, a channel CAN belong to multiple scan lists at once
        // (confirmed 2026-07-19 via the reference project's own
        // available_channels filter, which only excludes THIS list's own
        // members) - so only this scan list's Members are excluded, not
        // every other scan list's members too.
        foreach (var channel in Channels.Where(channel => !SelectedScanList.Members.Contains(channel)))
        {
            AvailableScanListChannels.Add(channel);
        }
    }

    private static void ReplaceSelection<T>(ObservableCollection<T> target, IEnumerable<T> channels)
    {
        target.Clear();
        foreach (var channel in channels)
        {
            target.Add(channel);
        }
    }

    private void SeedData()
    {
        var hallandsas = new ChannelEntry
        {
            Number = 1,
            Name = "HALLANDSAS VHF",
            RxFrequencyMHz = 145.78750,
            OffsetMHz = 0.6,
            OffsetDirection = 2,
            ChannelType = 0,
            TransmitPower = 3,
            Bandwidth = 0,
            CtcssDcsDecode = 0,
            CtcssDcsEncode = 1
        };
        var vhfSimplex = new ChannelEntry
        {
            Number = 100,
            Name = "V00",
            RxFrequencyMHz = 145.50000,
            OffsetMHz = 0,
            OffsetDirection = 0,
            ChannelType = 0,
            TransmitPower = 3,
            Bandwidth = 1
        };
        var uhfSimplex = new ChannelEntry
        {
            Number = 116,
            Name = "U00",
            RxFrequencyMHz = 433.50000,
            OffsetMHz = 0,
            OffsetDirection = 0,
            ChannelType = 0,
            TransmitPower = 3,
            Bandwidth = 1
        };
        var dmr = new ChannelEntry
        {
            Number = 400,
            Name = "DMRV1",
            RxFrequencyMHz = 145.37500,
            OffsetMHz = 0,
            OffsetDirection = 0,
            ChannelType = 1,
            TransmitPower = 3,
            Bandwidth = 0,
            ColorCode = 1,
            RepeaterSlot2 = false,
            ContactIndex = 99
        };

        Channels.Add(hallandsas);
        Channels.Add(vhfSimplex);
        Channels.Add(uhfSimplex);
        Channels.Add(dmr);

        var analogZone = new ZoneEntry
        {
            Number = 1,
            Name = "Analog"
        };
        analogZone.Members.Add(hallandsas);
        analogZone.Members.Add(vhfSimplex);
        analogZone.Members.Add(uhfSimplex);
        analogZone.AChannel = hallandsas;
        analogZone.BChannel = vhfSimplex;
        Zones.Add(analogZone);

        var dmrZone = new ZoneEntry
        {
            Number = 2,
            Name = "DMR Simplex"
        };
        dmrZone.Members.Add(dmr);
        dmrZone.AChannel = dmr;
        dmrZone.BChannel = dmr;
        Zones.Add(dmrZone);

        EnsureEncryptionKeySlotsPresent();
        EnsureGpsRoamingSlotsPresent();
        EnsureHotKeySlotsPresent();
        EnsureDtmfEncodeSlotsPresent();
    }

    // 2026-07-18: matches the real vendor CPS, which always shows every
    // slot (1-32 Basic/Digital, 1-34 ARC4, 1-255 AES) rather than a
    // variable-length list you add entries to - see RadioReadMapper's
    // Map*EncryptionKeys/MapBasicEncryptionCodes for the read-from-radio
    // side of the same convention, and its doc comment for why the
    // "companion" field (the one no radio address was found for) is a
    // computed value tied to the slot number rather than independent data.
    //
    // Idempotent and safe to call on a non-empty collection (e.g. after
    // loading a saved project file from before this convention existed,
    // which may have fewer than the full range, or a sparse subset) -
    // existing entries are kept as-is, only genuinely missing slot numbers
    // are backfilled with the default, and the result is always re-sorted
    // 1..count.
    private void EnsureEncryptionKeySlotsPresent()
    {
        // OnEncryptionKeysChanged runs a full RefreshValidationAndPreview()
        // per Clear/Add - detach while bulk-filling up to 321 entries
        // (32+34+255) and refresh once at the end instead.
        EncryptionKeys.CollectionChanged -= OnEncryptionKeysChanged;
        Arc4EncryptionKeys.CollectionChanged -= OnEncryptionKeysChanged;
        AesEncryptionKeys.CollectionChanged -= OnEncryptionKeysChanged;

        FillMissingSlots(EncryptionKeys, CodeplugLimits.BasicEncryptionCodeCount, number => new EncryptionKeyEntry
        {
            Kind = EncryptionKeyKind.Basic,
            Number = number,
            EncryptionIdText = "0000",
            EncryptionKeyText = $"{number:00}{number:00}"
        });

        FillMissingSlots(Arc4EncryptionKeys, CodeplugLimits.Arc4EncryptionKeyCount, number => new EncryptionKeyEntry
        {
            Kind = EncryptionKeyKind.Arc4,
            Number = number,
            EncryptionKeyText = "Off",
            EncryptionIdText = number.ToString(CultureInfo.InvariantCulture)
        });

        FillMissingSlots(AesEncryptionKeys, CodeplugLimits.AesEncryptionKeyCount, number => new EncryptionKeyEntry
        {
            Kind = EncryptionKeyKind.Aes,
            Number = number,
            EncryptionIdText = "Off",
            EncryptionKeyText = number.ToString(CultureInfo.InvariantCulture)
        });

        EncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;
        Arc4EncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;
        AesEncryptionKeys.CollectionChanged += OnEncryptionKeysChanged;

        // A Reset-action event (below) carries no NewItems, so the bulk-filled
        // entries need their per-entry PropertyChanged handler attached
        // explicitly here - otherwise editing one later would never
        // re-validate or mark the project dirty (found 2026-07-19). Detach
        // first since this method is idempotent/re-callable (after Load,
        // after Read, ...) and FillMissingSlots reuses existing entry
        // instances - without detaching first, a reused entry would pick up
        // a second (or third...) duplicate subscription each time this runs.
        DetachEncryptionKeyHandlers(EncryptionKeys);
        DetachEncryptionKeyHandlers(Arc4EncryptionKeys);
        DetachEncryptionKeyHandlers(AesEncryptionKeys);
        AttachEncryptionKeyHandlers(EncryptionKeys);
        AttachEncryptionKeyHandlers(Arc4EncryptionKeys);
        AttachEncryptionKeyHandlers(AesEncryptionKeys);
        OnEncryptionKeysChanged(EncryptionKeys, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        // Rebuild the Visible* lists from scratch rather than diffing -
        // this method is idempotent/re-callable (Load, Read, New Project),
        // and a full recompute is cheap (at most 321 slots) and can't drift
        // the way an incremental patch could.
        VisibleEncryptionKeys.Clear();
        VisibleArc4EncryptionKeys.Clear();
        VisibleAesEncryptionKeys.Clear();
        SyncAllEncryptionKeyVisibility(EncryptionKeys);
        SyncAllEncryptionKeyVisibility(Arc4EncryptionKeys);
        SyncAllEncryptionKeyVisibility(AesEncryptionKeys);
    }

    private static void FillMissingSlots(ObservableCollection<EncryptionKeyEntry> keys, int count, Func<int, EncryptionKeyEntry> createDefault)
    {
        var byNumber = keys.ToDictionary(key => key.Number);
        var complete = Enumerable.Range(1, count)
            .Select(number =>
            {
                if (byNumber.TryGetValue(number, out var existing))
                {
                    return existing;
                }

                // A backfilled placeholder is a UI stand-in, not something
                // meant to be written to the radio - mark it synced immediately so
                // it never shows as pending-write on its own (see
                // EncryptionKeyEntry's class doc comment).
                var created = createDefault(number);
                created.MarkRadioSynced();
                return created;
            })
            .ToList();

        keys.Clear();
        foreach (var entry in complete)
        {
            keys.Add(entry);
        }
    }

    private void OnChannelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachChannelHandlers(e.NewItems?.OfType<ChannelEntry>());
        DetachChannelHandlers(e.OldItems?.OfType<ChannelEntry>());
        _projectStructureDirty = true;
        OnPropertyChanged(nameof(ChannelCount));
        RefreshAvailableZoneChannels();
        RefreshAvailableScanListChannels();
        OnPropertyChanged(nameof(AlarmSettingsAnalogEmergencyChannelOptions));
        OnPropertyChanged(nameof(AlarmSettingsAnalogEmergencyChannelSelection));
        OnPropertyChanged(nameof(AlarmSettingsDigitalEmergencyChannelOptions));
        OnPropertyChanged(nameof(AlarmSettingsDigitalEmergencyChannelSelection));
        OnPropertyChanged(nameof(RoamingChannelFastSelectOptions));
        NotifyDirtyStateChanged();
        RefreshValidationAndPreview();
    }

    private void OnZonesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachZoneHandlers(e.NewItems?.OfType<ZoneEntry>());
        DetachZoneHandlers(e.OldItems?.OfType<ZoneEntry>());
        _projectStructureDirty = true;
        OnPropertyChanged(nameof(ZoneCount));
        OnPropertyChanged(nameof(OptionalSettingsZoneOptions));
        OnPropertyChanged(nameof(OptionalSettingsRoamingZoneOptions));
        NotifyOptionalSettingsStartupNamesChanged();
        NotifyDirtyStateChanged();
        RefreshValidationAndPreview();
    }

    /// <summary>Refreshes every OptionalSettings Startup Zone/Channel A/B
    /// AND Work Mode Mem Zone A/B picker NAME/cascading-options property -
    /// needed whenever the OptionalSettings byte fields they resolve
    /// against change (OnOptionalSettingsPropertyChanged). Deliberately
    /// does NOT touch OptionalSettingsZoneOptions/OptionalSettingsRoamingZoneOptions
    /// (the master picker ItemsSource lists) - those only change when Zones/
    /// RoamingZones itself changes (see OnZonesChanged). Confirmed live
    /// 2026-08-24: including them here made every Startup Zone A/B ComboBox
    /// selection snap back to zone 1 - selecting a zone changed StartupZoneA,
    /// which re-raised OptionalSettingsZoneOptions, which handed the
    /// ComboBox a brand-new (if content-identical) list instance for its
    /// ItemsSource in the middle of processing that same selection, and the
    /// ComboBox reset its selection to index 0 rather than preserve it.</summary>
    private void NotifyOptionalSettingsStartupNamesChanged()
    {
        OnPropertyChanged(nameof(OptionalSettingsStartupZoneAName));
        OnPropertyChanged(nameof(OptionalSettingsStartupZoneBName));
        OnPropertyChanged(nameof(OptionalSettingsStartupChannelAOptions));
        OnPropertyChanged(nameof(OptionalSettingsStartupChannelAName));
        OnPropertyChanged(nameof(OptionalSettingsStartupChannelBOptions));
        OnPropertyChanged(nameof(OptionalSettingsStartupChannelBName));
        OnPropertyChanged(nameof(OptionalSettingsMemZoneAName));
        OnPropertyChanged(nameof(OptionalSettingsMemZoneBName));
        OnPropertyChanged(nameof(OptionalSettingsPriorityZoneAName));
        OnPropertyChanged(nameof(OptionalSettingsPriorityZoneBName));
        OnPropertyChanged(nameof(OptionalSettingsRoamingZoneName));
    }

    private void OnOptionalSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OptionalSettingsEntry.StartupZoneA) or nameof(OptionalSettingsEntry.StartupZoneB)
            or nameof(OptionalSettingsEntry.MemZoneA) or nameof(OptionalSettingsEntry.MemZoneB)
            or nameof(OptionalSettingsEntry.PriorityZoneA) or nameof(OptionalSettingsEntry.PriorityZoneB)
            or nameof(OptionalSettingsEntry.RoamingZone))
        {
            NotifyOptionalSettingsStartupNamesChanged();
        }

        if (e.PropertyName == nameof(OptionalSettingsEntry.FmWorkChannel))
        {
            OnPropertyChanged(nameof(OptionalSettingsFmWorkChannelName));
        }

        if (e.PropertyName == nameof(OptionalSettingsEntry.AmWorkZone))
        {
            OnPropertyChanged(nameof(OptionalSettingsAmWorkZoneName));
        }
    }

    private void OnAlarmSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AlarmSettingsEntry.AnalogEniType) or nameof(AlarmSettingsEntry.AnalogEmergencyId))
        {
            OnPropertyChanged(nameof(AlarmSettingsAnalogEmergencyIdOptions));
            OnPropertyChanged(nameof(AlarmSettingsAnalogEmergencyIdSelection));
        }

        if (e.PropertyName == nameof(AlarmSettingsEntry.AnalogEmergencyChannel))
        {
            OnPropertyChanged(nameof(AlarmSettingsAnalogEmergencyChannelSelection));
        }

        if (e.PropertyName == nameof(AlarmSettingsEntry.DigitalEmergencyChannel))
        {
            OnPropertyChanged(nameof(AlarmSettingsDigitalEmergencyChannelSelection));
        }
    }

    private void OnEncryptionKeysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachEncryptionKeyHandlers(e.NewItems?.OfType<EncryptionKeyEntry>());
        DetachEncryptionKeyHandlers(e.OldItems?.OfType<EncryptionKeyEntry>());
        _projectStructureDirty = true;
        OnPropertyChanged(nameof(DigitalEncryptionKeyOptions));
        OnPropertyChanged(nameof(AesEncryptionKeyOptions));
        OnPropertyChanged(nameof(Arc4EncryptionKeyOptions));
        NotifyDirtyStateChanged();
        RefreshValidationAndPreview();
    }

    /// <summary>Editing an existing key's text (not adding/removing a slot -
    /// every slot always exists, see EnsureEncryptionKeySlotsPresent) never
    /// fired CollectionChanged, so it never marked the project dirty or
    /// re-ran validation - found 2026-07-19 while adding real format
    /// validation for these fields (AES/ARC4/Basic hex/digit length), which
    /// would otherwise never actually surface until some unrelated edit
    /// happened to trigger a refresh.</summary>
    private void OnEncryptionKeyEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _projectStructureDirty = true;
        NotifyDirtyStateChanged();
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();

        if (sender is EncryptionKeyEntry key)
        {
            SyncEncryptionKeyVisibility(key);
        }
    }

    private void AttachEncryptionKeyHandlers(IEnumerable<EncryptionKeyEntry>? keys)
    {
        if (keys is null)
        {
            return;
        }

        foreach (var key in keys)
        {
            key.PropertyChanged += OnEncryptionKeyEntryPropertyChanged;
        }
    }

    private void DetachEncryptionKeyHandlers(IEnumerable<EncryptionKeyEntry>? keys)
    {
        if (keys is null)
        {
            return;
        }

        foreach (var key in keys)
        {
            key.PropertyChanged -= OnEncryptionKeyEntryPropertyChanged;
        }
    }

    /// <summary>Whether a slot has a real value set, per its Kind's own
    /// "which column is the real one" convention (see EncryptionKeyEntry's
    /// own class doc comment) - Basic's real field defaults to "0000", not
    /// "Off" like ARC4/AES.</summary>
    private static bool IsEncryptionKeyOccupied(EncryptionKeyEntry key) => key.Kind switch
    {
        EncryptionKeyKind.Basic => key.EncryptionId != "0000",
        EncryptionKeyKind.Arc4 => key.EncryptionKey != "Off",
        EncryptionKeyKind.Aes => key.EncryptionId != "Off",
        _ => false
    };

    private ObservableCollection<EncryptionKeyEntry> VisibleEncryptionKeyCollectionFor(EncryptionKeyKind kind) => kind switch
    {
        EncryptionKeyKind.Basic => VisibleEncryptionKeys,
        EncryptionKeyKind.Arc4 => VisibleArc4EncryptionKeys,
        EncryptionKeyKind.Aes => VisibleAesEncryptionKeys,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>Adds/removes one entry from its Kind's Visible* list to
    /// match whether it's currently occupied - called on every property
    /// change of any encryption key entry (see
    /// OnEncryptionKeyEntryPropertyChanged), so Add/Remove/a project load
    /// all keep the visible lists correct without each call site needing to
    /// remember to update them.</summary>
    private void SyncEncryptionKeyVisibility(EncryptionKeyEntry key)
    {
        var visible = VisibleEncryptionKeyCollectionFor(key.Kind);
        var isOccupied = IsEncryptionKeyOccupied(key);
        var index = visible.IndexOf(key);

        if (isOccupied && index < 0)
        {
            var insertIndex = 0;
            while (insertIndex < visible.Count && visible[insertIndex].Number < key.Number)
            {
                insertIndex++;
            }

            visible.Insert(insertIndex, key);
        }
        else if (!isOccupied && index >= 0)
        {
            visible.RemoveAt(index);
        }
    }

    private void SyncAllEncryptionKeyVisibility(IEnumerable<EncryptionKeyEntry> keys)
    {
        foreach (var key in keys)
        {
            SyncEncryptionKeyVisibility(key);
        }
    }

    private void OnTalkgroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachEditorHandlers(e.NewItems);
        DetachEditorHandlers(e.OldItems);
    }

    private void OnRadioIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachEditorHandlers(e.NewItems);
        DetachEditorHandlers(e.OldItems);
    }

    private void OnTalkgroupWhitelistChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachEditorHandlers(e.NewItems);
        DetachEditorHandlers(e.OldItems);
    }

    private void OnDigitalContactWhitelistChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachEditorHandlers(e.NewItems);
        DetachEditorHandlers(e.OldItems);
    }

    /// <summary>Generic version of AttachChannelHandlers/AttachZoneHandlers/
    /// AttachScanListHandlers below, for entities that don't need any extra
    /// per-item wiring beyond OnEditorPropertyChanged itself (no nested
    /// member list, no entity-specific side effects).</summary>
    private void AttachEditorHandlers(System.Collections.IList? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items.OfType<INotifyPropertyChanged>())
        {
            item.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    private void DetachEditorHandlers(System.Collections.IList? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items.OfType<INotifyPropertyChanged>())
        {
            item.PropertyChanged -= OnEditorPropertyChanged;
        }
    }

    private void AttachChannelHandlers(IEnumerable<ChannelEntry>? channels)
    {
        if (channels is null)
        {
            return;
        }

        foreach (var channel in channels)
        {
            channel.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    private void DetachChannelHandlers(IEnumerable<ChannelEntry>? channels)
    {
        if (channels is null)
        {
            return;
        }

        foreach (var channel in channels)
        {
            channel.PropertyChanged -= OnEditorPropertyChanged;
        }
    }

    private void AttachZoneHandlers(IEnumerable<ZoneEntry>? zones)
    {
        if (zones is null)
        {
            return;
        }

        foreach (var zone in zones)
        {
            zone.PropertyChanged += OnEditorPropertyChanged;
            zone.Members.CollectionChanged += OnZoneMembersChanged;
        }
    }

    private void DetachZoneHandlers(IEnumerable<ZoneEntry>? zones)
    {
        if (zones is null)
        {
            return;
        }

        foreach (var zone in zones)
        {
            zone.PropertyChanged -= OnEditorPropertyChanged;
            zone.Members.CollectionChanged -= OnZoneMembersChanged;
        }
    }

    private void OnZoneMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _projectStructureDirty = true;
        RefreshAvailableZoneChannels();
        OnPropertyChanged(nameof(SelectedZoneMemberOptions));
        NotifyOptionalSettingsStartupNamesChanged();
        NotifyDirtyStateChanged();
        RefreshValidation();
    }

    private void OnScanListsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachScanListHandlers(e.NewItems?.OfType<ScanListEntry>());
        DetachScanListHandlers(e.OldItems?.OfType<ScanListEntry>());
    }

    private void AttachScanListHandlers(IEnumerable<ScanListEntry>? scanLists)
    {
        if (scanLists is null)
        {
            return;
        }

        foreach (var scanList in scanLists)
        {
            scanList.PropertyChanged += OnEditorPropertyChanged;
            scanList.Members.CollectionChanged += OnScanListMembersChanged;
        }
    }

    private void DetachScanListHandlers(IEnumerable<ScanListEntry>? scanLists)
    {
        if (scanLists is null)
        {
            return;
        }

        foreach (var scanList in scanLists)
        {
            scanList.PropertyChanged -= OnEditorPropertyChanged;
            scanList.Members.CollectionChanged -= OnScanListMembersChanged;
        }
    }

    private void OnScanListMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _projectStructureDirty = true;
        RefreshAvailableScanListChannels();
        OnPropertyChanged(nameof(SelectedScanListMemberOptions));
        NotifyDirtyStateChanged();
        RefreshValidation();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressEditorRefresh)
        {
            return;
        }

        NotifyDirtyStateChanged();
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();

        if (e.PropertyName == nameof(ChannelEntry.ChannelType))
        {
            OnPropertyChanged(nameof(BusyLockTxPermitValues));
            OnPropertyChanged(nameof(BusyLockTxPermitHeaderText));
        }
    }

    private void NotifyDirtyStateChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyIndicator));
        SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Every one of these XxxCount properties is a plain computed
    /// wrapper (Collection.Count) with no automatic INotifyPropertyChanged
    /// link to the collection's own Count - ObservableCollection raises ITS
    /// OWN "Count" PropertyChanged internally, but that does not propagate
    /// to notify a completely different property on this ViewModel that
    /// happens to be computed from it. Every one of these was previously
    /// only ever notified from its own Add/Remove command, never after a
    /// bulk repopulation (Read From Radio, Load Project, New Project)
    /// replaced the collection's contents directly - found 2026-08-02: the
    /// UI reported "0 AM zones" right after a real read that
    /// correctly found one (confirmed via a live USB capture: the read and
    /// decode were both correct, only this notification was missing). Every
    /// sidebar count badge was stale after every bulk repopulation until
    /// now, not just AM Zone's.</summary>
    private void NotifyAllEntityCounts()
    {
        OnPropertyChanged(nameof(ChannelCount));
        OnPropertyChanged(nameof(ZoneCount));
        OnPropertyChanged(nameof(RadioIdCount));
        OnPropertyChanged(nameof(TalkgroupCount));
        OnPropertyChanged(nameof(ScanListCount));
        OnPropertyChanged(nameof(RoamingChannelCount));
        OnPropertyChanged(nameof(RoamingZoneCount));
        OnPropertyChanged(nameof(ReceiveGroupListCount));
        OnPropertyChanged(nameof(AutoRepeaterOffsetCount));
        OnPropertyChanged(nameof(AnalogAddressCount));
        OnPropertyChanged(nameof(GpsRoamingCount));
        OnPropertyChanged(nameof(TalkgroupWhitelistCount));
        OnPropertyChanged(nameof(DigitalContactWhitelistCount));
        OnPropertyChanged(nameof(PrefabricatedSmsCount));
        OnPropertyChanged(nameof(AmAirCount));
        OnPropertyChanged(nameof(AmZoneCount));
        OnPropertyChanged(nameof(FmChannelCount));
        OnPropertyChanged(nameof(AprsReceiveFilterCount));
        OnPropertyChanged(nameof(AnalogQuickCallCount));
        OnPropertyChanged(nameof(StateInformationCount));
        OnPropertyChanged(nameof(Qdc1200IdCount));
        OnPropertyChanged(nameof(QdcAddressCount));
        OnPropertyChanged(nameof(FiveToneIdCount));
    }

    private void RefreshValidationAndPreview(string? status = null)
    {
        Validate();

        // CSV export is disabled during the Channel canonical-model
        // migration - CpsCsvExporter throws NotSupportedException. Skip the
        // preview rather than let it take down every channel/zone edit.
        try
        {
            ChannelPreview = CpsCsvExporter.BuildChannelCsv(Channels);
            ZonePreview = CpsCsvExporter.BuildZoneCsv(Zones);
        }
        catch (NotSupportedException)
        {
            ChannelPreview = "";
            ZonePreview = "";
        }

        RefreshCsvPreviewTable(ChannelPreview, ChannelPreviewHeaders, ChannelPreviewRows);
        RefreshCsvPreviewTable(ZonePreview, ZonePreviewHeaders, ZonePreviewRows);
        StatusMessage = status ?? StatusMessage;
    }

    private static void RefreshCsvPreviewTable(
        string csv,
        ObservableCollection<string> headers,
        ObservableCollection<CsvPreviewRow> rows)
    {
        var parsedRows = ParseCsvRows(csv);
        headers.Clear();
        rows.Clear();

        if (parsedRows.Count == 0)
        {
            return;
        }

        foreach (var header in parsedRows[0])
        {
            headers.Add(header);
        }

        foreach (var row in parsedRows.Skip(1))
        {
            rows.Add(new CsvPreviewRow { Cells = row });
        }
    }

    private static List<IReadOnlyList<string>> ParseCsvRows(string csv)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (current == ',' && !inQuotes)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if ((current == '\r' || current == '\n') && !inQuotes)
            {
                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                cell.Append(current);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private void RefreshValidation(string? status = null)
    {
        Validate();
        StatusMessage = status ?? StatusMessage;
    }

    private void Validate()
    {
        ValidationMessages.Clear();
        ValidateChannels();
        ValidateZones();
        ValidateRadioIds();
        ValidateTalkgroups();
        ValidateScanLists();
        ValidateRoamingChannels();
        ValidateRoamingZones();
        ValidateAutoRepeaterOffsets();
        ValidateGpsRoaming();
        ValidateReceiveGroupLists();
        ValidateMasterId();
        ValidateAnalogAddresses();
        ValidateTalkgroupWhitelist();
        ValidatePrefabricatedSms();
        ValidateAmAir();
        ValidateAmZones();
        ValidateFmChannels();
        ValidateAlarmSettings();
        ValidateDigitalContactWhitelist();
        ValidateDigitalContacts();
        ValidateAprsSettings();
        ValidateAprsReceiveFilters();
        ValidateTalkAliasSettings();
        ValidateOptionalSettings();
        ValidateEncryptionKeys();
        ValidateFiveTone();
        ValidateTwoTone();
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(HasBlockingValidationErrors));
        SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Validates the real key-material field for each encryption
    /// key type (the OTHER field on <see cref="EncryptionKeyEntry"/> is an
    /// unconfirmed placeholder - see RadioReadMapper.MapAesEncryptionKeys's
    /// doc comment - not a real radio field, deliberately not validated).
    /// These are blocking errors: an out-of-format value here would throw
    /// from EncryptionKeyCodec at write time (ArgumentException) rather
    /// than fail gracefully, and saving one to the project file silently
    /// would produce a file that can never actually be written back.</summary>
    private void ValidateEncryptionKeys()
    {
        // Blocking, not "Warning:" - see EncryptionKeyEntry's own doc
        // comment for the 2026-08-09 ObservableValidator conversion (same
        // format rules as before, now enforced live per-field instead of
        // only at Save/Write time).
        foreach (var key in AesEncryptionKeys)
        {
            foreach (var error in key.GetErrors(nameof(key.EncryptionIdText)))
            {
                ValidationMessages.Add($"AES Key {key.Number}: {error.ErrorMessage}");
            }
        }

        foreach (var key in Arc4EncryptionKeys)
        {
            foreach (var error in key.GetErrors(nameof(key.EncryptionKeyText)))
            {
                ValidationMessages.Add($"ARC4 Key {key.Number}: {error.ErrorMessage}");
            }
        }

        foreach (var code in EncryptionKeys)
        {
            foreach (var error in code.GetErrors(nameof(code.EncryptionIdText)))
            {
                ValidationMessages.Add($"Basic Encryption Code {code.Number}: {error.ErrorMessage}");
            }
        }

        // Vendor CPS enforces this too (english.ini: "Encryption Id Can not
        // repeat") - not previously replicated here. "0000" is Basic Code's
        // own default/unset value (EnsureEncryptionKeySlotsPresent seeds
        // every slot with it), so it's excluded the same way "Off" is for
        // AES/ARC4 - otherwise every untouched codeplug would immediately
        // warn about its 32 identical default slots.
        ValidateNoDuplicateValues(AesEncryptionKeys.Where(k => k.EncryptionId != "Off"), k => k.EncryptionId, "AES Key");
        ValidateNoDuplicateValues(Arc4EncryptionKeys.Where(k => k.EncryptionKey != "Off"), k => k.EncryptionKey, "ARC4 Key");
        ValidateNoDuplicateValues(EncryptionKeys.Where(k => k.EncryptionId != "0000"), k => k.EncryptionId, "Basic Encryption Code");
    }

    private void ValidateNoDuplicateValues<T>(IEnumerable<T> entries, Func<T, string> selectValue, string label)
    {
        var seenValues = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (!seenValues.Add(selectValue(entry)))
            {
                ValidationMessages.Add($"Warning: two or more {label} slots share the same value - the vendor CPS does not allow this");
                return;
            }
        }
    }

    private static bool IsHex(string value) => value.Length > 0 && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Validates the canonical typed <see cref="ChannelEntry"/> fields.
    /// Dramatically smaller than the old string-based version: an
    /// enum-like field (ChannelType, TransmitPower, Bandwidth, CTCSS/DCS
    /// mode, Squelch Mode, Optional Signal, Busy-Lock/TX-Permit, PTT ID) is
    /// now a raw byte that can't hold an invalid value in the first place -
    /// there's nothing left to check for those beyond what the compiler
    /// already guarantees. What's left: uniqueness/range checks that are
    /// still real constraints even on a typed field, and existence checks
    /// for reference fields (does the Talkgroup/Radio ID/Scan List/Receive
    /// Group List/encryption key this channel points at by index actually
    /// exist right now).
    /// </summary>
    private void ValidateChannels()
    {
        var numbers = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in Channels)
        {
            if (channel.Number <= 0 || channel.Number > D890UvMemoryMap.MaxRegularChannelCount)
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: channel number must be 1-{D890UvMemoryMap.MaxRegularChannelCount}");
            }

            if (!numbers.Add(channel.Number))
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: duplicate channel number");
            }

            if (string.IsNullOrWhiteSpace(channel.Name))
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: missing channel name");
            }
            else if (!names.Add(channel.Name))
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: duplicate channel name");
            }

            // Radio field is 32 bytes UTF-16LE = 16 chars max
            // (TextFieldCodec.EncodeName silently truncates anything longer -
            // this must block before that silent truncation ever happens).
            if (channel.Name.Length > 16)
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: channel name is {channel.Name.Length} characters, max 16");
            }

            // Range corrected 2026-08-02 to the real vendor CPS limit
            // (140-480 MHz continuous), then corrected AGAIN 2026-08-07:
            // the radio's real coverage is two disjoint bands (136-174 VHF,
            // 400-480 UHF), not one continuous span - a Roaming Channel
            // live capture proved the vendor CPS itself rejects the
            // 174-400 dead zone. See CodeplugLimits.IsValidVhfOrUhfFrequencyMhz's
            // own doc comment, matches ChannelEntry.RxFrequencyMHzText's
            // own ValidateFrequencyText.
            if (!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(channel.RxFrequencyMHz))
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: RX frequency is outside the {CodeplugLimits.VhfFrequencyMinMhz}-{CodeplugLimits.VhfFrequencyMaxMhz} or {CodeplugLimits.UhfFrequencyMinMhz}-{CodeplugLimits.UhfFrequencyMaxMhz} MHz range");
            }

            var txFrequency = channel.ComputeTransmitFrequencyMHz();
            if (!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(txFrequency))
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: TX frequency is outside the {CodeplugLimits.VhfFrequencyMinMhz}-{CodeplugLimits.VhfFrequencyMaxMhz} or {CodeplugLimits.UhfFrequencyMinMhz}-{CodeplugLimits.UhfFrequencyMaxMhz} MHz range");
            }

            // Range corrected 2026-08-02 to the real vendor CPS limit
            // (50-260 Hz) - confirmed directly. Skipped when 0 (the
            // field's default/untouched state) - "Custom CTCSS" isn't yet
            // selectable as an actual CTCSS Decode/Encode mode (task #37),
            // so most channels never touch this field at all and 0 must not
            // read as an out-of-range value.
            if (channel.CustomCtcss != 0 && channel.CustomCtcss is < 500 or > 2600)
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: Custom CTCSS is outside the 50.0-260.0 Hz range");
            }

            // Real vendor CPS limit is 0-1250 Hz (0-125 raw, byte*10) - see
            // CorrectFrequencyHzText's own doc comment. The Text property
            // only ever commits a value in this range through the UI, but a
            // raw byte up to 255 could still arrive via an old project file
            // or a radio read, so it's re-checked here the same way
            // Custom CTCSS is just above.
            if (channel.CorrectFrequencyHz > 125)
            {
                ValidationMessages.Add($"{channel.DisplayLabel}: Correct Frequency is outside the 0-1250 Hz range");
            }

            ValidateRange(channel, channel.ScrambleMode, 0, 15, "Scramble Set");
            if (channel.IsCustomScramble)
            {
                ValidateRange(channel, channel.CustomScrambleFrequencyIndex, 0, 28, "Custom Scrambler");
            }

            if (channel.IsDigital)
            {
                ValidateRange(channel, channel.ColorCode, 0, 15, "Color Code");
                ValidateRange(channel, channel.TxColorCode, 0, 15, "TX Color Code");
                ValidateIndexExists(channel, channel.ContactIndex, Talkgroups.Select(t => t.Number - 1), "Contact/Talk Group");
                ValidateIndexExists(channel, channel.RadioIdIndex, RadioIds.Select(r => r.Number - 1), "Radio ID");

                if (channel.UsesDigitalEncryption)
                {
                    ValidateEncryptionKeyExists(channel, channel.DigitalEncryptionIndex, EncryptionKeys, "Digital Encryption");
                }

                if (channel.UsesAesEncryption)
                {
                    ValidateEncryptionKeyExists(channel, channel.AesEncryptionIndex, AesEncryptionKeys, "AES Digital Encryption");
                }

                if (channel.UsesArc4Encryption)
                {
                    ValidateEncryptionKeyExists(channel, channel.Arc4EncryptionKeyIndex, Arc4EncryptionKeys, "ARC4");
                }
            }

            // 2026-07-19: confirmed via a live read against real hardware -
            // "no scan list"/"no receive group list" is raw byte 255 (0xFF),
            // NOT 0, on this radio (every single real channel read back had
            // exactly 255 here, which doesn't correspond to any real list -
            // a classic byte-sentinel-max-value convention, same idea as the
            // 0x00/0xFF blank-index sentinels used elsewhere on this radio).
            // Was wrongly assumed to be 0 when this validation was written,
            // which produced a false "index 255 is not defined" warning on
            // every single channel.
            const int noScanListOrReceiveGroupSentinel = 255;

            if (channel.ScanListIndex != noScanListOrReceiveGroupSentinel)
            {
                ValidateIndexExists(channel, channel.ScanListIndex, ScanLists.Select(s => s.Number - 1), "Scan List");
            }

            if (channel.ReceiveGroupListIndex != noScanListOrReceiveGroupSentinel)
            {
                ValidateIndexExists(channel, channel.ReceiveGroupListIndex, ReceiveGroupLists.Select(g => g.Number - 1), "Receive Group List");
            }
        }
    }

    private void ValidateIndexExists(ChannelEntry channel, int index, IEnumerable<int> validIndices, string field)
    {
        if (!validIndices.Contains(index))
        {
            ValidationMessages.Add($"Warning: {channel.DisplayLabel}: {field} index {index} is not defined");
        }
    }

    private void ValidateEncryptionKeyExists(
        ChannelEntry channel,
        int keyNumber,
        IEnumerable<EncryptionKeyEntry> keys,
        string field)
    {
        if (keyNumber == 0)
        {
            return;
        }

        if (keys.All(key => key.Number != keyNumber))
        {
            ValidationMessages.Add($"Warning: {channel.DisplayLabel}: {field} key {keyNumber} is not defined");
        }
    }

    private void ValidateKnownValue(
        ChannelEntry channel,
        string value,
        IReadOnlyCollection<string>? allowedValues,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ValidationMessages.Add($"{channel.DisplayLabel}: missing {field}");
            return;
        }

        if (allowedValues is not null && !allowedValues.Contains(value))
        {
            ValidationMessages.Add($"Warning: {channel.DisplayLabel}: unknown {field}: {value}");
        }
    }

    private void ValidateInteger(ChannelEntry channel, string value, string field)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            ValidationMessages.Add($"{channel.DisplayLabel}: {field} must be an integer");
        }
    }

    private void ValidateRange(ChannelEntry channel, int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            ValidationMessages.Add($"{channel.DisplayLabel}: {field} must be {min}-{max}");
        }
    }

    private void ValidateFrequency(ChannelEntry channel, string value, string field)
    {
        if (!FrequencyPattern.IsMatch(value))
        {
            ValidationMessages.Add($"{channel.DisplayLabel}: {field} must use 000.00000");
            return;
        }

        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var frequency)
            || frequency is < 100.0 or > 999.99999)
        {
            ValidationMessages.Add($"{channel.DisplayLabel}: {field} is outside expected range");
        }
    }

    private void ValidateZones()
    {
        var numbers = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Zones.Count > CodeplugLimits.ZoneListMax)
        {
            ValidationMessages.Add($"Warning: {Zones.Count} Zones defined, AnyTone lists support max {CodeplugLimits.ZoneListMax}");
        }

        foreach (var zone in Zones)
        {
            if (zone.Number <= 0)
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: zone number must be positive");
            }

            if (!numbers.Add(zone.Number))
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: duplicate zone number");
            }

            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: missing zone name");
            }
            else if (!names.Add(zone.Name))
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: duplicate zone name");
            }
            else if (zone.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            if (zone.Members.Count == 0)
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: zone has no channels");
            }

            if (zone.Members.Count > CodeplugLimits.ZoneMemberMax)
            {
                ValidationMessages.Add($"{zone.DisplayLabel}: zone channel list is limited to {CodeplugLimits.ZoneMemberMax} entries by the radio's memory layout");
            }

            // Softened to a warning 2026-07-18: root-caused via a real
            // hardware differential capture (Capture_Findings.md's
            // "RESOLVED 2026-07-15" note) - the radio's A/B channel fields
            // are its live front-panel selection, which legitimately drifts
            // out of sync with a zone's configured membership during normal
            // use. A zone read straight from the radio can fail this check
            // while being perfectly valid, so it's advisory, not an error.
            if (zone.AChannel is not null && !zone.Members.Contains(zone.AChannel))
            {
                ValidationMessages.Add($"Warning: {zone.DisplayLabel}: A channel is not currently a zone member (may just be the radio's last front-panel selection)");
            }

            if (zone.BChannel is not null && !zone.Members.Contains(zone.BChannel))
            {
                ValidationMessages.Add($"Warning: {zone.DisplayLabel}: B channel is not currently a zone member (may just be the radio's last front-panel selection)");
            }
        }
    }

    private static string GetDisplayLocation(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            return "";
        }

        var directory = Path.GetDirectoryName(files[0]) ?? "";
        return files.Count == 1
            ? files[0]
            : $"{directory} ({files.Count} files)";
    }

    // TEMPORARY - added 2026-08-16 for one live hardware test (read =>
    // image => model => image => write, using the radio's own real data
    // the whole way through, no fabricated values), remove after that
    // test. Forces every entity's own _radioSyncSnapshot to null (the
    // same null-means-dirty convention HasAnyPendingRadioWrite already
    // uses everywhere) via reflection, WITHOUT changing any field value -
    // this makes the normal Write to Radio path re-run every entity's own
    // BuildXValues/Encode/ApplyXPatch, producing a freshly re-encoded
    // image from the model's current (real, just-read) state instead of
    // re-sending the original captured bytes unchanged. Only meaningful
    // right after a Read From Radio.
    //
    // The DynamicDependency attributes below are load-bearing, not
    // decoration - real crash found live on Android 2026-08-16: NativeAOT's
    // trimmer strips a private field's reflection metadata unless something
    // tells it not to, since nothing else in the app ever reflects on
    // _radioSyncSnapshot. Without these, GetField(...) returns null on
    // every entity and the method throws immediately. Desktop never showed
    // this because a normal JIT build doesn't trim at all.
    [DynamicDependency("_radioSyncSnapshot", typeof(ChannelEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(ZoneEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(RadioIdEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TalkgroupEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(ScanListEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(RoamingChannelEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(RoamingZoneEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(ReceiveGroupListEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AutoRepeaterOffsetEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AnalogAddressEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(GpsRoamingEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TalkgroupWhitelistEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(DigitalContactWhitelistEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(PrefabricatedSmsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AmAirEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AmZoneEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(FmChannelEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AprsReceiveFilterEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(Qdc1200IdEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TwoToneEncodeEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TwoToneDecodeEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AnalogQuickCallEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(StateInformationEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(HotKeyEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(QdcAddressEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(FiveToneIdEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(DtmfEncodeEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(EncryptionKeyEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(MasterIdEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TalkAliasSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AlarmSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(AprsSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(OptionalSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(Qdc1200SettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(FiveToneSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(TwoToneEncodeSettingsEntry))]
    [DynamicDependency("_radioSyncSnapshot", typeof(DtmfSettingsEntry))]
    [RelayCommand]
    private void DevForceModelToImage()
    {
        void ResetOne(object entity)
        {
            var field = entity.GetType().GetField("_radioSyncSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{entity.GetType().Name} has no _radioSyncSnapshot field - can't force it dirty.");
            field.SetValue(entity, null);
        }

        foreach (var c in Channels) ResetOne(c);
        foreach (var z in Zones) ResetOne(z);
        foreach (var e in RadioIds) ResetOne(e);
        foreach (var e in Talkgroups) ResetOne(e);
        foreach (var e in ScanLists) ResetOne(e);
        foreach (var e in RoamingChannels) ResetOne(e);
        foreach (var e in RoamingZones) ResetOne(e);
        foreach (var e in ReceiveGroupLists) ResetOne(e);
        foreach (var e in AutoRepeaterOffsets) ResetOne(e);
        foreach (var e in AnalogAddresses) ResetOne(e);
        foreach (var e in GpsRoamingEntries) ResetOne(e);
        foreach (var e in TalkgroupWhitelist) ResetOne(e);
        foreach (var e in DigitalContactWhitelist) ResetOne(e);
        foreach (var e in PrefabricatedSmsMessages) ResetOne(e);
        foreach (var e in AmAirChannels) ResetOne(e);
        foreach (var e in AmZones) ResetOne(e);
        foreach (var e in FmChannels) ResetOne(e);
        foreach (var e in AprsReceiveFilters) ResetOne(e);
        foreach (var e in Qdc1200Ids) ResetOne(e);
        foreach (var e in TwoToneEncodeEntries) ResetOne(e);
        foreach (var e in TwoToneDecodeEntries) ResetOne(e);
        foreach (var e in AnalogQuickCalls) ResetOne(e);
        foreach (var e in StateInformationEntries) ResetOne(e);
        foreach (var e in HotKeys) ResetOne(e);
        foreach (var e in QdcAddresses) ResetOne(e);
        foreach (var e in FiveToneIds) ResetOne(e);
        foreach (var e in DtmfEncodeEntries) ResetOne(e);
        foreach (var e in EncryptionKeys) ResetOne(e);
        foreach (var e in Arc4EncryptionKeys) ResetOne(e);
        foreach (var e in AesEncryptionKeys) ResetOne(e);

        ResetOne(MasterId);
        ResetOne(TalkAliasSettings);
        ResetOne(AlarmSettings);
        ResetOne(AprsSettings);
        ResetOne(OptionalSettings);
        ResetOne(Qdc1200Settings);
        ResetOne(FiveToneSettings);
        ResetOne(TwoToneEncodeSettings);
        ResetOne(DtmfSettings);

        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        StatusMessage = "Every entity marked dirty from its own current (real) values - Write to Radio will now re-encode everything.";
    }
}
