using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class ScanListEntry : ObservableObject
{
    private ScanListSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// reasoning as ChannelEntry/ZoneEntry's own <c>_radioSyncSnapshot</c>:
    /// "unsaved to the project file" and "not yet written to the radio" are
    /// different concerns. Only set by <see cref="MarkRadioSynced"/>, never
    /// by <see cref="MarkClean"/>.</summary>
    private ScanListSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _priorityChannelSelect;
    [ObservableProperty] private ChannelEntry? _priorityChannel1;
    [ObservableProperty] private ChannelEntry? _priorityChannel2;
    [ObservableProperty] private int _lookbackTimeA;
    [ObservableProperty] private int _lookbackTimeB;
    [ObservableProperty] private int _dropoutDelayTime;
    [ObservableProperty] private int _dwellTime;
    [ObservableProperty] private int _revertChannel;

    /// <summary>Real source of truth for scan list membership, same
    /// object-reference model as Zone.Members (converted from raw channel
    /// indexes 2026-07-19, matching Zone's UI: two list boxes with Add/
    /// Remove buttons, rather than the channel editor's own one-list-only
    /// "SelectedChannelScanListName" convenience picker, which stays but no
    /// longer enforces single-list exclusivity - see that property's doc
    /// comment). A channel does NOT carry its own "which scan list am I in"
    /// field on the wire (see ChannelEntry's doc comment).</summary>
    public ObservableCollection<ChannelEntry> Members { get; } = [];

    // LookbackTimeA/B and DropoutDelayTime/DwellTime are stored as the raw
    // wire value (tenths of a second) - these 4 present/edit them as the
    // decimal-seconds value the vendor CPS itself displays (e.g. raw 25 =
    // "2.5"), confirmed 2026-07-19 via live differential test. Same
    // established pattern as ChannelEntry.CorrectFrequencyHzText.
    public string LookbackTimeAText
    {
        get => (LookbackTimeA / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseTenths(value, out var tenths))
            {
                LookbackTimeA = tenths;
            }
            else
            {
                OnPropertyChanged(nameof(LookbackTimeAText));
            }
        }
    }

    public string LookbackTimeBText
    {
        get => (LookbackTimeB / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseTenths(value, out var tenths))
            {
                LookbackTimeB = tenths;
            }
            else
            {
                OnPropertyChanged(nameof(LookbackTimeBText));
            }
        }
    }

    public string DropoutDelayTimeText
    {
        get => (DropoutDelayTime / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseTenths(value, out var tenths))
            {
                DropoutDelayTime = tenths;
            }
            else
            {
                OnPropertyChanged(nameof(DropoutDelayTimeText));
            }
        }
    }

    public string DwellTimeText
    {
        get => (DwellTime / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseTenths(value, out var tenths))
            {
                DwellTime = tenths;
            }
            else
            {
                OnPropertyChanged(nameof(DwellTimeText));
            }
        }
    }

    private static bool TryParseTenths(string value, out int tenths)
    {
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 6553.5)
        {
            tenths = (int)System.Math.Round(seconds * 10);
            return true;
        }

        tenths = 0;
        return false;
    }

    /// <summary>Confirmed 2026-07-19 directly from the vendor CPS help text
    /// (topic 64, "PriorityChannelSelect"): "Select the priority channel or
    /// off Priority. Can select Priority 1 or Priority 2 or Both" - a plain
    /// ascending 4-value enum, and the byte position/write-safety of index 2
    /// ("Priority 2") was independently confirmed by a live differential
    /// test the same day.</summary>
    public static IReadOnlyList<string> PriorityChannelSelectOptions { get; } = ["Off", "Priority 1", "Priority 2", "Both"];

    /// <summary>Corrected 2026-08-02 by reading the actual vendor CPS
    /// combobox item list directly (not just the help text, which was
    /// missing the 6th item) - these exact 6 labels, in this exact order and
    /// wording ("Priority Channel Select2", not "Priority Channel Select" -
    /// a real oddity in the vendor UI's own text, transcribed as-is). NOT a
    /// channel reference despite Field_Reference.md's "reference" type
    /// label, and NOT the reference project's own 4-value list
    /// (Constants::SCAN_LIST_REVERT_CHANNEL).</summary>
    public static IReadOnlyList<string> RevertChannelOptions { get; } =
    [
        "Selected",
        "Selected + TalkBack",
        "Priority Channel Select2",
        "Last Called",
        "Last Used",
        "Priority Channel Select2 + TalkBack"
    ];

    /// <summary>Legal value range for the 4 timer combo boxes - upper bound
    /// corrected 2026-08-02 to match the real vendor CPS combobox, which
    /// goes up to 5.0s inclusive (was previously capped at 4.9s). Wire
    /// encoding (raw tenths-of-a-second, no offset) confirmed 2026-07-19 via
    /// a live differential test - see LookbackTimeAText's doc comment.</summary>
    public static IReadOnlyList<string> LookBackTimeOptions { get; } = BuildTenthsRange(5, 50);

    public static IReadOnlyList<string> DropoutDelayDwellTimeOptions { get; } = BuildTenthsRange(1, 50);

    private static IReadOnlyList<string> BuildTenthsRange(int startTenths, int endTenthsInclusive)
    {
        var options = new List<string>(endTenthsInclusive - startTenths + 1);
        for (var tenths = startTenths; tenths <= endTenthsInclusive; tenths++)
        {
            options.Add((tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture));
        }

        return options;
    }

    /// <summary>String-based "None" + member-name picker, matching this
    /// app's established convention for optional references (Contact/
    /// RadioId/ReceiveGroupList on ChannelEntry) rather than binding a
    /// ComboBox directly to a nullable ChannelEntry - a real channel
    /// reference needs an explicit "None" entry (unlike Zone's A/B-Channel,
    /// which is confirmed to never be clearable), and this app doesn't have
    /// a null-friendly ComboBox item template. Resolves by DisplayLabel
    /// (Number+Name) against <see cref="Members"/> only, matching the real
    /// vendor CPS restricting these to the scan list's own member channels
    /// (confirmed 2026-07-19 via the reference project's own
    /// updatePriorityChannelList()).</summary>
    public string PriorityChannel1Text
    {
        get => PriorityChannel1?.DisplayLabel ?? "None";
        set => PriorityChannel1 = value == "None" ? null : Members.FirstOrDefault(m => m.DisplayLabel == value);
    }

    public string PriorityChannel2Text
    {
        get => PriorityChannel2?.DisplayLabel ?? "None";
        set => PriorityChannel2 = value == "None" ? null : Members.FirstOrDefault(m => m.DisplayLabel == value);
    }

    /// <summary>Row-scoped ItemsSource for the Priority Channel 1/2
    /// ComboBoxes - used by MobileMainView's flat per-row edit template,
    /// where the DataContext is this entry itself (not SelectedScanList as
    /// on Desktop, which instead uses MainViewModel.
    /// SelectedScanListMemberOptions - see that property's doc comment for
    /// why Desktop needs the indirection and Mobile doesn't).</summary>
    public IReadOnlyList<string> PriorityChannelOptions => new[] { "None" }.Concat(Members.Select(m => m.DisplayLabel)).ToList();

    /// <summary>Falls back to index 0 ("Off") for any value outside the 4
    /// known ones, same reasoning as RevertChannelText's doc comment - a
    /// raw-number fallback breaks ComboBox selection (shows blank) rather
    /// than a sensible default.</summary>
    public string PriorityChannelSelectText
    {
        get => PriorityChannelSelect >= 0 && PriorityChannelSelect < PriorityChannelSelectOptions.Count ? PriorityChannelSelectOptions[PriorityChannelSelect] : PriorityChannelSelectOptions[0];
        set
        {
            var index = PriorityChannelSelectOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                PriorityChannelSelect = index;
            }
        }
    }

    /// <summary>Falls back to index 0 ("Selected") for any value outside
    /// the 5 known ones, rather than printing the raw number - confirmed
    /// 2026-07-20 via a live radio read that a never-written scan list's
    /// RevertChannel byte is the 0xFF factory-default sentinel, and the
    /// real vendor CPS displays "Selected" (its first option) for it, not
    /// a raw number. Printing the raw number broke ComboBox selection
    /// entirely (SelectedItem showed blank, since "255" matches nothing in
    /// RevertChannelOptions).</summary>
    public string RevertChannelText
    {
        get => RevertChannel >= 0 && RevertChannel < RevertChannelOptions.Count ? RevertChannelOptions[RevertChannel] : RevertChannelOptions[0];
        set
        {
            var index = RevertChannelOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                RevertChannel = index;
            }
        }
    }

    public string DisplayLabel => $"{Number:00}  {Name}";

    public bool IsDirty => _cleanSnapshot is null
        || IsNameDirty
        || IsPriorityChannelSelectDirty
        || IsPriorityChannel1Dirty
        || IsPriorityChannel2Dirty
        || IsLookbackTimeADirty
        || IsLookbackTimeBDirty
        || IsDropoutDelayTimeDirty
        || IsDwellTimeDirty
        || IsRevertChannelDirty
        || IsMembersDirty;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public bool IsPriorityChannelSelectDirty => _cleanSnapshot is null || PriorityChannelSelect != _cleanSnapshot.PriorityChannelSelect;
    public bool IsPriorityChannel1Dirty => _cleanSnapshot is null || PriorityChannel1?.Number != _cleanSnapshot.PriorityChannel1Number;
    public bool IsPriorityChannel2Dirty => _cleanSnapshot is null || PriorityChannel2?.Number != _cleanSnapshot.PriorityChannel2Number;
    public bool IsLookbackTimeADirty => _cleanSnapshot is null || LookbackTimeA != _cleanSnapshot.LookbackTimeA;
    public bool IsLookbackTimeBDirty => _cleanSnapshot is null || LookbackTimeB != _cleanSnapshot.LookbackTimeB;
    public bool IsDropoutDelayTimeDirty => _cleanSnapshot is null || DropoutDelayTime != _cleanSnapshot.DropoutDelayTime;
    public bool IsDwellTimeDirty => _cleanSnapshot is null || DwellTime != _cleanSnapshot.DwellTime;
    public bool IsRevertChannelDirty => _cleanSnapshot is null || RevertChannel != _cleanSnapshot.RevertChannel;
    public bool IsMembersDirty => _cleanSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_cleanSnapshot.MemberChannelNumbers);
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";
    public string MembersFontWeight => IsMembersDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching Channel/Zone's
    // own pattern: Number determines which radio slot a scan list lives in,
    // it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || IsNamePendingRadioWrite
        || IsPriorityChannelSelectPendingRadioWrite
        || IsPriorityChannel1PendingRadioWrite
        || IsPriorityChannel2PendingRadioWrite
        || IsLookbackTimeAPendingRadioWrite
        || IsLookbackTimeBPendingRadioWrite
        || IsDropoutDelayTimePendingRadioWrite
        || IsDwellTimePendingRadioWrite
        || IsRevertChannelPendingRadioWrite
        || IsMembersPendingRadioWrite;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsPriorityChannelSelectPendingRadioWrite => _radioSyncSnapshot is null || PriorityChannelSelect != _radioSyncSnapshot.PriorityChannelSelect;
    public bool IsPriorityChannel1PendingRadioWrite => _radioSyncSnapshot is null || PriorityChannel1?.Number != _radioSyncSnapshot.PriorityChannel1Number;
    public bool IsPriorityChannel2PendingRadioWrite => _radioSyncSnapshot is null || PriorityChannel2?.Number != _radioSyncSnapshot.PriorityChannel2Number;
    public bool IsLookbackTimeAPendingRadioWrite => _radioSyncSnapshot is null || LookbackTimeA != _radioSyncSnapshot.LookbackTimeA;
    public bool IsLookbackTimeBPendingRadioWrite => _radioSyncSnapshot is null || LookbackTimeB != _radioSyncSnapshot.LookbackTimeB;
    public bool IsDropoutDelayTimePendingRadioWrite => _radioSyncSnapshot is null || DropoutDelayTime != _radioSyncSnapshot.DropoutDelayTime;
    public bool IsDwellTimePendingRadioWrite => _radioSyncSnapshot is null || DwellTime != _radioSyncSnapshot.DwellTime;
    public bool IsRevertChannelPendingRadioWrite => _radioSyncSnapshot is null || RevertChannel != _radioSyncSnapshot.RevertChannel;
    public bool IsMembersPendingRadioWrite => _radioSyncSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_radioSyncSnapshot.MemberChannelNumbers);

    public ScanListEntry()
    {
        Members.CollectionChanged += OnMembersCollectionChanged;
    }

    public void MarkClean()
    {
        _cleanSnapshot = CreateSnapshot();
        NotifyDirtyProperties();
    }

    /// <summary>Radio-write baseline only - deliberately separate from
    /// <see cref="MarkClean"/>, see <see cref="_radioSyncSnapshot"/>'s doc
    /// comment.</summary>
    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateSnapshot();
        NotifyPendingRadioWriteProperties();
    }

    private ScanListSnapshot CreateSnapshot() => new(
        Name,
        PriorityChannelSelect,
        PriorityChannel1?.Number,
        PriorityChannel2?.Number,
        LookbackTimeA,
        LookbackTimeB,
        DropoutDelayTime,
        DwellTime,
        RevertChannel,
        Members.Select(channel => channel.Number).ToArray());

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPriorityChannelSelectChanged(int value)
    {
        OnPropertyChanged(nameof(PriorityChannelSelectText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPriorityChannel1Changed(ChannelEntry? value)
    {
        OnPropertyChanged(nameof(PriorityChannel1Text));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPriorityChannel2Changed(ChannelEntry? value)
    {
        OnPropertyChanged(nameof(PriorityChannel2Text));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnLookbackTimeAChanged(int value)
    {
        OnPropertyChanged(nameof(LookbackTimeAText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnLookbackTimeBChanged(int value)
    {
        OnPropertyChanged(nameof(LookbackTimeBText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDropoutDelayTimeChanged(int value)
    {
        OnPropertyChanged(nameof(DropoutDelayTimeText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDwellTimeChanged(int value)
    {
        OnPropertyChanged(nameof(DwellTimeText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnRevertChannelChanged(int value)
    {
        OnPropertyChanged(nameof(RevertChannelText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void OnMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PriorityChannelOptions));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(IsPriorityChannelSelectDirty));
        OnPropertyChanged(nameof(IsPriorityChannel1Dirty));
        OnPropertyChanged(nameof(IsPriorityChannel2Dirty));
        OnPropertyChanged(nameof(IsLookbackTimeADirty));
        OnPropertyChanged(nameof(IsLookbackTimeBDirty));
        OnPropertyChanged(nameof(IsDropoutDelayTimeDirty));
        OnPropertyChanged(nameof(IsDwellTimeDirty));
        OnPropertyChanged(nameof(IsRevertChannelDirty));
        OnPropertyChanged(nameof(IsMembersDirty));
        OnPropertyChanged(nameof(NameFontWeight));
        OnPropertyChanged(nameof(MembersFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsPriorityChannelSelectPendingRadioWrite));
        OnPropertyChanged(nameof(IsPriorityChannel1PendingRadioWrite));
        OnPropertyChanged(nameof(IsPriorityChannel2PendingRadioWrite));
        OnPropertyChanged(nameof(IsLookbackTimeAPendingRadioWrite));
        OnPropertyChanged(nameof(IsLookbackTimeBPendingRadioWrite));
        OnPropertyChanged(nameof(IsDropoutDelayTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsDwellTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsRevertChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsMembersPendingRadioWrite));
    }

    private sealed record ScanListSnapshot(
        string Name,
        int PriorityChannelSelect,
        int? PriorityChannel1Number,
        int? PriorityChannel2Number,
        int LookbackTimeA,
        int LookbackTimeB,
        int DropoutDelayTime,
        int DwellTime,
        int RevertChannel,
        int[] MemberChannelNumbers);
}
