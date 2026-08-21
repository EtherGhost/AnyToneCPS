namespace AnyToneCPS.Services;

/// <summary>What the Read/Write "include options" popup shows and reports
/// back - both Digital Contact List and Encryption Keys can be slow/large,
/// so the user picks per read/write rather than always including them.
/// Pre-filled from whichever value was chosen last time.</summary>
public sealed class RadioIncludeOptionsRequest
{
    public bool IncludeDigitalContactList { get; set; }
    public bool IncludeEncryptionKeys { get; set; }

    /// <summary>Write-side only. `DigitalContactWriter.Write` always
    /// rewrites the whole contact stream from memory, not per-entry - so
    /// writing a list that was never genuinely read from this radio would
    /// replace the real on-radio list with an incomplete one. When false,
    /// the dialog should disable the checkbox rather than let it be
    /// checked.</summary>
    public bool DigitalContactListAvailableToInclude { get; set; } = true;

    /// <summary>Contacts currently in memory, shown next to the checkbox as
    /// a sanity check - null means don't show a count.</summary>
    public int? DigitalContactCount { get; set; }
}
