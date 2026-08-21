using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for the D890UV's Alarm/Emergency settings - a single
/// instance, not a list, like Master ID/Talk Alias Settings. Byte layout
/// transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (alarm_settings.cpp, decode_D890UV).
///
/// Reads from 3 separate addresses (not one contiguous record) - matches
/// the reference's own call shape exactly:
/// <c>decode_D890UV(data_3483000, data_3482e00, data_3500000)</c>.
///
/// Fields NOT ported (left out of <see cref="DecodedAlarmSettings"/>
/// entirely, matching what the reference itself does for this model):
/// <c>work_mode_voice_switch</c>/<c>work_mode_area_switch</c>/
/// <c>work_mode_mic_switch</c> are commented out in the reference's own
/// decode_D890UV (only ever set by decode_D878UVII), and <c>qdc_call_type</c>
/// is declared on the class but never assigned by either decode method for
/// any model - so none of these four have any real data source to trust.
/// </summary>
public static class AlarmSettingsCodec
{
    public const int Data3483000Length = 0x30;
    public const int Data3482e00Length = 0x10;
    public const int Data3500000Length = 0x50;

    public static DecodedAlarmSettings Decode(
        ReadOnlySpan<byte> data3483000,
        ReadOnlySpan<byte> data3482e00,
        ReadOnlySpan<byte> data3500000)
    {
        // (unverified) - the reference assigns this 2-byte little-endian
        // read into a uint8_t field, silently truncating any value above
        // 255. Since channel indices on this radio go well past 255, that
        // looks like a real upstream bug for anyone using a high channel
        // number here. Ported as the full untruncated value rather than
        // reproducing the truncation, since we have no confirmed real data
        // above 255 to know which behavior actually matches the radio.
        var analogEmergencyChannel = BinaryPrimitives.ReadUInt16LittleEndian(data3483000.Slice(0x6, 2));

        // Confirmed via live USB capture 2026-08-04 (see this class's own
        // write-side doc comment on EncodeD3483000): a genuine 2-byte little-
        // endian field at 0xe-0xf, NOT the single byte the reference project's
        // own decode_D890UV reads (matching Analog's own field shape at
        // 0x6-0x7) - the reference's decode/encode asymmetry for this one
        // field was a reference bug, not a real 1-byte radio field.
        var digitalEmergencyChannel = BinaryPrimitives.ReadUInt16LittleEndian(data3483000.Slice(0xe, 2));

        var qdcGroupIdHex = ReverseHex(data3483000.Slice(0x18, 2));
        var qdcPrivateIdHex = ReverseHex(data3483000.Slice(0x1a, 2));

        return new DecodedAlarmSettings
        {
            AnalogEmergencyAlarm = data3483000[0x0],
            AnalogEniType = data3483000[0x1],
            AnalogEmergencyId = data3483000[0x2],
            AnalogAlarmTime = data3483000[0x3],
            AnalogTxDuration = data3483000[0x4],
            AnalogRxDuration = data3483000[0x5],
            AnalogEmergencyChannel = analogEmergencyChannel,
            AnalogEniSend = data3483000[0x8],
            AnalogEmergencyCycle = data3483000[0x9],

            DigitalEmergencyAlarm = data3483000[0xa],
            DigitalAlarmTime = data3483000[0xb],
            DigitalTxDuration = data3483000[0xc],
            DigitalRxDuration = data3483000[0xd],
            DigitalEmergencyChannel = digitalEmergencyChannel,
            DigitalEmergencyCycle = data3483000[0x11],
            DigitalEniSend = data3483000[0x10],
            DigitalCallType = data3482e00[0x0],
            DigitalTgDmrId = BcdDecimalCodec.DecodeAsDecimal(data3482e00.Slice(0x02, 4)),

            ReceiveAlarm = data3483000[0x15] != 0,
            ManDown = data3500000[0x24] != 0,
            ManDownDelay = data3500000[0x4f],

            WorkAloneResponseTime = data3483000[0x12],
            WorkAloneWarningTime = data3483000[0x13],
            WorkAloneResponse = data3483000[0x14],

            // .mid(1, 3): skip the first hex character, keep the next 3 -
            // matches the reference's own odd nibble-trimming exactly.
            QdcGroupId = qdcGroupIdHex.Length >= 4 ? qdcGroupIdHex.Substring(1, 3) : qdcGroupIdHex,
            QdcPrivateId = qdcPrivateIdHex
        };
    }

