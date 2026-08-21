using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// There's only ever one APRS Settings record on the radio - single
/// instance, not a collection, like Master ID/Talk Alias Settings/Alarm
/// Settings. <see cref="AdditionalFixLocations"/> (fix 2-8) and
/// <see cref="DigitalReports"/> (report channels 1-8) are fixed-count
/// sub-lists, always pre-populated to their full size - not user
/// add/removable, since the radio always has exactly that many slots.
/// The TxFreq1-8 live validation added 2026-08-07 is purely a UI-input
/// safety net (same dead-zone bug as Channel/Roaming Channel, see
/// CodeplugLimits.IsValidVhfOrUhfFrequencyMhz's own doc comment), ahead of
/// this entity's own eventual write-support build.
///
/// Radio-write dirty-tracking scaffolding added 2026-08-15 (this class's
/// own <c>HasAnyPendingRadioWrite</c>/<c>MarkRadioSynced</c>, aggregating
/// <see cref="AprsFixLocationEntry"/>/<see cref="AprsDigitalReportEntry"/>'s
/// own per-entry tracking). Write support (Encode/patch/wiring into
/// MainViewModel.RadioWrite.cs) was built the same day, after a full live-
/// capture pass (16 differential write tests, see Capture_Findings.md)
/// confirmed almost every field's real byte offset and found/fixed 4 real
/// bugs in the reference-project-ported layout (<c>Dcs</c>'s width,
/// <c>FixedLocationBeacon</c>'s address, the <c>TxFreq</c> slot shift, and
/// <c>Ew</c> never being transmitted for Fix4-8 - see
/// <see cref="AprsSettingsCodec"/>'s own doc comments for each). Filters and
/// <c>DigipeaterPath</c>'s exact meaning beyond its confirmed slices remain
/// the only real gaps.
/// </summary>
public partial class AprsSettingsEntry : ObservableValidator
{
    /// <summary>Radio-write baseline only - deliberately separate from any
    /// project-file "dirty" tracking (this entity has none of its own; the
    /// whole project's dirty flag is tracked at MainViewModel level
    /// instead), same split every other radio-write-capable entity's own
    /// <c>_radioSyncSnapshot</c> uses. Only set by <see cref="MarkRadioSynced"/>.
    /// Deliberately excludes <see cref="AdditionalFixLocations"/>/
    /// <see cref="DigitalReports"/> - those track their own dirty state
    /// per-entry (see <see cref="HasAnyPendingRadioWrite"/>), same pattern
    /// as any other entity with fixed-count sub-records.</summary>
    private AprsSettingsSnapshot? _radioSyncSnapshot;

    public AprsSettingsEntry()
    {
        for (var i = 2; i <= 8; i++)
        {
            AdditionalFixLocations.Add(new AprsFixLocationEntry { Number = i });
        }

        for (var i = 1; i <= 8; i++)
        {
            DigitalReports.Add(new AprsDigitalReportEntry { Number = i });
        }
    }

    public bool HasAnyPendingRadioWrite =>
        _radioSyncSnapshot is null
        || CreateRadioSnapshot() != _radioSyncSnapshot
        || AdditionalFixLocations.Any(fix => fix.HasAnyPendingRadioWrite)
        || DigitalReports.Any(report => report.HasAnyPendingRadioWrite);

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        foreach (var fix in AdditionalFixLocations)
        {
            fix.MarkRadioSynced();
        }

        foreach (var report in DigitalReports)
        {
            report.MarkRadioSynced();
        }

        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AprsSettingsSnapshot CreateRadioSnapshot() => new(
        TxFreq1Mhz, TxDelay, SendSubtone, Ctcss, Dcs, ManualTxInterval, AutoTxInterval, TxTone, FixedLocationBeacon,
        Fix1Lat, Fix1Ns, Fix1Lng, Fix1Ew,
        ToCall, ToCallSsid, YourCall, YourCallSsid, DigipeaterPath,
        AprsSymbol, MapIcon, TxPower, PrewaveTime,
        RoamingSupport, RepeaterActivationDelay, DisTime, Altitude, AnalogTxMode, PassAll,
        TxFreq2Mhz, TxFreq3Mhz, TxFreq4Mhz, TxFreq5Mhz, TxFreq6Mhz, TxFreq7Mhz, TxFreq8Mhz,
        SendingText,
        FilterPosition, FilterMicE, FilterObject, FilterItem, FilterMessage, FilterWxReport, FilterNmeaReport, FilterStatusReport, FilterOther);

