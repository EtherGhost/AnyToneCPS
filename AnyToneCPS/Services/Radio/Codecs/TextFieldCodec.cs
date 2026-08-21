using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Shared helper for decoding the UTF-16LE "name" fields used throughout the
/// D890UV codeplug (channel, zone, radio ID, talkgroup, scan list, roaming
/// channel/zone, receive group list all have one).
///
/// A slot that was never written to (or was erased) reads back as flash's
/// erased state - typically all <c>0xFF</c> bytes. Naively decoding that as
/// UTF-16LE and trimming NUL characters does NOT clean it up: 0xFF 0xFF byte
/// pairs decode to the Unicode noncharacter U+FFFF repeated, which is not a
/// NUL character and is not whitespace, so it survives `TrimEnd('\0')` and
/// shows up in the UI as garbage "strange symbols" text - exactly the bug
/// found on unconfigured Roaming Zones. This helper detects
/// an all-0xFF (or all-0x00, just in case) blank field up front and returns
/// an empty string for it instead of decoding garbage.
/// </summary>
internal static class TextFieldCodec
{
    public static string DecodeName(ReadOnlySpan<byte> bytes)
    {
        if (IsBlank(bytes))
        {
            return "";
        }

        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>Inverse of <see cref="DecodeName"/>: UTF-16LE-encodes
    /// <paramref name="value"/> into a zero-padded buffer exactly
    /// <paramref name="byteLength"/> bytes long (truncating the value if it
    /// doesn't fit). Always zero-pads, never 0xFF-pads - an empty/cleared
    /// name field should read back as blank via <see cref="DecodeName"/>'s
    /// all-0x00 check, not accidentally look like erased flash.</summary>
    public static byte[] EncodeName(string value, int byteLength)
    {
        var result = new byte[byteLength];
        var maxChars = byteLength / 2;
        var truncated = value.Length > maxChars ? value[..maxChars] : value;
        Encoding.Unicode.GetBytes(truncated).CopyTo(result, 0);
        return result;
    }

    private static bool IsBlank(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return true;
        }

        var first = bytes[0];
        if (first != 0xFF && first != 0x00)
        {
            return false;
        }

        foreach (var b in bytes)
        {
            if (b != first)
            {
                return false;
            }
        }

        return true;
    }
}
