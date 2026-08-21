using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder/encoder for the D890UV's Talkgroup Whitelist and Digital
/// Contact Whitelist (vendor CPS tree: "Repeater Whitelist" -> "Talk Group
/// WhiteList" / "Digital Contact WhiteList") - NOT a per-entry fixed stride
/// like every other entity here. It's a packed, contiguous stream of 16-byte
/// blocks at a single fixed address (one block per entity type, no block/
/// stride splitting the way Digital Contact List needs), where each entry
/// embeds its own "id" field rather than the index being implied by read
/// position. Transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (desktop/src/device.cpp
/// Device::readTalkgroupWhitelist - the confirmed D890UV code path, gated on
/// radio_model == D890UV_FW103 which is assigned for ANY radio identifying
/// as "ID890UV"/"V100", i.e. our exact hardware).
///
/// KNOWN DEVIATION FROM UPSTREAM: the reference's second-half branch reads
/// `dmr_id_b` from the same `data.mid(0x0, 4)` as the first half (apparent
/// copy-paste bug - re-checked twice against the actual source), which would
/// make every second entry in a block silently report the first entry's DMR
/// ID. This port uses bytes 0x8-0xC for the second half instead, which is
/// clearly the intended field given the layout symmetry. CONFIRMED CORRECT
/// live 2026-08-09 (see below) - both halves of a block decoded exactly the
/// two distinct planted DMR IDs.
///
/// Live-tested 2026-08-09 (both lists in one combined capture, add-only -
/// no radio write support existed yet at the time): planted TG/DMR IDs
/// 91101/91102/91121 (Talkgroup Whitelist) and 92101/92102/92121 (Digital
/// Contact Whitelist) into grid rows 1/2/21 respectively (rows 1+2 share a
/// block, row 21 lands past the OLD MaxBlocks=10 cap). Findings:
/// - DMR ID encoding (shift-by-1-then-BCD-hex-trick, see DecodeEntry) is
///   exactly as already assumed - all 6 values round-tripped byte-for-byte.
/// - CallType (packed into the DMR ID word's LSB, NOT a separate byte the
///   way Talkgroup/Digital Contact List have): every Talkgroup Whitelist
///   entry captured bit=1, every Digital Contact Whitelist entry captured
///   bit=0. The vendor CPS's own edit popup has no control for this field on
///   EITHER list (Digital Contact WhiteList doesn't even show a Call Type
///   column; Talk Group WhiteList shows one but it's always "Group Call",
///   never user-editable) - so this app fixes CallType at write time rather
///   than exposing it: 1 for Talkgroup Whitelist, 0 for Digital Contact
///   Whitelist (which likely isn't really a "call type" concept there at
///   all, just an unused reserved bit always 0).
/// - MAJOR FINDING: the per-entry "id" field is NOT the row number typed
///   into the vendor CPS grid - it's a tightly packed 0-based sequential
///   index. Entries typed into rows 1/2/21 were written at packed positions
///   0/1/2, not 0/1/20. The "No." column is purely a grid-typing
///   convenience; on write, the vendor CPS compacts every filled row down,
///   skipping gaps entirely. This app mirrors that: Number is auto-assigned
///   (index+1, read-only in the UI) rather than user-choosable, and
///   <see cref="EncodeAll"/> ignores whatever Id/Number an entry carries,
///   using its list position instead.
///
/// <see cref="MaxBlocks"/> raised from 10 (a guess made before the real
/// capacity was known) to 500 once the row-range convention
/// (<c>CodeplugLimits.WhitelistSlotMax</c> = 1000, a separate constant)
/// was confirmed load-bearing: 500 blocks x 2 entries/block = 1000
/// entries, and comfortably fits the 0x2000-byte (512-block) gap between
/// <see cref="D890UvMemoryMap.TalkgroupWhitelistData"/> and
/// <see cref="D890UvMemoryMap.DigitalContactWhitelistData"/>. Since entries
/// are packed with no gaps regardless of which row numbers were used to
/// enter them, this cap is what actually determines real capacity - not the
/// row numbers themselves.
/// </summary>
public static class TalkgroupWhitelistCodec
{
    public const int BlockLength = 0x10;
    public const int MaxBlocks = 500;