    private sealed record AprsSettingsSnapshot(
        double TxFreq1Mhz, byte TxDelay, byte SendSubtone, byte Ctcss, int Dcs, byte ManualTxInterval, byte AutoTxInterval, byte TxTone, byte FixedLocationBeacon,
        double Fix1Lat, byte Fix1Ns, double Fix1Lng, byte Fix1Ew,
        string ToCall, byte ToCallSsid, string YourCall, byte YourCallSsid, string DigipeaterPath,
        string AprsSymbol, string MapIcon, byte TxPower, byte PrewaveTime,
        byte RoamingSupport, byte RepeaterActivationDelay, byte DisTime, int Altitude, byte AnalogTxMode, byte PassAll,
        double TxFreq2Mhz, double TxFreq3Mhz, double TxFreq4Mhz, double TxFreq5Mhz, double TxFreq6Mhz, double TxFreq7Mhz, double TxFreq8Mhz,
        string SendingText,
        bool FilterPosition, bool FilterMicE, bool FilterObject, bool FilterItem, bool FilterMessage, bool FilterWxReport, bool FilterNmeaReport, bool FilterStatusReport, bool FilterOther);

    [ObservableProperty] private double _txFreq1Mhz;
    [ObservableProperty] private byte _txDelay;
    [ObservableProperty] private byte _sendSubtone;
    [ObservableProperty] private byte _ctcss;
    [ObservableProperty] private int _dcs;
    [ObservableProperty] private byte _manualTxInterval;
    [ObservableProperty] private byte _autoTxInterval;
    [ObservableProperty] private byte _txTone;
    [ObservableProperty] private byte _fixedLocationBeacon;

    [ObservableProperty] private double _fix1Lat;
    [ObservableProperty] private byte _fix1Ns;
    [ObservableProperty] private double _fix1Lng;
    [ObservableProperty] private byte _fix1Ew;

    [ObservableProperty] private string _toCall = "";
    [ObservableProperty] private byte _toCallSsid;
    [ObservableProperty] private string _yourCall = "";
    [ObservableProperty] private byte _yourCallSsid;
    [ObservableProperty] private string _digipeaterPath = "";

    [ObservableProperty] private string _aprsSymbol = "";
    [ObservableProperty] private string _mapIcon = "";
    [ObservableProperty] private byte _txPower;
    [ObservableProperty] private byte _prewaveTime;

    [ObservableProperty] private byte _roamingSupport;
    [ObservableProperty] private byte _repeaterActivationDelay;
    [ObservableProperty] private byte _disTime;
    [ObservableProperty] private int _altitude;
    [ObservableProperty] private byte _analogTxMode;
    [ObservableProperty] private byte _passAll;

    [ObservableProperty] private double _txFreq2Mhz;
    [ObservableProperty] private double _txFreq3Mhz;
    [ObservableProperty] private double _txFreq4Mhz;
    [ObservableProperty] private double _txFreq5Mhz;
    [ObservableProperty] private double _txFreq6Mhz;
    [ObservableProperty] private double _txFreq7Mhz;
    [ObservableProperty] private double _txFreq8Mhz;

    [ObservableProperty] private string _sendingText = "";

    [ObservableProperty] private bool _filterPosition;
    [ObservableProperty] private bool _filterMicE;
    [ObservableProperty] private bool _filterObject;
    [ObservableProperty] private bool _filterItem;
    [ObservableProperty] private bool _filterMessage;
    [ObservableProperty] private bool _filterWxReport;
    [ObservableProperty] private bool _filterNmeaReport;
    [ObservableProperty] private bool _filterStatusReport;
    [ObservableProperty] private bool _filterOther;

    // --- General / TX Settings option lists and ComboBox-friendly Text
    // pass-throughs, transcribed 2026-08-15 directly from the
    // real vendor CPS UI (not the reference project, not guessed) - see
    // RESUME_HERE.md. The raw-byte<->option INDEX mapping itself (which
    // list entry a given byte value selects) has since been confirmed by
    // live differential writes for every field in this section (see each
    // one's own comment below for its exact confirmed address/encoding) -
    // Encode/patch code for all of them now exists in AprsSettingsCodec.

    // Byte offset CONFIRMED 2026-08-15 by live differential write:
    // AprsSettingsMainData + 0x06, 1 byte, plain sequential index into this
    // list (Off->0, CTC->1, DCS->2) - see Capture_Findings.md.
    public static IReadOnlyList<string> SendSubtoneOptions { get; } = ["Off", "CTC", "DCS"];

