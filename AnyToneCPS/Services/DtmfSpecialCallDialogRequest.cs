namespace AnyToneCPS.Services;

/// <summary>What the DTMF Settings "Special Call" popup shows and reports
/// back. Simpler than FiveToneSpecialCallDialogRequest - only ANI exists as
/// a calling type, and the M1-M16 slots are fixed, so there's no
/// CallingType or group redirect to carry.</summary>
public sealed class DtmfSpecialCallDialogRequest
{
    public string OtherSideId { get; set; } = "";
}
