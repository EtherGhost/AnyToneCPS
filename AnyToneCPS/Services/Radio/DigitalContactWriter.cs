using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Dedicated write path for the Digital Contact database - deliberately NOT
/// part of the <see cref="RadioCodeplugRawSnapshot"/>/<see cref="RadioCodeplugPatcher"/>/
/// <see cref="RadioCodeplugWriter"/> pipeline every other entity uses. That
/// pipeline assumes a fixed-address, already-captured byte region to patch
/// in place; Digital Contact data is a variable-length stream with no fixed
/// per-record stride, and (being the one opt-in read in the app) is never
/// part of the cached snapshot in the first place. Confirmed safe via 3 live
/// differential write captures 2026-08-09 - see <see cref="DigitalContactCodec"/>'s
/// own doc comment for the byte-level findings this mirrors: the vendor CPS
/// itself always rewrites the WHOLE stream from the start on every write, so
/// this does the same rather than attempting any narrower patch.
///
/// Multi-block writes added 2026-08-09, same day - chunks the buffer at
/// <see cref="DigitalContactCodec.BlockLength"/> boundaries and translates
/// each chunk's physical address via <see cref="DigitalContactCodec.LogicalToPhysicalAddress"/>,
/// the same formula <see cref="DigitalContactCodec.DecodeAll"/> already uses
/// for reads. Capped at <see cref="DigitalContactCodec.MaxBlocks"/> - see
/// that constant's own doc comment for why.
///
/// Confirmed live 2026-08-09 crossing into block 1 (1968 contacts, ~204KB):
/// the Meta end-address field IS a physical, block/stride-translated
/// address (matching <see cref="DigitalContactCodec.EncodeMeta"/>'s
/// corrected formula) - the naive "DigitalContactData + logical byte
/// count" this class originally used was wrong beyond block 0, just never
/// distinguishable from the correct formula until data actually crossed a
/// block boundary. <see cref="ReadPreviousTotalBytes"/> now uses
/// <see cref="DigitalContactCodec.PhysicalToLogicalAddress"/> to match.
/// </summary>
public static class DigitalContactWriter
{
    private const int MetaLength = 0x10;
    private const int MemoryBlockLength = 0x10;

    public static void Write(IRadioConnection connection, IReadOnlyList<DigitalContactCodec.DecodedDigitalContact> contacts)
    {
        var previousTotalBytes = ReadPreviousTotalBytes(connection);
        var encoded = DigitalContactCodec.EncodeAll(contacts);

        // Zero-fill any space the previous (longer) list used but the new
        // one doesn't - confirmed live 2026-08-09 (delete round): the
        // vendor CPS actively clears freed space rather than leaving stale
        // bytes behind.
        var writeLength = Math.Max(encoded.Length, previousTotalBytes);
        writeLength = (writeLength + MemoryBlockLength - 1) / MemoryBlockLength * MemoryBlockLength;
        var maxBytes = DigitalContactCodec.BlockLength * DigitalContactCodec.MaxBlocks;
        if (writeLength > maxBytes)
        {
            throw new InvalidOperationException(
                $"Digital contact write would be {writeLength} bytes, which spills past the {DigitalContactCodec.MaxBlocks}-block ({maxBytes}-byte) cap this app currently allows - refusing.");
        }

        var buffer = new byte[writeLength];
        encoded.CopyTo(buffer, 0);
        WriteLogicalBytes(connection, buffer);

        var meta = DigitalContactCodec.EncodeMeta(contacts.Count, encoded.Length);
        connection.WriteMemory(D890UvMemoryMap.DigitalContactMeta, meta);
    }

    /// <summary>Writes <paramref name="data"/> (starting at logical offset
    /// 0) to the radio, splitting at each <see cref="DigitalContactCodec.BlockLength"/>
    /// boundary since consecutive blocks are NOT contiguous in physical
    /// memory (<see cref="DigitalContactCodec.BlockStride"/> &gt; BlockLength).
    /// Every chunk stays a multiple of 16 bytes (WriteMemory's own
    /// requirement) because BlockLength itself is - confirmed by
    /// <c>0x30d40 % 0x10 == 0</c>.</summary>
    private static void WriteLogicalBytes(IRadioConnection connection, byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var addrModAtStart = offset % DigitalContactCodec.BlockLength;
            var bytesLeftInBlock = DigitalContactCodec.BlockLength - addrModAtStart;
            var chunkLength = Math.Min(bytesLeftInBlock, data.Length - offset);
            var physicalAddress = DigitalContactCodec.LogicalToPhysicalAddress(offset);
            connection.WriteMemory(physicalAddress, data.AsSpan(offset, chunkLength).ToArray());
            offset += chunkLength;
        }
    }

    /// <summary>Reads the CURRENT end-address field (see DigitalContactCodec's
    /// own doc comment) to know how much previously-written space needs
    /// zero-filling if the new list is shorter. A never-written Meta record
    /// (brand new radio) reads back with an end address at or before
    /// DigitalContactData, which correctly yields 0.</summary>
    private static int ReadPreviousTotalBytes(IRadioConnection connection)
    {
        var metaBytes = connection.ReadMemoryStrict(D890UvMemoryMap.DigitalContactMeta, MetaLength);
        var endAddress = BinaryPrimitives.ReadUInt32LittleEndian(metaBytes.AsSpan(4, 4));
        return endAddress > D890UvMemoryMap.DigitalContactData
            ? DigitalContactCodec.PhysicalToLogicalAddress((int)endAddress)
            : 0;
    }
}
