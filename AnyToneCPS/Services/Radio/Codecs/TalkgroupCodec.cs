using System;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder/encoder for a single D890UV Talkgroup (DMR contact) record. The
/// reference project's `encode_D890UV` allocates 0xc8 bytes for this
/// struct, but the actual on-device READ length is
/// D890UvMemoryMap.TalkgroupDataLength = 0x80 - that memory-map value is
/// trusted here since it's what the device-read code actually uses.
/// Byte offsets transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (contact.cpp, decode_D890UV) and cross-
/// validated against real hardware USB captures (that
/// reference's channel-name-field offset exactly matched an independent
/// USB capture, giving high confidence in the rest of its transcribed
/// offsets too).
///
/// The CallType/CallAlert VALUE mappings below are NOT from the reference
/// project - they were wrong there (or at least misread here originally) -
/// and were corrected 2026-08-07 via two live differential write captures:
/// byte 0x00 (CallType) is 0=Private/1=Group/2=All, and byte 0x01
/// (CallAlert) is a plain 0=None/1=Ring/2=Online Alert value byte, not a
/// bitmask as the old code here assumed. Full test data: row written as
/// Group Call/Online Alert came back 0x01/0x02; Private Call/Online Alert
/// came back 0x00/0x02; All Call (DMR ID and Call Alert both disabled in
/// the vendor UI) came back 0x02/0x00 with DMR ID clamped to 16777215;
/// Private Call/Ring came back 0x00/0x01.
/// </summary>
public static class TalkgroupCodec
{
    public const int RecordLength = D890UvMemoryMap.TalkgroupDataLength; // 0x80

    public static DecodedTalkgroup Decode(ReadOnlySpan<byte> data, int index)
    {
        var callType = CallTypeToString(data[0x00]);
        var callAlert = CallAlertToString(data[0x01]);
        var dmrId = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x02, 4));
        var name = TextFieldCodec.DecodeName(data.Slice(0x06, 0x20));

        return new DecodedTalkgroup(index)
        {
            CallType = callType,
            CallAlert = callAlert,
            DmrId = dmrId,
            Name = name
        };
    }

    /// <summary>Inverse of <see cref="Decode"/> - see this class's own doc
    /// comment for the live-capture confirmation. When CallType is "All
    /// Call" this forces the DMR ID and Call Alert bytes to the vendor CPS's
    /// own observed values (16777215 sentinel / None) regardless of
    /// <paramref name="values"/>, matching its disabled-controls behavior.
    /// Bytes 0x26-0x7F (beyond the 32-byte Name field) are left
    /// untouched.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedTalkgroup values)
    {
        var result = currentRecord.ToArray();

        var isAllCall = values.CallType == "All Call";
        result[0x00] = StringToCallTypeByte(values.CallType);
        result[0x01] = isAllCall ? (byte)0 : StringToCallAlertByte(values.CallAlert);

        var dmrId = isAllCall ? CodeplugLimits.TalkgroupAllCallDmrIdSentinel : values.DmrId;
        BcdDecimalCodec.EncodeAsDecimal(dmrId, 4).CopyTo(result, 0x02);
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x06);

        return result;
    }

    /// <summary>Maps the on-wire call_type byte to the exact strings used by
    /// MainViewModel.ContactCallTypes = ["Group Call", "Private Call", "All Call"].</summary>
    public static string CallTypeToString(byte raw) => raw switch
    {
        0 => "Private Call",
        1 => "Group Call",
        2 => "All Call",
        _ => "Group Call"
    };

    public static byte StringToCallTypeByte(string value) => value switch
    {
        "Private Call" => 0,
        "Group Call" => 1,
        "All Call" => 2,
        _ => 1
    };

    /// <summary>Maps the on-wire call_alert byte to the exact strings used
    /// by TalkgroupEntry.CallAlertOptions.</summary>
    public static string CallAlertToString(byte raw) => raw switch
    {
        0 => "None",
        1 => "Ring",
        2 => "Online Alert",
        _ => "None"
    };

    public static byte StringToCallAlertByte(string value) => value switch
    {
        "None" => 0,
        "Ring" => 1,
        "Online Alert" => 2,
        _ => 0
    };

    public sealed record DecodedTalkgroup(int Index)
    {
        public string CallType { get; init; } = "Group Call";
        public string CallAlert { get; init; } = "None";
        public long DmrId { get; init; }
        public string Name { get; init; } = "";
    }
}