    // Same physical CTCSS tone table as Channel's own (confirmed
    // 2026-08-15 by directly comparing the two dropdowns in the
    // vendor CPS) - reusing ChannelEntry.CtcssToneLabels rather than a
    // second copy. Unlike Channel's own dropdown, APRS's does NOT offer
    // "Custom CTCSS" at all (confirmed), so this list stops at
    // CtcssToneCount (51), not the full label list. Byte offset CONFIRMED
    // 2026-08-15 by live differential write: AprsSettingsMainData + 0x07,
    // 1 byte, plain sequential index (matches this list's own order exactly:
    // "62.5"->0, "100.0"->13) - see Capture_Findings.md.
    public static IReadOnlyList<string> CtcssOptions { get; } = ChannelEntry.CtcssToneLabels.Take(ChannelEntry.CtcssToneCount).ToList();

    // Same physical DCS code table as Channel's own (confirmed 2026-08-15:
    // "D000N-D777I. Same as channel DCS"). Encoding CONFIRMED 2026-08-15 by
    // live differential write: AprsSettingsMainData + 0x08, 2 bytes
    // little-endian (NOT 4-byte as AprsSettingsCodec previously assumed -
    // that width mismatch is a real bug, now confirmed wrong and needing a
    // fix). Value = the DCS code's 3 octal digits read as a decimal number,
    // plus 0x200 if Inverted (e.g. D023N -> 19 (0x0013), D754I -> 492+512 =
    // 1004 (0x03EC)) - see Capture_Findings.md for the exact captured bytes.
    public static IReadOnlyList<string> DcsOptions { get; } = ChannelEntry.DcsCodeLabels;

    // 0-5100ms, steps of 20 (256 values, exact byte fit) - confirmed
    // 2026-08-15 directly from the vendor CPS (previously not located).
    // Byte offset and encoding CONFIRMED 2026-08-15 by live differential
    // write: AprsSettingsMainData + 0x05, 1 byte, value = ms / 20 (exactly
    // this list's own index formula) - see Capture_Findings.md.
    public static IReadOnlyList<string> TxDelayOptions { get; } =
        Enumerable.Range(0, 256).Select(step => (step * 20).ToString(CultureInfo.InvariantCulture)).ToList();

    // Byte offset CONFIRMED 2026-08-15 by live differential write:
    // AprsSettingsMainData + 0x0c, 1 byte, Off->0, On->1 - see
    // Capture_Findings.md.
    public static IReadOnlyList<string> TxToneOptions { get; } = ["Off", "On"];

    // Off + 60-3870s, steps of 15 (256 values, exact byte fit) -
    // confirmed 2026-08-15. Byte offset and encoding CONFIRMED
    // 2026-08-15 by live differential write: AprsSettingsMainData + 0x0b,
    // 1 byte, plain sequential index into this list (matches the formula
    // above exactly) - see Capture_Findings.md.
    public static IReadOnlyList<string> AutoTxIntervalOptions { get; } =
        ["Off", .. Enumerable.Range(0, 255).Select(step => (60 + step * 15).ToString(CultureInfo.InvariantCulture))];

    // Off + fix location 1-8 (9 values) - which Fix tab this beacon reports
    // from. Confirmed 2026-08-15: this is also what enables the
    // corresponding Fix tab in the vendor CPS (picking "2" here enables the
    // Fix2 tab, etc.) - resolves the Fix1/Fix3-8-disabled-tabs mystery from
    // RESUME_HERE.md. The exact Off-vs-a-cached-selection interaction seen
    // in the one available screenshot (Beacon showed "Off" but Fix2 was
    // still the enabled tab) isn't fully explained yet.
    // Byte offset CONFIRMED 2026-08-15 by live differential write - but NOT
    // in AprsSettingsMainData at all: it's at absolute address 0x0350014e,
    // inside the separate general-settings-ish block at 0x03500000-
    // 0x035001f0, 1 byte, plain sequential index (Off->0, "1"->1, etc.).
    // This is why it also gates the Fix1-8 tab enable state - it lives
    // outside AprsSettingsEntry's own data region, in whatever shared
    // struct that other block belongs to. See Capture_Findings.md.
    public static IReadOnlyList<string> FixedLocationBeaconOptions { get; } =
        ["Off", .. Enumerable.Range(1, 8).Select(n => n.ToString(CultureInfo.InvariantCulture))];

