using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnyToneCPS.Views;

/// <summary>
/// Blocks any non-digit character from being typed or pasted into a
/// TextBox, and optionally caps the resulting number at a max value - used
/// for the Power-on Password Char field (digits only, no cap) and the
/// Alert Tone Frequency/Period fields (digits only, capped at 3000/200).
///
/// TWO SEPARATE PIECES, both needed, added 2026-07-28 - don't remove
/// either without re-testing on both Desktop AND Android:
///
/// 1. THIS HANDLER (the actual character block). Must be wired via
///    code-behind with <c>AddHandler(InputElement.TextInputEvent,
///    DigitOnlyInput.RejectNonDigits, RoutingStrategies.Tunnel)</c> on the
///    specific named TextBox - see MainView.axaml.cs's constructor and
///    MobileMainView.axaml.cs's constructor for the two live examples.
///    A first attempt used the plain XAML attribute form
///    (<c>TextInput="SomeHandler"</c>) - that does NOT work: it attaches
///    at Bubble routing, but TextBox's own internal "insert this
///    character into Text" logic already runs earlier on the same
///    control, so by the time a Bubble handler sees the event the letter
///    has already been inserted. Tunnel fires first, so setting
///    <c>e.Handled = true</c> during Tunnel actually prevents the
///    insertion.
///
/// 2. THE "PinDigits" STYLE CLASS (MainView.axaml's and
///    MobileMainView.axaml's own `&lt;UserControl.Styles&gt;`, setter
///    `ti:TextInputOptions.ContentType="Number"`) - hints the platform's
///    on-screen keyboard to show a numeric layout, mainly relevant on
///    Android's soft keyboard. Switched from a plain inline XAML
///    attribute to a style class so it can be reused app-wide.
///
/// A range cap (<see cref="MaxValueProperty"/>) can't be enforced the same
/// way as the digit check, since a single digit is always a valid
/// character on its own - only the resulting whole number can be judged
/// out of range (e.g. typing "9" then "9" then "9" then "9" into a
/// max="3000" field is fine until the last keystroke). Set
/// <c>views:DigitOnlyInput.MaxValue="3000"</c> alongside
/// <c>Enabled="True"</c> to reject a keystroke whose resulting text would
/// exceed it - this was missed on the Alert Tone Frequency/Period fields
/// when they first got digit-only filtering (2026-07-29): they had the
/// character check but no range cap, so e.g. "9999" typed in fine despite
/// the model's own 0-3000 range validation - that validation only reverts
/// on a full invalid value being committed to the bound property, which
/// doesn't stop the digits from visibly being typed in the meantime. Also
/// missed on the same fields: the "PinDigits" style class itself (Android's
/// numeric keyboard hint, see piece 2 below) - only wired up for Password
/// Char at first, added retroactively to these fields the same day.
///
/// <b>Confirmed working on both Desktop and Android</b> (2026-07-28/29).
/// Android testing had repeatedly appeared broken across several fix
/// attempts, which looked like this code was at fault - the real cause
/// turned out to be unrelated to any of it: testing was done by
/// `adb install`-ing the plain Debug APK, but Debug Android builds use
/// Fast Deployment (assemblies pushed to the device separately from the
/// APK by the normal build/deploy pipeline, not embedded in it) - a raw
/// `adb install` skips that push, so the app aborted on startup
/// (`monodroid: No assemblies found ... Assuming this is part of Fast
/// Deployment. Exiting`) before it ever got to render anything. Every
/// "still broken" report was this crash, not the actual fix failing.
/// Confirmed by checking logcat after a fresh install. The fix: test
/// Android changes with a real NativeAOT publish
/// (`scripts/publish-android-nativeaot.sh`) instead of a plain debug
/// `adb install` - NativeAOT publishes are self-contained and don't hit
/// this Fast Deployment path at all.
/// </summary>
internal static class DigitOnlyInput
{
    /// <summary>Optional per-control cap (see <see cref="MaxValueProperty"/>)
    /// - if set, a keystroke is rejected when the resulting full text would
    /// parse to a number above it. Added 2026-07-29 after the Alert Tone
    /// Frequency/Period fields shipped with only the digit-only check: that
    /// blocks letters but not e.g. typing "9999" into a field capped at
    /// 3000, since every individual digit is itself a valid character - the
    /// range can only be judged by looking at the resulting whole number,
    /// which needs the control's current Text/selection, not just the
    /// incoming keystroke. <see cref="AllowDecimalPointProperty"/> extends
    /// this to decimal fields (added 2026-07-30 for the VFO Scan frequency
    /// fields) - a single '.' is allowed through in addition to digits, and
    /// the range check parses as a double instead of an integer. Only an
    /// upper bound is enforced here by design: a lower bound can't be judged
    /// mid-keystroke the same way, since more digits could still push an
    /// currently-too-small prefix up into range (e.g. typing "1" toward
    /// "136" for a 136-174 VHF field) - the model's own Text setter handles
    /// the lower bound with reject-and-revert on focus loss/commit instead.</summary>
    public static void RejectNonDigits(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null)
        {
            return;
        }

