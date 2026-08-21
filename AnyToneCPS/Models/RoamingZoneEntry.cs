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
/// Only radio-write dirty tracking, no separate file-save dirty tracking -
/// matches RoamingChannelEntry's own established pattern (its closest
/// write-support sibling), not ZoneEntry's heavier one. Unlike
/// RoamingChannelEntry, dirtiness is checked field-by-field rather than via
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

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsNamePendingRadioWrite || IsMembersPendingRadioWrite;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsMembersPendingRadioWrite => _radioSyncSnapshot is null || !Members.Select(m => m.Number).SequenceEqual(_radioSyncSnapshot.MemberChannelNumbers);

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        NotifyPendingRadioWriteProperties();
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
    }

    private void OnMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyPendingRadioWriteProperties();

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsMembersPendingRadioWrite));
    }

    private sealed record RoamingZoneSnapshot(string Name, int[] MemberChannelNumbers);
}
