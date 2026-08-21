using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Digital Contact database: the big DMR-ID address book, up to 500,000
/// entries - NOT the small Digital-Contact Whitelist (which reuses
/// <see cref="TalkgroupWhitelistCodec"/>). Transcribed from the MIT-licensed
/// reference project github.com/xbenkozx/anytone-cps (desktop/src/device.cpp:
/// Device::readDigitalContacts, Device::getDigitalContactDataBuffer,
/// Device::parseDigitalContact_D890UV - the confirmed D890UV code path).
///
/// UNLIKE every other list entity in this app, records are variable-length
/// (no fixed per-entry stride) and packed into one sequential logical byte
/// stream, so this codec isn't a pure function of an already-read buffer
/// like the others - the buffer's own length isn't known ahead of time, so
/// <see cref="DecodeAll"/> takes a live <see cref="IRadioConnection"/> and
/// reads incrementally as it parses, refilling <see cref="RefillChunkSize"/>
/// bytes at a time (matches the reference's own
/// <c>DigitalContactBufferLength</c> = 0x100 refill granularity for D890UV).
///
/// Address translation: the stream is not contiguous in device memory - it's
/// chunked into <see cref="BlockLength"/>-byte logical blocks, each mapped to
/// a physical address <see cref="BlockStride"/> bytes apart (block N's base
/// = <see cref="D890UvMemoryMap.DigitalContactData"/> + N * BlockStride).
/// Ported byte-for-byte from getDigitalContactDataBuffer's address_mod/block
/// arithmetic.
///
/// Record layout (D890UV): call_type(1) + call_alert(1) + radio_id(4,
/// BCD-hex-as-decimal, same trick as <see cref="BcdDecimalCodec"/>)
/// + 6 UTF-16LE strings each terminated by a double-null (name, city,
/// callsign, state, country, remarks) - no padding between records.
///
/// CONFIRMED UPSTREAM BUG, NOT PORTED: the reference's own
/// parseDigitalContact_D890UV has a bare <c>offset++;</c> right after the
/// remarks field, with no corresponding byte written by its own
/// writeDigitalContacts (which appends each field's wide-char bytes plus a
/// 2-byte "\0\0" terminator only - nothing extra between records). Initially
/// ported faithfully per this app's "port faithfully when unconfirmed" rule,
/// but a real differential test caught it: writing 900 real DMR-ID entries
/// (RadioID.net data, imported via the vendor CPS in a Windows VM) and
/// reading them back showed every ODD-indexed record garbled into CJK-range
/// mojibake while even-indexed records decoded perfectly - the classic
/// symptom of a 1-byte UTF-16 alignment drift. The stray odd-length
/// <c>offset++</c> flips the byte parity every record it runs on, so it
/// self-corrects every other record and corrupts the ones in between. Fixed
/// by dropping the stray increment entirely; re-verified against the same
/// 900-entry real dataset with 100% correct decodes across the board,
/// including the non-ASCII (Greek) entries deliberately included to stress
/// the UTF-16LE path.
///
/// THIS IS THE ONLY OPT-IN READ IN THE APP: <see cref="RadioCodeplugReader.Read"/>
/// only calls this when the caller explicitly asks for it (default off),
/// matching the vendor CPS's own "Digital Contact List" checkbox in its
/// read/write options dialog, which is ALSO unchecked by default in the
/// reference project (<c>UserSettings::read_write_options =
/// DeviceRWType::RADIO_DATA</c> only - digital contacts excluded). Every
/// other entity here is read unconditionally because it's fast and bounded;
/// this one can take a long time if a large DMR-ID database has been
/// imported onto the radio (real-world CSV imports from sources like
/// RadioID.net can be 100,000+ rows). Live-verified only for the
/// count-is-zero path so far (the test radio has no contacts imported) -
/// the per-record parsing logic has NOT been differentially verified
/// against a real populated contact on real hardware.
///
/// call_alert decode changed 2026-08-09: originally ported from the
/// reference project as a coarse bool ("==2 means enabled"). Since this
/// entity's UI now exposes the same None/Ring/Online Alert 3-state picker
/// Talkgroup has, this now reuses <see cref="TalkgroupCodec.CallAlertToString"/>
/// (0=None/1=Ring/2=Online Alert).
///
/// WRITE support added 2026-08-09, confirmed via 3 live differential write
/// captures against a real 900-contact database (RadioID.net import) - add
/// (a new contact appended after the existing 900), edit (grew AND shrank
/// two string fields on the same record in one write), and delete, each
/// re-read and diffed byte-for-byte:
/// - Record layout (call_type/call_alert/radio_id BCD/6 double-null
///   strings) confirmed exactly as Decode already assumed.
/// - call_type: 0=Private Call, 1=Group Call confirmed directly (All
///   Call=2 still only decode-side-confirmed, via the reference project).
/// - call_alert: 0=None and 2=Online Alert confirmed directly; 1=Ring
///   still only inferred by analogy with Talkgroup's independently-
///   confirmed mapping.
/// - The vendor CPS always rewrites the ENTIRE contact stream from the
///   start on every write (never a narrow per-record patch) - confirmed by
///   watching an edited/shrunk record correctly shift every byte of the
///   record after it, matching this app's own full-region-rewrite safety
///   convention used everywhere else. Freed space past the new end is
///   actively zeroed (confirmed on delete), not left as stale bytes.
/// - <see cref="D890UvMemoryMap.DigitalContactMeta"/>'s second 4-byte field
///   (offset 4, previously undecoded) is the ABSOLUTE END ADDRESS of the
///   data stream - NOT a length. Confirmed matching exactly across all 3
///   captures (901/902/901 contacts).
///
/// Multi-block write support added 2026-08-09, same day - the write side
/// shares <see cref="LogicalToPhysicalAddress"/> with <see cref="DecodeAll"/>'s
/// own block/stride translation (previously only proven for reads, and
/// even then only ever exercised within a single block - the 900-contact
/// database was ~76KB, under one block's ~195KB capacity).
///
/// Live-tested crossing a block boundary the same day (900 real contacts +
/// 1067 filler + 1 marker = 1968 contacts, ~204KB, landing a known record
/// physically inside block 1): confirmed the write-side chunking/address
/// translation is correct (marker decoded byte-for-byte from block 1, all
/// 900 real contacts still intact), AND caught a real bug in this class's
/// own first-pass Meta encoding - the "end address" field is NOT
/// <c>DigitalContactData + logical byte count</c> (that formula only
/// happened to look right because it's identical to the correct one for
/// any data still inside block 0). It's the PHYSICAL, block/stride-
/// translated address of the end of data - i.e. exactly
/// <see cref="LogicalToPhysicalAddress"/> applied to the logical byte
/// count, which is what <see cref="EncodeMeta"/> now does.
///
/// <see cref="MaxBlocks"/> raised to 500 (comfortably covering the
/// vendor's own claimed 500,000-entry capacity even at worst-case per-
/// record field lengths) once the block-crossing math itself was proven
/// generalizable - it's one uniform <c>offset / BlockStride</c> division
/// with no per-block special-casing, so correctness at block 1 implies
/// correctness at block 250 the same way. No other entity in this app's
/// memory map is known to sit anywhere near this address range even at
/// the high end (checked directly against <see cref="D890UvMemoryMap"/>),
/// though the RADIO's real flash capacity at that scale is still an open,
/// hardware-side question this app's logic can't answer on its own.
///
/// REAL BUG FOUND 2026-08-09, same day: the vendor CPS's "Friend List
/// Edit" dialog (a separate search-and-add picker over the SAME Digital
/// Contact List, capped at 1000 friends per a vendor error string) is
/// NOT a separate memory region at all. It's a single bit (0x10) packed
/// into the same byte this codec already decoded as call_alert (which
/// only ever uses the low 2 bits). Confirmed live: added 2 real contacts
/// to Friends, re-read all 900 real records, and exactly those 2 came
/// back with that byte at 0x10 instead of 0x00 - every other record was
/// unaffected. Since call_alert's own decode (<see cref="TalkgroupCodec.CallAlertToString"/>)
/// silently falls back to "None" for any raw value it doesn't recognize,
/// this bit had been invisibly discarded on every decode already shipped
/// - worse, <c>EncodeRecord</c> only ever emitted
/// <see cref="TalkgroupCodec.StringToCallAlertByte"/>'s 0/1/2 range, so
/// this app's own (already-shipped) Digital Contact write path would
/// have silently un-friended every contact on its very next write. Fixed
/// by masking call_alert to the low 2 bits on decode, extracting
/// <see cref="DecodedDigitalContact.IsFriend"/> from bit 0x10 separately,
/// and OR-ing it back in on encode. The 1000-friend cap is enforced at
/// the ViewModel validation layer, not here - this codec has no count-
/// wide state to check it against.
/// </summary>
public static class DigitalContactCodec
{
    public const int BlockLength = 0x30d40;
    public const int BlockStride = 0x80000;
    private const int RefillChunkSize = 0x100;
    private const int ReadBlockSize = 0x10;

