using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class AnalogAddressEntry : ObservableObject
{
    private AnalogAddressSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio"
    /// split as every other entity's own <c>_radioSyncSnapshot</c>. Only
    /// set by <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private AnalogAddressSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private long _addressNumber;
    [ObservableProperty] private string _name = "";

    public bool IsDirty => _cleanSnapshot is null || IsAddressNumberDirty || IsNameDirty;
    public bool IsAddressNumberDirty => _cleanSnapshot is null || AddressNumber != _cleanSnapshot.AddressNumber;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching Channel/
    // AM Air's own pattern: Number determines which radio slot this entry
    // lives in, it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsAddressNumberPendingRadioWrite || IsNamePendingRadioWrite;
    public bool IsAddressNumberPendingRadioWrite => _radioSyncSnapshot is null || AddressNumber != _radioSyncSnapshot.AddressNumber;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;

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

    private AnalogAddressSnapshot CreateSnapshot() => new(AddressNumber, Name);

    partial void OnAddressNumberChanged(long value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnNameChanged(string value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsAddressNumberDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(NameFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsAddressNumberPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
    }

    private sealed record AnalogAddressSnapshot(long AddressNumber, string Name);
}
