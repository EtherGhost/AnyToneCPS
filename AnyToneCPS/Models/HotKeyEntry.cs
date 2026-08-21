using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Hot Key &gt; Hot Key tab - a fixed 18-row list (CodeplugLimits.HotKeyKeyCount),
/// one row per physical/programmable key (Hot Key 1-6, Fun Key+0-9, Fun
/// Key+*, Fun Key+#ish - see <see cref="KeyNames"/>). Not addable/removable,
/// unlike Analog Quick Call/State Information.
///
/// The full field spec was captured 2026-08-04 from live vendor CPS
/// inspection: Mode (Call/Menu), Menu (14 items, only when Mode=Menu),
/// Call Type (Off/Analog/Digital, only when Mode=Call), Call Object
/// (reference into Analog Quick Call or Contact/Talkgroup depending on Call
/// Type, only when Call Type isn't Off), Digi Call Type (Off/DMR Hot Text/
/// DMR Call Tip/DMR State Information, only when Call Type=Digital), and
/// Content (reference into Prefabricated SMS for Hot Text, or into State
/// Information itself for State Information - confirmed via a live write
/// capture the same day, see HotKeyCodec's doc comment; a real reference
/// both ways, not the literal "1"/"16" pair this class first guessed).
///
/// The xbenkozx/anytone-cps reference project has real Hot Key byte-layout
/// code (anytone-lib/src/memory/hotkey.cpp, desktop/src/device.cpp's read/
/// writeHotKeySettings), independently confirming this exact field shape
/// (same 18 key names, same Mode/Menu/CallType/CallObject/DigiCallType/
/// Content fields, same enable/disable gating logic in its own table
/// model's flags(), and - confirmed later the same day via a live write
/// capture - the same raw CallType/DigiCallType byte values too) - a
/// strong hint that mostly held up, but it is D878UVII-only
/// (`radio_model != D878UVII_FW400 -&gt; skip`, explicit "TODO: Implement for
/// D890UV" comment) and had never been run against a D890UV, so its
/// addresses were NOT trusted blind - see D890UvMemoryMap's doc comment
/// for the real D890UV addresses a live capture found (completely
/// different from the reference's guesses, even though the per-record
/// byte shape held up). Full radio-write support added 2026-08-04, using
/// the byte layout the two live differential captures (READ + WRITE)
/// already confirmed - see HotKeyCodec's own doc comment for the Encode
/// side. Not yet confirmed against this app's own write path specifically
/// (only against vendor CPS's) - deliberately deferred until the rest of
/// the app is done.
/// </summary>
public partial class HotKeyEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - see AnalogQuickCallEntry's own
    /// doc comment for the split rationale. Deliberately excludes
    /// <see cref="Key"/> - it's the fixed slot name, not an encoded
    /// field.</summary>
    private HotKeySnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private HotKeySnapshot CreateRadioSnapshot() => new(Mode, Menu, CallType, CallObject, DigiCallType, Content);

    private sealed record HotKeySnapshot(byte Mode, byte Menu, byte CallType, int CallObject, byte DigiCallType, int Content);

    public static IReadOnlyList<string> KeyNames { get; } =
    [
        "Hot Key 1", "Hot Key 2", "Hot Key 3", "Hot Key 4", "Hot Key 5", "Hot Key 6",
        "Fun Key+0", "Fun Key+1", "Fun Key+2", "Fun Key+3", "Fun Key+4",
        "Fun Key+5", "Fun Key+6", "Fun Key+7", "Fun Key+8", "Fun Key+9",
        "Fun Key+*", "Fun Key+#"
    ];

    public static IReadOnlyList<string> ModeOptions { get; } = ["Call", "Menu"];

    public static IReadOnlyList<string> MenuOptions { get; } =
    [
        "Off", "SMS", "New SMS", "Hot Text", "Received SMS", "Send SMS", "Contact List", "Manual Dial",
        "Call Log", "Dialed Call", "Received Call", "Missed Call", "Zone", "Radio Set", "Channel Set"
    ];

    public static IReadOnlyList<string> CallTypeOptions { get; } = ["Off", "Analog", "Digital"];

    public static IReadOnlyList<string> DigiCallTypeOptions { get; } = ["Off", "DMR Hot Text", "DMR Call Tip", "DMR State Information"];

    [ObservableProperty] private string _key = "";
    [ObservableProperty] private byte _mode;
    [ObservableProperty] private byte _menu;
    [ObservableProperty] private byte _callType;

    /// <summary>-1 = Off. Otherwise a reference whose meaning depends on
    /// <see cref="CallType"/>: Analog Quick Call's own Number (1-4) when
    /// Analog, or a Talkgroup's own Number when Digital. Resolved to a
    /// display string on MainViewModel (needs sibling collections this
    /// entry doesn't have access to), same pattern as AlarmSettingsEntry's
    /// Emergency Channel fields.</summary>
    [ObservableProperty] private int _callObject = -1;

    [ObservableProperty] private byte _digiCallType;

    /// <summary>-1 = Off. Meaning depends on <see cref="DigiCallType"/>: a
    /// Prefabricated SMS's own Number for DMR Hot Text, or the literal raw
    /// value 1 or 16 for DMR State Information (user unsure what those two
    /// values represent - kept literal, not derived from anything).</summary>
    [ObservableProperty] private int _content = -1;

    public bool IsMenuEnabled => Mode == 1;
    public bool IsCallTypeEnabled => Mode == 0;
    public bool IsCallObjectEnabled => Mode == 0 && CallType != 0;
    public bool IsDigiCallTypeEnabled => Mode == 0 && CallType == 2;
    public bool IsContentEnabled => Mode == 0 && CallType == 2 && DigiCallType is 1 or 3;

    public string DisplayLabel => $"{Key} - {ModeOptions[Mode]}";

    // ComboBox-friendly wrappers - see AnalogQuickCallEntry.OperationTypeText's
    // doc comment for why (matches ChannelEntry.ColorCodeText's convention
    // app-wide: a string "Text" property over a raw byte/index, never a raw
    // SelectedIndex binding).
    public string ModeText
    {
        get => ModeOptions[Mode];
        set
        {
            var index = ModeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Mode = (byte)index;
            }
        }
    }

    public string MenuText
    {
        get => MenuOptions[Menu];
        set
        {
            var index = MenuOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Menu = (byte)index;
            }
        }
    }

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

    public string DigiCallTypeText
    {
        get => DigiCallTypeOptions[DigiCallType];
        set
        {
            var index = DigiCallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DigiCallType = (byte)index;
            }
        }
    }

    partial void OnKeyChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnModeChanged(byte value)
    {
        // Switching Mode invalidates every field gated below it - matches
        // the real vendor CPS resetting dependent fields on a type change
        // (same convention as AnalogQuickCallEntry.OnOperationTypeChanged).
        CallType = 0;
        NotifyEnabledFlags();
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnMenuChanged(byte value)
    {
        OnPropertyChanged(nameof(MenuText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnCallTypeChanged(byte value)
    {
        CallObject = -1;
        DigiCallType = 0;
        NotifyEnabledFlags();
        OnPropertyChanged(nameof(CallTypeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigiCallTypeChanged(byte value)
    {
        Content = -1;
        NotifyEnabledFlags();
        OnPropertyChanged(nameof(DigiCallTypeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnCallObjectChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnContentChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    private void NotifyEnabledFlags()
    {
        OnPropertyChanged(nameof(IsMenuEnabled));
        OnPropertyChanged(nameof(IsCallTypeEnabled));
        OnPropertyChanged(nameof(IsCallObjectEnabled));
        OnPropertyChanged(nameof(IsDigiCallTypeEnabled));
        OnPropertyChanged(nameof(IsContentEnabled));
    }
}
