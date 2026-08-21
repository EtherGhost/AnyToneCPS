using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class AmAirEntry : ObservableValidator
{
    private AmAirSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio"
    /// split as ScanListEntry's own <c>_radioSyncSnapshot</c>. Only set by
    /// <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private AmAirSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private double _frequencyMhz;
    [ObservableProperty] private string _name = "";

    // The special always-present "VFO" row (AmAirCodec.VfoIndex) is
    // deliberately excluded before it ever reaches this model - see
    // RadioReadMapper.MapAmAir's doc comment. Matches how Channel's own
    // VFO A/B (indices 4000/4001) never appear as a ChannelEntry either.
    public string DisplayLabel => $"{Number:000}  {Name}";

    public string FrequencyLabel => $"{FrequencyMhzText} MHz";

    // 108-145 MHz raw text wrapper - deliberately NOT reject-and-revert
    // (see OptionalSettingsEntry.VfoScanStartFreqUhfText's doc comment for
    // the exact bug shape: a floor of 108 is unreachable by typing if every
    // keystroke that produces a below-floor prefix ("1", "10", "108" is
    // fine but "1" alone isn't) gets silently reverted). The raw text is
    // always accepted; ValidateProperty attaches an error via the
    // CustomValidation attribute below, and MainViewModel.ValidateAmAir
    // still independently blocks Save/Write on an out-of-range FrequencyMhz
    // regardless of whether this property's own error state is current.
    [CustomValidation(typeof(AmAirEntry), nameof(ValidateFrequencyText))]
    public string FrequencyMhzText
    {
        get => FrequencyMhz.ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(FrequencyMhzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz)
                && mhz is >= CodeplugLimits.AmAirFrequencyMinMhz and <= CodeplugLimits.AmAirFrequencyMaxMhz)
            {
                FrequencyMhz = mhz;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return mhz is >= CodeplugLimits.AmAirFrequencyMinMhz and <= CodeplugLimits.AmAirFrequencyMaxMhz
            ? ValidationResult.Success
            : new ValidationResult(FormattableString.Invariant($"Must be {CodeplugLimits.AmAirFrequencyMinMhz:0.0000}-{CodeplugLimits.AmAirFrequencyMaxMhz:0.0000} MHz - the AM air band."), [context.MemberName!]);
    }

    public bool IsDirty => _cleanSnapshot is null || IsFrequencyMhzDirty || IsNameDirty;
    public bool IsFrequencyMhzDirty => _cleanSnapshot is null || FrequencyMhz != _cleanSnapshot.FrequencyMhz;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching Channel/
    // ScanList's own pattern: Number determines which radio slot this
    // channel lives in, it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsFrequencyMhzPendingRadioWrite || IsNamePendingRadioWrite;
    public bool IsFrequencyMhzPendingRadioWrite => _radioSyncSnapshot is null || FrequencyMhz != _radioSyncSnapshot.FrequencyMhz;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;

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

    private AmAirSnapshot CreateSnapshot() => new(FrequencyMhz, Name);

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnFrequencyMhzChanged(double value)
    {
        OnPropertyChanged(nameof(FrequencyMhzText));
        OnPropertyChanged(nameof(FrequencyLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsFrequencyMhzDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(NameFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsFrequencyMhzPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
    }

    private sealed record AmAirSnapshot(double FrequencyMhz, string Name);
}
