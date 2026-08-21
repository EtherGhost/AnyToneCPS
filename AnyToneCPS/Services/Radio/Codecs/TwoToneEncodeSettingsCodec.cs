using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 2Tone Settings' Encode tab scalar fields - a single 0x10-byte block at
/// D890UvMemoryMap.TwoToneEncodeSettingsData. Confirmed 2026-08-06 across 2
/// live differential WRITE captures: all 6 fields matched their set values
/// exactly (2.5/3.5/4.5s durations, 1500ms gap, 55s auto reset, Side Tone
/// on). Bytes 0x00-0x08 and 0x0F were all-zero in both captures and never
/// attributed to any field - left untouched via the usual RMW discipline.
///
/// Sits 0x20 bytes after D890UvMemoryMap.TwoToneEncodeBitmap - see that
/// constant's own doc comment for the full 3-region layout (bitmap
/// block, this settings block, then the row tables).
/// </summary>
public static class TwoToneEncodeSettingsCodec
{
    public const int RecordLength = 0x10;

    private const int FirstToneDurationOffset = 0x09;
    private const int SecondToneDurationOffset = 0x0A;
    private const int LongToneDurationOffset = 0x0B;
    private const int GapTimeOffset = 0x0C;
    private const int AutoResetTimeOffset = 0x0D;
    private const int SideToneOffset = 0x0E;

    public sealed record DecodedTwoToneEncodeSettings(
        double FirstToneDurationSeconds,
        double SecondToneDurationSeconds,
        double LongToneDurationSeconds,
        int GapTimeMs,
        int AutoResetTimeSeconds,
        bool SideTone);

    public static DecodedTwoToneEncodeSettings Decode(ReadOnlySpan<byte> data) => new(
        FirstToneDurationSeconds: data[FirstToneDurationOffset] / 10.0,
        SecondToneDurationSeconds: data[SecondToneDurationOffset] / 10.0,
        LongToneDurationSeconds: data[LongToneDurationOffset] / 10.0,
        GapTimeMs: data[GapTimeOffset] * 100,
        AutoResetTimeSeconds: data[AutoResetTimeOffset],
        SideTone: data[SideToneOffset] != 0);

    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedTwoToneEncodeSettings values)
    {
        var result = currentRecord.ToArray();

        result[FirstToneDurationOffset] = (byte)Math.Round(values.FirstToneDurationSeconds * 10);
        result[SecondToneDurationOffset] = (byte)Math.Round(values.SecondToneDurationSeconds * 10);
        result[LongToneDurationOffset] = (byte)Math.Round(values.LongToneDurationSeconds * 10);
        result[GapTimeOffset] = (byte)(values.GapTimeMs / 100);
        result[AutoResetTimeOffset] = (byte)values.AutoResetTimeSeconds;
        result[SideToneOffset] = (byte)(values.SideTone ? 1 : 0);

        return result;
    }
}
