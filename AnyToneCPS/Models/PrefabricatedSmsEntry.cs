using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class PrefabricatedSmsEntry : ObservableObject
{
    private PrefabricatedSmsSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio" split
    /// as every other entity's own <c>_radioSyncSnapshot</c>. Only set by
    /// <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private PrefabricatedSmsSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _text = "";

    public bool IsDirty => _cleanSnapshot is null || IsTextDirty;
    public bool IsTextDirty => _cleanSnapshot is null || Text != _cleanSnapshot.Text;
    public string TextFontWeight => IsTextDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching every other
    // entity's own pattern: Number determines which radio slot this
    // message lives in, it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsTextPendingRadioWrite;
    public bool IsTextPendingRadioWrite => _radioSyncSnapshot is null || Text != _radioSyncSnapshot.Text;

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

    private PrefabricatedSmsSnapshot CreateSnapshot() => new(Text);

    partial void OnTextChanged(string value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsTextDirty));
        OnPropertyChanged(nameof(TextFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsTextPendingRadioWrite));
    }

    private sealed record PrefabricatedSmsSnapshot(string Text);
}
