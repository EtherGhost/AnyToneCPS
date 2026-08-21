using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// DTMF Settings' Encode tab - a FIXED 16-slot M1-M16 list (not addable/
/// removable, same "fixed named set" convention as HotKeyEntry's own 18
/// keys), see CodeplugLimits.DtmfEncodeSlotCount. Confirmed field
/// shape 2026-08-06.
///
/// <see cref="Code"/> is the M-slot's own raw text (manual entry restricted
/// to 0-9/A-D in the view, MaxLength 16) - once &amp;Special Call is used
/// (<see cref="IsSpecialCallConfigured"/> true), it auto-composes instead as
/// <c>{OtherSideId}{DTMF Interval Character}{DTMF Self ID}</c> - confirmed
/// via 5 worked examples against the real vendor CPS (e.g. Self ID "002" +
/// Other Side ID "111" -&gt; "111*002"). Only ANI exists as a calling type here (see
/// the DTMF Special Call popup screenshot) - unlike 5Tone's own 3-type
/// popup, so no CallingType field is modeled.
///
/// Composition needs DTMF Settings' own Self ID/Interval Character (NOT
/// self-contained the way 5Tone's row-level composition is), so the actual
/// compose-on-change logic lives in MainViewModel.Dtmf.cs, which has
/// access to both this entry and DtmfSettingsEntry.
///
/// Full radio-write support added 2026-08-06, confirmed via 2 live
/// differential WRITE captures - see DtmfEncodeCodec's own doc comment for
/// the byte-level confirmation. Only <see cref="Code"/> is ever actually
/// stored on the wire - <see cref="OtherSideId"/>/<see
/// cref="IsSpecialCallConfigured"/> are pure UI/popup state (there's no way
/// to tell a composed Code from a manually-typed one on read, no marker
/// prefix the way 5Tone's own Send Message/PTTID formulas have), so the
/// radio-write snapshot deliberately excludes them.
/// </summary>
public partial class DtmfEncodeEntry : ObservableObject
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _otherSideId = "";
    [ObservableProperty] private bool _isSpecialCallConfigured;

    public string DisplayLabel => $"M{Number}";

    /// <summary>Radio-write baseline only - see FiveToneIdEntry's own doc
    /// comment for the split rationale. Deliberately excludes <see
    /// cref="Number"/> AND <see cref="OtherSideId"/>/<see
    /// cref="IsSpecialCallConfigured"/> (pure UI state, see class doc
    /// comment).</summary>
    private string? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || Code != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = Code;
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnCodeChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
