using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// 2Tone Settings - Encode tab's 24-row frequency table (No., 1st/2nd Tone
/// Frequency, Name). See <see cref="TwoToneEncodeSettingsEntry"/> for the
/// tab's own scalar fields.
///
/// ObservableValidator (not plain ObservableObject, unlike QdcAddressEntry/
/// Qdc1200IdEntry) because the Frequency fields are free-typed with a floor
/// above zero - same INotifyDataErrorInfo shape as AmAirEntry.FrequencyMhzText,
/// needed so a below-floor prefix while typing doesn't get silently reverted.
///
/// Full radio-write support added 2026-08-06, confirmed via 2 live
/// differential WRITE captures - see TwoToneEncodeCodec's own doc comment
/// for the byte-level confirmation.
/// </summary>
public partial class TwoToneEncodeEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private double _firstToneFrequencyHz;
    [ObservableProperty] private double _secondToneFrequencyHz;
    [ObservableProperty] private string _name = "";

    public string DisplayLabel => $"{Number}  {Name}";

    /// <summary>Radio-write baseline only - see FiveToneIdEntry's own doc
    /// comment for the split rationale. Deliberately excludes
    /// <see cref="Number"/>.</summary>
    private TwoToneEncodeSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private TwoToneEncodeSnapshot CreateRadioSnapshot() => new(FirstToneFrequencyHz, SecondToneFrequencyHz, Name);

    private sealed record TwoToneEncodeSnapshot(double FirstToneFrequencyHz, double SecondToneFrequencyHz, string Name);

    [CustomValidation(typeof(TwoToneEncodeEntry), nameof(ValidateFrequencyText))]
    public string FirstToneFrequencyHzText
    {
        get => FirstToneFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(FirstToneFrequencyHzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hz)
                && hz is >= CodeplugLimits.TwoToneFrequencyMinHz and <= CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                FirstToneFrequencyHz = hz;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(TwoToneEncodeEntry), nameof(ValidateFrequencyText))]
    public string SecondToneFrequencyHzText
    {
        get => SecondToneFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(SecondToneFrequencyHzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hz)
                && hz is >= CodeplugLimits.TwoToneFrequencyMinHz and <= CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                SecondToneFrequencyHz = hz;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hz))
        {
            return new ValidationResult("Enter a decimal frequency in Hz.", [context.MemberName!]);
        }

        return hz is >= CodeplugLimits.TwoToneFrequencyMinHz and <= CodeplugLimits.TwoToneFrequencyMaxHz
            ? ValidationResult.Success
            : new ValidationResult(FormattableString.Invariant($"Must be {CodeplugLimits.TwoToneFrequencyMinHz:0.0}-{CodeplugLimits.TwoToneFrequencyMaxHz:0.0} Hz."), [context.MemberName!]);
    }

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnFirstToneFrequencyHzChanged(double value)
    {
        OnPropertyChanged(nameof(FirstToneFrequencyHzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSecondToneFrequencyHzChanged(double value)
    {
        OnPropertyChanged(nameof(SecondToneFrequencyHzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
