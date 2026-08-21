using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class AutoRepeaterOffsetEntry : ObservableValidator
{
    private AutoRepeaterOffsetSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio"
    /// split as AmAirEntry's own <c>_radioSyncSnapshot</c>. Only set by
    /// <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private AutoRepeaterOffsetSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private double _offsetFrequencyMhz;
    [ObservableProperty] private int _rawOffset;

    public string OffsetFrequencyLabel => $"{OffsetFrequencyMhzText} MHz";

    public bool IsDirty => _cleanSnapshot is null || IsOffsetFrequencyMhzDirty;
    public bool IsOffsetFrequencyMhzDirty => _cleanSnapshot is null || OffsetFrequencyMhz != _cleanSnapshot.OffsetFrequencyMhz;
    public string OffsetFrequencyMhzFontWeight => IsOffsetFrequencyMhzDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching every other
    // entity's own convention: Number determines which radio slot this
    // entry lives in, it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsOffsetFrequencyMhzPendingRadioWrite;
    public bool IsOffsetFrequencyMhzPendingRadioWrite => _radioSyncSnapshot is null || OffsetFrequencyMhz != _radioSyncSnapshot.OffsetFrequencyMhz;

    public void MarkClean()
    {
        _cleanSnapshot = CreateSnapshot();
        NotifyDirtyProperties();
    }

    /// <summary>Radio-write baseline only - deliberately separate from
    /// <see cref="MarkClean"/>, see <see cref="_radioSyncSnapshot"/>'s doc
    /// comment.</summary>
    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateSnapshot();
        NotifyPendingRadioWriteProperties();
    }

    private AutoRepeaterOffsetSnapshot CreateSnapshot() => new(OffsetFrequencyMhz);

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsOffsetFrequencyMhzDirty));
        OnPropertyChanged(nameof(OffsetFrequencyMhzFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsOffsetFrequencyMhzPendingRadioWrite));
    }

    // 0.001-90.0 MHz (1 kHz - 90 MHz) raw text wrapper - deliberately NOT
    // reject-and-revert, same shape as AmAirEntry.FrequencyMhzText (see that
    // property's doc comment for the exact bug shape a floor above zero
    // creates - this field's floor is especially low, 1 kHz, so it would be
    // hit almost immediately). The raw text is always accepted;
    // ValidateProperty attaches an error via the CustomValidation attribute
    // below, and MainViewModel.ValidateAutoRepeaterOffsets still
    // independently blocks Save/Write on the underlying value regardless of
    // whether this property's own error state is current. Kept in MHz
    // (not kHz) for consistency with every other frequency field app-wide -
    // vendor CPS itself always takes MHz as input, only switching its own
    // on-screen label to kHz for small values (confirmed 2026-08-03).
    [CustomValidation(typeof(AutoRepeaterOffsetEntry), nameof(ValidateOffsetFrequencyText))]
    public string OffsetFrequencyMhzText
    {
        get => OffsetFrequencyMhz.ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(OffsetFrequencyMhzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz)
                && mhz is >= CodeplugLimits.AutoRepeaterOffsetFrequencyMinMhz and <= CodeplugLimits.AutoRepeaterOffsetFrequencyMaxMhz)
            {
                OffsetFrequencyMhz = mhz;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateOffsetFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return mhz is >= CodeplugLimits.AutoRepeaterOffsetFrequencyMinMhz and <= CodeplugLimits.AutoRepeaterOffsetFrequencyMaxMhz
            ? ValidationResult.Success
            : new ValidationResult(FormattableString.Invariant($"Must be {CodeplugLimits.AutoRepeaterOffsetFrequencyMinMhz:0.000}-{CodeplugLimits.AutoRepeaterOffsetFrequencyMaxMhz:0.00000} MHz."), [context.MemberName!]);
    }

    partial void OnOffsetFrequencyMhzChanged(double value)
    {
        OnPropertyChanged(nameof(OffsetFrequencyMhzText));
        OnPropertyChanged(nameof(OffsetFrequencyLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private sealed record AutoRepeaterOffsetSnapshot(double OffsetFrequencyMhz);
}