    public readonly record struct BlockResult(DecodedTalkgroupWhitelist? First, DecodedTalkgroupWhitelist? Second, bool StopReading);

    public static BlockResult DecodeBlock(ReadOnlySpan<byte> block)
    {
        DecodedTalkgroupWhitelist? first = null;
        if (!IsAllFF(block[..0x8]))
        {
            var id = (int)BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(0x4, 4));
            first = DecodeEntry(block[..0x4], id);
        }

        var secondHalfBlank = IsAllFF(block.Slice(0x8, 0x8));
        DecodedTalkgroupWhitelist? second = null;
        if (!secondHalfBlank)
        {
            var id = (int)BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(0xc, 4));
            second = DecodeEntry(block.Slice(0x8, 0x4), id);
        }

        // Matches the reference's own loop-termination condition exactly:
        // it only stops on a blank SECOND half, never on a blank first half.
        return new BlockResult(first, second, StopReading: secondHalfBlank);
    }

    /// <summary>Encodes the full whitelist as one contiguous
    /// <see cref="MaxBlocks"/> * <see cref="BlockLength"/>-byte region -
    /// real entries packed sequentially from position 0 (see this class's
    /// own doc comment: the vendor CPS itself ignores row-number gaps), the
    /// remainder 0xFF-filled to match the blank-slot sentinel
    /// <see cref="DecodeBlock"/> already expects. Always emits the FULL
    /// fixed-size region (never a partial/previous-length-aware write) since
    /// unlike Digital Contact List this region is small and fixed - always
    /// rewriting all of it is simpler and just as safe. Each entry's own
    /// Id/Number is ignored in favor of its list position, matching the
    /// confirmed packed-position behavior.</summary>
    public static byte[] EncodeAll(IReadOnlyList<DecodedTalkgroupWhitelist> entries)
    {
        var maxEntries = MaxBlocks * 2;
        if (entries.Count > maxEntries)
        {
            throw new InvalidOperationException(
                $"Whitelist has {entries.Count} entries, which spills past the {MaxBlocks}-block ({maxEntries}-entry) cap this app currently allows - refusing.");
        }

        var result = new byte[MaxBlocks * BlockLength];
        Array.Fill(result, (byte)0xff);
        for (var i = 0; i < entries.Count; i++)
        {
            var blockOffset = i / 2 * BlockLength + i % 2 * 8;
            EncodeEntry(entries[i], i, result.AsSpan(blockOffset, 8));
        }

        return result;
    }

    private static void EncodeEntry(DecodedTalkgroupWhitelist entry, int slotIndex, Span<byte> destination)
    {
        var shiftedBytes = BcdDecimalCodec.EncodeAsDecimal(entry.DmrId, 4);
        var shifted = BinaryPrimitives.ReadUInt32BigEndian(shiftedBytes);
        var dmrIdRaw = (shifted << 1) | (uint)(entry.CallType & 0x1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[..4], dmrIdRaw);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), (uint)slotIndex);
    }

    private static DecodedTalkgroupWhitelist DecodeEntry(ReadOnlySpan<byte> dmrIdBytes, int id)
    {
        var dmrIdRaw = BinaryPrimitives.ReadUInt32LittleEndian(dmrIdBytes);
        var callType = (int)(dmrIdRaw & 0x1);

        // `Int::toBytes(dmr_id_b >> 1, 4, Endian::Big).toHex().toInt()`: drop
        // the call-type bit, then re-serialize the remaining value as big-
        // endian bytes and BCD-decode them (same hex-string-as-decimal trick
        // used for frequencies/DMR IDs elsewhere in this codebase).
        var shifted = dmrIdRaw >> 1;
        Span<byte> bigEndian = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bigEndian, shifted);
        var dmrId = BcdDecimalCodec.DecodeAsDecimal(bigEndian);

        return new DecodedTalkgroupWhitelist(id) { DmrId = dmrId, CallType = callType };
    }

    private static bool IsAllFF(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (b != 0xff)
            {
                return false;
            }
        }

        return true;
    }

    public sealed record DecodedTalkgroupWhitelist(int Id)
    {
        public long DmrId { get; init; }
        public int CallType { get; init; }
    }
}
