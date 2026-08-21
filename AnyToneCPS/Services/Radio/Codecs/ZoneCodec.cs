using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for D890UV zone data. Unlike channels, a zone is NOT one
/// contiguous record - it is assembled from 4 separate parallel arrays
/// (name, channel-membership list, A-channel index, B-channel index) plus
/// a hide-flag bitmap, all keyed by zone index. The orchestrator reads
/// each region ONCE for all zones and hands slices here per zone index.
///
/// Every one of this class's Encode* functions (and every address
/// <see cref="RadioCodeplugPatcher.ApplyZonePatch"/> computes from them) was
/// confirmed write-safe via a live differential test 2026-07-19 - a real USB
/// capture of the vendor CPS writing to a real D890UV, changing
/// Name, A-Channel, B-Channel, adding a channel to Membership, and setting
/// Hide, all independently verified byte-for-byte (including the write
/// protocol's checksum) against this codec's own encode output. The byte
/// OFFSETS/write mechanics this test covered are still correct, but its
/// interpretation of what <see cref="DecodeAChannelIndex"/>/
/// <see cref="DecodeBChannelIndex"/>'s VALUE means was wrong - see
/// <c>RadioReadMapper.MapZones</c>'s 2026-08-01 doc comment: it's a 0-based
/// position within the zone's own member list, not a global radio channel
/// index. The 2026-07-19 test zone's members happened to be numbered in
/// exact position order, which is why it didn't catch this.
/// </summary>
public static class ZoneCodec
{
    /// <summary>Fixed at 256 - the full 0x200-byte membership region is
    /// 256 x uint16 slots. Confirmed 2026-07-19 by 3 independent sources
    /// after this constant was found set to 128 (silently truncating any
    /// zone with more than 128 members on read): dmrconfig's
    /// betweenZoneChannels()=0x200 and qdmr's zoneChannels() both document
    /// the same 0x200 stride, and the xbenkozx/anytone-cps reference
    /// project's own zone-read loop (device.cpp, kZoneChannelsBytes=0x200)
    /// iterates the entire region rather than stopping halfway. The vendor
    /// CPS help text itself doesn't give a number ("Zones do not have a
    /// limit of 16 channels on this radio" - a removed old restriction, not
    /// a claim of unlimited storage), so 256 is the physical capacity, not
    /// a documented vendor figure.</summary>
    private const int ChannelMemberSlotCount = 256;
    private const ushort EmptyChannelSlot = 0xFFFF;

    public static string DecodeName(ReadOnlySpan<byte> nameRegionBytes, int idx)
    {
        var offset = idx * D890UvMemoryMap.ZoneDataOffset;
        var slice = nameRegionBytes.Slice(offset, D890UvMemoryMap.ZoneDataLength);
        return TextFieldCodec.DecodeName(slice);
    }

    public static IReadOnlyList<ushort> DecodeChannelMembers(ReadOnlySpan<byte> channelsRegionBytes, int idx)
    {
        const int regionSize = 0x200;
        var offset = idx * regionSize;
        var slice = channelsRegionBytes.Slice(offset, regionSize);

        var members = new List<ushort>(ChannelMemberSlotCount);
        for (var slot = 0; slot < ChannelMemberSlotCount; slot++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(slice.Slice(slot * 2, 2));
            if (value != EmptyChannelSlot)
            {
                members.Add(value);
            }
        }

        return members;
    }

    public static ushort DecodeAChannelIndex(ReadOnlySpan<byte> aChannelRegionBytes, int idx) =>
        BinaryPrimitives.ReadUInt16LittleEndian(aChannelRegionBytes.Slice(idx * 2, 2));

