using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 2Tone Settings' Decode tab table - one 0x40-byte record per row (see
/// D890UvMemoryMap.TwoToneDecodeData). Confirmed 2026-08-06 across 2 live
/// differential WRITE captures, same technique as <see cref="TwoToneEncodeCodec"/>.
///
/// Confirmed offsets: 1st/2nd Tone Frequency (bytes 0x00/0x02, same uint16
/// LE Hz x10 encoding as Encode), Name (bytes 0x04-0x25, UTF-16LE, NULL-
/// padded - starts right after the frequencies here, unlike Encode's own
/// 4-byte gap before Name), Decoding Response (byte 0x26, raw index into
/// ["None", "Beep tone", "Beep tone &amp; Respond"] - confirmed both a
/// nonzero value (1) and 0/None on separate rows in the same capture). No
/// stored row-number byte, same convention as Encode.
/// </summary>
public static class TwoToneDecodeCodec
{
    public const int RecordLength = 0x40;

    private const int FirstToneFrequencyOffset = 0x00;
    private const int SecondToneFrequencyOffset = 0x02;
    private const int NameOffset = 0x04;
    private const int NameLength = 0x22; // wire capacity - UI still caps at 7, see CodeplugLimits.TwoToneNameMaxLength
    private const int DecodingResponseOffset = 0x26;

    public sealed record DecodedTwoToneDecode(int Index)
    {
        public double FirstToneFrequencyHz { get; init; }
        public double SecondToneFrequencyHz { get; init; }
        public byte DecodingResponse { get; init; }
        public string Name { get; init; } = "";
    }

    public static DecodedTwoToneDecode Decode(ReadOnlySpan<byte> data, int index)
    {
        var firstToneFrequencyHz = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(FirstToneFrequencyOffset, 2)) / 10.0;
        var secondToneFrequencyHz = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(SecondToneFrequencyOffset, 2)) / 10.0;
        var name = TextFieldCodec.DecodeName(data.Slice(NameOffset, NameLength));
        var decodingResponse = data[DecodingResponseOffset];

        return new DecodedTwoToneDecode(index)
        {
            FirstToneFrequencyHz = firstToneFrequencyHz,
            SecondToneFrequencyHz = secondToneFrequencyHz,
            DecodingResponse = decodingResponse,
            Name = name
        };
    }

    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedTwoToneDecode values)
    {
        var result = currentRecord.ToArray();

        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(FirstToneFrequencyOffset, 2), (ushort)Math.Round(values.FirstToneFrequencyHz * 10));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(SecondToneFrequencyOffset, 2), (ushort)Math.Round(values.SecondToneFrequencyHz * 10));
        TextFieldCodec.EncodeName(values.Name, NameLength).CopyTo(result, NameOffset);
        result[DecodingResponseOffset] = values.DecodingResponse;

        return result;
    }
}
