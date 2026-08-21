using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AnyToneCPS.Converters;

/// <summary>
/// Binds a ToggleSwitch's IsChecked to an existing "On"/"Off" string
/// property, reusing its existing dirty-tracking wiring instead of adding a
/// separate bool property per field.
/// </summary>
public sealed class OnOffToBoolConverter : IValueConverter
{
    public static readonly OnOffToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text && text == "On";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "On" : "Off";
}
