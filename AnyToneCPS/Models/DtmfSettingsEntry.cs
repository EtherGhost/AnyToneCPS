using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// DTMF Settings - the single dialog's own global/singleton fields (no
/// Encode/Decode tab split, unlike 2Tone/5Tone - see DtmfEncodeEntry for
/// the M1-M16 list on the same screen). The full field spec was captured
/// 2026-08-06 from live vendor CPS screenshots.
///
/// Full radio-write support added the same day, confirmed via 2 live
/// differential WRITE captures - see DtmfSettingsCodec's own doc comment
/// for the byte-level confirmation.
///
/// <see cref="IntervalCharacter"/>/<see cref="GroupCode"/> are stored as
/// plain strings directly matching their own ComboBox option lists
/// (including GroupCode's own "Off" item) - deliberately NOT a byte index
/// like every other option-list-backed field in this app, because the wire
/// format itself stores the raw DTMF symbol VALUE (confirmed live), not a
/// list-position index, so a string avoids a pointless index<->symbol
/// translation layer at the read/write boundary.
/// </summary>
public partial class DtmfSettingsEntry : ObservableObject
{
    [ObservableProperty] private int _transmittingTimeMs = 50;
    [ObservableProperty] private string _selfId = "";
    [ObservableProperty] private string _intervalCharacter = "A";
    [ObservableProperty] private string _groupCode = "Off";
    [ObservableProperty] private byte _decodingResponse;
    [ObservableProperty] private int _firstDigitTimeMs;
    [ObservableProperty] private int _pretimeMs = 10;
    [ObservableProperty] private int _autoResetTimeSeconds;
    [ObservableProperty] private int _timeLapseAfterEncodeMs = 10;

    /// <summary>0 = Off, matching FiveToneSettingsEntry.PttIdPauseTime's own
    /// sentinel convention.</summary>
    [ObservableProperty] private int _pttIdPauseTimeSeconds;

    /// <summary>0 = Off, same sentinel convention as PttIdPauseTimeSeconds.</summary>
    [ObservableProperty] private int _dCodePauseSeconds;

    [ObservableProperty] private bool _sideTone;
    [ObservableProperty] private bool _pttId;
    [ObservableProperty] private string _pttIdStartingBot = "";
    [ObservableProperty] private string _pttIdEndingEot = "";
    [ObservableProperty] private string _remotelyKill = "";
    [ObservableProperty] private string _remotelyStun = "";

    /// <summary>Radio-write baseline only - see Qdc1200SettingsEntry's own
    /// doc comment for the split rationale.</summary>
    private DtmfSettingsSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private DtmfSettingsSnapshot CreateRadioSnapshot() => new(
        TransmittingTimeMs, SelfId, IntervalCharacter, GroupCode, DecodingResponse, FirstDigitTimeMs, PretimeMs,
        AutoResetTimeSeconds, TimeLapseAfterEncodeMs, PttIdPauseTimeSeconds, DCodePauseSeconds, SideTone, PttId,
        PttIdStartingBot, PttIdEndingEot, RemotelyKill, RemotelyStun);

    private sealed record DtmfSettingsSnapshot(
        int TransmittingTimeMs, string SelfId, string IntervalCharacter, string GroupCode, byte DecodingResponse,
        int FirstDigitTimeMs, int PretimeMs, int AutoResetTimeSeconds, int TimeLapseAfterEncodeMs,
        int PttIdPauseTimeSeconds, int DCodePauseSeconds, bool SideTone, bool PttId,
        string PttIdStartingBot, string PttIdEndingEot, string RemotelyKill, string RemotelyStun);

    public static IReadOnlyList<string> TransmittingTimeMsOptions { get; } = ["50", "100", "200", "300", "500"];
    public static IReadOnlyList<string> IntervalCharacterOptions { get; } = ["A", "B", "C", "D", "*", "#"];
    public static IReadOnlyList<string> GroupCodeOptions { get; } = ["Off", "A", "B", "C", "D", "*", "#"];
    public static IReadOnlyList<string> DecodingResponseOptions { get; } = ["None", "Beep tone", "Beep tone & Respond"];
    public static IReadOnlyList<string> FirstDigitTimeMsOptions { get; } = Range(0, 2500, 10);
    public static IReadOnlyList<string> PretimeMsOptions { get; } = Range(10, 2500, 10);
    public static IReadOnlyList<string> AutoResetTimeSecondsOptions { get; } = Enumerable.Range(0, 251).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> TimeLapseAfterEncodeMsOptions { get; } = Range(10, 2500, 10);
    public static IReadOnlyList<string> PttIdPauseTimeSecondsOptions { get; } = new[] { "Off" }.Concat(Enumerable.Range(5, 6).Select(i => i.ToString(CultureInfo.InvariantCulture))).ToList();
    public static IReadOnlyList<string> DCodePauseSecondsOptions { get; } = new[] { "Off" }.Concat(Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture))).ToList();

    private static IReadOnlyList<string> Range(int min, int max, int step) =>
        Enumerable.Range(0, (max - min) / step + 1).Select(i => (min + i * step).ToString(CultureInfo.InvariantCulture)).ToList();

    public string TransmittingTimeMsText
    {
        get => TransmittingTimeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && TransmittingTimeMsOptions.Contains(value))
            {
                TransmittingTimeMs = parsed;
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

    public string FirstDigitTimeMsText
    {
        get => FirstDigitTimeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2500)
            {
                FirstDigitTimeMs = parsed;
            }
        }
    }

    public string PretimeMsText
    {
        get => PretimeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2500)
            {
                PretimeMs = parsed;
            }
        }
    }

    public string AutoResetTimeSecondsText
    {
        get => AutoResetTimeSeconds.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 250)
            {
                AutoResetTimeSeconds = parsed;
            }
        }
    }

    public string TimeLapseAfterEncodeMsText
    {
        get => TimeLapseAfterEncodeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 10 and <= 2500)
            {
                TimeLapseAfterEncodeMs = parsed;
            }
        }
    }

    public string PttIdPauseTimeSecondsText
    {
        get => PttIdPauseTimeSeconds == 0 ? "Off" : PttIdPauseTimeSeconds.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Off")
            {
                PttIdPauseTimeSeconds = 0;
            }
            else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 5 and <= 10)
            {
                PttIdPauseTimeSeconds = parsed;
            }
        }
    }

    public string DCodePauseSecondsText
    {
        get => DCodePauseSeconds == 0 ? "Off" : DCodePauseSeconds.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Off")
            {
                DCodePauseSeconds = 0;
            }
            else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 16)
            {
                DCodePauseSeconds = parsed;
            }
        }
    }

    partial void OnTransmittingTimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(TransmittingTimeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDecodingResponseChanged(byte value)
    {
        OnPropertyChanged(nameof(DecodingResponseText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFirstDigitTimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(FirstDigitTimeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPretimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(PretimeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAutoResetTimeSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(AutoResetTimeSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTimeLapseAfterEncodeMsChanged(int value)
    {
        OnPropertyChanged(nameof(TimeLapseAfterEncodeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPttIdPauseTimeSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(PttIdPauseTimeSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDCodePauseSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(DCodePauseSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSelfIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnIntervalCharacterChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnGroupCodeChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnSideToneChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnPttIdChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnPttIdStartingBotChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnPttIdEndingEotChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnRemotelyKillChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnRemotelyStunChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
