using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// 5Tone Settings - the global fields live on FiveToneSettings
/// (MainViewModel.CoreEntities.cs); this file holds the 100-row ID table.
/// UI/model only as of 2026-08-05 - no radio address confirmed yet, see
/// FiveToneSettingsEntry's own class doc comment. Split out of
/// MainViewModel.cs to keep that file from growing further, same
/// reasoning as MainViewModel.Qdc1200.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<FiveToneIdEntry> FiveToneIds { get; } = [];

    [ObservableProperty] private FiveToneIdEntry? _selectedFiveToneId;

    public int FiveToneIdCount => FiveToneIds.Count;

    public IReadOnlyList<string> FiveToneDecodeStandardOptions => FiveToneSettingsEntry.DecodeStandardOptions;
    public IReadOnlyList<string> FiveToneDecodingResponseOptions => FiveToneSettingsEntry.DecodingResponseOptions;
    public IReadOnlyList<string> FiveToneFunctionOptionOptions => FiveToneIdEntry.FunctionOptionOptions;
    public IReadOnlyList<string> FiveToneDecodeTimeMsOptions => FiveToneSettingsEntry.DecodeTimeMsOptions;

    /// <summary>Confirmed 2026-08-06: Information ID NO. isn't a
    /// free 1-16 pick - it selects which existing 5Tone ID (row) to view/
    /// set the Information Code Function for, so the real option list is
    /// whatever row numbers are currently in <see cref="FiveToneIds"/>,
    /// not a flat range (the earlier "reverted to 1" live finding was
    /// just "only row 1 exists yet", not a genuine 1-slot cap).</summary>
    public IReadOnlyList<string> FiveToneInfoIdNoOptions =>
        FiveToneIds.Select(e => e.Number).OrderBy(n => n).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();

    /// <summary>Resolves "Information ID NO." to the row it actually
    /// refers to - added 2026-08-06 after finding a real bug live:
    /// Function Option/Function Decoding Response/Information ID/Function
    /// Name used to live on FiveToneSettings as a shared singleton, so
    /// switching Information ID NO. never actually changed what was shown
    /// underneath. Those 4 fields now live on FiveToneIdEntry itself (see
    /// its own class doc comment); this is what the XAML actually binds to
    /// for that section. Null when no row exists yet, or InfoIdNo doesn't
    /// (yet) match any row's own Number - the dropdown only ever offers
    /// existing row numbers (see FiveToneInfoIdNoOptions above), so this
    /// should resolve successfully whenever at least one row exists.</summary>
    public FiveToneIdEntry? SelectedInfoIdRow => FiveToneIds.FirstOrDefault(e => e.Number == FiveToneSettings.InfoIdNo);

    /// <summary>Gates the whole "Information ID / Information Code
    /// Function1" section - disabled when no row exists to resolve
    /// Information ID NO. against (e.g. a brand-new project with no
    /// 5Tone IDs added yet).</summary>
    public bool IsInfoIdRowSelected => SelectedInfoIdRow is not null;

    public IReadOnlyList<string> FiveTonePretimeOptions => FiveToneSettingsEntry.PretimeOptions;
    public IReadOnlyList<string> FiveToneAutoResetTimeOptions => FiveToneSettingsEntry.AutoResetTimeOptions;
    public IReadOnlyList<string> FiveToneTimeLapseAfterEncodeOptions => FiveToneSettingsEntry.TimeLapseAfterEncodeOptions;
    public IReadOnlyList<string> FiveTonePttIdPauseTimeOptions => FiveToneSettingsEntry.PttIdPauseTimeOptions;
    public IReadOnlyList<string> FiveToneFirstToneLengthOptions => FiveToneSettingsEntry.FirstToneLengthOptions;
    public IReadOnlyList<string> FiveToneStopCodeOptions => FiveToneSettingsEntry.StopCodeOptions;
    public IReadOnlyList<string> FiveToneStopTimeLengthOptions => FiveToneSettingsEntry.StopTimeLengthOptions;
    public IReadOnlyList<string> FiveToneFirstToneLengthAfterStopOptions => FiveToneSettingsEntry.FirstToneLengthAfterStopOptions;
    public IReadOnlyList<string> FiveToneTimeOfEncodeToneOptions => FiveToneSettingsEntry.TimeOfEncodeToneOptions;

    /// <summary>The Other Side ID field's real max length in every
    /// Special Call popup - "max 7, but no longer than the current Self
    /// ID" (confirmed 2026-08-05), so it shrinks as Self ID shrinks
    /// rather than always being a flat 7.</summary>
    public int FiveToneOtherSideIdMaxLength => Math.Min(7, FiveToneSettings.SelfId.Length);

    [RelayCommand(CanExecute = nameof(CanOpenFiveToneRowSpecialCall))]
    private async Task OpenFiveToneRowSpecialCall()
    {
        if (SelectedFiveToneId is null)
        {
            return;
        }

        var request = new FiveToneSpecialCallDialogRequest
        {
            ShowGroupNo = true,
            GroupNo = SelectedFiveToneId.Number,
            MaxGroupNo = CodeplugLimits.FiveToneIdMax,
            OtherSideIdMaxLength = FiveToneOtherSideIdMaxLength
        };
        request.Values.CopyFrom(SelectedFiveToneId.SpecialCall);

        if (!await _storagePicker.ShowFiveToneSpecialCallDialogAsync(request))
        {
            return;
        }

        // The popup's own "Choose Encoding Group NO." can retarget a
        // DIFFERENT row than the one that was selected when it was opened
        // (confirmed 2026-08-05) - find that row, creating it if it
        // doesn't exist yet (the real vendor CPS offers all 1-100 as
        // targets regardless of which are already "added" here).
        var target = FiveToneIds.FirstOrDefault(e => e.Number == request.GroupNo);
        if (target is null)
        {
            if (FiveToneIds.Count >= CodeplugLimits.FiveToneIdMax)
            {
                StatusMessage = $"Cannot configure 5Tone ID {request.GroupNo}: the radio only has {CodeplugLimits.FiveToneIdMax} slots.";
                return;
            }

            target = new FiveToneIdEntry { Number = request.GroupNo };
            FiveToneIds.Add(target);
        }

        // EncodeId itself auto-composes via FiveToneIdEntry's own
        // constructor subscription to SpecialCall.PropertyChanged - see
        // that class's own doc comment.
        target.SpecialCall.CopyFrom(request.Values);
        SelectedFiveToneId = target;
        OnPropertyChanged(nameof(FiveToneIdCount));
        RefreshValidation();
    }

    private bool CanOpenFiveToneRowSpecialCall() => SelectedFiveToneId is not null;

    [RelayCommand]
    private async Task OpenFiveToneBotSpecialCall()
    {
        var request = new FiveToneSpecialCallDialogRequest
        {
            ShowGroupNo = false,
            OtherSideIdMaxLength = FiveToneOtherSideIdMaxLength
        };
        request.Values.CopyFrom(FiveToneSettings.BotSpecialCall);

        if (await _storagePicker.ShowFiveToneSpecialCallDialogAsync(request))
        {
            // EncodeId itself auto-composes (for ANI/PTTID only - Send
            // Message isn't confirmed yet) via FiveToneSettingsEntry's own
            // constructor subscription - see that class's own doc comment.
            FiveToneSettings.BotSpecialCall.CopyFrom(request.Values);
        }
    }

    [RelayCommand]
    private async Task OpenFiveToneEotSpecialCall()
    {
        var request = new FiveToneSpecialCallDialogRequest
        {
            ShowGroupNo = false,
            OtherSideIdMaxLength = FiveToneOtherSideIdMaxLength
        };
        request.Values.CopyFrom(FiveToneSettings.EotSpecialCall);

        if (await _storagePicker.ShowFiveToneSpecialCallDialogAsync(request))
        {
            FiveToneSettings.EotSpecialCall.CopyFrom(request.Values);
        }
    }

    /// <summary>Double-clicking a row that's already been set by
    /// &amp;Special Call resets it back to "never configured" after a
    /// confirmation, matching the real vendor CPS gesture (2026-08-05):
    /// "Reset special call of this channel, ok or no?" - not the most
    /// discoverable interaction, but it's how the vendor CPS itself works.
    /// A never-configured row is a no-op
    /// (nothing to reset).</summary>
    [RelayCommand]
    private async Task ResetFiveToneRowSpecialCall(FiveToneIdEntry? entry)
    {
        if (entry is null || !entry.SpecialCall.IsConfigured)
        {
            return;
        }

        if (!await _storagePicker.ConfirmResetFiveToneSpecialCallAsync())
        {
            return;
        }

        entry.SpecialCall.Reset();
        entry.EncodeId = "";
        RefreshValidation();
    }

    /// <summary>BOT/EOT's own Encode ID box now goes read-only once
    /// &amp;Special Call is used (matches the real vendor CPS - confirmed
    /// 2026-08-06 to apply beyond just the row-level table), so they need
    /// their own way back to editable, same reasoning as
    /// <see cref="ResetFiveToneRowSpecialCall"/>. Unlike the row-level
    /// gesture, there's no double-click target here (BOT/EOT aren't list
    /// rows) - button only.</summary>
    [RelayCommand]
    private async Task ResetFiveToneBotSpecialCall()
    {
        if (!FiveToneSettings.BotSpecialCall.IsConfigured)
        {
            return;
        }

        if (!await _storagePicker.ConfirmResetFiveToneSpecialCallAsync())
        {
            return;
        }

        FiveToneSettings.BotSpecialCall.Reset();
        FiveToneSettings.BotEncodeId = "";
    }

    /// <summary>Same as <see cref="ResetFiveToneBotSpecialCall"/>, for EOT.</summary>
    [RelayCommand]
    private async Task ResetFiveToneEotSpecialCall()
    {
        if (!FiveToneSettings.EotSpecialCall.IsConfigured)
        {
            return;
        }

        if (!await _storagePicker.ConfirmResetFiveToneSpecialCallAsync())
        {
            return;
        }

        FiveToneSettings.EotSpecialCall.Reset();
        FiveToneSettings.EotEncodeId = "";
    }

    [RelayCommand]
    private void AddFiveToneId()
    {
        if (FiveToneIds.Count >= CodeplugLimits.FiveToneIdMax)
        {
            StatusMessage = $"Cannot add 5Tone ID: the radio only has {CodeplugLimits.FiveToneIdMax} slots.";
            return;
        }

        var number = NextNumber(FiveToneIds.Select(e => e.Number));
        var entry = new FiveToneIdEntry { Number = number };
        FiveToneIds.Add(entry);
        SelectedFiveToneId = entry;
        OnPropertyChanged(nameof(FiveToneIdCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFiveToneId))]
    private void RemoveFiveToneId()
    {
        if (SelectedFiveToneId is null)
        {
            return;
        }

        // See _pendingDeleteFiveToneIdIndices' own doc comment - without
        // this, a delete never actually reaches the radio (same gap every
        // other entity's deletion had before each was fixed).
        var removedIndex = SelectedFiveToneId.Number - 1;
        FiveToneIds.Remove(SelectedFiveToneId);
        _pendingDeleteFiveToneIdIndices.Add(removedIndex);
        SelectedFiveToneId = FiveToneIds.FirstOrDefault();
        OnPropertyChanged(nameof(FiveToneIdCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedFiveToneId() => SelectedFiveToneId is not null;

    partial void OnSelectedFiveToneIdChanged(FiveToneIdEntry? value)
    {
        RemoveFiveToneIdCommand.NotifyCanExecuteChanged();
        OpenFiveToneRowSpecialCallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Wired from the main constructor in MainViewModel.cs - marks
    /// the project dirty on any edit here, same reasoning as
    /// WireQdc1200Notifications. No radio-write dirty tracking yet (no
    /// write path exists), so no pending-delete set either - see
    /// FiveToneSettingsEntry's class doc comment. FiveToneSettings' own
    /// flat properties are already covered by
    /// WireCoreEntityOptionNotifications's blanket
    /// "FiveToneSettings.PropertyChanged += MarkDirtyAndRevalidate" - the
    /// 2 nested SpecialCall sub-objects need their own separate
    /// subscription here, since a nested object's own property changes
    /// don't bubble up through the parent's PropertyChanged event.</summary>
    private void WireFiveToneNotifications()
    {
        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        FiveToneIds.CollectionChanged += MarkDirtyAndRevalidate;
        FiveToneIds.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FiveToneInfoIdNoOptions));
            OnPropertyChanged(nameof(SelectedInfoIdRow));
            OnPropertyChanged(nameof(IsInfoIdRowSelected));
        };
        FiveToneSettings.BotSpecialCall.PropertyChanged += MarkDirtyAndRevalidate;
        FiveToneSettings.EotSpecialCall.PropertyChanged += MarkDirtyAndRevalidate;

        FiveToneSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FiveToneSettingsEntry.SelfId))
            {
                OnPropertyChanged(nameof(FiveToneOtherSideIdMaxLength));
            }

            if (e.PropertyName == nameof(FiveToneSettingsEntry.InfoIdNo))
            {
                OnPropertyChanged(nameof(SelectedInfoIdRow));
                OnPropertyChanged(nameof(IsInfoIdRowSelected));
            }
        };
    }
}
