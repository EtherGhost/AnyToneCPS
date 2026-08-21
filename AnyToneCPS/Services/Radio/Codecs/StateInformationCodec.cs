using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>Hot Key &gt; State Information - a flat 32-slot array, 64 bytes
/// each. Each slot is a UTF-16LE text buffer with no separate length field
/// (32 chars x 2 bytes = 64 bytes exactly), reusing TextFieldCodec like
/// every other text field. No bitmap - an unused slot just decodes to an
/// empty string. Address/stride confirmed 2026-08-04 via a live
/// differential read capture.</summary>
public static class StateInformationCodec
{
    public const int RecordLength = 0x40;
    public const int SlotCount = 32;

    public static string Decode(ReadOnlySpan<byte> data) => TextFieldCodec.DecodeName(data);

    public static byte[] Encode(string content) => TextFieldCodec.EncodeName(content, RecordLength);
}
