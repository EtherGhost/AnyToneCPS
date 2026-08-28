using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for a single 128-byte D890UV channel record. No I/O.
/// Byte offsets confirmed against real hardware captures
/// (notably the UTF-16LE name field at 0x44) and cross-checked against
/// the reference project github.com/xbenkozx/anytone-cps (channel.cpp).
/// </summary>
public static class ChannelCodec
{
    public const int RecordLength = 0x80;

    // Frequency field (offset 0x00 RX, 0x04 TX-offset, BCD/100000.0) confirmed
    // 2026-07-14 by a read-only cross-check against a real radio: the
    // PMR446 channels (idx 599-606) decoded to exactly 446.00625-446.09375 MHz
    // in 12.5kHz steps, and the marine VHF channels (idx 699-703, e.g. "MAR16"
    // -> 156.80000 MHz) matched the internationally standardized channel plan
    // exactly - both are independently-known external standards, not
    // guessable/coincidental, so this is stronger confirmation than a synthetic
    // write-and-diff test and required no write to the radio at all.
    public static DecodedChannel Decode(ReadOnlySpan<byte> data, int index)
    {
        var rxFrequency = DecodeBcdFrequency(data.Slice(0x00, 4));
        if (rxFrequency == 0)
        {
            // Matches channel.cpp's decode_D890UV: `if (rx_frequency == 0) return;`
            // The rest of the buffer is erased-flash garbage on an unused slot.
            return new DecodedChannel(index) { IsBlank = true };
        }

        var offsetByte = data[0x08];
        var flagsByte = data[0x09];
        var squelchPttByte = data[0x19];
        var signalBusyByte = data[0x1a];
        var digitalByte = data[0x21];
        var extra34 = data[0x34];
        var extra3b = data[0x3b];

        return new DecodedChannel(index)
        {
            IsBlank = false,
            RxFrequencyMHz = rxFrequency,
            OffsetMHz = DecodeBcdFrequency(data.Slice(0x04, 4)),
            OffsetDirection = (byte)((offsetByte >> 6) & 0x3),
            BandWidth = (byte)((offsetByte >> 4) & 0x3),
            TxPower = (byte)((offsetByte >> 2) & 0x3),
            ChannelType = (byte)(offsetByte & 0x3),

            Talkaround = (flagsByte & 0x80) != 0,
            CallConfirmation = (flagsByte & 0x40) != 0,
            PttProhibit = (flagsByte & 0x20) != 0,
            Reverse = (flagsByte & 0x10) != 0,
            // Confirmed 2026-07-17 by a live differential test (real vendor CPS,
            // spare test channels written to real hardware): 0=Off, 1=CTCSS, 2=DCS.
            CtcssDcsEncode = (byte)((flagsByte >> 2) & 0x3),
            CtcssDcsDecode = (byte)(flagsByte & 0x3),

            // Confirmed write-safe via 2 live differential tests 2026-08-02
            // (real vendor CPS writes against channel AV00). Round 1: CTCSS
            // Encode = 100.0 (index 13 in ChannelEntry.CtcssToneLabels) ->
            // byte 0x0a = 13; DCS Decode = D023N -> bytes 0x0e-0x0f = 19
            // (octal "023" read as a plain number). Round 2: DCS Encode =
            // D023I -> bytes 0x0c-0x0d = 531 = 19 + 512, confirming Inverted
            // codes are Normal + 512; CTCSS Decode = 62.5 (index 0) -> byte
            // 0x0b = 0. Both CTCSS bytes are a 0-based index into the
            // 51-tone list; both DCS words are a 0-based index into the
            // 1024-entry N-then-I list (ChannelEntry.DcsCodeLabels) - same
            // "plain list index" shape as every other ID field, no octal
            // math needed at the property level. 3
            // pre-existing real channels (HALLANDSAS VHF/UHF, SODERAS VHF)
            // independently corroborated the CTCSS formula: their existing
            // raw values (45/6/26) decode to 225.7/79.7/156.7 Hz, all
            // plausible real-world CTCSS tones.
            CtcssEncodeTone = data[0x0a],
            CtcssDecodeTone = data[0x0b],
            DcsEncodeTone = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0c, 2)),
            DcsDecodeTone = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0e, 2)),
            CustomCtcss = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x10, 2)),
            Tone2Decode = data[0x12],
            // Explicitly confirmed big-endian in the reference source:
            // `Int::fromBytes(data.mid(0x13, 2), Endian::Big)`.
            ContactIndex = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(0x13, 2)),

            RadioIdIndex = data[0x18],
            // Confirmed 2026-07-17 by a live differential test: this is a 3-bit
            // field (bits 6-4 of byte 0x19), not 2 bits as originally assumed -
            // the official CPS has 5 Squelch Mode options (Field_Reference.md),
            // which a 2-bit field can't represent. The old `& 0x3` mask silently
            // wrapped the 5th option (raw 4, "CTC/DCS|Optional Signal", byte
            // value 0x40) back down to 0 ("Carrier"). See ChannelCodec doc
            // comment / RadioReadMapper.MapSquelchMode for the confirmed values.
            SquelchMode = (byte)((squelchPttByte >> 4) & 0x7),
            PttId = (byte)(squelchPttByte & 0x3),
            // Confirmed 2026-07-17: matches the existing 0=Off/1=DTMF/2=2Tone/
            // 5Tone mapping exactly, no change needed.
            // CORRECTED 2026-08-01: same class of bug as SquelchMode -
            // this is a 3-bit field (bits 6-4 of 0x1a), not 2 bits. A live
            // differential test (real vendor CPS, channel AV00, Optional
            // Signal set to the 5th item "QDC1200") produced byte 0x1a =
            // 0x40, i.e. bit 6 set - the old `& 0x3` mask would have
            // wrapped raw 4 back down to 0 ("Off"). BusyLock (bits 1-0)
            // was untouched.
            OptionalSignal = (byte)((signalBusyByte >> 4) & 0x7),
            // Confirmed 2026-07-17: Busy Lock/TX Permit is ONE shared 4-value
            // enum for both digital and analog channels (0=Always, 1=Channel
            // Free, 2=Different Color Code, 3=Same Color Code) - not two
            // separate lists as previously assumed. See
            // RadioReadMapper.MapBusyLock.
            BusyLock = (byte)(signalBusyByte & 0x3),
            ScanListIndex = data[0x1b],
            ReceiveGroupCallListIndex = data[0x1c],
            Tone2IdIndex = data[0x1d],
            Tone5IdIndex = data[0x1e],
            DtmfIdIndex = data[0x1f],
            RxColorCode = data[0x20],

            WorkAlone = (digitalByte & 0x80) != 0,
            AprsRx = (digitalByte & 0x20) != 0,
            SlotSuit = (digitalByte & 0x10) != 0,
            DmrModeDcdm = (byte)((digitalByte >> 2) & 0x3),
            TimeSlot = (digitalByte & 0x02) != 0,
            SmsConfirmation = (digitalByte & 0x01) != 0,

            AesEncryptionIndex = data[0x22],

            DmrCrcIgnore = (extra34 & 0x80) != 0,
            // Confirmed write-safe via a live differential test 2026-08-01
            // (real vendor CPS write against channel AV00, "Idle TX"
            // toggled on - a single clean bit flip against the channel's
            // own prior state, nothing else in the 128-byte record
            // touched). Previously the last unclaimed bit in 0x34 besides
            // bit 6.
            IdleTx = (extra34 & 0x20) != 0,
            AutoScan = (extra34 & 0x10) != 0,
            DataAckDisable = (extra34 & 0x08) != 0,
            ExcludeChannelRoaming = (extra34 & 0x04) != 0,
            DmrMode = (extra34 & 0x02) != 0,
            Ranging = (extra34 & 0x01) != 0,

            AprsReportType = data[0x35],
            AnalogAprsPttMode = data[0x36],
            DigitalAprsPttMode = data[0x37],
            DigitalAprsReportChannel = data[0x38],
            CorrectFrequency = data[0x39],
            DigitalEncryption = data[0x3a],

            // Confirmed write-safe via a live differential test 2026-08-01
            // (real vendor CPS write against channel EV01, "Low priority"
            // set - a single clean bit flip against the channel's own
            // prior state). Only Off/Low priority (this bit clear/set)
            // are confirmed; "High priority"'s encoding is untested.
            TxInterrupt = (extra3b & 0x80) != 0,
            ExtendEncryption = (extra3b & 0x20) != 0,
            SendTalkerAlias = (extra3b & 0x10) != 0,
            AnalogAprsMute = (extra3b & 0x08) != 0,
            SmsForbid = (extra3b & 0x04) != 0,
            AesRandomKey = (extra3b & 0x02) != 0,
            AesMultipleKey = (extra3b & 0x01) != 0,

            AnalogAprsReportFrequencyIndex = data[0x3c],
            Arc4EncryptionKeyIndex = data[0x3d],
            ScramblerSet = data[0x3e],
            CustomScrambler = data[0x3f],
            R5ToneBot = data[0x40],
            R5ToneEot = data[0x41],
            // Confirmed write-safe via a live differential test 2026-08-01
            // (real vendor CPS write against channel AV00, Optional Signal
            // set to QDC1200 with the QDC1200 ID field set to its 3rd
            // entry). Previously an unclaimed byte between R5ToneEot
            // (0x41) and TxColorCode (0x43) - now holds 0-based index 2,
            // same 0-based-raw-byte shape as DtmfIdIndex/Tone2IdIndex/
            // Tone5IdIndex. Uniquely nonzero among every other populated
            // channel in the capture.
            QdcIdIndex = data[0x42],
            TxColorCode = data[0x43],

            Name = TextFieldCodec.DecodeName(data.Slice(0x44, 0x20))
        };
    }

    private static double DecodeBcdFrequency(ReadOnlySpan<byte> fourBytes)
    {
        // Was a duplicate throwing long.Parse - consolidated onto the shared
        // helper after a real erased-flash slot (all 0xFF bytes) crashed a
        // read here. See BcdDecimalCodec's doc comment for why 0 is correct.
        return BcdDecimalCodec.DecodeAsDecimal(fourBytes) / 100000.0;
    }

    private static byte[] EncodeBcdFrequency(double mhz)
    {
        var raw = (long)Math.Round(mhz * 100000.0);
        return BcdDecimalCodec.EncodeAsDecimal(raw, 4);
    }

    /// <summary>Replaces the bits [<paramref name="startBit"/>,
    /// <paramref name="startBit"/> + <paramref name="width"/>) of
    /// <paramref name="original"/> with the low <paramref name="width"/>
    /// bits of <paramref name="newValue"/>, leaving every other bit in the
    /// byte untouched - the bit-level half of this codec's field-level
    /// read-modify-write contract (see <see cref="Encode"/>'s doc comment).</summary>
    private static byte PatchBits(byte original, int startBit, int width, byte newValue)
    {
        var mask = (byte)(((1 << width) - 1) << startBit);
        return (byte)((original & ~mask) | ((newValue << startBit) & mask));
    }

    /// <summary>
    /// Strict field-level read-modify-write: takes a freshly-read 128-byte
    /// record and a <see cref="ChannelFieldPatch"/> with only the fields
    /// actually being changed set (everything else null), and returns a new
    /// 128-byte record identical to <paramref name="currentRecord"/> except
    /// for those specific fields. Never constructs a record from scratch -
    /// every byte/bit not named in <paramref name="patch"/> is copied
    /// through unchanged, including bytes this codec doesn't even understand
    /// yet (the unexplained <c>00 00 00 01</c> prefix before the name field,
    /// every "misc"/unconfirmed byte, etc.).
    ///
    /// Only exposes fields differential-test-confirmed against real
    /// hardware (frequency via independent real-world standards 2026-07-14;
    /// CTCSS/DCS mode, Squelch Mode, Optional Signal, Busy-Lock/TX-Permit via
    /// a live differential test 2026-07-17; Contact/Talk Group, Radio ID,
    /// Receive Group List, PTT ID, ChannelType, TransmitPower, Bandwidth,
    /// TalkAround, CallConfirmation, PttProhibit, Reverse, RxColorCode,
    /// TxColorCode, WorkAlone, SlotSuit, RepeaterSlot2, SmsConfirmation,
    /// AesEncryptionIndex, Arc4EncryptionKeyIndex, AutoScan, ScrambleMode,
    /// CustomScrambleFrequencyIndex, DigitalEncryptionIndex,
    /// CorrectFrequencyHz, CustomCtcss, DmrModeDcdm via live differential
    /// tests 2026-07-19; DmrCrcIgnore, SendTalkerAlias, SmsForbid,
    /// DataAckDisable, ExcludeChannelRoaming, AesRandomKey, AesMultipleKey,
    /// DmrMode via live differential tests 2026-07-31; AprsRx, DtmfIdIndex,
    /// Tone2IdIndex, Tone5IdIndex, Tone2Decode, R5ToneBot, R5ToneEot,
    /// ExtendEncryption, IdleTx, TxInterrupt via live differential tests
    /// 2026-08-01) plus Name (confirmed via the original write-protocol
    /// test). Any other field not listed here is not yet confirmed. Scan
    /// List is NOT a per-channel
    /// field at all - confirmed 2026-07-19 that scan list membership is
    /// stored on the scan list side (like Zone membership), not as a
    /// channel-level index - see the "Rework Scan List assignment" task.
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, ChannelFieldPatch patch)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Channel record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();

        if (patch.RxFrequencyMHz is { } rxFrequency)
        {
            EncodeBcdFrequency(rxFrequency).CopyTo(result, 0x00);
        }

        if (patch.OffsetMHz is { } offset)
        {
            EncodeBcdFrequency(offset).CopyTo(result, 0x04);
        }

        if (patch.OffsetDirection is { } offsetDirection)
        {
            // Bits 7-6 of 0x08 - shares this byte with BandWidth (bits 5-4),
            // TxPower (bits 3-2), ChannelType (bits 1-0).
            result[0x08] = PatchBits(result[0x08], 6, 2, offsetDirection);
        }

        if (patch.Bandwidth is { } bandwidth)
        {
            // Bits 5-4 of 0x08. Confirmed write-safe via a live differential
            // test 2026-07-19 (real vendor CPS write, exact match on
            // read-back) - see ChannelFieldPatch's doc comment.
            result[0x08] = PatchBits(result[0x08], 4, 2, bandwidth);
        }

        if (patch.TransmitPower is { } transmitPower)
        {
            // Bits 3-2 of 0x08. Confirmed write-safe 2026-07-19.
            result[0x08] = PatchBits(result[0x08], 2, 2, transmitPower);
        }

        if (patch.ChannelType is { } channelType)
        {
            // Bits 1-0 of 0x08. Confirmed write-safe 2026-07-19.
            result[0x08] = PatchBits(result[0x08], 0, 2, channelType);
        }

        if (patch.CtcssDcsEncode is { } ctcssDcsEncode)
        {
            // Bits 3-2 of 0x09 - shares this byte with the 4 boolean flags
            // (bits 7-4) and CtcssDcsDecode (bits 1-0), which stay untouched.
            result[0x09] = PatchBits(result[0x09], 2, 2, ctcssDcsEncode);
        }

        if (patch.CtcssDcsDecode is { } ctcssDcsDecode)
        {
            result[0x09] = PatchBits(result[0x09], 0, 2, ctcssDcsDecode);
        }

        // Round 2, confirmed write-safe via a live differential test
        // 2026-07-19 (real vendor CPS write, exact match on read-back -
        // Reverse only applies to analog channels, confirmed to be a real
        // vendor-side interlock, not a decode bug, when it didn't stick on
        // a non-analog test channel).
        if (patch.TalkAround is { } talkAround)
        {
            result[0x09] = PatchBits(result[0x09], 7, 1, (byte)(talkAround ? 1 : 0));
        }

        if (patch.CallConfirmation is { } callConfirmation)
        {
            result[0x09] = PatchBits(result[0x09], 6, 1, (byte)(callConfirmation ? 1 : 0));
        }

        if (patch.PttProhibit is { } pttProhibit)
        {
            result[0x09] = PatchBits(result[0x09], 5, 1, (byte)(pttProhibit ? 1 : 0));
        }

        if (patch.Reverse is { } reverse)
        {
            result[0x09] = PatchBits(result[0x09], 4, 1, (byte)(reverse ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-02 -
        // see Decode's doc comment above.
        if (patch.CtcssEncodeTone is { } ctcssEncodeTone)
        {
            result[0x0a] = ctcssEncodeTone;
        }

        if (patch.CtcssDecodeTone is { } ctcssDecodeTone)
        {
            result[0x0b] = ctcssDecodeTone;
        }

        if (patch.DcsEncodeTone is { } dcsEncodeTone)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x0c, 2), dcsEncodeTone);
        }

        if (patch.DcsDecodeTone is { } dcsDecodeTone)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x0e, 2), dcsDecodeTone);
        }

        if (patch.SquelchMode is { } squelchMode)
        {
            // Bits 6-4 of 0x19 (3 bits - see the 2026-07-17 confirmation
            // above) - shares this byte with PttId (bits 1-0), untouched.
            result[0x19] = PatchBits(result[0x19], 4, 3, squelchMode);
        }

        if (patch.OptionalSignal is { } optionalSignal)
        {
            // Bits 6-4 of 0x1a (3 bits - see the 2026-08-01 correction
            // above) - shares this byte with BusyLock (bits 1-0), untouched.
            result[0x1a] = PatchBits(result[0x1a], 4, 3, optionalSignal);
        }

        if (patch.BusyLock is { } busyLock)
        {
            result[0x1a] = PatchBits(result[0x1a], 0, 2, busyLock);
        }

        if (patch.Name is { } name)
        {
            TextFieldCodec.EncodeName(name, 0x20).CopyTo(result, 0x44);
        }

        // Round 5, confirmed write-safe via a live differential test
        // 2026-07-19 (real vendor CPS write, exact match on read-back).
        // Found RX (0x20) and TX (0x43) color code are genuinely
        // INDEPENDENT fields - this app previously only tracked RX and
        // assumed TX always matched it, which turned out to be wrong.
        if (patch.RxColorCode is { } rxColorCode)
        {
            result[0x20] = rxColorCode;
        }

        if (patch.TxColorCode is { } txColorCode)
        {
            result[0x43] = txColorCode;
        }

        if (patch.ContactIndex is { } contactIndex)
        {
            // Big-endian per Decode's confirmed reading - see its doc
            // comment. Confirmed write-safe via a live differential test
            // 2026-07-19 (real vendor CPS write, exact match on read-back).
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(0x13, 2), contactIndex);
        }

        if (patch.RadioIdIndex is { } radioIdIndex)
        {
            result[0x18] = radioIdIndex;
        }

        if (patch.PttId is { } pttId)
        {
            // Bits 1-0 of 0x19 - shares this byte with SquelchMode (bits
            // 6-4), which stays untouched.
            result[0x19] = PatchBits(result[0x19], 0, 2, pttId);
        }

        if (patch.ReceiveGroupCallListIndex is { } receiveGroupCallListIndex)
        {
            result[0x1c] = receiveGroupCallListIndex;
        }

        // Round 6/7/8/9, confirmed write-safe via a combined live
        // differential test 2026-07-19 (real vendor CPS write, exact match
        // on read-back).
        if (patch.WorkAlone is { } workAlone)
        {
            result[0x21] = PatchBits(result[0x21], 7, 1, (byte)(workAlone ? 1 : 0));
        }

        if (patch.SlotSuit is { } slotSuit)
        {
            result[0x21] = PatchBits(result[0x21], 4, 1, (byte)(slotSuit ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-07-19
        // (real vendor CPS write, exact match on read-back). Bits 3-2 of
        // 0x21 - a 3-value "DCDM submode" (0=Off/Normal, 1=Double Slot,
        // 2=TS Split) - DMO Simplex vs Repeater is a separate bit entirely
        // (ChannelEntry.DmrMode, byte 0x34 bit 1 - see
        // ChannelCodec.DmrModeSelectionToString, corrected 2026-07-31).
        if (patch.DmrModeDcdm is { } dmrModeDcdm)
        {
            result[0x21] = PatchBits(result[0x21], 2, 2, dmrModeDcdm);
        }

        if (patch.RepeaterSlot2 is { } repeaterSlot2)
        {
            result[0x21] = PatchBits(result[0x21], 1, 1, (byte)(repeaterSlot2 ? 1 : 0));
        }

        if (patch.SmsConfirmation is { } smsConfirmation)
        {
            result[0x21] = PatchBits(result[0x21], 0, 1, (byte)(smsConfirmation ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel DV02, exact match - byte
        // 0x21 changed from 0x00 to 0x20, only bit 5, nothing else in the
        // record touched, and cross-checked against 550 other populated
        // channels with none showing this bit set).
        if (patch.AprsRx is { } aprsRx)
        {
            result[0x21] = PatchBits(result[0x21], 5, 1, (byte)(aprsRx ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to DTMF - 0-based index into the vendor CPS's M1-M16 DTMF ID
        // list, exact match, cross-checked against every other populated
        // channel in the same write).
        if (patch.DtmfIdIndex is { } dtmfIdIndex)
        {
            result[0x1f] = dtmfIdIndex;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to 2Tone - 0-based index into the vendor CPS's 2Tone settings
        // list, exact match, only AV00 had a nonzero value in the whole
        // write).
        if (patch.Tone2IdIndex is { } tone2IdIndex)
        {
            result[0x1d] = tone2IdIndex;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to 5Tone - 0-based index into the vendor CPS's 5Tone settings
        // list, exact match - a clean 2-byte diff against the prior 2Tone
        // capture, the other byte being OptionalSignal itself switching
        // from 2Tone to 5Tone).
        if (patch.Tone5IdIndex is { } tone5IdIndex)
        {
            result[0x1e] = tone5IdIndex;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to 2Tone - a clean 2-byte diff against the prior 5Tone capture,
        // the other byte being OptionalSignal switching back to 2Tone).
        // Only raw 0/1 confirmed - the vendor CPS dropdown showed just 2
        // items because only 2 real 2Tone settings entries existed at
        // test time, so this is treated as the same 16-slot field as
        // DtmfIdIndex/Tone2IdIndex/Tone5IdIndex rather than assumed to
        // cap at 2.
        if (patch.Tone2Decode is { } tone2Decode)
        {
            result[0x12] = tone2Decode;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to 5Tone - a clean 2-byte diff against the prior 2Tone Decode
        // capture, the other byte being OptionalSignal switching to
        // 5Tone). Real vendor CPS items are "1"/"2"/"Customize" - the 2
        // presets are raw 0/1 (0-based). "Customize" confirmed write-safe
        // via a second live differential test 2026-08-02 (channel AV00, 5Tone
        // Bot set to "Customize" - byte 0x40 went to 0x64/100 decimal, a
        // sentinel value uniquely isolated against every other populated
        // channel - see ChannelEntry.R5ToneBotSelection).
        if (patch.R5ToneBot is { } r5ToneBot)
        {
            result[0x40] = r5ToneBot;
        }

        if (patch.R5ToneEot is { } r5ToneEot)
        {
            result[0x41] = r5ToneEot;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, Optional Signal set
        // to QDC1200 - a clean 2-byte diff against the prior state, the
        // other byte being OptionalSignal itself switching to QDC1200).
        if (patch.QdcIdIndex is { } qdcIdIndex)
        {
            result[0x42] = qdcIdIndex;
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel EV01, Extended
        // Encryption set to ARC4 - a single clean bit flip, bit 5 of
        // 0x3b, 0x00 -> 0x20, against the channel's own prior state from
        // an earlier capture - nothing else touched, including the
        // AesEncryptionIndex/Arc4EncryptionKeyIndex bytes, which stayed
        // at their existing values). False = AES, true = ARC4.
        if (patch.ExtendEncryption is { } extendEncryption)
        {
            result[0x3b] = PatchBits(result[0x3b], 5, 1, (byte)(extendEncryption ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel EV01, TX Interrupt set to
        // "Low priority" - a single clean bit flip, bit 7 of 0x3b,
        // 0x00 -> 0x80, isolated from an unrelated backup-restore write
        // that happened just before it). Real vendor CPS items are
        // "Off"/"Low priority"/"High priority" - a "High priority" write
        // attempt failed with a communication error before completing, so
        // only Off/Low priority (bit 7 clear/set) are confirmed; bit 6
        // (a plausible High priority bit) is left untouched rather than
        // guessed at.
        if (patch.TxInterrupt is { } txInterrupt)
        {
            result[0x3b] = PatchBits(result[0x3b], 7, 1, (byte)(txInterrupt ? 1 : 0));
        }

        if (patch.AesEncryptionIndex is { } aesEncryptionIndex)
        {
            result[0x22] = aesEncryptionIndex;
        }

        if (patch.Arc4EncryptionKeyIndex is { } arc4EncryptionKeyIndex)
        {
            result[0x3d] = arc4EncryptionKeyIndex;
        }

        if (patch.AutoScan is { } autoScan)
        {
            // Bit 4 of 0x34 - shares this byte with DmrCrcIgnore/
            // DataAckDisable/ExcludeChannelRoaming/DmrMode/Ranging.
            result[0x34] = PatchBits(result[0x34], 4, 1, (byte)(autoScan ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-07-31 (2
        // clean single-bit diffs on channel DV01: explicitly picking
        // "DMO/Simplex" in the real vendor CPS's DMR Mode dropdown set this
        // bit to 1, picking "Repeater" set it back to 0, with nothing else
        // in the 128-byte record touched either time). This is what
        // actually distinguishes "DMO/Simplex" from "Repeater" in the
        // vendor CPS's 4-item DMR Mode dropdown - DmrModeDcdm (0x21 bits
        // 2-3) alone can't, since both those options leave it at raw 0 -
        // see ChannelEntry.DmrModeSelection, which combines this bit with
        // DmrModeDcdm into the vendor CPS's real 4-item list.
        if (patch.DmrMode is { } dmrMode)
        {
            result[0x34] = PatchBits(result[0x34], 1, 1, (byte)(dmrMode ? 1 : 0));
        }

        if (patch.ScrambleMode is { } scrambleMode)
        {
            result[0x3e] = scrambleMode;
        }

        if (patch.CustomScrambleFrequencyIndex is { } customScrambleFrequencyIndex)
        {
            result[0x3f] = customScrambleFrequencyIndex;
        }

        // Round 7/8/9 follow-ups, confirmed write-safe via a live
        // differential test 2026-07-19: DigitalEncryptionIndex re-tested
        // after clearing a vendor CPS interlock (channel already using
        // AES/ARC4); CorrectFrequencyHz and CustomCtcss re-tested to find
        // their real scale factors (raw byte is 10 Hz per count for
        // CorrectFrequencyHz, raw ushort is tenths of a Hz for CustomCtcss -
        // see ChannelEntry.CorrectFrequencyHzText/CustomCtcssText).
        if (patch.DigitalEncryptionIndex is { } digitalEncryptionIndex)
        {
            result[0x3a] = digitalEncryptionIndex;
        }

        if (patch.CorrectFrequencyHz is { } correctFrequencyHz)
        {
            result[0x39] = correctFrequencyHz;
        }

        if (patch.CustomCtcss is { } customCtcss)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x10, 2), customCtcss);
        }

        // Confirmed write-safe via a live differential test 2026-07-31
        // (real vendor CPS write against channel DV01, exact match on the
        // captured payload - only these 3 bits differed from every other
        // populated channel in the same codeplug write).
        if (patch.DmrCrcIgnore is { } dmrCrcIgnore)
        {
            // Bit 7 of 0x34 - shares this byte with IdleTx (bit 5),
            // AutoScan (bit 4), DataAckDisable (bit 3),
            // ExcludeChannelRoaming (bit 2), DmrMode (bit 1), and the
            // still-unclaimed bit 6.
            result[0x34] = PatchBits(result[0x34], 7, 1, (byte)(dmrCrcIgnore ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-01
        // (real vendor CPS write against channel AV00, "Idle TX" toggled
        // on - a single clean bit flip against the channel's own prior
        // state, nothing else in the record touched).
        if (patch.IdleTx is { } idleTx)
        {
            result[0x34] = PatchBits(result[0x34], 5, 1, (byte)(idleTx ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-08-02
        // (real vendor CPS write against channel EV01, "Ranging" checked -
        // a single clean bit flip, bit 0 of 0x34, 0x0a -> 0x0b, uniquely
        // isolated against every other populated channel in the capture,
        // including EV01's own digital siblings which all stayed at
        // 0x0a).
        if (patch.Ranging is { } ranging)
        {
            result[0x34] = PatchBits(result[0x34], 0, 1, (byte)(ranging ? 1 : 0));
        }

        if (patch.SendTalkerAlias is { } sendTalkerAlias)
        {
            // Bit 4 of 0x3b - shares this byte with SmsForbid (bit 2).
            result[0x3b] = PatchBits(result[0x3b], 4, 1, (byte)(sendTalkerAlias ? 1 : 0));
        }

        if (patch.SmsForbid is { } smsForbid)
        {
            result[0x3b] = PatchBits(result[0x3b], 2, 1, (byte)(smsForbid ? 1 : 0));
        }

        // Confirmed write-safe via a live differential test 2026-07-31 (2
        // rounds on channel DV01: DataAckDisable's bit flipped 1->0 in
        // isolation the first round; ExcludeChannelRoaming/AesRandomKey/
        // AesMultipleKey were confirmed the second round, deliberately set
        // to different values from each other so the previously-ambiguous
        // AesRandomKey/AesMultipleKey bit order could be told apart).
        if (patch.DataAckDisable is { } dataAckDisable)
        {
            // Bit 3 of 0x34 - shares this byte with DmrCrcIgnore (bit 7),
            // AutoScan (bit 4), ExcludeChannelRoaming (bit 2), DmrMode
            // (bit 1), and Ranging (bit 0).
            result[0x34] = PatchBits(result[0x34], 3, 1, (byte)(dataAckDisable ? 1 : 0));
        }

        if (patch.ExcludeChannelRoaming is { } excludeChannelRoaming)
        {
            result[0x34] = PatchBits(result[0x34], 2, 1, (byte)(excludeChannelRoaming ? 1 : 0));
        }

        if (patch.AesRandomKey is { } aesRandomKey)
        {
            // Bit 1 of 0x3b - only meaningful once an AES key is assigned
            // (AesEncryptionIndex != 0).
            result[0x3b] = PatchBits(result[0x3b], 1, 1, (byte)(aesRandomKey ? 1 : 0));
        }

        if (patch.AesMultipleKey is { } aesMultipleKey)
        {
            result[0x3b] = PatchBits(result[0x3b], 0, 1, (byte)(aesMultipleKey ? 1 : 0));
        }

        NormalizeErasedUnclaimedBytes(result);

        return result;
    }

    /// <summary>Found live 2026-08-29: 42 real channels in a live user
    /// codeplug had 0xFF (erased flash) sitting in several byte ranges this
    /// codec has never decoded/modeled - including the APRS report fields
    /// below, which ARE decoded on read (see Decode above) but were never
    /// wired into ChannelFieldPatch, so a normal write always left them
    /// untouched. The vendor CPS's own read routine ("GetFreFromCommData")
    /// crashed trying to parse one of these channels - traced to
    /// AnalogAprsReportFrequencyIndex (0x3c) most directly by name, but a
    /// live differential test proved it wasn't that field alone; cloning a
    /// known-good neighbor channel's whole unclaimed-byte structure is what
    /// actually let the vendor CPS read past it. Root cause: a channel
    /// freshly written onto a blank/reset radio slot for the first time
    /// (HasAnyPendingRadioWrite true purely because _radioSyncSnapshot is
    /// null, not because of any real field edit - see ChannelEntry's own
    /// doc comment) goes through this Encode, but every field patch above
    /// is null since the user changed nothing - so whatever the radio's
    /// blank-slot default happens to be (0xFF, i.e. erased flash) passed
    /// straight through unfixed. Runs unconditionally at the end of every
    /// Encode call so this can't recur, e.g. after a future stock reset.
    /// Only ever clears a byte that's CURRENTLY 0xFF (or, for the "mixed"
    /// bytes below, only the specific bit positions this codec has never
    /// claimed) - never overwrites a byte holding any other value, so a
    /// channel with genuine data in these positions is left alone.</summary>
    private static void NormalizeErasedUnclaimedBytes(byte[] result)
    {
        void ClearIfErased(int offset)
        {
            if (result[offset] == 0xFF)
            {
                result[offset] = 0x00;
            }
        }

        for (var i = 0x15; i <= 0x17; i++) ClearIfErased(i); // fully unclaimed gap

        // squelchPttByte/signalBusyByte: bits 7,3,2 (mask 0x8C) are unclaimed -
        // SquelchMode/PttId and OptionalSignal/BusyLock respectively occupy
        // the rest. Always safe to clear regardless of current value - these
        // specific bit positions have no legitimate meaning either way.
        result[0x19] = (byte)(result[0x19] & ~0x8C);
        result[0x1a] = (byte)(result[0x1a] & ~0x8C);

        // digitalByte/extra34: only bit 6 (0x40) is unclaimed in each.
        result[0x21] = (byte)(result[0x21] & ~0x40);
        result[0x34] = (byte)(result[0x34] & ~0x40);

        for (var i = 0x23; i <= 0x33; i++) ClearIfErased(i); // fully unclaimed gap

        // AprsReportType/AnalogAprsPttMode/DigitalAprsPttMode/
        // DigitalAprsReportChannel/CorrectFrequency/DigitalEncryption/
        // AnalogAprsReportFrequencyIndex - plain byte values, only clear the
        // exact erased-flash sentinel, never a real (if unrecognized) value.
        // extra3b (0x3b) is a flag byte between these - only its unclaimed
        // bit 6 gets cleared, same treatment as digitalByte/extra34 above.
        for (var i = 0x35; i <= 0x3c; i++)
        {
            if (i == 0x3b)
            {
                result[i] = (byte)(result[i] & ~0x40);
            }
            else
            {
                ClearIfErased(i);
            }
        }

        for (var i = 0x64; i <= 0x7f; i++) ClearIfErased(i); // fully unclaimed gap
    }

    public static string ChannelTypeToString(byte raw) => raw switch
    {
        0 => "A-Analog",
        1 => "D-Digital",
        2 => "A+D TX A",
        3 => "D+A TX D",
        _ => "A-Analog"
    };

    public static string BandWidthToString(byte raw) => raw switch
    {
        0 => "12.5K",
        1 => "25K",
        _ => "12.5K"
    };

    // CORRECTED 2026-07-31: an earlier 2026-07-19 finding concluded DMO
    // Simplex and Repeater both map to DmrModeDcdm raw 0 with no way to
    // tell them apart - true as far as it went, but it never actually
    // isolated the "DMR Mode" dropdown itself, so it missed that a
    // separate bit (DmrMode, 0x34 bit 1) is what the vendor CPS actually
    // uses to distinguish them (confirmed via 2 clean live differential
    // tests today - see Encode's doc comment). DmrModeDcdm (0x21 bits 2-3)
    // is still a genuine 3-value field (0=Off/Normal, 1=Double Slot,
    // 2=TS Split, raw 3 unused/untested) - it just isn't the whole story
    // for what the vendor CPS's single 4-item dropdown shows.
    public static string DmrModeSelectionToString(byte dmrModeDcdm, bool dmrMode) => dmrModeDcdm switch
    {
        1 => "DCDM Double Slot",
        2 => "DCDM TS Split",
        _ => dmrMode ? "DMO/simplex" : "Repeater"
    };

    // DmrMode is null for the 2 DCDM options - what DmrMode's bit holds
    // while a DCDM mode is selected is unconfirmed, so parsing one of
    // those labels deliberately leaves it untouched rather than guessing.
    public static (byte DmrModeDcdm, bool? DmrMode)? ParseDmrModeSelection(string value) => value switch
    {
        "DCDM Double Slot" => ((byte)1, (bool?)null),
        "DCDM TS Split" => ((byte)2, (bool?)null),
        "DMO/simplex" => ((byte)0, (bool?)true),
        "Repeater" => ((byte)0, (bool?)false),
        _ => null
    };

    public static string TxPowerToString(byte raw) => raw switch
    {
        0 => "Low",
        1 => "Mid",
        2 => "High",
        3 => "Turbo",
        _ => "Low"
    };

    public static string OffsetDirectionToString(byte raw) => raw switch
    {
        0 => "None",
        1 => "+",
        2 => "-",
        _ => "None"
    };

    // Confirmed 2026-07-17 by a live differential test (real vendor CPS,
    // real hardware): 0=Off, 1=CTCSS, 2=DCS.
    public static string CtcssDcsModeToString(byte raw) => raw switch
    {
        1 => "CTCSS",
        2 => "DCS",
        _ => "Off"
    };

    // Reverse of CtcssDcsModeToString, used by the write path
    // (MainViewModel.RadioWrite.cs). Null for an unrecognized string -
    // a write should fail loudly rather than guess.
    public static byte? ParseCtcssDcsMode(string value) => value switch
    {
        "Off" => 0,
        "CTCSS" => 1,
        "DCS" => 2,
        _ => null
    };

    public static byte? ParseChannelType(string value) => value switch
    {
        "A-Analog" => 0,
        "D-Digital" => 1,
        "A+D TX A" => 2,
        "D+A TX D" => 3,
        _ => null
    };

    public static byte? ParseTxPower(string value) => value switch
    {
        "Low" => 0,
        "Mid" => 1,
        "High" => 2,
        "Turbo" => 3,
        _ => null
    };

    public static byte? ParseBandWidth(string value) => value switch
    {
        "12.5K" => 0,
        "25K" => 1,
        _ => null
    };

    public static byte? ParseOffsetDirection(string value) => value switch
    {
        "None" => 0,
        "+" => 1,
        "-" => 2,
        _ => null
    };

    // Confirmed 2026-07-17 via a live differential test: 10 spare channels
    // were configured in the REAL vendor CPS with specific field
    // combinations, written to a real D890UV, then read back
    // read-only and compared. All 5 official Squelch Mode options
    // (Field_Reference.md) were reproduced by cycling raw 0-4 in sequence -
    // confirming this is a 3-bit field (see Decode), not the 2-bit field
    // originally assumed. Moved here from RadioReadMapper - a channel-field
    // label/parse pair belongs with the rest of them.
    public static string SquelchModeToString(byte raw) => raw switch
    {
        0 => "Carrier",
        1 => "CTCSS/DCS",
        2 => "Optional Signal",
        3 => "CTC/DCS&Optional Signal",
        4 => "CTC/DCS|Optional Signal",
        _ => "Carrier"
    };

    public static byte? ParseSquelchMode(string value) => value switch
    {
        "Carrier" => 0,
        "CTCSS/DCS" => 1,
        "Optional Signal" => 2,
        "CTC/DCS&Optional Signal" => 3,
        "CTC/DCS|Optional Signal" => 4,
        _ => null
    };

    // Confirmed 2026-07-17 via the same live differential test: setting
    // Optional Signal = 5Tone (the last of 4 options) produced raw 3,
    // confirming the original 0=Off/1=DTMF/2=2Tone/3=5Tone mapping was
    // already correct.
    // Extended 2026-08-01: QDC1200 (raw 4) confirmed via a live
    // differential test - see the OptionalSignal decode comment above.
    public static string OptionalSignalToString(byte raw) => raw switch
    {
        0 => "Off",
        1 => "DTMF",
        2 => "2Tone",
        3 => "5Tone",
        4 => "QDC1200",
        _ => "Off"
    };

    public static byte? ParseOptionalSignal(string value) => value switch
    {
        "Off" => 0,
        "DTMF" => 1,
        "2Tone" => 2,
        "5Tone" => 3,
        "QDC1200" => 4,
        _ => null
    };

    // CORRECTED 2026-07-31 - the 2026-07-17 "one shared 4-value enum, only
    // index 0's label differs" finding was wrong. A direct vendor CPS
    // comparison found analog channels only have 3 real values with their
    // own distinct raw mapping (Off=0, Different CDT=1, Channel Free=2) -
    // NOT a relabeled subset of digital's 4-value list (Always=0, Channel
    // Free=1, Different Color Code=2, Same Color Code=3). Independently
    // confirmed against the xbenkozx/anytone-cps reference source
    // (constants.cpp): `Constants::BUSY_LOCK = {"Off", "Different CDT",
    // "Channel Free"}` - exactly this 3-item analog list, in this exact
    // order. Digital's own 4-item list is unchanged/still correct (matches
    // a fresh comparison too) - only analog's mapping was wrong.
    public static string BusyLockToString(byte raw, bool isDigital) => isDigital
        ? raw switch
        {
            0 => "Always",
            1 => "Channel Free",
            2 => "Different Color Code",
            3 => "Same Color Code",
            _ => "Always"
        }
        : raw switch
        {
            0 => "Off",
            1 => "Different CDT",
            2 => "Channel Free",
            _ => "Off"
        };

    public static byte? ParseBusyLock(string value, bool isDigital) => isDigital
        ? value switch
        {
            "Always" => 0,
            "Channel Free" => 1,
            "Different Color Code" => 2,
            "Same Color Code" => 3,
            _ => null
        }
        : value switch
        {
            "Off" => 0,
            "Different CDT" => 1,
            "Channel Free" => 2,
            _ => null
        };

    public static string PttIdToString(byte raw) => raw switch
    {
        0 => "Off",
        1 => "Start",
        2 => "End",
        3 => "Start&End",
        _ => "Off"
    };

    public static byte? ParsePttId(string value) => value switch
    {
        "Off" => 0,
        "Start" => 1,
        "End" => 2,
        "Start&End" => 3,
        _ => null
    };

    /// <summary>Patch for <see cref="Encode"/>: only the non-null fields are
    /// applied, everything else in the target record is left byte-identical
    /// to whatever was freshly read. See <see cref="Encode"/>'s doc comment
    /// for exactly which fields are safe to set here and why.</summary>
    public sealed record ChannelFieldPatch
    {
        public string? Name { get; init; }
        public double? RxFrequencyMHz { get; init; }
        public double? OffsetMHz { get; init; }
        public byte? OffsetDirection { get; init; }
        public byte? CtcssDcsEncode { get; init; }
        public byte? CtcssDcsDecode { get; init; }
        public byte? CtcssEncodeTone { get; init; }
        public byte? CtcssDecodeTone { get; init; }
        public ushort? DcsEncodeTone { get; init; }
        public ushort? DcsDecodeTone { get; init; }
        public byte? SquelchMode { get; init; }
        public byte? OptionalSignal { get; init; }
        public byte? BusyLock { get; init; }

        // Confirmed write-safe via a live differential test 2026-07-19
        // (real vendor CPS write to real hardware, exact match on
        // read-back).
        public ushort? ContactIndex { get; init; }
        public byte? RadioIdIndex { get; init; }
        public byte? PttId { get; init; }
        public byte? ReceiveGroupCallListIndex { get; init; }

        // Round 1, confirmed write-safe via a live differential test
        // 2026-07-19 (real vendor CPS write, exact match on read-back).
        public byte? ChannelType { get; init; }
        public byte? TransmitPower { get; init; }
        public byte? Bandwidth { get; init; }

        // Round 2, confirmed write-safe via a live differential test
        // 2026-07-19 (real vendor CPS write, exact match on read-back).
        public bool? TalkAround { get; init; }
        public bool? CallConfirmation { get; init; }
        public bool? PttProhibit { get; init; }
        public bool? Reverse { get; init; }

        // Round 5, confirmed write-safe via a live differential test
        // 2026-07-19. RX and TX color code are independent fields (byte
        // 0x20 and 0x43 respectively) - not the same value, as previously
        // assumed.
        public byte? RxColorCode { get; init; }
        public byte? TxColorCode { get; init; }

        // Round 6/7/8/9, confirmed write-safe via a combined live
        // differential test 2026-07-19 (real vendor CPS write, exact match
        // on read-back).
        public bool? WorkAlone { get; init; }
        public bool? SlotSuit { get; init; }
        public bool? RepeaterSlot2 { get; init; }
        public byte? DmrModeDcdm { get; init; }
        public bool? SmsConfirmation { get; init; }
        public byte? AesEncryptionIndex { get; init; }
        public byte? Arc4EncryptionKeyIndex { get; init; }
        public bool? AutoScan { get; init; }
        // Confirmed write-safe via a live differential test 2026-07-31 -
        // see Encode's doc comment. Bit 1 of 0x34.
        public bool? DmrMode { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. Bit 5 of 0x21.
        public bool? AprsRx { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. 0-based index into the M1-M16 DTMF ID
        // list, byte 0x1f.
        public byte? DtmfIdIndex { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. 0-based index into the 2Tone settings
        // list, byte 0x1d.
        public byte? Tone2IdIndex { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. 0-based index into the 5Tone settings
        // list, byte 0x1e.
        public byte? Tone5IdIndex { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. 0-based, byte 0x12 - only raw 0/1
        // tested, full range not independently confirmed.
        public byte? Tone2Decode { get; init; }
        // Confirmed write-safe via live differential tests 2026-08-01/
        // 2026-08-02 - see Encode's doc comment. Presets "1"/"2" are raw
        // 0/1, "Customize" is raw 100 (sentinel). Bytes 0x40/0x41.
        public byte? R5ToneBot { get; init; }
        public byte? R5ToneEot { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. 0-based index into the QDC1200 ID
        // list, byte 0x42 (previously unclaimed).
        public byte? QdcIdIndex { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. Bit 5 of 0x3b - false=AES, true=ARC4.
        public bool? ExtendEncryption { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. Bit 7 of 0x3b - false=Off, true=Low
        // priority. "High priority"'s encoding is unconfirmed.
        public bool? TxInterrupt { get; init; }
        public byte? ScrambleMode { get; init; }
        public byte? CustomScrambleFrequencyIndex { get; init; }

        // Round 7/8/9 follow-ups, confirmed write-safe via a live
        // differential test 2026-07-19.
        public byte? DigitalEncryptionIndex { get; init; }
        public byte? CorrectFrequencyHz { get; init; }
        public ushort? CustomCtcss { get; init; }

        // Confirmed write-safe via a live differential test 2026-07-31.
        public bool? DmrCrcIgnore { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-01 -
        // see Encode's doc comment. Bit 5 of 0x34.
        public bool? IdleTx { get; init; }
        public bool? SendTalkerAlias { get; init; }
        public bool? SmsForbid { get; init; }
        public bool? DataAckDisable { get; init; }
        public bool? ExcludeChannelRoaming { get; init; }
        public bool? AesRandomKey { get; init; }
        public bool? AesMultipleKey { get; init; }
        // Confirmed write-safe via a live differential test 2026-08-02 -
        // see Encode's doc comment. Bit 0 of 0x34.
        public bool? Ranging { get; init; }
    }

    public sealed record DecodedChannel(int Index)
    {
        public bool IsBlank { get; init; }
        public double RxFrequencyMHz { get; init; }
        public double OffsetMHz { get; init; }
        public byte OffsetDirection { get; init; }
        public byte BandWidth { get; init; }
        public byte TxPower { get; init; }
        public byte ChannelType { get; init; }

        public bool Talkaround { get; init; }
        public bool CallConfirmation { get; init; }
        public bool PttProhibit { get; init; }
        public bool Reverse { get; init; }
        public byte CtcssDcsEncode { get; init; }
        public byte CtcssDcsDecode { get; init; }

        public byte CtcssEncodeTone { get; init; }
        public byte CtcssDecodeTone { get; init; }
        public ushort DcsEncodeTone { get; init; }
        public ushort DcsDecodeTone { get; init; }
        public ushort CustomCtcss { get; init; }
        public byte Tone2Decode { get; init; }
        public ushort ContactIndex { get; init; }

        public byte RadioIdIndex { get; init; }
        public byte SquelchMode { get; init; }
        public byte PttId { get; init; }
        public byte OptionalSignal { get; init; }
        public byte BusyLock { get; init; }
        public byte ScanListIndex { get; init; }
        public byte ReceiveGroupCallListIndex { get; init; }
        public byte Tone2IdIndex { get; init; }
        public byte Tone5IdIndex { get; init; }
        public byte DtmfIdIndex { get; init; }
        public byte RxColorCode { get; init; }

        public bool WorkAlone { get; init; }
        public bool AprsRx { get; init; }
        public bool SlotSuit { get; init; }
        public byte DmrModeDcdm { get; init; }
        public bool TimeSlot { get; init; }
        public bool SmsConfirmation { get; init; }

        public byte AesEncryptionIndex { get; init; }

        public bool DmrCrcIgnore { get; init; }
        public bool IdleTx { get; init; }
        public bool AutoScan { get; init; }
        public bool DataAckDisable { get; init; }
        public bool ExcludeChannelRoaming { get; init; }
        public bool DmrMode { get; init; }
        public bool Ranging { get; init; }

        public byte AprsReportType { get; init; }
        public byte AnalogAprsPttMode { get; init; }
        public byte DigitalAprsPttMode { get; init; }
        public byte DigitalAprsReportChannel { get; init; }
        public byte CorrectFrequency { get; init; }
        public byte DigitalEncryption { get; init; }

        public bool TxInterrupt { get; init; }
        public bool ExtendEncryption { get; init; }
        public bool SendTalkerAlias { get; init; }
        public bool AnalogAprsMute { get; init; }
        public bool SmsForbid { get; init; }
        public bool AesRandomKey { get; init; }
        public bool AesMultipleKey { get; init; }

        public byte AnalogAprsReportFrequencyIndex { get; init; }
        public byte Arc4EncryptionKeyIndex { get; init; }
        public byte ScramblerSet { get; init; }
        public byte CustomScrambler { get; init; }
        public byte R5ToneBot { get; init; }
        public byte R5ToneEot { get; init; }
        public byte QdcIdIndex { get; init; }
        public byte TxColorCode { get; init; }

        public string Name { get; init; } = "";
    }
}
