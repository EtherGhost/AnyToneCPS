using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>Which of the 3 encryption key lists an <see cref="EncryptionKeyEntry"/>
/// belongs to - needed because EncryptionKey/EncryptionId's validation rules
/// (and which of the two columns holds the real key material) differ per
/// list. See <see cref="Services.Radio.Codecs.EncryptionKeyCodec"/>'s doc
/// comment for the full column-meaning writeup this mirrors.</summary>
public enum EncryptionKeyKind
{
    Basic,
    Arc4,
    Aes
}

/// <summary>
/// One slot in the Digital/ARC4/AES encryption key lists - see
/// <see cref="Services.Radio.Codecs.EncryptionKeyCodec"/>'s doc comment for
/// which of EncryptionKey/EncryptionId holds the real key material for each
/// of the 3 lists (it differs per list). File-save dirty tracking for this
/// entity is handled generically at the MainViewModel level (every property
/// change marks the project dirty) - unlike Channel/Zone/ScanList, this
/// class only needs its OWN dirty concept for radio-write eligibility.
///
/// HasAnyPendingRadioWrite uses the same "_radioSyncSnapshot is null means
/// pending" convention as ChannelEntry/ZoneEntry/ScanListEntry - a loaded
/// project's key values are correctly treated as pending-write, same as any
/// other loaded entity, until a real Write or opt-in Read confirms them.
/// The one place this needs active management is
/// MainViewModel.FillMissingSlots: a freshly-synthesized "Off" placeholder
/// slot is a UI stand-in, not something meant to be written to the radio, so that
/// method calls MarkRadioSynced() on it immediately - if it didn't, the
/// very first Write of a session could blast "Off" over whatever real keys
/// the radio already has, since encryption keys are read from the radio
/// only opt-in (IncludeEncryptionKeysList, off by default - the vendor CPS
/// itself never reads them back at all), so a full Read+Write round trip
/// can easily happen without ever confirming these slots' real values.
///
/// EncryptionKeyText/EncryptionIdText added 2026-08-09, converting this
/// class to ObservableValidator - same "block Save/Write via HasErrors,
/// never revert what was typed" pattern as RadioIdEntry.DmrIdText, replacing
/// what used to be a soft-looking-but-actually-blocking manual check in
/// MainViewModel.Validation.cs (ValidateEncryptionKeys) with real per-field
/// live validation. The exact format rules (64 hex/"Off" for AES, 1-10
/// hex/"Off" for ARC4, 4 decimal digits for Basic) are unchanged - only the
/// "other" column per Kind (never independently confirmed to have a real
/// radio address - see this class's own doc comment above) is left
/// unvalidated, matching the original manual check's own scope exactly.
/// </summary>
public partial class EncryptionKeyEntry : ObservableValidator
{
    public required EncryptionKeyKind Kind { get; init; }

    /// <summary>Radio-write baseline, same idea as ChannelEntry/ZoneEntry/
    /// ScanListEntry's own <c>_radioSyncSnapshot</c> - null until
    /// <see cref="MarkRadioSynced"/> is called.</summary>
    private EncryptionKeySnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _encryptionKey = "";
    [ObservableProperty] private string _encryptionId = "";

    /// <summary>Pure pass-through to <see cref="EncryptionKey"/> - exists so
    /// XAML can bind a validated property. Only meaningful for Kind == Arc4,
    /// see ValidateEncryptionKeyText. Validation itself runs from
    /// <see cref="OnEncryptionKeyChanged"/> instead of this setter, so it
    /// fires no matter which property was actually assigned (raw
    /// EncryptionKey, e.g. from a project load, or this Text property from
    /// the UI) - same reasoning as EncryptionIdText below.</summary>
    [CustomValidation(typeof(EncryptionKeyEntry), nameof(ValidateEncryptionKeyText))]
    public string EncryptionKeyText
    {
        get => EncryptionKey;
        set => EncryptionKey = value;
    }

