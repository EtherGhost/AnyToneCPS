using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnyToneCPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// QDC 1200 Setting - Decode/Encode tabs' global fields live on
/// Qdc1200Settings (MainViewModel.CoreEntities.cs); this file holds the
/// Encode tab's 100-row ID table. Full radio-write support as of
/// 2026-08-04 - see Qdc1200SettingsEntry's class doc comment for the byte-
/// layout confirmation. Split out of MainViewModel.cs to keep that file
/// from growing further, same reasoning as MainViewModel.HotKey.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<Qdc1200IdEntry> Qdc1200Ids { get; } = [];

    [ObservableProperty] private Qdc1200IdEntry? _selectedQdc1200Id;

    public int Qdc1200IdCount => Qdc1200Ids.Count;

    // Static option lists re-exposed here (rather than only on the model
    // types) so Desktop's detail panel - whose DataContext is MainViewModel,
    // not the selected row - can bind a ComboBox directly. Same reasoning as
    // MainViewModel.ContactCallTypes mirroring TalkgroupEntry.CallTypeOptions.
    public IReadOnlyList<string> Qdc1200AutoResetTimeOptions => Qdc1200SettingsEntry.AutoResetTimeOptions;
    public IReadOnlyList<string> Qdc1200RemoteListeningDurationOptions => Qdc1200SettingsEntry.RemoteListeningDurationOptions;
    public IReadOnlyList<string> Qdc1200MaxAckWaitTimeOptions => Qdc1200SettingsEntry.MaxAckWaitTimeOptions;
    public IReadOnlyList<string> Qdc1200PretimeOptions => Qdc1200SettingsEntry.PretimeOptions;
    public IReadOnlyList<string> Qdc1200ResendCodeOptions => Qdc1200SettingsEntry.ResendCodeOptions;
    public IReadOnlyList<string> Qdc1200IdCallTypeOptions => Qdc1200IdEntry.CallTypeOptions;

    [RelayCommand]
    private void AddQdc1200Id()
    {
        if (Qdc1200Ids.Count >= CodeplugLimits.Qdc1200IdMax)
        {
            StatusMessage = $"Cannot add QDC 1200 ID: the radio only has {CodeplugLimits.Qdc1200IdMax} slots.";
            return;
        }

        var number = NextNumber(Qdc1200Ids.Select(e => e.Number));
        var entry = new Qdc1200IdEntry { Number = number };
        Qdc1200Ids.Add(entry);
        SelectedQdc1200Id = entry;
        OnPropertyChanged(nameof(Qdc1200IdCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedQdc1200Id))]
    private void RemoveQdc1200Id()
    {
        if (SelectedQdc1200Id is null)
        {
            return;
        }

        var removedIndex = SelectedQdc1200Id.Number - 1;
        Qdc1200Ids.Remove(SelectedQdc1200Id);
        // See _pendingDeleteQdc1200IdIndices' doc comment - without this, a
        // delete never actually reaches the radio (same gap every other
        // entity's deletion had before each was fixed).
        _pendingDeleteQdc1200IdIndices.Add(removedIndex);
        SelectedQdc1200Id = Qdc1200Ids.FirstOrDefault();
        OnPropertyChanged(nameof(Qdc1200IdCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedQdc1200Id() => SelectedQdc1200Id is not null;

    partial void OnSelectedQdc1200IdChanged(Qdc1200IdEntry? value) => RemoveQdc1200IdCommand.NotifyCanExecuteChanged();

    /// <summary>Wired from the main constructor in MainViewModel.cs -
    /// dirty-tracking only (no cross-collection dependent lists here,
    /// unlike Hot Key's Call Object/Content).</summary>
    private void WireQdc1200Notifications()
    {
        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        Qdc1200Ids.CollectionChanged += MarkDirtyAndRevalidate;
    }
}