    // Same 4-item list as OptionalSettingsEntry.TxPowerOptions (Satellite TX
    // Power) - confirmed 2026-08-15 the vendor CPS shows the identical
    // Low/Mid/High/Turbo set here. Byte offset CONFIRMED 2026-08-15 by live
    // differential write: AprsSettingsMainData + 0x3b, 1 byte, plain
    // sequential index into this list - see Capture_Findings.md. (Corrected
    // from an initial +0x3c manual-indexing mistake in the first test pass
    // - +0x3c is actually PrewaveTime, confirmed in the second test pass.)
    public static IReadOnlyList<string> TxPowerOptions { get; } = ["Low", "Mid", "High", "Turbo"];

    // 0-2550ms, steps of 10 (256 values, exact byte fit, no "Off" prefix -
    // 0 is itself a valid value here) - confirmed 2026-08-15. Byte
    // offset and encoding CONFIRMED 2026-08-15 by live differential write:
    // AprsSettingsMainData + 0x3c, 1 byte, value = ms / 10 - see
    // Capture_Findings.md. (This is the address originally mis-attributed
    // to TxPower in the first test pass - see TxPowerOptions's own comment.)
    public static IReadOnlyList<string> PrewaveTimeOptions { get; } =
        Enumerable.Range(0, 256).Select(step => (step * 10).ToString(CultureInfo.InvariantCulture)).ToList();

    public static IReadOnlyList<string> RoamingSupportOptions { get; } = ["Off", "On"];

    // Off + 100-1000ms, steps of 100 (11 values) - confirmed 2026-08-15.
    public static IReadOnlyList<string> RepeaterActivationDelayOptions { get; } =
        ["Off", .. Enumerable.Range(1, 9).Select(step => (step * 100).ToString(CultureInfo.InvariantCulture))];

    public static IReadOnlyList<string> AnalogTxModeOptions { get; } = ["Narrow", "Wide"];

    // 3-15 + Infinity, steps of 1 (14 values) - matches the vendor CPS's
    // "APRSDisTime" field (General panel, shows as "5S" in the reference
    // screenshot) - confirmed 2026-08-15. Unit ("s") moved into the
    // field's own header per this app's usual "units in headers, not
    // values" rule, not appended to each item here. Byte offset and
    // encoding CONFIRMED 2026-08-15 by live differential write:
    // AprsSettingsMainData + 0x82, 1 byte, plain sequential index into this
    // list (index 13 = "Infinity") - see Capture_Findings.md.
    public static IReadOnlyList<string> DisTimeOptions { get; } =
        [.. Enumerable.Range(3, 13).Select(n => n.ToString(CultureInfo.InvariantCulture)), "Infinity"];
    public static IReadOnlyList<string> PassAllOptions { get; } = ["Off", "On"];

    // Fix 1 (Home Position) - confirmed 2026-08-15. Same North/South,
    // East/West option shape repeats for Fix 2-8 on AprsFixLocationEntry.
    public static IReadOnlyList<string> NsOptions { get; } = ["N", "S"];
    public static IReadOnlyList<string> EwOptions { get; } = ["E", "W"];

    // "-0" through "-15" (16 values) - the vendor CPS displays the SSID
    // with a "-" prefix (matches standard AX.25 CALL-SSID notation, and the
    // "Your SSID: -8" value seen directly in the vendor CPS screenshot) -
    // confirmed 2026-08-15. Shared by ToCallSsid/YourCallSsid.
    public static IReadOnlyList<string> SsidOptions { get; } =
        Enumerable.Range(0, 16).Select(n => $"-{n}").ToList();

    private static string LabelFor(byte value, IReadOnlyList<string> options) =>
        value < options.Count ? options[value] : value.ToString(CultureInfo.InvariantCulture);

    private static byte IndexFor(string value, IReadOnlyList<string> options, byte currentValue)
    {
        var index = options.ToList().IndexOf(value);
        return index >= 0 ? (byte)index : currentValue;
    }

    public string SendSubtoneText
    {
        get => LabelFor(SendSubtone, SendSubtoneOptions);
        set => SendSubtone = IndexFor(value, SendSubtoneOptions, SendSubtone);
    }

    public string CtcssText
    {
        get => LabelFor(Ctcss, CtcssOptions);
        set => Ctcss = IndexFor(value, CtcssOptions, Ctcss);
    }

