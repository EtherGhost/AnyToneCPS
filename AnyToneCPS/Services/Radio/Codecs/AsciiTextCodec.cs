using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Shared helper for the D890UV's narrow/8-bit ASCII text fields (APRS
/// callsigns, digipeater path, symbol/icon codes) - a genuine exception to
/// the UTF-16LE convention used by every named entity elsewhere in this
/// codebase (channel/zone/AM Air/FM names etc, see <see cref="TextFieldCodec"/>).
/// APRS/AX.25 callsigns are conventionally narrow ASCII on the wire, matching
/// the reference project's own `QString(bytes)` conversion for these
/// specific fields (unlike the D890UV name fields, where that same
/// conversion pattern is a latent bug - here it's actually correct).
///
/// Applies the same all-0xFF/all-0x00 blank-field detection as
/// <see cref="TextFieldCodec"/> before decoding, for the same reason: an
/// erased/unconfigured field reads back as one of those two fill patterns,
/// and naively decoding either as ASCII produces garbage ('?' repeated for
/// 0xFF, since it's not valid 7-bit ASCII) that would survive a plain
/// `TrimEnd('\0')`.
/// </summary>
internal static class AsciiTextCodec
{
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (IsBlank(bytes))
        {
            return "";
        }

        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>Inverse of <see cref="Decode"/>: ASCII-encodes
    /// <paramref name="value"/> into a zero-padded buffer exactly
    /// <paramref name="byteLength"/> bytes long (truncating the value if it
    /// doesn't fit) - same zero-pad-never-0xFF-pad convention as
    /// <see cref="TextFieldCodec.EncodeName"/>.</summary>
    public static byte[] Encode(string value, int byteLength)
    {
        var result = new byte[byteLength];
        var truncated = value.Length > byteLength ? value[..byteLength] : value;
        Encoding.ASCII.GetBytes(truncated).CopyTo(result, 0);
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
