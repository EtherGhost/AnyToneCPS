using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>One of the 8 Digital APRS Report Channel slots. Fixed count,
/// always exactly 8 - not user add/removable. Radio-write dirty-tracking
/// scaffolding only, added ahead of the actual encode/patch work - see
/// AprsSettingsEntry's own doc comment for why.</summary>
public partial class AprsDigitalReportEntry : ObservableValidator
{
    /// <summary>Radio-write baseline only, same "_radioSyncSnapshot" split
    /// every other radio-write-capable entity uses. Deliberately excludes
    /// Number - it's the slot position, not an encoded field.</summary>
    private AprsDigitalReportSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;

    /// <summary>Byte offset and width CONFIRMED 2026-08-15 by live
    /// differential write (uint16 little-endian, see Capture_Findings.md):
    /// this is a sentinel scheme, not a plain 1-based channel index. VFO A
    /// = 4000, VFO B = 4001 confirmed exactly; real channel numbers
    /// presumably occupy the range below 4000; "Current Channel"'s raw
    /// value not yet confirmed. <see cref="ChannelText"/> only covers the
    /// two confirmed sentinels - a real channel number or "Current
    /// Channel" still displays as a plain number (see that property's own
    /// doc comment), not lost, just not pickable from the dropdown yet.
    /// Full ComboBox coverage (Current Channel + digital channels + VFO
    /// A/B) still needs a MainViewModel-level dynamic list plus confirming
    /// the "Current Channel" sentinel and the real-channel-number range.</summary>
    [ObservableProperty] private int _channel;

    private const int VfoAChannel = 4000;
    private const int VfoBChannel = 4001;
    public static IReadOnlyList<string> ChannelOptions { get; } = ["VFO A", "VFO B"];

    /// <summary>Only the two confirmed sentinels (see <see cref="Channel"/>'s
    /// own doc comment) are selectable here - a real channel number or the
    /// unconfirmed "Current Channel" sentinel falls back to displaying the
    /// raw number (matches this codebase's existing CallTypeText/SlotText
    /// convention), not editable via this dropdown until those are
    /// confirmed live.</summary>
    public string ChannelText
    {
        get => Channel switch
        {
            VfoAChannel => "VFO A",
            VfoBChannel => "VFO B",
            _ => Channel.ToString(CultureInfo.InvariantCulture)
        };
        set
        {
            Channel = value switch
            {
                "VFO A" => VfoAChannel,
                "VFO B" => VfoBChannel,
                _ => Channel
            };
        }
    }
    [ObservableProperty] private long _talkgroupId;
    [ObservableProperty] private byte _callType;
    [ObservableProperty] private byte _slot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AprsDigitalReportSnapshot CreateRadioSnapshot() => new(Channel, TalkgroupId, CallType, Slot);

    private sealed record AprsDigitalReportSnapshot(int Channel, long TalkgroupId, byte CallType, byte Slot);

    /// <summary>0 means "unused" for this slot (same convention
    /// MainViewModel.ValidateAprsSettings already used) - bypasses the
    /// shared DmrIdValidation range check for that one sentinel value. See
    /// DmrIdValidation's own doc comment for why this wrapper exists at all.</summary>
    [CustomValidation(typeof(AprsDigitalReportEntry), nameof(ValidateTalkgroupIdText))]
    public string TalkgroupIdText
    {
        get => TalkgroupId.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(TalkgroupIdText));
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var talkgroupId) && (talkgroupId == 0 || DmrIdValidation.IsValidDmrId(talkgroupId)))
            {
                TalkgroupId = talkgroupId;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateTalkgroupIdText(string? value, ValidationContext context) =>
        value == "0" ? ValidationResult.Success : DmrIdValidation.ValidateDmrIdText(value, context);

    // Confirmed 2026-08-15: real vendor CPS label is "APRS TG", not
    // "Talkgroup ID"/"TG/DMR ID" - same underlying DMR talkgroup ID data
    // (matches the 5057 value seen directly in the vendor CPS screenshot),
    // just a different display label. Kept the C# property name as-is
    // (TalkgroupId/TalkgroupIdText) since only the UI label was wrong, not
    // the field's meaning - see the "APRS TG" header in both platforms' XAML.

    public static IReadOnlyList<string> CallTypeOptions { get; } = ["Private call", "Group call", "All call"];
    public static IReadOnlyList<string> SlotOptions { get; } = ["Channel slot", "Slot 1", "Slot 2"];

    public string CallTypeText
    {
        get => CallType < CallTypeOptions.Count ? CallTypeOptions[CallType] : CallType.ToString(CultureInfo.InvariantCulture);
        set
        {
            var index = CallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                CallType = (byte)index;
            }
        }
    }

    public string SlotText
    {
        get => Slot < SlotOptions.Count ? SlotOptions[Slot] : Slot.ToString(CultureInfo.InvariantCulture);
        set
        {
            var index = SlotOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                Slot = (byte)index;
            }
        }
    }

    partial void OnTalkgroupIdChanged(long value)
    {
        OnPropertyChanged(nameof(TalkgroupIdText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnChannelChanged(int value)
    {
        OnPropertyChanged(nameof(ChannelText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnCallTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(CallTypeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnSlotChanged(byte value)
    {
        OnPropertyChanged(nameof(SlotText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
