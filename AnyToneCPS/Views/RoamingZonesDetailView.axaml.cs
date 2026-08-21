using Avalonia.Controls;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class RoamingZonesDetailView : UserControl
{
    public RoamingZonesDetailView()
    {
        InitializeComponent();
    }

    private void AvailableRoamingZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableRoamingZoneChannels(ListSelectionHelper.GetSelectedRoamingChannels(listBox.SelectedItems));
        }
    }

    private void RoamingZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedRoamingZoneMembers(ListSelectionHelper.GetSelectedRoamingChannels(listBox.SelectedItems));
        }
    }
}
