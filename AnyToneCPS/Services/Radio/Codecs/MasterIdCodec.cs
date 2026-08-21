using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder/encoder for the D890UV's single Master ID record (0x40 bytes,
/// fixed address - there is only ever one, no bitmap/list involved). Byte
/// layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (master_id.cpp, decode_D890UV) - CONFIRMED
/// 2026-08-06 via a live differential WRITE capture, all 3 fields matched
/// exactly (DMR ID BCD at 0x00-0x03, Name UTF-16LE 32 bytes/16 chars at
/// 0x04-0x23, Used at 0x26). This also resolved a real doubt raised before
/// the capture: an initial field spec said Name maxlength 26,
/// which conflicted with this codec's own 16-char capacity - the vendor CPS
/// itself silently truncates anything past 16 characters, confirming this
/// layout was right and the spec was the one that needed correcting.
/// </summary>
public static class MasterIdCodec
{
    public const int RecordLength = D890UvMemoryMap.MasterIdDataLength; // 0x40

    public static DecodedMasterId Decode(ReadOnlySpan<byte> data)
    {
        var dmrId = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 4));
        var used = data[0x26] == 1;
        var name = TextFieldCodec.DecodeName(data.Slice(0x04, 0x20));

        return new DecodedMasterId
        {
            DmrId = dmrId,
            Used = used,
            Name = name
        };
    }

    /// <summary>Inverse of <see cref="Decode"/> - write support added
    /// 2026-08-06, confirmed via a live differential WRITE capture
    /// (DMR ID 12345678, Name "ABCDEFGHIJKLMNOP" filling the full 16-char
    /// capacity, Used checked - all 3 fields matched exactly, including
    /// the Name field boundary that was flagged as unconfirmed before this
    /// capture). Bytes 0x24-0x25 and 0x27-0x3F are left untouched.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedMasterId values)
    {
        var result = currentRecord.ToArray();

        BcdDecimalCodec.EncodeAsDecimal(values.DmrId, 4).CopyTo(result, 0x00);
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x04);
        result[0x26] = (byte)(values.Used ? 1 : 0);

        return result;
    }

    public sealed record DecodedMasterId
    {
        public long DmrId { get; init; }
        public bool Used { get; init; }
        public string Name { get; init; } = "";
    }
}
