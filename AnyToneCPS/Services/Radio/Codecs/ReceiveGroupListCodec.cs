using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for a single D890UV Receive Group List record (0x120 bytes). Byte
/// layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (receivegrouplist.cpp, decode_D890UV),
/// cross-validated against real hardware USB captures (that
/// reference's channel-name-field offset exactly matched an independent
/// USB capture, giving high confidence in the rest of its transcribed
/// layouts too).
///
/// Write side confirmed 2026-08-08 via a live differential write capture
/// (real vendor CPS, 2-member list): member indexes are the same 0-based
/// "Number - 1" radio index already used for Channel.ContactIndex (Talkgroup
/// No. 1/No. 2 -&gt; indexes 0/1), stored in ascending index order regardless
/// of the order they were added in the vendor CPS UI (which itself re-sorts
/// on add), terminated by 0xFFFFFFFF with every remaining slot up to the
/// full 64-slot capacity filled with 0xFF - not zero. The Name field wrote
/// back byte-for-byte what Decode already expected (UTF-16LE, null-
/// terminated, zero-padded), confirming this codec's existing shared
/// TextFieldCodec usage needed no changes for write support.
/// </summary>
public static class ReceiveGroupListCodec
{
    public const int RecordLength = D890UvMemoryMap.ReceiveGroupDataLength; // 0x120

    private const int TalkgroupSlotCount = 64;
    private const uint EndOfListMarker = 0xFFFFFFFF;

    public static DecodedReceiveGroupList Decode(ReadOnlySpan<byte> data, int index)
    {
        // Talkgroup index list is 32-bit (unlike most other index lists in
        // this codeplug, which are 16-bit). Reference:
        // `if (idx == 0xffffffff) break;` - a hard stop, not a skip.
        var talkgroupIndexes = new List<long>(TalkgroupSlotCount);
        for (var i = 0; i < TalkgroupSlotCount; i++)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i * 4, 4));
            if (value == EndOfListMarker)
            {
                break;
            }

            talkgroupIndexes.Add(value);
        }

        return new DecodedReceiveGroupList(index)
        {
            TalkgroupIndexes = talkgroupIndexes,
            Name = TextFieldCodec.DecodeName(data.Slice(0x100, 0x20))
        };
    }

    public sealed record DecodedReceiveGroupList(int Index)
    {
        public IReadOnlyList<long> TalkgroupIndexes { get; init; } = [];
        public string Name { get; init; } = "";
    }

    /// <summary>
    /// Encodes a full receive group list record from scratch - unlike
    /// ScanListCodec.Encode, there is no unknown tail to preserve from
    /// <paramref name="currentRecord"/> (the talkgroup index list plus the
    /// Name field account for all 0x120 bytes exactly), so currentRecord is
    /// only used to validate the caller passed the right record length.
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedReceiveGroupList values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Receive group list record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        if (values.TalkgroupIndexes.Count > TalkgroupSlotCount)
        {
            throw new ArgumentException($"Receive group list can hold at most {TalkgroupSlotCount} member talkgroups, got {values.TalkgroupIndexes.Count}.", nameof(values));
        }

        var result = new byte[RecordLength];
        result.AsSpan().Fill(0xFF);
        for (var i = 0; i < values.TalkgroupIndexes.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4, 4), (uint)values.TalkgroupIndexes[i]);
        }

        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x100);

        return result;
    }
}