    /// <summary>Pure pass-through to <see cref="EncryptionId"/> - see
    /// EncryptionKeyText's own doc comment for why validation lives in
    /// <see cref="OnEncryptionIdChanged"/> instead of here. Meaningful for
    /// Kind == Aes/Basic, see ValidateEncryptionIdText.</summary>
    [CustomValidation(typeof(EncryptionKeyEntry), nameof(ValidateEncryptionIdText))]
    public string EncryptionIdText
    {
        get => EncryptionId;
        set => EncryptionId = value;
    }

    public bool HasAnyPendingRadioWrite => _radioSyncSnapshot is null
        || EncryptionKey != _radioSyncSnapshot.EncryptionKey
        || EncryptionId != _radioSyncSnapshot.EncryptionId;

    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = new EncryptionKeySnapshot(EncryptionKey, EncryptionId);
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    // Validates on ANY change to the raw property - not just via
    // EncryptionKeyText's setter - so a direct assignment (project load,
    // radio read, or a test/programmatic mutation) is checked exactly the
    // same as a value typed through the UI. Confirmed necessary by
    // InvalidEncryptionKeyFormatsBlockSave, which sets the raw property
    // directly and expects HasBlockingValidationErrors to react.
    partial void OnEncryptionKeyChanged(string value)
    {
        ValidateProperty(value, nameof(EncryptionKeyText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(EncryptionKeyText));
        OnPropertyChanged(nameof(HasErrors));
    }

    partial void OnEncryptionIdChanged(string value)
    {
        ValidateProperty(value, nameof(EncryptionIdText));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
        OnPropertyChanged(nameof(EncryptionIdText));
        OnPropertyChanged(nameof(HasErrors));
    }

    private sealed record EncryptionKeySnapshot(string EncryptionKey, string EncryptionId);

    /// <summary>ARC4's real key field - "Off" or 1-10 hex chars, left-zero-
    /// padded on write (see EncryptionKeyCodec.EncodeArc4Key). For AES/Basic,
    /// this column has no confirmed radio address (see this class's own doc
    /// comment), so it's left unvalidated rather than guessed.</summary>
    public static ValidationResult? ValidateEncryptionKeyText(string? value, ValidationContext context)
    {
        if (context.ObjectInstance is not EncryptionKeyEntry { Kind: EncryptionKeyKind.Arc4 })
        {
            return ValidationResult.Success;
        }

        if (value == "Off")
        {
            return ValidationResult.Success;
        }

        const int maxHexChars = 10;
        return !string.IsNullOrEmpty(value) && value.Length <= maxHexChars && value.All(Uri.IsHexDigit)
            ? ValidationResult.Success
            : new ValidationResult($"Must be 1-{maxHexChars} hex characters (or 'Off').", [context.MemberName!]);
    }

    /// <summary>AES's real key field (64 hex chars or "Off") and Basic's
    /// real code field (exactly 4 decimal digits). For ARC4, this column
    /// has no confirmed radio address, so it's left unvalidated.</summary>
    public static ValidationResult? ValidateEncryptionIdText(string? value, ValidationContext context)
    {
        if (context.ObjectInstance is not EncryptionKeyEntry entry)
        {
            return ValidationResult.Success;
        }

        switch (entry.Kind)
        {
            case EncryptionKeyKind.Aes:
                if (value == "Off")
                {
                    return ValidationResult.Success;
                }

                return value is { Length: 64 } && value.All(Uri.IsHexDigit)
                    ? ValidationResult.Success
                    : new ValidationResult("Must be exactly 64 hex characters (or 'Off').", [context.MemberName!]);

            case EncryptionKeyKind.Basic:
                return value is { Length: 4 } && value.All(char.IsAsciiDigit)
                    ? ValidationResult.Success
                    : new ValidationResult("Must be exactly 4 decimal digits.", [context.MemberName!]);

            default:
                return ValidationResult.Success;
        }
    }
}