        var allowDecimalPoint = sender is InputElement senderElement && GetAllowDecimalPoint(senderElement);

        foreach (var c in e.Text)
        {
            if (char.IsAsciiDigit(c) || (allowDecimalPoint && c == '.'))
            {
                continue;
            }

            e.Handled = true;
            return;
        }

        if (sender is not TextBox textBox)
        {
            return;
        }

        var prospectiveText = ProspectiveText(textBox, e.Text);

        if (allowDecimalPoint && prospectiveText.Count(c => c == '.') > 1)
        {
            e.Handled = true;
            return;
        }

        if (GetMaxValue(textBox) is { } maxValue)
        {
            var style = allowDecimalPoint ? NumberStyles.Float : NumberStyles.Integer;
            if (double.TryParse(prospectiveText, style, CultureInfo.InvariantCulture, out var prospectiveValue)
                && prospectiveValue > maxValue)
            {
                e.Handled = true;
            }
        }
    }

    private static string ProspectiveText(TextBox textBox, string insertedText)
    {
        var text = textBox.Text ?? "";
        var start = Math.Clamp(textBox.SelectionStart, 0, text.Length);
        var end = Math.Clamp(textBox.SelectionEnd, 0, text.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return string.Concat(text.AsSpan(0, start), insertedText, text.AsSpan(end));
    }

    /// <summary>Declarative alternative to the code-behind
    /// <see cref="RejectNonDigits"/> wiring above - needed for the Alert
    /// Tone Frequency/Period fields (added 2026-07-29), where each TextBox
    /// is either one of 50 individual controls (Desktop) or one instance of
    /// a DataTemplate repeated per list item (Mobile) - neither has a
    /// single named control a constructor could reach. Set
    /// <c>views:DigitOnlyInput.Enabled="True"</c> directly on the TextBox in
    /// XAML instead; this fires per-instance, including once per templated
    /// item.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(DigitOnlyInput));

    /// <summary>Optional companion to <see cref="EnabledProperty"/> - set
    /// <c>views:DigitOnlyInput.MaxValue="3000"</c> alongside
    /// <c>Enabled="True"</c> to also block keystrokes that would push the
    /// field's number above this value. Leave unset for a plain digit-only
    /// field with no upper bound (e.g. Power-on Password Char). A double
    /// (widened from int 2026-07-30 for the VFO Scan frequency fields) -
    /// works identically for whole-number fields like Alert Tone's
    /// Frequency/Period, since e.g. "3000" parses the same as a double or
    /// an int.</summary>
    public static readonly AttachedProperty<double?> MaxValueProperty =
        AvaloniaProperty.RegisterAttached<InputElement, double?>("MaxValue", typeof(DigitOnlyInput));

    /// <summary>Set <c>views:DigitOnlyInput.AllowDecimalPoint="True"</c>
    /// alongside <c>Enabled="True"</c> for a decimal field (e.g. a MHz
    /// frequency) - allows one '.' through in addition to digits, and makes
    /// <see cref="MaxValueProperty"/>'s range check parse as a decimal
    /// instead of a whole number. Added 2026-07-30 for the VFO Scan Start/
    /// End Freq (UHF/VHF) fields.</summary>
    public static readonly AttachedProperty<bool> AllowDecimalPointProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("AllowDecimalPoint", typeof(DigitOnlyInput));

    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);
    public static void SetMaxValue(InputElement element, double? value) => element.SetValue(MaxValueProperty, value);
    public static double? GetMaxValue(InputElement element) => element.GetValue(MaxValueProperty);
    public static void SetAllowDecimalPoint(InputElement element, bool value) => element.SetValue(AllowDecimalPointProperty, value);
    public static bool GetAllowDecimalPoint(InputElement element) => element.GetValue(AllowDecimalPointProperty);

    static DigitOnlyInput()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            element.AddHandler(InputElement.TextInputEvent, RejectNonDigits, RoutingStrategies.Tunnel);
        }
    }
}
