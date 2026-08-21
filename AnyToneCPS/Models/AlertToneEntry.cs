using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>One of the 25 Alert Tone slots (5 categories - CallPermit/
/// UnMatchEnd/CallReset/CallEnd/CallAll - x 5 tones each). Fixed count,
/// always exactly 25 - not user add/removable, same pattern as
/// AprsFixLocationEntry/AprsDigitalReportEntry.</summary>
public partial class AlertToneEntry : ObservableValidator
{
    /// <summary>Radio-write baseline, same separate-from-file-save-dirty
    /// pattern as ChannelEntry's own _radioSyncSnapshot - only set by
    /// <see cref="MarkRadioSynced"/>, after a successful Read From Radio or
    /// Write, never by a project Save.</summary>
    private AlertToneSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private string _category = "";
    [ObservableProperty] private int _toneNumber;
    [ObservableProperty] private int _frequency;
    [ObservableProperty] private int _period;

    /// <summary>Displayed range is 0-3000 (Hz) - confirmed 2026-07-29
    /// directly in the vendor CPS's own UI (typed values outside this
    /// range and observed the CPS reject/clamp them).
    /// PowerOnPasswordChar - digit-only entry is enforced separately in the
    /// UI layer (DigitOnlyInput), so a negative number can never actually be
    /// typed here; the lower bound is just belt-and-braces.
    ///
    /// Was reject-and-revert (force the TextBox back to its old value on
    /// every out-of-range keystroke) until 2026-07-31 - converted to real
    /// validation for consistency with OptionalSettingsEntry's VFO Scan/
    /// Auto Repeater frequency fields (see that class's ObservableValidator
    /// doc comment for why reject-and-revert is broken by design whenever a
    /// range's floor is above zero). This field's own floor is zero, so it
    /// never exhibited the "impossible to type" bug - converted anyway for
    /// consistency across every numeric field in the app.</summary>
    [CustomValidation(typeof(AlertToneEntry), nameof(ValidateFrequencyText))]
    public string FrequencyText
    {
        get => Frequency.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(FrequencyText));
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 3000)
            {
                Frequency = parsed;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return new ValidationResult("Enter a whole number of Hz.", [context.MemberName!]);
        }

        return parsed is >= 0 and <= 3000
            ? ValidationResult.Success
            : new ValidationResult("Must be 0-3000 Hz.", [context.MemberName!]);
    }

    /// <summary>Period is stored raw (wire units), but the real value is
    /// 10x the raw number - confirmed 2026-07-20 comparing this app's
    /// display against the vendor CPS's own (raw 15 shown as "150", raw 10
    /// as "100"), matching the "Period[10ms]" column header's own unit.
    /// Same established pattern as ScanListEntry.LookbackTimeAText: keep
    /// the raw wire value in <see cref="Period"/> (no write path exists
    /// for this entity yet, but a future one would need the raw number),
    /// expose the human-facing value here.
    ///
    /// Displayed range is 0-200 - confirmed twice: a 2026-07-28 live
    /// differential write test found the vendor CPS silently clamping a
    /// requested displayed 260 down to 200, and a direct check of the
    /// vendor CPS's own UI on 2026-07-29 confirmed 0-200. Same reject-and-
    /// revert to real-validation conversion as FrequencyText above,
    /// 2026-07-31.</summary>
    [CustomValidation(typeof(AlertToneEntry), nameof(ValidatePeriodText))]
    public string PeriodText
    {
        get => (Period * 10).ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(PeriodText));
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scaled) && scaled is >= 0 and <= 200)
            {
                Period = scaled / 10;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidatePeriodText(string? value, ValidationContext context)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scaled))
        {
            return new ValidationResult("Enter a whole number of 10ms units.", [context.MemberName!]);
        }

        return scaled is >= 0 and <= 200
            ? ValidationResult.Success
            : new ValidationResult("Must be 0-200 (x10ms).", [context.MemberName!]);
    }

    partial void OnPeriodChanged(int value)
    {
        ValidateProperty(PeriodText, nameof(PeriodText));
        OnPropertyChanged(nameof(PeriodText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnFrequencyChanged(int value)
    {
        ValidateProperty(FrequencyText, nameof(FrequencyText));
        OnPropertyChanged(nameof(FrequencyText));
        NotifyPendingRadioWriteProperties();
    }

    public bool IsFrequencyPendingRadioWrite => _radioSyncSnapshot is null || Frequency != _radioSyncSnapshot.Frequency;
    public bool IsPeriodPendingRadioWrite => _radioSyncSnapshot is null || Period != _radioSyncSnapshot.Period;
    public bool HasAnyPendingRadioWrite => IsFrequencyPendingRadioWrite || IsPeriodPendingRadioWrite;

    /// <summary>Establishes the radio-write baseline - call after a
    /// successful Read From Radio (baseline = what the radio has now) or a
    /// successful Write (baseline = what was just confirmed written).
    /// Deliberately never called by project Save.</summary>
    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = new AlertToneSnapshot(Frequency, Period);
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(IsFrequencyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPeriodPendingRadioWrite));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private sealed record AlertToneSnapshot(int Frequency, int Period);
}
