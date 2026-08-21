using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

public partial class FmChannelEntry : ObservableValidator
{
    private FmChannelSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - same
    /// "unsaved to the project file" vs "not yet written to the radio"
    /// split as AmAirEntry's own <c>_radioSyncSnapshot</c>. Only set by
    /// <see cref="MarkRadioSynced"/>, never by <see cref="MarkClean"/>.</summary>
    private FmChannelSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private double _frequencyMhz;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _scanAdd;

    // The special always-present "home"/VFO channel (FmChannelCodec.HomeIndex)
    // is deliberately excluded before it ever reaches this model - see
    // RadioReadMapper.MapFmChannels' doc comment. Matches AmAirEntry's own
    // VFO exclusion.
    public string DisplayLabel => $"{Number:00}  {Name}";

    public string FrequencyLabel => $"{FrequencyMhzText} MHz";

    // 76.00-108.00 MHz raw text wrapper - deliberately NOT reject-and-revert,
    // same shape as AmAirEntry.FrequencyMhzText (see that property's doc
    // comment for the exact bug shape a floor above zero creates). The raw
    // text is always accepted; ValidateProperty attaches an error via the
    // CustomValidation attribute below, and MainViewModel.ValidateFmChannels
    // still independently blocks Save/Write on an out-of-range FrequencyMhz
    // regardless of whether this property's own error state is current.
    [CustomValidation(typeof(FmChannelEntry), nameof(ValidateFrequencyText))]
    public string FrequencyMhzText
    {
        get => FrequencyMhz.ToString("000.0000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(FrequencyMhzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz)
                && mhz is >= CodeplugLimits.FmChannelFrequencyMinMhz and <= CodeplugLimits.FmChannelFrequencyMaxMhz)
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

        return mhz is >= CodeplugLimits.FmChannelFrequencyMinMhz and <= CodeplugLimits.FmChannelFrequencyMaxMhz
            ? ValidationResult.Success
            : new ValidationResult(FormattableString.Invariant($"Must be {CodeplugLimits.FmChannelFrequencyMinMhz:0.00}-{CodeplugLimits.FmChannelFrequencyMaxMhz:0.00} MHz - the FM broadcast band."), [context.MemberName!]);
    }

    public bool IsDirty => _cleanSnapshot is null || IsFrequencyMhzDirty || IsNameDirty || IsScanAddDirty;
    public bool IsFrequencyMhzDirty => _cleanSnapshot is null || FrequencyMhz != _cleanSnapshot.FrequencyMhz;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public bool IsScanAddDirty => _cleanSnapshot is null || ScanAdd != _cleanSnapshot.ScanAdd;
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";

    // --- Radio-write dirty tracking (see _radioSyncSnapshot doc comment
    // above) - deliberately does NOT include Number, matching AmAir/Channel/
    // ScanList's own pattern: Number determines which radio slot this
    // channel lives in, it isn't itself an encoded field.
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || IsFrequencyMhzPendingRadioWrite || IsNamePendingRadioWrite || IsScanAddPendingRadioWrite;
    public bool IsFrequencyMhzPendingRadioWrite => _radioSyncSnapshot is null || FrequencyMhz != _radioSyncSnapshot.FrequencyMhz;
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsScanAddPendingRadioWrite => _radioSyncSnapshot is null || ScanAdd != _radioSyncSnapshot.ScanAdd;

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

    private FmChannelSnapshot CreateSnapshot() => new(FrequencyMhz, Name, ScanAdd);

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

    partial void OnScanAddChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsFrequencyMhzDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(IsScanAddDirty));
        OnPropertyChanged(nameof(NameFontWeight));
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(IsFrequencyMhzPendingRadioWrite));
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsScanAddPendingRadioWrite));
    }

    private sealed record FmChannelSnapshot(double FrequencyMhz, string Name, bool ScanAdd);
}
