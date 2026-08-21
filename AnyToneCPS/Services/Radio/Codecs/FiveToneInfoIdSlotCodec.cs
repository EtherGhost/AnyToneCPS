using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// 5Tone Settings' "Information ID / Information Code Function1" area - a
/// small slot array, one 0x40-byte slot per "Information ID NO." selection
/// (see D890UvMemoryMap.FiveToneInfoIdData's own doc comment for the slot
/// count caveat). Confirmed 2026-08-06 across 4 live differential WRITE
/// captures, found blind via a digit-count byte + "111111" typed into
/// Information ID as the anchor.
///
/// Confirmed offsets, all within one slot: Function Option (byte 0x00,
/// index into the shared 7-item list), Function Decoding Response (byte
/// 0x01, index into whichever of its own 2 option lists Function Option
/// currently selects), Information ID (byte 0x02 = digit count, bytes
/// 0x03-0x0E = one RAW NIBBLE VALUE per hex digit - NOT ASCII and NOT the
/// 2-hex-chars-per-byte packing every other hex field in this app uses,
/// confirmed via "111111" decoding as 0x02=06 then six 0x01 bytes, same
/// "one byte per digit, raw value" convention as Self ID), Function Name
/// (bytes 0x10-0x1F, UTF-16LE, NULL-padded - unlike the ID table/BOT/EOT's
/// own Name field, which space-pads - confirmed via "ONETEST" decoding
/// with 2 trailing null bytes, not spaces).
///
/// Everything past Function Name (bytes 0x20-0x3F) was always observed as
/// zero across every capture - unconfirmed, assumed unused/padding, left
/// untouched via the usual RMW discipline.
/// </summary>
public static class FiveToneInfoIdSlotCodec
{
    public const int RecordLength = 0x40;
    private const int FunctionOptionOffset = 0x00;
    private const int FunctionDecodingResponseOffset = 0x01;
    private const int InformationIdLengthOffset = 0x02;
    private const int InformationIdDigitsOffset = 0x03;
    private const int InformationIdMaxDigits = 12; // CodeplugLimits.FiveToneInformationIdMaxLength
    private const int FunctionNameOffset = 0x10;
    private const int FunctionNameLength = 0x10; // 8 UTF-16LE chars, null-padded

    public static DecodedFiveToneInfoIdSlot Decode(ReadOnlySpan<byte> data)
    {
        var functionOption = data[FunctionOptionOffset];
        var functionDecodingResponse = data[FunctionDecodingResponseOffset];
        var informationId = DecodeInformationId(data.Slice(InformationIdDigitsOffset, InformationIdMaxDigits), data[InformationIdLengthOffset]);
        var functionName = TextFieldCodec.DecodeName(data.Slice(FunctionNameOffset, FunctionNameLength));

        return new DecodedFiveToneInfoIdSlot
        {
            FunctionOption = functionOption,
            FunctionDecodingResponse = functionDecodingResponse,
            InformationId = informationId,
            FunctionName = functionName
        };
    }

    /// <summary>Encodes every confirmed field into a copy of
    /// <paramref name="currentRecord"/>, leaving bytes 0x20-0x3F untouched
    /// - same "preserve the unknown bytes" discipline as every other codec
    /// in this app.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedFiveToneInfoIdSlot values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"5Tone Information ID slot must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[FunctionOptionOffset] = values.FunctionOption;
        result[FunctionDecodingResponseOffset] = values.FunctionDecodingResponse;

        var digits = values.InformationId;
        var digitCount = Math.Min(digits.Length, InformationIdMaxDigits);
        result[InformationIdLengthOffset] = (byte)digitCount;
        for (var i = 0; i < InformationIdMaxDigits; i++)
        {
            result[InformationIdDigitsOffset + i] = i < digitCount ? HexCharToNibble(digits[i]) : (byte)0;
        }

        TextFieldCodec.EncodeName(values.FunctionName, FunctionNameLength).CopyTo(result, FunctionNameOffset);

        return result;
    }

    private static string DecodeInformationId(ReadOnlySpan<byte> bytes, byte digitCount)
    {
        var count = Math.Min((int)digitCount, bytes.Length);
        if (count <= 0)
        {
            return "";
        }

        var sb = new StringBuilder(count);
        for (var i = 0; i < count; i++)
        {
            sb.Append(NibbleToHexChar(bytes[i]));
        }

        return sb.ToString();
    }

    private static char NibbleToHexChar(byte value) => value < 10 ? (char)('0' + value) : (char)('A' + value - 10);

    private static byte HexCharToNibble(char c) => (byte)(c is >= '0' and <= '9' ? c - '0' : char.ToUpperInvariant(c) - 'A' + 10);

    public sealed record DecodedFiveToneInfoIdSlot
    {
        public byte FunctionOption { get; init; }
        public byte FunctionDecodingResponse { get; init; }
        public string InformationId { get; init; } = "";
        public string FunctionName { get; init; } = "";
    }
}
