using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnyToneCPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// QDC Address Book - Add/Remove-with-cap list (No. 1-128, see
/// CodeplugLimits.QdcAddressMax), same pattern as Analog Address Book.
/// Full radio-write support as of 2026-08-04 - see QdcAddressEntry's own
/// class doc comment for the byte-layout confirmation. Split out of
/// MainViewModel.cs to keep that file from growing further, same
/// reasoning as MainViewModel.Qdc1200.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<QdcAddressEntry> QdcAddresses { get; } = [];

    [ObservableProperty] private QdcAddressEntry? _selectedQdcAddress;

    public int QdcAddressCount => QdcAddresses.Count;

    public IReadOnlyList<string> QdcAddressCallTypeOptions => QdcAddressEntry.CallTypeOptions;

    [RelayCommand]
    private void AddQdcAddress()
    {
        if (QdcAddresses.Count >= CodeplugLimits.QdcAddressMax)
        {
            StatusMessage = $"Cannot add QDC Address: the radio only has {CodeplugLimits.QdcAddressMax} slots.";
            return;
        }

        var number = NextNumber(QdcAddresses.Select(e => e.Number));
        var entry = new QdcAddressEntry { Number = number };
        QdcAddresses.Add(entry);
        SelectedQdcAddress = entry;
        OnPropertyChanged(nameof(QdcAddressCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedQdcAddress))]
    private void RemoveQdcAddress()
    {
        if (SelectedQdcAddress is null)
        {
            return;
        }

        var removedIndex = SelectedQdcAddress.Number - 1;
        QdcAddresses.Remove(SelectedQdcAddress);
        // See _pendingDeleteQdcAddressIndices' doc comment - without this,
        // a delete never actually reaches the radio (same gap every other
        // entity's deletion had before each was fixed).
        _pendingDeleteQdcAddressIndices.Add(removedIndex);
        SelectedQdcAddress = QdcAddresses.FirstOrDefault();
        OnPropertyChanged(nameof(QdcAddressCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedQdcAddress() => SelectedQdcAddress is not null;

    partial void OnSelectedQdcAddressChanged(QdcAddressEntry? value) => RemoveQdcAddressCommand.NotifyCanExecuteChanged();

    /// <summary>Wired from the main constructor in MainViewModel.cs - marks
    /// the project dirty on any edit here, same reasoning as
    /// WireQdc1200Notifications.</summary>
    private void WireQdcAddressBookNotifications()
    {
        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        QdcAddresses.CollectionChanged += MarkDirtyAndRevalidate;
    }
}
