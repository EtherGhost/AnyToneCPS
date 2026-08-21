using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AnyToneCPS.Converters;

/// <summary>
/// Shows the AES/ARC4 Key combobox based on the actual type selectors, not
/// the raw key index - gating on the key index was circular (the combobox
/// that assigns a key never appeared until a key was already assigned).
/// [0] = OptionalSettings.IsAesArc4EncryptionTypeSelected (device-wide
/// switch), [1] = SelectedChannel.ExtendEncryption (false=AES, true=ARC4).
/// ConverterParameter is the expected polarity of [1].
/// </summary>
public sealed class EncryptionKeyVisibilityConverter : IMultiValueConverter
{
    public static readonly EncryptionKeyVisibilityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count == 2
            && values[0] is true
            && values[1] is bool actual
            && parameter is string expectedText
            && bool.TryParse(expectedText, out var expected)
            && actual == expected;
}
