using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Hot Key &gt; State Information - a fixed 32-slot list (No. 1-32, see
/// CodeplugLimits.StateInformationMax), each slot a single free-text
/// content string. User-specified field shape 2026-08-04: max 32
/// characters, plain ASCII only. Character-set restriction is enforced the
/// same way as every other Name/label field app-wide - the
/// Views.AsciiTextInput keystroke filter in XAML, not model-level
/// validation - since it already blocks the character before it's ever
/// typed or pasted (see AsciiTextInput's own doc comment: this radio's
/// screen can't render most non-ASCII characters correctly). Full
/// radio-write support added 2026-08-04 (see StateInformationCodec's own
/// doc comment).
/// </summary>
public partial class StateInformationEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - see AnalogQuickCallEntry's own
    /// doc comment for the split rationale. Deliberately excludes
    /// <see cref="Number"/>.</summary>
    private string? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || Content != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = Content;
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _content = "";

    public string DisplayLabel => $"{Number}  {Content}";

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
