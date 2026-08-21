using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// A DMR contact ("Digital Contact / Talk Group" in the vendor CPS UI).
/// Named "Talkgroup" to match the reference project's naming and to avoid
/// confusion with <see cref="ChannelEntry.Contact"/>, which is an existing,
/// unrelated free-text field on the channel entry.
///
/// Full radio-write support added 2026-08-07, confirmed via two live
/// differential write captures - see TalkgroupCodec's own doc comment for
/// the byte-level findings. When CallType is "All Call" the vendor CPS
/// itself disables the DMR ID and Call Alert controls (DMR ID reads back
/// as the 16777215/0xFFFFFF sentinel, Call Alert forced to "None") - this
/// class mirrors that by forcing CallAlert back to "None" whenever CallType
/// becomes "All Call"; TalkgroupCodec.Encode independently forces the
/// DMR ID sentinel too, so this class doesn't need to touch DmrId itself.
/// </summary>
public partial class TalkgroupEntry : ObservableValidator
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private long _dmrId;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _callType = "Group Call";
    [ObservableProperty] private string _callAlert = "None";

    /// <summary>DMR ID is disabled in the UI once CallType is "All Call"
    /// (see IsAllCallFieldsEditable) - the bypass below just means a stale
    /// real DmrId sitting under an All Call row (loaded from an old project
    /// or a fresh radio read, before TalkgroupCodec.Encode forces the
    /// sentinel at write time) never shows a false validation error. See
    /// DmrIdValidation's own doc comment for why this wrapper exists at all.</summary>
    [CustomValidation(typeof(TalkgroupEntry), nameof(ValidateDmrIdText))]
    public string DmrIdText
    {
        get => DmrId.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(DmrIdText));
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var dmrId) && (CallType == "All Call" || DmrIdValidation.IsValidDmrId(dmrId)))
            {
                DmrId = dmrId;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateDmrIdText(string? value, ValidationContext context) =>
        context.ObjectInstance is TalkgroupEntry { CallType: "All Call" } ? ValidationResult.Success : DmrIdValidation.ValidateDmrIdText(value, context);

    /// <summary>Matches MainViewModel.ContactCallTypes exactly. Exposed here
    /// too (rather than only on the ViewModel) so Mobile's compact per-row
    /// item template - whose DataContext is this entry, not the ViewModel -
    /// can bind a real ComboBox instead of a free-text box, without a
    /// cross-DataContext binding.</summary>
    public IReadOnlyList<string> CallTypeOptions { get; } = ["Group Call", "Private Call", "All Call"];

    private static readonly IReadOnlyList<string> GroupOrAllCallAlertOptions = ["None", "Online Alert"];
    private static readonly IReadOnlyList<string> PrivateCallAlertOptions = ["None", "Ring", "Online Alert"];

    /// <summary>Confirmed 2026-08-07 via live capture: "Ring" is only
    /// offered for Private Call in the vendor CPS - Group/All Call only
    /// offer None/Online Alert.</summary>
    public IReadOnlyList<string> CallAlertOptions => CallType == "Private Call" ? PrivateCallAlertOptions : GroupOrAllCallAlertOptions;

    /// <summary>Drives IsEnabled for both the DMR ID and Call Alert controls
    /// - the vendor CPS disables both together when CallType is All Call.</summary>
    public bool IsAllCallFieldsEditable => CallType != "All Call";

    partial void OnCallTypeChanged(string value)
    {
        OnPropertyChanged(nameof(CallAlertOptions));
        OnPropertyChanged(nameof(IsAllCallFieldsEditable));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        // The DmrIdText validator bypasses its range check while CallType is
        // "All Call" - re-validate against the (possibly now-stale) current
        // text so an error appears/clears immediately on the CallType flip,
        // not just on the next keystroke into DmrIdText itself.
        ValidateProperty(DmrIdText, nameof(DmrIdText));
        OnPropertyChanged(nameof(HasErrors));
        if (value == "All Call")
        {
            CallAlert = "None";
        }
    }

    /// <summary>Radio-write baseline only - see FiveToneIdEntry's own doc
    /// comment for the split rationale. Deliberately excludes <see
    /// cref="Number"/>.</summary>
    private TalkgroupSnapshot? _radioSyncSnapshot;

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null || CreateRadioSnapshot() != _radioSyncSnapshot;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateRadioSnapshot();
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private TalkgroupSnapshot CreateRadioSnapshot() => new(DmrId, Name, CallType, CallAlert);

    private sealed record TalkgroupSnapshot(long DmrId, string Name, string CallType, string CallAlert);

    partial void OnDmrIdChanged(long value)
    {
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(DmrIdText));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    partial void OnCallAlertChanged(string value) => OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
}
