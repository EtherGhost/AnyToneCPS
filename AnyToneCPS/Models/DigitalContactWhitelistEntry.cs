using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>Same wire shape as TalkgroupWhitelistEntry (see
/// TalkgroupWhitelistCodec doc comment) - a distinct radio memory region
/// and a distinct list in the vendor CPS, but byte-for-byte identical
/// encoding, so the same codec is reused for both. Full radio-write support
/// added 2026-08-09. CallType is fixed at 0 - this list's edit popup
/// doesn't even show a Call Type column (unlike Talkgroup Whitelist), and
/// every entry captured live had this bit at 0, so it's likely not really a
/// "call type" concept here at all, just an unused reserved bit. Number is
/// likewise not independently meaningful - see TalkgroupWhitelistEntry's own
/// doc comment for the packed-position finding this mirrors.</summary>
public partial class DigitalContactWhitelistEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private long _dmrId;
    [ObservableProperty] private int _callType;

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

    /// <summary>See TalkgroupWhitelistEntry's own identical member for why
    /// only DmrId is tracked.</summary>
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
