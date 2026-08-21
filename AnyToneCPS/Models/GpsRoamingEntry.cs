using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// One of the radio's 32 FIXED GPS Roaming slots - like EncryptionKeyEntry,
/// every slot 1-32 always exists on the radio; there is no add/remove, only
/// editing a slot's own fields (see GpsRoamingCodec's own doc comment for
/// the live-confirmed fixed two-512-byte-half addressing this implies).
///
/// Full radio-write support added 2026-08-09, confirmed via one live
/// differential write capture. North/South and East/West are confirmed
/// 0=N/E, 1=S/W. Latitude/Longitude Minute are exposed as a single
/// "MM.mm" text field (matching the vendor CPS's own edit popup, which
/// shows ONE textbox for this rather than the grid's separate Minute/
/// MinMark columns) - LatMinuteDecimal/LongMinuteDecimal ARE the hundredths-
/// of-a-minute fraction, confirmed live (planted .34/.78/.23/.67 all
/// round-tripped exactly). The vendor CPS's second "ddd.ddddd" tab is just
/// an alternate DISPLAY of the same stored coordinate (decimal degrees
/// computed from degrees+minutes for convenience) - not a separately
/// stored value, so this app only exposes the one real stored
/// representation.
/// </summary>
public partial class GpsRoamingEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private int _zoneIndex = 255;

    /// <summary>Cached display name only - like ChannelEntry.ContactDisplayName,
    /// resolved against MainViewModel.Zones at read/selection time, not
    /// part of the radio-write baseline. "Off" matches the vendor CPS's own
    /// text for the 255 (unset) sentinel.</summary>
    [ObservableProperty] private string _zoneDisplayName = "Off";

    [ObservableProperty] private int _latDegree;
    [ObservableProperty] private int _latMinute;
    [ObservableProperty] private int _latMinuteDecimal;
    [ObservableProperty] private int _northSouth;
    [ObservableProperty] private int _longDegree;
    [ObservableProperty] private int _longMinute;
    [ObservableProperty] private int _longMinuteDecimal;
    [ObservableProperty] private int _eastWest;
    [ObservableProperty] private int _radius;

    /// <summary>Confirmed live 2026-08-09 - vendor CPS dropdown shows 0-90
    /// in steps of 1.</summary>
    public static IReadOnlyList<int> LatDegreeOptions { get; } = BuildRange(0, 90);

    /// <summary>Confirmed live 2026-08-09 - vendor CPS dropdown shows
    /// 0-180 in steps of 1.</summary>
    public static IReadOnlyList<int> LongDegreeOptions { get; } = BuildRange(0, 180);

    private static IReadOnlyList<int> BuildRange(int start, int endInclusive)
    {
        var options = new List<int>(endInclusive - start + 1);
        for (var i = start; i <= endInclusive; i++)
        {
            options.Add(i);
        }

        return options;
    }

    public static IReadOnlyList<string> NorthSouthOptions { get; } = ["N", "S"];
    public static IReadOnlyList<string> EastWestOptions { get; } = ["E", "W"];

    public string NorthSouthText
    {
        get => NorthSouth == 1 ? "S" : "N";
        set => NorthSouth = value == "S" ? 1 : 0;
    }

    public string EastWestText
    {
        get => EastWest == 1 ? "W" : "E";
        set => EastWest = value == "W" ? 1 : 0;
    }

    private static readonly Regex MinuteTextPattern = new(@"^\d{1,2}\.\d{2}$", RegexOptions.Compiled);

    /// <summary>MM.mm - matches the vendor CPS's own single-textbox format
    /// exactly (see this class's own doc comment). The 2 fraction digits
    /// ARE LatMinuteDecimal, confirmed live - not a separate/unrelated
    /// field.</summary>
    [CustomValidation(typeof(GpsRoamingEntry), nameof(ValidateMinuteText))]
    public string LatMinuteText
    {
        get => $"{LatMinute:00}.{LatMinuteDecimal:00}";
        set
        {
            ValidateProperty(value, nameof(LatMinuteText));
            if (TryParseMinute(value, out var whole, out var fraction))
            {
                LatMinute = whole;
                LatMinuteDecimal = fraction;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(GpsRoamingEntry), nameof(ValidateMinuteText))]
    public string LongMinuteText
    {
        get => $"{LongMinute:00}.{LongMinuteDecimal:00}";
        set
        {
            ValidateProperty(value, nameof(LongMinuteText));
            if (TryParseMinute(value, out var whole, out var fraction))
            {
                LongMinute = whole;
                LongMinuteDecimal = fraction;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    private static bool TryParseMinute(string? value, out int whole, out int fraction)
    {
        whole = 0;
        fraction = 0;
        if (value is null || !MinuteTextPattern.IsMatch(value))
        {
            return false;
        }

        var parts = value.Split('.');
        return int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out whole)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out fraction)
            && whole <= 59;
    }

    public static ValidationResult? ValidateMinuteText(string? value, ValidationContext context) =>
        TryParseMinute(value, out _, out _) ? ValidationResult.Success : new ValidationResult("Must be MM.mm, minutes 00-59");

    /// <summary>Confirmed live 2026-08-09 as exactly 2 bytes (0-65535) -
    /// see GpsRoamingCodec's own doc comment. The vendor CPS's own edit
    /// popup allows typing more digits than that, but this is the real
    /// wire-format cap.</summary>
    [CustomValidation(typeof(GpsRoamingEntry), nameof(ValidateRadiusText))]
    public string RadiusText
    {
        get => Radius.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(RadiusText));
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var radius) && radius is >= 0 and <= 65535)
            {
                Radius = radius;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateRadiusText(string? value, ValidationContext context) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var radius) && radius is >= 0 and <= 65535
            ? ValidationResult.Success
            : new ValidationResult("Must be 0-65535");

    /// <summary>Radio-write baseline only - see TalkgroupEntry's own
    /// identically-shaped member. Deliberately excludes Number/ZoneDisplayName
    /// (display-only).</summary>
    private GpsRoamingSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private GpsRoamingSnapshot CreateRadioSnapshot() => new(
        Enabled, ZoneIndex, LatDegree, LatMinute, LatMinuteDecimal, NorthSouth,
        LongDegree, LongMinute, LongMinuteDecimal, EastWest, Radius);

    private sealed record GpsRoamingSnapshot(
        bool Enabled, int ZoneIndex, int LatDegree, int LatMinute, int LatMinuteDecimal, int NorthSouth,
        int LongDegree, int LongMinute, int LongMinuteDecimal, int EastWest, int Radius);

    partial void OnEnabledChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnZoneIndexChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnLatDegreeChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnLatMinuteChanged(int value)
    {
        OnPropertyChanged(nameof(LatMinuteText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnLatMinuteDecimalChanged(int value)
    {
        OnPropertyChanged(nameof(LatMinuteText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNorthSouthChanged(int value)
    {
        OnPropertyChanged(nameof(NorthSouthText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnLongDegreeChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnLongMinuteChanged(int value)
    {
        OnPropertyChanged(nameof(LongMinuteText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnLongMinuteDecimalChanged(int value)
    {
        OnPropertyChanged(nameof(LongMinuteText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnEastWestChanged(int value)
    {
        OnPropertyChanged(nameof(EastWestText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnRadiusChanged(int value)
    {
        OnPropertyChanged(nameof(RadiusText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
