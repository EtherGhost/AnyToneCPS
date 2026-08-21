using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnyToneCPS.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnyToneCPS.ViewModels;

/// <summary>
/// Hot Key - three tabs (Analog Quick Call, State Information, Hot Key).
/// Full radio-write support as of 2026-08-04 (see the codecs in
/// Services/Radio/Codecs and HotKeyEntry's own class doc comment for the
/// byte-layout confirmation). Split out of MainViewModel.cs to keep that
/// file from growing further, same reasoning as
/// MainViewModel.CoreEntities.cs.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<AnalogQuickCallEntry> AnalogQuickCalls { get; } = [];
    public ObservableCollection<StateInformationEntry> StateInformationEntries { get; } = [];

    /// <summary>Always exactly CodeplugLimits.HotKeyKeyCount rows, one per
    /// physical/programmable key - never added/removed by the user, so this
    /// is seeded once (see <see cref="EnsureHotKeySlotsPresent"/>) rather
    /// than following the Add/Remove-with-cap pattern the two lists above
    /// use.</summary>
    public ObservableCollection<HotKeyEntry> HotKeys { get; } = [];

    [ObservableProperty] private AnalogQuickCallEntry? _selectedAnalogQuickCall;
    [ObservableProperty] private StateInformationEntry? _selectedStateInformation;
    [ObservableProperty] private HotKeyEntry? _selectedHotKey;

    public int AnalogQuickCallCount => AnalogQuickCalls.Count;
    public int StateInformationCount => StateInformationEntries.Count;

    // Static option lists re-exposed here (rather than only on the model
    // types) so Desktop's detail panel - whose DataContext is MainViewModel,
    // not the selected row - can bind a ComboBox directly. Same reasoning as
    // MainViewModel.ContactCallTypes mirroring TalkgroupEntry.CallTypeOptions.
    public IReadOnlyList<string> AnalogQuickCallOperationTypeOptions => AnalogQuickCallEntry.OperationTypeOptions;
    public IReadOnlyList<string> HotKeyModeOptions => HotKeyEntry.ModeOptions;
    public IReadOnlyList<string> HotKeyMenuOptions => HotKeyEntry.MenuOptions;
    public IReadOnlyList<string> HotKeyCallTypeOptions => HotKeyEntry.CallTypeOptions;
    public IReadOnlyList<string> HotKeyDigiCallTypeOptions => HotKeyEntry.DigiCallTypeOptions;

    /// <summary>Wired from the main constructor in MainViewModel.cs, same
    /// reasoning as WireCoreEntityOptionNotifications - keeps the dependent
    /// Call ID/Call Object/Content option lists fresh as the entries that
    /// feed them change, and marks the project dirty on any edit here.</summary>
    private void WireHotKeyNotifications()
    {
        void MarkDirtyAndRevalidate(object? sender, object args)
        {
            _projectStructureDirty = true;
            NotifyDirtyStateChanged();
            RefreshValidation();
        }

        AnalogQuickCalls.CollectionChanged += MarkDirtyAndRevalidate;
        StateInformationEntries.CollectionChanged += MarkDirtyAndRevalidate;
        HotKeys.CollectionChanged += MarkDirtyAndRevalidate;

        // Analog Quick Call/Talkgroups/Prefabricated SMS all feed Hot Key's
        // Call Object/Content dependent lists (see HotKeyCallObjectOptions/
        // HotKeyContentOptions above) - refresh those whenever the source
        // collections change, not just when SelectedHotKey itself changes.
        AnalogQuickCalls.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HotKeyCallObjectOptions));
            OnPropertyChanged(nameof(HotKeyCallObjectSelection));
        };
        Talkgroups.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HotKeyCallObjectOptions));
            OnPropertyChanged(nameof(HotKeyCallObjectSelection));
        };
        PrefabricatedSmsMessages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HotKeyContentOptions));
            OnPropertyChanged(nameof(HotKeyContentSelection));
        };
        StateInformationEntries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HotKeyContentOptions));
            OnPropertyChanged(nameof(HotKeyContentSelection));
        };

        AnalogQuickCalls.CollectionChanged += (_, args) => AttachOrDetachAnalogQuickCallHandlers(args);
        HotKeys.CollectionChanged += (_, args) => AttachOrDetachHotKeyHandlers(args);
    }

    private void AttachOrDetachAnalogQuickCallHandlers(System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
        {
            foreach (AnalogQuickCallEntry entry in args.OldItems)
            {
                entry.PropertyChanged -= OnAnalogQuickCallEntryPropertyChanged;
            }
        }

        if (args.NewItems is not null)
        {
            foreach (AnalogQuickCallEntry entry in args.NewItems)
            {
                entry.PropertyChanged += OnAnalogQuickCallEntryPropertyChanged;
            }
        }
    }

    private void OnAnalogQuickCallEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != SelectedAnalogQuickCall || e.PropertyName != nameof(AnalogQuickCallEntry.OperationType))
        {
            return;
        }

        OnPropertyChanged(nameof(AnalogQuickCallCallIdOptions));
        OnPropertyChanged(nameof(AnalogQuickCallCallIdSelection));
    }

    private void AttachOrDetachHotKeyHandlers(System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
        {
            foreach (HotKeyEntry entry in args.OldItems)
            {
                entry.PropertyChanged -= OnHotKeyEntryPropertyChanged;
            }
        }

        if (args.NewItems is not null)
        {
            foreach (HotKeyEntry entry in args.NewItems)
            {
                entry.PropertyChanged += OnHotKeyEntryPropertyChanged;
            }
        }
    }

    private void OnHotKeyEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != SelectedHotKey)
        {
            return;
        }

        if (e.PropertyName == nameof(HotKeyEntry.CallType))
        {
            OnPropertyChanged(nameof(HotKeyCallObjectOptions));
            OnPropertyChanged(nameof(HotKeyCallObjectSelection));
        }

        if (e.PropertyName == nameof(HotKeyEntry.DigiCallType))
        {
            OnPropertyChanged(nameof(HotKeyContentOptions));
            OnPropertyChanged(nameof(HotKeyContentSelection));
        }
    }

    /// <summary>Ensures <see cref="HotKeys"/> always has exactly
    /// CodeplugLimits.HotKeyKeyCount rows, one per HotKeyEntry.KeyNames
    /// entry, in order. Idempotent - safe to call more than once (matches
    /// EnsureEncryptionKeySlotsPresent's own reasoning), though unlike that
    /// method there's no radio-sync/dirty-tracking angle here yet, just
    /// keeping the fixed row set intact.</summary>
    private void EnsureHotKeySlotsPresent()
    {
        if (HotKeys.Count == CodeplugLimits.HotKeyKeyCount)
        {
            return;
        }

        HotKeys.Clear();
        foreach (var name in HotKeyEntry.KeyNames)
        {
            HotKeys.Add(new HotKeyEntry { Key = name });
        }
    }

    // --- Analog Quick Call: Call ID depends on Operation Type - 2Tone/
    // 5Tone/QDC1200 reuse the same fixed 16-item ID lists other analog
    // signalling fields already use (Tone2Ids/Tone5Ids/QdcIds - see their
    // own doc comments for the "always shows all 16" known limitation,
    // task #55). Off/DTMF always resolve to the single-item "Off" list -
    // confirmed directly against the real vendor CPS.
    public IReadOnlyList<string> AnalogQuickCallCallIdOptions
    {
        get
        {
            if (SelectedAnalogQuickCall is null)
            {
                return [];
            }

            return SelectedAnalogQuickCall.OperationType switch
            {
                2 => Tone2Ids,
                3 => Tone5Ids,
                4 => QdcIds,
                _ => ["Off"]
            };
        }
    }

    public string AnalogQuickCallCallIdSelection
    {
        get
        {
            if (SelectedAnalogQuickCall is null)
            {
                return "";
            }

            var options = AnalogQuickCallCallIdOptions;
            var index = SelectedAnalogQuickCall.CallId;
            return index >= 0 && index < options.Count ? options[index] : "Off";
        }
        set
        {
            if (SelectedAnalogQuickCall is null)
            {
                return;
            }

            var index = AnalogQuickCallCallIdOptions.ToList().IndexOf(value);
            SelectedAnalogQuickCall.CallId = index;
        }
    }

    // --- Hot Key: Call Object depends on Call Type - Analog references an
    // Analog Quick Call slot that's actually configured (OperationType !=
    // Off, matching the reference project's own "filled only if possible"
    // filter), Digital references a Talkgroup/Contact.
    public IReadOnlyList<string> HotKeyCallObjectOptions
    {
        get
        {
            if (SelectedHotKey is null)
            {
                return [];
            }

            return SelectedHotKey.CallType switch
            {
                1 => new[] { "Off" }.Concat(AnalogQuickCalls.Where(q => q.OperationType != 0).Select(q => q.Number.ToString())).ToList(),
                2 => new[] { "Off" }.Concat(Talkgroups.Select(t => t.Name)).ToList(),
                _ => []
            };
        }
    }

    public string HotKeyCallObjectSelection
    {
        get
        {
            if (SelectedHotKey is null || SelectedHotKey.CallObject < 0)
            {
                return "Off";
            }

            return SelectedHotKey.CallType switch
            {
                1 => AnalogQuickCalls.FirstOrDefault(q => q.Number == SelectedHotKey.CallObject)?.Number.ToString() ?? "Off",
                2 => Talkgroups.FirstOrDefault(t => t.Number == SelectedHotKey.CallObject)?.Name ?? "Off",
                _ => "Off"
            };
        }
        set
        {
            if (SelectedHotKey is null)
            {
                return;
            }

            if (value == "Off")
            {
                SelectedHotKey.CallObject = -1;
                return;
            }

            SelectedHotKey.CallObject = SelectedHotKey.CallType switch
            {
                1 => AnalogQuickCalls.FirstOrDefault(q => q.Number.ToString() == value)?.Number ?? -1,
                2 => Talkgroups.FirstOrDefault(t => t.Name == value)?.Number ?? -1,
                _ => -1
            };
        }
    }

    // --- Hot Key: Content depends on Digi Call Type - a real reference
    // into Prefabricated SMS for DMR Hot Text, or into State Information
    // itself for DMR State Information (confirmed 2026-08-04 via a live
    // differential WRITE capture - the real vendor CPS dropdown only
    // ever showed "Off" and the entries State Information actually has
    // configured, and picking one round-tripped back to that same State
    // Information text ("1" resolved to "Status Message 1") - this codec's
    // first draft wrongly guessed a literal "1"/"16" pair before that was
    // confirmed).
    public IReadOnlyList<string> HotKeyContentOptions
    {
        get
        {
            if (SelectedHotKey is null)
            {
                return [];
            }

            return SelectedHotKey.DigiCallType switch
            {
                1 => new[] { "Off" }.Concat(PrefabricatedSmsMessages.Where(s => !string.IsNullOrEmpty(s.Text)).Select(s => s.Number.ToString())).ToList(),
                3 => new[] { "Off" }.Concat(StateInformationEntries.Select(s => s.Number.ToString())).ToList(),
                _ => []
            };
        }
    }

    public string HotKeyContentSelection
    {
        get
        {
            if (SelectedHotKey is null || SelectedHotKey.Content < 0)
            {
                return "Off";
            }

            return SelectedHotKey.DigiCallType switch
            {
                1 => PrefabricatedSmsMessages.FirstOrDefault(s => s.Number == SelectedHotKey.Content)?.Number.ToString() ?? "Off",
                3 => StateInformationEntries.FirstOrDefault(s => s.Number == SelectedHotKey.Content)?.Number.ToString() ?? "Off",
                _ => "Off"
            };
        }
        set
        {
            if (SelectedHotKey is null)
            {
                return;
            }

            if (value == "Off")
            {
                SelectedHotKey.Content = -1;
                return;
            }

            SelectedHotKey.Content = SelectedHotKey.DigiCallType switch
            {
                1 => PrefabricatedSmsMessages.FirstOrDefault(s => s.Number.ToString() == value)?.Number ?? -1,
                3 => StateInformationEntries.FirstOrDefault(s => s.Number.ToString() == value)?.Number ?? -1,
                _ => -1
            };
        }
    }

    [RelayCommand]
    private void AddAnalogQuickCall()
    {
        if (AnalogQuickCalls.Count >= CodeplugLimits.AnalogQuickCallMax)
        {
            StatusMessage = $"Cannot add Analog Quick Call: the radio only has {CodeplugLimits.AnalogQuickCallMax} slots.";
            return;
        }

        var number = NextNumber(AnalogQuickCalls.Select(e => e.Number));
        var entry = new AnalogQuickCallEntry { Number = number };
        AnalogQuickCalls.Add(entry);
        SelectedAnalogQuickCall = entry;
        OnPropertyChanged(nameof(AnalogQuickCallCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedAnalogQuickCall))]
    private void RemoveAnalogQuickCall()
    {
        if (SelectedAnalogQuickCall is null)
        {
            return;
        }

        var removedIndex = SelectedAnalogQuickCall.Number - 1;
        AnalogQuickCalls.Remove(SelectedAnalogQuickCall);
        // See _pendingDeleteAnalogQuickCallIndices' doc comment - without
        // this, a delete never actually reaches the radio (same gap every
        // other entity's deletion had before each was fixed).
        _pendingDeleteAnalogQuickCallIndices.Add(removedIndex);
        SelectedAnalogQuickCall = AnalogQuickCalls.FirstOrDefault();
        OnPropertyChanged(nameof(AnalogQuickCallCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedAnalogQuickCall() => SelectedAnalogQuickCall is not null;

    [RelayCommand]
    private void AddStateInformation()
    {
        if (StateInformationEntries.Count >= CodeplugLimits.StateInformationMax)
        {
            StatusMessage = $"Cannot add State Information: the radio only has {CodeplugLimits.StateInformationMax} slots.";
            return;
        }

        var number = NextNumber(StateInformationEntries.Select(e => e.Number));
        var entry = new StateInformationEntry { Number = number };
        StateInformationEntries.Add(entry);
        SelectedStateInformation = entry;
        OnPropertyChanged(nameof(StateInformationCount));
        RefreshValidation();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedStateInformation))]
    private void RemoveStateInformation()
    {
        if (SelectedStateInformation is null)
        {
            return;
        }

        var removedIndex = SelectedStateInformation.Number - 1;
        StateInformationEntries.Remove(SelectedStateInformation);
        // See _pendingDeleteStateInformationIndices' doc comment - without
        // this, a delete never actually reaches the radio (same gap every
        // other entity's deletion had before each was fixed).
        _pendingDeleteStateInformationIndices.Add(removedIndex);
        SelectedStateInformation = StateInformationEntries.FirstOrDefault();
        OnPropertyChanged(nameof(StateInformationCount));
        RefreshValidation();
        NotifyDirtyStateChanged();
        WriteChangesToRadioCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedStateInformation() => SelectedStateInformation is not null;

    partial void OnSelectedAnalogQuickCallChanged(AnalogQuickCallEntry? value)
    {
        RemoveAnalogQuickCallCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(AnalogQuickCallCallIdOptions));
        OnPropertyChanged(nameof(AnalogQuickCallCallIdSelection));
    }

    partial void OnSelectedStateInformationChanged(StateInformationEntry? value) => RemoveStateInformationCommand.NotifyCanExecuteChanged();

    partial void OnSelectedHotKeyChanged(HotKeyEntry? value)
    {
        OnPropertyChanged(nameof(HotKeyCallObjectOptions));
        OnPropertyChanged(nameof(HotKeyCallObjectSelection));
        OnPropertyChanged(nameof(HotKeyContentOptions));
        OnPropertyChanged(nameof(HotKeyContentSelection));
    }
}
