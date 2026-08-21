using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// There's only ever one Alarm/Emergency settings record on the radio -
/// single instance, not a collection, like MasterIdEntry/TalkAliasSettingsEntry.
/// Most fields are kept as raw bytes rather than guessed enum strings - only
/// AnalogEmergencyAlarm's rough shape is hinted at by Field_Reference.md §13,
/// not confirmed byte-for-byte against real hardware. AnalogEniType is the
/// one exception with a confirmed *order*: HPT topic 218 states it as one
/// flowing sentence ("select None / DTMF / 5Tone"), a strong signal of the
/// real UI order. The two EniSend fields also have confirmed label text
/// (english.ini ids 2634/2635: "Assigned Channel"/"Selected Channel") but a
/// genuinely CONFLICTING order: the ini's own id sequence puts Assigned
/// first, while HPT topic 223's prose describes Selected first ("Selected
/// channel : Send... Assigned channel : Send..."). Deliberately left as raw
/// bytes rather than guessing which source reflects the real byte order -
/// this needs a hardware differential test to resolve, not another guess.
///
/// UPDATE 2026-08-04 (Analog Alarm groupbox only, user-provided field specs
/// plus 4 vendor CPS screenshots "Analog alarm - *.PNG" showing the real
/// Emergency Information dialog): the Analog Alarm fields below are now
/// wired up as real dropdowns with confirmed item lists and enable/disable
/// logic, sourced from the reference project's own alert_settings_dialog.cpp
/// (setupUI/loadData/save/analogAlarmUpdated/analogEniSendChanged) - this
/// resolves the AnalogEniSend order question above IN FAVOR of the ini's
/// order (Assigned Channel=0, Selected Channel=1), since that's the literal
/// array order the reference uses to populate the real combo box
/// (Constants::ENI_SEND_SELECT). Not hardware-confirmed (no live write test
/// has been done for this entity - it remains fully read-only/not-writable
/// in this app), so treat this as a strong inference, not gospel. The 4
/// screenshots all show the same ENI Type state (5Tone) with Selected
/// Channel - they confirm layout and the Alarm-Time/TX-RX-Duration enable
/// pattern exactly, but do NOT independently confirm the None/DTMF/QDC1200
/// states for every field (inferred from the reference source alone for
/// those).
///
/// UPDATE 2026-08-04: radio-write support added, confirmed by 4 live USB
/// write captures covering every field below except <see cref="QdcCallType"/>
/// (confirmed to have no byte address at all - see its own doc comment).
/// See <see cref="AlarmSettingsCodec.EncodeD3483000"/> for the full write-
/// side confirmation notes, including the Digital Emergency Channel byte-
/// count fix the captures uncovered.
/// </summary>
public partial class AlarmSettingsEntry : ObservableValidator
{
    /// <summary>Radio-write baseline only - deliberately separate from any
    /// project-file "dirty" tracking (this entity has none of its own; the
    /// whole project's dirty flag is tracked at MainViewModel level instead),
    /// same split every other radio-write-capable entity's own
    /// <c>_radioSyncSnapshot</c> uses. Only set by <see cref="MarkRadioSynced"/>,
    /// after a successful read OR a successful write. Deliberately excludes
    /// <see cref="QdcCallType"/> - it has no byte address at all (confirmed
    /// by a live differential capture, see <see cref="AlarmSettingsCodec.EncodeD3483000"/>'s
    /// doc comment), so it can never be "pending" a write that will never
    /// happen.</summary>
    private AlarmSettingsSnapshot? _radioSyncSnapshot;

