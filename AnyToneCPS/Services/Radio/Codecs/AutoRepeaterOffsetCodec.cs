using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for a single D890UV Auto Repeater Offset Frequency entry.
/// Unlike every other entity decoded in this directory, this is a flat
/// contiguous array - 250 entries x 4 bytes each, no bitmap, no per-entry
/// name - starting at D890UvMemoryMap.AutoRepeaterData. Byte layout
/// transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (autorepeateroffsetfrequency.cpp,
/// decode_D890UV). Unlike RoamingChannelCodec/RoamingZoneCodec (whose
/// name-field offset was independently confirmed via a real hardware USB
/// capture), this entity has no name field to cross-check against, so that
/// same "cross-validated" confidence claim doesn't apply here - fixed
/// 2026-07-19 after this doc comment was found copy-pasted from those
/// codecs without adjusting for that difference.
///
/// Write side confirmed 2026-08-03 via a live differential write (Auto
/// Repeater Offset #3 set to 1 MHz): raw bytes A0 86 01 00 (LE) at
/// AutoRepeaterData + 2*4 = raw 100000 = 1.0 MHz, matching Encode below
/// exactly. Unlike every other entity in this project, there's no presence
/// bitmap at all - the whole 250-slot array is a flat contiguous region,
/// always captured in full regardless of which slots are "in use", and an
/// unused slot's raw value is plain 0 (not the usual 0xFF-erased
/// convention) - confirmed directly in the same capture, where every
/// not-yet-set slot surrounding the edited one read back as all-zero.
/// </summary>
public static class AutoRepeaterOffsetCodec
{
    public const int RecordLength = 4;
    public const int EntryCount = 250;

    public static DecodedAutoRepeaterOffset Decode(ReadOnlySpan<byte> data, int index)
    {
        // NOT the BCD-hex-string trick used elsewhere - the reference uses
        // a plain little-endian integer (`Int::fromBytes`), then divides by
        // 100000.0 for the MHz value (`getFrequencyDouble()`).
        var raw = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));

        return new DecodedAutoRepeaterOffset(index)
        {
            RawOffset = raw,
            OffsetFrequencyMhz = raw / 100000.0
        };
    }

    public sealed record DecodedAutoRepeaterOffset(int Index)
    {
        public int RawOffset { get; init; }
        public double OffsetFrequencyMhz { get; init; }
    }

    /// <summary>Encodes a single 4-byte little-endian record - the exact
    /// inverse of <see cref="Decode"/>. Passing 0.0 produces the same
    /// all-zero bytes the "unused slot" sentinel already uses, so this
    /// single method covers both a real value and a delete/clear.</summary>
    public static byte[] Encode(double offsetFrequencyMhz)
    {
        var raw = (uint)Math.Round(offsetFrequencyMhz * 100000.0);
        var result = new byte[RecordLength];
        BinaryPrimitives.WriteUInt32LittleEndian(result, raw);
        return result;
    }
}
