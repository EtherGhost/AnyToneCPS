using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

/// <summary>
/// Blocks anything except uppercase A-Z and digits 0-9 from being typed or
/// pasted into a TextBox - added 2026-08-15 for APRS ToCall/YourCall
/// (confirmed against the real vendor CPS: capital letters and digits
/// only, no lowercase, no special characters). Deliberately rejects
/// lowercase rather than auto-uppercasing it - same "reject, don't
/// transform" convention as every other input filter in this app
/// (DigitOnlyInput/AsciiTextInput/HexDigitInput).
///
/// Same attached-property pattern as <see cref="AsciiTextInput"/> - set
/// <c>views:CallsignInput.Enabled="True"</c> on a TextBox in XAML. Fires on
/// the Tunnel phase so it runs before the TextBox's own "insert this
/// character" logic (see DigitOnlyInput's doc comment for why Tunnel, not
/// Bubble, is required).
/// </summary>
internal static class CallsignInput
{
    public static void RejectNonCallsignCharacters(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null)
        {
            return;
        }

        foreach (var c in e.Text)
        {
            if (c is < 'A' or > 'Z' && c is < '0' or > '9')
            {
                e.Handled = true;
                return;
            }
        }
    }

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(CallsignInput));

    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);

    static CallsignInput()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            element.AddHandler(InputElement.TextInputEvent, RejectNonCallsignCharacters, RoutingStrategies.Tunnel);
        }
    }
}
