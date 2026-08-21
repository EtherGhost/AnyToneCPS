using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// There's only ever one Master ID on the radio (a designated "primary" DMR
/// ID among the Radio ID list) - unlike the other new entities, this is a
/// single instance, not a collection.
///
/// Full radio-write support added 2026-08-06, confirmed via a live
/// differential WRITE capture - see MasterIdCodec's own doc comment for the
/// byte-level confirmation (which also resolved an initial Name maxlength
/// discrepancy - see CodeplugLimits.MasterIdNameMaxLength's own doc comment).
/// </summary>
public partial class MasterIdEntry : ObservableValidator
{
    [ObservableProperty] private long _dmrId;
    [ObservableProperty] private bool _used;
    [ObservableProperty] private string _name = "";

    /// <summary>See DmrIdValidation's own doc comment.</summary>
    [CustomValidation(typeof(DmrIdValidation), nameof(DmrIdValidation.ValidateDmrIdText))]
    public string DmrIdText
    {
        get => DmrId.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(DmrIdText));
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var dmrId) && DmrIdValidation.IsValidDmrId(dmrId))
            {
                DmrId = dmrId;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    /// <summary>Radio-write baseline only - see Qdc1200SettingsEntry's own
    /// doc comment for the split rationale.</summary>
    private MasterIdSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private MasterIdSnapshot CreateRadioSnapshot() => new(DmrId, Used, Name);

    private sealed record MasterIdSnapshot(long DmrId, bool Used, string Name);

    partial void OnDmrIdChanged(long value)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(DmrIdText));
    }

    partial void OnUsedChanged(bool value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
