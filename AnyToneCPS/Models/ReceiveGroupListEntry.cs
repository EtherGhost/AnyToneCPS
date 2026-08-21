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

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || Name != _radioSyncSnapshot.Name
        || !TalkgroupIndexes.SequenceEqual(_radioSyncSnapshot.TalkgroupIndexes);

    public ReceiveGroupListEntry()
    {
        TalkgroupIndexes.CollectionChanged += OnTalkgroupIndexesChanged;
    }

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private ReceiveGroupListSnapshot CreateRadioSnapshot() => new(Name, TalkgroupIndexes.ToArray());

    private void OnTalkgroupIndexesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    private sealed record ReceiveGroupListSnapshot(string Name, long[] TalkgroupIndexes);
}
