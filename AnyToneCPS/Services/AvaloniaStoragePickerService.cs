using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AnyToneCPS.Models;
using AnyToneCPS.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace AnyToneCPS.Services;

public sealed class AvaloniaStoragePickerService(TopLevel topLevel) : IStoragePickerService
{
    private readonly LocalProjectSettingsStore _settingsStore = new();

    private static readonly FilePickerFileType ProjectFileType = new("AnyToneCPS project")
    {
        Patterns = ["*.dat", "*.json"]
    };

    private static readonly FilePickerFileType CsvFileType = new("CSV files")
    {
        Patterns = ["*.csv", "*.CSV"]
    };

    public async Task<IProjectStorage?> PickOpenProjectAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open codeplug",
            AllowMultiple = false,
            FileTypeFilter = [ProjectFileType, FilePickerFileTypes.All]
        });

        return await CreateProjectStorageAsync(files.FirstOrDefault());
    }

    public async Task<IProjectStorage?> PickSaveProjectAsync(string suggestedFileName)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save codeplug",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "dat",
            FileTypeChoices = [ProjectFileType, FilePickerFileTypes.All],
            ShowOverwritePrompt = true
        });

        return await CreateProjectStorageAsync(file);
    }

    public async Task<IProjectStorage?> OpenRememberedProjectAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        if (settings is null)
        {
            return null;
        }

        if (settings.Kind == "file" && !string.IsNullOrWhiteSpace(settings.Location))
        {
            return File.Exists(settings.Location)
                ? new JsonFileProjectStorage(settings.Location)
                : null;
        }

        IStorageFile? file = null;
        if (!string.IsNullOrWhiteSpace(settings.Bookmark))
        {
            file = await topLevel.StorageProvider.OpenFileBookmarkAsync(settings.Bookmark);
        }

        if (file is null && Uri.TryCreate(settings.Location, UriKind.Absolute, out var uri))
        {
            file = await topLevel.StorageProvider.TryGetFileFromPathAsync(uri);
        }

        return file is null
            ? null
            : new AvaloniaProjectStorage(file, settings.Bookmark);
    }

    public async Task RememberProjectAsync(IProjectStorage projectStorage)
    {
        switch (projectStorage)
        {
            case JsonFileProjectStorage fileProjectStorage:
                await _settingsStore.SaveAsync(new ProjectStorageSettings
                {
                    Kind = "file",
                    Location = fileProjectStorage.Path,
                    DisplayLocation = fileProjectStorage.DisplayLocation
                });
                return;

            case AvaloniaProjectStorage avaloniaProjectStorage:
                await _settingsStore.SaveAsync(new ProjectStorageSettings
                {
                    Kind = "bookmark",
                    Location = avaloniaProjectStorage.Location,
                    Bookmark = avaloniaProjectStorage.Bookmark,
                    DisplayLocation = avaloniaProjectStorage.DisplayLocation
                });
                return;

        }
    }

    public Task ForgetRememberedProjectAsync()
    {
        return _settingsStore.ClearAsync();
    }

    public async Task<IReadOnlyList<string>> PickCsvFilesAsync(string title)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = [CsvFileType, FilePickerFileTypes.All]
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<bool> ConfirmOverwriteAsync(IProjectStorage projectStorage)
    {
        if (projectStorage is not JsonFileProjectStorage fileProjectStorage)
        {
            return true;
        }

        var path = fileProjectStorage.DisplayLocation;
        if (!File.Exists(path))
        {
            return true;
        }

        if (topLevel is not Window owner)
        {
            return false;
        }

        var result = false;
        var dialog = new Window
        {
            Title = "Overwrite file?",
            Width = 440,
            Height = 170,
            MinWidth = 440,
            MinHeight = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock
        {
            Text = $"File already exists:\n{path}\n\nOverwrite it?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92
        };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var overwriteButton = new Button
        {
            Content = "Overwrite",
            MinWidth = 92
        };
        overwriteButton.Click += (_, _) => dialog.Close(true);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, overwriteButton }
        };
        Grid.SetRow(buttons, 1);

        dialog.Content = new Grid
        {
            Margin = new Avalonia.Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                message,
                buttons
            }
        };

        result = await dialog.ShowDialog<bool>(owner);
        return result;
    }

    public async Task CopyToClipboardAsync(string text)
    {
        if (topLevel.Clipboard is { } clipboard)
        {
            using var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(dataTransfer);
        }
    }

    private static async Task<IProjectStorage?> CreateProjectStorageAsync(IStorageFile? file)
    {
        if (file is null)
        {
            return null;
        }

        var localPath = file.TryGetLocalPath();
        var bookmark = file.CanBookmark ? await file.SaveBookmarkAsync() : null;
        return string.IsNullOrWhiteSpace(localPath)
            ? new AvaloniaProjectStorage(file, bookmark ?? "")
            : new JsonFileProjectStorage(localPath);
    }

    public Task<bool> ConfirmDiscardUnsavedChangesAsync()
    {
        return ShowConfirmationDialogAsync(
            "Unsaved changes",
            "There are unsaved changes.\n\nContinue and lose those changes?",
            "Continue");
    }

    /// <summary>No modal dialog concept on Mobile - MobileMainView.axaml's
    /// own Radio tab exposes IncludeDigitalContactList/IncludeEncryptionKeysList
    /// as always-visible inline checkboxes instead (see that view's own
    /// comment), so passing through unchanged here is correct: Read never
    /// had a confirmation step to begin with, this dialog is purely an
    /// extra opt-in layer on top of that, safe to skip on a platform that
    /// already exposes the same choice a different way.</summary>
    public Task<bool> ShowReadOptionsDialogAsync(RadioIncludeOptionsRequest options)
    {
        return ShowIncludeOptionsDialogAsync(
            "Read from radio",
            "Reading the whole known codeplug. This takes a few seconds to a minute or so - the two lists " +
            "below can each add significantly more time on their own, so they're opt-in.",
            "Read from radio",
            options,
            blockIfNoDialog: false);
    }

    /// <summary>On Desktop, shows the modal confirmation dialog with its own
    /// safety warning text (narrow writes have been found to corrupt
    /// unrelated flash). On Mobile (no Window to show a dialog in) that
    /// warning text is instead permanently visible next to the Write
    /// Changes to Radio button (MobileMainView.axaml's own "Write changes
    /// to radio" panel) rather than gated behind a per-click dialog, and the
    /// two checkboxes this method would otherwise let the user toggle are
    /// bound directly from Mobile's own "Include options" popup to
    /// <see cref="MainViewModel.WriteIncludeDigitalContactList"/>/
    /// <see cref="MainViewModel.WriteIncludeEncryptionKeys"/> - so passing
    /// through here (rather than hard-blocking) is safe: the information a
    /// blocking dialog would have shown is already on screen, just via a
    /// different mechanism, same as <see cref="ShowReadOptionsDialogAsync"/>
    /// already does. Until 2026-08-16 this used blockIfNoDialog: true and
    /// Mobile write-to-radio support didn't exist yet; now it does.</summary>
    public Task<bool> ConfirmWriteToRadioAsync(string summary, RadioIncludeOptionsRequest options)
    {
        return ShowIncludeOptionsDialogAsync(
            "Write to radio",
            $"{summary}\n\nWriting a single field safely requires reading and rewriting the radio's " +
            "whole known codeplug (not just this one field) - narrow single-record writes were found " +
            "to silently erase unrelated data sharing the same flash region. This will take several " +
            "minutes, not seconds. The two lists below can each add significantly more on top of that, " +
            "so they're opt-in per write - leaving one unchecked here does NOT discard those pending " +
            "edits, it just skips writing them out this time.",
            "Write to radio",
            options,
            blockIfNoDialog: false);
    }

    /// <summary>Shared by <see cref="ShowReadOptionsDialogAsync"/> and
    /// <see cref="ConfirmWriteToRadioAsync"/> - both need the same "confirm
    /// plus Digital Contact List/Encryption Keys checkboxes" shape, just
    /// different title/message/confirm text and fallback behavior when
    /// there's no Window available (see each caller's own doc comment for
    /// why those differ). Built entirely in code (no separate .axaml), same
    /// as <see cref="ShowFiveToneSpecialCallDialogAsync"/>.</summary>
    private async Task<bool> ShowIncludeOptionsDialogAsync(string title, string messageText, string confirmText, RadioIncludeOptionsRequest options, bool blockIfNoDialog)
    {
        if (topLevel is not Window owner)
        {
            return !blockIfNoDialog;
        }

        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            MinWidth = 480,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock
        {
            Text = messageText,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var digitalContactLabel = options.DigitalContactCount is { } count
            ? $"Include Digital Contact List ({count} currently loaded)"
            : "Include Digital Contact List";
        var digitalContactCheckBox = new CheckBox
        {
            Content = digitalContactLabel,
            IsChecked = options.DigitalContactListAvailableToInclude && options.IncludeDigitalContactList,
            IsEnabled = options.DigitalContactListAvailableToInclude
        };
        if (!options.DigitalContactListAvailableToInclude)
        {
            ToolTip.SetTip(digitalContactCheckBox, "Read the Digital Contact List from the radio at least once this session before writing it - this prevents accidentally replacing the whole real list on the radio with an incomplete one.");
        }

        var encryptionKeysCheckBox = new CheckBox { Content = "Include Encryption Keys", IsChecked = options.IncludeEncryptionKeys };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var confirmButton = new Button { Content = confirmText, MinWidth = 92 };
        confirmButton.Click += (_, _) =>
        {
            options.IncludeDigitalContactList = digitalContactCheckBox.IsChecked ?? false;
            options.IncludeEncryptionKeys = encryptionKeysCheckBox.IsChecked ?? false;
            dialog.Close(true);
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, confirmButton }
        };

        // The summary text is unbounded (one line per dirty entity/row,
        // e.g. Dev Options' "Force Model -> Image" can dirty everything at
        // once) - a real incident 2026-08-16: with SizeToContent.Height and
        // no scroll container, a long enough summary grew the window
        // taller than the screen and pushed Cancel/Write off-screen with
        // no way to reach them. Only the summary text itself scrolls
        // (capped at MaxHeight) - the checkboxes and buttons are both
        // actionable controls, so they stay outside the scroll area and
        // are always visible regardless of summary length.
        var scrollableMessage = new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = message
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children = { scrollableMessage, digitalContactCheckBox, encryptionKeysCheckBox, buttons }
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    public Task<bool> ConfirmResetFiveToneSpecialCallAsync()
    {
        return ShowConfirmationDialogAsync(
            "Reset special call",
            "Reset special call of this channel?",
            "Ok");
    }

    public async Task<UsedEncryptionKeyRemovalChoice> ConfirmRemoveUsedEncryptionKeyAsync(string messageText)
    {
        if (topLevel is not Window owner)
        {
            return UsedEncryptionKeyRemovalChoice.Cancel;
        }

        var dialog = new Window
        {
            Title = "Encryption key is in use",
            Width = 560,
            Height = 260,
            MinWidth = 560,
            MinHeight = 260,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock
        {
            Text = messageText,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92
        };
        cancelButton.Click += (_, _) => dialog.Close(UsedEncryptionKeyRemovalChoice.Cancel);

        var removeReferencesButton = new Button
        {
            Content = "Disable encryption on channels",
            MinWidth = 190
        };
        removeReferencesButton.Click += (_, _) => dialog.Close(UsedEncryptionKeyRemovalChoice.RemoveReferences);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, removeReferencesButton }
        };
        Grid.SetRow(buttons, 1);

        dialog.Content = new Grid
        {
            Margin = new Avalonia.Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                message,
                buttons
            }
        };

        return await dialog.ShowDialog<UsedEncryptionKeyRemovalChoice>(owner);
    }

    /// <summary>See IStoragePickerService's own doc comment. Built entirely
    /// in code (no separate .axaml), same as
    /// <see cref="ConfirmRemoveUsedEncryptionKeyAsync"/> above - avoids a
    /// dedicated dialog view-model/code-behind pair for what's otherwise a
    /// one-shot popup. Desktop-only, like every other dialog in this
    /// class (guarded by the same "topLevel is not Window" check) - on
    /// Android, MobileMainView.axaml exposes the same fields inline
    /// instead (no modal dialog concept there), see its own 5Tone Settings
    /// section.</summary>
    public async Task<bool> ShowFiveToneSpecialCallDialogAsync(FiveToneSpecialCallDialogRequest request)
    {
        if (topLevel is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "&Special Call",
            Width = 420,
            MinWidth = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var rootStack = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };

        ComboBox? groupNoBox = null;
        if (request.ShowGroupNo)
        {
            rootStack.Children.Add(new TextBlock { Text = "Choose Encoding Group NO." });
            groupNoBox = new ComboBox
            {
                ItemsSource = Enumerable.Range(1, request.MaxGroupNo).Select(i => i.ToString()).ToList(),
                SelectedItem = request.GroupNo.ToString(),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            rootStack.Children.Add(groupNoBox);
        }

        rootStack.Children.Add(new TextBlock { Text = "Choose Calling Type" });
        var callingTypeBox = new ComboBox
        {
            ItemsSource = FiveToneSpecialCallEntry.CallingTypeOptions,
            SelectedItem = request.Values.CallingTypeText,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        rootStack.Children.Add(callingTypeBox);

        // ONE shared "Other Side ID" box for Send Message AND ANI - NOT
        // 2 separate controls. Found the hard way 2026-08-05: BOT/EOT's
        // own PTTID formula ("E6"+OtherSideId) needs whatever was last
        // typed here even though PTTID's own group is hidden (matches the
        // real vendor CPS - the field survives a Calling Type switch, same
        // "stale value persists" behavior already confirmed for QDC 1200
        // Setting's own Private/Group Call ID fields) - an earlier version
        // of this dialog cleared it for PTTID, silently breaking BOT/EOT's
        // composition (row-level's own PTTID formula ignores OtherSideId
        // entirely, so that bug never showed up there).
        var otherSideIdBox = new TextBox { Text = request.Values.OtherSideId, MaxLength = request.OtherSideIdMaxLength };
        DigitOnlyInput.SetEnabled(otherSideIdBox, true);
        var otherSideIdGroup = new StackPanel
        {
            Spacing = 6,
            Children = { new TextBlock { Text = "The Other Side ID" }, otherSideIdBox }
        };

        var messageBox = new TextBox { Text = request.Values.Message, MaxLength = 15 };
        AsciiTextInput.SetEnabled(messageBox, true);
        var sendMessageGroup = new StackPanel
        {
            Spacing = 6,
            Children = { new TextBlock { Text = "Message" }, messageBox }
        };

        var intervalBox = new ComboBox
        {
            ItemsSource = FiveToneSpecialCallEntry.IntervalCharacterOptions,
            SelectedItem = request.Values.IntervalCharacterText,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var aniGroup = new StackPanel
        {
            Spacing = 6,
            Children = { new TextBlock { Text = "Interval Character" }, intervalBox }
        };

        rootStack.Children.Add(otherSideIdGroup);
        rootStack.Children.Add(sendMessageGroup);
        rootStack.Children.Add(aniGroup);

        void UpdateGroupVisibility()
        {
            var index = FiveToneSpecialCallEntry.CallingTypeOptions.ToList().IndexOf(callingTypeBox.SelectedItem as string ?? "");
            otherSideIdGroup.IsVisible = index != FiveToneSpecialCallEntry.CallingTypePttId;
            sendMessageGroup.IsVisible = index == FiveToneSpecialCallEntry.CallingTypeSendMessage;
            aniGroup.IsVisible = index == FiveToneSpecialCallEntry.CallingTypeAni;
        }

        callingTypeBox.SelectionChanged += (_, _) => UpdateGroupVisibility();
        UpdateGroupVisibility();

        var errorText = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
        rootStack.Children.Add(errorText);

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var okButton = new Button { Content = "Ok", MinWidth = 92 };
        okButton.Click += async (_, _) =>
        {
            var callingTypeIndex = FiveToneSpecialCallEntry.CallingTypeOptions.ToList().IndexOf(callingTypeBox.SelectedItem as string ?? "");
            var callingType = callingTypeIndex >= 0 ? (byte)callingTypeIndex : (byte)0;

            // Confirmed 2026-08-05, then corrected the same day:
            // Other Side ID must be EXACTLY as long as the current Self
            // ID, not just "no longer than" - too long is already
            // impossible to type (MaxLength caps it), so only "too
            // short" needs an explicit check here. Only meaningful while
            // its own group is actually visible (Send Message/ANI),
            // matching the real vendor CPS's own gating.
            if (callingType != FiveToneSpecialCallEntry.CallingTypePttId)
            {
                var length = (otherSideIdBox.Text ?? "").Length;
                if (length != request.OtherSideIdMaxLength)
                {
                    errorText.Text = $"The Other Side ID must be exactly {request.OtherSideIdMaxLength} digits long (matching the current Self ID).";
                    errorText.IsVisible = true;
                    return;
                }
            }

            errorText.IsVisible = false;

            if (groupNoBox?.SelectedItem is string groupNoText && int.TryParse(groupNoText, out var groupNo))
            {
                request.GroupNo = groupNo;
            }

            request.Values.CallingType = callingType;
            request.Values.OtherSideId = otherSideIdBox.Text ?? "";
            request.Values.Message = request.Values.IsSendMessage ? messageBox.Text ?? "" : "";
            var intervalIndex = FiveToneSpecialCallEntry.IntervalCharacterOptions.ToList().IndexOf(intervalBox.SelectedItem as string ?? "");
            request.Values.IntervalCharacter = request.Values.IsAni && intervalIndex >= 0 ? (byte)intervalIndex : (byte)0;
            request.Values.IsConfigured = true;

            // BOT/EOT's own Send Message formula isn't confirmed yet (see
            // FiveToneSpecialCallEntry's class doc comment) - Encode ID
            // won't auto-update for this combination, so say so rather
            // than leaving the user to wonder why nothing happened.
            if (!request.ShowGroupNo && request.Values.IsSendMessage)
            {
                await ShowInfoDialogAsync(
                    dialog,
                    "Encode ID not auto-updated",
                    "Send Message's Encode ID composition for PTT ID Starting/Ending isn't confirmed yet - " +
                    "the Calling Type/Other Side ID/Message were saved, but Encode ID won't be filled in automatically. Edit it manually if needed.");
            }

            dialog.Close(true);
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, okButton }
        };
        rootStack.Children.Add(buttons);

        dialog.Content = rootStack;

        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>See IStoragePickerService's own doc comment. Much simpler
    /// than <see cref="ShowFiveToneSpecialCallDialogAsync"/> - only ANI
    /// exists as a calling type here (see DtmfSpecialCallDialogRequest's
    /// own doc comment), so there's no calling-type switch or group NO.
    /// redirect to wire up, just the one Other Side ID field.</summary>
    public async Task<bool> ShowDtmfSpecialCallDialogAsync(DtmfSpecialCallDialogRequest request)
    {
        if (topLevel is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "DTMF Special Call",
            Width = 340,
            MinWidth = 340,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var rootStack = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };

        rootStack.Children.Add(new TextBlock { Text = "Choose Calling Type" });
        rootStack.Children.Add(new ComboBox
        {
            ItemsSource = new[] { "ANI" },
            SelectedIndex = 0,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });

        var otherSideIdBox = new TextBox { Text = request.OtherSideId, MaxLength = CodeplugLimits.DtmfOtherSideIdMaxLength };
        DigitOnlyInput.SetEnabled(otherSideIdBox, true);
        rootStack.Children.Add(new StackPanel
        {
            Spacing = 6,
            Children = { new TextBlock { Text = "The Other Side ID" }, otherSideIdBox }
        });

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var okButton = new Button { Content = "Ok", MinWidth = 92 };
        okButton.Click += (_, _) =>
        {
            request.OtherSideId = otherSideIdBox.Text ?? "";
            dialog.Close(true);
        };

        rootStack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, okButton }
        });

        dialog.Content = rootStack;

        return await dialog.ShowDialog<bool>(owner);
    }

    private static async Task ShowInfoDialogAsync(Window owner, string title, string messageText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            MinWidth = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock { Text = messageText, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var okButton = new Button { Content = "Ok", MinWidth = 92, HorizontalAlignment = HorizontalAlignment.Right };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children = { message, okButton }
        };

        await dialog.ShowDialog(owner);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string messageText, string confirmText)
    {
        if (topLevel is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 170,
            MinWidth = 440,
            MinHeight = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock
        {
            Text = messageText,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92
        };
        cancelButton.Click += (_, _) => dialog.Close(false);

        var confirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 92
        };
        confirmButton.Click += (_, _) => dialog.Close(true);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, confirmButton }
        };
        Grid.SetRow(buttons, 1);

        dialog.Content = new Grid
        {
            Margin = new Avalonia.Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                message,
                buttons
            }
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    private sealed class AvaloniaProjectStorage(IStorageFile file, string bookmark) : IProjectStorage
    {
        public string Bookmark { get; } = bookmark;
        public string Location { get; } = file.Path?.ToString() ?? "";
        public string DisplayLocation => file.Path?.ToString() ?? file.Name;

        public async Task<RadioProjectData?> LoadAsync()
        {
            await using var stream = await file.OpenReadAsync();
            var project = await JsonSerializer.DeserializeAsync(
                stream,
                RadioProjectJsonContext.Default.RadioProjectData);

            if (project is not null)
            {
                JsonRadioDataStore.DecryptKeysAfterLoad(project);
            }

            return project;
        }

        public async Task SaveAsync(RadioProjectData project)
        {
            var toSave = JsonRadioDataStore.BuildEncryptedCloneForSave(project);
            await using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(
                stream,
                toSave,
                RadioProjectJsonContext.Default.RadioProjectData);
        }
    }

    private sealed class LocalProjectSettingsStore
    {
        private readonly string _settingsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AnyToneCPS",
            "settings.json");

        public async Task<ProjectStorageSettings?> LoadAsync()
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync(
                stream,
                RadioProjectJsonContext.Default.ProjectStorageSettings);
        }

        public async Task SaveAsync(ProjectStorageSettings settings)
        {
            var directory = System.IO.Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_settingsPath);
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                RadioProjectJsonContext.Default.ProjectStorageSettings);
        }

        public Task ClearAsync()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }

            return Task.CompletedTask;
        }
    }
}
