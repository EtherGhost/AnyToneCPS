using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

/// <summary>
/// Blocks any non-printable-ASCII character (so no å/ä/ö or any other
/// accented/non-Latin letter) from being typed or pasted into a TextBox -
/// added 2026-07-31 for Name/label fields across Zones and Radio Settings -
/// this radio's screen can't render most non-ASCII characters correctly
/// even though the underlying wire encoding
/// (UTF-16LE on most fields) can technically store them.
///
/// Same attached-property pattern as <see cref="DigitOnlyInput"/> - set
/// <c>views:AsciiTextInput.Enabled="True"</c> on a TextBox in XAML. Fires
/// on the Tunnel phase so it runs before the TextBox's own "insert this
/// character" logic (see DigitOnlyInput's doc comment for why Tunnel,
/// not Bubble, is required).
/// </summary>
internal static class AsciiTextInput
{
    public static void RejectNonAscii(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null)
        {
            return;
        }

        foreach (var c in e.Text)
        {
            // Printable ASCII only (space through tilde) - excludes both
            // control characters and every accented/non-Latin character,
            // including å/ä/ö.
            if (c is < ' ' or > '~')
            {
                e.Handled = true;
                return;
            }
        }
    }

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(AsciiTextInput));

    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);

    static AsciiTextInput()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            element.AddHandler(InputElement.TextInputEvent, RejectNonAscii, RoutingStrategies.Tunnel);
        }
    }
}
