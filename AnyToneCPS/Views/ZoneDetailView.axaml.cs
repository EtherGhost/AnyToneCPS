using Avalonia.Controls;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class ZoneDetailView : UserControl
{
    public ZoneDetailView()
    {
        InitializeComponent();
    }

    private void AvailableZoneChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableZoneChannels(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void ZoneMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedZoneMembers(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }
}
