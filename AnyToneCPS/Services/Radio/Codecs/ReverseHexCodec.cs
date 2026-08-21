using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Shared helper for the "2 raw bytes, byte-order reversed, then hex-
/// encoded" ID trick confirmed via live differential write captures for
/// both AlarmSettingsCodec's own QDC Group/Private ID fields and QDC 1200
/// Setting's Self ID/Call ID fields (Qdc1200SettingsCodec/Qdc1200IdCodec) -
/// same encoding, different entities. AlarmSettingsCodec keeps its own
/// private copy (already shipped/tested, not touched here); this shared
/// version is for the two new QDC 1200 Setting codecs only, to avoid a
/// third copy-paste.
/// </summary>
internal static class ReverseHexCodec
{
    public static string Decode(ReadOnlySpan<byte> twoBytes)
    {
        Span<byte> reversed = stackalloc byte[2];
        reversed[0] = twoBytes[1];
        reversed[1] = twoBytes[0];
        return Convert.ToHexString(reversed);
    }

    public static byte[] Encode(string fourCharHex)
    {
        var bytes = Convert.FromHexString(fourCharHex);
        return [bytes[1], bytes[0]];
    }
}
