using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for a single D890UV Scan List record (0xd0 bytes). Byte
/// layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (scanlist.cpp, decode_D890UV). The
/// original "channel-name-field offset match gives high confidence in the
/// rest" claim here turned out to be too optimistic: a 2026-07-19 live
/// differential test (real vendor CPS, 4 timing fields changed together)
/// found LookbackTimeA/B and DropoutDelayTime/DwellTime's ported "- 5"/"- 1"
/// offset was simply wrong - the byte positions were right, but the value
/// transform wasn't. Every field's address and encoding is now confirmed
/// against that same test (Name, PriorityChannelSelect/1/2, the 4 timing
/// fields, ChannelMemberIndexes) - see this class's Decode/Encode doc
/// comments for specifics. A second live capture on 2026-08-02 (a
/// brand-new scan list added from scratch in vendor CPS) found a further
/// bug in the callers, not this codec: PriorityChannel1/2's raw value is
/// the 1-based channel number, while ChannelMemberIndexes is a 0-based
/// radio index - the two callers (RadioReadMapper, MainViewModel.RadioWrite)
/// had been treating both the same way. Fixed there; see PriorityChannel1's
/// doc comment below. NOTE: this app only targets the D890UV - the
/// reference project's D878UVII variant uses different name-field offsets
/// and is intentionally ignored here.
/// </summary>
public static class ScanListCodec
{
    public const int RecordLength = D890UvMemoryMap.ScanListDataLength; // 0xd0

    private const ushort EmptyChannelSlot = 0xFFFF;
    private const int ChannelMemberSlotCount = 50;

    public static DecodedScanList Decode(ReadOnlySpan<byte> data, int index)
    {
        var priorityChannel1Raw = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x02, 2));
        var priorityChannel2Raw = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x04, 2));

        var members = new List<int>(ChannelMemberSlotCount);
        for (var i = 0; i < ChannelMemberSlotCount; i++)
        {
            var offset = 0x30 + i * 2;
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
            if (value != EmptyChannelSlot)
            {
                members.Add(value);
            }
        }

        return new DecodedScanList(index)
        {
            PriorityChannelSelect = data[0x01],
            // 0xFFFF means "none" for both priority channel slots. The raw
            // value itself is the 1-based channel NUMBER (not a 0-based
            // radio index like ChannelMemberIndexes below) - confirmed
            // 2026-08-02 via a live capture of a brand-new scan list add in
            // vendor CPS: Priority Channel 1 set to the 3rd programmed
            // channel wrote raw 3, not 2. Callers must convert to/from a
            // 0-based radio index themselves (see RadioReadMapper.MapScanLists
            // and MainViewModel.RadioWrite.cs's BuildSafeScanListValues).
            PriorityChannel1 = priorityChannel1Raw == EmptyChannelSlot ? null : priorityChannel1Raw,
            PriorityChannel2 = priorityChannel2Raw == EmptyChannelSlot ? null : priorityChannel2Raw,
            // Raw uint16 is tenths of a second, no offset - confirmed
            // 2026-07-19 via a live differential test (real vendor CPS,
            // 4 fields changed together: 2.5/3.7/4.4/3.5s produced raw
            // 25/37/44/35 exactly). The reference project's `- 5`/`- 1`
            // this was originally ported from was wrong - it happened to
            // still look like a plausible number for pre-existing/default
            // values, which is why it went unnoticed until real values were
            // set and cross-checked against the vendor CPS's own display.
            LookbackTimeA = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x06, 2)),
            LookbackTimeB = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x08, 2)),
            DropoutDelayTime = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0a, 2)),
            DwellTime = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0c, 2)),
            Name = TextFieldCodec.DecodeName(data.Slice(0x0e, 0x20)),
            ChannelMemberIndexes = members,
            RevertChannel = data[0x94]
        };
    }

    public sealed record DecodedScanList(int Index)
    {
        public int PriorityChannelSelect { get; init; }
        public int? PriorityChannel1 { get; init; }
        public int? PriorityChannel2 { get; init; }

        /// <summary>Raw wire value in tenths of a second (e.g. 25 = 2.5s) -
        /// confirmed 2026-07-19 via live differential test. See ScanListEntry's
        /// LookbackTimeAText for the human-facing decimal-seconds conversion.</summary>
        public int LookbackTimeA { get; init; }

        /// <summary>Same units as <see cref="LookbackTimeA"/>.</summary>
        public int LookbackTimeB { get; init; }

        /// <summary>Same units as <see cref="LookbackTimeA"/>.</summary>
        public int DropoutDelayTime { get; init; }

        /// <summary>Same units as <see cref="LookbackTimeA"/>.</summary>
        public int DwellTime { get; init; }
        public string Name { get; init; } = "";
        public IReadOnlyList<int> ChannelMemberIndexes { get; init; } = [];
        public int RevertChannel { get; init; }
    }

    /// <summary>
    /// Encodes every known field from <paramref name="values"/> into a copy
    /// of <paramref name="currentRecord"/>, leaving only the unknown tail
    /// (0x95-0xcf, past RevertChannel) untouched. Unlike ChannelCodec's
    /// nullable-per-field patch, this always rewrites every field
    /// unconditionally rather than tracking per-field dirtiness at the codec
    /// level - safe here because, unlike Channel, none of this record's
    /// fields share bytes/bits with each other (see this class's doc
    /// comment), so writing an unchanged field back verbatim can never
    /// corrupt a sibling field the way a bit-packed byte could. The caller
    /// (MainViewModel.RadioWrite.cs) still only calls this at all when at
    /// least one field is actually dirty.
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedScanList values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Scan list record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();

        result[0x01] = (byte)values.PriorityChannelSelect;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x02, 2), values.PriorityChannel1 is { } priorityChannel1 ? (ushort)priorityChannel1 : EmptyChannelSlot);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x04, 2), values.PriorityChannel2 is { } priorityChannel2 ? (ushort)priorityChannel2 : EmptyChannelSlot);
        // Raw tenths-of-a-second, no offset - see Decode's doc comment.
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x06, 2), (ushort)values.LookbackTimeA);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x08, 2), (ushort)values.LookbackTimeB);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x0a, 2), (ushort)values.DropoutDelayTime);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x0c, 2), (ushort)values.DwellTime);
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x0e);
        EncodeChannelMembers(values.ChannelMemberIndexes).CopyTo(result, 0x30);
        result[0x94] = (byte)values.RevertChannel;

        return result;
    }

    /// <summary>Encodes the full 50-slot channel-membership record. Unused
    /// slots are filled with <see cref="EmptyChannelSlot"/> (0xFFFF), matching
    /// what <see cref="Decode"/> treats as "no channel here".</summary>
    private static byte[] EncodeChannelMembers(IReadOnlyList<int> members)
    {
        if (members.Count > ChannelMemberSlotCount)
        {
            throw new ArgumentException($"Scan list can hold at most {ChannelMemberSlotCount} member channels, got {members.Count}.", nameof(members));
        }

        var result = new byte[ChannelMemberSlotCount * 2];
        result.AsSpan().Fill(0xFF);
        for (var i = 0; i < members.Count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(i * 2, 2), (ushort)members[i]);
        }

        return result;
    }
}
