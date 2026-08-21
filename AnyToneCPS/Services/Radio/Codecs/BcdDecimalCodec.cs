using System;
using System.Globalization;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Shared helper for the D890UV's "BCD-hex-string-as-decimal" encoding
/// trick used for both frequencies and DMR IDs: each byte holds two BCD
/// digits (0-9), so converting the raw bytes to a hex string yields a
/// string made entirely of decimal digit characters, which is then parsed
/// as a base-10 integer (`QString(bytes.toHex()).toInt()` in the reference
/// project github.com/xbenkozx/anytone-cps, MIT-licensed).
/// </summary>
internal static class BcdDecimalCodec
{
    public static long DecodeAsDecimal(ReadOnlySpan<byte> bytes)
    {
        // Qt's QString::toInt() (used by the reference project) silently
        // returns 0 when the string isn't a valid number - it never throws.
        // An erased/unused flash slot reads back as all 0xFF bytes, whose hex
        // string ("FFFFFFFF"...) contains letters and isn't valid decimal, so
        // this must degrade to 0 (meaning "no ID set") rather than throw -
        // otherwise a single blank slot among thousands would crash the
        // entire read.
        var hex = Convert.ToHexString(bytes);
        return long.TryParse(hex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    /// <summary>Inverse of <see cref="DecodeAsDecimal(ReadOnlySpan{byte})"/>:
    /// formats <paramref name="value"/> as a zero-padded decimal string
    /// exactly <c>byteCount * 2</c> digits long, then interprets that digit
    /// string as hex byte pairs - the same "hex string of a byte array is
    /// literally its decimal digits" trick the decode side relies on, run
    /// backward. Throws if <paramref name="value"/> doesn't fit in
    /// <c>byteCount * 2</c> decimal digits.</summary>
    public static byte[] EncodeAsDecimal(long value, int byteCount)
    {
        var digits = byteCount * 2;
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Length > digits)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {digits} decimal digits.");
        }

        return Convert.FromHexString(text.PadLeft(digits, '0'));
    }

    /// <summary>Variable-length counterpart used by Analog Address Book:
    /// only the first <paramref name="digitCount"/> hex characters (out of
    /// the full 2-per-byte hex string) are meaningful, matching the
    /// reference's `bytes.toHex().mid(0, number_len).toInt()`.</summary>
    public static long DecodeAsDecimal(ReadOnlySpan<byte> bytes, int digitCount)
    {
        if (digitCount <= 0)
        {
            return 0;
        }

        var hex = Convert.ToHexString(bytes);
        var prefix = hex[..Math.Min(digitCount, hex.Length)];
        return long.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
