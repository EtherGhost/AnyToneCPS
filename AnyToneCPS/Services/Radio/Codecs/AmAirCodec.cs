using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for a single D890UV AM Air ("AM broadcast/aviation band" receive
/// channel) record, 0x40 bytes. Byte layout transcribed from the
/// MIT-licensed reference project github.com/xbenkozx/anytone-cps
/// (am_air.cpp, decode_D890UV). Same frequency BCD-hex-as-decimal trick as
/// <see cref="ChannelCodec"/>, and the same UTF-16LE name-with-blank-detection
/// as every other named entity (via <see cref="TextFieldCodec"/> - the
/// reference's own decode_D890UV constructs a QString from raw narrow bytes
/// unconditionally, which is the same latent bug already fixed elsewhere in
/// this codebase for channel/zone names). Frequency and Name are the only
/// two fields vendor CPS itself exposes for AM Air - confirmed 2026-08-02
/// directly from the vendor CPS dialog, no Scan flag or anything else.
/// Encode confirmed the same day via a live differential write (new row 11,
/// 125.30000 MHz "AM CH 011") - raw bytes matched this codec's existing
/// Decode transform exactly, and the presence bitmap set bit 10 as expected.
/// </summary>
public static class AmAirCodec
{
    public const int RecordLength = 0x40;

    /// <summary>Slot index used for the special always-present "VFO" entry -
    /// one beyond the 256 normal bitmap-addressable slots (0-255).</summary>
    public const int VfoIndex = 256;

    public static DecodedAmAir Decode(ReadOnlySpan<byte> data, int index)
    {
        var frequency = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 4)) / 100000.0;
        var name = TextFieldCodec.DecodeName(data.Slice(0x04, 0x20));

        return new DecodedAmAir(index)
        {
            FrequencyMHz = frequency,
            Name = name
        };
    }

    public sealed record DecodedAmAir(int Index)
    {
        public double FrequencyMHz { get; init; }
        public string Name { get; init; } = "";
    }

    /// <summary>Encodes both known fields into a copy of <paramref name="currentRecord"/>,
    /// leaving the trailing 0x1c bytes (0x24-0x40, always observed as zero
    /// in the live capture) untouched - same "preserve the unknown tail"
    /// discipline as <see cref="ScanListCodec.Encode"/>.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedAmAir values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"AM Air record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();

        var raw = (long)Math.Round(values.FrequencyMHz * 100000.0);
        BcdDecimalCodec.EncodeAsDecimal(raw, 4).CopyTo(result, 0x00);
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x04);

        return result;
    }
}
