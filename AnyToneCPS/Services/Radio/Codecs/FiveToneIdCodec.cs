using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 5Tone Settings' ID table - one 0x40-byte record per row (see
/// D890UvMemoryMap.FiveToneIdData). Confirmed 2026-08-05/06 across 4 live
/// differential WRITE captures, blind-searched via the row's own Name field
/// as a grep anchor (same technique used for every other blind-search
/// entity).
///
/// Confirmed offsets: Standard (byte 0x01, index into the shared 15-item
/// list), Time Of Encode Tone (byte 0x03, raw value), Name (bytes 0x18-
/// 0x27, UTF-16LE, SPACE-padded to a fixed 8 characters - NOT zero-padded
/// like every other named entity in this codebase, confirmed both for a
/// real value ("TESTID1" -&gt; "TESTID1 ") and a genuinely blank one (8
/// literal spaces)). Bytes 0x00, 0x02, and 0x28-0x3F were never attributed
/// to any field across 4 rounds of testing - left untouched via the usual
/// RMW discipline.
///
/// Bytes 0x04-0x17 (20 bytes) hold the Special Call's own Encode ID, in
/// whichever of 3 wire shapes its Calling Type produces - see
/// <see cref="Decode"/>/<see cref="Encode"/> for the full per-shape logic.
/// There is NO separate stored "Calling Type" byte anywhere in this record
/// - decode identifies the shape purely from the bytes themselves (the
/// marker prefix for Send Message/PTTID, or its absence for everything
/// else), which is provably sufficient: the radio only ever cares about the
/// final Encode ID bytes, never how a human composed them (confirmed
/// directly - the Encode ID box is genuinely hand-editable free text
/// until &amp;Special Call is used once, so "manually typed" and "popup
/// composed" are the same kind of value on the wire).
/// </summary>
public static class FiveToneIdCodec
{
    public const int RecordLength = 0x40;

    /// <summary>Shared with BOT/EOT (FiveToneSettingsCodec) - confirmed
    /// byte-identical layout: same offset, same length, same marker-based
    /// shape. Only Standard's own offset differs between row/BOT/EOT.</summary>
    internal const int PackedRegionOffset = 0x04;
    internal const int PackedRegionLength = 0x14; // 20 bytes, 0x04-0x17
    internal const int NameOffset = 0x18;
    internal const int NameLength = 0x10; // 8 UTF-16LE chars

    /// <summary>Space-padded fixed-width UTF-16LE text, confirmed as the
    /// real wire convention for every 5Tone Name field (row-level and
    /// BOT/EOT alike) - unlike every other named entity in this app, which
    /// zero-pads (see TextFieldCodec). A field of all spaces (or all-0xFF/
    /// all-zero, just in case) decodes as blank.</summary>
    public static string DecodeName(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.Unicode.GetString(bytes).TrimEnd(' ', '\0');
        var allBlankByte = true;
        foreach (var b in bytes)
        {
            if (b != 0xFF)
            {
                allBlankByte = false;
                break;
            }
        }

        return allBlankByte ? "" : text;
    }

    /// <summary>Inverse of <see cref="DecodeName"/> - pads with SPACE
    /// (0x0020), not zero, to fill every unused character slot.</summary>
    public static byte[] EncodeName(string value, int byteLength)
    {
        var maxChars = byteLength / 2;
        var truncated = value.Length > maxChars ? value[..maxChars] : value;
        var padded = truncated.PadRight(maxChars, ' ');
        return Encoding.Unicode.GetBytes(padded);
    }

    public static DecodedFiveToneId Decode(ReadOnlySpan<byte> data, int index, int selfIdLength)
    {
        var standard = data[0x01];
        var timeOfEncodeTone = data[0x03];
        var name = DecodeName(data.Slice(NameOffset, NameLength));

        var (specialCall, encodeId) = DecodePackedRegion(data.Slice(PackedRegionOffset, PackedRegionLength), selfIdLength, pttIdUsesE6Formula: false);

        return new DecodedFiveToneId(index)
        {
            Standard = standard,
            TimeOfEncodeTone = timeOfEncodeTone,
            Name = name,
            EncodeId = encodeId,
            SpecialCall = specialCall
        };
    }

