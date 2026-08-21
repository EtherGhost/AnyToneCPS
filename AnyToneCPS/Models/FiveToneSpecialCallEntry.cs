using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// The persisted result of a 5Tone Settings "Special Call" popup - shared
/// shape across all 3 trigger points (the ID table's own per-row
/// "&amp;Special Call", and the PTT ID Starting(BOT)/Ending(EOT)
/// "Special Call" buttons, which lack the row-level popup's own "Choose
/// Encoding Group NO." field - that field picks WHICH row a row-level
/// popup targets, not a value stored here, so it's not part of this
/// class). Confirmed field shape 2026-08-05.
///
/// <see cref="IsConfigured"/> is true once the popup's own OK button has
/// actually been used - distinguishes "never touched" from "explicitly
/// set to Send Message/etc." (mirrors HotKeyEntry's own Off-vs-untouched
/// distinction).
///
/// Deliberately a PURE data holder - no "compose the owning Encode ID
/// field" logic lives here, even though that's what the real vendor CPS
/// does once OK is pressed. Found out the hard way 2026-08-05: the
/// row-level ID table's own composition (confirmed for THAT popup
/// specifically: what's entered in this popup sets the Encode ID text
/// shown in the row's own grid - Send Message -&gt; OtherSideId + "Information:"
/// + Message; ANI -&gt; OtherSideId + Interval Character; PTTID -&gt; empty
/// and disabled) is NOT the same formula the BOT/EOT popups use - real
/// hex examples from BOT/EOT showed ANI repeating OtherSideId on both
/// sides of the Interval Character, and PTTID producing "E6"+OtherSideId
/// (NOT empty) - so this is genuinely 2 different formulas depending on
/// WHICH popup, not one shared rule. FiveToneIdEntry owns the row-level
/// formula (the only one confirmed enough to implement); BOT/EOT's own
/// formula is still pending more real examples (also unknown: whether
/// any of this depends on the selected Decode/Encode Standard - an open
/// question as of the same day).
/// </summary>
public partial class FiveToneSpecialCallEntry : ObservableObject
{
    public const byte CallingTypeSendMessage = 0;
    public const byte CallingTypeAni = 1;
    public const byte CallingTypePttId = 2;

    public static IReadOnlyList<string> CallingTypeOptions { get; } = ["Send Message", "ANI", "PTTID"];

    /// <summary>"No stop" is index 0 - ANI-only field, see
    /// <see cref="IsAni"/>.</summary>
    public static IReadOnlyList<string> IntervalCharacterOptions { get; } = ["No stop", "A", "B", "C", "D", "E", "F"];

    [ObservableProperty] private byte _callingType;

    /// <summary>Confirmed 2026-08-05, corrected the same day: must
    /// be EXACTLY as long as the current Self ID - not merely "no longer
    /// than" (see MainViewModel.FiveToneOtherSideIdMaxLength). Enforced
    /// in the Desktop popup dialog (`AvaloniaStoragePickerService.
    /// ShowFiveToneSpecialCallDialogAsync`'s own OK handler - too-long is
    /// already impossible via MaxLength, too-short is blocked with an
    /// inline error). NOT enforced on Mobile's inline per-row Other Side
    /// ID field (static MaxLength=7, no submit step to hang a "too
    /// short" check off of - documented gap, see that field's own
    /// tooltip).</summary>
    [ObservableProperty] private string _otherSideId = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private byte _intervalCharacter;
    [ObservableProperty] private bool _isConfigured;

    public bool IsSendMessage => CallingType == CallingTypeSendMessage;
    public bool IsAni => CallingType == CallingTypeAni;
    public bool IsPttId => CallingType == CallingTypePttId;

    public string CallingTypeText
    {
        get => CallingTypeOptions[CallingType];
        set
        {
            var index = CallingTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                CallingType = (byte)index;
            }
        }
    }

    public string IntervalCharacterText
    {
        get => IntervalCharacterOptions[IntervalCharacter];
        set
        {
            var index = IntervalCharacterOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                IntervalCharacter = (byte)index;
            }
        }
    }

    public void CopyFrom(FiveToneSpecialCallEntry other)
    {
        CallingType = other.CallingType;
        OtherSideId = other.OtherSideId;
        Message = other.Message;
        IntervalCharacter = other.IntervalCharacter;
        IsConfigured = other.IsConfigured;
    }

    /// <summary>Clears back to "never configured" - used by the ID
    /// table's own double-click-to-reset gesture (confirmed
    /// 2026-08-05: double-clicking a row that's been set by &amp;Special
    /// Call asks "Reset special call of this channel, ok or no?"). The
    /// caller is responsible for also clearing the owning Encode ID field
    /// back to empty - this class doesn't own that field.</summary>
    public void Reset()
    {
        CallingType = 0;
        OtherSideId = "";
        Message = "";
        IntervalCharacter = 0;
        IsConfigured = false;
    }

    partial void OnCallingTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(CallingTypeText));
        OnPropertyChanged(nameof(IsSendMessage));
        OnPropertyChanged(nameof(IsAni));
        OnPropertyChanged(nameof(IsPttId));
    }

    partial void OnIntervalCharacterChanged(byte value) => OnPropertyChanged(nameof(IntervalCharacterText));
}