    // Dcs is int (not byte), so this doesn't reuse LabelFor/IndexFor -
    // same 0-based-index-into-DcsOptions convention as Channel's own
    // CtcssDcsModeToString, but see DcsOptions' own doc comment on the
    // unconfirmed byte-width mismatch.
    public string DcsText
    {
        get => Dcs >= 0 && Dcs < DcsOptions.Count ? DcsOptions[Dcs] : Dcs.ToString(CultureInfo.InvariantCulture);
        set
        {
            var index = DcsOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Dcs = index;
            }
        }
    }

    public string TxDelayText
    {
        get => LabelFor(TxDelay, TxDelayOptions);
        set => TxDelay = IndexFor(value, TxDelayOptions, TxDelay);
    }

    public string AutoTxIntervalText
    {
        get => LabelFor(AutoTxInterval, AutoTxIntervalOptions);
        set => AutoTxInterval = IndexFor(value, AutoTxIntervalOptions, AutoTxInterval);
    }

    public string TxToneText
    {
        get => LabelFor(TxTone, TxToneOptions);
        set => TxTone = IndexFor(value, TxToneOptions, TxTone);
    }

    public string FixedLocationBeaconText
    {
        get => LabelFor(FixedLocationBeacon, FixedLocationBeaconOptions);
        set => FixedLocationBeacon = IndexFor(value, FixedLocationBeaconOptions, FixedLocationBeacon);
    }

    public string TxPowerText
    {
        get => LabelFor(TxPower, TxPowerOptions);
        set => TxPower = IndexFor(value, TxPowerOptions, TxPower);
    }

    public string PrewaveTimeText
    {
        get => LabelFor(PrewaveTime, PrewaveTimeOptions);
        set => PrewaveTime = IndexFor(value, PrewaveTimeOptions, PrewaveTime);
    }

    public string RoamingSupportText
    {
        get => LabelFor(RoamingSupport, RoamingSupportOptions);
        set => RoamingSupport = IndexFor(value, RoamingSupportOptions, RoamingSupport);
    }

    public string RepeaterActivationDelayText
    {
        get => LabelFor(RepeaterActivationDelay, RepeaterActivationDelayOptions);
        set => RepeaterActivationDelay = IndexFor(value, RepeaterActivationDelayOptions, RepeaterActivationDelay);
    }

    public string AnalogTxModeText
    {
        get => LabelFor(AnalogTxMode, AnalogTxModeOptions);
        set => AnalogTxMode = IndexFor(value, AnalogTxModeOptions, AnalogTxMode);
    }

    public string DisTimeText
    {
        get => LabelFor(DisTime, DisTimeOptions);
        set => DisTime = IndexFor(value, DisTimeOptions, DisTime);
    }

    public string PassAllText
    {
        get => LabelFor(PassAll, PassAllOptions);
        set => PassAll = IndexFor(value, PassAllOptions, PassAll);
    }

    public string Fix1NsText
    {
        get => LabelFor(Fix1Ns, NsOptions);
        set => Fix1Ns = IndexFor(value, NsOptions, Fix1Ns);
    }

    public string Fix1EwText
    {
        get => LabelFor(Fix1Ew, EwOptions);
        set => Fix1Ew = IndexFor(value, EwOptions, Fix1Ew);
    }

    public string ToCallSsidText
    {
        get => LabelFor(ToCallSsid, SsidOptions);
        set => ToCallSsid = IndexFor(value, SsidOptions, ToCallSsid);
    }

    public string YourCallSsidText
    {
        get => LabelFor(YourCallSsid, SsidOptions);
        set => YourCallSsid = IndexFor(value, SsidOptions, YourCallSsid);
    }

    // Not reject-and-revert, same reasoning as TxFreq*MhzText above -
    // 0-90/0-180 decimal degrees, confirmed 2026-08-15.
    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateLatitudeText))]
    public string Fix1LatText
    {
        get => Fix1Lat.ToString("00.00000", CultureInfo.InvariantCulture);
        set => SetDegreesText(value, nameof(Fix1LatText), 0, 90, v => Fix1Lat = v);
    }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateLongitudeText))]
    public string Fix1LngText
    {
        get => Fix1Lng.ToString("000.00000", CultureInfo.InvariantCulture);
        set => SetDegreesText(value, nameof(Fix1LngText), 0, 180, v => Fix1Lng = v);
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

    public static ValidationResult? ValidateLatitudeText(string? value, ValidationContext context) =>
        ValidateDegreesText(value, context, 0, 90);

    public static ValidationResult? ValidateLongitudeText(string? value, ValidationContext context) =>
        ValidateDegreesText(value, context, 0, 180);

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

    public ObservableCollection<AprsFixLocationEntry> AdditionalFixLocations { get; } = [];
    public ObservableCollection<AprsDigitalReportEntry> DigitalReports { get; } = [];

    // --- TxFreq1-8 text-entry wrappers ---
    // Deliberately NOT reject-and-revert - same reasoning as
    // ChannelEntry.RxFrequencyMHzText's own doc comment. The real valid
    // range is two disjoint bands (see CodeplugLimits.IsValidVhfOrUhfFrequencyMhz),
    // not a single continuous span.
    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq1MhzText { get => TxFreq1Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq1MhzText), v => TxFreq1Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq2MhzText { get => TxFreq2Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq2MhzText), v => TxFreq2Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq3MhzText { get => TxFreq3Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq3MhzText), v => TxFreq3Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq4MhzText { get => TxFreq4Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq4MhzText), v => TxFreq4Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq5MhzText { get => TxFreq5Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq5MhzText), v => TxFreq5Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq6MhzText { get => TxFreq6Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq6MhzText), v => TxFreq6Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq7MhzText { get => TxFreq7Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq7MhzText), v => TxFreq7Mhz = v); }

    [CustomValidation(typeof(AprsSettingsEntry), nameof(ValidateFrequencyText))]
    public string TxFreq8MhzText { get => TxFreq8Mhz.ToString("000.00000", CultureInfo.InvariantCulture); set => SetTxFreqMhz(value, nameof(TxFreq8MhzText), v => TxFreq8Mhz = v); }

    private void SetTxFreqMhz(string? value, string propertyName, Action<double> assign)
    {
        ValidateProperty(value, propertyName);
        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
            && CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(v))
        {
            assign(v);
        }

        OnPropertyChanged(nameof(HasErrors));
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

    partial void OnTxFreq1MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq1MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq2MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq2MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq3MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq3MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq4MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq4MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq5MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq5MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq6MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq6MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq7MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq7MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxFreq8MhzChanged(double value)
    {
        OnPropertyChanged(nameof(TxFreq8MhzText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // --- Radio-write dirty tracking for every other scalar field (see
    // _radioSyncSnapshot's doc comment above) ---
    partial void OnTxDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(TxDelayText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSendSubtoneChanged(byte value)
    {
        OnPropertyChanged(nameof(SendSubtoneText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnCtcssChanged(byte value)
    {
        OnPropertyChanged(nameof(CtcssText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDcsChanged(int value)
    {
        OnPropertyChanged(nameof(DcsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnManualTxIntervalChanged(byte value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnAutoTxIntervalChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoTxIntervalText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTxToneChanged(byte value)
    {
        OnPropertyChanged(nameof(TxToneText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFixedLocationBeaconChanged(byte value)
    {
        OnPropertyChanged(nameof(FixedLocationBeaconText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFix1LatChanged(double value)
    {
        OnPropertyChanged(nameof(Fix1LatText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFix1NsChanged(byte value)
    {
        OnPropertyChanged(nameof(Fix1NsText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFix1LngChanged(double value)
    {
        OnPropertyChanged(nameof(Fix1LngText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnFix1EwChanged(byte value)
    {
        OnPropertyChanged(nameof(Fix1EwText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnToCallChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnToCallSsidChanged(byte value)
    {
        OnPropertyChanged(nameof(ToCallSsidText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnYourCallChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnYourCallSsidChanged(byte value)
    {
        OnPropertyChanged(nameof(YourCallSsidText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigipeaterPathChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnAprsSymbolChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnMapIconChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnTxPowerChanged(byte value)
    {
        OnPropertyChanged(nameof(TxPowerText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPrewaveTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(PrewaveTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnRoamingSupportChanged(byte value)
    {
        OnPropertyChanged(nameof(RoamingSupportText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnRepeaterActivationDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterActivationDelayText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDisTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(DisTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
    partial void OnAltitudeChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnAnalogTxModeChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogTxModeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPassAllChanged(byte value)
    {
        OnPropertyChanged(nameof(PassAllText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSendingTextChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    partial void OnFilterPositionChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterMicEChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterObjectChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterItemChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterMessageChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterWxReportChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterNmeaReportChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterStatusReportChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnFilterOtherChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