    public static ushort DecodeBChannelIndex(ReadOnlySpan<byte> bChannelRegionBytes, int idx) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bChannelRegionBytes.Slice(idx * 2, 2));

    public static bool DecodeHide(ReadOnlySpan<byte> hideRegionBytes, int idx) =>
        (hideRegionBytes[idx / 8] & (1 << (idx % 8))) != 0;

    /// <summary>Encodes a zone name into its own dedicated 0x20-byte record -
    /// unlike Channel, a zone's fields are 4 separate parallel arrays with no
    /// shared bytes/bits, so each field can be encoded/patched completely
    /// independently (no bit-sharing risk like Channel's packed byte
    /// fields).</summary>
    public static byte[] EncodeName(string name) => TextFieldCodec.EncodeName(name, D890UvMemoryMap.ZoneDataLength);

    /// <summary>Encodes the full 256-slot channel-membership record. Unused
    /// slots are filled with <see cref="EmptyChannelSlot"/> (0xFFFF), matching
    /// what <see cref="DecodeChannelMembers"/> treats as "no channel here".</summary>
    public static byte[] EncodeChannelMembers(IReadOnlyList<ushort> members)
    {
        if (members.Count > ChannelMemberSlotCount)
        {
            throw new ArgumentException($"Zone can hold at most {ChannelMemberSlotCount} member channels, got {members.Count}.", nameof(members));
        }

        var result = new byte[ChannelMemberSlotCount * 2];
        result.AsSpan().Fill(0xFF);
        for (var i = 0; i < members.Count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(i * 2, 2), members[i]);
        }

        return result;
    }

    /// <summary>Encodes a single A/B-channel index slot (2 bytes) - both
    /// fields share this same shape, just at different base addresses.</summary>
    public static byte[] EncodeChannelIndex(ushort value)
    {
        var result = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(result, value);
        return result;
    }

    /// <param name="nameAndChannelsAlreadySliced">When true, <paramref name="nameRegionBytes"/>
    /// and <paramref name="channelsRegionBytes"/> are already exactly this one
    /// zone's bytes (read directly at this zone's address, not a shared region
    /// spanning all zones) - so they're decoded at relative offset 0 instead of
    /// <c>idx * stride</c>. <paramref name="aChannelRegionBytes"/>/
    /// <paramref name="bChannelRegionBytes"/>/<paramref name="hideRegionBytes"/>
    /// are always the full multi-zone regions regardless, since those are cheap
    /// to batch-read for every possible zone slot up front.</param>
    public static DecodedZone Decode(
        int idx,
        ReadOnlySpan<byte> nameRegionBytes,
        ReadOnlySpan<byte> channelsRegionBytes,
        ReadOnlySpan<byte> aChannelRegionBytes,
        ReadOnlySpan<byte> bChannelRegionBytes,
        ReadOnlySpan<byte> hideRegionBytes,
        bool nameAndChannelsAlreadySliced = false)
    {
        return new DecodedZone(idx)
        {
            Name = DecodeName(nameRegionBytes, nameAndChannelsAlreadySliced ? 0 : idx),
            ChannelMembers = DecodeChannelMembers(channelsRegionBytes, nameAndChannelsAlreadySliced ? 0 : idx),
            AChannelIndex = DecodeAChannelIndex(aChannelRegionBytes, idx),
            BChannelIndex = DecodeBChannelIndex(bChannelRegionBytes, idx),
            IsHidden = DecodeHide(hideRegionBytes, idx)
        };
    }

    public sealed record DecodedZone(int Index)
    {
        public string Name { get; init; } = "";
        public IReadOnlyList<ushort> ChannelMembers { get; init; } = [];
        public ushort AChannelIndex { get; init; }
        public ushort BChannelIndex { get; init; }
        public bool IsHidden { get; init; }
    }

    /// <summary>Null fields are left untouched by <see cref="RadioCodeplugPatcher.ApplyZonePatch"/> -
    /// same nullable-per-field convention as <see cref="ChannelCodec.ChannelFieldPatch"/>,
    /// but unlike Channel there's no bit-sharing to worry about here, since
    /// each field already lives in its own dedicated array/bitmap.</summary>
    public sealed record ZoneFieldPatch
    {
        public string? Name { get; init; }
        public IReadOnlyList<ushort>? ChannelMembers { get; init; }
        public ushort? AChannelIndex { get; init; }
        public ushort? BChannelIndex { get; init; }
        public bool? IsHidden { get; init; }
    }
}
