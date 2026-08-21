using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// DTMF Settings' M1-M16 list - one 0x10-byte record per slot (see
/// D890UvMemoryMap.DtmfEncodeData). Confirmed 2026-08-06 across 2 live
/// differential WRITE captures - see DtmfCodeCodec's own doc comment for
/// the shared raw-nibble-per-char/0xFF-padded encoding, and
/// D890UvMemoryMap.DtmfSettingsData's doc comment for the full
/// confirmation story (including the composed-code byte match).
/// </summary>
public static class DtmfEncodeCodec
{
    public const int RecordLength = 0x10;

    /// <summary>Fixed 16-slot M1-M16 list (see CodeplugLimits.DtmfEncodeSlotCount
    /// for the Models-layer equivalent) - kept here too so
    /// RadioCodeplugReader doesn't need a Models dependency, same reasoning
    /// as DtmfSettingsCodec.TransmittingTimeMsValues.</summary>
    public const int SlotCount = 16;

    public sealed record DecodedDtmfEncode(int Index)
    {
        public string Code { get; init; } = "";
    }

    public static DecodedDtmfEncode Decode(ReadOnlySpan<byte> data, int index) => new(index)
    {
        Code = DtmfCodeCodec.Decode(data)
    };

    public static byte[] Encode(string code) => DtmfCodeCodec.Encode(code, RecordLength);
}
