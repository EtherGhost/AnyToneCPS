using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// QDC 1200 Setting &gt; Encode tab's ID table - a flat 100-slot array,
/// 0x40 (64) bytes each, starting at D890UvMemoryMap.Qdc1200IdData. No
/// bitmap or presence list found anywhere nearby in either capture -
/// presence is inferred the same way as Auto Repeater Offset/Analog Quick
/// Call (a blank record, all-zero, is treated as unconfigured).
///
/// Confirmed 2026-08-04 via TWO live differential WRITE captures (searched
/// blind - no reference project data exists at all for this entity, see
/// Qdc1200SettingsEntry's class doc comment):
///
/// - Type (byte 0): an ABSOLUTE/shared code across BOTH Call Types, not a
///   fresh 0-based index per option list - "ALEART" read back as the same
///   raw byte (2) whether Call Type was Private or Group. Only ALEART=2 is
///   directly confirmed; see Qdc1200IdEntry's own absolute-code constants
///   for the rest, which assume Private's own display order is the
///   underlying enum (unconfirmed).
/// - Call Type (byte 1): 0=Private Call, 1=Group Call, confirmed by
///   switching a row from one to the other between the two captures.
///   2=All Call is inferred, not directly confirmed.
/// - Need to Answer (byte 2): a plain bool, confirmed both sides (1 for
///   Private+ALEART with it enabled, 0 once switched to Group Call+ALEART
///   where the real vendor CPS disables the control entirely).
/// - Group Call ID (bytes 4-5) and Private Call ID (bytes 6-7): SEPARATE,
///   INDEPENDENT byte slots, each a 2-byte reverse-byte-order hex string -
///   same "ReverseHexCodec" trick as AlarmSettingsCodec's own QDC Group/
///   Private ID fields. Group Call ID is padded to 4 hex chars with a
///   leading 0 before reversing (same as AlarmSettingsCodec.QdcGroupId),
///   Private Call ID uses its full 4 chars directly. Confirmed NOT cleared
///   on the wire when Call Type switches away from them - a stale
///   "5564" Private Call ID survived a write that changed Call Type to
///   Group and set a different Group Call ID.
/// - Name (byte 8, UTF-16LE, 12 chars/24 bytes) - decoded/encoded exactly
///   like every other name field via TextFieldCodec.
///
/// Byte 3 and everything past the Name field (byte 32 onward) were always
/// observed as zero in both captures - unconfirmed, assumed unused/padding.
/// </summary>
public static class Qdc1200IdCodec
{
    public const int RecordLength = 0x40;
    public const int SlotCount = 100;
    private const int NameLength = 24; // 12 chars x 2 bytes/char UTF-16LE

    public static DecodedQdc1200Id Decode(ReadOnlySpan<byte> data, int index)
    {
        var type = data[0];
        var callType = data[1];
        var needToAnswer = data[2] != 0;

        var groupCallIdHex = ReverseHexCodec.Decode(data.Slice(4, 2));
        var groupCallId = groupCallIdHex.Length >= 4 ? groupCallIdHex.Substring(1, 3) : groupCallIdHex;
        var privateCallId = ReverseHexCodec.Decode(data.Slice(6, 2));

        var name = TextFieldCodec.DecodeName(data.Slice(8, NameLength));

        return new DecodedQdc1200Id(index)
        {
            Type = type,
            CallType = callType,
            NeedToAnswer = needToAnswer,
            GroupCallId = groupCallId,
            PrivateCallId = privateCallId,
            Name = name
        };
    }

    /// <summary>Encodes every confirmed field into a copy of <paramref name="currentRecord"/>,
    /// leaving byte 3 and the unknown tail (byte 32 onward) untouched -
    /// same "preserve the unknown bytes" discipline as AmAirCodec.Encode.
    /// An empty Private/Group Call ID encodes as "0000"/"000" (no
    /// confirmed "Off" sentinel exists for these fields, unlike Content/
    /// CallObject elsewhere in this app - see this class's own doc
    /// comment).</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedQdc1200Id values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"QDC 1200 ID record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[0] = values.Type;
        result[1] = values.CallType;
        result[2] = (byte)(values.NeedToAnswer ? 1 : 0);

        var groupDigits = string.IsNullOrEmpty(values.GroupCallId) ? "000" : values.GroupCallId.PadLeft(3, '0');
        ReverseHexCodec.Encode("0" + groupDigits).CopyTo(result, 4);

        var privateDigits = string.IsNullOrEmpty(values.PrivateCallId) ? "0000" : values.PrivateCallId.PadLeft(4, '0');
        ReverseHexCodec.Encode(privateDigits).CopyTo(result, 6);

        TextFieldCodec.EncodeName(values.Name, NameLength).CopyTo(result, 8);

        return result;
    }

    public sealed record DecodedQdc1200Id(int Index)
    {
        public byte Type { get; init; }
        public byte CallType { get; init; }
        public bool NeedToAnswer { get; init; }
        public string GroupCallId { get; init; } = "";
        public string PrivateCallId { get; init; } = "";
        public string Name { get; init; } = "";
    }
}
