using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for a single D890UV FM broadcast-radio channel record, 0x40
/// bytes. Byte layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (desktop/src/device.cpp
/// Device::readFmData - the confirmed D890UV branch; fm.cpp itself has no
/// decode method at all, everything is done inline in device.cpp).
///
/// Frequency uses the same BCD-hex-as-decimal trick as <see cref="ChannelCodec"/>
/// and <see cref="AmAirCodec"/>, but a DIFFERENT scale factor - confirmed via
/// the reference's own <c>FM::getFrequencyDouble()</c> (<c>frequency / 10000</c>,
/// not <c>/ 100000</c> like every other frequency field in this codebase) -
/// makes sense for FM broadcast (87.5-108.0 MHz range needs fewer decimal
/// digits of precision than a narrowband ham/DMR channel).
///
/// Name is decoded via the shared <see cref="TextFieldCodec"/> (UTF-16LE with
/// blank detection), NOT the reference's own inline `QString(bytes)`
/// conversion (misleadingly named `readFixedStringUtf8` in device.cpp despite
/// doing a narrow/8-bit conversion) - same latent bug class already fixed
/// elsewhere in this codebase for channel/zone/AM Air names.
/// </summary>
public static class FmChannelCodec
{
    public const int RecordLength = 0x40;

    /// <summary>Fixed 101-slot list (0-99 normal channels + one special
    /// always-present "home"/VFO channel, read from a different address -
    /// see <see cref="D890UvMemoryMap.FmMeta"/>).</summary>
    public const int HomeIndex = 100;

    public static DecodedFmChannel Decode(ReadOnlySpan<byte> data, bool scanAdd, int index)
    {
        var frequency = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 4)) / 10000.0;
        var name = TextFieldCodec.DecodeName(data.Slice(0x04, 0x20));

        return new DecodedFmChannel(index)
        {
            FrequencyMHz = frequency,
            Name = name,
            ScanAdd = scanAdd
        };
    }

    public sealed record DecodedFmChannel(int Index)
    {
        public double FrequencyMHz { get; init; }
        public string Name { get; init; } = "";
        public bool ScanAdd { get; init; }
    }

    /// <summary>Encodes Frequency+Name into a copy of <paramref name="currentRecord"/>,
    /// leaving the trailing 0x1c bytes untouched, same "preserve the unknown
    /// tail" discipline as AmAirCodec.Encode. Confirmed 2026-08-03 via a live
    /// differential write (new FM CH 2, 108.00 MHz): the record bytes matched
    /// this transform exactly (raw frequency "01080000" = 108.00 * 10000),
    /// and the active/scan bits (see <see cref="D890UvMemoryMap.FmActiveMaskOffset"/>/
    /// <see cref="D890UvMemoryMap.FmScanMaskOffset"/>, both inside the shared
    /// FmMeta block rather than a separate bitmap region like most other
    /// entities) were both set for the new channel's index.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedFmChannel values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"FM channel record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();

        var raw = (long)Math.Round(values.FrequencyMHz * 10000.0);
        BcdDecimalCodec.EncodeAsDecimal(raw, 4).CopyTo(result, 0x00);
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x04);

        return result;
    }
}
