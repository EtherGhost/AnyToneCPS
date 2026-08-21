using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnyToneCPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// 2Tone Settings - Encode tab's global fields live on TwoToneEncodeSettings
/// (MainViewModel.CoreEntities.cs); this file holds the Encode tab's 24-row
/// frequency table and the Decode tab's 16-row table (which has no scalar
/// fields of its own). Full radio-write support added 2026-08-06, confirmed
/// via 2 live differential WRITE captures - see TwoToneEncodeCodec's own
/// doc comment. Split out of MainViewModel.cs to keep that file from
/// growing further, same reasoning as MainViewModel.Qdc1200.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<TwoToneEncodeEntry> TwoToneEncodeEntries { get; } = [];
    public ObservableCollection<TwoToneDecodeEntry> TwoToneDecodeEntries { get; } = [];

    [ObservableProperty] private TwoToneEncodeEntry? _selectedTwoToneEncodeEntry;
    [ObservableProperty] private TwoToneDecodeEntry? _selectedTwoToneDecodeEntry;

    public int TwoToneEncodeCount => TwoToneEncodeEntries.Count;
    public int TwoToneDecodeCount => TwoToneDecodeEntries.Count;

    public IReadOnlyList<string> TwoToneDecodingResponseOptions => TwoToneDecodeEntry.DecodingResponseOptions;

    [RelayCommand]
    private void AddTwoToneEncodeEntry()
    {
        if (TwoToneEncodeEntries.Count >= CodeplugLimits.TwoToneEncodeMax)
        {
            StatusMessage = $"Cannot add 2Tone Encode entry: the radio only has {CodeplugLimits.TwoToneEncodeMax} slots.";
            return;
        }

        var number = NextNumber(TwoToneEncodeEntries.Select(e => e.Number));
        var entry = new TwoToneEncodeEntry { Number = number };
        TwoToneEncodeEntries.Add(entry);
        SelectedTwoToneEncodeEntry = entry;
        OnPropertyChanged(nameof(TwoToneEncodeCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedTwoToneEncodeEntry))]
    private void RemoveTwoToneEncodeEntry()
    {
        if (SelectedTwoToneEncodeEntry is null)
        {
            return;
        }

        var removedIndex = SelectedTwoToneEncodeEntry.Number - 1;
        TwoToneEncodeEntries.Remove(SelectedTwoToneEncodeEntry);
        _pendingDeleteTwoToneEncodeIndices.Add(removedIndex);
        SelectedTwoToneEncodeEntry = TwoToneEncodeEntries.FirstOrDefault();
        OnPropertyChanged(nameof(TwoToneEncodeCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedTwoToneEncodeEntry() => SelectedTwoToneEncodeEntry is not null;

    partial void OnSelectedTwoToneEncodeEntryChanged(TwoToneEncodeEntry? value) => RemoveTwoToneEncodeEntryCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void AddTwoToneDecodeEntry()
    {
        if (TwoToneDecodeEntries.Count >= CodeplugLimits.TwoToneDecodeMax)
        {
            StatusMessage = $"Cannot add 2Tone Decode entry: the radio only has {CodeplugLimits.TwoToneDecodeMax} slots.";
            return;
        }

        var number = NextNumber(TwoToneDecodeEntries.Select(e => e.Number));
        var entry = new TwoToneDecodeEntry { Number = number };
        TwoToneDecodeEntries.Add(entry);
        SelectedTwoToneDecodeEntry = entry;
        OnPropertyChanged(nameof(TwoToneDecodeCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedTwoToneDecodeEntry))]
    private void RemoveTwoToneDecodeEntry()
    {
        if (SelectedTwoToneDecodeEntry is null)
        {
            return;
        }

        var removedIndex = SelectedTwoToneDecodeEntry.Number - 1;
        TwoToneDecodeEntries.Remove(SelectedTwoToneDecodeEntry);
        _pendingDeleteTwoToneDecodeIndices.Add(removedIndex);
        SelectedTwoToneDecodeEntry = TwoToneDecodeEntries.FirstOrDefault();
        OnPropertyChanged(nameof(TwoToneDecodeCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedTwoToneDecodeEntry() => SelectedTwoToneDecodeEntry is not null;

    partial void OnSelectedTwoToneDecodeEntryChanged(TwoToneDecodeEntry? value) => RemoveTwoToneDecodeEntryCommand.NotifyCanExecuteChanged();

    /// <summary>Wired from the main constructor in MainViewModel.cs -
    /// dirty-tracking only (no cross-collection dependent lists here).</summary>
    private void WireTwoToneNotifications()
    {
        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        TwoToneEncodeEntries.CollectionChanged += MarkDirtyAndRevalidate;
        TwoToneDecodeEntries.CollectionChanged += MarkDirtyAndRevalidate;
    }
}
