using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AnyToneCPS.Views;

public partial class AboutDetailView : UserControl
{
    public AboutDetailView()
    {
        InitializeComponent();
        SetLogoImage();
    }

    private void SetLogoImage()
    {
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://AnyToneCPS/Assets/Icon.png"));
            LogoImage.Source = new Bitmap(iconStream);
        }
        catch
        {
            // No logo shown if the packaged resource is unavailable - see
            // MainWindow.axaml.cs's identical fallback for the window icon.
        }
    }
}
