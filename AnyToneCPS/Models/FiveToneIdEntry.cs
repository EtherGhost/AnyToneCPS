using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// 5Tone Settings' ID table - fixed cap 100 (No. 1-100, see
/// CodeplugLimits.FiveToneIdMax), same Add/Remove-with-cap convention as
/// Analog Address Book/QDC Address Book. UI/model only, see
/// FiveToneSettingsEntry's own class doc comment.
///
/// <see cref="SpecialCall"/> holds what the row-level "&amp;Special Call"
/// popup sets (Choose Calling Type + its own dependent field group - see
/// FiveToneSpecialCallEntry's own class doc comment). The popup's own
/// "Choose Encoding Group NO." field (1-100) is NOT part of this class -
/// it lets the popup target ANY row, not just whichever one was selected
/// when it was opened (confirmed 2026-08-05) - the caller resolves
/// that to a specific FiveToneIdEntry (creating one if the chosen number
/// doesn't have a row yet) before writing into its own SpecialCall.
/// <see cref="Standard"/>/<see cref="TimeOfEncodeTone"/>/<see cref="Name"/>
/// are disabled until <see cref="FiveToneSpecialCallEntry.IsConfigured"/>
/// is true, matching the real vendor CPS's own "disabled if &amp;Special
/// call is empty" behavior. <see cref="EncodeId"/> auto-composes from
/// <see cref="SpecialCall"/> once configured, and is disabled for PTTID
/// specifically - confirmed 2026-08-05, describing THIS popup
/// specifically ("What is entered in this popup, sets Encode id text in
/// 'datagrid'"): Send Message -&gt; "{OtherSideId} Information:{Message}"
/// (a SPACE before "Information:", confirmed after an initial miss -
/// e.g. "12345" + "MYMESSAGE" -&gt; "12345 Information:MYMESSAGE"); ANI -&gt; OtherSideId +
/// the single Interval Character letter, or just OtherSideId alone for
/// "No stop" (e.g. "12345"+"A" -&gt; "12345A", "12345"+"No stop" -&gt;
/// "12345"); PTTID -&gt; empty, with Encode ID disabled. This is NOT the
/// same formula the BOT/EOT "Special Call" popups use for their own
/// Encode ID fields - see FiveToneSpecialCallEntry's own class doc
/// comment for why this composition logic deliberately lives HERE and
/// not on the shared SpecialCall class. Whether any of this depends on
/// the selected Decode/Encode Standard is untested (an open question as
/// of the same day) - assumed Standard-independent for now.
/// The constructor subscription below keeps EncodeId correct not just
/// for the Desktop popup's own explicit OK step, but also Mobile's
/// inline editing, which has no such step.
/// </summary>
public partial class FiveToneIdEntry : ObservableObject
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _encodeId = "";
    [ObservableProperty] private byte _standard;
    [ObservableProperty] private int _timeOfEncodeTone = 30;
    [ObservableProperty] private string _name = "";

    // --- Information ID / Information Code Function1 - moved here from
    // FiveToneSettingsEntry 2026-08-06. Confirmed: "Information ID
    // NO." isn't a free 1-16 choice, it's a selector that picks WHICH
    // existing row's own Function Option/Function Decoding Response/
    // Information ID/Function Name to view and edit - so those 4 fields
    // are genuinely per-row data, not a shared singleton (the earlier
    // model, where they lived on FiveToneSettingsEntry, meant switching
    // "Information ID NO." never actually changed what was displayed -
    // a real bug caught live). "Information ID NO." itself
    // stays on FiveToneSettingsEntry (it's the selector, not per-row
    // data) - see MainViewModel.FiveTone.cs's own SelectedInfoIdRow for
    // how the selector resolves to one of these rows.
    [ObservableProperty] private byte _functionOption;
    [ObservableProperty] private byte _functionDecodingResponse;
    [ObservableProperty] private string _informationId = "";
    [ObservableProperty] private string _functionName = "";

    public FiveToneSpecialCallEntry SpecialCall { get; } = new();

    /// <summary>Radio-write baseline only - separate from any project-file
    /// "dirty" tracking, same split every other radio-write-capable entity
    /// in this app uses. Only set by <see cref="MarkRadioSynced"/>.
    /// Deliberately excludes <see cref="Number"/> (determines which radio
    /// slot this entry lives in, not itself an encoded field, same
    /// convention as Qdc1200IdEntry/AnalogAddressEntry).</summary>
    private FiveToneIdSnapshot? _radioSyncSnapshot;

    /// <summary>Single aggregate flag rather than per-field
    /// IsXPendingRadioWrite booleans - this view has no per-field "pending
    /// write" UI indicator to drive, same reasoning as Qdc1200IdEntry.</summary>
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private FiveToneIdSnapshot CreateRadioSnapshot() => new(
        Standard, TimeOfEncodeTone, Name, EncodeId,
        SpecialCall.CallingType, SpecialCall.OtherSideId, SpecialCall.Message, SpecialCall.IntervalCharacter, SpecialCall.IsConfigured,
        FunctionOption, FunctionDecodingResponse, InformationId, FunctionName);

    private sealed record FiveToneIdSnapshot(
        byte Standard, int TimeOfEncodeTone, string Name, string EncodeId,
        byte CallingType, string OtherSideId, string Message, byte IntervalCharacter, bool IsConfigured,
        byte FunctionOption, byte FunctionDecodingResponse, string InformationId, string FunctionName);

    public FiveToneIdEntry()
    {
        SpecialCall.PropertyChanged += (_, _) =>
        {
            if (SpecialCall.IsConfigured)
            {
                EncodeId = ComposeEncodeIdFromSpecialCall();
            }

            OnPropertyChanged(nameof(IsEncodeIdEnabled));
            OnPropertyChanged(nameof(IsEncodeIdHexOnly));
            OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        };
    }

    /// <summary>Row-level-specific composition - see this class's own
    /// doc comment for the confirmed formula and why it's not shared with
    /// BOT/EOT.</summary>
    private string ComposeEncodeIdFromSpecialCall()
    {
        if (SpecialCall.IsSendMessage)
        {
            return SpecialCall.OtherSideId + " Information:" + SpecialCall.Message;
        }

        if (SpecialCall.IsAni)
        {
            var intervalSuffix = SpecialCall.IntervalCharacter == 0 ? "" : FiveToneSpecialCallEntry.IntervalCharacterOptions[SpecialCall.IntervalCharacter];
            return SpecialCall.OtherSideId + intervalSuffix;
        }

        // PTTID
        return "";
    }

    /// <summary>Matches the real vendor CPS: the Encode ID box starts out
    /// hand-editable (free text/hex) and stays that way until &amp;Special
    /// Call is used ONCE, at which point it goes read-only regardless of
    /// which Calling Type was chosen - not just PTTID, corrected 2026-08-06
    /// from an earlier, narrower reading of "PTTID=&gt;Empty and disabled"
    /// (true, but PTTID isn't the only Calling Type that locks the field -
    /// Send Message/ANI populate it AND lock it too). The only way back to
    /// editable is the Reset Special Call button/double-click gesture,
    /// matching vendor CPS's own double-click-to-reset behavior.</summary>
    public bool IsEncodeIdEnabled => !SpecialCall.IsConfigured;

    /// <summary>The Send Message formula above embeds literal text
    /// (" Information:" plus an arbitrary ASCII Message) - not hex, unlike
    /// every other state this field can be in (manual entry, ANI, or
    /// disabled-for-PTTID). The Encode ID TextBox's hex-only keystroke
    /// filter needs to turn off specifically for a configured Send
    /// Message row, otherwise a user hand-editing the auto-composed value
    /// couldn't type the space/colon/message characters it actually
    /// contains. Found 2026-08-06 while auditing this view's input
    /// restrictions before calling it finished.</summary>
    public bool IsEncodeIdHexOnly => !(SpecialCall.IsConfigured && SpecialCall.IsSendMessage);

    public static IReadOnlyList<string> StandardOptions => FiveToneSettingsEntry.DecodeStandardOptions;
    public static IReadOnlyList<string> TimeOfEncodeToneOptions => FiveToneSettingsEntry.TimeOfEncodeToneOptions;

    public string StandardText
    {
        get => StandardOptions[Standard];
        set
        {
            var index = StandardOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Standard = (byte)index;
            }
        }
    }

    public string TimeOfEncodeToneText
    {
        get => TimeOfEncodeTone.ToString();
        set
        {
            if (int.TryParse(value, out var parsed) && parsed is >= 30 and <= 100)
            {
                TimeOfEncodeTone = parsed;
            }
        }
    }

    public string DisplayLabel => $"{Number}  {Name}";

    public static IReadOnlyList<string> FunctionOptionOptions { get; } =
        ["Squelch Off", "Call all", "Emergency alarm", "Remotely kill", "Remotely stun", "Remotely wake up", "MSG group"];

    /// <summary>Function Decoding Response's own option list depends on
    /// Function Option - "Call all" drops "Beep tone &amp; Respond", and
    /// every other Function Option disables the field entirely (see
    /// <see cref="IsFunctionDecodingResponseEnabled"/>).</summary>
    public static IReadOnlyList<string> FunctionDecodingResponseSquelchOffOptions { get; } = ["None", "Beep tone", "Beep tone & Respond"];
    public static IReadOnlyList<string> FunctionDecodingResponseCallAllOptions { get; } = ["None", "Beep tone"];

    public string FunctionOptionText
    {
        get => FunctionOptionOptions[FunctionOption];
        set
        {
            var index = FunctionOptionOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                FunctionOption = (byte)index;
            }
        }
    }

    /// <summary>Enabled only for Function Option = Squelch Off or Call
    /// all - every other Function Option disables it entirely (confirmed
    /// against the real vendor CPS).</summary>
    public bool IsFunctionDecodingResponseEnabled => FunctionOption is 0 or 1;

    /// <summary>Disabled specifically for Function Option = Squelch Off
    /// or MSG group (confirmed 2026-08-06 via live vendor CPS
    /// testing) - every other Function Option (Call all/Emergency
    /// alarm/Remotely kill/Remotely stun/Remotely wake up) leaves it
    /// enabled. Opposite shape from <see cref="IsFunctionDecodingResponseEnabled"/>:
    /// the two fields don't share the same enable rule.</summary>
    public bool IsFunctionNameEnabled => FunctionOption is not (0 or 6);

    public IReadOnlyList<string> FunctionDecodingResponseOptions => FunctionOption switch
    {
        0 => FunctionDecodingResponseSquelchOffOptions,
        1 => FunctionDecodingResponseCallAllOptions,
        _ => []
    };

    public string FunctionDecodingResponseText
    {
        get => IsFunctionDecodingResponseEnabled && FunctionDecodingResponse < FunctionDecodingResponseOptions.Count
            ? FunctionDecodingResponseOptions[FunctionDecodingResponse]
            : "";
        set
        {
            var index = FunctionDecodingResponseOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                FunctionDecodingResponse = (byte)index;
            }
        }
    }

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnStandardChanged(byte value)
    {
        OnPropertyChanged(nameof(StandardText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTimeOfEncodeToneChanged(int value)
    {
        OnPropertyChanged(nameof(TimeOfEncodeToneText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnEncodeIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnInformationIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    /// <summary>Switching Function Option invalidates any previously
    /// selected Function Decoding Response - matches the real vendor CPS
    /// resetting dependent fields on a type change (same convention as
    /// AnalogQuickCallEntry.OnOperationTypeChanged, and the identical
    /// logic this replaced on FiveToneSettingsEntry before the 2026-08-06
    /// per-row move).</summary>
    partial void OnFunctionOptionChanged(byte value)
    {
        FunctionDecodingResponse = 0;
        OnPropertyChanged(nameof(FunctionOptionText));
        OnPropertyChanged(nameof(IsFunctionDecodingResponseEnabled));
        OnPropertyChanged(nameof(FunctionDecodingResponseOptions));
        OnPropertyChanged(nameof(FunctionDecodingResponseText));
        OnPropertyChanged(nameof(IsFunctionNameEnabled));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFunctionDecodingResponseChanged(byte value)
    {
        OnPropertyChanged(nameof(FunctionDecodingResponseText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFunctionNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
