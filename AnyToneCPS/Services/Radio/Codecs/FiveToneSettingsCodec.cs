using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 5Tone Settings' Decode/Information ID/Encode singleton block plus PTT ID
/// Starting (BOT)/Ending (EOT). Originally confirmed 2026-08-05/06 across 5
/// live differential WRITE captures, but BOT's own address was WRONG in
/// that original pass - corrected 2026-08-16 after live capture proved
/// writing BOT's Standard/Encode ID through the real vendor CPS never
/// touched the originally-claimed address at all. See
/// D890UvMemoryMap's own doc comment for the corrected region layout and
/// the full story.
///
/// Does NOT cover Stop Code (never independently located) or Information
/// ID NO. itself (confirmed 2026-08-06: not a stored value at all - it's
/// a transient UI selector, picking which slot's own Function Option/
/// Function Decoding Response/Information ID/Function Name to view - see
/// FiveToneInfoIdSlotCodec for those 4 fields' own confirmed byte layout,
/// a SEPARATE small slot array from this singleton block, not covered
/// here).
///
/// BOT and EOT now share the identical internal layout (Standard at +0x01,
/// TimeOfEncodeTone at +0x03, packed Encode-ID/Special-Call region at
/// +0x04, same as the row-level ID table's own FiveToneIdCodec) - the
/// original "BOT's Standard sits at a different offset than EOT's" claim
/// was itself a symptom of BOT's wrong base address, not a real
/// difference. Neither has a Name field (see DecodeBotEot's own doc
/// comment) - unlike the row-level ID table, whose Name IS real.
/// </summary>
public static class FiveToneSettingsCodec
{
    private const int PresenceBitmapOffset = 0x00; // one bit per populated ID table row - managed by the patcher, not this codec
    private const int DecodingResponseOffset = 0x11;
    private const int DecodeStandardOffset = 0x12;
    private const int SelfIdOffset = 0x15;
    private const int SelfIdFieldLength = 7; // max Self ID length - see CodeplugLimits.FiveToneSelfIdMaxLength
    private const int TimeLapseAfterEncodeOffset = 0x1C;
    private const int PttIdPauseTimeOffset = 0x1D;
    private const int AutoResetTimeOffset = 0x1E;
    private const int FirstToneLengthOffset = 0x1F;
    private const int SideToneOffset = 0x20;
    private const int StopTimeLengthOffset = 0x23;
    private const int DecodeTimeMsOffset = 0x24;
    private const int FirstToneLengthAfterStopOffset = 0x25;
    private const int PretimeOffset = 0x26;
    private const int DecUnitBitmaskOffset = 0x27;
    private const int DispAnyIdOffset = 0x28;

    public static DecodedFiveToneSettings DecodeSingleton(ReadOnlySpan<byte> data)
    {
        var decUnitMask = data[DecUnitBitmaskOffset];

        return new DecodedFiveToneSettings
        {
            SelfId = DecodeSelfId(data.Slice(SelfIdOffset, SelfIdFieldLength)),
            DecodeStandard = data[DecodeStandardOffset],
            DecodingResponse = data[DecodingResponseOffset],
            DecodeTimeMs = data[DecodeTimeMsOffset] * 10,
            DecUnit1 = (decUnitMask & 0x01) != 0,
            DecUnit2 = (decUnitMask & 0x02) != 0,
            DecUnit3 = (decUnitMask & 0x04) != 0,
            DecUnit4 = (decUnitMask & 0x08) != 0,
            DecUnit5 = (decUnitMask & 0x10) != 0,
            DecUnit6 = (decUnitMask & 0x20) != 0,
            DecUnit7 = (decUnitMask & 0x40) != 0,
            DispAnyId = data[DispAnyIdOffset] != 0,
            Pretime = data[PretimeOffset] * 10,
            AutoResetTime = data[AutoResetTimeOffset],
            TimeLapseAfterEncode = data[TimeLapseAfterEncodeOffset] * 10,
            PttIdPauseTime = data[PttIdPauseTimeOffset] == 0 ? -1 : data[PttIdPauseTimeOffset],
            FirstToneLength = data[FirstToneLengthOffset] * 10,
            StopTimeLength = data[StopTimeLengthOffset] * 10,
            FirstToneLengthAfterStop = data[FirstToneLengthAfterStopOffset] * 10,
            SideTone = data[SideToneOffset] != 0
        };
    }

