using Avalonia.Controls;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class AmZoneDetailView : UserControl
{
    public AmZoneDetailView()
    {
        InitializeComponent();
    }

    private void AvailableAmZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableAmZoneChannels(ListSelectionHelper.GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AmZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAmZoneMembers(ListSelectionHelper.GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AvailableAmZoneScanChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableAmZoneScanChannels(ListSelectionHelper.GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }

    private void AmZoneScanChannelMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAmZoneScanChannelMembers(ListSelectionHelper.GetSelectedAmAirChannels(listBox.SelectedItems));
        }
    }
}
