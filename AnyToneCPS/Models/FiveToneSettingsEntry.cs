using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// 5Tone Settings - the Decode/Information ID/Encode/PTT ID Starting(BOT)/
/// PTT ID Ending(EOT) global fields, all on one single vendor CPS screen
/// (unlike QDC 1200 Setting, no separate Decode/Encode tabs here). The
/// Encode tab's 100-row ID table is a separate list entity,
/// <see cref="FiveToneIdEntry"/>, matching how QDC 1200 Setting/Hot Key
/// split their own list portions out.
///
/// The full field spec (option lists/ranges/max lengths) was captured
/// 2026-08-05 from 4 real vendor CPS screenshots. UI/model only - no radio
/// address, codec, or write path exists yet, and the 3 "Special Call"
/// popup dialogs are not yet built (their full Calling Type option list
/// is still unknown - only "Send Message" has ever been seen expanded).
/// </summary>
public partial class FiveToneSettingsEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - separate from any project-file
    /// "dirty" tracking, same split every other radio-write-capable entity
    /// in this app uses. Only set by <see cref="MarkRadioSynced"/>.
    /// Deliberately excludes <see cref="InfoIdNo"/> (a transient UI
    /// selector, confirmed not to be a stored value at all - see this
    /// property's own doc comment) and <see cref="StopCode"/> (never
    /// independently located on the wire, so there's nothing to encode -
    /// see FiveToneSettingsCodec's own doc comment).</summary>
    private FiveToneSettingsSnapshot? _radioSyncSnapshot;

    /// <summary>Single aggregate flag rather than per-field
    /// IsXPendingRadioWrite booleans - same reasoning as
    /// Qdc1200SettingsEntry's own HasAnyPendingRadioWrite.</summary>
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private FiveToneSettingsSnapshot CreateRadioSnapshot() => new(
        SelfId, DecodeStandard, DecodingResponse, DecodeTimeMs,
        DecUnit1, DecUnit2, DecUnit3, DecUnit4, DecUnit5, DecUnit6, DecUnit7, DispAnyId,
        Pretime, AutoResetTime, TimeLapseAfterEncode, PttIdPauseTime, FirstToneLength, StopTimeLength, FirstToneLengthAfterStop, SideTone,
        BotEncodeId, BotStandard, BotTimeOfEncodeTone,
        BotSpecialCall.CallingType, BotSpecialCall.OtherSideId, BotSpecialCall.Message, BotSpecialCall.IntervalCharacter, BotSpecialCall.IsConfigured,
        EotEncodeId, EotStandard, EotTimeOfEncodeTone,
        EotSpecialCall.CallingType, EotSpecialCall.OtherSideId, EotSpecialCall.Message, EotSpecialCall.IntervalCharacter, EotSpecialCall.IsConfigured);

    private sealed record FiveToneSettingsSnapshot(
        string SelfId, byte DecodeStandard, byte DecodingResponse, int DecodeTimeMs,
        bool DecUnit1, bool DecUnit2, bool DecUnit3, bool DecUnit4, bool DecUnit5, bool DecUnit6, bool DecUnit7, bool DispAnyId,
        int Pretime, int AutoResetTime, int TimeLapseAfterEncode, int PttIdPauseTime, int FirstToneLength, int StopTimeLength, int FirstToneLengthAfterStop, bool SideTone,
        string BotEncodeId, byte BotStandard, int BotTimeOfEncodeTone,
        byte BotCallingType, string BotOtherSideId, string BotMessage, byte BotIntervalCharacter, bool BotIsConfigured,
        string EotEncodeId, byte EotStandard, int EotTimeOfEncodeTone,
        byte EotCallingType, string EotOtherSideId, string EotMessage, byte EotIntervalCharacter, bool EotIsConfigured);

    // --- Decode ---
    [ObservableProperty] private string _selfId = "";
    [ObservableProperty] private byte _decodeStandard;
    [ObservableProperty] private byte _decodingResponse;
    [ObservableProperty] private int _decodeTimeMs;
    [ObservableProperty] private bool _decUnit1 = true;
    [ObservableProperty] private bool _decUnit2 = true;
    [ObservableProperty] private bool _decUnit3 = true;
    [ObservableProperty] private bool _decUnit4 = true;
    [ObservableProperty] private bool _decUnit5 = true;
    [ObservableProperty] private bool _decUnit6 = true;
    [ObservableProperty] private bool _decUnit7;
    [ObservableProperty] private bool _dispAnyId;

    // --- Information ID / Information Code Function1 ---
    // Only the selector itself lives here now - Function Option/Function
    // Decoding Response/Information ID/Function Name moved to
    // FiveToneIdEntry 2026-08-06 (see that class's own doc comment for
    // why: they're genuinely per-row data, not a shared singleton).
    [ObservableProperty] private int _infoIdNo = 1;

    // --- Encode ---
    [ObservableProperty] private int _pretime = 10;
    [ObservableProperty] private int _autoResetTime;
    [ObservableProperty] private int _timeLapseAfterEncode = 10;
    [ObservableProperty] private int _pttIdPauseTime = -1;
    [ObservableProperty] private int _firstToneLength = 10;
    [ObservableProperty] private byte _stopCode;
    [ObservableProperty] private int _stopTimeLength;
    [ObservableProperty] private int _firstToneLengthAfterStop;
    [ObservableProperty] private bool _sideTone;

    // --- PTT ID Starting (BOT) ---
    [ObservableProperty] private string _botEncodeId = "";
    [ObservableProperty] private byte _botStandard;
    [ObservableProperty] private int _botTimeOfEncodeTone = 30;

    /// <summary>What the BOT "Special Call" popup sets - same shape as
    /// FiveToneIdEntry.SpecialCall but with no "Choose Encoding Group NO."
    /// concept (that field only exists on the row-level popup).</summary>
    public FiveToneSpecialCallEntry BotSpecialCall { get; } = new();

    // --- PTT ID Ending (EOT) ---
    [ObservableProperty] private string _eotEncodeId = "";
    [ObservableProperty] private byte _eotStandard;
    [ObservableProperty] private int _eotTimeOfEncodeTone = 30;

    /// <summary>Same as <see cref="BotSpecialCall"/>, for the EOT popup.</summary>
    public FiveToneSpecialCallEntry EotSpecialCall { get; } = new();

    public FiveToneSettingsEntry()
    {
        // Keeps BotEncodeId/EotEncodeId correct not just for the Desktop
        // popup's own explicit OK step, but also Mobile's inline editing,
        // which has no such step - same reasoning as FiveToneIdEntry's own
        // constructor subscription.
        BotSpecialCall.PropertyChanged += (_, _) =>
        {
            if (ComposeBotEotEncodeId(BotSpecialCall) is { } composed)
            {
                BotEncodeId = composed;
            }

            OnPropertyChanged(nameof(IsBotEncodeIdEnabled));
            OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        };
        EotSpecialCall.PropertyChanged += (_, _) =>
        {
            if (ComposeBotEotEncodeId(EotSpecialCall) is { } composed)
            {
                EotEncodeId = composed;
            }

            OnPropertyChanged(nameof(IsEotEncodeIdEnabled));
            OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        };
    }

    /// <summary>Same "read-only once &amp;Special Call is used" behavior as
    /// FiveToneIdEntry.IsEncodeIdEnabled - confirmed 2026-08-06 to apply
    /// here too, not just the row-level table.</summary>
    public bool IsBotEncodeIdEnabled => !BotSpecialCall.IsConfigured;

    /// <summary>Same as <see cref="IsBotEncodeIdEnabled"/>, for EOT.</summary>
    public bool IsEotEncodeIdEnabled => !EotSpecialCall.IsConfigured;

    /// <summary>BOT/EOT's own Encode ID composition - CONFIRMED DIFFERENT
    /// from the row-level ID table's own formula (see
    /// FiveToneSpecialCallEntry's class doc comment for the full story).
    /// Confirmed 2026-08-05 via real hex examples from the vendor
    /// CPS, Other Side ID="1234567" throughout: ANI -&gt; OtherSideId +
    /// Interval Character (or nothing for "No stop") + OtherSideId AGAIN
    /// (e.g. "1234567" + "A" + "1234567" -&gt; "1234567A1234567"); PTTID -&gt;
    /// the fixed 2-char code "E6" + OtherSideId (e.g. "E61234567") - NOT
    /// empty/disabled here, unlike the row-level table's own PTTID rule.
    /// Send Message's own formula is NOT confirmed yet - the tail is
    /// confirmed to be hex(Message) (ASCII bytes, one hex-pair per
    /// character) and the head is confirmed to be a fixed 10-hex-char,
    /// Message- and Standard-independent function of Other Side ID alone,
    /// but the exact head algorithm couldn't be reverse-engineered from
    /// the examples gathered so far - returns null (no auto-compose,
    /// left as manual free text) for Send Message until that's cracked.</summary>
    private static string? ComposeBotEotEncodeId(FiveToneSpecialCallEntry specialCall)
    {
        if (!specialCall.IsConfigured)
        {
            return null;
        }

        if (specialCall.IsAni)
        {
            var intervalSuffix = specialCall.IntervalCharacter == 0 ? "" : FiveToneSpecialCallEntry.IntervalCharacterOptions[specialCall.IntervalCharacter];
            return specialCall.OtherSideId + intervalSuffix + specialCall.OtherSideId;
        }

        if (specialCall.IsPttId)
        {
            return "E6" + specialCall.OtherSideId;
        }

        // Send Message - not confirmed yet, see this method's own doc comment.
        return null;
    }

    /// <summary>Shared with FiveToneIdEntry.Standard and the BOT/EOT
    /// Standard fields above - same 15-item list everywhere it appears
    /// (confirmed against the real vendor CPS).</summary>
    public static IReadOnlyList<string> DecodeStandardOptions { get; } =
    [
        "ZVEI1", "ZVEI2", "ZVEI3", "PZVEI", "DZVEI", "PDZVEI", "CCIR1", "CCIR2", "PCCIR",
        "EEA", "EURO SIGNAL", "NATEL", "MODAT", "CCITT", "EIA"
    ];

    public static IReadOnlyList<string> DecodingResponseOptions { get; } = ["None", "Beep tone", "Beep tone & Respond"];

    public static IReadOnlyList<string> StopCodeOptions { get; } = ["Off", "B", "C", "D", "F"];

    public static IReadOnlyList<string> DecodeTimeMsOptions { get; } = Range(0, 2000, 10);
    public static IReadOnlyList<string> PretimeOptions { get; } = Range(10, 2550, 10);
    public static IReadOnlyList<string> AutoResetTimeOptions { get; } = Enumerable.Range(0, 251).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> TimeLapseAfterEncodeOptions { get; } = Range(10, 2550, 10);
    public static IReadOnlyList<string> PttIdPauseTimeOptions { get; } = new[] { "Off" }.Concat(Enumerable.Range(5, 71).Select(i => i.ToString(CultureInfo.InvariantCulture))).ToList();
    public static IReadOnlyList<string> FirstToneLengthOptions { get; } = Range(10, 2550, 10);
    public static IReadOnlyList<string> StopTimeLengthOptions { get; } = Range(0, 2550, 10);
    public static IReadOnlyList<string> FirstToneLengthAfterStopOptions { get; } = Range(0, 2500, 10);

    /// <summary>Shared with FiveToneIdEntry.TimeOfEncodeTone - same 30-100
    /// step-1 range everywhere it appears (confirmed against the real vendor CPS).</summary>
    public static IReadOnlyList<string> TimeOfEncodeToneOptions { get; } = Enumerable.Range(30, 71).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

    private static IReadOnlyList<string> Range(int min, int max, int step) =>
        Enumerable.Range(0, (max - min) / step + 1).Select(i => (min + i * step).ToString(CultureInfo.InvariantCulture)).ToList();

    public string DecodeStandardText
    {
        get => DecodeStandardOptions[DecodeStandard];
        set
        {
            var index = DecodeStandardOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DecodeStandard = (byte)index;
            }
        }
    }

    public string DecodingResponseText
    {
        get => DecodingResponseOptions[DecodingResponse];
        set
        {
            var index = DecodingResponseOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DecodingResponse = (byte)index;
            }
        }
    }

    public string DecodeTimeMsText
    {
        get => DecodeTimeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2000)
            {
                DecodeTimeMs = parsed;
            }
        }
    }

    /// <summary>Confirmed 2026-08-06: this isn't a free 1-16 choice
    /// - it picks which 5Tone ID (row) to view/set the Information Code
    /// Function for, so its real range is whatever row numbers currently
    /// exist in <see cref="FiveToneIdEntry"/>'s own list (same "1 row =
    /// only option 1" behavior that looked like a cap before turned out
    /// to just be "only row 1 exists yet"). The option list itself is
    /// built in MainViewModel.FiveTone.cs, which has access to that list;
    /// this setter only guards against a value outside the radio's own
    /// max row count.</summary>
    public string InfoIdNoText
    {
        get => InfoIdNo.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= CodeplugLimits.FiveToneIdMax)
            {
                InfoIdNo = parsed;
            }
        }
    }

    public string PretimeText
    {
        get => Pretime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2550)
            {
                Pretime = parsed;
            }
        }
    }

    public string AutoResetTimeText
    {
        get => AutoResetTime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 250)
            {
                AutoResetTime = parsed;
            }
        }
    }

    public string TimeLapseAfterEncodeText
    {
        get => TimeLapseAfterEncode.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2550)
            {
                TimeLapseAfterEncode = parsed;
            }
        }
    }

    /// <summary>-1 = Off.</summary>
    public string PttIdPauseTimeText
    {
        get => PttIdPauseTime == -1 ? "Off" : PttIdPauseTime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Off")
            {
                PttIdPauseTime = -1;
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 5 and <= 75)
            {
                PttIdPauseTime = parsed;
            }
        }
    }

    public string FirstToneLengthText
    {
        get => FirstToneLength.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2550)
            {
                FirstToneLength = parsed;
            }
        }
    }

    public string StopCodeText
    {
        get => StopCodeOptions[StopCode];
        set
        {
            var index = StopCodeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                StopCode = (byte)index;
            }
        }
    }

    public string StopTimeLengthText
    {
        get => StopTimeLength.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2550)
            {
                StopTimeLength = parsed;
            }
        }
    }

    public string FirstToneLengthAfterStopText
    {
        get => FirstToneLengthAfterStop.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2500)
            {
                FirstToneLengthAfterStop = parsed;
            }
        }
    }

    public string BotStandardText
    {
        get => DecodeStandardOptions[BotStandard];
        set
        {
            var index = DecodeStandardOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                BotStandard = (byte)index;
            }
        }
    }

    public string BotTimeOfEncodeToneText
    {
        get => BotTimeOfEncodeTone.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 30 and <= 100)
            {
                BotTimeOfEncodeTone = parsed;
            }
        }
    }

    public string EotStandardText
    {
        get => DecodeStandardOptions[EotStandard];
        set
        {
            var index = DecodeStandardOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                EotStandard = (byte)index;
            }
        }
    }

    public string EotTimeOfEncodeToneText
    {
        get => EotTimeOfEncodeTone.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 30 and <= 100)
            {
                EotTimeOfEncodeTone = parsed;
            }
        }
    }

    // InfoIdNo/StopCode deliberately do NOT notify HasAnyPendingRadioWrite
    // - InfoIdNo is a transient UI selector (confirmed not stored on the
    // wire at all), StopCode was never independently located, so neither
    // is part of the radio-write snapshot above.
    partial void OnSelfIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecodeStandardChanged(byte value)
    {
        OnPropertyChanged(nameof(DecodeStandardText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDecodingResponseChanged(byte value)
    {
        OnPropertyChanged(nameof(DecodingResponseText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDecodeTimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(DecodeTimeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnInfoIdNoChanged(int value) => OnPropertyChanged(nameof(InfoIdNoText));

    partial void OnDecUnit1Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit2Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit3Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit4Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit5Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit6Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDecUnit7Changed(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnDispAnyIdChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnSideToneChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnPretimeChanged(int value)
    {
        OnPropertyChanged(nameof(PretimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAutoResetTimeChanged(int value)
    {
        OnPropertyChanged(nameof(AutoResetTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTimeLapseAfterEncodeChanged(int value)
    {
        OnPropertyChanged(nameof(TimeLapseAfterEncodeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPttIdPauseTimeChanged(int value)
    {
        OnPropertyChanged(nameof(PttIdPauseTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFirstToneLengthChanged(int value)
    {
        OnPropertyChanged(nameof(FirstToneLengthText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnStopCodeChanged(byte value) => OnPropertyChanged(nameof(StopCodeText));

    partial void OnStopTimeLengthChanged(int value)
    {
        OnPropertyChanged(nameof(StopTimeLengthText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFirstToneLengthAfterStopChanged(int value)
    {
        OnPropertyChanged(nameof(FirstToneLengthAfterStopText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnBotEncodeIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnBotStandardChanged(byte value)
    {
        OnPropertyChanged(nameof(BotStandardText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnBotTimeOfEncodeToneChanged(int value)
    {
        OnPropertyChanged(nameof(BotTimeOfEncodeToneText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnEotEncodeIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnEotStandardChanged(byte value)
    {
        OnPropertyChanged(nameof(EotStandardText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnEotTimeOfEncodeToneChanged(int value)
    {
        OnPropertyChanged(nameof(EotTimeOfEncodeToneText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
