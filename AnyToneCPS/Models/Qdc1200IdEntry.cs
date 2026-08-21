using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// QDC 1200 Setting &gt; Encode tab's ID table - fixed cap 100 (No. 1-100),
/// same Add/Remove-with-cap convention as Analog Quick Call/State
/// Information (see CodeplugLimits.Qdc1200IdMax) - not a fixed named list
/// like Hot Key's own 18 rows.
///
/// The full field spec was captured 2026-08-04 from live vendor CPS
/// inspection, confirmed via two live differential WRITE captures the same
/// day (see Qdc1200IdCodec's doc comment for the exact bytes):
///
/// - Call Type (byte 1: 0=Private, 1=Group Call, 2=All Call unconfirmed).
/// - Private Call ID (bytes 6-7) and Group Call ID (bytes 4-5) are
///   SEPARATE, INDEPENDENT byte slots, confirmed NOT cleared on the wire
///   when Call Type switches away from them (a stale "5564" Private Call
///   ID survived a write that changed Call Type to Group and set a
///   different Group Call ID) - this class still resets them in the UI on
///   a Call Type change for a clean editing experience, a deliberate UI
///   choice, not a wire requirement.
/// - Type (byte 0) is a single ABSOLUTE/shared code across BOTH Call
///   Types, NOT a fresh 0-based index per option list as first assumed -
///   "ALEART" read back as the same raw byte (2) whether Call Type was
///   Private or Group, even though it sits at a different position in
///   each list. The two option lists are different visible SUBSETS of one
///   shared code space, not independently numbered. Only ALEART=2 is
///   directly confirmed; the rest of the absolute numbering below assumes
///   Private's own display order is the underlying enum, unconfirmed.
/// - Need to Answer (byte 2) is enabled ONLY for Private Call's ALEART/
///   Remotely Moniton - confirmed DISABLED for Group Call even when Type
///   is ALEART too (observed directly while setting up the
///   second capture), correcting this class's first assumption that any
///   Call Type's ALEART/Remotely Moniton enabled it.
/// - Name (byte 8, UTF-16LE, 12 chars/24 bytes) - unaffected by either
///   capture, decodes/encodes like every other name field.
/// Full radio-write support added the same day, once the byte layout was
/// confirmed - see Qdc1200IdCodec's own doc comment for the Encode side.
/// </summary>
public partial class Qdc1200IdEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - separate from any project-file
    /// "dirty" tracking (this entity has none of its own), same split
    /// every other radio-write-capable entity's own
    /// <c>_radioSyncSnapshot</c> uses. Only set by <see cref="MarkRadioSynced"/>.
    /// Deliberately excludes <see cref="Number"/> - it determines which
    /// radio slot this entry lives in, it isn't itself an encoded field
    /// (same convention as AnalogAddressEntry/AmAirEntry).</summary>
    private Qdc1200IdSnapshot? _radioSyncSnapshot;

    /// <summary>Single aggregate flag rather than per-field
    /// IsXPendingRadioWrite booleans - this view has no per-field "pending
    /// write" UI indicator to drive, same reasoning as
    /// AlarmSettingsEntry/Qdc1200SettingsEntry.</summary>
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private Qdc1200IdSnapshot CreateRadioSnapshot() => new(CallType, PrivateCallId, GroupCallId, Type, NeedToAnswer, Name);

    private sealed record Qdc1200IdSnapshot(byte CallType, string PrivateCallId, string GroupCallId, byte Type, bool NeedToAnswer, string Name);

    public const byte TypeSelCal = 0;
    public const byte TypeCheck = 1;
    public const byte TypeAleart = 2;
    public const byte TypeRemotelyKill = 3;
    public const byte TypeWake = 4;
    public const byte TypeAlarm = 5;
    public const byte TypeRemotelyMoniton = 6;

    /// <summary>Absolute Type code -&gt; user-provided label, not
    /// independently verified against the real vendor CPS text beyond
    /// ALEART=2 - see this class's own doc comment.</summary>
    private static readonly IReadOnlyDictionary<byte, string> AbsoluteTypeLabels = new Dictionary<byte, string>
    {
        [TypeSelCal] = "SEL CAL",
        [TypeCheck] = "CHECK",
        [TypeAleart] = "ALEART",
        [TypeRemotelyKill] = "Remotely kill",
        [TypeWake] = "WAKE",
        [TypeAlarm] = "ALARM",
        [TypeRemotelyMoniton] = "Remotely Moniton"
    };

    public static IReadOnlyList<string> CallTypeOptions { get; } = ["Private Call", "Group Call", "All Call"];

    public static IReadOnlyList<string> PrivateTypeOptions { get; } =
        ["SEL CAL", "CHECK", "ALEART", "Remotely kill", "WAKE", "ALARM", "Remotely Moniton"];

    public static IReadOnlyList<string> GroupTypeOptions { get; } = ["SEL CAL", "ALEART", "ALARM"];

    public static IReadOnlyList<string> NeedToAnswerOptions { get; } = ["Off", "On"];

    [ObservableProperty] private int _number;
    [ObservableProperty] private byte _callType;
    [ObservableProperty] private string _privateCallId = "";
    [ObservableProperty] private string _groupCallId = "";
    [ObservableProperty] private byte _type = TypeSelCal;
    [ObservableProperty] private bool _needToAnswer;
    [ObservableProperty] private string _name = "";

    public bool IsPrivateCallIdEnabled => CallType == 0;
    public bool IsGroupCallIdEnabled => CallType == 1;
    public bool IsTypeEnabled => CallType is 0 or 1;

    public string CallTypeText
    {
        get => CallTypeOptions[CallType];
        set
        {
            var index = CallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                CallType = (byte)index;
            }
        }
    }

    /// <summary>The labels offered for <see cref="Type"/> - a genuinely
    /// different SUBSET per Call Type, not a differently-indexed list (see
    /// this class's own doc comment) - <see cref="Type"/> itself is always
    /// an absolute code from the same shared space.</summary>
    public IReadOnlyList<string> TypeOptions => CallType switch
    {
        0 => PrivateTypeOptions,
        1 => GroupTypeOptions,
        _ => []
    };

    public string TypeText
    {
        get => IsTypeEnabled && AbsoluteTypeLabels.TryGetValue(Type, out var label) && TypeOptions.Contains(label) ? label : "";
        set
        {
            foreach (var (code, label) in AbsoluteTypeLabels)
            {
                if (label == value)
                {
                    Type = code;
                    return;
                }
            }
        }
    }

    /// <summary>Need to Answer is enabled ONLY for Private Call's ALEART/
    /// Remotely Moniton - confirmed live that Group Call's own ALEART does
    /// NOT enable it, even though the label is identical (see this class's
    /// own doc comment).</summary>
    public bool IsNeedToAnswerEnabled => CallType == 0 && Type is TypeAleart or TypeRemotelyMoniton;

    public string NeedToAnswerText
    {
        get => NeedToAnswerOptions[NeedToAnswer ? 1 : 0];
        set => NeedToAnswer = value == "On";
    }

    public string DisplayLabel => $"{Number}  {Name}";

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnCallTypeChanged(byte value)
    {
        // Private Call ID/Group Call ID and Type are deliberately NOT
        // reset here - confirmed live that the real wire keeps them
        // independent/unchanged across a Call Type switch (see this
        // class's own doc comment). Only Need to Answer actually gets
        // cleared when it becomes disabled, matching what was directly
        // observed.
        OnPropertyChanged(nameof(IsPrivateCallIdEnabled));
        OnPropertyChanged(nameof(IsGroupCallIdEnabled));
        OnPropertyChanged(nameof(IsTypeEnabled));
        OnPropertyChanged(nameof(TypeOptions));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(CallTypeText));
        RefreshNeedToAnswerEnabled();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(TypeText));
        RefreshNeedToAnswerEnabled();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private void RefreshNeedToAnswerEnabled()
    {
        if (!IsNeedToAnswerEnabled)
        {
            NeedToAnswer = false;
        }

        OnPropertyChanged(nameof(IsNeedToAnswerEnabled));
    }

    partial void OnNeedToAnswerChanged(bool value)
    {
        OnPropertyChanged(nameof(NeedToAnswerText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPrivateCallIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnGroupCallIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
