using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Full radio-write support added 2026-08-10 - see RoamingZoneCodec's own
/// doc comment for the live-capture confirmation. Members holds real
/// <see cref="RoamingChannelEntry"/> objects (not raw indices), same
/// reasoning as ZoneEntry.Members - the dual-list editing UI needs to
/// display Number/Name, and reordering the collection directly reorders
/// the on-radio slot order (confirmed NOT sorted by the live capture).
/// Radio-write AND file-save dirty tracking both present (the file-save
/// half - IsDirty/MarkClean - was missing entirely until a real bug found
/// live 2026-10-02: editing a Roaming Zone's own fields never enabled
/// Save). Unlike RoamingChannelEntry, dirtiness is checked field-by-field
/// rather than via
/// a single record equality check - a snapshot record's default equality
/// compares Members' backing array by reference, not content, which would
/// make HasAnyPendingRadioWrite always true after any Members edit even when
/// reverted - same reasoning as ZoneEntry's own IsMembersDirty.
/// </summary>
public partial class RoamingZoneEntry : ObservableObject
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";

    public ObservableCollection<RoamingChannelEntry> Members { get; } = [];
    public string DisplayLabel => $"{Number:00}  {Name}";

    private RoamingZoneSnapshot? _radioSyncSnapshot;

    /// <summary>Separate baseline from <see cref="_radioSyncSnapshot"/> -
    /// same "unsaved to the project file" vs "not yet written to the
    /// radio" split every other write-capable entity uses. Was missing
    /// entirely until found live 2026-10-02, same gap as
    /// ReceiveGroupListEntry/RoamingChannelEntry's own identical fix - this
    /// class's own doc comment above calling out "no separate file-save
    /// dirty tracking" as deliberate turned out to be the bug, not a real
    /// design choice.</summary>
    private RoamingZoneSnapshot? _cleanSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsNamePendingRadioWrite || IsMembersPendingRadioWrite;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsMembersPendingRadioWrite => _radioSyncSnapshot is null || !Members.Select(m => m.Number).SequenceEqual(_radioSyncSnapshot.MemberChannelNumbers);

    public bool IsDirty => _cleanSnapshot is null
        || Name != _cleanSnapshot.Name
        || !Members.Select(m => m.Number).SequenceEqual(_cleanSnapshot.MemberChannelNumbers);

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        NotifyPendingRadioWriteProperties();
    }

    public void MarkClean()
    {
        _cleanSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(IsDirty));
    }

    private RoamingZoneSnapshot CreateRadioSnapshot() => new(Name, Members.Select(m => m.Number).ToArray());

    public RoamingZoneEntry()
    {
        Members.CollectionChanged += OnMembersCollectionChanged;
    }

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyPendingRadioWriteProperties();
        OnPropertyChanged(nameof(IsDirty));
    }

    private void OnMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyPendingRadioWriteProperties();
        OnPropertyChanged(nameof(IsDirty));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsMembersPendingRadioWrite));
    }

    private sealed record RoamingZoneSnapshot(string Name, int[] MemberChannelNumbers);
}
