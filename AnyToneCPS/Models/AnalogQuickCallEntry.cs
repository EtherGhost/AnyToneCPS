using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Hot Key &gt; Analog Quick Call - a fixed 4-slot list (No. 1-4, see
/// CodeplugLimits.AnalogQuickCallMax), each slot picking an Operation Type
/// and (depending on that type) a Call ID from the relevant signalling
/// settings list. User-specified field shape 2026-08-04, directly from the
/// real vendor CPS. Full radio-write support added the same day (see
/// AnalogQuickCallCodec's own doc comment) - the real address/byte shape
/// was confirmed via a live differential READ capture, not yet against
/// this app's own write path.
/// </summary>
public partial class AnalogQuickCallEntry : ObservableObject
{
    /// <summary>Radio-write baseline only, same split every other
    /// radio-write-capable entity's own <c>_radioSyncSnapshot</c> uses -
    /// see Qdc1200IdEntry's own doc comment. Deliberately excludes
    /// <see cref="Number"/>.</summary>
    private AnalogQuickCallSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private AnalogQuickCallSnapshot CreateRadioSnapshot() => new(OperationType, CallId);

    private sealed record AnalogQuickCallSnapshot(byte OperationType, int CallId);

    [ObservableProperty] private int _number;
    [ObservableProperty] private byte _operationType;

    /// <summary>0-based index into the option list matching <see cref="OperationType"/>
    /// (Tone2Ids/Tone5Ids/QdcIds on MainViewModel) - -1 means "Off". Off and
    /// DTMF only ever offer "Off" here (confirmed directly against the
    /// real vendor CPS - unlike the xbenkozx/anytone-cps reference project,
    /// which shows a DTMF message picker for this case; a live observation
    /// against real hardware wins over the reference project here).</summary>
    [ObservableProperty] private int _callId = -1;

    public static IReadOnlyList<string> OperationTypeOptions { get; } = ["Off", "DTMF", "2Tone", "5Tone", "QDC1200"];

    /// <summary>Call ID is only meaningful for 2Tone/5Tone/QDC1200 - Off and
    /// DTMF always show as plain "Off", matching the real vendor CPS.</summary>
    public bool IsCallIdEnabled => OperationType is 2 or 3 or 4;

    public string DisplayLabel => $"{Number}  {OperationTypeOptions[OperationType]}";

    /// <summary>ComboBox-friendly wrapper - matches the app-wide convention
    /// of a string "Text" property over a raw byte/index field (see
    /// ChannelEntry.ColorCodeText), never a raw SelectedIndex binding.</summary>
    public string OperationTypeText
    {
        get => OperationTypeOptions[OperationType];
        set
        {
            var index = OperationTypeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                OperationType = (byte)index;
            }
        }
    }

    partial void OnNumberChanged(int value) => OnPropertyChanged(nameof(DisplayLabel));

    partial void OnOperationTypeChanged(byte value)
    {
        // Switching type invalidates any previously selected Call ID -
        // matches the real vendor CPS resetting to "Off" on a type change.
        CallId = -1;
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(IsCallIdEnabled));
        OnPropertyChanged(nameof(OperationTypeText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnCallIdChanged(int value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