    /// <summary>Single aggregate flag rather than the per-field
    /// IsXPendingRadioWrite booleans OptionalSettingsEntry/ChannelEntry use -
    /// this entity's view has no per-field "pending write" UI indicator to
    /// drive (no bold-font-per-field convention was built for Alarm
    /// Settings), so a plain record-equality comparison against the last
    /// synced snapshot is all MainViewModel.RadioWrite.cs's dirty-detection
    /// actually needs.</summary>
    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AlarmSettingsSnapshot CreateRadioSnapshot() => new(
        AnalogEmergencyAlarm, AnalogEniType, AnalogEmergencyId, AnalogAlarmTime, AnalogTxDuration, AnalogRxDuration,
        AnalogEmergencyChannel, AnalogEniSend, AnalogEmergencyCycle,
        DigitalEmergencyAlarm, DigitalAlarmTime, DigitalTxDuration, DigitalRxDuration, DigitalEmergencyChannel,
        DigitalEmergencyCycle, DigitalEniSend, DigitalCallType, DigitalTgDmrId,
        ReceiveAlarm, ManDown, ManDownDelay,
        WorkAloneResponseTime, WorkAloneWarningTime, WorkAloneResponse,
        QdcGroupId, QdcPrivateId);

    private sealed record AlarmSettingsSnapshot(
        byte AnalogEmergencyAlarm, byte AnalogEniType, byte AnalogEmergencyId, byte AnalogAlarmTime, byte AnalogTxDuration, byte AnalogRxDuration,
        int AnalogEmergencyChannel, byte AnalogEniSend, byte AnalogEmergencyCycle,
        byte DigitalEmergencyAlarm, byte DigitalAlarmTime, byte DigitalTxDuration, byte DigitalRxDuration, ushort DigitalEmergencyChannel,
        byte DigitalEmergencyCycle, byte DigitalEniSend, byte DigitalCallType, long DigitalTgDmrId,
        bool ReceiveAlarm, bool ManDown, byte ManDownDelay,
        byte WorkAloneResponseTime, byte WorkAloneWarningTime, byte WorkAloneResponse,
        string QdcGroupId, string QdcPrivateId);

    [ObservableProperty] private byte _analogEmergencyAlarm;
    [ObservableProperty] private byte _analogEniType;
    [ObservableProperty] private byte _analogEmergencyId;
    [ObservableProperty] private byte _analogAlarmTime;
    [ObservableProperty] private byte _analogTxDuration;
    [ObservableProperty] private byte _analogRxDuration;
    [ObservableProperty] private int _analogEmergencyChannel;
    [ObservableProperty] private byte _analogEniSend;
    [ObservableProperty] private byte _analogEmergencyCycle;

    [ObservableProperty] private byte _digitalEmergencyAlarm;
    [ObservableProperty] private byte _digitalAlarmTime;
    [ObservableProperty] private byte _digitalTxDuration;
    [ObservableProperty] private byte _digitalRxDuration;
    // Fixed 2026-08-04 from byte to ushort: a live USB capture proved the
    // real write is 2 bytes little-endian (0xe-0xf), matching Analog's own
    // Emergency Channel field shape - see AlarmSettingsCodec.Decode's doc
    // comment on this field.
    [ObservableProperty] private ushort _digitalEmergencyChannel;
    [ObservableProperty] private byte _digitalEmergencyCycle;
    [ObservableProperty] private byte _digitalEniSend;
    [ObservableProperty] private byte _digitalCallType;
    [ObservableProperty] private long _digitalTgDmrId;

