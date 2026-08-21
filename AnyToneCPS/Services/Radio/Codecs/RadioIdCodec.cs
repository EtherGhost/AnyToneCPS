using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder/encoder for a single D890UV Radio ID record (0x40 bytes). Byte
/// layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (radioid.cpp, decode_D890UV) - CONFIRMED
/// 2026-08-06 via a live differential WRITE capture (2 rows: No.1 DMR ID
/// 11223344/Name "ABCDEFGHIJKLMNOPQ" - the full 17-char field, No.2 Name
/// "SHORT"). DMR ID BCD at 0x00-0x03, Name UTF-16LE at 0x04, stride 0x40,
/// and the presence bitmap at D890UvMemoryMap.RadioIdSet all matched
/// exactly. One informative side-finding, not a bug: No.2's own DMR ID was
/// typed as 99887766 but came back as 16777215 (0xFFFFFF) - the vendor CPS
/// itself silently clamps DMR ID to its real 24-bit range before ever
/// writing anything, confirming BcdDecimalCodec's own decode is correct
/// (it cleanly decoded the clamped value), not a sign the encode is wrong.
/// </summary>
public static class RadioIdCodec
{
    public const int RecordLength = D890UvMemoryMap.RadioIdDataLength; // 0x40

    public static DecodedRadioId Decode(ReadOnlySpan<byte> data, int index)
    {
        // Reference: `QString(data.mid(0,4).toHex()).toInt()`. Unlike
        // frequency fields, this is NOT divided afterward - the parsed
        // value IS the DMR ID directly.
        var dmrId = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 4));
        var name = TextFieldCodec.DecodeName(data.Slice(0x04, 52));

        return new DecodedRadioId(index)
        {
            DmrId = dmrId,
            Name = name
        };
    }

    /// <summary>Inverse of <see cref="Decode"/> - see this class's own doc
    /// comment for the live-capture confirmation. Bytes 0x38-0x3F (beyond
    /// the 52-byte Name field) are left untouched.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedRadioId values)
    {
        var result = currentRecord.ToArray();

        BcdDecimalCodec.EncodeAsDecimal(values.DmrId, 4).CopyTo(result, 0x00);
        TextFieldCodec.EncodeName(values.Name, 52).CopyTo(result, 0x04);

        return result;
    }

    public sealed record DecodedRadioId(int Index)
    {
        public long DmrId { get; init; }
        public string Name { get; init; } = "";
    }
}
