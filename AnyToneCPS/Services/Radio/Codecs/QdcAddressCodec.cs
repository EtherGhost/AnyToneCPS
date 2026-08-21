using System;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// QDC Address Book - a flat 128-slot array, 0x30 (48) bytes each,
/// starting at D890UvMemoryMap.QdcAddressData. No bitmap or presence list
/// found anywhere nearby - presence is inferred the same way as Analog
/// Address Book (a blank record, all-0xFF, is treated as unconfigured -
/// see this class's own RecordLength doc comment on D890UvMemoryMap for
/// why 0xFF and not 0x00 here, unlike Qdc1200IdCodec).
///
/// See D890UvMemoryMap.QdcAddressData's own doc comment for the full
/// confirmation story (one live differential WRITE capture plus one READ
/// capture, 2026-08-04). Byte layout is an exact match for Qdc1200IdCodec:
/// Type(0)/CallType(1)/Ack(2)/pad(3)/GroupID(4-5)/PrivateID(6-7)/Name(8+),
/// with Type's ALEART=2 raw byte independently confirmed here too. Only
/// Private Call/ALEART were directly exercised - Group/All Call's raw
/// byte and the rest of the Type code table are inherited from
/// Qdc1200IdCodec's own confirmed values, not independently re-verified
/// for this address.
/// </summary>
public static class QdcAddressCodec
{
    public const int RecordLength = D890UvMemoryMap.QdcAddressRecordLength;
    public const int SlotCount = CodeplugLimits.QdcAddressMax;
    private const int NameLength = RecordLength - 8; // 40 bytes = 20 UTF-16LE characters

    public static DecodedQdcAddress Decode(ReadOnlySpan<byte> data, int index)
    {
        var type = data[0];
        var callType = data[1];
        var ack = data[2] != 0;

        var groupCallIdHex = ReverseHexCodec.Decode(data.Slice(4, 2));
        var groupCallId = groupCallIdHex.Length >= 4 ? groupCallIdHex.Substring(1, 3) : groupCallIdHex;
        var privateCallId = ReverseHexCodec.Decode(data.Slice(6, 2));

        var name = TextFieldCodec.DecodeName(data.Slice(8, NameLength));

        return new DecodedQdcAddress(index)
        {
            Type = type,
            CallType = callType,
            Ack = ack,
            GroupCallId = groupCallId,
            PrivateCallId = privateCallId,
            Name = name
        };
    }

    /// <summary>Encodes every confirmed field into a copy of <paramref name="currentRecord"/>,
    /// leaving byte 3 untouched - same "preserve the unknown bytes"
    /// discipline as Qdc1200IdCodec.Encode. An empty Private/Group Call ID
    /// encodes as "0000"/"000" (no confirmed "Off" sentinel exists for
    /// these fields, matching Qdc1200IdCodec's own documented gap).</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedQdcAddress values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"QDC Address record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[0] = values.Type;
        result[1] = values.CallType;
        result[2] = (byte)(values.Ack ? 1 : 0);

        var groupDigits = string.IsNullOrEmpty(values.GroupCallId) ? "000" : values.GroupCallId.PadLeft(3, '0');
        ReverseHexCodec.Encode("0" + groupDigits).CopyTo(result, 4);

        var privateDigits = string.IsNullOrEmpty(values.PrivateCallId) ? "0000" : values.PrivateCallId.PadLeft(4, '0');
        ReverseHexCodec.Encode(privateDigits).CopyTo(result, 6);

        TextFieldCodec.EncodeName(values.Name, NameLength).CopyTo(result, 8);

        return result;
    }

    public sealed record DecodedQdcAddress(int Index)
    {
        public byte Type { get; init; }
        public byte CallType { get; init; }
        public bool Ack { get; init; }
        public string GroupCallId { get; init; } = "";
        public string PrivateCallId { get; init; } = "";
        public string Name { get; init; } = "";
    }
}
