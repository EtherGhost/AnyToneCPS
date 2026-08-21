using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>Mirrors ZoneEntry's shape (Name/AChannel/Members with full
/// dirty-tracking), just without BChannel/IsHidden, plus a second member
/// list (ScanChannelMembers) for the separate "Zone Scan Channel Member"
/// field - confirmed 2026-08-02 directly from the vendor CPS screenshot and
/// dialog.</summary>
public partial class AmZoneEntry : ObservableObject
{
    private AmZoneSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio" split
    /// as ZoneEntry's own <c>_radioSyncSnapshot</c>. Only set by
    /// <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private AmZoneSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private AmAirEntry? _aChannel;

    public ObservableCollection<AmAirEntry> Members { get; } = [];

    /// <summary>"Zone Scan Channel Member" - a separate list from
    /// <see cref="Members"/>, encoded as a 128-bit bitmask rather than an
    /// index list (see AmZoneCodec's doc comment) - can only reference AM
    /// Air channels with Number 1-128 (radio index 0-127), a real hardware
    /// limitation on this field specifically.</summary>
    public ObservableCollection<AmAirEntry> ScanChannelMembers { get; } = [];

    public string DisplayLabel => $"{Number:00}  {Name}";

    public bool IsDirty => _cleanSnapshot is null || IsNameDirty || IsAChannelDirty || IsMembersDirty || IsScanChannelMembersDirty;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public bool IsAChannelDirty => _cleanSnapshot is null || AChannel?.Number != _cleanSnapshot.AChannelNumber;
    public bool IsMembersDirty => _cleanSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_cleanSnapshot.MemberChannelNumbers);
    public bool IsScanChannelMembersDirty => _cleanSnapshot is null || !ScanChannelMembers.Select(channel => channel.Number).SequenceEqual(_cleanSnapshot.ScanChannelMemberNumbers);
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";
    public string AChannelFontWeight => IsAChannelDirty ? "Bold" : "Normal";
    public string MembersFontWeight => IsMembersDirty ? "Bold" : "Normal";
    public string ScanChannelMembersFontWeight => IsScanChannelMembersDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching Zone/Channel's
    // own pattern: Number determines which radio slot this zone lives in,
    // it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || IsNamePendingRadioWrite
        || IsAChannelPendingRadioWrite
        || IsMembersPendingRadioWrite
        || IsScanChannelMembersPendingRadioWrite;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsAChannelPendingRadioWrite => _radioSyncSnapshot is null || AChannel?.Number != _radioSyncSnapshot.AChannelNumber;
    public bool IsMembersPendingRadioWrite => _radioSyncSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_radioSyncSnapshot.MemberChannelNumbers);
    public bool IsScanChannelMembersPendingRadioWrite => _radioSyncSnapshot is null || !ScanChannelMembers.Select(channel => channel.Number).SequenceEqual(_radioSyncSnapshot.ScanChannelMemberNumbers);

    public AmZoneEntry()
    {
        Members.CollectionChanged += OnMembersCollectionChanged;
        ScanChannelMembers.CollectionChanged += OnScanChannelMembersCollectionChanged;
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

    private AmZoneSnapshot CreateSnapshot() => new(
        Name,
        AChannel?.Number,
        Members.Select(channel => channel.Number).ToArray(),
        ScanChannelMembers.Select(channel => channel.Number).ToArray());

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAChannelChanged(AmAirEntry? value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void OnMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void OnScanChannelMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(IsAChannelDirty));
        OnPropertyChanged(nameof(IsMembersDirty));
        OnPropertyChanged(nameof(IsScanChannelMembersDirty));
        OnPropertyChanged(nameof(NameFontWeight));
        OnPropertyChanged(nameof(AChannelFontWeight));
        OnPropertyChanged(nameof(MembersFontWeight));
        OnPropertyChanged(nameof(ScanChannelMembersFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsAChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsMembersPendingRadioWrite));
        OnPropertyChanged(nameof(IsScanChannelMembersPendingRadioWrite));
    }

    private sealed record AmZoneSnapshot(string Name, int? AChannelNumber, int[] MemberChannelNumbers, int[] ScanChannelMemberNumbers);
}
