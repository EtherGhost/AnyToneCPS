using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AnyToneCPS.Converters;

/// <summary>
/// Disables a single ComboBoxItem inside a plain string-list ItemsSource -
/// bind IsEnabled to this with the item's own value as the bound value and
/// the value to disable as ConverterParameter. Used for TX Interrupt's
/// "High priority": a real vendor CPS option whose raw encoding isn't
/// confirmed, so it's shown for parity but blocked from being selected.
/// </summary>
public sealed class DisabledWhenEqualsConverter : IValueConverter
{
    public static readonly DisabledWhenEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
