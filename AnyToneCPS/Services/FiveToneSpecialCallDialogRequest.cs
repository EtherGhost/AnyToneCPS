using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

/// <summary>
/// What the 5Tone Settings "Special Call" popup needs to show itself and
/// report back. <see cref="ShowGroupNo"/> is only true for the row-level
/// popup (triggered from the ID table's own "&amp;Special Call") - the
/// BOT/EOT popups don't have that field at all (confirmed
/// 2026-08-05). <see cref="Values"/> is pre-filled from the target's
/// current state and edited in place by the dialog - the caller only
/// applies it back to the real model if this method returns true (OK, not
/// Cancel).
/// </summary>
public sealed class FiveToneSpecialCallDialogRequest
{
    public required bool ShowGroupNo { get; init; }
    public int GroupNo { get; set; } = 1;
    public int MaxGroupNo { get; init; } = CodeplugLimits.FiveToneIdMax;
    public required int OtherSideIdMaxLength { get; init; }
    public FiveToneSpecialCallEntry Values { get; } = new();
}
