using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for a single D890UV AM Zone record, 0x80 bytes. Byte layout
/// transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (am_zone.cpp, decode_D890UV +
/// desktop/src/device.cpp Device::readAmZones for the AChannel/scan-channel
/// handling that lives outside decode_D890UV).
///
/// AChannelIndex and ScanChannelIndexes both live OUTSIDE this 0x80-byte
/// record, in their own separate flat per-zone regions (D890UvMemoryMap.
/// AmZoneAChannel/AmZoneScan) - Encode below only covers Name/
/// MemberChannelIndexes, which do share this one record; the other two are
/// encoded by their own small helpers and patched at their own addresses by
/// RadioCodeplugPatcher.ApplyAmZonePatch, same multi-region pattern as
/// ZoneCodec/ApplyZonePatch.
///
/// Every field's write behavior confirmed 2026-08-02 via two live
/// differential writes on a real D890UV: (1) adding AM CH
/// 001/002 (0-based AM Air radio indexes 0/1) to a zone's "Zone Scan
/// Channel Member" list produced raw byte 0x03 at AmZoneScan (bits 0+1 set)
/// - confirming ScanChannelIndexes is a 128-bit BITMASK, not an index list
/// like MemberChannelIndexes; (2) renaming the zone, changing A Channel
/// (raw 0 -> 2), removing a member (AM CH 010, radio index 9, correctly
/// dropped and the list re-packed), and removing one scan-channel bit
/// (0x03 -> 0x01) all independently verified byte-for-byte against this
/// codec's own Decode output. The reference project's own AmZoneScan
/// address guess turned out to be right; its apparent out-of-bounds bug was
/// in how its C++ code sliced its own already-fetched buffer before
/// indexing into it, unrelated to whether the address itself was correct.
/// </summary>
public static class AmZoneCodec
{
    public const int RecordLength = 0x80;
    private const int MemberChannelCount = 0x40 / 2; // 32 uint16 slots
    private const ushort EmptyChannelSlot = 0xFFFF;

    /// <summary>128 bits (D890UvMemoryMap.AmZoneScanLength * 8) - the scan-
    /// channel bitmask can only reference AM Air radio indexes 0-127, unlike
    /// the regular member list's full 0-255 range. A real hardware
    /// limitation, not a bug - callers restricting the "available" list for
    /// this field must cap at this.</summary>
    public const int ScanChannelBitCount = 128;

    public static DecodedAmZone Decode(ReadOnlySpan<byte> data, int aChannelIndex, ReadOnlySpan<byte> scanChannelBitmask, int index)
    {
        var name = TextFieldCodec.DecodeName(data[..0x20]);

        var memberChannelIndexes = new List<int>(MemberChannelCount);
        for (var i = 0; i < MemberChannelCount; i++)
        {
            var channelIndex = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x22 + i * 2, 2));
            if (channelIndex < AmAirCodec.VfoIndex + 1)
            {
                memberChannelIndexes.Add(channelIndex);
            }
        }

        var scanChannelIndexes = new List<int>();
        for (var bit = 0; bit < scanChannelBitmask.Length * 8; bit++)
        {
            if ((scanChannelBitmask[bit / 8] & (1 << (bit % 8))) != 0)
            {
                scanChannelIndexes.Add(bit);
            }
        }

        return new DecodedAmZone(index)
        {
            Name = name,
            AChannelIndex = aChannelIndex,
            MemberChannelIndexes = memberChannelIndexes,
            ScanChannelIndexes = scanChannelIndexes
        };
    }

    public sealed record DecodedAmZone(int Index)
    {
        public string Name { get; init; } = "";
        public int AChannelIndex { get; init; }
        public IReadOnlyList<int> MemberChannelIndexes { get; init; } = [];

        /// <summary>0-based AM Air radio indexes, 0-127 only - see this
        /// class's doc comment for the confirmed bitmask format.</summary>
        public IReadOnlyList<int> ScanChannelIndexes { get; init; } = [];
    }

    /// <summary>Encodes Name/MemberChannelIndexes into a copy of
    /// <paramref name="currentRecord"/> - both fields are unconditionally
    /// re-encoded (no bit-sharing between them, same reasoning as
    /// ScanListCodec.Encode's doc comment). AChannelIndex/ScanChannelIndexes
    /// are NOT part of this record - see this class's doc comment.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedAmZone values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"AM Zone record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x00);
        EncodeChannelMembers(values.MemberChannelIndexes).CopyTo(result, 0x22);
        return result;
    }

    /// <summary>Encodes the 32-slot channel-membership block (0x40 bytes).
    /// Unused slots are filled with <see cref="EmptyChannelSlot"/> (0xFFFF),
    /// matching what <see cref="Decode"/> treats as "no channel here".</summary>
    private static byte[] EncodeChannelMembers(IReadOnlyList<int> members)
    {
        if (members.Count > MemberChannelCount)
        {
            throw new ArgumentException($"AM Zone can hold at most {MemberChannelCount} member channels, got {members.Count}.", nameof(members));
        }

        var result = new byte[MemberChannelCount * 2];
        result.AsSpan().Fill(0xFF);
        for (var i = 0; i < members.Count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(i * 2, 2), (ushort)members[i]);
        }

        return result;
    }

    /// <summary>Encodes the separate 2-byte AChannel slot - same shape as
    /// ZoneCodec.EncodeChannelIndex.</summary>
    public static byte[] EncodeAChannelIndex(int value)
    {
        var result = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)value);
        return result;
    }

    /// <summary>Encodes the separate 16-byte (128-bit) scan-channel bitmask.
    /// Throws if any index is out of the confirmed 0-127 range - see
    /// <see cref="ScanChannelBitCount"/>.</summary>
    public static byte[] EncodeScanChannelBitmask(IReadOnlyList<int> scanChannelIndexes)
    {
        var result = new byte[D890UvMemoryMap.AmZoneScanLength];
        foreach (var index in scanChannelIndexes)
        {
            if (index < 0 || index >= ScanChannelBitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(scanChannelIndexes), index, $"Scan channel bitmask can only reference AM Air radio indexes 0-{ScanChannelBitCount - 1}.");
            }

            result[index / 8] |= (byte)(1 << (index % 8));
        }

        return result;
    }
}
