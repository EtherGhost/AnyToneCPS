using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// Full radio-write support added 2026-08-06 - see RadioIdCodec's own doc
/// comment for the byte-level confirmation status (indirectly confirmed via
/// Channel's own name-field offset match, not independently live-tested for
/// this record's own fields).
/// </summary>
public partial class RadioIdEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private long _dmrId;
    [ObservableProperty] private string _name = "";

    /// <summary>See DmrIdValidation's own doc comment for why this exists -
    /// the underlying <see cref="DmrId"/> stays directly settable (radio
    /// reads/project loads always land a valid value), this wrapper is only
    /// for the XAML TextBox so a bad keystroke reports an error and lets
    /// typing continue instead of either reverting or silently writing an
    /// out-of-range ID.</summary>
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

    /// <summary>Radio-write baseline only - see FiveToneIdEntry's own doc
    /// comment for the split rationale. Deliberately excludes <see
    /// cref="Number"/>.</summary>
    private RadioIdSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private RadioIdSnapshot CreateRadioSnapshot() => new(DmrId, Name);

    private sealed record RadioIdSnapshot(long DmrId, string Name);

    partial void OnDmrIdChanged(long value)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(DmrIdText));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