    /// <summary>See this class's own doc comment - 500 blocks (100,000,000
    /// bytes) comfortably covers the vendor's own claimed 500,000-entry
    /// capacity even at every field maxed to its real length limit
    /// (6 + 6 fields x up to 34 bytes each ~= 186 bytes/record worst case,
    /// 500,000 x 186 = 93,000,000 bytes = 465 blocks).</summary>
    public const int MaxBlocks = 500;

    public static int ReadCount(IRadioConnection connection)
    {
        var metaBytes = connection.ReadMemory(D890UvMemoryMap.DigitalContactMeta, ReadBlockSize);
        return (int)BinaryPrimitives.ReadUInt32LittleEndian(metaBytes.AsSpan(0, 4));
    }

    public static List<DecodedDigitalContact> DecodeAll(IRadioConnection connection, int count, Action<int, int>? onProgress = null)
    {
        var results = new List<DecodedDigitalContact>(Math.Max(count, 0));
        if (count <= 0)
        {
            return results;
        }

        var buffer = new List<byte>(RefillChunkSize * 4);
        var offset = 0;

        void EnsureTotal(int totalBytesNeeded)
        {
            while (buffer.Count < totalBytesNeeded)
            {
                buffer.AddRange(ReadLogicalChunk(connection, buffer.Count, RefillChunkSize));
            }
        }

        string ReadDoubleNullString()
        {
            var start = offset;
            var end = start;
            while (true)
            {
                EnsureTotal(end + 2);
                if (buffer[end] == 0 && buffer[end + 1] == 0)
                {
                    break;
                }

                end += 2;
            }

            var span = CollectionsMarshal.AsSpan(buffer).Slice(start, end - start);
            var text = TextFieldCodec.DecodeName(span);
            offset = end + 2;
            return text;
        }

        for (var i = 0; i < count; i++)
        {
            EnsureTotal(offset + 6);
            var fixedSpan = CollectionsMarshal.AsSpan(buffer);
            var callType = fixedSpan[offset];
            offset += 1;
            var callAlertRaw = fixedSpan[offset];
            offset += 1;
            var radioId = BcdDecimalCodec.DecodeAsDecimal(fixedSpan.Slice(offset, 4));
            offset += 4;

            var name = ReadDoubleNullString();
            var city = ReadDoubleNullString();
            var callsign = ReadDoubleNullString();
            var state = ReadDoubleNullString();
            var country = ReadDoubleNullString();
            var remarks = ReadDoubleNullString();

            results.Add(new DecodedDigitalContact(i)
            {
                CallType = callType,
                CallAlert = TalkgroupCodec.CallAlertToString((byte)(callAlertRaw & 0x03)),
                IsFriend = (callAlertRaw & 0x10) != 0,
                RadioId = radioId,
                Name = name,
                City = city,
                Callsign = callsign,
                State = state,
                Country = country,
                Remarks = remarks
            });

            if (count > 100 && i % (count / 100) == 0)
            {
                onProgress?.Invoke(i, count);
            }
        }

        onProgress?.Invoke(count, count);
        return results;
    }

