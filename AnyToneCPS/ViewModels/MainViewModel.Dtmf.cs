using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// DTMF Settings - global fields live on DtmfSettings
/// (MainViewModel.CoreEntities.cs); this file holds the fixed 16-slot
/// M1-M16 list (see CodeplugLimits.DtmfEncodeSlotCount, same "fixed named
/// set" convention as HotKeys) and the per-slot "&amp;Special Call" popup.
/// UI/model only - no radio codec/write path yet. Split out of
/// MainViewModel.cs, same reasoning as MainViewModel.HotKey.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<DtmfEncodeEntry> DtmfEncodeEntries { get; } = [];

    [ObservableProperty] private DtmfEncodeEntry? _selectedDtmfEncodeEntry;

    // Static option lists re-exposed here (rather than only on the model
    // type) so Desktop's detail panel - whose DataContext is MainViewModel,
    // not the selected row - can bind a ComboBox directly. Same reasoning
    // as MainViewModel.Qdc1200IdCallTypeOptions mirroring Qdc1200IdEntry's
    // own option lists.
    public IReadOnlyList<string> DtmfIntervalCharacterOptions => DtmfSettingsEntry.IntervalCharacterOptions;
    public IReadOnlyList<string> DtmfGroupCodeOptions => DtmfSettingsEntry.GroupCodeOptions;
    public IReadOnlyList<string> DtmfDecodingResponseOptions => DtmfSettingsEntry.DecodingResponseOptions;

    /// <summary>Ensures DtmfEncodeEntries always has exactly
    /// CodeplugLimits.DtmfEncodeSlotCount rows (M1-M16, in order) - same
    /// idempotent seeding pattern as EnsureHotKeySlotsPresent.</summary>
    private void EnsureDtmfEncodeSlotsPresent()
    {
        if (DtmfEncodeEntries.Count == CodeplugLimits.DtmfEncodeSlotCount)
        {
            return;
        }

        DtmfEncodeEntries.Clear();
        for (var number = 1; number <= CodeplugLimits.DtmfEncodeSlotCount; number++)
        {
            var entry = new DtmfEncodeEntry { Number = number };
            // Mobile has no popup concept (dialogs are Desktop-only, same
            // as every other dialog in AvaloniaStoragePickerService) - it
            // edits Other Side ID inline instead, same "no redirect
            // needed" pattern 5Tone's own Mobile rows already use. Any
            // edit to Other Side ID (Desktop popup OR Mobile inline) is
            // itself the configuration action.
            entry.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DtmfEncodeEntry.OtherSideId) && sender is DtmfEncodeEntry changed)
                {
                    changed.IsSpecialCallConfigured = changed.IsSpecialCallConfigured || !string.IsNullOrEmpty(changed.OtherSideId);
                    ComposeDtmfCode(changed);
                }
            };
            DtmfEncodeEntries.Add(entry);
        }
    }

    /// <summary>Takes the target row directly as a parameter, same pattern
    /// as ResetFiveToneRowSpecialCallCommand - the DTMF Encode list's own
    /// per-row "Special Call" button can't bind a MainViewModel command
    /// from inside its own DataTemplate (no ambient access to the
    /// MainViewModel DataContext there), so MainView.axaml.cs's own
    /// DtmfSpecialCallButton_OnClick reads the button's DataContext (the
    /// row) in code-behind and calls Execute(entry) directly. Also avoids
    /// depending on ListBox SelectedItem at all, which turned out
    /// unreliable here - clicking into the row's own large inline Code
    /// TextBox to edit it doesn't reliably select the ListBoxItem first.</summary>
    [RelayCommand]
    private async Task OpenDtmfSpecialCall(DtmfEncodeEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var request = new DtmfSpecialCallDialogRequest { OtherSideId = entry.OtherSideId };
        if (!await _storagePicker.ShowDtmfSpecialCallDialogAsync(request))
        {
            return;
        }

        // Setting OtherSideId fires the per-entry PropertyChanged
        // subscription wired in EnsureDtmfEncodeSlotsPresent, which sets
        // IsSpecialCallConfigured and recomposes Code.
        entry.OtherSideId = request.OtherSideId;
        RefreshValidation();
    }

    /// <summary>Code = {OtherSideId}{DTMF Interval Character}{DTMF Self
    /// ID} once configured - confirmed via 5 worked examples against the
    /// real vendor CPS (e.g. Self ID "002" + Other Side ID "111" -&gt; "111*002").
    /// Needs DtmfSettings' own Self ID/Interval Character, so this lives
    /// here rather than on DtmfEncodeEntry itself - see that class's own
    /// doc comment.</summary>
    private void ComposeDtmfCode(DtmfEncodeEntry entry)
    {
        if (!entry.IsSpecialCallConfigured)
        {
            return;
        }

        entry.Code = $"{entry.OtherSideId}{DtmfSettings.IntervalCharacter}{DtmfSettings.SelfId}";
    }

    /// <summary>Wired from the main constructor in MainViewModel.cs -
    /// dirty-tracking, plus keeping every already-configured slot's Code in
    /// sync when DTMF Settings' own Self ID/Interval Character change
    /// (both are shared inputs to every configured slot's composition, not
    /// self-contained per-row like 5Tone's own formula).</summary>
    private void WireDtmfNotifications()
    {
        DtmfEncodeEntries.CollectionChanged += (_, _) =>
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        };

        DtmfSettings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(DtmfSettingsEntry.SelfId) or nameof(DtmfSettingsEntry.IntervalCharacter))
            {
                foreach (var entry in DtmfEncodeEntries)
                {
                    ComposeDtmfCode(entry);
                }
            }
        };
    }
}
