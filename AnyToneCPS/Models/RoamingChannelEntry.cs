using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AnyToneCPS.Services.Radio.Codecs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Full radio-write support added 2026-08-07 - see RoamingChannelCodec's
/// own doc comment for the live-capture confirmation, including the
/// ColorCode "No Use" (16) and Slot 0-indexed (0/1/2) findings.
/// </summary>
public partial class RoamingChannelEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private double _rxFrequencyMhz;
    [ObservableProperty] private double _txFrequencyMhz;
    [ObservableProperty] private int _colorCode;
    [ObservableProperty] private int _slot;
    [ObservableProperty] private string _name = "";

    /// <summary>Deliberately NOT reject-and-revert - same reasoning as
    /// ChannelEntry.RxFrequencyMHzText's own doc comment. The real valid
    /// range is two disjoint bands (see
    /// CodeplugLimits.IsValidVhfOrUhfFrequencyMhz), not a single
    /// continuous span - confirmed via this entity's own live capture
    /// (a frequency picked from the 174-400 dead zone was silently
    /// rejected and reverted by the vendor CPS).</summary>
    [CustomValidation(typeof(RoamingChannelEntry), nameof(ValidateFrequencyText))]
    public string RxFrequencyMhzText
    {
        get => RxFrequencyMhz.ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(RxFrequencyMhzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
                && CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(v))
            {
                RxFrequencyMhz = v;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(RoamingChannelEntry), nameof(ValidateFrequencyText))]
    public string TxFrequencyMhzText
    {
        get => TxFrequencyMhz.ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(TxFrequencyMhzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
                && CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(v))
            {
                TxFrequencyMhz = v;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    /// <summary>Instance property (not static) so Mobile's per-row
    /// DataTemplate - whose DataContext is this entry, not the ViewModel -
    /// can bind a real ComboBox, same reasoning as TalkgroupEntry.CallTypeOptions.</summary>
    public IReadOnlyList<string> ColorCodeOptions { get; } =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "No Use"];

    public IReadOnlyList<string> SlotOptions { get; } = ["Slot 1", "Slot 2", "No Use"];

    public string ColorCodeSelection
    {
        get => RoamingChannelCodec.ColorCodeToString(ColorCode);
        set { if (RoamingChannelCodec.ParseColorCode(value) is { } v) ColorCode = v; }
    }

    public string SlotSelection
    {
        get => RoamingChannelCodec.SlotToString(Slot);
        set { if (RoamingChannelCodec.ParseSlot(value) is { } v) Slot = v; }
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(mhz)
            ? ValidationResult.Success
            : new ValidationResult($"Must be {CodeplugLimits.VhfFrequencyMinMhz:0.00000}-{CodeplugLimits.VhfFrequencyMaxMhz:0.00000} or {CodeplugLimits.UhfFrequencyMinMhz:0.00000}-{CodeplugLimits.UhfFrequencyMaxMhz:0.00000} MHz (the radio's VHF/UHF band limits).", [context.MemberName!]);
    }

    partial void OnRxFrequencyMhzChanged(double value)
    {
        OnPropertyChanged(nameof(RxFrequencyMhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFrequencyMhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFrequencyMhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private RoamingChannelSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private RoamingChannelSnapshot CreateRadioSnapshot() => new(RxFrequencyMhz, TxFrequencyMhz, ColorCode, Slot, Name);

    private sealed record RoamingChannelSnapshot(double RxFrequencyMhz, double TxFrequencyMhz, int ColorCode, int Slot, string Name);

    partial void OnColorCodeChanged(int value)
    {
        OnPropertyChanged(nameof(ColorCodeSelection));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSlotChanged(int value)
    {
        OnPropertyChanged(nameof(SlotSelection));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
