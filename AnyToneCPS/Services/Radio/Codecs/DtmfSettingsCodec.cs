using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// DTMF Settings - the single dialog's own scalar fields (see
/// D890UvMemoryMap.DtmfSettingsData), plus PTT ID Starting(BOT)/Ending(EOT)/
/// Remotely Kill/Remotely Stun, 4 separate fixed-length records immediately
/// adjacent to it. Confirmed 2026-08-06 across 2 live differential WRITE
/// captures - see D890UvMemoryMap.DtmfSettingsData's own doc comment for
/// the full per-field confirmation story.
/// </summary>
public static class DtmfSettingsCodec
{
    /// <summary>DTMF Transmitting Time's own discrete option list, in wire
    /// index order - kept here (Codecs layer) rather than duplicated from
    /// DtmfSettingsEntry (Models layer) so RadioCodeplugReader doesn't need
    /// a Models dependency, same layering FiveToneIdCodec's own doc
    /// comment already establishes.</summary>
    public static readonly int[] TransmittingTimeMsValues = [50, 100, 200, 300, 500];

    private const int IntervalCharacterOffset = 0x00;
    private const int GroupCodeOffset = 0x01;
    private const int DecodingResponseOffset = 0x02;
    private const int PretimeOffset = 0x03;
    private const int FirstDigitTimeOffset = 0x04;
    private const int AutoResetTimeOffset = 0x05;
    private const int SelfIdOffset = 0x06;
    private const int SelfIdLength = 3;
    private const int TimeLapseAfterEncodeOffset = 0x0A;
    private const int PttIdPauseTimeOffset = 0x0B;
    private const int PttIdOffset = 0x0C;
    private const int DCodePauseOffset = 0x0D;
    private const int SideToneOffset = 0x0E;

    /// <summary>0x0-0x9=digit, 0xA-0xD=A-D, 0xE=&#42;, 0xF=# - reuses
    /// DtmfCodeCodec's own raw-value convention even though these are
    /// single-character fields (not a whole code string).</summary>
    private static string NibbleToSymbol(byte b) => DtmfCodeCodec.Decode([b]);

    private static byte SymbolToNibble(string symbol) => DtmfCodeCodec.Encode(symbol, 1)[0];

    public sealed record DecodedDtmfSettings
    {
        public string IntervalCharacter { get; init; } = "A";

        /// <summary>"Off" = 0xFF raw - genuinely different sentinel from
        /// PttIdPauseTimeSeconds/DCodePauseSeconds below (0x00), both
        /// independently confirmed via the same differential capture.</summary>
        public string GroupCode { get; init; } = "Off";

        public byte DecodingResponse { get; init; }
        public int PretimeMs { get; init; }
        public int FirstDigitTimeMs { get; init; }
        public int AutoResetTimeSeconds { get; init; }
        public string SelfId { get; init; } = "";
        public int TimeLapseAfterEncodeMs { get; init; }

        /// <summary>0 = Off, matching FiveToneSettingsEntry.PttIdPauseTime's
        /// own sentinel convention.</summary>
        public int PttIdPauseTimeSeconds { get; init; }

        public bool PttId { get; init; }

        /// <summary>0 = Off, same sentinel convention as PttIdPauseTimeSeconds.</summary>
        public int DCodePauseSeconds { get; init; }

        public bool SideTone { get; init; }
    }

    public static DecodedDtmfSettings DecodeSingleton(ReadOnlySpan<byte> data)
    {
        var groupCodeByte = data[GroupCodeOffset];

        return new DecodedDtmfSettings
        {
            IntervalCharacter = NibbleToSymbol(data[IntervalCharacterOffset]),
            GroupCode = groupCodeByte == 0xFF ? "Off" : NibbleToSymbol(groupCodeByte),
            DecodingResponse = data[DecodingResponseOffset],
            PretimeMs = data[PretimeOffset] * 10,
            FirstDigitTimeMs = data[FirstDigitTimeOffset] * 10,
            AutoResetTimeSeconds = data[AutoResetTimeOffset],
            SelfId = DtmfCodeCodec.Decode(data.Slice(SelfIdOffset, SelfIdLength)),
            TimeLapseAfterEncodeMs = data[TimeLapseAfterEncodeOffset] * 10,
            PttIdPauseTimeSeconds = data[PttIdPauseTimeOffset],
            PttId = data[PttIdOffset] != 0,
            DCodePauseSeconds = data[DCodePauseOffset],
            SideTone = data[SideToneOffset] != 0
        };
    }

    public static byte[] EncodeSingleton(ReadOnlySpan<byte> currentRecord, DecodedDtmfSettings values)
    {
        var result = currentRecord.ToArray();

        result[IntervalCharacterOffset] = SymbolToNibble(values.IntervalCharacter);
        result[GroupCodeOffset] = values.GroupCode == "Off" ? (byte)0xFF : SymbolToNibble(values.GroupCode);
        result[DecodingResponseOffset] = values.DecodingResponse;
        result[PretimeOffset] = (byte)(values.PretimeMs / 10);
        result[FirstDigitTimeOffset] = (byte)(values.FirstDigitTimeMs / 10);
        result[AutoResetTimeOffset] = (byte)values.AutoResetTimeSeconds;
        DtmfCodeCodec.Encode(values.SelfId, SelfIdLength).CopyTo(result, SelfIdOffset);
        result[TimeLapseAfterEncodeOffset] = (byte)(values.TimeLapseAfterEncodeMs / 10);
        result[PttIdPauseTimeOffset] = (byte)values.PttIdPauseTimeSeconds;
        result[PttIdOffset] = (byte)(values.PttId ? 1 : 0);
        result[DCodePauseOffset] = (byte)values.DCodePauseSeconds;
        result[SideToneOffset] = (byte)(values.SideTone ? 1 : 0);

        return result;
    }

    public static string DecodeCode(ReadOnlySpan<byte> data) => DtmfCodeCodec.Decode(data);

    public static byte[] EncodeCode(string code, int byteLength) => DtmfCodeCodec.Encode(code, byteLength);

    /// <summary>0-based index into ["50","100","200","300","500"] -
    /// confirmed via an isolated round-2 diff (see
    /// D890UvMemoryMap.DtmfTransmittingTimeIndexData's own doc comment).</summary>
    public static byte EncodeTransmittingTimeIndex(int index) => (byte)index;

    public static int DecodeTransmittingTimeIndex(byte raw) => raw;
}
