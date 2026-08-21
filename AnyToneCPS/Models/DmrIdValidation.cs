using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace AnyToneCPS.Models;

/// <summary>
/// Shared DMR ID range validator (CodeplugLimits.DmrIdMin-DmrIdMax) reused by
/// every DMR-ID-bearing entity, so the same check can't drift field-by-field.
/// Added 2026-08-08 after a live test showed typing an out-of-range DMR ID
/// (e.g. 87654321, which is bigger than the real 24-bit max) into the vendor
/// CPS silently snapped it to the 16777215 "All Call" sentinel and flipped an
/// unrelated field (Call Type) - this app previously only had a soft
/// "Warning:" message for the same problem (MainViewModel.Validation.cs),
/// which never blocked typing an invalid value or writing it to the radio.
/// </summary>
public static class DmrIdValidation
{
    /// <summary>Also used by each entity's DmrIdText setter to decide
    /// whether to actually commit the parsed value to the canonical DmrId
    /// property - same "only commit a genuinely valid value, but never
    /// revert the displayed text" gate OptionalSettingsEntry's VFO Scan
    /// frequency fields already established.</summary>
    public static bool IsValidDmrId(long dmrId) => dmrId >= CodeplugLimits.DmrIdMin && dmrId <= CodeplugLimits.DmrIdMax;

    public static ValidationResult? ValidateDmrIdText(string? value, ValidationContext context)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var dmrId))
        {
            return new ValidationResult("Enter a whole number.", [context.MemberName!]);
        }

        return IsValidDmrId(dmrId)
            ? ValidationResult.Success
            : new ValidationResult($"Must be {CodeplugLimits.DmrIdMin}-{CodeplugLimits.DmrIdMax}.", [context.MemberName!]);
    }
}