    /// <summary>Encodes every confirmed field into a copy of
    /// <paramref name="currentRecord"/>, leaving the presence bitmap
    /// (managed separately by the patcher, same as every other
    /// bitmap-backed entity in this app) and every other unattributed byte
    /// - including the whole Information ID/Function1 sub-area sitting
    /// past these offsets - untouched.</summary>
    public static byte[] EncodeSingleton(ReadOnlySpan<byte> currentRecord, DecodedFiveToneSettings values)
    {
        var result = currentRecord.ToArray();

        EncodeSelfId(values.SelfId).CopyTo(result, SelfIdOffset);
        result[DecodeStandardOffset] = values.DecodeStandard;
        result[DecodingResponseOffset] = values.DecodingResponse;
        result[DecodeTimeMsOffset] = (byte)(values.DecodeTimeMs / 10);

        var decUnitMask = 0;
        if (values.DecUnit1) decUnitMask |= 0x01;
        if (values.DecUnit2) decUnitMask |= 0x02;
        if (values.DecUnit3) decUnitMask |= 0x04;
        if (values.DecUnit4) decUnitMask |= 0x08;
        if (values.DecUnit5) decUnitMask |= 0x10;
        if (values.DecUnit6) decUnitMask |= 0x20;
        if (values.DecUnit7) decUnitMask |= 0x40;
        result[DecUnitBitmaskOffset] = (byte)decUnitMask;

        result[DispAnyIdOffset] = (byte)(values.DispAnyId ? 1 : 0);
        result[PretimeOffset] = (byte)(values.Pretime / 10);
        result[AutoResetTimeOffset] = (byte)values.AutoResetTime;
        result[TimeLapseAfterEncodeOffset] = (byte)(values.TimeLapseAfterEncode / 10);
        result[PttIdPauseTimeOffset] = values.PttIdPauseTime < 0 ? (byte)0 : (byte)values.PttIdPauseTime;
        result[FirstToneLengthOffset] = (byte)(values.FirstToneLength / 10);
        result[StopTimeLengthOffset] = (byte)(values.StopTimeLength / 10);
        result[FirstToneLengthAfterStopOffset] = (byte)(values.FirstToneLengthAfterStop / 10);
        result[SideToneOffset] = (byte)(values.SideTone ? 1 : 0);

        return result;
    }

    /// <summary>One raw digit-value byte per character (NOT ASCII, NOT
    /// nibble-packed hex), zero-padded after - confirmed via a real Self ID
    /// change between two live captures ("12345" -&gt; "67890" showed up as
    /// "01 02 03 04 05" -&gt; "06 07 08 09 00" at this exact offset). Trailing
    /// zero bytes are treated as unused padding on decode - this is
    /// ambiguous with a real trailing '0' digit (e.g. Self ID "50000"),
    /// which is NOT resolvable from the captures taken so far (both
    /// confirmed test values, 12345 and 67890, happen to have no trailing
    /// zero digits). Flagged, not fixable without another live test using a
    /// Self ID that legitimately ends in one or more zeros.</summary>
    private static string DecodeSelfId(ReadOnlySpan<byte> bytes)
    {
        // Self ID is confirmed to always be 5-7 digits (never shorter), so
        // trailing zero bytes are only padding once the count would drop
        // below 5 - this correctly keeps a genuine trailing '0' digit for
        // the (real, tested) 5-digit case, e.g. "67890" ("06 07 08 09 00"
        // + 2 more zero-padding bytes - trimming ALL trailing zeros would
        // wrongly cut it to "6789"). Does NOT fully resolve the same
        // ambiguity for a 6- or 7-digit Self ID that itself ends in one or
        // more zeros (e.g. "670000") - that needs a live test that hasn't
        // been run yet; flagged, not fixable from the captures taken so far.
        const int minDigits = 5;
        var length = bytes.Length;
        while (length > minDigits && bytes[length - 1] == 0)
        {
            length--;
        }

        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append((char)('0' + bytes[i]));
        }

