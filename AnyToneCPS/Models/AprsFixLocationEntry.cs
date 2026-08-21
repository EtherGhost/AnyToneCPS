using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>One of the 7 additional APRS fix-location waypoints (fix 2-8;
/// fix 1 is the primary/home position and lives directly on
/// AprsSettingsEntry). Fixed count, always exactly 7 - not user add/removable.
/// Radio-write dirty-tracking scaffolding only, added ahead of the actual
/// encode/patch work - see AprsSettingsEntry's own doc comment for why.</summary>
public partial class AprsFixLocationEntry : ObservableValidator
{
    /// <summary>Radio-write baseline only, same "_radioSyncSnapshot" split
    /// every other radio-write-capable entity uses. Deliberately excludes
    /// Number - it's the slot position, not an encoded field.</summary>
    private AprsFixLocationSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private double _lat;
    [ObservableProperty] private byte _ns;
    [ObservableProperty] private double _lng;
    [ObservableProperty] private byte _ew;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AprsFixLocationSnapshot CreateRadioSnapshot() => new(Lat, Ns, Lng, Ew);

    private sealed record AprsFixLocationSnapshot(double Lat, byte Ns, double Lng, byte Ew);

    /// <summary>Ew only has real backing data on the wire for Fix2/Fix3
    /// (Number 2/3) - CONFIRMED 2026-08-15 by 3 independent live
    /// differential writes, see AprsSettingsCodec's own doc comment and
    /// Capture_Findings.md. Fix4-8's E/W control is disabled in the UI
    /// using this property rather than silently dropping edits on write.</summary>
    public bool CanEditEw => Number <= 3;

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(CanEditEw));

    // Same N/S, E/W option lists as AprsSettingsEntry's Fix 1 - user-
    // confirmed 2026-08-15 the same shape repeats for Fix 2-8.
    public string NsText
    {
        get => Ns < AprsSettingsEntry.NsOptions.Count ? AprsSettingsEntry.NsOptions[Ns] : Ns.ToString(CultureInfo.InvariantCulture);
        set
        {
            var index = AprsSettingsEntry.NsOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Ns = (byte)index;
            }
        }
    }

    public string EwText
    {
        get => Ew < AprsSettingsEntry.EwOptions.Count ? AprsSettingsEntry.EwOptions[Ew] : Ew.ToString(CultureInfo.InvariantCulture);
        set
        {
            var index = AprsSettingsEntry.EwOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Ew = (byte)index;
            }
        }
    }

    // Not reject-and-revert, same reasoning as AprsSettingsEntry.Fix1LatText -
    // 0-90/0-180 decimal degrees, confirmed 2026-08-15.
    [CustomValidation(typeof(AprsFixLocationEntry), nameof(ValidateLatText))]
    public string LatText
    {
        get => Lat.ToString("00.00000", CultureInfo.InvariantCulture);
        set => SetDegreesText(value, nameof(LatText), 0, 90, v => Lat = v);
    }

    [CustomValidation(typeof(AprsFixLocationEntry), nameof(ValidateLngText))]
    public string LngText
    {
        get => Lng.ToString("000.00000", CultureInfo.InvariantCulture);
        set => SetDegreesText(value, nameof(LngText), 0, 180, v => Lng = v);
    }

    private void SetDegreesText(string? value, string propertyName, double min, double max, Action<double> assign)
    {
        ValidateProperty(value, propertyName);
        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) && v >= min && v <= max)
        {
            assign(v);
        }

        OnPropertyChanged(nameof(HasErrors));
    }

    public static ValidationResult? ValidateLatText(string? value, ValidationContext context) => ValidateDegreesText(value, context, 0, 90);
    public static ValidationResult? ValidateLngText(string? value, ValidationContext context) => ValidateDegreesText(value, context, 0, 180);

    private static ValidationResult? ValidateDegreesText(string? value, ValidationContext context, double min, double max)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var degrees))
        {
            return new ValidationResult("Enter a decimal degree value.", [context.MemberName!]);
        }

        return degrees >= min && degrees <= max
            ? ValidationResult.Success
            : new ValidationResult(FormattableString.Invariant($"Must be {min:0.00000}-{max:0.00000}."), [context.MemberName!]);
    }

    partial void OnLatChanged(double value)
    {
        OnPropertyChanged(nameof(LatText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNsChanged(byte value)
    {
        OnPropertyChanged(nameof(NsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnLngChanged(double value)
    {
        OnPropertyChanged(nameof(LngText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnEwChanged(byte value)
    {
        OnPropertyChanged(nameof(EwText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
