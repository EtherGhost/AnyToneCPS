using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

/// <summary>
/// Blocks any character other than 0-9 and A-D (NOT full hex A-F - the
/// DTMF keypad has no E/F keys) from being typed or pasted into a
/// TextBox - added 2026-08-06 for DTMF Settings' M1-M16/PTT ID Starting
/// (BOT)/PTT ID Ending (EOT)/Remotely Kill/Remotely Stun fields,
/// confirmed directly from the vendor CPS field spec.
///
/// Same attached-property pattern as <see cref="HexDigitInput"/> - set
/// <c>views:DtmfCodeInput.Enabled="True"</c> on a TextBox in XAML.
/// </summary>
internal static class DtmfCodeInput
{
    public static void RejectNonDtmfCodeChars(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null)
        {
            return;
        }

        foreach (var c in e.Text)
        {
            if (!char.IsAsciiDigit(c) && c is (< 'A' or > 'D') and (< 'a' or > 'd'))
            {
                e.Handled = true;
                return;
            }
        }
    }

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(DtmfCodeInput));

    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);

    static DtmfCodeInput()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        element.RemoveHandler(InputElement.TextInputEvent, RejectNonDtmfCodeChars);
        if (args.NewValue is true)
        {
            element.AddHandler(InputElement.TextInputEvent, RejectNonDtmfCodeChars, RoutingStrategies.Tunnel);
        }
    }
}