    /// <summary>Encodes the full contact list as one contiguous byte
    /// stream, in list order - see this class's own doc comment for why a
    /// full rewrite (never a narrow per-record patch) is required. Throws
    /// if the result would spill past <see cref="MaxBlocks"/> blocks.</summary>
    public static byte[] EncodeAll(IReadOnlyList<DecodedDigitalContact> contacts)
    {
        var result = new List<byte>();
        foreach (var contact in contacts)
        {
            result.AddRange(EncodeRecord(contact));
        }

        var maxBytes = BlockLength * MaxBlocks;
        if (result.Count > maxBytes)
        {
            throw new InvalidOperationException(
                $"Digital contact data is {result.Count} bytes, which spills past the {MaxBlocks}-block ({maxBytes}-byte) cap this app currently allows - refusing.");
        }

        return result.ToArray();
    }

    /// <summary>Shared block/stride address translation - see this class's
    /// own doc comment. Block N's logical bytes 0..<see cref="BlockLength"/>
    /// live physically at <see cref="D890UvMemoryMap.DigitalContactData"/> +
    /// N * <see cref="BlockStride"/> (BlockStride &gt; BlockLength, so
    /// consecutive blocks are NOT contiguous in device memory).</summary>
    public static int LogicalToPhysicalAddress(int logicalOffset)
    {
        var addrMod = logicalOffset % BlockLength;
        var block = (logicalOffset - addrMod) / BlockLength;
        return D890UvMemoryMap.DigitalContactData + block * BlockStride + addrMod;
    }

