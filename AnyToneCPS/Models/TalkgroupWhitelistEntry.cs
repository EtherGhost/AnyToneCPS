using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>Full radio-write support added 2026-08-09, confirmed via a live
/// differential write capture - see TalkgroupWhitelistCodec's own doc
/// comment for the byte-level findings. CallType is fixed at 1 (Group
/// Call) - the vendor CPS's own edit popup has no control for it, always
/// Group Call, confirmed live. Number is likewise not independently
/// meaningful: the radio packs entries by list position regardless of what
/// row they were entered into, so MainViewModel keeps Number as a read-only
/// auto-assigned index+1 rather than letting it be typed.</summary>
public partial class TalkgroupWhitelistEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private long _dmrId;
    [ObservableProperty] private int _callType = 1;

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

    /// <summary>Radio-write baseline only tracks DmrId - Number is auto-
    /// assigned/positional (see this class's own doc comment) and CallType
    /// is a fixed constant, neither is meaningful to diff against.</summary>
    private long? _radioSyncedDmrId;

    public bool HasAnyPendingRadioWrite => _radioSyncedDmrId is null || DmrId != _radioSyncedDmrId;

    public void MarkRadioSynced()
    {
        _radioSyncedDmrId = DmrId;
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnDmrIdChanged(long value)
    {
        OnPropertyChanged(nameof(DmrIdText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }
}
