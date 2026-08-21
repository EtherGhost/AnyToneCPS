using Avalonia.Controls;
using Avalonia.Interactivity;
using AnyToneCPS.Models;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class DtmfDetailView : UserControl
{
    public DtmfDetailView()
    {
        InitializeComponent();
    }

    // Same pattern as MainView's own FiveToneRow_OnDoubleTapped - the DTMF
    // Encode list's own per-row "Special Call" button can't bind a
    // MainViewModel command from inside its own DataTemplate, so this reads
    // the button's own DataContext (the row) directly instead. Moved here
    // 2026-08-06/2026-08-10 with the rest of the Dtmf detail panel.
    private void DtmfSpecialCallButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DtmfEncodeEntry entry } && DataContext is MainViewModel viewModel)
        {
            viewModel.OpenDtmfSpecialCallCommand.Execute(entry);
        }
    }
}
