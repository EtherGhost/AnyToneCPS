using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class ReceiveGroupListEntry : ObservableObject
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";

    /// <summary>Talkgroup indexes (32-bit on the wire, hence long). Stored
    /// in ascending order - confirmed via live capture 2026-08-08 that the
    /// vendor CPS itself re-sorts member talkgroups by radio index rather
    /// than preserving add-order.</summary>
    public ObservableCollection<long> TalkgroupIndexes { get; } = [];

    /// <summary>Radio-write baseline only - see TalkgroupEntry's own doc
    /// comment for the split rationale. Deliberately excludes Number.</summary>
    private ReceiveGroupListSnapshot? _radioSyncSnapshot;

    /// <summary>Separate baseline from <see cref="_radioSyncSnapshot"/> -
    /// same "unsaved to the project file" vs "not yet written to the
    /// radio" split as AutoRepeaterOffsetEntry's own pair. Was missing
    /// entirely until found live 2026-10-02 (renaming a Receive Group List
    /// never enabled Save) - only set by <see cref="MarkClean"/>.</summary>
    private ReceiveGroupListSnapshot? _cleanSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || Name != _radioSyncSnapshot.Name
        || !TalkgroupIndexes.SequenceEqual(_radioSyncSnapshot.TalkgroupIndexes);

    public bool IsDirty => _cleanSnapshot is null
        || Name != _cleanSnapshot.Name
        || !TalkgroupIndexes.SequenceEqual(_cleanSnapshot.TalkgroupIndexes);

    public ReceiveGroupListEntry()
    {
        TalkgroupIndexes.CollectionChanged += OnTalkgroupIndexesChanged;
    }

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    public void MarkClean()
    {
        _cleanSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(IsDirty));
    }

    private ReceiveGroupListSnapshot CreateRadioSnapshot() => new(Name, TalkgroupIndexes.ToArray());

    private void OnTalkgroupIndexesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsDirty));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsDirty));
    }

    private sealed record ReceiveGroupListSnapshot(string Name, long[] TalkgroupIndexes);
}
