using Avalonia.Controls;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class ReceiveGroupListDetailView : UserControl
{
    public ReceiveGroupListDetailView()
    {
        InitializeComponent();
    }

    private void AvailableReceiveGroupListTalkgroupsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedAvailableReceiveGroupListTalkgroups(ListSelectionHelper.GetSelectedTalkgroups(listBox.SelectedItems));
        }
    }

    private void ReceiveGroupListMemberTalkgroupsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is ListBox listBox)
        {
            viewModel.SetSelectedReceiveGroupListMemberTalkgroups(ListSelectionHelper.GetSelectedTalkgroups(listBox.SelectedItems));
        }
    }
}
