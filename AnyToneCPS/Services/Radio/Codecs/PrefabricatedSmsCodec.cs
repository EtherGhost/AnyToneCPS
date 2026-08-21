using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for the D890UV's Prefabricated SMS entity. Two genuinely different
/// pieces, both transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps
/// (desktop/src/device.cpp Device::readPrefabricatedSms - the confirmed
/// D890UV code path):
///
/// 1. A linked-list "used slot" index, walked via <see cref="TryDecodeSetEntry"/> -
///    NOT a bitmap like most entities here. Starting at slot 0, each 0x10-byte
///    entry at <c>PrefabSmsSet + current*0x10</c> names the next slot to visit
///    (byte 2) and which of the 100 fixed SMS slots it represents (byte 3),
///    terminated by <see cref="EndMarker"/>. Max <see cref="MaxHops"/> hops,
///    matching the reference's own cycle-safety cap.
/// 2. The actual SMS text record, at a genuinely two-level address (a stride
///    within a physical block, physical blocks themselves strided) -
///    <see cref="ComputeAddress"/> - decoded as UTF-16LE via the shared
///    <see cref="TextFieldCodec"/> (the reference's own generic decode()
///    treats data as narrow/8-bit text unconditionally, which is wrong for
///    the D890UV exactly like the channel/zone name bug fixed earlier this
///    project - not ported here, TextFieldCodec.DecodeName used instead).
///
/// Both write behaviors confirmed 2026-08-03 via a live differential write
/// on a real D890UV: adding SMS #6 with a 100-character text
/// (the real vendor CPS limit, independently confirmed - 100 characters
/// exactly produced 200 bytes of text with the remaining 216 bytes of the
/// 416-byte record zero-padded) wrote the text at exactly the address
/// <see cref="ComputeAddress"/> computes, AND rewrote the entire set chain
/// as nodes 0-5 in sequential id order (previously nodes 0-4 for slots 1-5,
/// terminated at node 4; after the add, node 4's `next` was changed from
/// the end marker to 5, and a new terminal node 5 was appended) - the whole
/// chain gets rewritten on any change, not just a single patched node,
/// matching RadioCodeplugPatcher.ApplyPrefabricatedSmsSetChain's per-node
/// use of EncodeSetNode below.
/// </summary>
public static class PrefabricatedSmsCodec
{
    public const int SetEntryLength = 0x10;
    public const int MaxHops = 100;
    public const byte EndMarker = 0xff;
    public const int SlotCount = 100;

    /// <summary>Parses one 0x10-byte linked-list node from the "set" region.
    /// Returns false if the entry is too short to contain the fields
    /// (matches the reference's own defensive `entry.size() < 4` check).</summary>
    public static bool TryDecodeSetEntry(ReadOnlySpan<byte> entry, out byte next, out byte id)
    {
        if (entry.Length < 4)
        {
            next = EndMarker;
            id = EndMarker;
            return false;
        }

        next = entry[2];
        id = entry[3];
        return true;
    }

    /// <summary>Address of the SlotIndex-th SMS text record, given the
    /// D890UV's stride/block constants from <see cref="D890UvMemoryMap"/>.</summary>
    public static int ComputeAddress(int slotIndex)
    {
        var byteOffset = slotIndex * D890UvMemoryMap.PrefabSmsDataOffset;
        var blockIndex = byteOffset / D890UvMemoryMap.PrefabSmsDataBlockSize;
        var offsetInBlock = byteOffset % D890UvMemoryMap.PrefabSmsDataBlockSize;
        return D890UvMemoryMap.PrefabSmsData + blockIndex * D890UvMemoryMap.PrefabSmsDataBlockOffset + offsetInBlock;
    }

    public static DecodedPrefabricatedSms Decode(ReadOnlySpan<byte> data, int slotIndex)
    {
        return new DecodedPrefabricatedSms(slotIndex) { Text = TextFieldCodec.DecodeName(data) };
    }

    public sealed record DecodedPrefabricatedSms(int SlotIndex)
    {
        public string Text { get; init; } = "";
    }

    /// <summary>Encodes the SMS text into a full PrefabSmsDataLength-byte
    /// record, matching <see cref="Decode"/>'s own slicing.</summary>
    public static byte[] Encode(string text) => TextFieldCodec.EncodeName(text, D890UvMemoryMap.PrefabSmsDataLength);

    /// <summary>Encodes the ENTIRE used-slot linked list as nodes 0..count-1
    /// in sequential order - confirmed 2026-08-03 via a live differential
    /// write that the whole chain gets rewritten on any change, not just a
    /// single patched node (there's no way to patch a mid-chain link
    /// without already knowing the full current chain anyway). Encodes ONE
    /// node at a time (rather than the whole chain as one combined block) -
    /// each node is independently addressed/captured (mirroring how the
    /// walk visits and captures them one at a time, see
    /// RadioCodeplugRawSnapshot.CapturePrefabricatedSms), not one
    /// contiguous region, so RadioCodeplugPatcher.ApplyPrefabricatedSmsSetChain
    /// patches each node's own address separately using this.</summary>
    public static byte[] EncodeSetNode(byte next, byte id)
    {
        var node = new byte[SetEntryLength];
        node[2] = next;
        node[3] = id;
        return node;
    }
}
