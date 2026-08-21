using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

public partial class OptionalSettingsView : UserControl
{
    public OptionalSettingsView()
    {
        InitializeComponent();

        // Tunnel, not the XAML "TextInput=..." attribute's default Bubble
        // routing - TextBox's own internal insert-into-text logic runs
        // first on Bubble (same control), so a Bubble handler is always too
        // late to block the character. Moved here 2026-08-07 when this
        // section was split out of MainView.axaml.cs, which had the same
        // wiring under an `OperatingSystem.IsAndroid()` early return (this
        // control is Desktop-only, MobileMainView has its own equivalent
        // PowerOnPasswordCharBox wiring in its own code-behind).
        PowerOnPasswordCharBox.AddHandler(InputElement.TextInputEvent, DigitOnlyInput.RejectNonDigits, RoutingStrategies.Tunnel);
    }
}
