using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using AnyToneCPS.Models;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        if (OperatingSystem.IsAndroid())
        {
            Content = new MobileMainView();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is MainViewModel viewModel && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            viewModel.SetStoragePicker(new AvaloniaStoragePickerService(topLevel));

            // On Desktop this duplicates MainWindow.axaml.cs's own call
            // (harmless - SetRadioServices just reassigns fields and
            // rescans). On Android/Mobile this is the ONLY place it's
            // called from, since there's no MainWindow there - see
            // AnyToneCPS.Android/MainActivity.cs for where Factory gets set.
            if (RadioConnectionProvider.Factory is { } factory)
            {
                viewModel.SetRadioServices(factory, RadioConnectionProvider.PortLister);
            }
        }
    }

    // 2026-08-01: the chevron alone was too small a click target (user
    // feedback, especially on Android). Rather than enlarge the chevron
    // itself, the header text next to it now toggles expand/collapse too -
    // no new ViewModel state needed, since TreeViewItem already owns
    // IsExpanded; this just flips it directly on the container found by
    // walking up from whatever was tapped. Leaf nodes (no children) still
    // only navigate via SelectedItem, unaffected.
    private void NavigationTreeItem_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: NavigationTreeNode { HasChildren: true } } control
            && control.FindAncestorOfType<TreeViewItem>() is { } item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    // Channels ListBox uses SelectionMode="Multiple" for Ctrl/Shift-click
    // bulk select (built into Avalonia's SelectingItemsControl, same as the
    // "Available/Members" ListBox pairs elsewhere in the app) - this keeps
    // MainViewModel.SelectedChannels in sync with the ListBox's own selection,
    // the same one-way view-to-VM pattern ZoneDetailView.axaml.cs already uses.
    private void ChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedChannels(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }

    // Real vendor CPS gesture (confirmed 2026-08-05) - not the most
    // discoverable interaction, but it's how the vendor CPS itself works:
    // double-clicking a 5Tone ID row asks to reset its own Special Call
    // state. MainViewModel.ResetFiveToneRowSpecialCallCommand itself
    // no-ops for a never-configured row, so no need to check that here.
    private void FiveToneRow_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FiveToneIdEntry entry } && DataContext is MainViewModel viewModel)
        {
            viewModel.ResetFiveToneRowSpecialCallCommand.Execute(entry);
        }
    }
}
