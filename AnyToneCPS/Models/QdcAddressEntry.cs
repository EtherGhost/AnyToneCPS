using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// QDC Address Book - a fixed cap 128 list (No. 1-128, see
/// CodeplugLimits.QdcAddressMax), same Add/Remove-with-cap convention as
/// Analog Address Book. User-specified field shape 2026-08-04 directly
/// from the real vendor CPS.
///
/// Full radio-write support added the same day, confirmed via one live
/// differential WRITE capture (No. 1: Call Type=Private, Private
/// ID="ABCD", Type=ALEART, Ack=On, Name="QDCADDRTEST1") plus one live
/// differential READ capture (to resolve the exact record boundary - the
/// write only sent the bytes that actually changed) - see
/// QdcAddressCodec's own doc comment for the full byte confirmation.
///
/// The wire byte layout turned out to be an EXACT match for QDC 1200
/// Setting's own ID table (Qdc1200IdEntry) - same Call Type/Private-Group
/// ID/Type/Ack/Name shape, same absolute Type code for ALEART (2, directly
/// re-confirmed on this separate screen). <see cref="Type"/> is therefore
/// kept as the SAME absolute/shared code across Call Types as
/// Qdc1200IdEntry uses, not the "naive list-relative first draft" this
/// class started with before its own live test. Only Private Call (raw 0)
/// and ALEART (raw 2) were directly exercised here - Group/All Call's own
/// raw Call Type byte and the rest of the absolute Type code table are
/// INHERITED from Qdc1200IdEntry's own confirmed values (same assumption:
/// this is the same underlying firmware table reused across two UI
/// screens), not independently re-verified for this entity. Likewise,
/// <see cref="OnCallTypeChanged"/> no longer resets Private ID/Group
/// ID/Type - inferred from the identical wire layout (each is its own
/// independent byte field, same as Qdc1200IdEntry, where this was
/// directly confirmed live), not independently re-tested here.
///
/// Two label sets exist for the SAME absolute code space because this
/// entity genuinely uses different text per Call Type context for
/// code 0 and code 6 ("SEL CALL"/"Remotely Monitor" for Private vs "SEL
/// CAL" for Group/All) - unlike Qdc1200IdEntry, which uses one canonical
/// label per code in both contexts.
/// </summary>
public partial class QdcAddressEntry : ObservableObject
{
    /// <summary>Radio-write baseline only - see Qdc1200IdEntry's own doc
    /// comment for the split rationale. Deliberately excludes
    /// <see cref="Number"/>.</summary>
    private QdcAddressSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private QdcAddressSnapshot CreateRadioSnapshot() => new(CallType, PrivateCallId, GroupCallId, Type, Ack, Name);

    private sealed record QdcAddressSnapshot(byte CallType, string PrivateCallId, string GroupCallId, byte Type, bool Ack, string Name);

    public const byte TypeSelCal = 0;
    public const byte TypeCheck = 1;
    public const byte TypeAleart = 2;
    public const byte TypeRemotelyKill = 3;
    public const byte TypeWake = 4;
    public const byte TypeAlarm = 5;
    public const byte TypeRemotelyMoniton = 6;

    /// <summary>Only <see cref="TypeAleart"/>=2 is directly confirmed for
    /// THIS entity (see this class's own doc comment) - the rest are
    /// inherited from Qdc1200IdEntry's own absolute-code table.</summary>
    private static readonly IReadOnlyDictionary<byte, string> PrivateAbsoluteTypeLabels = new Dictionary<byte, string>
    {
        [TypeSelCal] = "SEL CALL",
        [TypeCheck] = "CHECK",
        [TypeAleart] = "ALEART",
        [TypeRemotelyKill] = "Remotely kill",
        [TypeWake] = "WAKE",
        [TypeAlarm] = "ALARM",
        [TypeRemotelyMoniton] = "Remotely Monitor"
    };

