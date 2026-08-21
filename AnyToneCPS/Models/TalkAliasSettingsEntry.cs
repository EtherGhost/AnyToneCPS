using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// There's only ever one Talk Alias Settings record on the radio - like
/// MasterIdEntry, a single instance, not a collection. Full radio-write
/// support added 2026-08-09, confirmed via one live differential write
/// capture - see TalkAliasSettingsCodec's own doc comment for the byte-
/// level findings. DisplayPriorityOptions corrected to the real 3-value
/// enum confirmed by that capture (the previous 5-value list was an
/// unconfirmed guess from a vendor string-table extraction, never matching
/// the real vendor CPS dropdown). DataFormatOptions (ISO 8/ISO 7/Unicode,
/// from Field_Reference.md) confirmed correct by the same capture.
/// </summary>
public partial class TalkAliasSettingsEntry : ObservableObject
{
    [ObservableProperty] private byte _displayPriority;
    [ObservableProperty] private byte _dataFormat;

    public static IReadOnlyList<string> DisplayPriorityOptions { get; } = ["Off", "Contact Alias", "Air Alias DMR/NX"];
    public static IReadOnlyList<string> DataFormatOptions { get; } = ["ISO 8", "ISO 7", "Unicode"];

    public string DisplayPriorityText
    {
        get => DisplayPriority < DisplayPriorityOptions.Count ? DisplayPriorityOptions[DisplayPriority] : DisplayPriority.ToString();
        set
        {
            var index = DisplayPriorityOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DisplayPriority = (byte)index;
            }
        }
    }

    public string DataFormatText
    {
        get => DataFormat < DataFormatOptions.Count ? DataFormatOptions[DataFormat] : DataFormat.ToString();
        set
        {
            var index = DataFormatOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                DataFormat = (byte)index;
            }
        }
    }

    /// <summary>Radio-write baseline only - see Qdc1200SettingsEntry's own
    /// identically-shaped member for the split rationale.</summary>
    private TalkAliasSettingsSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private TalkAliasSettingsSnapshot CreateRadioSnapshot() => new(DisplayPriority, DataFormat);

    private sealed record TalkAliasSettingsSnapshot(byte DisplayPriority, byte DataFormat);

    partial void OnDisplayPriorityChanged(byte value)
    {
        OnPropertyChanged(nameof(DisplayPriorityText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDataFormatChanged(byte value)
    {
        OnPropertyChanged(nameof(DataFormatText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
