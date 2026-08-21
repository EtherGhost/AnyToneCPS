using Avalonia.Controls;
using Avalonia.Platform;
using System;
using AnyToneCPS.Services;
using AnyToneCPS.Services.Radio;
using AnyToneCPS.ViewModels;

namespace AnyToneCPS.Views;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        SetLinuxFriendlyWindowIcon();
    }

    private void SetLinuxFriendlyWindowIcon()
    {
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://AnyToneCPS/Assets/Icon.png"));
            Icon = new WindowIcon(iconStream);
        }
        catch
        {
            // Keep the XAML icon fallback if the packaged resource is unavailable.
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetStoragePicker(new AvaloniaStoragePickerService(this));

            if (RadioConnectionProvider.Factory is { } factory)
            {
                viewModel.SetRadioServices(factory, RadioConnectionProvider.PortLister);
            }
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        // Unconditional, not a confirm/discard choice - interrupting a live
        // serial write partway through could leave the radio's codeplug in
        // a corrupted state (found live 2026-07-19: nothing stopped closing
        // the app mid-write before this). No safe way to "confirm" this one
        // away; the only safe action is to wait for it to finish.
        if (DataContext is MainViewModel writingViewModel && writingViewModel.IsWritingToRadio)
        {
            e.Cancel = true;
            writingViewModel.StatusMessage = "Cannot close while a write to the radio is in progress - please wait for it to finish.";
            return;
        }

        if (_closeConfirmed || DataContext is not MainViewModel viewModel || !viewModel.IsDirty)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (!await viewModel.ConfirmCanDiscardUnsavedChangesAsync())
        {
            return;
        }

        _closeConfirmed = true;
        Close();
    }
}
