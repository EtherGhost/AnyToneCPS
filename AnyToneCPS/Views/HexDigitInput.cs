using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

/// <summary>
/// Blocks any non-hex character (anything other than 0-9, a-f, A-F) from
/// being typed or pasted into a TextBox - added 2026-08-04 for Alarm
/// Settings' QDC1200 Group ID/Private ID fields (both raw hex-nibble
/// strings, per AlarmSettingsCodec's own reversed-hex encode/decode).
/// Character-count limits are handled by TextBox's own native MaxLength
/// property, not here - this only filters which characters are allowed.
///
/// Same attached-property pattern as <see cref="DigitOnlyInput"/>/
/// <see cref="AsciiTextInput"/> - set <c>views:HexDigitInput.Enabled="True"</c>
/// on a TextBox in XAML. Fires on the Tunnel phase so it runs before the
/// TextBox's own "insert this character" logic.
/// </summary>
internal static class HexDigitInput
{
    public static void RejectNonHexDigits(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null)
        {
            return;
        }

        foreach (var c in e.Text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                e.Handled = true;
                return;
            }
        }
    }

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(HexDigitInput));

    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);

    static HexDigitInput()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        // Every prior usage set Enabled="True" as a static XAML literal, so
        // this never had to handle toggling back to False - added 2026-08-06
        // for FiveToneIdEntry's own Encode ID field, whose hex-only
        // restriction needs to turn off when a Send Message Special Call
        // (free ASCII text, not hex) is composed into it. Without the
        // RemoveHandler call, a control that was once Enabled="True" would
        // keep rejecting non-hex keystrokes forever, even after being
        // re-bound to False.
        element.RemoveHandler(InputElement.TextInputEvent, RejectNonHexDigits);
        if (args.NewValue is true)
        {
            element.AddHandler(InputElement.TextInputEvent, RejectNonHexDigits, RoutingStrategies.Tunnel);
        }
    }
}
