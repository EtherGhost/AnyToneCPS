using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// 2Tone Settings - Decode tab's 16-row table (No., 1st/2nd Tone Frequency,
/// Decoding Response, Name). Unlike the Encode tab, Decode has no scalar
/// singleton fields of its own - everything lives in this row list.
///
/// See TwoToneEncodeEntry's class doc comment for why this is
/// ObservableValidator and for the write-support confirmation.
/// </summary>
public partial class TwoToneDecodeEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private double _firstToneFrequencyHz;
    [ObservableProperty] private double _secondToneFrequencyHz;
    [ObservableProperty] private byte _decodingResponse;
    [ObservableProperty] private string _name = "";

    public string DisplayLabel => $"{Number}  {Name}";

    public static IReadOnlyList<string> DecodingResponseOptions { get; } = ["None", "Beep tone", "Beep tone & Respond"];

    /// <summary>Radio-write baseline only - see FiveToneIdEntry's own doc
    /// comment for the split rationale. Deliberately excludes
    /// <see cref="Number"/>.</summary>
    private TwoToneDecodeSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private TwoToneDecodeSnapshot CreateRadioSnapshot() => new(FirstToneFrequencyHz, SecondToneFrequencyHz, DecodingResponse, Name);

    private sealed record TwoToneDecodeSnapshot(double FirstToneFrequencyHz, double SecondToneFrequencyHz, byte DecodingResponse, string Name);

    [CustomValidation(typeof(TwoToneDecodeEntry), nameof(ValidateFrequencyText))]
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

    [CustomValidation(typeof(TwoToneDecodeEntry), nameof(ValidateFrequencyText))]
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

    public string DecodingResponseText
    {
        get => DecodingResponseOptions[DecodingResponse];
        set
        {
            var index = ((List<string>)DecodingResponseOptions).IndexOf(value);
            if (index >= 0)
            {
                DecodingResponse = (byte)index;
            }
        }
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

    partial void OnDecodingResponseChanged(byte value)
    {
        OnPropertyChanged(nameof(DecodingResponseText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