        return sb.ToString();
    }

    private static byte[] EncodeSelfId(string digits)
    {
        var result = new byte[SelfIdFieldLength];
        for (var i = 0; i < digits.Length && i < SelfIdFieldLength; i++)
        {
            result[i] = (byte)(digits[i] - '0');
        }

        return result;
    }

    /// <summary>Standard's own offset within BOT/EOT's real record -
    /// CORRECTED 2026-08-16: live capture proved this is 0x01 for BOT too
    /// (not a separate 0x02 as originally, wrongly, documented), the exact
    /// same offset FiveToneIdCodec already confirmed for ID table rows.
    /// BOT and EOT share this identical internal layout now that both
    /// addresses are known - only the base address passed to
    /// <see cref="EncodeBotEot"/>/<see cref="DecodeBotEot"/> differs
    /// between them (see D890UvMemoryMap's own doc comment for both
    /// addresses).</summary>
    private const int StandardOffset = 0x01;
    private const int TimeOfEncodeToneOffset = 0x03;

    public static DecodedFiveToneBotEot DecodeBot(ReadOnlySpan<byte> data, int selfIdLength) => DecodeBotEot(data, selfIdLength);

    public static byte[] EncodeBot(ReadOnlySpan<byte> currentRecord, DecodedFiveToneBotEot values) => EncodeBotEot(currentRecord, values);

    public static DecodedFiveToneBotEot DecodeEot(ReadOnlySpan<byte> data, int selfIdLength) => DecodeBotEot(data, selfIdLength);

    public static byte[] EncodeEot(ReadOnlySpan<byte> currentRecord, DecodedFiveToneBotEot values) => EncodeBotEot(currentRecord, values);

    /// <summary>No Name field exists for BOT/EOT - CORRECTED 2026-08-16.
    /// The original claim ("Name confirmed on the wire, space-padded, same
    /// as the ID table's own Name") was a misreading of 5Tone ID row 100's
    /// OWN Name field, which happens to alias the address this record used
    /// to be (wrongly) based at - confirmed directly: no Name
    /// control exists anywhere in the vendor CPS's BOT/EOT tabs. Byte
    /// +0x00 and +0x02 are unattributed (same convention as
    /// FiveToneIdCodec's own row layout) and left untouched.</summary>
    private static DecodedFiveToneBotEot DecodeBotEot(ReadOnlySpan<byte> data, int selfIdLength)
    {
        var standard = data[StandardOffset];
        var timeOfEncodeTone = data[TimeOfEncodeToneOffset];
        var (specialCall, encodeId) = FiveToneIdCodec.DecodePackedRegion(data.Slice(FiveToneIdCodec.PackedRegionOffset, FiveToneIdCodec.PackedRegionLength), selfIdLength, pttIdUsesE6Formula: true);

        return new DecodedFiveToneBotEot
        {
            Standard = standard,
            TimeOfEncodeTone = timeOfEncodeTone,
            EncodeId = encodeId,
            SpecialCall = specialCall
        };
    }

    private static byte[] EncodeBotEot(ReadOnlySpan<byte> currentRecord, DecodedFiveToneBotEot values)
    {
        var result = currentRecord.ToArray();
        result[StandardOffset] = values.Standard;
        result[TimeOfEncodeToneOffset] = values.TimeOfEncodeTone;

        var packed = new byte[FiveToneIdCodec.PackedRegionLength];
        FiveToneIdCodec.EncodePackedRegion(packed, values.SpecialCall, values.EncodeId, pttIdUsesE6Formula: true);
        packed.CopyTo(result, FiveToneIdCodec.PackedRegionOffset);

        return result;
    }

    public sealed record DecodedFiveToneSettings
    {
        public string SelfId { get; init; } = "";
        public byte DecodeStandard { get; init; }
        public byte DecodingResponse { get; init; }
        public int DecodeTimeMs { get; init; }
        public bool DecUnit1 { get; init; }
        public bool DecUnit2 { get; init; }
        public bool DecUnit3 { get; init; }
        public bool DecUnit4 { get; init; }
        public bool DecUnit5 { get; init; }
        public bool DecUnit6 { get; init; }
        public bool DecUnit7 { get; init; }
        public bool DispAnyId { get; init; }
        public int Pretime { get; init; }
        public int AutoResetTime { get; init; }
        public int TimeLapseAfterEncode { get; init; }
        public int PttIdPauseTime { get; init; }
        public int FirstToneLength { get; init; }
        public int StopTimeLength { get; init; }
        public int FirstToneLengthAfterStop { get; init; }
        public bool SideTone { get; init; }
    }

    public sealed record DecodedFiveToneBotEot
    {
        public byte Standard { get; init; }
        public byte TimeOfEncodeTone { get; init; }
        public string EncodeId { get; init; } = "";
        public FiveToneSpecialCallCodecValues SpecialCall { get; init; } = FiveToneSpecialCallCodecValues.NotConfigured;
    }
}
