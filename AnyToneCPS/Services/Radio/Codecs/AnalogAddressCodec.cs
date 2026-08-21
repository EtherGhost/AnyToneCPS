using System;
using System.Globalization;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Codec for a single D890UV Analog Address Book ("Analog Address")
/// record, 0x40 bytes. Byte layout transcribed from the MIT-licensed
/// reference project github.com/xbenkozx/anytone-cps
/// (analog_address.cpp, decode_D890UV).
///
/// The 5-byte Number slice (10 BCD hex digits max) was briefly suspected
/// wrong 2026-08-04 - an initial report said the real vendor CPS
/// Number field accepts 14 digits - but a follow-up test confirmed typing
/// more than 10 digits into the real field actually CRASHES vendor CPS,
/// settling it: 10 digits is correct, matching this decode and the
/// reference project's own edit dialog (maxLength=10) exactly. See
/// CodeplugLimits.AnalogAddressNumberMaxDigits's doc comment.
///
/// Encode confirmed the same day via a live differential write (a brand
/// new No. 2 entry, Number=1234567890 (all 10 digits used) Name=
/// "TESTADDR1") - the written record matched this decode's own transform
/// exactly (numberLen=10, bytes[0:5]="1234567890" as hex-digit-string,
/// name UTF-16LE at 0x08), and the id list at
/// D890UvMemoryMap.AnalogBookId turned out to be a plain "byte[i]=i means
/// record i is populated, else 0xFF" presence marker (a full byte per
/// slot instead of a bit) - confirmed by the existing entry (No. 1,
/// record index 0) writing id-list byte 0 = 0x00, and the new entry
/// (No. 2, record index 1) writing id-list byte 1 = 0x01.
/// </summary>
public static class AnalogAddressCodec
{
    public const int RecordLength = 0x40;

    public static DecodedAnalogAddress Decode(ReadOnlySpan<byte> data, int index)
    {
        // number_len tells us how many of the (up to 10) BCD hex-digit
        // characters in bytes[0..5) are meaningful - matches the reference's
        // `data.mid(0x0, 0x5).toHex().mid(0, number_len).toInt()`: convert
        // the 5 bytes to their 10-hex-character representation, take the
        // first number_len characters (as literal decimal digits, same BCD
        // idea as BcdDecimalCodec), parse as an integer.
        var numberLen = data[0x7];
        var number = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 5), numberLen);
        var name = TextFieldCodec.DecodeName(data.Slice(0x08, 0x1e));

        return new DecodedAnalogAddress(index)
        {
            Number = number,
            Name = name
        };
    }

    /// <summary>Encodes both known fields into a copy of <paramref name="currentRecord"/>,
    /// leaving the trailing 0x26-byte unknown tail (0x26-0x40) untouched -
    /// same "preserve the unknown tail" discipline as AmAirCodec.Encode.
    /// numberLen is derived from the decimal digit count of
    /// <paramref name="values"/>.Number (defensively clamped to 10 digits,
    /// matching CodeplugLimits.AnalogAddressNumberMaxDigits - the UI's own
    /// DigitOnlyInput/MaxLength already prevent a longer value from ever
    /// reaching here).</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedAnalogAddress values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Analog Address record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();

        var digitsText = values.Number.ToString(CultureInfo.InvariantCulture);
        if (digitsText.Length > 10)
        {
            digitsText = digitsText[..10];
        }

        result[0x7] = (byte)digitsText.Length;
        Convert.FromHexString(digitsText.PadRight(10, '0')).CopyTo(result, 0x00);
        TextFieldCodec.EncodeName(values.Name, 0x1e).CopyTo(result, 0x08);

        return result;
    }

    public sealed record DecodedAnalogAddress(int Index)
    {
        public long Number { get; init; }
        public string Name { get; init; } = "";
    }
}
