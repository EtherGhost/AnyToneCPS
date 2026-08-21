using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// 2Tone Settings - Encode tab's global/singleton fields only (1st/2nd/Long
/// Tone Duration, Gap Time, Auto Reset Time, Side Tone). The Encode tab's
/// own 24-row frequency table is a separate list entity, <see
/// cref="TwoToneEncodeEntry"/>, matching how Qdc1200SettingsEntry split its
/// own Decode/Encode tabs' scalar fields from Qdc1200IdEntry's row table.
/// The Decode tab has no scalar fields of its own - see <see
/// cref="TwoToneDecodeEntry"/>.
///
/// The full field spec was captured 2026-08-06 from live vendor CPS
/// screenshots. Full radio-write support added the same day, confirmed via
/// 2 live differential WRITE captures - see TwoToneEncodeSettingsCodec's
/// own doc comment for the byte-level confirmation (all 6 fields matched
/// exactly).
/// </summary>
public partial class TwoToneEncodeSettingsEntry : ObservableObject
{
    [ObservableProperty] private double _firstToneDurationSeconds = 0.5;
    [ObservableProperty] private double _secondToneDurationSeconds = 0.5;
    [ObservableProperty] private double _longToneDurationSeconds = 0.5;
    [ObservableProperty] private int _gapTimeMs;
    [ObservableProperty] private int _autoResetTimeSeconds;
    [ObservableProperty] private bool _sideTone;

    /// <summary>Radio-write baseline only - see Qdc1200SettingsEntry's own
    /// doc comment for the split rationale.</summary>
    private TwoToneEncodeSettingsSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private TwoToneEncodeSettingsSnapshot CreateRadioSnapshot() => new(
        FirstToneDurationSeconds, SecondToneDurationSeconds, LongToneDurationSeconds, GapTimeMs, AutoResetTimeSeconds, SideTone);

    private sealed record TwoToneEncodeSettingsSnapshot(
        double FirstToneDurationSeconds, double SecondToneDurationSeconds, double LongToneDurationSeconds, int GapTimeMs, int AutoResetTimeSeconds, bool SideTone);

    public static IReadOnlyList<string> ToneDurationOptions { get; } =
        Enumerable.Range(5, 101).Select(i => (i * 0.1).ToString("0.0", CultureInfo.InvariantCulture)).ToList();

    public static IReadOnlyList<string> GapTimeMsOptions { get; } =
        Enumerable.Range(0, 21).Select(i => (i * 100).ToString(CultureInfo.InvariantCulture)).ToList();

    public static IReadOnlyList<string> AutoResetTimeSecondsOptions { get; } =
        Enumerable.Range(0, 251).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

    public string FirstToneDurationSecondsText
    {
        get => FirstToneDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0.5 and <= 10.5)
            {
                FirstToneDurationSeconds = parsed;
            }
        }
    }

    public string SecondToneDurationSecondsText
    {
        get => SecondToneDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0.5 and <= 10.5)
            {
                SecondToneDurationSeconds = parsed;
            }
        }
    }

    public string LongToneDurationSecondsText
    {
        get => LongToneDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0.5 and <= 10.5)
            {
                LongToneDurationSeconds = parsed;
            }
        }
    }

    public string GapTimeMsText
    {
        get => GapTimeMs.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2000)
            {
                GapTimeMs = parsed;
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

    partial void OnFirstToneDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(FirstToneDurationSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSecondToneDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(SecondToneDurationSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnLongToneDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(LongToneDurationSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnGapTimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(GapTimeMsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAutoResetTimeSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(AutoResetTimeSecondsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSideToneChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
