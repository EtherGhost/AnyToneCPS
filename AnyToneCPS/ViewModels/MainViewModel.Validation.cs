using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AnyToneCPS.Models;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// Validation for the new codeplug entity types (RadioId, Talkgroup,
/// ScanList, RoamingChannel, RoamingZone, ReceiveGroupList), added alongside
/// the pre-existing ValidateChannels()/ValidateZones() in MainViewModel.cs
/// (left in place there rather than moved, to avoid risk to working code).
/// Follows the exact same conventions: a flat ValidationMessages string
/// list, "Warning: " prefix for soft/advisory issues, hard messages for
/// everything else. Limits come from Models/CodeplugLimits.cs, which in turn
/// cites Docs/AnyTone_D890UV/Field_Reference.md for each number.
/// </summary>
public partial class MainViewModel
{
    private void ValidateRadioIds()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in RadioIds)
        {
            if (entry.Number is < 1 || entry.Number > CodeplugLimits.RadioIdListMax)
            {
                ValidationMessages.Add($"Radio ID: number {entry.Number} must be 1-{CodeplugLimits.RadioIdListMax}");
            }

            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Radio ID {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Radio ID {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.RadioIdNameMaxLength)
            {
                ValidationMessages.Add($"Radio ID {entry.Number}: name exceeds {CodeplugLimits.RadioIdNameMaxLength} characters");
            }

            // Blocking, not "Warning:" - see DmrIdValidation's own doc
            // comment for why an out-of-range DMR ID can no longer reach
            // Save/Write at all, not just get a soft note in this panel.
            foreach (var error in entry.GetErrors(nameof(entry.DmrIdText)))
            {
                ValidationMessages.Add($"Radio ID {entry.Number}: {error.ErrorMessage}");
            }
        }

        if (RadioIds.Count > CodeplugLimits.RadioIdListMax)
        {
            ValidationMessages.Add($"Warning: {RadioIds.Count} Radio IDs defined, AnyTone lists support max {CodeplugLimits.RadioIdListMax}");
        }
    }

    private void ValidateTalkgroups()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in Talkgroups)
        {
            if (entry.Number is < 1 || entry.Number > CodeplugLimits.TalkgroupListMax)
            {
                ValidationMessages.Add($"Talkgroup: number {entry.Number} must be 1-{CodeplugLimits.TalkgroupListMax}");
            }

            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Talkgroup {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Talkgroup {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"Talkgroup {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            // Blocking, not "Warning:" - see DmrIdValidation's own doc
            // comment. TalkgroupEntry.ValidateDmrIdText already bypasses
            // this for CallType == "All Call" (DMR ID 16777215, the sentinel
            // TalkgroupCodec.Encode forces at write time).
            foreach (var error in entry.GetErrors(nameof(entry.DmrIdText)))
            {
                ValidationMessages.Add($"Talkgroup {entry.Number}: {error.ErrorMessage}");
            }

            if (entry.CallType is not ("Group Call" or "Private Call" or "All Call"))
            {
                ValidationMessages.Add($"Warning: Talkgroup {entry.Number}: unknown call type '{entry.CallType}'");
            }

            // "Ring" only ever appears as an option for Private Call (see
            // TalkgroupEntry.CallAlertOptions) - a project file hand-edited
            // or loaded from an older/foreign source could still combine
            // them, which the vendor CPS itself never allows.
            if (entry.CallAlert == "Ring" && entry.CallType != "Private Call")
            {
                ValidationMessages.Add($"Warning: Talkgroup {entry.Number}: Call Alert 'Ring' is only valid for Private Call");
            }
        }

        // Confirmed mandatory in the vendor CPS 2026-08-07: the Talkgroup
        // list must contain at least one Group Call entry once it's non-
        // empty (an all Private/All Call list is rejected).
        if (Talkgroups.Count > 0 && Talkgroups.All(entry => entry.CallType != "Group Call"))
        {
            ValidationMessages.Add("Talkgroup list must include at least one Group Call entry");
        }

        if (Talkgroups.Count > CodeplugLimits.TalkgroupListMax)
        {
            ValidationMessages.Add($"Warning: {Talkgroups.Count} Talkgroups defined, AnyTone lists support max {CodeplugLimits.TalkgroupListMax}");
        }
    }

    private void ValidateScanLists()
    {
        var numbers = new HashSet<int>();

        foreach (var entry in ScanLists)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Scan List {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Scan List {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"Scan List {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            if (entry.Members.Count == 0)
            {
                ValidationMessages.Add($"Warning: Scan List {entry.Number}: no channel members");
            }

            if (entry.Members.Count > CodeplugLimits.ScanListMemberMax)
            {
                ValidationMessages.Add($"Scan List {entry.Number}: channel list is limited to {CodeplugLimits.ScanListMemberMax} entries by the radio's memory layout");
            }

            // PriorityChannel1/2 are now real ChannelEntry references (like
            // Zone.AChannel/BChannel) - they can't hold a dangling index, so
            // there's nothing left to validate there. PriorityChannelSelect's
            // 4-value range (Off/Priority 1/Priority 2/Both) and byte offset
            // are both confirmed via a 2026-07-19 live differential test
            // (see ScanListEntry.PriorityChannelSelectOptions's doc comment),
            // so this is a real range check now, not a softened guess.
            if (entry.PriorityChannelSelect < 0 || entry.PriorityChannelSelect >= ScanListEntry.PriorityChannelSelectOptions.Count)
            {
                ValidationMessages.Add($"Scan List {entry.Number}: priority channel select value {entry.PriorityChannelSelect} is outside the expected 0-{ScanListEntry.PriorityChannelSelectOptions.Count - 1} range");
            }
        }

        if (ScanLists.Count > CodeplugLimits.ScanListMax)
        {
            ValidationMessages.Add($"Warning: {ScanLists.Count} Scan Lists defined, AnyTone lists support max {CodeplugLimits.ScanListMax}");
        }
    }

    private void ValidateRoamingChannels()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in RoamingChannels)
        {
            if (entry.Number is < 1 || entry.Number > CodeplugLimits.RoamingChannelMax)
            {
                ValidationMessages.Add($"Roaming Channel: number {entry.Number} must be 1-{CodeplugLimits.RoamingChannelMax}");
            }

            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            // Fixed 2026-08-07 via a live differential write capture (see
            // RoamingChannelCodec's own doc comment): Color Code's real
            // range is 0-15 plus a 16th "No Use" value (16); Slot is a raw
            // 0-indexed byte - 0=Slot 1, 1=Slot 2, 2="No Use" - not the
            // 1-or-2 this check used to assume (which would have rejected
            // the real Slot 1 encoding of 0).
            if (entry.ColorCode < CodeplugLimits.ColorCodeMin || entry.ColorCode > CodeplugLimits.RoamingChannelColorCodeNoUseValue)
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: color code {entry.ColorCode} is outside the expected {CodeplugLimits.ColorCodeMin}-{CodeplugLimits.RoamingChannelColorCodeNoUseValue} range");
            }

            if (entry.Slot < 0 || entry.Slot > CodeplugLimits.RoamingChannelSlotNoUseValue)
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: slot value {entry.Slot} is not a valid Slot 1/Slot 2/No Use value");
            }

            // Corrected 2026-08-07: a single continuous range silently
            // accepted the 174-400 MHz dead zone between this radio's real
            // VHF/UHF coverage - see CodeplugLimits.IsValidVhfOrUhfFrequencyMhz's
            // own doc comment, confirmed by this exact entity's own live capture.
            if (!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(entry.RxFrequencyMhz))
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: RX frequency is outside the {CodeplugLimits.VhfFrequencyMinMhz}-{CodeplugLimits.VhfFrequencyMaxMhz} or {CodeplugLimits.UhfFrequencyMinMhz}-{CodeplugLimits.UhfFrequencyMaxMhz} MHz range");
            }

            if (!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(entry.TxFrequencyMhz))
            {
                ValidationMessages.Add($"Roaming Channel {entry.Number}: TX frequency is outside the {CodeplugLimits.VhfFrequencyMinMhz}-{CodeplugLimits.VhfFrequencyMaxMhz} or {CodeplugLimits.UhfFrequencyMinMhz}-{CodeplugLimits.UhfFrequencyMaxMhz} MHz range");
            }
        }

        if (RoamingChannels.Count > CodeplugLimits.RoamingChannelMax)
        {
            ValidationMessages.Add($"Warning: {RoamingChannels.Count} Roaming Channels defined, AnyTone lists support max {CodeplugLimits.RoamingChannelMax}");
        }
    }

    private void ValidateRoamingZones()
    {
        var numbers = new HashSet<int>();

        if (RoamingZones.Count > CodeplugLimits.RoamingZoneMax)
        {
            ValidationMessages.Add($"Warning: {RoamingZones.Count} Roaming Zones defined, the radio supports max {CodeplugLimits.RoamingZoneMax}");
        }

        foreach (var entry in RoamingZones)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Roaming Zone {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Roaming Zone {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"Roaming Zone {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            if (entry.Members.Count == 0)
            {
                ValidationMessages.Add($"Warning: Roaming Zone {entry.Number}: no roaming channel members");
            }
            else if (entry.Members.Count > CodeplugLimits.RoamingZoneMemberMax)
            {
                ValidationMessages.Add($"Roaming Zone {entry.Number}: {entry.Members.Count} members, the radio supports max {CodeplugLimits.RoamingZoneMemberMax} per zone");
            }
        }
    }

    private void ValidateAutoRepeaterOffsets()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in AutoRepeaterOffsets)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Auto Repeater Offset {entry.Number}: duplicate number");
            }

            if (entry.OffsetFrequencyMhz < CodeplugLimits.AutoRepeaterOffsetFrequencyMinMhz || entry.OffsetFrequencyMhz > CodeplugLimits.AutoRepeaterOffsetFrequencyMaxMhz)
            {
                ValidationMessages.Add($"Auto Repeater Offset {entry.Number}: offset frequency {entry.OffsetFrequencyMhz} MHz is outside the {CodeplugLimits.AutoRepeaterOffsetFrequencyMinMhz}-{CodeplugLimits.AutoRepeaterOffsetFrequencyMaxMhz} MHz range");
            }
        }

        if (AutoRepeaterOffsets.Count > CodeplugLimits.AutoRepeaterOffsetMax)
        {
            ValidationMessages.Add($"Warning: {AutoRepeaterOffsets.Count} Auto Repeater Offsets defined, the radio only has {CodeplugLimits.AutoRepeaterOffsetMax} slots");
        }
    }

    /// <summary>Only a duplicate-number check - Field_Reference.md §17's GPS
    /// Roaming section turned out to document a different vendor CPS screen
    /// (Repeater Check/Auto Roaming settings) than the 32-slot per-location
    /// list this entity actually models (confirmed via the vendor HPT's
    /// "ListGPS Roaming"/"Zone" topics, which describe switching the radio's
    /// current Zone by GPS location) - so there's no confirmed doc section
    /// to validate ZoneIndex/Radius ranges against yet. Not guessing.</summary>
    private void ValidateGpsRoaming()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in GpsRoamingEntries)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"GPS Roaming {entry.Number}: duplicate number");
            }

            if (entry.Number < 1 || entry.Number > GpsRoamingCodec.EntryCount)
            {
                ValidationMessages.Add($"Warning: GPS Roaming {entry.Number}: number is outside the radio's {GpsRoamingCodec.EntryCount} fixed slots");
            }

            // Blocking, not "Warning:" - see LatMinuteText/LongMinuteText/
            // RadiusText's own ValidationAttribute doc comments.
            foreach (var error in entry.GetErrors(nameof(entry.LatMinuteText))
                .Concat(entry.GetErrors(nameof(entry.LongMinuteText)))
                .Concat(entry.GetErrors(nameof(entry.RadiusText))))
            {
                ValidationMessages.Add($"GPS Roaming {entry.Number}: {error.ErrorMessage}");
            }
        }
    }

    private void ValidateReceiveGroupLists()
    {
        var numbers = new HashSet<int>();
        var talkgroupNumbers = Talkgroups.Select(t => (long)t.Number).ToHashSet();

        foreach (var entry in ReceiveGroupLists)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Receive Group List {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Receive Group List {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"Receive Group List {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            if (entry.TalkgroupIndexes.Count == 0)
            {
                ValidationMessages.Add($"Warning: Receive Group List {entry.Number}: no talkgroup members (at least one group call contact expected)");
            }

            if (entry.TalkgroupIndexes.Count > CodeplugLimits.ReceiveGroupListMemberMax)
            {
                ValidationMessages.Add($"Receive Group List {entry.Number}: talkgroup list is limited to {CodeplugLimits.ReceiveGroupListMemberMax} entries by the radio's memory layout");
            }

            // Vendor CPS help text (ListReceiveGroupCallList topic): "If the
            // Talk Group List contains a TG with the same number as another
            // one, then this Receive Group List will not work" - a real
            // functional break on the radio, not just a soft duplicate.
            var seenTalkgroupIndexes = new HashSet<long>();
            foreach (var memberIndex in entry.TalkgroupIndexes)
            {
                if (!seenTalkgroupIndexes.Add(memberIndex))
                {
                    ValidationMessages.Add($"Receive Group List {entry.Number}: talkgroup index {memberIndex} appears more than once - the radio will not use this list correctly");
                }

                if (!talkgroupNumbers.Contains(memberIndex + 1))
                {
                    ValidationMessages.Add($"Warning: Receive Group List {entry.Number}: talkgroup index {memberIndex} does not match a known talkgroup");
                }
            }
        }

        if (ReceiveGroupLists.Count > CodeplugLimits.ReceiveGroupListMax)
        {
            ValidationMessages.Add($"Warning: {ReceiveGroupLists.Count} Receive Group Lists defined, AnyTone lists support max {CodeplugLimits.ReceiveGroupListMax}");
        }
    }

    /// <summary>Range checks only - both fields are confirmed 3-value enums
    /// (see TalkAliasSettingsCodec's own doc comment for the live capture):
    /// DisplayPriority 0=Off/1=Contact Alias/2=Air Alias DMR/NX,
    /// DataFormat 0=ISO 8/1=ISO 7/2=Unicode. The ComboBox-driven Text
    /// wrapper properties can't themselves produce an out-of-range byte -
    /// this only catches a stale project file or a raw project-JSON edit.</summary>
    private void ValidateTalkAliasSettings()
    {
        if (TalkAliasSettings.DisplayPriority > 2)
        {
            ValidationMessages.Add($"Warning: Talk Alias Settings: display priority value {TalkAliasSettings.DisplayPriority} is outside the expected 0-2 range");
        }

        if (TalkAliasSettings.DataFormat > 2)
        {
            ValidationMessages.Add($"Warning: Talk Alias Settings: data format value {TalkAliasSettings.DataFormat} is outside the expected 0-2 range");
        }
    }

    private void ValidateMasterId()
    {
        if (!MasterId.Used)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(MasterId.Name))
        {
            ValidationMessages.Add("Master ID: missing name");
        }
        else if (MasterId.Name.Length > CodeplugLimits.MasterIdNameMaxLength)
        {
            ValidationMessages.Add($"Master ID: name exceeds {CodeplugLimits.MasterIdNameMaxLength} characters");
        }

        // Blocking, not "Warning:" - see DmrIdValidation's own doc comment.
        foreach (var error in MasterId.GetErrors(nameof(MasterId.DmrIdText)))
        {
            ValidationMessages.Add($"Master ID: {error.ErrorMessage}");
        }
    }

    /// <summary>Only the digital talkgroup/private-call DMR ID and
    /// AnalogEniType (now with a real ComboBox - see AlarmSettingsEntry)
    /// have a confirmed range to check - everything else in Alarm Settings
    /// is raw bytes with no documented range (or, for the two EniSend
    /// fields, a confirmed label set but a conflicting order between
    /// sources - see AlarmSettingsEntry's doc comment), so left
    /// unvalidated rather than guessing.</summary>
    private void ValidateAlarmSettings()
    {
        if (AlarmSettings.AnalogEniType >= AlarmSettingsEntry.EniTypeOptions.Count)
        {
            ValidationMessages.Add($"Warning: Alarm Settings: ENI type value {AlarmSettings.AnalogEniType} is outside the expected 0-{AlarmSettingsEntry.EniTypeOptions.Count - 1} range");
        }

        // Blocking, not "Warning:" - see DmrIdValidation's own doc comment.
        // AlarmSettingsEntry.ValidateDigitalTgDmrIdText already bypasses
        // this for 0 ("off").
        foreach (var error in AlarmSettings.GetErrors(nameof(AlarmSettings.DigitalTgDmrIdText)))
        {
            ValidationMessages.Add($"Alarm Settings: {error.ErrorMessage}");
        }
    }

    /// <summary>Only latitude/longitude range (a basic geographic fact, not
    /// a vendor-specific guess) and digital-report DMR IDs are validated -
    /// most other fields are raw bytes with no documented meaning/range.</summary>
    private void ValidateAprsSettings()
    {
        ValidateLatLng("APRS Fix 1", AprsSettings.Fix1Lat, AprsSettings.Fix1Lng);

        foreach (var fix in AprsSettings.AdditionalFixLocations)
        {
            ValidateLatLng($"APRS Fix {fix.Number}", fix.Lat, fix.Lng);
        }

        foreach (var report in AprsSettings.DigitalReports)
        {
            // Blocking, not "Warning:" - see DmrIdValidation's own doc
            // comment. AprsDigitalReportEntry.ValidateTalkgroupIdText
            // already bypasses this for 0 ("off").
            foreach (var error in report.GetErrors(nameof(report.TalkgroupIdText)))
            {
                ValidationMessages.Add($"APRS Digital Report {report.Number}: {error.ErrorMessage}");
            }
        }

        // Added 2026-08-07 alongside the app-wide VHF/UHF dead-zone fix -
        // TxFreq1MhzText etc. already validate live per-keystroke
        // (CodeplugLimits.IsValidVhfOrUhfFrequencyMhz), but this list-level
        // check is what actually blocks Save/Write, matching every other
        // frequency field in the app. 0 means "unset" (this entity has no
        // write support yet, so its own default state is untouched zeros),
        // same skip-when-zero reasoning as the DigitalReport check above.
        ValidateAprsTxFrequency(1, AprsSettings.TxFreq1Mhz);
        ValidateAprsTxFrequency(2, AprsSettings.TxFreq2Mhz);
        ValidateAprsTxFrequency(3, AprsSettings.TxFreq3Mhz);
        ValidateAprsTxFrequency(4, AprsSettings.TxFreq4Mhz);
        ValidateAprsTxFrequency(5, AprsSettings.TxFreq5Mhz);
        ValidateAprsTxFrequency(6, AprsSettings.TxFreq6Mhz);
        ValidateAprsTxFrequency(7, AprsSettings.TxFreq7Mhz);
        ValidateAprsTxFrequency(8, AprsSettings.TxFreq8Mhz);
    }

    private void ValidateAprsTxFrequency(int slot, double mhz)
    {
        if (mhz == 0)
        {
            return;
        }

        if (!CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(mhz))
        {
            ValidationMessages.Add($"APRS TX Freq {slot}: {mhz} MHz is outside the {CodeplugLimits.VhfFrequencyMinMhz}-{CodeplugLimits.VhfFrequencyMaxMhz} or {CodeplugLimits.UhfFrequencyMinMhz}-{CodeplugLimits.UhfFrequencyMaxMhz} MHz range");
        }
    }

    private void ValidateLatLng(string label, double lat, double lng)
    {
        if (lat is < 0 or > 90)
        {
            ValidationMessages.Add($"Warning: {label}: latitude {lat} is outside the expected 0-90 range");
        }

        if (lng is < 0 or > 180)
        {
            ValidationMessages.Add($"Warning: {label}: longitude {lng} is outside the expected 0-180 range");
        }
    }

    private void ValidateAprsReceiveFilters()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in AprsReceiveFilters)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"APRS Receive Filter {entry.Number}: duplicate number");
            }
        }
    }

    /// <summary>Optional Settings is a ~230-field raw-byte blob with no
    /// validator at all before this - most fields have no documented
    /// meaning to check. These 7 are the exception: real, vendor-confirmed
    /// enums (Docs/AnyTone_D890UV/field_options.json), now also given real
    /// ComboBoxes (see OptionalSettingsEntry's *Options/*Text properties)
    /// instead of raw byte TextBoxes - so a value outside the documented
    /// option count can only happen via an old project file or a fresh
    /// radio read, not through the UI itself, hence "Warning:" not a
    /// blocking error.</summary>
    private void ValidateOptionalSettings()
    {
        CheckOptionalSettingsEnumRange("VOX Level", OptionalSettings.VoxLevel, OptionalSettingsEntry.VoxLevelOptions.Count);
        CheckOptionalSettingsEnumRange("Language", OptionalSettings.Language, OptionalSettingsEntry.LanguageOptions.Count);
        CheckOptionalSettingsEnumRange("Time Display", OptionalSettings.TimeDisplay, OptionalSettingsEntry.TimeDisplayOptions.Count);
        CheckOptionalSettingsEnumRange("Distance Unit", OptionalSettings.DistanceUnit, OptionalSettingsEntry.DistanceUnitOptions.Count);
        CheckOptionalSettingsEnumRange("GPS Mode", OptionalSettings.GpsMode, OptionalSettingsEntry.GpsModeOptions.Count);
        CheckOptionalSettingsEnumRange("Encryption Type", OptionalSettings.EncryptionType, OptionalSettingsEntry.EncryptionTypeOptions.Count);
        CheckOptionalSettingsEnumRange("VF/MR A", OptionalSettings.VfMrA, OptionalSettingsEntry.VfoMemModeOptions.Count);
        CheckOptionalSettingsEnumRange("VF/MR B", OptionalSettings.VfMrB, OptionalSettingsEntry.VfoMemModeOptions.Count);

        // Text-entry fields with real validation attributes (VFO Scan's 4
        // band-limited frequencies, the 8 Auto Repeater min/max frequencies)
        // - see OptionalSettingsEntry's ObservableValidator conversion doc
        // comment on VfoScanStartFreqUhfText. These are blocking, not
        // "Warning:", because an out-of-range value here can't be written
        // to the radio meaningfully.
        foreach (var error in OptionalSettings.GetErrors())
        {
            ValidationMessages.Add($"Radio Settings: {error.ErrorMessage}");
        }

        // AlertToneEntry (the Alert Tone tab's 25 Frequency/Period slots)
        // got the same ObservableValidator conversion 2026-07-31, for
        // consistency - see AlertToneEntry.FrequencyText's doc comment.
        foreach (var tone in OptionalSettings.AlertTones)
        {
            if (!tone.HasErrors)
            {
                continue;
            }

            var categoryLabel = tone.Category switch
            {
                "CallPermit" => "Call Permit Tone",
                "CallEnd" => "Match End Tone",
                "CallReset" => "Call Reset Tone",
                "UnMatchEnd" => "UnMatch End Tone",
                "CallAll" => "All Call End Tone",
                _ => tone.Category
            };

            foreach (var error in tone.GetErrors())
            {
                ValidationMessages.Add($"Radio Settings: Alert Tone - {categoryLabel} #{tone.ToneNumber}: {error.ErrorMessage}");
            }
        }
    }

    private void CheckOptionalSettingsEnumRange(string label, byte value, int optionCount)
    {
        if (value >= optionCount)
        {
            ValidationMessages.Add($"Warning: Optional Settings: {label} value {value} is outside the expected 0-{optionCount - 1} range");
        }
    }

    private void ValidateAnalogAddresses()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in AnalogAddresses)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Analog Address {entry.Number}: duplicate number");
            }

            if (entry.Number < 1 || entry.Number > CodeplugLimits.AnalogAddressMax)
            {
                ValidationMessages.Add($"Analog Address {entry.Number}: number must be 1-{CodeplugLimits.AnalogAddressMax}");
            }

            if (entry.AddressNumber.ToString(CultureInfo.InvariantCulture).Length > CodeplugLimits.AnalogAddressNumberMaxDigits)
            {
                ValidationMessages.Add($"Analog Address {entry.Number}: address number exceeds {CodeplugLimits.AnalogAddressNumberMaxDigits} digits");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"Analog Address {entry.Number}: missing name");
            }
            // Shorter than the generic NameMaxLength - this entity's wire
            // name field is one character shorter than every other entity's
            // (see CodeplugLimits.AnalogAddressNameMaxLength's doc comment).
            else if (entry.Name.Length > CodeplugLimits.AnalogAddressNameMaxLength)
            {
                ValidationMessages.Add($"Analog Address {entry.Number}: name exceeds {CodeplugLimits.AnalogAddressNameMaxLength} characters");
            }
        }
    }

    private void ValidateTalkgroupWhitelist()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in TalkgroupWhitelist)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Talkgroup Whitelist {entry.Number}: duplicate number");
            }

            if (entry.Number < 1 || entry.Number > CodeplugLimits.WhitelistSlotMax)
            {
                ValidationMessages.Add($"Warning: Talkgroup Whitelist {entry.Number}: number is outside the expected 1-{CodeplugLimits.WhitelistSlotMax} range");
            }

            // Blocking, not "Warning:" - see DmrIdValidation's own doc comment.
            foreach (var error in entry.GetErrors(nameof(entry.DmrIdText)))
            {
                ValidationMessages.Add($"Talkgroup Whitelist {entry.Number}: {error.ErrorMessage}");
            }
        }

        if (TalkgroupWhitelist.Count > CodeplugLimits.WhitelistSlotMax)
        {
            ValidationMessages.Add($"Warning: {TalkgroupWhitelist.Count} whitelist entries defined - the radio only supports up to {CodeplugLimits.WhitelistSlotMax} ({CodeplugLimits.WhitelistSlotMax / 2} blocks x 2 entries)");
        }
    }

    private void ValidateDigitalContactWhitelist()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in DigitalContactWhitelist)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Digital Contact Whitelist {entry.Number}: duplicate number");
            }

            if (entry.Number < 1 || entry.Number > CodeplugLimits.WhitelistSlotMax)
            {
                ValidationMessages.Add($"Warning: Digital Contact Whitelist {entry.Number}: number is outside the expected 1-{CodeplugLimits.WhitelistSlotMax} range");
            }

            // Blocking, not "Warning:" - see DmrIdValidation's own doc comment.
            foreach (var error in entry.GetErrors(nameof(entry.DmrIdText)))
            {
                ValidationMessages.Add($"Digital Contact Whitelist {entry.Number}: {error.ErrorMessage}");
            }
        }

        if (DigitalContactWhitelist.Count > CodeplugLimits.WhitelistSlotMax)
        {
            ValidationMessages.Add($"Warning: {DigitalContactWhitelist.Count} whitelist entries defined - the radio only supports up to {CodeplugLimits.WhitelistSlotMax} ({CodeplugLimits.WhitelistSlotMax / 2} blocks x 2 entries)");
        }
    }

    /// <summary>Just the Friends List cap check - see CodeplugLimits.DigitalContactFriendsMax's
    /// own doc comment. Deliberately the only Digital Contact validation:
    /// this list can be 500,000 rows, so anything per-entry here would run
    /// on every keystroke elsewhere in the app via RefreshValidation.</summary>
    private void ValidateDigitalContacts()
    {
        var friendCount = DigitalContacts.Count(c => c.IsFriend);
        if (friendCount > CodeplugLimits.DigitalContactFriendsMax)
        {
            ValidationMessages.Add($"Warning: {friendCount} contacts marked as Friend - the radio only supports up to {CodeplugLimits.DigitalContactFriendsMax}");
        }
    }

    private void ValidatePrefabricatedSms()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in PrefabricatedSmsMessages)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"Prefabricated SMS {entry.Number}: duplicate number");
            }

            if (entry.Text.Length > CodeplugLimits.PrefabricatedSmsTextMaxLength)
            {
                ValidationMessages.Add($"Prefabricated SMS {entry.Number}: text exceeds {CodeplugLimits.PrefabricatedSmsTextMaxLength} characters");
            }
        }

        if (PrefabricatedSmsMessages.Count > PrefabricatedSmsCodec.SlotCount)
        {
            ValidationMessages.Add($"Warning: {PrefabricatedSmsMessages.Count} SMS messages defined, radio supports max {PrefabricatedSmsCodec.SlotCount}");
        }
    }

    private void ValidateAmAir()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in AmAirChannels)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"AM Air {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"AM Air {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"AM Air {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            // Vendor CPS help text (FrmAM/Frequency topic): "Allows AM air
            // band frequency 108-145MHz."
            if (entry.FrequencyMhz < CodeplugLimits.AmAirFrequencyMinMhz || entry.FrequencyMhz > CodeplugLimits.AmAirFrequencyMaxMhz)
            {
                ValidationMessages.Add($"AM Air {entry.Number}: frequency {entry.FrequencyMhz} MHz is outside the {CodeplugLimits.AmAirFrequencyMinMhz}-{CodeplugLimits.AmAirFrequencyMaxMhz} MHz AM air band");
            }
        }

        if (AmAirChannels.Count > CodeplugLimits.AmAirMax)
        {
            ValidationMessages.Add($"Warning: {AmAirChannels.Count} AM Air channels defined, AnyTone lists support max {CodeplugLimits.AmAirMax}");
        }
    }

    private void ValidateAmZones()
    {
        var numbers = new HashSet<int>();

        foreach (var entry in AmZones)
        {
            if (entry.Number <= 0 || entry.Number > CodeplugLimits.AmZoneMax)
            {
                ValidationMessages.Add($"AM Zone {entry.Number}: zone number must be 1-{CodeplugLimits.AmZoneMax}");
            }

            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"AM Zone {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"AM Zone {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"AM Zone {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }
        }

        if (AmZones.Count > CodeplugLimits.AmZoneMax)
        {
            ValidationMessages.Add($"AM Zone: {AmZones.Count} zones defined, the radio only has {CodeplugLimits.AmZoneMax} zone slots");
        }
    }

    private void ValidateFmChannels()
    {
        var numbers = new HashSet<int>();
        foreach (var entry in FmChannels)
        {
            if (!numbers.Add(entry.Number))
            {
                ValidationMessages.Add($"FM Channel {entry.Number}: duplicate number");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                ValidationMessages.Add($"FM Channel {entry.Number}: missing name");
            }
            else if (entry.Name.Length > CodeplugLimits.NameMaxLength)
            {
                ValidationMessages.Add($"FM Channel {entry.Number}: name exceeds {CodeplugLimits.NameMaxLength} characters");
            }

            if (entry.FrequencyMhz < CodeplugLimits.FmChannelFrequencyMinMhz || entry.FrequencyMhz > CodeplugLimits.FmChannelFrequencyMaxMhz)
            {
                ValidationMessages.Add($"FM Channel {entry.Number}: frequency {entry.FrequencyMhz} MHz is outside the {CodeplugLimits.FmChannelFrequencyMinMhz}-{CodeplugLimits.FmChannelFrequencyMaxMhz} MHz FM broadcast band");
            }
        }

        if (FmChannels.Count > CodeplugLimits.FmChannelMax)
        {
            ValidationMessages.Add($"Warning: {FmChannels.Count} FM channels defined, the radio only has {CodeplugLimits.FmChannelMax} FM channel slots");
        }
    }

    /// <summary>Confirmed 2026-08-05: Self ID must be 5-7 digits -
    /// the real vendor CPS itself doesn't enforce this (a bug in the
    /// vendor CPS), but the true constraint is real
    /// (every 5Tone Special Call popup's own Other Side ID field must
    /// exactly match Self ID's length, which only makes sense if Self ID
    /// itself is never shorter than 5 or longer than 7). Blank Self ID
    /// (never configured) is not flagged - same "don't validate an
    /// unconfigured field" convention as every other optional text field
    /// in this app.</summary>
    private void ValidateFiveTone()
    {
        if (!string.IsNullOrEmpty(FiveToneSettings.SelfId) && FiveToneSettings.SelfId.Length is < 5 or > 7)
        {
            ValidationMessages.Add($"5Tone Settings: Self ID must be 5-7 digits, got {FiveToneSettings.SelfId.Length}");
        }

        // Desktop can't set Number out of range or to a duplicate (read-only
        // there, only changeable via the Group NO. redirect, which is
        // already capped 1-100 and can only ever land on an existing row or
        // create a fresh one). Mobile has no such redirect - each row edits
        // its own Number directly in a plain digit-only TextBox with no
        // lower bound or duplicate check available at the keystroke level
        // (found 2026-08-06 while auditing this view's input restrictions) -
        // so both are checked here instead, same "validate, don't revert"
        // convention as Self ID above.
        foreach (var entry in FiveToneIds)
        {
            if (entry.Number is < 1 or > CodeplugLimits.FiveToneIdMax)
            {
                ValidationMessages.Add($"5Tone Settings: ID number {entry.Number} must be 1-{CodeplugLimits.FiveToneIdMax}");
            }
        }

        foreach (var duplicateNumber in FiveToneIds.GroupBy(e => e.Number).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            ValidationMessages.Add($"5Tone Settings: ID number {duplicateNumber} is used by more than one row");
        }
    }

    /// <summary>Frequency range/Name length are also enforced live via each
    /// entry's own ObservableValidator (see TwoToneEncodeEntry's class doc
    /// comment) - that only drives the per-TextBox red-border UI feedback,
    /// it does NOT feed HasBlockingValidationErrors (which is driven purely
    /// by ValidationMessages), so the same checks are repeated here, same
    /// pattern as ValidateAmAir.</summary>
    private void ValidateTwoTone()
    {
        foreach (var entry in TwoToneEncodeEntries)
        {
            if (entry.Number is < 1 or > CodeplugLimits.TwoToneEncodeMax)
            {
                ValidationMessages.Add($"2Tone Encode: number {entry.Number} must be 1-{CodeplugLimits.TwoToneEncodeMax}");
            }

            if (entry.FirstToneFrequencyHz < CodeplugLimits.TwoToneFrequencyMinHz || entry.FirstToneFrequencyHz > CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                ValidationMessages.Add($"2Tone Encode {entry.Number}: 1st Tone Frequency {entry.FirstToneFrequencyHz} Hz is outside {CodeplugLimits.TwoToneFrequencyMinHz}-{CodeplugLimits.TwoToneFrequencyMaxHz} Hz");
            }

            if (entry.SecondToneFrequencyHz < CodeplugLimits.TwoToneFrequencyMinHz || entry.SecondToneFrequencyHz > CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                ValidationMessages.Add($"2Tone Encode {entry.Number}: 2nd Tone Frequency {entry.SecondToneFrequencyHz} Hz is outside {CodeplugLimits.TwoToneFrequencyMinHz}-{CodeplugLimits.TwoToneFrequencyMaxHz} Hz");
            }

            if (entry.Name.Length > CodeplugLimits.TwoToneNameMaxLength)
            {
                ValidationMessages.Add($"2Tone Encode {entry.Number}: name exceeds {CodeplugLimits.TwoToneNameMaxLength} characters");
            }
        }

        foreach (var duplicateNumber in TwoToneEncodeEntries.GroupBy(e => e.Number).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            ValidationMessages.Add($"2Tone Encode: number {duplicateNumber} is used by more than one row");
        }

        foreach (var entry in TwoToneDecodeEntries)
        {
            if (entry.Number is < 1 or > CodeplugLimits.TwoToneDecodeMax)
            {
                ValidationMessages.Add($"2Tone Decode: number {entry.Number} must be 1-{CodeplugLimits.TwoToneDecodeMax}");
            }

            if (entry.FirstToneFrequencyHz < CodeplugLimits.TwoToneFrequencyMinHz || entry.FirstToneFrequencyHz > CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                ValidationMessages.Add($"2Tone Decode {entry.Number}: 1st Tone Frequency {entry.FirstToneFrequencyHz} Hz is outside {CodeplugLimits.TwoToneFrequencyMinHz}-{CodeplugLimits.TwoToneFrequencyMaxHz} Hz");
            }

            if (entry.SecondToneFrequencyHz < CodeplugLimits.TwoToneFrequencyMinHz || entry.SecondToneFrequencyHz > CodeplugLimits.TwoToneFrequencyMaxHz)
            {
                ValidationMessages.Add($"2Tone Decode {entry.Number}: 2nd Tone Frequency {entry.SecondToneFrequencyHz} Hz is outside {CodeplugLimits.TwoToneFrequencyMinHz}-{CodeplugLimits.TwoToneFrequencyMaxHz} Hz");
            }

            if (entry.Name.Length > CodeplugLimits.TwoToneNameMaxLength)
            {
                ValidationMessages.Add($"2Tone Decode {entry.Number}: name exceeds {CodeplugLimits.TwoToneNameMaxLength} characters");
            }
        }

        foreach (var duplicateNumber in TwoToneDecodeEntries.GroupBy(e => e.Number).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            ValidationMessages.Add($"2Tone Decode: number {duplicateNumber} is used by more than one row");
        }
    }
}
