using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder for the D890UV's "Local Information" block - factory/dealer
/// metadata (area code, manufacture code, serial number, production/
/// maintenance dates, dealer info), shown read-only in vendor CPS's
/// "Embedded Message" dialog (screenshot: "Local information.PNG",
/// 2026-08-04). Byte layout transcribed from the MIT-licensed reference
/// project github.com/xbenkozx/anytone-cps (anytone-lib/src/memory/expert_options.cpp,
/// ExpertOptions::decode).
///
/// Read-only in this app - vendor CPS itself supports writing this block
/// (factory/dealer provisioning), but this app deliberately only exposes a
/// read-only display, so no Encode exists here.
///
/// UNLIKE every other text field in this codebase, this data is narrow
/// (1 byte/char) ASCII, NOT UTF-16LE - confirmed by the field widths
/// themselves, not guessed: SerialNumber is a 0x10=16-byte span, and the
/// screenshot's real serial number ("2603250170400002") is exactly 16
/// characters - at 2 bytes/char that would only fit 8 characters, so the
/// data must be 1 byte/char. Cross-checked against ManufactureCode (8-byte
/// span, screenshot shows "D03-020" = 7 characters, fits 1 byte/char, would
/// not fit 2 byte/char). Consistent with this data being written by a
/// separate factory provisioning tool, not the same "Text" UI convention as
/// every user-editable name field.
/// </summary>
public static class LocalInfoCodec
{
    public const int RecordLength = 0x100;

    public static DecodedLocalInfo Decode(ReadOnlySpan<byte> data)
    {
        return new DecodedLocalInfo
        {
            RadioType = DecodeNarrowAscii(data.Slice(0x10, 7)),
            AreaCode = DecodeNarrowAscii(data.Slice(0x2c, 4)),
            SerialNumber = DecodeNarrowAscii(data.Slice(0x30, 0x10)),
            ProductionDate = DecodeNarrowAscii(data.Slice(0x40, 0x10)),
            ManufactureCode = DecodeNarrowAscii(data.Slice(0x50, 8)),
            MaintenanceDate = DecodeNarrowAscii(data.Slice(0x60, 0x10)),
            DealerCode = DecodeNarrowAscii(data.Slice(0x70, 0x10)),
            StockDate = DecodeNarrowAscii(data.Slice(0x80, 0x10)),
            SellDate = DecodeNarrowAscii(data.Slice(0x90, 0x10)),
            Seller = DecodeNarrowAscii(data.Slice(0xa0, 0x10)),
            MaintenanceDescription = DecodeNarrowAscii(data.Slice(0xb0, 0x50))
        };
    }

    public sealed record DecodedLocalInfo
    {
        public string RadioType { get; init; } = "";
        public string AreaCode { get; init; } = "";
        public string SerialNumber { get; init; } = "";
        public string ProductionDate { get; init; } = "";
        public string ManufactureCode { get; init; } = "";
        public string MaintenanceDate { get; init; } = "";
        public string DealerCode { get; init; } = "";
        public string StockDate { get; init; } = "";
        public string SellDate { get; init; } = "";
        public string Seller { get; init; } = "";
        public string MaintenanceDescription { get; init; } = "";
    }

    /// <summary>Same all-0xFF/all-0x00 blank detection as TextFieldCodec's
    /// UTF-16LE decode, adapted for 1-byte-per-char text.</summary>
    private static string DecodeNarrowAscii(ReadOnlySpan<byte> bytes)
    {
        if (IsBlank(bytes))
        {
            return "";
        }

        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    private static bool IsBlank(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return true;
        }

        var first = bytes[0];
        if (first != 0xFF && first != 0x00)
        {
            return false;
        }

        foreach (var b in bytes)
        {
            if (b != first)
            {
                return false;
            }
        }

        return true;
    }
}
