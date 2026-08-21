using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for a single D890UV GPS Roaming record, 0x10 bytes, one of 32
/// FIXED slots (no bitmap, no add/remove - see GpsRoamingEntry's own doc
/// comment). Byte layout transcribed from the MIT-licensed reference
/// project github.com/xbenkozx/anytone-cps (gps_roaming.cpp, decode()) and
/// desktop/src/device.cpp Device::readGpsRoamingData.
///
/// REAL BUG FOUND AND FIXED 2026-08-09, live capture: the reference
/// project's own addressing (and this port's original, unconfirmed
/// <see cref="SecondHalfBias"/> = 0x10) is wrong. The 0x400-byte region
/// isn't 16 x 0x20-byte slots each holding 2 packed entries - it's two
/// SEPARATE 0x200-byte halves, each independently laid out as 16 entries
/// x 0x20-byte stride (only the first 0x10 bytes of each 32-byte stride
/// slot hold a real record; the other 16 are unused padding, confirmed
/// always zero). Entries 0-15 live in the first half at
/// <c>index * StrideBytes</c>; entries 16-31 live in the SECOND half,
/// 0x200 bytes further in, at <c>(index - 16) * StrideBytes</c> - i.e.
/// <see cref="SecondHalfBias"/> needed to be 0x200, not 0x10 (off by a
/// factor of 32). This bug was invisible until now because the test
/// radio's GPS Roaming was empty/never configured - it would have
/// corrupted data the moment write support shipped (writing entry 16's
/// record over entry 0's own bytes instead of its real slot). Confirmed
/// by writing a distinct real record to index 16 (row 17 in the vendor
/// CPS grid) and finding it at physical offset 0x200, with offset 0x10
/// reading back all-zero (unrelated padding, matching every other
/// unused half of a stride slot).
///
/// Also confirmed the same capture, planting index 0 (row 1) and index 16
/// (row 17) with distinct values:
/// - Enabled: 0=Off, 1=On (this port's own `data[0] != 0` check already
///   matched).
/// - ZoneIndex: a plain 0-based position into this app's own Zones list
///   (matches the same convention every other index-referencing field in
///   this app uses) - 255 is the "no zone" sentinel, shown as "Off" in
///   the vendor CPS's own Zone column for an unconfigured slot.
/// - LatDegree/LongDegree: plain bytes, confirmed exact (59/145 and
///   12/34 both round-tripped).
/// - LatMinute/LatMinuteDecimal (and Long-): the vendor CPS's edit popup
///   shows ONE "MM.mm" textbox for Latitude/Longitude Minute - the grid's
///   separate "LatiMinMark"/"LongiMinMark" columns are just that same
///   value's fractional half broken out for the grid. Confirmed exactly:
///   planted .34/.78/.23/.67 all came back byte-for-byte in
///   LatMinuteDecimal/LongMinuteDecimal.
/// - NorthSouth/EastWest: confirmed 0=N/E, 1=S/W (both directions planted
///   as S/W, both read back as 1; blank/default reads 0, matching "N"/"E"
///   shown for unconfigured-but-zeroed slots in the vendor grid).
/// - Radius: confirmed exactly 2 bytes at offset 12-13 (LE uint16, 0-65535)
///   - settles the reference project's own inconsistency (its encode()
///   writes 4 bytes here despite decode() only reading 2). Bytes 14-15
///   stayed zero in every real and blank record observed - not part of
///   Radius on this hardware. Confirmed by the vendor CPS's own blank-slot
///   sentinel too: unconfigured rows show Radius=65535 (0xFFFF) in the
///   grid, not a 32-bit max - consistent with a genuine 2-byte field.
/// - Bytes 10-11: still fully unknown purpose, but confirmed always zero
///   in both real and blank records - encoded as zero on write.
/// </summary>
public static class GpsRoamingCodec
{
    public const int RecordLength = 0x10;
    public const int EntryCount = 32;
    public const int StrideBytes = 0x20;

    /// <summary>See this class's own doc comment for the live-confirmed
    /// correction - 0x200 (half the 0x400-byte region), not 0x10.</summary>
    public const int SecondHalfBias = 0x200;

    /// <summary>Byte offset of this entry's 0x10-byte record within the
    /// single <see cref="D890UvMemoryMap.GpsRoamingDataLength"/>-byte block
    /// read for all 32 entries at once.</summary>
    public static int OffsetForIndex(int index) => (index % 16) * StrideBytes + (index >= 16 ? SecondHalfBias : 0);

    public static DecodedGpsRoaming Decode(ReadOnlySpan<byte> data, int index)
    {
        return new DecodedGpsRoaming(index)
        {
            Enabled = data[0] != 0,
            ZoneIndex = data[1],
            LatDegree = data[2],
            LatMinute = data[3],
            LatMinuteDecimal = data[4],
            NorthSouth = data[5],
            LongDegree = data[6],
            LongMinute = data[7],
            LongMinuteDecimal = data[8],
            EastWest = data[9],
            Radius = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(12, 2))
        };
    }

    public static byte[] Encode(DecodedGpsRoaming values)
    {
        var result = new byte[RecordLength];
        result[0] = (byte)(values.Enabled ? 1 : 0);
        result[1] = (byte)values.ZoneIndex;
        result[2] = (byte)values.LatDegree;
        result[3] = (byte)values.LatMinute;
        result[4] = (byte)values.LatMinuteDecimal;
        result[5] = (byte)values.NorthSouth;
        result[6] = (byte)values.LongDegree;
        result[7] = (byte)values.LongMinute;
        result[8] = (byte)values.LongMinuteDecimal;
        result[9] = (byte)values.EastWest;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12, 2), (ushort)values.Radius);
        return result;
    }

    public sealed record DecodedGpsRoaming(int Index)
    {
        public bool Enabled { get; init; }
        public int ZoneIndex { get; init; }
        public int LatDegree { get; init; }
        public int LatMinute { get; init; }
        public int LatMinuteDecimal { get; init; }
        public int NorthSouth { get; init; }
        public int LongDegree { get; init; }
        public int LongMinute { get; init; }
        public int LongMinuteDecimal { get; init; }
        public int EastWest { get; init; }
        public int Radius { get; init; }
    }
}