    /// <summary>Encodes every confirmed field into a copy of
    /// <paramref name="currentRecord"/>, leaving bytes 0x00, 0x02, and
    /// 0x28-0x3F untouched - same "preserve the unknown bytes" discipline
    /// as every other codec in this app.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedFiveToneId values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"5Tone ID record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[0x01] = values.Standard;
        result[0x03] = values.TimeOfEncodeTone;
        EncodeName(values.Name, NameLength).CopyTo(result, NameOffset);

        var packed = new byte[PackedRegionLength];
        EncodePackedRegion(packed, values.SpecialCall, values.EncodeId, pttIdUsesE6Formula: false);
        packed.CopyTo(result, PackedRegionOffset);

        return result;
    }

    /// <summary>Shared with BOT/EOT (FiveToneSettingsCodec), which pass
    /// <paramref name="pttIdUsesE6Formula"/>=true - their own PTTID formula
    /// is "E6"+compressed(OtherSideId), CONFIRMED DIFFERENT from the
    /// row-level table's own PTTID rule (Encode ID empty/disabled), so the
    /// row-level <see cref="Encode"/> above always passes false.</summary>
    internal static void EncodePackedRegion(Span<byte> region, FiveToneSpecialCallCodecValues specialCall, string encodeId, bool pttIdUsesE6Formula)
    {
        region.Clear();

        if (!specialCall.IsConfigured)
        {
            EncodeManualHex(region, encodeId);
            return;
        }

        switch (specialCall.CallingType)
        {
            case FiveToneCallingType.SendMessage:
                EncodeSendMessage(region, specialCall.OtherSideId, specialCall.Message);
                break;
            case FiveToneCallingType.Ani:
                EncodeAni(region, specialCall.OtherSideId, specialCall.IntervalSuffix);
                break;
            case FiveToneCallingType.PttId:
                if (pttIdUsesE6Formula)
                {
                    var head = FiveToneToneSequenceCodec.Compose(specialCall.OtherSideId, FiveToneToneSequenceCodec.PttIdMarker);
                    Convert.FromHexString(head).CopyTo(region);
                }
                // Else empty/disabled - region already cleared above.
                break;
        }
    }

    private static void EncodeSendMessage(Span<byte> region, string otherSideId, string message)
    {
        var head = FiveToneToneSequenceCodec.Compose(otherSideId, FiveToneToneSequenceCodec.SendMessageMarker);
        var headBytes = Convert.FromHexString(head);
        headBytes.CopyTo(region);

        var messageBytes = Encoding.ASCII.GetBytes(message);
        var messageSpan = region[headBytes.Length..];
        var copyLength = Math.Min(messageBytes.Length, messageSpan.Length);
        messageBytes.AsSpan(0, copyLength).CopyTo(messageSpan);
    }

    private static void EncodeAni(Span<byte> region, string otherSideId, string intervalSuffix)
    {
        var text = otherSideId + intervalSuffix + otherSideId;
        if (text.Length % 2 != 0)
        {
            // Not independently byte-confirmed - every live-tested ANI
            // example used "No stop" (no interval letter), which always
            // produces an even-length string (2xN digits). An interval
            // letter present makes the total odd; padded here the same
            // way the tone-sequence format pads (trailing 'E'), but this
            // specific case hasn't been verified against a real capture.
            text += "E";
        }

        var bytes = Convert.FromHexString(text);
        bytes.AsSpan(0, Math.Min(bytes.Length, region.Length)).CopyTo(region);
    }

    private static void EncodeManualHex(Span<byte> region, string encodeId)
    {
        if (string.IsNullOrEmpty(encodeId))
        {
            return;
        }

        var text = encodeId.Length % 2 != 0 ? encodeId + "0" : encodeId;
        var bytes = Convert.FromHexString(text);
        bytes.AsSpan(0, Math.Min(bytes.Length, region.Length)).CopyTo(region);
    }

    internal static (FiveToneSpecialCallCodecValues SpecialCall, string EncodeId) DecodePackedRegion(ReadOnlySpan<byte> region, int selfIdLength, bool pttIdUsesE6Formula)
    {
        if (IsAllZero(region))
        {
            return (FiveToneSpecialCallCodecValues.NotConfigured, "");
        }

        var hex = Convert.ToHexString(region);

        if (hex.StartsWith(FiveToneToneSequenceCodec.SendMessageMarker, StringComparison.OrdinalIgnoreCase)
            && selfIdLength > 0
            && FiveToneToneSequenceCodec.TryReverse(hex, FiveToneToneSequenceCodec.SendMessageMarker, selfIdLength) is { } otherSideId)
        {
            var headHexChars = HeadHexCharCount(selfIdLength);
            var headByteCount = headHexChars / 2;
            var messageBytes = region[headByteCount..];
            var message = DecodeAsciiTrimNul(messageBytes);
            var encodeId = otherSideId + " Information:" + message;
            return (new FiveToneSpecialCallCodecValues(FiveToneCallingType.SendMessage, otherSideId, message, ""), encodeId);
        }

        if (hex.StartsWith(FiveToneToneSequenceCodec.PttIdMarker, StringComparison.OrdinalIgnoreCase)
            && selfIdLength > 0
            && FiveToneToneSequenceCodec.TryReverse(hex, FiveToneToneSequenceCodec.PttIdMarker, selfIdLength) is { } pttOtherSideId)
        {
            // Row-level PTTID's own confirmed rule is "Encode ID empty" -
            // BOT/EOT's own confirmed rule is "E6"+OtherSideId (NOT
            // empty), matching FiveToneSettingsEntry.ComposeBotEotEncodeId's
            // own confirmed formula exactly. Real bug found 2026-08-06:
            // this branch used to hardcode "" unconditionally, which broke
            // a live radio read for a BOT record configured with PTTID -
            // it decoded a correct OtherSideId but showed an empty, locked
            // Encode ID box instead of "E6"+id.
            var pttEncodeId = pttIdUsesE6Formula ? FiveToneToneSequenceCodec.PttIdMarker + pttOtherSideId : "";
            return (new FiveToneSpecialCallCodecValues(FiveToneCallingType.PttId, pttOtherSideId, "", ""), pttEncodeId);
        }

        // No recognized marker - could be a real ANI value (plain digit
        // concatenation, no marker) or a manually hand-typed hex string.
        // Both are the same kind of wire value (confirmed: the Encode ID
        // box is genuinely free-hex-text until a popup is used once), so
        // this is decoded as raw/manual rather than guessed at as ANI -
        // matches "manual entry" state (SpecialCall not configured), with
        // the raw hex preserved in EncodeId either way.
        var trimmed = hex.TrimEnd('0');
        if (trimmed.Length % 2 != 0)
        {
            trimmed += "0";
        }

        return (FiveToneSpecialCallCodecValues.NotConfigured, trimmed);
    }

    private static int HeadHexCharCount(int digitCount)
    {
        var length = 2 + digitCount + 1;
        return length % 2 != 0 ? length + 1 : length;
    }

    private static string DecodeAsciiTrimNul(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        var slice = end >= 0 ? bytes[..end] : bytes;
        return Encoding.ASCII.GetString(slice);
    }

    private static bool IsAllZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (b != 0)
            {
                return false;
            }
        }

        return true;
    }

    public sealed record DecodedFiveToneId(int Index)
    {
        public byte Standard { get; init; }
        public byte TimeOfEncodeTone { get; init; }
        public string Name { get; init; } = "";
        public string EncodeId { get; init; } = "";
        public FiveToneSpecialCallCodecValues SpecialCall { get; init; } = FiveToneSpecialCallCodecValues.NotConfigured;
    }
}

/// <summary>Calling Type as understood by the wire codecs - deliberately
/// separate from FiveToneSpecialCallEntry's own byte constants (Models
/// layer) to keep Services/Radio/Codecs free of a Models dependency, same
/// layering every other codec in this namespace already follows.</summary>
public enum FiveToneCallingType
{
    SendMessage,
    Ani,
    PttId
}

/// <summary>Wire-level view of a Special Call's own fields - the codec
/// layer's equivalent of FiveToneSpecialCallEntry, translated at the
/// ViewModel/mapping boundary rather than referencing the Models type
/// directly.</summary>
public sealed record FiveToneSpecialCallCodecValues(FiveToneCallingType CallingType, string OtherSideId, string Message, string IntervalSuffix)
{
    public bool IsConfigured { get; init; } = true;

    public static FiveToneSpecialCallCodecValues NotConfigured { get; } = new(FiveToneCallingType.SendMessage, "", "", "") { IsConfigured = false };
}
