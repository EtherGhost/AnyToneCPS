using System;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder/encoder for a single D890UV Roaming Zone record (0x80 bytes).
/// Byte layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (roamingzone.cpp, decode_D890UV),
/// cross-validated against real hardware USB captures (that
/// reference's channel-name-field offset exactly matched an independent
/// USB capture, giving high confidence in the rest of its transcribed
/// layouts too). FULLY confirmed 2026-08-10 via a live
/// differential write capture: an existing zone (4 members) had its Name
/// changed to "ZTEST1" and its members reordered to CH3/CH1/CH4/CH2 -
/// the capture showed bytes 0x00-0x03 as 02 00 03 01 (exactly those
/// 0-based radio indices, in that exact order - confirming both the
/// 1-byte-per-slot layout AND that slot order is preserved, not sorted)
/// and the Name field at 0x40 as UTF-16LE "ZTEST1". Bytes 0x60-0x7F were
/// all zero in that capture and are left untouched by <see cref="Encode"/>
/// (unknown/reserved, same treatment as RoamingChannelCodec's own tail).
/// </summary>
public static class RoamingZoneCodec
{
    public const int RecordLength = D890UvMemoryMap.RoamingZoneDataLength; // 0x80

    private const int ChannelSlotCount = 64;
    private const byte EmptySlot = 0xFF;
    private const int NameFieldOffset = 0x40;
    private const int NameFieldLength = 0x20;

    public static DecodedRoamingZone Decode(ReadOnlySpan<byte> data, int index)
    {
        // Unlike most index lists here, this is NOT uint16 indices - it's
        // one raw byte per slot (roaming zones can reference at most 64
        // roaming channels, indexed 0-254; 0xFF = empty slot).
        var channelIndexes = new List<int>(ChannelSlotCount);
        for (var i = 0; i < ChannelSlotCount; i++)
        {
            var slotValue = data[i];
            if (slotValue != EmptySlot)
            {
                channelIndexes.Add(slotValue);
            }
        }

        return new DecodedRoamingZone(index)
        {
            RoamingChannelIndexes = channelIndexes,
            Name = TextFieldCodec.DecodeName(data.Slice(NameFieldOffset, NameFieldLength))
        };
    }

    /// <summary>Inverse of <see cref="Decode"/> - see this class's own doc
    /// comment for the live-capture confirmation. Bytes 0x60-0x7f (beyond
    /// the Name field) are left untouched.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedRoamingZone values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Roaming zone record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        EncodeChannelMembers(values.RoamingChannelIndexes).CopyTo(result, 0);
        TextFieldCodec.EncodeName(values.Name, NameFieldLength).CopyTo(result, NameFieldOffset);

        return result;
    }

    /// <summary>Encodes the full 64-slot channel-membership record, one raw
    /// byte per slot (see this class's own doc comment). Unused slots are
    /// filled with <see cref="EmptySlot"/> (0xFF), matching what
    /// <see cref="Decode"/> treats as "no channel here". Order is preserved
    /// exactly - confirmed by the live capture NOT to be sorted.</summary>
    private static byte[] EncodeChannelMembers(IReadOnlyList<int> members)
    {
        if (members.Count > ChannelSlotCount)
        {
            throw new ArgumentException($"Roaming zone can hold at most {ChannelSlotCount} member channels, got {members.Count}.", nameof(members));
        }

        var result = new byte[ChannelSlotCount];
        result.AsSpan().Fill(EmptySlot);
        for (var i = 0; i < members.Count; i++)
        {
            result[i] = (byte)members[i];
        }

        return result;
    }

    public sealed record DecodedRoamingZone(int Index)
    {
        public IReadOnlyList<int> RoamingChannelIndexes { get; init; } = [];
        public string Name { get; init; } = "";
    }
}