    /// <summary>0 means "off" for this field (same convention
    /// ValidateAlarmSettings already used) - bypasses the shared
    /// DmrIdValidation range check for that one sentinel value. See
    /// DmrIdValidation's own doc comment for why this wrapper exists at all.</summary>
    [CustomValidation(typeof(AlarmSettingsEntry), nameof(ValidateDigitalTgDmrIdText))]
    public string DigitalTgDmrIdText
    {
        get => DigitalTgDmrId.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(DigitalTgDmrIdText));
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var dmrId) && (dmrId == 0 || DmrIdValidation.IsValidDmrId(dmrId)))
            {
                DigitalTgDmrId = dmrId;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateDigitalTgDmrIdText(string? value, ValidationContext context) =>
        value == "0" ? ValidationResult.Success : DmrIdValidation.ValidateDmrIdText(value, context);

    // Confirmed 2026-08-04 via the reference project's own combo-box
    // construction (Constants::ANALOG_EMERGENCY_ALARM) - index 0 = "Alarm"
    // is the state where the radio treats this as a real user-triggered
    // alarm (Alarm Time meaningful, ENI Type/Emergency ID/TX/RX Duration
    // not), the other 3 are "transponder" style background states (the
    // opposite gating - see IsAnalogEmergencyAlarmAlarm below). Matches all
    // 4 user-provided screenshots exactly.
    public static IReadOnlyList<string> AnalogEmergencyAlarmOptions { get; } =
        ["Alarm", "Transpond+Background", "Transpond+Alarm", "Both"];

    public string AnalogEmergencyAlarmText
    {
        get => AnalogEmergencyAlarm < AnalogEmergencyAlarmOptions.Count ? AnalogEmergencyAlarmOptions[AnalogEmergencyAlarm] : AnalogEmergencyAlarm.ToString();
        set
        {
            var index = AnalogEmergencyAlarmOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                AnalogEmergencyAlarm = (byte)index;
            }
        }
    }

    /// <summary>Gates ENI Type/Emergency ID/Alarm Time/TX-RX Duration below -
    /// confirmed 2026-08-04 ("if Emergency alarm not Alarm =>
    /// disabled" for Alarm Time), and directly visible across all 4
    /// screenshots for the other fields (they're enabled together, opposite
    /// of Alarm Time).</summary>
    public bool IsAnalogEmergencyAlarmAlarm => AnalogEmergencyAlarm == 0;

    public bool IsAnalogEniTypeEnabled => !IsAnalogEmergencyAlarmAlarm;
    public bool IsAnalogAlarmTimeEnabled => IsAnalogEmergencyAlarmAlarm;
    public bool IsAnalogTxRxDurationEnabled => !IsAnalogEmergencyAlarmAlarm;

    partial void OnAnalogEmergencyAlarmChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogEmergencyAlarmText));
        OnPropertyChanged(nameof(IsAnalogEmergencyAlarmAlarm));
        OnPropertyChanged(nameof(IsAnalogEniTypeEnabled));
        OnPropertyChanged(nameof(IsAnalogEmergencyIdEnabled));
        OnPropertyChanged(nameof(IsAnalogAlarmTimeEnabled));
        OnPropertyChanged(nameof(IsAnalogTxRxDurationEnabled));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Extended 2026-08-04 with "QDC1200" as a 4th item - confirmed by the
    // reference project's setupUI(), which appends it specifically for the
    // D890UV (unconditionally here, since this app only ever targets the
    // D890UV).
    public static IReadOnlyList<string> EniTypeOptions { get; } = ["None", "DTMF", "5Tone", "QDC1200"];

    public string AnalogEniTypeText
    {
        get => AnalogEniType < EniTypeOptions.Count ? EniTypeOptions[AnalogEniType] : AnalogEniType.ToString();
        set
        {
            var index = EniTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                AnalogEniType = (byte)index;
            }
        }
    }

    /// <summary>Emergency ID is only meaningful for DTMF(1)/5Tone(2) - None
    /// has nothing to list, QDC1200 uses its own separate Kind/Group ID/
    /// Private ID fields instead (see MainViewModel's
    /// AlarmSettingsAnalogEmergencyIdOptions, which resolves the actual
    /// item list against DtmfIds/Tone5Ids - AlarmSettingsEntry itself has no
    /// access to those Channel-level lists).</summary>
    public bool IsAnalogEmergencyIdEnabled => IsAnalogEniTypeEnabled && AnalogEniType is 1 or 2;

    partial void OnAnalogEniTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogEniTypeText));
        OnPropertyChanged(nameof(IsAnalogEmergencyIdEnabled));
        OnPropertyChanged(nameof(IsQdcSettingEnabled));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAnalogEmergencyIdChanged(byte value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    // Vendor CPS presents Alarm Time/TX Duration/RX Duration as bounded
    // dropdowns (Constants::ALARM_DURATION, "1".."255"), not free text -
    // matched here with a plain 1-255 string list shared by all three.
    public static IReadOnlyList<string> DurationOptions { get; } =
        Enumerable.Range(1, 255).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

    public string AnalogAlarmTimeText
    {
        get => AnalogAlarmTime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                AnalogAlarmTime = parsed;
            }
        }
    }

    public string AnalogTxDurationText
    {
        get => AnalogTxDuration.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                AnalogTxDuration = parsed;
            }
        }
    }

    public string AnalogRxDurationText
    {
        get => AnalogRxDuration.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                AnalogRxDuration = parsed;
            }
        }
    }

    partial void OnAnalogAlarmTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogAlarmTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAnalogTxDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogTxDurationText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAnalogRxDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogRxDurationText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnAnalogEmergencyChannelChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    // Confirmed 2026-08-04 via the reference project's own combo-box
    // construction (Constants::ENI_SEND_SELECT) - see this class's own doc
    // comment for why this resolves the earlier ini-vs-HPT order conflict
    // in favor of "Assigned Channel"=0, "Selected Channel"=1. Always
    // enabled in all 4 screenshots regardless of Emergency Alarm state -
    // matches the reference, which never gates this field on anything.
    public static IReadOnlyList<string> EniSendOptions { get; } = ["Assigned Channel", "Selected Channel"];

    public string AnalogEniSendText
    {
        get => AnalogEniSend < EniSendOptions.Count ? EniSendOptions[AnalogEniSend] : AnalogEniSend.ToString();
        set
        {
            var index = EniSendOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                AnalogEniSend = (byte)index;
            }
        }
    }

    /// <summary>Gates Emergency Channel below. Corrected 2026-08-04 - the
    /// first pass had this backwards (enabled for "Selected Channel"). The
    /// reference project's own analogEniSendChanged() actually disables the
    /// channel picker unless index==0 ("Assigned Channel"), confirmed
    /// independently against the same live behavior: Emergency
    /// Channel is enabled for "Assigned Channel", not "Selected Channel".</summary>
    public bool IsAnalogEniSendAssignedChannel => AnalogEniSend == 0;

    partial void OnAnalogEniSendChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogEniSendText));
        OnPropertyChanged(nameof(IsAnalogEniSendAssignedChannel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Emergency Channel's actual item list (filtered to analog channels)
    // lives on MainViewModel (AlarmSettingsAnalogEmergencyChannelOptions/
    // Selection) - AlarmSettingsEntry itself has no access to the live
    // Channels collection, same reason Emergency ID's list lives there too.

    // "Continuous" + "1".."255" - confirmed 2026-08-04 ("Continuous,
    // 1, 2, 3 and so on until 255"), matches the reference's own
    // addItem("Continuous") + Constants::ALARM_DURATION combo construction
    // exactly, with a direct (no +/-1 offset) index-to-raw-byte mapping.
    // Always enabled in all 4 screenshots.
    public static IReadOnlyList<string> EmergencyCycleOptions { get; } =
        new[] { "Continuous" }.Concat(Enumerable.Range(1, 255).Select(i => i.ToString(CultureInfo.InvariantCulture))).ToList();

    public string AnalogEmergencyCycleText
    {
        get => AnalogEmergencyCycle == 0 ? "Continuous" : AnalogEmergencyCycle.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Continuous")
            {
                AnalogEmergencyCycle = 0;
            }
            else if (byte.TryParse(value, out var parsed))
            {
                AnalogEmergencyCycle = parsed;
            }
        }
    }

    partial void OnAnalogEmergencyCycleChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogEmergencyCycleText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Digital Alarm - confirmed 2026-08-04. Unlike Analog Alarm, NONE
    // of these fields are gated by Emergency Alarm state (confirmed
    // only Emergency Channel is ever disabled here, by ENI Send,
    // same as Analog's own Emergency Channel gating) - matches the
    // reference project's own alert_settings_dialog.cpp, which has no
    // digitalAlarmUpdated() handler at all (only digitalEniSendChanged()).
    // Digital also has no ENI Type Select/Emergency ID fields at all - those
    // are analog-signaling-specific (DTMF/5Tone/QDC1200), meaningless for a
    // DMR digital channel.
    public static IReadOnlyList<string> DigitalEmergencyAlarmOptions { get; } =
        ["Alarm", "Transpond+Background", "Transpond+NoLocalAlarm", "Transpond+LocalAlarm"];

    public string DigitalEmergencyAlarmText
    {
        get => DigitalEmergencyAlarm < DigitalEmergencyAlarmOptions.Count ? DigitalEmergencyAlarmOptions[DigitalEmergencyAlarm] : DigitalEmergencyAlarm.ToString();
        set
        {
            var index = DigitalEmergencyAlarmOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DigitalEmergencyAlarm = (byte)index;
            }
        }
    }

    partial void OnDigitalEmergencyAlarmChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalEmergencyAlarmText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Alarm Time/TX Duration/RX Duration - same 1-255 DurationOptions list
    // Analog Alarm already uses above, always enabled (no gating).
    public string DigitalAlarmTimeText
    {
        get => DigitalAlarmTime.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                DigitalAlarmTime = parsed;
            }
        }
    }

    public string DigitalTxDurationText
    {
        get => DigitalTxDuration.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                DigitalTxDuration = parsed;
            }
        }
    }

    public string DigitalRxDurationText
    {
        get => DigitalRxDuration.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                DigitalRxDuration = parsed;
            }
        }
    }

    partial void OnDigitalAlarmTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalAlarmTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigitalTxDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalTxDurationText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigitalRxDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalRxDurationText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Same 2-item EniSendOptions list Analog Alarm already uses above
    // ("Assigned Channel"/"Selected Channel").
    public string DigitalEniSendText
    {
        get => DigitalEniSend < EniSendOptions.Count ? EniSendOptions[DigitalEniSend] : DigitalEniSend.ToString();
        set
        {
            var index = EniSendOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DigitalEniSend = (byte)index;
            }
        }
    }

    /// <summary>Gates Emergency Channel below - confirmed 2026-08-04
    /// ("disabled ... when Emergency ENI send select=selected channel", i.e.
    /// enabled for "Assigned Channel"), matching the reference project's own
    /// digitalEniSendChanged() and the corrected Analog Alarm behavior
    /// above.</summary>
    public bool IsDigitalEniSendAssignedChannel => DigitalEniSend == 0;

    partial void OnDigitalEniSendChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalEniSendText));
        OnPropertyChanged(nameof(IsDigitalEniSendAssignedChannel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigitalEmergencyChannelChanged(ushort value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    // Digital Emergency Channel's actual item list (filtered to digital
    // channels) lives on MainViewModel (AlarmSettingsDigitalEmergencyChannelOptions/
    // Selection), same "AlarmSettingsEntry has no access to the live
    // Channels collection" reason as Analog Emergency Channel/Emergency ID
    // above. NOTE: DigitalEmergencyChannel is a byte (0-255), not the int
    // AnalogEmergencyChannel is - matches the reference project's own
    // decode_D890UV, which reads only 1 byte for this field even though its
    // OWN encode writes 2 bytes at the same offset (a genuine inconsistency
    // in the reference, not resolved here) - so only channels with radio
    // index 0-255 (Number 1-256) can be referenced at all; the picker's
    // options list is filtered to that range for exactly this reason.

    // Same "Continuous"+1-255 EmergencyCycleOptions list Analog Alarm
    // already uses above.
    public string DigitalEmergencyCycleText
    {
        get => DigitalEmergencyCycle == 0 ? "Continuous" : DigitalEmergencyCycle.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Continuous")
            {
                DigitalEmergencyCycle = 0;
            }
            else if (byte.TryParse(value, out var parsed))
            {
                DigitalEmergencyCycle = parsed;
            }
        }
    }

    partial void OnDigitalEmergencyCycleChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalEmergencyCycleText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // "Call Type" - same 3-item CallTypeOptions list QDC1200's own "Kind"
    // uses below (Constants::CALL_TYPE in the reference, shared by both).
    public string DigitalCallTypeText
    {
        get => DigitalCallType < CallTypeOptions.Count ? CallTypeOptions[DigitalCallType] : DigitalCallType.ToString();
        set
        {
            var index = CallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DigitalCallType = (byte)index;
            }
        }
    }

    partial void OnDigitalCallTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalCallTypeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDigitalTgDmrIdChanged(long value)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(DigitalTgDmrIdText));
    }


    [ObservableProperty] private bool _receiveAlarm;
    [ObservableProperty] private bool _manDown;

    partial void OnReceiveAlarmChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnManDownChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));

    // "Man Down Delay [s]" - confirmed 2026-08-04: 0-255, matching the
    // reference project's own Constants::MAN_DOWN_DELAY combo exactly
    // (direct raw-byte-equals-index mapping, no offset - unlike Work
    // Alone's own Response/Warning Time fields above, this one starts at 0
    // not 1).
    public static IReadOnlyList<string> ManDownDelayOptions { get; } =
        Enumerable.Range(0, 256).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();

    [ObservableProperty] private byte _manDownDelay;

    public string ManDownDelayText
    {
        get => ManDownDelay.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var parsed))
            {
                ManDownDelay = parsed;
            }
        }
    }

    partial void OnManDownDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(ManDownDelayText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // "Response Time" - confirmed 2026-08-04: 1-256 minutes, matching
    // the reference project's own combo construction exactly
    // (addItem(i+1 + "m") for i in 0..255) - a direct raw-byte-plus-1
    // display offset, 256 possible raw values (0-255) covering the full
    // byte range.
    public static IReadOnlyList<string> WorkAloneResponseTimeOptions { get; } =
        Enumerable.Range(1, 256).Select(i => $"{i}m").ToList();

    [ObservableProperty] private byte _workAloneResponseTime;

    public string WorkAloneResponseTimeText
    {
        get => WorkAloneResponseTime < WorkAloneResponseTimeOptions.Count ? WorkAloneResponseTimeOptions[WorkAloneResponseTime] : $"{WorkAloneResponseTime}m";
        set
        {
            var index = WorkAloneResponseTimeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                WorkAloneResponseTime = (byte)index;
            }
        }
    }

    partial void OnWorkAloneResponseTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(WorkAloneResponseTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // "Warning Time" - confirmed 2026-08-04: 1-255 seconds. The
    // reference project's own combo construction uses the exact same 256-
    // item loop as Response Time above (1s-256s, not 1-255) - almost
    // certainly copy-pasted from Response Time without adjusting the upper
    // bound, matching a repeated pattern of reference inaccuracies
    // elsewhere. The directly-confirmed range wins over the reference
    // here. Same direct raw-byte-plus-1 display offset,
    // just one fewer item (raw 255 has no matching list entry - if the
    // radio ever reports it, the dropdown simply shows unselected, same
    // degenerate handling as every other ComboBox-SelectedItem field in
    // this app for an out-of-range legacy raw value).
    public static IReadOnlyList<string> WorkAloneWarningTimeOptions { get; } =
        Enumerable.Range(1, 255).Select(i => $"{i}s").ToList();

    [ObservableProperty] private byte _workAloneWarningTime;

    public string WorkAloneWarningTimeText
    {
        get => WorkAloneWarningTime < WorkAloneWarningTimeOptions.Count ? WorkAloneWarningTimeOptions[WorkAloneWarningTime] : $"{WorkAloneWarningTime}s";
        set
        {
            var index = WorkAloneWarningTimeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                WorkAloneWarningTime = (byte)index;
            }
        }
    }

    partial void OnWorkAloneWarningTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(WorkAloneWarningTimeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // "Response" - matches the reference project's own combo exactly
    // (QStringList{"Key", "Voice Transmit"}), direct raw 0/1 index mapping.
    public static IReadOnlyList<string> WorkAloneResponseOptions { get; } = ["Key", "Voice Transmit"];

    [ObservableProperty] private byte _workAloneResponse;

    public string WorkAloneResponseText
    {
        get => WorkAloneResponse < WorkAloneResponseOptions.Count ? WorkAloneResponseOptions[WorkAloneResponse] : WorkAloneResponse.ToString();
        set
        {
            var index = WorkAloneResponseOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                WorkAloneResponse = (byte)index;
            }
        }
    }

    partial void OnWorkAloneResponseChanged(byte value)
    {
        OnPropertyChanged(nameof(WorkAloneResponseText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Whole QDC1200 Setting groupbox is only relevant when ENI Type Select
    // is QDC1200(3) - confirmed 2026-08-04.
    public bool IsQdcSettingEnabled => AnalogEniType == 3;

    // Shared by "Kind" below (QDC1200 Setting) AND Digital Alarm's own
    // "Call Type" above - both are the reference project's identical
    // Constants::CALL_TYPE combo. QDC1200's own Kind field is the odd one
    // out, though: unlike every other field in this class, the reference
    // NEVER actually decodes/assigns qdc_call_type for the D890UV (or any
    // model) - it's declared on the class, populated into the UI combo,
    // but has no confirmed byte address at all. QdcCallType is modeled
    // anyway (defaulting to 0/"Private Call") so the Group ID/Private ID
    // enable-gating below has something real to bind to, same as this
    // class's own AnalogEmergencyAlarm precedent (shape known, byte offset
    // not yet confirmed) - but this one is a genuine unknown, not just
    // unconfirmed, until a live capture finds it. DigitalCallType (above),
    // by contrast, DOES have a confirmed byte address (data3482e00[0x0],
    // see AlarmSettingsCodec).
    public static IReadOnlyList<string> CallTypeOptions { get; } = ["Private Call", "Group Call", "All Call"];

    [ObservableProperty] private byte _qdcCallType;

    public string QdcCallTypeText
    {
        get => QdcCallType < CallTypeOptions.Count ? CallTypeOptions[QdcCallType] : QdcCallType.ToString();
        set
        {
            var index = CallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                QdcCallType = (byte)index;
            }
        }
    }

    /// <summary>Gates Group ID below - confirmed 2026-08-04 ("If Kind
    /// is not Group call => disabled").</summary>
    public bool IsQdcGroupIdEnabled => QdcCallType == 1;

    /// <summary>Gates Private ID below - confirmed 2026-08-04 ("If
    /// Kind is not Private call => disabled").</summary>
    public bool IsQdcPrivateIdEnabled => QdcCallType == 0;

    partial void OnQdcCallTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(QdcCallTypeText));
        OnPropertyChanged(nameof(IsQdcGroupIdEnabled));
        OnPropertyChanged(nameof(IsQdcPrivateIdEnabled));
    }

    // Group ID: 3 hex digits (0-9, A-F) - matches AlarmSettingsCodec.Decode's
    // own QdcGroupId shape (.Substring(1, 3) of a 4-nibble reversed-hex
    // string = exactly 3 characters). Private ID: 4 hex digits, matches the
    // full reversed-hex string used for QdcPrivateId. Character-set and
    // length restricted in the view (HexDigitInput + native MaxLength), not
    // here - the underlying strings are plain, unvalidated at the model
    // level like every other raw text field in this app.
    [ObservableProperty] private string _qdcGroupId = "";
    [ObservableProperty] private string _qdcPrivateId = "";

    partial void OnQdcGroupIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnQdcPrivateIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
