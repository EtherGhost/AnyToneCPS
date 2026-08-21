using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Shared raw-nibble-per-character encoding for every DTMF code-shaped
/// field in this app (M1-M16, PTT ID Starting/Ending, Remotely Kill/Stun) -
/// one byte per character, the byte's own VALUE is the DTMF symbol itself
/// (0-9 -&gt; 0x0-0x9, A-D -&gt; 0xA-0xD, * -&gt; 0xE confirmed, # -&gt; 0xF
/// inferred by the same pattern), 0xFF-padded to the field's fixed byte
/// length - confirmed 2026-08-06 via 2 live differential WRITE captures
/// (see D890UvMemoryMap.DtmfSettingsData's own doc comment for the full
/// confirmation, including M2's composed code matching this exact byte
/// shape). Deliberately NOT the same as TextFieldCodec's own UTF-16LE/NULL-
/// padding convention, and NOT the same as HexDigitInput's A-F range - DTMF
/// has no E/F keys, only A-D plus the two symbol keys.
/// </summary>
internal static class DtmfCodeCodec
{
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            if (b == 0xFF)
            {
                break;
            }

            sb.Append(NibbleToChar(b));
        }

        return sb.ToString();
    }

    public static byte[] Encode(string value, int byteLength)
    {
        var result = new byte[byteLength];
        Array.Fill(result, (byte)0xFF);
        var truncated = value.Length > byteLength ? value[..byteLength] : value;
        for (var i = 0; i < truncated.Length; i++)
        {
            result[i] = CharToNibble(truncated[i]);
        }

        return result;
    }

    private static char NibbleToChar(byte b) => b switch
    {
        <= 9 => (char)('0' + b),
        0xA => 'A',
        0xB => 'B',
        0xC => 'C',
        0xD => 'D',
        0xE => '*',
        0xF => '#',
        _ => '?'
    };

    private static byte CharToNibble(char c) => c switch
    {
        >= '0' and <= '9' => (byte)(c - '0'),
        'A' => 0xA,
        'B' => 0xB,
        'C' => 0xC,
        'D' => 0xD,
        '*' => 0xE,
        '#' => 0xF,
        _ => 0x0
    };
}