    private static readonly IReadOnlyDictionary<byte, string> GroupOrAllAbsoluteTypeLabels = new Dictionary<byte, string>
    {
        [TypeSelCal] = "SEL CAL",
        [TypeAleart] = "ALEART",
        [TypeAlarm] = "ALARM"
    };

    public static IReadOnlyList<string> CallTypeOptions { get; } = ["Private Call", "Group Call", "All Call"];

    public static IReadOnlyList<string> PrivateTypeOptions { get; } =
        ["SEL CALL", "CHECK", "ALEART", "Remotely kill", "WAKE", "ALARM", "Remotely Monitor"];

    public static IReadOnlyList<string> GroupOrAllTypeOptions { get; } = ["SEL CAL", "ALEART", "ALARM"];

    public static IReadOnlyList<string> AckOptions { get; } = ["Off", "On"];

    [ObservableProperty] private int _number;
    [ObservableProperty] private byte _callType;
    [ObservableProperty] private string _privateCallId = "";
    [ObservableProperty] private string _groupCallId = "";
    [ObservableProperty] private byte _type = TypeSelCal;
    [ObservableProperty] private bool _ack;
    [ObservableProperty] private string _name = "";

    public bool IsPrivateCallIdEnabled => CallType == 0;
    public bool IsGroupCallIdEnabled => CallType == 1;

    /// <summary>The labels offered for <see cref="Type"/> - Group and All
    /// Call share one 3-item list, Private Call has its own 7-item list
    /// (see this class's own doc comment for the exact text).</summary>
    public IReadOnlyList<string> TypeOptions => CallType == 0 ? PrivateTypeOptions : GroupOrAllTypeOptions;

    private IReadOnlyDictionary<byte, string> ActiveAbsoluteTypeLabels => CallType == 0 ? PrivateAbsoluteTypeLabels : GroupOrAllAbsoluteTypeLabels;

    public string CallTypeText
    {
        get => CallTypeOptions[CallType];
        set
        {
            var index = CallTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                CallType = (byte)index;
            }
        }
    }

    public string TypeText
    {
        get => ActiveAbsoluteTypeLabels.TryGetValue(Type, out var label) && TypeOptions.Contains(label) ? label : "";
        set
        {
            foreach (var (code, label) in ActiveAbsoluteTypeLabels)
            {
                if (label == value)
                {
                    Type = code;
                    return;
                }
            }
        }
    }

    /// <summary>Enabled for ALEART/Remotely Monitor regardless of Call
    /// Type - this entity has no Call-Type restriction here,
    /// unlike Qdc1200IdEntry's own Private-Call-only rule (confirmed live
    /// for that separate entity, not assumed to carry over here).</summary>
    public bool IsAckEnabled => Type is TypeAleart or TypeRemotelyMoniton;

    public string AckText
    {
        get => AckOptions[Ack ? 1 : 0];
        set => Ack = value == "On";
    }

    public string DisplayLabel => $"{Number}  {Name}";

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnCallTypeChanged(byte value)
    {
        // Private ID/Group ID/Type are deliberately NOT reset here -
        // inferred from Qdc1200IdEntry's own confirmed wire behavior for
        // the identical byte layout (see this class's own doc comment).
        // Only Ack actually gets cleared when it becomes disabled.
        OnPropertyChanged(nameof(IsPrivateCallIdEnabled));
        OnPropertyChanged(nameof(IsGroupCallIdEnabled));
        OnPropertyChanged(nameof(TypeOptions));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(CallTypeText));
        RefreshAckEnabled();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(TypeText));
        RefreshAckEnabled();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private void RefreshAckEnabled()
    {
        if (!IsAckEnabled)
        {
            Ack = false;
        }

        OnPropertyChanged(nameof(IsAckEnabled));
    }

    partial void OnAckChanged(bool value)
    {
        OnPropertyChanged(nameof(AckText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnPrivateCallIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnGroupCallIdChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
