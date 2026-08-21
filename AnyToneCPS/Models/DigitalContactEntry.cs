using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>One entry in the big Digital Contact (DMR-ID address book)
/// database. Editable in the project file and full radio write support
/// added 2026-08-09 (matching the vendor CPS's own per-row edit fields).
/// CallType is kept as a raw int (not "Group Call"/"Private Call") for
/// decode fidelity - <see cref="CallTypeText"/> is the editable/UI-facing
/// wrapper. The xbenkozx/anytone-cps reference project's own read-only
/// table view decodes the D890UV's full, unmasked byte as a clean 0/1/2
/// Private/Group/All Call enum (digital_contacts_table_model.cpp),
/// matching the vendor CPS's own Call Type dropdown order (Private Call,
/// Group Call, All Call) confirmed directly from the vendor CPS edit
/// popup.
///
/// <see cref="IsFriend"/> added 2026-08-09, same day, after a live capture
/// showed the vendor CPS's separate "Friend List Edit" dialog isn't a
/// separate memory region at all - see DigitalContactCodec's own doc
/// comment for the full finding. Exposed here as a plain checkbox on this
/// entry's own edit panel rather than a dedicated picker view, per
/// explicit user request (simpler than mirroring the vendor's search-and-
/// add dialog) - MainViewModel enforces the vendor's own "max 1000
/// friends" cap since that's a whole-list constraint this entry can't
/// check on its own.</summary>
public partial class DigitalContactEntry : ObservableValidator
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private int _callType;
    [ObservableProperty] private string _callAlert = "None";
    [ObservableProperty] private bool _isFriend;
    [ObservableProperty] private long _radioId;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _callsign = "";
    [ObservableProperty] private string _state = "";
    [ObservableProperty] private string _country = "";
    [ObservableProperty] private string _remarks = "";

    /// <summary>Matches the vendor CPS edit popup's own dropdown order
    /// (confirmed directly from the screenshot) and CallType's own raw byte
    /// values (0/1/2).</summary>
    public IReadOnlyList<string> CallTypeOptions { get; } = ["Private Call", "Group Call", "All Call"];

    public string CallTypeText
    {
        get => CallType switch
        {
            0 => "Private Call",
            1 => "Group Call",
            2 => "All Call",
            _ => "Private Call"
        };
        set => CallType = value switch
        {
            "Group Call" => 1,
            "All Call" => 2,
            _ => 0
        };
    }

    private static readonly IReadOnlyList<string> GroupOrAllCallAlertOptions = ["None", "Online Alert"];
    private static readonly IReadOnlyList<string> PrivateCallAlertOptions = ["None", "Ring", "Online Alert"];

    /// <summary>Same shape as TalkgroupEntry.CallAlertOptions - "Ring" only
    /// offered for Private Call. Unlike Talkgroup's, this hasn't been
    /// independently live-confirmed for this entity - see
    /// DigitalContactCodec's own doc comment.</summary>
    public IReadOnlyList<string> CallAlertOptions => CallTypeText == "Private Call" ? PrivateCallAlertOptions : GroupOrAllCallAlertOptions;

    /// <summary>Drives IsEnabled for both the TG/DMR ID and Call Alert
    /// controls, per the vendor CPS's own edit popup behavior.</summary>
    public bool IsAllCallFieldsEditable => CallTypeText != "All Call";

    /// <summary>See DmrIdValidation's own doc comment for why this wrapper
    /// exists. Bypassed while CallTypeText is "All Call" - same reasoning as
    /// TalkgroupEntry.DmrIdText (the field is disabled there anyway).</summary>
    [CustomValidation(typeof(DigitalContactEntry), nameof(ValidateRadioIdText))]
    public string RadioIdText
    {
        get => RadioId.ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(RadioIdText));
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var radioId) && (CallTypeText == "All Call" || DmrIdValidation.IsValidDmrId(radioId)))
            {
                RadioId = radioId;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateRadioIdText(string? value, ValidationContext context) =>
        context.ObjectInstance is DigitalContactEntry { CallTypeText: "All Call" } ? ValidationResult.Success : DmrIdValidation.ValidateDmrIdText(value, context);

    partial void OnCallTypeChanged(int value)
    {
        OnPropertyChanged(nameof(CallTypeText));
        OnPropertyChanged(nameof(CallAlertOptions));
        OnPropertyChanged(nameof(IsAllCallFieldsEditable));
        // The RadioIdText validator bypasses its range check while CallType
        // is "All Call" - re-validate against the current text so an error
        // appears/clears immediately on the CallType flip, not just on the
        // next keystroke into RadioIdText itself.
        ValidateProperty(RadioIdText, nameof(RadioIdText));
        OnPropertyChanged(nameof(HasErrors));
        if (CallTypeText == "All Call")
        {
            CallAlert = "None";
        }
    }

    partial void OnRadioIdChanged(long value) => OnPropertyChanged(nameof(RadioIdText));
}
