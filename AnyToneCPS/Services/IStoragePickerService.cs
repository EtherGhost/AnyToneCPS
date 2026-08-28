using System.Collections.Generic;
using System.Threading.Tasks;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public enum UsedEncryptionKeyRemovalChoice
{
    Cancel,
    RemoveReferences
}

public interface IStoragePickerService
{
    Task<IProjectStorage?> PickOpenProjectAsync();
    Task<IProjectStorage?> PickSaveProjectAsync(string suggestedFileName);
    Task<IProjectStorage?> OpenRememberedProjectAsync();
    Task RememberProjectAsync(IProjectStorage projectStorage);
    Task ForgetRememberedProjectAsync();
    Task<IReadOnlyList<string>> PickCsvFilesAsync(string title);
    Task<string?> PickFolderAsync(string title);
    Task<bool> ConfirmOverwriteAsync(IProjectStorage projectStorage);
    Task<bool> ConfirmDiscardUnsavedChangesAsync();
    Task<UsedEncryptionKeyRemovalChoice> ConfirmRemoveUsedEncryptionKeyAsync(string message);

    /// <summary>Shows the Read from Radio confirmation, including the
    /// Digital Contact List/Encryption Keys include checkboxes (see
    /// RadioIncludeOptionsRequest's own doc comment) - both are slow/large
    /// enough that they're opt-in per read, not always included. Returns
    /// true (OK) or false (Cancel/closed) - <paramref name="options"/> is
    /// only meaningful to apply back when this returns true.</summary>
    Task<bool> ShowReadOptionsDialogAsync(RadioIncludeOptionsRequest options);

    /// <summary>The one real write-to-radio confirmation gate -
    /// <paramref name="summary"/> should be a plain-English description of
    /// exactly what's about to be written (e.g. "Channel 3990: Name 'AV00'
    /// -> 'ZZDIFFTST1'; no other fields changed."). Also folds in the same
    /// Digital Contact List/Encryption Keys include checkboxes
    /// ShowReadOptionsDialogAsync shows for reads - see
    /// RadioIncludeOptionsRequest's own doc comment for why write needs its
    /// own independent choice rather than reusing the read-side one.</summary>
    Task<bool> ConfirmWriteToRadioAsync(string summary, RadioIncludeOptionsRequest options);

    /// <summary>Shows the 5Tone Settings "Special Call" popup - see
    /// FiveToneSpecialCallDialogRequest's own doc comment. Returns true
    /// (OK) or false (Cancel/closed) - the request's own Values/GroupNo
    /// are only meaningful to apply back to the real model when this
    /// returns true.</summary>
    Task<bool> ShowFiveToneSpecialCallDialogAsync(FiveToneSpecialCallDialogRequest request);

    /// <summary>Double-clicking a 5Tone ID row that's already been set by
    /// &amp;Special Call asks this, matching the real vendor CPS gesture:
    /// "Reset special call of this channel, ok or no?" - not the most
    /// discoverable interaction, but it's how the vendor CPS itself works.</summary>
    Task<bool> ConfirmResetFiveToneSpecialCallAsync();

    /// <summary>Shows the DTMF Settings "Special Call" popup - see
    /// DtmfSpecialCallDialogRequest's own doc comment. Returns true (OK) or
    /// false (Cancel/closed).</summary>
    Task<bool> ShowDtmfSpecialCallDialogAsync(DtmfSpecialCallDialogRequest request);

    /// <summary>Copies the full text of a status/warnings message (e.g. a
    /// Read/Write From Radio result) to the system clipboard - added so the
    /// whole message can be copied in one action instead of the per-row
    /// text selection each warning line's own SelectableTextBlock otherwise
    /// limits the user to.</summary>
    Task CopyToClipboardAsync(string text);
}

public interface IProjectStorage
{
    string DisplayLocation { get; }
    Task<RadioProjectData?> LoadAsync();
    Task SaveAsync(RadioProjectData project);
}

public sealed class NullStoragePickerService : IStoragePickerService
{
    public Task<IProjectStorage?> PickOpenProjectAsync() => Task.FromResult<IProjectStorage?>(null);
    public Task<IProjectStorage?> PickSaveProjectAsync(string suggestedFileName) => Task.FromResult<IProjectStorage?>(null);
    public Task<IProjectStorage?> OpenRememberedProjectAsync() => Task.FromResult<IProjectStorage?>(null);
    public Task RememberProjectAsync(IProjectStorage projectStorage) => Task.CompletedTask;
    public Task ForgetRememberedProjectAsync() => Task.CompletedTask;
    public Task<IReadOnlyList<string>> PickCsvFilesAsync(string title) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
    public Task<bool> ConfirmOverwriteAsync(IProjectStorage projectStorage) => Task.FromResult(false);
    public Task<bool> ConfirmDiscardUnsavedChangesAsync() => Task.FromResult(false);
    public Task<UsedEncryptionKeyRemovalChoice> ConfirmRemoveUsedEncryptionKeyAsync(string message) =>
        Task.FromResult(UsedEncryptionKeyRemovalChoice.Cancel);
    public Task<bool> ShowReadOptionsDialogAsync(RadioIncludeOptionsRequest options) => Task.FromResult(false);
    public Task<bool> ConfirmWriteToRadioAsync(string summary, RadioIncludeOptionsRequest options) => Task.FromResult(false);
    public Task<bool> ShowFiveToneSpecialCallDialogAsync(FiveToneSpecialCallDialogRequest request) => Task.FromResult(false);
    public Task<bool> ConfirmResetFiveToneSpecialCallAsync() => Task.FromResult(false);
    public Task<bool> ShowDtmfSpecialCallDialogAsync(DtmfSpecialCallDialogRequest request) => Task.FromResult(false);
    public Task CopyToClipboardAsync(string text) => Task.CompletedTask;
}
