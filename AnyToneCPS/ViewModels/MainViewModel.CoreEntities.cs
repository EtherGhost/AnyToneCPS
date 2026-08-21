using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AnyToneCPS.Models;
using AnyToneCPS.Services.Radio.Codecs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// The "core codeplug" entities a channel references by index: Radio ID,
/// Talkgroup (DMR contact), Scan List, Roaming Channel/Zone, Receive Group
/// List, and Auto Repeater Offset Frequency. Split out of MainViewModel.cs
/// to keep that file from growing further. Follows the same simple,
/// no-dirty-tracking pattern as the existing EncryptionKeyEntry lists
/// (these are simpler, flatter data than Channel/Zone).
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<RadioIdEntry> RadioIds { get; } = [];
    public ObservableCollection<TalkgroupEntry> Talkgroups { get; } = [];
    public ObservableCollection<ScanListEntry> ScanLists { get; } = [];
    public ObservableCollection<RoamingChannelEntry> RoamingChannels { get; } = [];
    public ObservableCollection<RoamingZoneEntry> RoamingZones { get; } = [];
    public ObservableCollection<ReceiveGroupListEntry> ReceiveGroupLists { get; } = [];
    public ObservableCollection<AutoRepeaterOffsetEntry> AutoRepeaterOffsets { get; } = [];
    public ObservableCollection<AnalogAddressEntry> AnalogAddresses { get; } = [];
    public ObservableCollection<GpsRoamingEntry> GpsRoamingEntries { get; } = [];
    public ObservableCollection<TalkgroupWhitelistEntry> TalkgroupWhitelist { get; } = [];
    public ObservableCollection<DigitalContactWhitelistEntry> DigitalContactWhitelist { get; } = [];

    /// <summary>Removing an entry leaves no per-entry trace on
    /// HasAnyPendingRadioWrite (the entry is just gone), so a pure deletion
    /// with no other edits needs its own signal to trigger a write - a
    /// simple count comparison against the last-synced count, updated
    /// alongside the per-entry MarkRadioSynced loop after every successful
    /// Read/Write (see MainViewModel.Radio.cs/MainViewModel.RadioWrite.cs).</summary>
    private int _talkgroupWhitelistSyncedCount;
    private int _digitalContactWhitelistSyncedCount;

    public ObservableCollection<PrefabricatedSmsEntry> PrefabricatedSmsMessages { get; } = [];
    public ObservableCollection<AmAirEntry> AmAirChannels { get; } = [];
    public ObservableCollection<AmZoneEntry> AmZones { get; } = [];
    public ObservableCollection<FmChannelEntry> FmChannels { get; } = [];

    /// <summary>Digital Contact database - only populated when the user
    /// opts into <see cref="IncludeDigitalContactList"/> before reading
    /// (see MainViewModel.Radio.cs), since this can be a very large,
    /// slow-to-read list unlike everything else here. <see cref="FilteredDigitalContacts"/>
    /// is the view actually bound in the UI, filtered client-side by
    /// <see cref="DigitalContactFilterText"/> since a raw ListBox over
    /// tens of thousands of rows needs a way to narrow down to one contact.</summary>
    public ObservableCollection<DigitalContactEntry> DigitalContacts { get; } = [];
    public ObservableCollection<DigitalContactEntry> FilteredDigitalContacts { get; } = [];

    [ObservableProperty] private string _digitalContactFilterText = "";

    /// <summary>Added 2026-08-09 alongside <see cref="DigitalContactEntry.IsFriend"/>
    /// - combines with <see cref="DigitalContactFilterText"/> (AND, not OR)
    /// so "friends named X" is expressible over a 500,000-row list without
    /// a dedicated Friends List picker view.</summary>
    [ObservableProperty] private bool _digitalContactFriendsOnly;

    /// <summary>Radio-write dirty flag for the WHOLE list - deliberately
    /// coarse (not a per-entry HasAnyPendingRadioWrite like every other
    /// entity) since any single edit/add/delete requires rewriting the
    /// ENTIRE contact stream anyway (see DigitalContactWriter's own doc
    /// comment). Defaults false, NOT true - unlike other entities, treating
    /// a freshly-loaded project's digital contacts as automatically
    /// "pending write" would risk silently overwriting a real, possibly
    /// very large on-radio database with stale project-file data the first
    /// time Write to Radio runs for something unrelated. Only set true by
    /// an explicit edit/add/delete actually made this session; a fresh
    /// Read From Radio clears it back to false (see MainViewModel.Radio.cs).</summary>
    private bool _digitalContactsDirty;

    /// <summary>Editable since 2026-08-09 (radio write support added the
    /// same day - see DigitalContactWriter's own doc comment). Unlike
    /// Channel/Zone/Talkgroup/etc., this does NOT attach a PropertyChanged
    /// handler to every entry up front - the list can be 100,000+ rows, so
    /// that would mean 100,000+ live subscriptions for a list where only
    /// one row is ever being edited at a time. Instead,
    /// OnSelectedDigitalContactChanged attaches/detaches just the ONE
    /// currently-selected entry's handler.</summary>
    [ObservableProperty] private DigitalContactEntry? _selectedDigitalContact;

    partial void OnSelectedDigitalContactChanged(DigitalContactEntry? oldValue, DigitalContactEntry? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnDigitalContactPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnDigitalContactPropertyChanged;
        }

        RemoveDigitalContactCommand.NotifyCanExecuteChanged();
    }

    private void OnDigitalContactPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _digitalContactsDirty = true;
        OnEditorPropertyChanged(sender, e);

        // Toggling IsFriend while the "friends only" filter is active must
        // drop/show the row immediately, not just on the next unrelated
        // filter refresh.
        if (e.PropertyName == nameof(DigitalContactEntry.IsFriend) && DigitalContactFriendsOnly)
        {
            RefreshFilteredDigitalContacts();
        }
    }

    partial void OnDigitalContactFilterTextChanged(string value) => RefreshFilteredDigitalContacts();
    partial void OnDigitalContactFriendsOnlyChanged(bool value) => RefreshFilteredDigitalContacts();

    [RelayCommand]
    private void AddDigitalContact()
    {
        var index = DigitalContacts.Count == 0 ? 0 : DigitalContacts.Max(c => c.Index) + 1;
        var entry = new DigitalContactEntry { Index = index, Name = $"Contact {index + 1}" };
        DigitalContacts.Add(entry);
        _digitalContactsDirty = true;
        SelectedDigitalContact = entry;
        RefreshFilteredDigitalContacts();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedDigitalContact))]
    private void RemoveDigitalContact()
    {
        if (SelectedDigitalContact is null)
        {
            return;
        }

        DigitalContacts.Remove(SelectedDigitalContact);
        _digitalContactsDirty = true;
        SelectedDigitalContact = null;
        RefreshFilteredDigitalContacts();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedDigitalContact() => SelectedDigitalContact is not null;

    public void RefreshFilteredDigitalContacts()
    {
        FilteredDigitalContacts.Clear();
        var filter = DigitalContactFilterText;
        IEnumerable<DigitalContactEntry> matches = string.IsNullOrWhiteSpace(filter)
            ? DigitalContacts
            : DigitalContacts.Where(c =>
                c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Callsign.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.City.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Country.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.RadioId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase));

        if (DigitalContactFriendsOnly)
        {
            matches = matches.Where(c => c.IsFriend);
        }

        foreach (var c in matches)
        {
            FilteredDigitalContacts.Add(c);
        }
    }

    /// <summary>Single instance, not a collection - there's only ever one
    /// Master ID on the radio.</summary>
    public MasterIdEntry MasterId { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// Talk Alias Settings record on the radio.</summary>
    public TalkAliasSettingsEntry TalkAliasSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// Alarm/Emergency settings record on the radio.</summary>
    public AlarmSettingsEntry AlarmSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// APRS Settings record on the radio.</summary>
    public AprsSettingsEntry AprsSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// Optional Settings record on the radio. Deliberately a partial port
    /// (Power-on/Display/Key Function only) - see OptionalSettingsCodec.</summary>
    public OptionalSettingsEntry OptionalSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// QDC 1200 Setting record on the radio. UI/model only, see
    /// Qdc1200SettingsEntry's class doc comment.</summary>
    public Qdc1200SettingsEntry Qdc1200Settings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// 5Tone Settings record on the radio. UI/model only, see
    /// FiveToneSettingsEntry's class doc comment.</summary>
    public FiveToneSettingsEntry FiveToneSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// 2Tone Settings Encode-tab scalar-field record on the radio. UI/model
    /// only, see TwoToneEncodeSettingsEntry's class doc comment.</summary>
    public TwoToneEncodeSettingsEntry TwoToneEncodeSettings { get; } = new();

    /// <summary>Single instance, not a collection - there's only ever one
    /// DTMF Settings record on the radio. UI/model only, see
    /// DtmfSettingsEntry's class doc comment.</summary>
    public DtmfSettingsEntry DtmfSettings { get; } = new();

    public ObservableCollection<AprsReceiveFilterEntry> AprsReceiveFilters { get; } = [];

    /// <summary>Wired up from the main constructor in MainViewModel.cs.
    /// Keeps ContactOptions/RadioIdOptions/ScanListOptions/
    /// ReceiveGroupListOptions fresh as those lists change (same idea as
    /// OnEncryptionKeysChanged), AND - unlike the narrower option-refresh
    /// this replaced - marks the project dirty and re-validates on ANY
    /// change to these 7 collections or MasterId. Without this, adding/
    /// removing/editing these entities (including via a radio read) would
    /// silently NOT mark the project as having unsaved changes, risking
    /// data loss if the app were closed without a save prompt.</summary>
    private void WireCoreEntityOptionNotifications()
    {
        Talkgroups.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ContactOptions));
            RefreshReceiveGroupListMemberPicker();
        };
        RadioIds.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RadioIdOptions));
        ScanLists.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ScanListOptions));
        ReceiveGroupLists.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ReceiveGroupListOptions));
        FmChannels.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(OptionalSettingsFmChannelOptions));
            OnPropertyChanged(nameof(OptionalSettingsFmWorkChannelName));
        };
        AmZones.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(OptionalSettingsAmZoneOptions));
            OnPropertyChanged(nameof(OptionalSettingsAmWorkZoneName));
        };

        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        RadioIds.CollectionChanged += MarkDirtyAndRevalidate;
        Talkgroups.CollectionChanged += MarkDirtyAndRevalidate;
        ScanLists.CollectionChanged += MarkDirtyAndRevalidate;
        RoamingChannels.CollectionChanged += MarkDirtyAndRevalidate;
        RoamingZones.CollectionChanged += MarkDirtyAndRevalidate;
        ReceiveGroupLists.CollectionChanged += MarkDirtyAndRevalidate;
        AutoRepeaterOffsets.CollectionChanged += MarkDirtyAndRevalidate;
        AnalogAddresses.CollectionChanged += MarkDirtyAndRevalidate;
        GpsRoamingEntries.CollectionChanged += MarkDirtyAndRevalidate;
        TalkgroupWhitelist.CollectionChanged += MarkDirtyAndRevalidate;
        DigitalContactWhitelist.CollectionChanged += MarkDirtyAndRevalidate;
        PrefabricatedSmsMessages.CollectionChanged += MarkDirtyAndRevalidate;
        AmAirChannels.CollectionChanged += MarkDirtyAndRevalidate;
        AmZones.CollectionChanged += MarkDirtyAndRevalidate;
        FmChannels.CollectionChanged += MarkDirtyAndRevalidate;
        MasterId.PropertyChanged += MarkDirtyAndRevalidate;
        TalkAliasSettings.PropertyChanged += MarkDirtyAndRevalidate;
        AlarmSettings.PropertyChanged += MarkDirtyAndRevalidate;
        AlarmSettings.PropertyChanged += OnAlarmSettingsPropertyChanged;
        AprsSettings.PropertyChanged += MarkDirtyAndRevalidate;
        AprsReceiveFilters.CollectionChanged += MarkDirtyAndRevalidate;
        OptionalSettings.PropertyChanged += MarkDirtyAndRevalidate;
        OptionalSettings.PropertyChanged += OnOptionalSettingsPropertyChanged;
        Qdc1200Settings.PropertyChanged += MarkDirtyAndRevalidate;
        FiveToneSettings.PropertyChanged += MarkDirtyAndRevalidate;
        TwoToneEncodeSettings.PropertyChanged += MarkDirtyAndRevalidate;
        DtmfSettings.PropertyChanged += MarkDirtyAndRevalidate;

        // AlertTones is a fixed 25-entry sub-list on OptionalSettings (see
        // OptionalSettingsEntry's own doc comment) - never added/removed
        // after construction, so a one-time subscription here (rather than
        // a CollectionChanged handler) is enough to cover every entry for
        // the lifetime of this MainViewModel.
        foreach (var tone in OptionalSettings.AlertTones)
        {
            tone.PropertyChanged += MarkDirtyAndRevalidate;
        }
    }

    [ObservableProperty] private RadioIdEntry? _selectedRadioId;
    [ObservableProperty] private TalkgroupEntry? _selectedTalkgroup;
    [ObservableProperty] private ScanListEntry? _selectedScanList;
    [ObservableProperty] private RoamingChannelEntry? _selectedRoamingChannel;
    [ObservableProperty] private ChannelEntry? _roamingChannelFastSelect;
    [ObservableProperty] private RoamingZoneEntry? _selectedRoamingZone;
    [ObservableProperty] private ReceiveGroupListEntry? _selectedReceiveGroupList;
    [ObservableProperty] private AutoRepeaterOffsetEntry? _selectedAutoRepeaterOffset;
    [ObservableProperty] private AnalogAddressEntry? _selectedAnalogAddress;
    [ObservableProperty] private GpsRoamingEntry? _selectedGpsRoaming;
    [ObservableProperty] private TalkgroupWhitelistEntry? _selectedTalkgroupWhitelistEntry;
    [ObservableProperty] private DigitalContactWhitelistEntry? _selectedDigitalContactWhitelistEntry;
    [ObservableProperty] private AprsReceiveFilterEntry? _selectedAprsReceiveFilter;
    [ObservableProperty] private PrefabricatedSmsEntry? _selectedPrefabricatedSms;
    [ObservableProperty] private AmAirEntry? _selectedAmAir;
    [ObservableProperty] private AmZoneEntry? _selectedAmZone;
    [ObservableProperty] private FmChannelEntry? _selectedFmChannel;
    // Fixed 7/8-count sub-lists inside AprsSettings (see
    // AprsFixLocationEntry/AprsDigitalReportEntry's own class doc comments)
    // - no Add/Remove, same "fixed named set" idea as HotKey/StateInformation.
    [ObservableProperty] private AprsFixLocationEntry? _selectedFixLocation;
    [ObservableProperty] private AprsDigitalReportEntry? _selectedDigitalReport;

    public int RadioIdCount => RadioIds.Count;
    public int TalkgroupCount => Talkgroups.Count;
    public int ScanListCount => ScanLists.Count;
    public int RoamingChannelCount => RoamingChannels.Count;
    public int RoamingZoneCount => RoamingZones.Count;
    public int ReceiveGroupListCount => ReceiveGroupLists.Count;
    public int AutoRepeaterOffsetCount => AutoRepeaterOffsets.Count;
    public int AnalogAddressCount => AnalogAddresses.Count;
    public int GpsRoamingCount => GpsRoamingEntries.Count;
    public int TalkgroupWhitelistCount => TalkgroupWhitelist.Count;
    public int DigitalContactWhitelistCount => DigitalContactWhitelist.Count;
    public int AprsReceiveFilterCount => AprsReceiveFilters.Count;
    public int PrefabricatedSmsCount => PrefabricatedSmsMessages.Count;
    public int AmAirCount => AmAirChannels.Count;
    public int AmZoneCount => AmZones.Count;
    public int FmChannelCount => FmChannels.Count;

    [RelayCommand]
    private void AddRadioId()
    {
        if (RadioIds.Count >= CodeplugLimits.RadioIdListMax)
        {
            StatusMessage = $"Cannot add Radio ID: the radio only has {CodeplugLimits.RadioIdListMax} slots.";
            return;
        }

        var number = NextNumber(RadioIds.Select(e => e.Number));
        var entry = new RadioIdEntry { Number = number, Name = $"Radio {number}" };
        RadioIds.Add(entry);
        SelectedRadioId = entry;
        OnPropertyChanged(nameof(RadioIdCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedRadioId))]
    private void RemoveRadioId()
    {
        if (SelectedRadioId is null)
        {
            return;
        }

        var removedIndex = SelectedRadioId.Number - 1;
        RadioIds.Remove(SelectedRadioId);
        _pendingDeleteRadioIdIndices.Add(removedIndex);
        SelectedRadioId = RadioIds.FirstOrDefault();
        OnPropertyChanged(nameof(RadioIdCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedRadioId() => SelectedRadioId is not null;

    [RelayCommand]
    private void AddTalkgroup()
    {
        if (Talkgroups.Count >= CodeplugLimits.TalkgroupListMax)
        {
            StatusMessage = $"Cannot add Talkgroup: the radio only has {CodeplugLimits.TalkgroupListMax} slots.";
            return;
        }

        var number = NextNumber(Talkgroups.Select(e => e.Number));
        var entry = new TalkgroupEntry { Number = number, Name = $"TG {number}" };
        Talkgroups.Add(entry);
        SelectedTalkgroup = entry;
        OnPropertyChanged(nameof(TalkgroupCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedTalkgroup))]
    private void RemoveTalkgroup()
    {
        if (SelectedTalkgroup is null)
        {
            return;
        }

        var removedIndex = SelectedTalkgroup.Number - 1;
        Talkgroups.Remove(SelectedTalkgroup);
        _pendingDeleteTalkgroupIndices.Add(removedIndex);
        SelectedTalkgroup = Talkgroups.FirstOrDefault();
        OnPropertyChanged(nameof(TalkgroupCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedTalkgroup() => SelectedTalkgroup is not null;

    [RelayCommand]
    private void AddScanList()
    {
        if (ScanLists.Count >= CodeplugLimits.ScanListMax)
        {
            StatusMessage = $"Cannot add scan list: AnyTone lists support max {CodeplugLimits.ScanListMax} scan lists.";
            return;
        }

        var number = NextNumber(ScanLists.Select(e => e.Number));
        var entry = new ScanListEntry { Number = number, Name = $"Scan List {number}" };
        ScanLists.Add(entry);
        SelectedScanList = entry;
        OnPropertyChanged(nameof(ScanListCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedScanList))]
    private void RemoveScanList()
    {
        if (SelectedScanList is null)
        {
            return;
        }

        var removedIndex = SelectedScanList.Number - 1;
        ScanLists.Remove(SelectedScanList);
        // See _pendingDeleteScanListRadioIndices's doc comment - without
        // this, a delete never actually reaches the radio (the same gap
        // Channel/Zone deletion had before each was fixed).
        _pendingDeleteScanListRadioIndices.Add(removedIndex);
        SelectedScanList = ScanLists.FirstOrDefault();
        OnPropertyChanged(nameof(ScanListCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedScanList() => SelectedScanList is not null;

    [RelayCommand]
    private void AddRoamingChannel()
    {
        if (RoamingChannels.Count >= CodeplugLimits.RoamingChannelMax)
        {
            StatusMessage = $"Cannot add Roaming Channel: the radio only has {CodeplugLimits.RoamingChannelMax} slots.";
            return;
        }

        var number = NextNumber(RoamingChannels.Select(e => e.Number));
        var entry = new RoamingChannelEntry { Number = number, Name = $"Roam CH {number}" };
        RoamingChannels.Add(entry);
        SelectedRoamingChannel = entry;
        OnPropertyChanged(nameof(RoamingChannelCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedRoamingChannel))]
    private void RemoveRoamingChannel()
    {
        if (SelectedRoamingChannel is null)
        {
            return;
        }

        var removed = SelectedRoamingChannel;
        var removedIndex = removed.Number - 1;

        // A deleted roaming channel must not leave dangling membership
        // behind in any Roaming Zone - same reasoning as RemoveChannel's
        // own Zone/ScanList cleanup.
        foreach (var roamingZone in RoamingZones)
        {
            while (roamingZone.Members.Remove(removed))
            {
            }
        }

        RoamingChannels.Remove(removed);
        _pendingDeleteRoamingChannelIndices.Add(removedIndex);
        SelectedRoamingChannel = RoamingChannels.FirstOrDefault();
        OnPropertyChanged(nameof(RoamingChannelCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedRoamingChannel() => SelectedRoamingChannel is not null;

    /// <summary>"Fast select" combobox in the Roaming Channel detail panel -
    /// the vendor CPS's own popup offers a picker listing repeater-style
    /// channels (confirmed 2026-08-07 on a real test radio: its 3 analog
    /// repeaters showed up there, not its many simplex channels).
    /// Approximated here as "any channel whose RX and TX differ" (a
    /// repeater has a duplex offset; a simplex channel doesn't) since the
    /// vendor CPS's own exact filter rule isn't independently confirmed -
    /// only copies Name/RX/TX (not Color Code/Slot, which aren't
    /// meaningful on an analog source channel).</summary>
    public IReadOnlyList<ChannelEntry> RoamingChannelFastSelectOptions =>
        Channels.Where(c => Math.Abs(c.RxFrequencyMHz - c.ComputeTransmitFrequencyMHz()) > 0.000001).ToList();

    partial void OnRoamingChannelFastSelectChanged(ChannelEntry? value)
    {
        if (value is null || SelectedRoamingChannel is null)
        {
            return;
        }

        SelectedRoamingChannel.Name = value.Name;
        SelectedRoamingChannel.RxFrequencyMhz = value.RxFrequencyMHz;
        SelectedRoamingChannel.TxFrequencyMhz = value.ComputeTransmitFrequencyMHz();

        // One-shot action, not a durable link - reset back to the
        // placeholder so picking the same channel again still fires.
        RoamingChannelFastSelect = null;
    }

    [RelayCommand]
    private void AddRoamingZone()
    {
        if (RoamingZones.Count >= CodeplugLimits.RoamingZoneMax)
        {
            StatusMessage = $"Cannot add Roaming Zone: the radio only has {CodeplugLimits.RoamingZoneMax} slots.";
            return;
        }

        var number = NextNumber(RoamingZones.Select(e => e.Number));
        var entry = new RoamingZoneEntry { Number = number, Name = $"Roam Zone {number}" };
        RoamingZones.Add(entry);
        SelectedRoamingZone = entry;
        OnPropertyChanged(nameof(RoamingZoneCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedRoamingZone))]
    private void RemoveRoamingZone()
    {
        if (SelectedRoamingZone is null)
        {
            return;
        }

        var removedIndex = SelectedRoamingZone.Number - 1;
        RoamingZones.Remove(SelectedRoamingZone);
        _pendingDeleteRoamingZoneIndices.Add(removedIndex);
        SelectedRoamingZone = RoamingZones.FirstOrDefault();
        OnPropertyChanged(nameof(RoamingZoneCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedRoamingZone() => SelectedRoamingZone is not null;

    [RelayCommand]
    private void AddReceiveGroupList()
    {
        if (ReceiveGroupLists.Count >= CodeplugLimits.ReceiveGroupListMax)
        {
            StatusMessage = $"Cannot add Receive Group List: the radio only has {CodeplugLimits.ReceiveGroupListMax} slots.";
            return;
        }

        var number = NextNumber(ReceiveGroupLists.Select(e => e.Number));
        var entry = new ReceiveGroupListEntry { Number = number, Name = $"RX Group {number}" };
        ReceiveGroupLists.Add(entry);
        SelectedReceiveGroupList = entry;
        OnPropertyChanged(nameof(ReceiveGroupListCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedReceiveGroupList))]
    private void RemoveReceiveGroupList()
    {
        if (SelectedReceiveGroupList is null)
        {
            return;
        }

        var removedIndex = SelectedReceiveGroupList.Number - 1;
        ReceiveGroupLists.Remove(SelectedReceiveGroupList);
        // See _pendingDeleteReceiveGroupListIndices's doc comment - without
        // this, a delete never actually reaches the radio (same gap
        // Channel/Zone/Talkgroup deletion had before each was fixed).
        _pendingDeleteReceiveGroupListIndices.Add(removedIndex);
        SelectedReceiveGroupList = ReceiveGroupLists.FirstOrDefault();
        OnPropertyChanged(nameof(ReceiveGroupListCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedReceiveGroupList() => SelectedReceiveGroupList is not null;

    /// <summary>Talkgroups already a member of <see cref="SelectedReceiveGroupList"/>,
    /// in the same ascending-index order the radio itself stores them in -
    /// confirmed via live capture 2026-08-08 (see ReceiveGroupListEntry's
    /// own TalkgroupIndexes doc comment).</summary>
    public ObservableCollection<TalkgroupEntry> ReceiveGroupListMemberTalkgroups { get; } = [];

    /// <summary>Talkgroups NOT yet a member of <see cref="SelectedReceiveGroupList"/> -
    /// the "Available" side of the two-list member picker, matching the
    /// vendor CPS's own "Available Receive Group Call Contact"/"Receive
    /// Group Call List Member" pair (same >>/&lt;&lt; shape as the Scan List
    /// channel-member picker - see RefreshAvailableScanListChannels).</summary>
    public ObservableCollection<TalkgroupEntry> AvailableReceiveGroupListTalkgroups { get; } = [];

    public ObservableCollection<TalkgroupEntry> SelectedAvailableReceiveGroupListTalkgroups { get; } = [];
    public ObservableCollection<TalkgroupEntry> SelectedReceiveGroupListMemberTalkgroupsSelection { get; } = [];

    public void SetSelectedAvailableReceiveGroupListTalkgroups(IEnumerable<TalkgroupEntry> talkgroups)
    {
        ReplaceSelection(SelectedAvailableReceiveGroupListTalkgroups, talkgroups);
        AddReceiveGroupListMembersCommand.NotifyCanExecuteChanged();
    }

    public void SetSelectedReceiveGroupListMemberTalkgroups(IEnumerable<TalkgroupEntry> talkgroups)
    {
        ReplaceSelection(SelectedReceiveGroupListMemberTalkgroupsSelection, talkgroups);
        RemoveReceiveGroupListMembersCommand.NotifyCanExecuteChanged();
    }

    private void RefreshReceiveGroupListMemberPicker()
    {
        ReceiveGroupListMemberTalkgroups.Clear();
        AvailableReceiveGroupListTalkgroups.Clear();

        if (SelectedReceiveGroupList is null)
        {
            return;
        }

        foreach (var index in SelectedReceiveGroupList.TalkgroupIndexes.OrderBy(idx => idx))
        {
            var talkgroup = Talkgroups.FirstOrDefault(t => t.Number - 1 == index);
            if (talkgroup is not null)
            {
                ReceiveGroupListMemberTalkgroups.Add(talkgroup);
            }
        }

        foreach (var talkgroup in Talkgroups.Where(t => !SelectedReceiveGroupList.TalkgroupIndexes.Contains((long)(t.Number - 1))))
        {
            AvailableReceiveGroupListTalkgroups.Add(talkgroup);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddReceiveGroupListMembers))]
    private void AddReceiveGroupListMembers()
    {
        if (SelectedReceiveGroupList is null || SelectedAvailableReceiveGroupListTalkgroups.Count == 0)
        {
            return;
        }

        var receiveGroupList = SelectedReceiveGroupList;
        var added = 0;
        foreach (var talkgroup in SelectedAvailableReceiveGroupListTalkgroups.ToList())
        {
            if (receiveGroupList.TalkgroupIndexes.Count >= CodeplugLimits.ReceiveGroupListMemberMax)
            {
                StatusMessage = $"Cannot add every selected talkgroup: a Receive Group List can hold at most {CodeplugLimits.ReceiveGroupListMemberMax} talkgroups.";
                break;
            }

            var radioIndex = (long)(talkgroup.Number - 1);
            if (receiveGroupList.TalkgroupIndexes.Contains(radioIndex))
            {
                continue;
            }

            // Insert in ascending order - matches the order the radio
            // itself stores members in (see TalkgroupIndexes's own doc
            // comment), so the member list always previews in the same
            // order it'll be written/read back in.
            var insertAt = 0;
            while (insertAt < receiveGroupList.TalkgroupIndexes.Count && receiveGroupList.TalkgroupIndexes[insertAt] < radioIndex)
            {
                insertAt++;
            }

            receiveGroupList.TalkgroupIndexes.Insert(insertAt, radioIndex);
            added++;
        }

        SetSelectedAvailableReceiveGroupListTalkgroups([]);
        RefreshReceiveGroupListMemberPicker();
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        StatusMessage = added == 1 ? "Receive group list member added" : $"{added} receive group list members added";
    }

    private bool CanAddReceiveGroupListMembers() => SelectedReceiveGroupList is not null && SelectedAvailableReceiveGroupListTalkgroups.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveReceiveGroupListMembers))]
    private void RemoveReceiveGroupListMembers()
    {
        if (SelectedReceiveGroupList is null || SelectedReceiveGroupListMemberTalkgroupsSelection.Count == 0)
        {
            return;
        }

        var receiveGroupList = SelectedReceiveGroupList;
        var removed = SelectedReceiveGroupListMemberTalkgroupsSelection.ToList();
        foreach (var talkgroup in removed)
        {
            receiveGroupList.TalkgroupIndexes.Remove((long)(talkgroup.Number - 1));
        }

        SetSelectedReceiveGroupListMemberTalkgroups([]);
        RefreshReceiveGroupListMemberPicker();
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
        StatusMessage = removed.Count == 1 ? "Receive group list member removed" : $"{removed.Count} receive group list members removed";
    }

    private bool CanRemoveReceiveGroupListMembers() => SelectedReceiveGroupList is not null && SelectedReceiveGroupListMemberTalkgroupsSelection.Count > 0;

    [RelayCommand]
    private void AddAutoRepeaterOffset()
    {
        if (AutoRepeaterOffsets.Count >= CodeplugLimits.AutoRepeaterOffsetMax)
        {
            StatusMessage = $"Cannot add Auto Repeater Offset: the radio only has {CodeplugLimits.AutoRepeaterOffsetMax} slots.";
            return;
        }

        var number = NextNumber(AutoRepeaterOffsets.Select(e => e.Number));
        var entry = new AutoRepeaterOffsetEntry { Number = number };
        AutoRepeaterOffsets.Add(entry);
        SelectedAutoRepeaterOffset = entry;
        OnPropertyChanged(nameof(AutoRepeaterOffsetCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAutoRepeaterOffset))]
    private void RemoveAutoRepeaterOffset()
    {
        if (SelectedAutoRepeaterOffset is null)
        {
            return;
        }

        var removedIndex = SelectedAutoRepeaterOffset.Number - 1;
        AutoRepeaterOffsets.Remove(SelectedAutoRepeaterOffset);
        // See _pendingDeleteAutoRepeaterOffsetIndices's doc comment - without
        // this, a delete never actually reaches the radio (same gap Channel/
        // Zone/Scan List/AM Air deletion had before each was fixed).
        _pendingDeleteAutoRepeaterOffsetIndices.Add(removedIndex);
        SelectedAutoRepeaterOffset = AutoRepeaterOffsets.FirstOrDefault();
        OnPropertyChanged(nameof(AutoRepeaterOffsetCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedAutoRepeaterOffset() => SelectedAutoRepeaterOffset is not null;

    [RelayCommand]
    private void AddAnalogAddress()
    {
        if (AnalogAddresses.Count >= CodeplugLimits.AnalogAddressMax)
        {
            StatusMessage = $"Cannot add Analog Address: the radio only has {CodeplugLimits.AnalogAddressMax} slots.";
            return;
        }

        var number = NextNumber(AnalogAddresses.Select(e => e.Number));
        var entry = new AnalogAddressEntry { Number = number, Name = $"Contact {number}" };
        AnalogAddresses.Add(entry);
        SelectedAnalogAddress = entry;
        OnPropertyChanged(nameof(AnalogAddressCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAnalogAddress))]
    private void RemoveAnalogAddress()
    {
        if (SelectedAnalogAddress is null)
        {
            return;
        }

        var removedIndex = SelectedAnalogAddress.Number - 1;
        AnalogAddresses.Remove(SelectedAnalogAddress);
        // See _pendingDeleteAnalogAddressRadioIndices' doc comment - without
        // this, a delete never actually reaches the radio (same gap every
        // other entity's deletion had before each was fixed).
        _pendingDeleteAnalogAddressRadioIndices.Add(removedIndex);
        SelectedAnalogAddress = AnalogAddresses.FirstOrDefault();
        OnPropertyChanged(nameof(AnalogAddressCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedAnalogAddress() => SelectedAnalogAddress is not null;

    /// <summary>All 32 slots are physically fixed on the radio (see
    /// GpsRoamingEntry's own doc comment) - unlike every variable-length
    /// list elsewhere in this app, there is no Add/Remove, only resetting a
    /// slot back to its Off/default state, matching EncryptionKeyEntry's
    /// own "Remove just resets to Off" precedent.</summary>
    [RelayCommand(CanExecute = nameof(CanResetSelectedGpsRoaming))]
    private void ResetGpsRoaming()
    {
        if (SelectedGpsRoaming is null)
        {
            return;
        }

        SelectedGpsRoaming.Enabled = false;
        SelectedGpsRoaming.ZoneIndex = 255;
        SelectedGpsRoaming.ZoneDisplayName = "Off";
        SelectedGpsRoaming.LatDegree = 0;
        SelectedGpsRoaming.LatMinute = 0;
        SelectedGpsRoaming.LatMinuteDecimal = 0;
        SelectedGpsRoaming.NorthSouth = 0;
        SelectedGpsRoaming.LongDegree = 0;
        SelectedGpsRoaming.LongMinute = 0;
        SelectedGpsRoaming.LongMinuteDecimal = 0;
        SelectedGpsRoaming.EastWest = 0;
        SelectedGpsRoaming.Radius = 0;
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanResetSelectedGpsRoaming() => SelectedGpsRoaming is not null;

    /// <summary>Backfills any missing slot 1-32 with an Off default -
    /// called after a live Read and after a project load, matching
    /// EnsureEncryptionKeySlotsPresent's own reasoning (a slot the radio
    /// read never populated, or an older project file saved before this
    /// entity had full write support, must still show all 32 fixed rows).
    /// A backfilled placeholder is a UI stand-in, not something the user
    /// asked to write, so it's marked synced immediately.</summary>
    private void EnsureGpsRoamingSlotsPresent()
    {
        var byNumber = GpsRoamingEntries.ToDictionary(e => e.Number);
        for (var number = 1; number <= GpsRoamingCodec.EntryCount; number++)
        {
            if (byNumber.ContainsKey(number))
            {
                continue;
            }

            var entry = new GpsRoamingEntry { Number = number };
            entry.MarkRadioSynced();
            GpsRoamingEntries.Add(entry);
        }

        OnPropertyChanged(nameof(GpsRoamingCount));
    }

    /// <summary>Renumbers 1..N in list order - see TalkgroupWhitelistEntry's
    /// own doc comment: the radio packs entries by position regardless of
    /// row number, so Number is display-only and must stay in sync with
    /// position after every add/remove, not user-editable.</summary>
    private static void RenumberSequentially(IEnumerable<TalkgroupWhitelistEntry> entries)
    {
        var i = 1;
        foreach (var entry in entries)
        {
            entry.Number = i++;
        }
    }

    private static void RenumberSequentially(IEnumerable<DigitalContactWhitelistEntry> entries)
    {
        var i = 1;
        foreach (var entry in entries)
        {
            entry.Number = i++;
        }
    }

    [RelayCommand]
    private void AddTalkgroupWhitelistEntry()
    {
        if (TalkgroupWhitelist.Count >= CodeplugLimits.WhitelistSlotMax)
        {
            StatusMessage = $"Cannot add Talkgroup Whitelist entry: the radio only has {CodeplugLimits.WhitelistSlotMax} slots.";
            return;
        }

        var entry = new TalkgroupWhitelistEntry { Number = TalkgroupWhitelist.Count + 1 };
        TalkgroupWhitelist.Add(entry);
        SelectedTalkgroupWhitelistEntry = entry;
        OnPropertyChanged(nameof(TalkgroupWhitelistCount));
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedTalkgroupWhitelistEntry))]
    private void RemoveTalkgroupWhitelistEntry()
    {
        if (SelectedTalkgroupWhitelistEntry is null)
        {
            return;
        }

        TalkgroupWhitelist.Remove(SelectedTalkgroupWhitelistEntry);
        RenumberSequentially(TalkgroupWhitelist);
        SelectedTalkgroupWhitelistEntry = TalkgroupWhitelist.FirstOrDefault();
        OnPropertyChanged(nameof(TalkgroupWhitelistCount));
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedTalkgroupWhitelistEntry() => SelectedTalkgroupWhitelistEntry is not null;

    [RelayCommand]
    private void AddDigitalContactWhitelistEntry()
    {
        if (DigitalContactWhitelist.Count >= CodeplugLimits.WhitelistSlotMax)
        {
            StatusMessage = $"Cannot add Digital Contact Whitelist entry: the radio only has {CodeplugLimits.WhitelistSlotMax} slots.";
            return;
        }

        var entry = new DigitalContactWhitelistEntry { Number = DigitalContactWhitelist.Count + 1 };
        DigitalContactWhitelist.Add(entry);
        SelectedDigitalContactWhitelistEntry = entry;
        OnPropertyChanged(nameof(DigitalContactWhitelistCount));
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedDigitalContactWhitelistEntry))]
    private void RemoveDigitalContactWhitelistEntry()
    {
        if (SelectedDigitalContactWhitelistEntry is null)
        {
            return;
        }

        DigitalContactWhitelist.Remove(SelectedDigitalContactWhitelistEntry);
        RenumberSequentially(DigitalContactWhitelist);
        SelectedDigitalContactWhitelistEntry = DigitalContactWhitelist.FirstOrDefault();
        OnPropertyChanged(nameof(DigitalContactWhitelistCount));
        RefreshValidation();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedDigitalContactWhitelistEntry() => SelectedDigitalContactWhitelistEntry is not null;

    [RelayCommand]
    private void AddAprsReceiveFilter()
    {
        var number = NextNumber(AprsReceiveFilters.Select(e => e.Number));
        var entry = new AprsReceiveFilterEntry { Number = number };
        AprsReceiveFilters.Add(entry);
        SelectedAprsReceiveFilter = entry;
        OnPropertyChanged(nameof(AprsReceiveFilterCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAprsReceiveFilter))]
    private void RemoveAprsReceiveFilter()
    {
        if (SelectedAprsReceiveFilter is null)
        {
            return;
        }

        AprsReceiveFilters.Remove(SelectedAprsReceiveFilter);
        SelectedAprsReceiveFilter = AprsReceiveFilters.FirstOrDefault();
        OnPropertyChanged(nameof(AprsReceiveFilterCount));
        RefreshValidation();
    }

    private bool CanRemoveSelectedAprsReceiveFilter() => SelectedAprsReceiveFilter is not null;

    [RelayCommand]
    private void AddPrefabricatedSms()
    {
        if (PrefabricatedSmsMessages.Count >= PrefabricatedSmsCodec.SlotCount)
        {
            StatusMessage = $"Cannot add prefabricated SMS: the radio only has {PrefabricatedSmsCodec.SlotCount} slots.";
            return;
        }

        var number = NextNumber(PrefabricatedSmsMessages.Select(e => e.Number));
        var entry = new PrefabricatedSmsEntry { Number = number };
        PrefabricatedSmsMessages.Add(entry);
        SelectedPrefabricatedSms = entry;
        OnPropertyChanged(nameof(PrefabricatedSmsCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedPrefabricatedSms))]
    private void RemovePrefabricatedSms()
    {
        if (SelectedPrefabricatedSms is null)
        {
            return;
        }

        var removedIndex = SelectedPrefabricatedSms.Number - 1;
        PrefabricatedSmsMessages.Remove(SelectedPrefabricatedSms);
        // See _pendingDeletePrefabricatedSmsIndices's doc comment - without
        // this, a delete never actually reaches the radio (same gap every
        // other entity's deletion had before each was fixed). Note the
        // shared used-slot chain gets rewritten from current state
        // regardless (see PrefabricatedSmsCodec's doc comment), so this
        // mainly matters for making sure the deleted slot's own text record
        // gets blanked too.
        _pendingDeletePrefabricatedSmsIndices.Add(removedIndex);
        SelectedPrefabricatedSms = PrefabricatedSmsMessages.FirstOrDefault();
        OnPropertyChanged(nameof(PrefabricatedSmsCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedPrefabricatedSms() => SelectedPrefabricatedSms is not null;

    [RelayCommand]
    private void AddAmAir()
    {
        if (AmAirChannels.Count >= CodeplugLimits.AmAirMax)
        {
            StatusMessage = $"Cannot add AM Air channel: AnyTone lists support max {CodeplugLimits.AmAirMax} AM Air channels.";
            return;
        }

        var number = NextNumber(AmAirChannels.Select(e => e.Number));
        var entry = new AmAirEntry { Number = number, Name = $"AM {number}" };
        AmAirChannels.Add(entry);
        SelectedAmAir = entry;
        OnPropertyChanged(nameof(AmAirCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAmAir))]
    private void RemoveAmAir()
    {
        if (SelectedAmAir is null)
        {
            return;
        }

        var removed = SelectedAmAir;
        // An AM Zone that loses its last member is removed entirely, same
        // convention as regular Zone (see ReassignZoneChannels' doc
        // comment) - collect first, remove after the loop since AmZones
        // itself can't be mutated mid-iteration.
        var emptiedAmZones = new List<AmZoneEntry>();
        foreach (var amZone in AmZones)
        {
            while (amZone.Members.Remove(removed))
            {
            }

            if (amZone.Members.Count == 0)
            {
                emptiedAmZones.Add(amZone);
            }
            else
            {
                ReassignAmZoneChannel(amZone);
            }
        }

        foreach (var amZone in emptiedAmZones)
        {
            RemoveAmZoneInternal(amZone);
        }

        var removedIndex = SelectedAmAir.Number - 1;
        AmAirChannels.Remove(SelectedAmAir);
        // See _pendingDeleteAmAirRadioIndices's doc comment - without this,
        // a delete never actually reaches the radio (same gap Channel/Zone/
        // Scan List deletion had before each was fixed).
        _pendingDeleteAmAirRadioIndices.Add(removedIndex);
        SelectedAmAir = AmAirChannels.FirstOrDefault();
        OnPropertyChanged(nameof(AmAirCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedAmAir() => SelectedAmAir is not null;

    [RelayCommand]
    private void AddAmZone()
    {
        if (AmZones.Count >= CodeplugLimits.AmZoneMax)
        {
            StatusMessage = $"Cannot add AM Zone: the radio only has {CodeplugLimits.AmZoneMax} zone slots.";
            return;
        }

        var number = NextNumber(AmZones.Select(e => e.Number));
        var entry = new AmZoneEntry { Number = number, Name = $"AM Zone {number}" };
        AmZones.Add(entry);
        SelectedAmZone = entry;
        OnPropertyChanged(nameof(AmZoneCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAmZone))]
    private void RemoveAmZone()
    {
        if (SelectedAmZone is null)
        {
            return;
        }

        RemoveAmZoneInternal(SelectedAmZone);
        OnPropertyChanged(nameof(AmZoneCount));
        RefreshValidation();
    }

    private bool CanRemoveSelectedAmZone() => SelectedAmZone is not null;

    [RelayCommand]
    private void AddFmChannel()
    {
        if (FmChannels.Count >= CodeplugLimits.FmChannelMax)
        {
            StatusMessage = $"Cannot add FM channel: the radio only has {CodeplugLimits.FmChannelMax} FM channel slots.";
            return;
        }

        var number = NextNumber(FmChannels.Select(e => e.Number));
        var entry = new FmChannelEntry { Number = number, Name = $"FM {number}", ScanAdd = true };
        FmChannels.Add(entry);
        SelectedFmChannel = entry;
        OnPropertyChanged(nameof(FmChannelCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFmChannel))]
    private void RemoveFmChannel()
    {
        if (SelectedFmChannel is null)
        {
            return;
        }

        var removedIndex = SelectedFmChannel.Number - 1;
        FmChannels.Remove(SelectedFmChannel);
        // See _pendingDeleteFmChannelRadioIndices's doc comment - without
        // this, a delete never actually reaches the radio (same gap
        // Channel/Zone/Scan List/AM Air deletion had before each was fixed).
        _pendingDeleteFmChannelRadioIndices.Add(removedIndex);
        SelectedFmChannel = FmChannels.FirstOrDefault();
        OnPropertyChanged(nameof(FmChannelCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedFmChannel() => SelectedFmChannel is not null;

    partial void OnSelectedRadioIdChanged(RadioIdEntry? value) => RemoveRadioIdCommand.NotifyCanExecuteChanged();
    partial void OnSelectedTalkgroupChanged(TalkgroupEntry? value) => RemoveTalkgroupCommand.NotifyCanExecuteChanged();
    partial void OnSelectedScanListChanged(ScanListEntry? value)
    {
        RemoveScanListCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableScanListChannels([]);
        SetSelectedScanListMemberChannels([]);
        RefreshAvailableScanListChannels();
        OnPropertyChanged(nameof(SelectedScanListMemberOptions));
        AddScanListMembersCommand.NotifyCanExecuteChanged();
        RemoveScanListMembersCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedRoamingChannelChanged(RoamingChannelEntry? value) => RemoveRoamingChannelCommand.NotifyCanExecuteChanged();
    partial void OnSelectedRoamingZoneChanged(RoamingZoneEntry? value)
    {
        SelectedRoamingZoneMember = value?.Members.FirstOrDefault();
        RemoveRoamingZoneCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableRoamingZoneChannels([]);
        SetSelectedRoamingZoneMembers([]);
        RefreshAvailableRoamingZoneChannels();
        AddRoamingZoneMembersCommand.NotifyCanExecuteChanged();
        RemoveRoamingZoneMembersCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRoamingZoneMemberChanged(RoamingChannelEntry? value)
    {
        RemoveRoamingZoneMembersCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberUpCommand.NotifyCanExecuteChanged();
        MoveRoamingZoneMemberDownCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedReceiveGroupListChanged(ReceiveGroupListEntry? value)
    {
        RemoveReceiveGroupListCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableReceiveGroupListTalkgroups([]);
        SetSelectedReceiveGroupListMemberTalkgroups([]);
        RefreshReceiveGroupListMemberPicker();
    }
    partial void OnSelectedAutoRepeaterOffsetChanged(AutoRepeaterOffsetEntry? value) => RemoveAutoRepeaterOffsetCommand.NotifyCanExecuteChanged();
    partial void OnSelectedAnalogAddressChanged(AnalogAddressEntry? value) => RemoveAnalogAddressCommand.NotifyCanExecuteChanged();
    partial void OnSelectedGpsRoamingChanged(GpsRoamingEntry? value) => ResetGpsRoamingCommand.NotifyCanExecuteChanged();
    partial void OnSelectedTalkgroupWhitelistEntryChanged(TalkgroupWhitelistEntry? value) => RemoveTalkgroupWhitelistEntryCommand.NotifyCanExecuteChanged();
    partial void OnSelectedDigitalContactWhitelistEntryChanged(DigitalContactWhitelistEntry? value) => RemoveDigitalContactWhitelistEntryCommand.NotifyCanExecuteChanged();
    partial void OnSelectedAprsReceiveFilterChanged(AprsReceiveFilterEntry? value) => RemoveAprsReceiveFilterCommand.NotifyCanExecuteChanged();
    partial void OnSelectedPrefabricatedSmsChanged(PrefabricatedSmsEntry? value) => RemovePrefabricatedSmsCommand.NotifyCanExecuteChanged();
    partial void OnSelectedAmAirChanged(AmAirEntry? value) => RemoveAmAirCommand.NotifyCanExecuteChanged();
    partial void OnSelectedAmZoneChanged(AmZoneEntry? value)
    {
        RemoveAmZoneCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableAmZoneChannels([]);
        SetSelectedAmZoneMembers([]);
        RefreshAvailableAmZoneChannels();
        OnPropertyChanged(nameof(SelectedAmZoneMemberOptions));
        AddAmZoneMembersCommand.NotifyCanExecuteChanged();
        RemoveAmZoneMembersCommand.NotifyCanExecuteChanged();
        SetSelectedAvailableAmZoneScanChannels([]);
        SetSelectedAmZoneScanChannelMembers([]);
        RefreshAvailableAmZoneScanChannels();
        AddAmZoneScanChannelMembersCommand.NotifyCanExecuteChanged();
        RemoveAmZoneScanChannelMembersCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedFmChannelChanged(FmChannelEntry? value) => RemoveFmChannelCommand.NotifyCanExecuteChanged();

    private static int NextNumber(System.Collections.Generic.IEnumerable<int> existingNumbers)
    {
        var numbers = existingNumbers.ToList();
        return numbers.Count == 0 ? 1 : numbers.Max() + 1;
    }
}