    /// <summary>Inverse of <see cref="LogicalToPhysicalAddress"/> - needed
    /// to interpret the Meta end-address field (see this class's own doc
    /// comment) back into a logical byte count once data spans more than
    /// one block. Only meaningful for a physical address that actually
    /// falls within a block's <see cref="BlockLength"/>-byte valid range,
    /// not the unused gap between consecutive blocks' physical bases.</summary>
    public static int PhysicalToLogicalAddress(int physicalAddress)
    {
        var offsetFromBase = physicalAddress - D890UvMemoryMap.DigitalContactData;
        var block = offsetFromBase / BlockStride;
        var addrMod = offsetFromBase % BlockStride;
        return block * BlockLength + addrMod;
    }

    /// <summary>Encodes the 16-byte Meta record (<see cref="D890UvMemoryMap.DigitalContactMeta"/>):
    /// count at offset 0, and the ABSOLUTE END ADDRESS of the data stream
    /// at offset 4 - see this class's own doc comment for the live-
    /// confirmed field meaning (a PHYSICAL, block/stride-translated
    /// address via <see cref="LogicalToPhysicalAddress"/>, not a naive
    /// base+length sum - confirmed live 2026-08-09 crossing a block
    /// boundary). The remaining 8 bytes are left zero; their purpose (if
    /// any) was never observed as non-zero across any live capture.</summary>
    public static byte[] EncodeMeta(int count, int totalDataBytes)
    {
        var result = new byte[ReadBlockSize];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), (uint)count);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)LogicalToPhysicalAddress(totalDataBytes));
        return result;
    }

    private static byte[] EncodeRecord(DecodedDigitalContact contact)
    {
        var callAlertByte = (byte)(TalkgroupCodec.StringToCallAlertByte(contact.CallAlert) | (contact.IsFriend ? 0x10 : 0));
        var result = new List<byte>(64)
        {
            contact.CallType,
            callAlertByte
        };
        result.AddRange(BcdDecimalCodec.EncodeAsDecimal(contact.RadioId, 4));
        result.AddRange(EncodeDoubleNullString(contact.Name));
        result.AddRange(EncodeDoubleNullString(contact.City));
        result.AddRange(EncodeDoubleNullString(contact.Callsign));
        result.AddRange(EncodeDoubleNullString(contact.State));
        result.AddRange(EncodeDoubleNullString(contact.Country));
        result.AddRange(EncodeDoubleNullString(contact.Remarks));
        return result.ToArray();
    }

    /// <summary>UTF-16LE bytes followed by a double-null terminator - the
    /// variable-length counterpart to TextFieldCodec.EncodeName's fixed-
    /// length padding, matching this entity's own confirmed wire shape.</summary>
    private static byte[] EncodeDoubleNullString(string value)
    {
        var textBytes = System.Text.Encoding.Unicode.GetBytes(value);
        var result = new byte[textBytes.Length + 2];
        textBytes.CopyTo(result, 0);
        return result;
    }

    private static byte[] ReadLogicalChunk(IRadioConnection connection, int logicalOffset, int length)
    {
        var result = new byte[length];
        var pos = 0;
        while (pos < length)
        {
            var addr = LogicalToPhysicalAddress(logicalOffset);
            var take = Math.Min(ReadBlockSize, length - pos);
            var raw = connection.ReadMemory(addr, ReadBlockSize);
            Array.Copy(raw, 0, result, pos, take);
            pos += take;
            logicalOffset += take;
        }

        return result;
    }

    public sealed record DecodedDigitalContact(int Index)
    {
        public byte CallType { get; init; }
        public string CallAlert { get; init; } = "None";
        public bool IsFriend { get; init; }
        public long RadioId { get; init; }
        public string Name { get; init; } = "";
        public string City { get; init; } = "";
        public string Callsign { get; init; } = "";
        public string State { get; init; } = "";
        public string Country { get; init; } = "";
        public string Remarks { get; init; } = "";
    }
}
