using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 2Tone Settings' Encode tab frequency table - one 0x20-byte record per
/// row (see D890UvMemoryMap.TwoToneEncodeData). Confirmed 2026-08-06 across
/// 2 live differential WRITE captures, blind-searched via the row's own
/// Name field as a grep anchor (same technique used for every other
/// blind-search entity).
///
/// Confirmed offsets: 1st/2nd Tone Frequency (bytes 0x00/0x02, uint16 LE,
/// raw = Hz x10 - e.g. 321.7 Hz -&gt; 0x0c91 = 3217), Name (bytes 0x08-0x1F,
/// UTF-16LE, NULL-padded - the standard TextFieldCodec convention, unlike
/// 5Tone's own space-padded Name). Bytes 0x04-0x07 were all-zero in every
/// sample so far and never attributed to any field - left untouched via
/// the usual RMW discipline. No stored row-number byte - Number is
/// inferred from array position, same convention as every other list
/// entity in this app.
/// </summary>
public static class TwoToneEncodeCodec
{
    public const int RecordLength = 0x20;

    private const int FirstToneFrequencyOffset = 0x00;
    private const int SecondToneFrequencyOffset = 0x02;
    private const int NameOffset = 0x08;
    private const int NameLength = 0x18; // 12 UTF-16LE chars of wire capacity - UI still caps at 7, see CodeplugLimits.TwoToneNameMaxLength

    public sealed record DecodedTwoToneEncode(int Index)
    {
        public double FirstToneFrequencyHz { get; init; }
        public double SecondToneFrequencyHz { get; init; }
        public string Name { get; init; } = "";
    }

    public static DecodedTwoToneEncode Decode(ReadOnlySpan<byte> data, int index)
    {
        var firstToneFrequencyHz = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(FirstToneFrequencyOffset, 2)) / 10.0;
        var secondToneFrequencyHz = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(SecondToneFrequencyOffset, 2)) / 10.0;
        var name = TextFieldCodec.DecodeName(data.Slice(NameOffset, NameLength));

        return new DecodedTwoToneEncode(index)
        {
            FirstToneFrequencyHz = firstToneFrequencyHz,
            SecondToneFrequencyHz = secondToneFrequencyHz,
            Name = name
        };
    }

    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedTwoToneEncode values)
    {
        var result = currentRecord.ToArray();

        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(FirstToneFrequencyOffset, 2), (ushort)Math.Round(values.FirstToneFrequencyHz * 10));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(SecondToneFrequencyOffset, 2), (ushort)Math.Round(values.SecondToneFrequencyHz * 10));
        TextFieldCodec.EncodeName(values.Name, NameLength).CopyTo(result, NameOffset);

        return result;
    }
}
