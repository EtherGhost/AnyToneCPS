using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class ZoneEntry : ObservableObject
{
    private ZoneSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// reasoning as ChannelEntry's own <c>_radioSyncSnapshot</c>: "unsaved to
    /// the project file" and "not yet written to the radio" are different
    /// concerns, so saving the project must never make Write-to-Radio forget
    /// a pending zone edit, and vice versa. Only set by <see cref="MarkRadioSynced"/>,
    /// never by <see cref="MarkClean"/>.</summary>
    private ZoneSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ChannelEntry? _aChannel;
    [ObservableProperty] private ChannelEntry? _bChannel;
    [ObservableProperty] private bool _isHidden;

    public ObservableCollection<ChannelEntry> Members { get; } = [];
    public string DisplayLabel => $"{Number:00}  {Name}";
    public bool IsDirty => _cleanSnapshot is null
        || IsNumberDirty
        || IsNameDirty
        || IsAChannelDirty
        || IsBChannelDirty
        || IsHiddenDirty
        || IsMembersDirty;
    public bool IsNumberDirty => _cleanSnapshot is null || Number != _cleanSnapshot.Number;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public bool IsAChannelDirty => _cleanSnapshot is null || AChannel?.Number != _cleanSnapshot.AChannelNumber;
    public bool IsBChannelDirty => _cleanSnapshot is null || BChannel?.Number != _cleanSnapshot.BChannelNumber;
    public bool IsHiddenDirty => _cleanSnapshot is null || IsHidden != _cleanSnapshot.IsHidden;
    public bool IsMembersDirty => _cleanSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_cleanSnapshot.MemberChannelNumbers);
    public string NumberFontWeight => IsNumberDirty ? "Bold" : "Normal";
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";
    public string AChannelFontWeight => IsAChannelDirty ? "Bold" : "Normal";
    public string BChannelFontWeight => IsBChannelDirty ? "Bold" : "Normal";
    public string IsHiddenFontWeight => IsHiddenDirty ? "Bold" : "Normal";
    public string MembersFontWeight => IsMembersDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching Channel's own
    // pattern: Number determines which radio slot a zone lives in, it isn't
    // itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || IsNamePendingRadioWrite
        || IsAChannelPendingRadioWrite
        || IsBChannelPendingRadioWrite
        || IsHiddenPendingRadioWrite
        || IsMembersPendingRadioWrite;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsAChannelPendingRadioWrite => _radioSyncSnapshot is null || AChannel?.Number != _radioSyncSnapshot.AChannelNumber;
    public bool IsBChannelPendingRadioWrite => _radioSyncSnapshot is null || BChannel?.Number != _radioSyncSnapshot.BChannelNumber;
    public bool IsHiddenPendingRadioWrite => _radioSyncSnapshot is null || IsHidden != _radioSyncSnapshot.IsHidden;
    public bool IsMembersPendingRadioWrite => _radioSyncSnapshot is null || !Members.Select(channel => channel.Number).SequenceEqual(_radioSyncSnapshot.MemberChannelNumbers);

    public ZoneEntry()
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

    private ZoneSnapshot CreateSnapshot() => new(
        Number,
        Name,
        AChannel?.Number,
        BChannel?.Number,
        IsHidden,
        Members.Select(channel => channel.Number).ToArray());

    partial void OnNumberChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAChannelChanged(ChannelEntry? value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnBChannelChanged(ChannelEntry? value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnIsHiddenChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void OnMembersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsNumberDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(IsAChannelDirty));
        OnPropertyChanged(nameof(IsBChannelDirty));
        OnPropertyChanged(nameof(IsHiddenDirty));
        OnPropertyChanged(nameof(IsMembersDirty));
        OnPropertyChanged(nameof(NumberFontWeight));
        OnPropertyChanged(nameof(NameFontWeight));
        OnPropertyChanged(nameof(AChannelFontWeight));
        OnPropertyChanged(nameof(BChannelFontWeight));
        OnPropertyChanged(nameof(IsHiddenFontWeight));
        OnPropertyChanged(nameof(MembersFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsAChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsBChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsHiddenPendingRadioWrite));
        OnPropertyChanged(nameof(IsMembersPendingRadioWrite));
    }

    private sealed record ZoneSnapshot(
        int Number,
        string Name,
        int? AChannelNumber,
        int? BChannelNumber,
        bool IsHidden,
        int[] MemberChannelNumbers);
}
