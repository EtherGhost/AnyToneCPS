using Avalonia.Controls;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class ScanListDetailView : UserControl
{
    public ScanListDetailView()
    {
        InitializeComponent();
    }

    private void AvailableScanListChannelsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableScanListChannels(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }

    private void ScanListMembersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedScanListMemberChannels(ListSelectionHelper.GetSelectedChannels(listBox.SelectedItems));
        }
    }
}