    /// <summary>Reverses 2 bytes (endian swap) then hex-encodes them -
    /// matches the reference's `std::reverse` + `toHex()` combination used
    /// for both QDC id fields.</summary>
    private static string ReverseHex(ReadOnlySpan<byte> twoBytes)
    {
        Span<byte> reversed = stackalloc byte[2];
        reversed[0] = twoBytes[1];
        reversed[1] = twoBytes[0];
        return Convert.ToHexString(reversed);
    }

    /// <summary>Inverse of <see cref="ReverseHex"/>: hex-decodes a 4-char
    /// string then reverses the 2 resulting bytes back into wire order.
    /// Confirmed exactly by a live write/read-back capture 2026-08-04 (see
    /// <see cref="EncodeD3483000"/>'s doc comment).</summary>
    private static byte[] EncodeReverseHex(string fourCharHex)
    {
        var bytes = Convert.FromHexString(fourCharHex);
        return [bytes[1], bytes[0]];
    }

    /// <summary>RMW encode for the data_3483000 block (Analog/Digital Alarm,
    /// Work Alone, Receive Alarm, QDC Group/Private ID) - every other byte
    /// in this 0x30-byte record (the 0x16-0x17 gap, 0x1c-0x2f) is left
    /// untouched, same discipline as <see cref="OptionalSettingsCodec.EncodeMain"/>.
    /// Confirmed field-for-field by 4 live USB write captures 2026-08-04
    /// (all Analog/Digital/Work Alone/Man Down/QDC Group ID fields in one
    /// combined write, then two more isolating Analog Emergency ID and QDC
    /// Private ID respectively) - every offset below matches <see cref="Decode"/>
    /// exactly, including the QDC Group ID's odd "reverse-hex then keep only
    /// the last 3 of 4 hex chars" shape (the discarded first hex character
    /// is always re-derived as '0' on encode, matching every real capture -
    /// this radio only ever uses a 12-bit Group ID) and the Digital Emergency
    /// Channel 2-byte fix (see <see cref="Decode"/>'s own doc comment on that
    /// field). QDC "Kind" (<see cref="AlarmSettingsEntry.QdcCallType"/>) is
    /// deliberately NOT encoded anywhere - a dedicated live differential test
    /// (2 writes differing ONLY in Kind + Private ID) confirmed it has no
    /// byte representation anywhere in the codeplug at all, matching the
    /// reference project's own finding.</summary>
    public static byte[] EncodeD3483000(ReadOnlySpan<byte> current, DecodedAlarmSettings values)
    {
        if (current.Length != Data3483000Length)
        {
            throw new ArgumentException($"Alarm Settings data_3483000 record must be exactly {Data3483000Length} bytes.", nameof(current));
        }

        var result = current.ToArray();

        result[0x0] = values.AnalogEmergencyAlarm;
        result[0x1] = values.AnalogEniType;
        result[0x2] = values.AnalogEmergencyId;
        result[0x3] = values.AnalogAlarmTime;
        result[0x4] = values.AnalogTxDuration;
        result[0x5] = values.AnalogRxDuration;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x6, 2), values.AnalogEmergencyChannel);
        result[0x8] = values.AnalogEniSend;
        result[0x9] = values.AnalogEmergencyCycle;

        result[0xa] = values.DigitalEmergencyAlarm;
        result[0xb] = values.DigitalAlarmTime;
        result[0xc] = values.DigitalTxDuration;
        result[0xd] = values.DigitalRxDuration;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0xe, 2), values.DigitalEmergencyChannel);
        result[0x10] = values.DigitalEniSend;
        result[0x11] = values.DigitalEmergencyCycle;

        result[0x12] = values.WorkAloneResponseTime;
        result[0x13] = values.WorkAloneWarningTime;
        result[0x14] = values.WorkAloneResponse;

        result[0x15] = (byte)(values.ReceiveAlarm ? 1 : 0);

        EncodeReverseHex("0" + values.QdcGroupId.PadLeft(3, '0')).CopyTo(result.AsSpan(0x18, 2));
        EncodeReverseHex(values.QdcPrivateId.PadLeft(4, '0')).CopyTo(result.AsSpan(0x1a, 2));

        return result;
    }

    /// <summary>RMW encode for the data_3482e00 block (Digital Call Type,
    /// Digital TG/DMR ID) - confirmed by the same live captures as
    /// <see cref="EncodeD3483000"/>.</summary>
    public static byte[] EncodeD3482e00(ReadOnlySpan<byte> current, DecodedAlarmSettings values)
    {
        if (current.Length != Data3482e00Length)
        {
            throw new ArgumentException($"Alarm Settings data_3482e00 record must be exactly {Data3482e00Length} bytes.", nameof(current));
        }

        var result = current.ToArray();
        result[0x0] = values.DigitalCallType;
        BcdDecimalCodec.EncodeAsDecimal(values.DigitalTgDmrId, 4).CopyTo(result.AsSpan(0x02, 4));
        return result;
    }

    /// <summary>RMW encode touching ONLY Man Down (0x24) and Man Down Delay
    /// (0x4f) within the data_3500000 block - this base address is shared
    /// with Talk Alias Settings and (at a much larger 0x160-byte length)
    /// Optional Settings' own Power-on record, so every other byte here is
    /// left strictly untouched, same discipline as every other RMW encode in
    /// this app. <paramref name="current"/> is expected to be exactly
    /// <see cref="Data3500000Length"/> bytes - <see cref="RadioCodeplugPatcher.ApplyPatch"/>
    /// slices that much out of whichever larger captured region actually
    /// contains this address before calling in.</summary>
    public static byte[] EncodeD3500000(ReadOnlySpan<byte> current, DecodedAlarmSettings values)
    {
        if (current.Length != Data3500000Length)
        {
            throw new ArgumentException($"Alarm Settings data_3500000 record must be exactly {Data3500000Length} bytes.", nameof(current));
        }

        var result = current.ToArray();
        result[0x24] = (byte)(values.ManDown ? 1 : 0);
        result[0x4f] = values.ManDownDelay;
        return result;
    }

    public sealed record DecodedAlarmSettings
    {
        public byte AnalogEmergencyAlarm { get; init; }
        public byte AnalogEniType { get; init; }
        public byte AnalogEmergencyId { get; init; }
        public byte AnalogAlarmTime { get; init; }
        public byte AnalogTxDuration { get; init; }
        public byte AnalogRxDuration { get; init; }
        public ushort AnalogEmergencyChannel { get; init; }
        public byte AnalogEniSend { get; init; }
        public byte AnalogEmergencyCycle { get; init; }

        public byte DigitalEmergencyAlarm { get; init; }
        public byte DigitalAlarmTime { get; init; }
        public byte DigitalTxDuration { get; init; }
        public byte DigitalRxDuration { get; init; }
        public ushort DigitalEmergencyChannel { get; init; }
        public byte DigitalEmergencyCycle { get; init; }
        public byte DigitalEniSend { get; init; }
        public byte DigitalCallType { get; init; }
        public long DigitalTgDmrId { get; init; }

        public bool ReceiveAlarm { get; init; }
        public bool ManDown { get; init; }
        public byte ManDownDelay { get; init; }

        public byte WorkAloneResponseTime { get; init; }
        public byte WorkAloneWarningTime { get; init; }
        public byte WorkAloneResponse { get; init; }

        public string QdcGroupId { get; init; } = "";
        public string QdcPrivateId { get; init; } = "";
    }
}
