using System;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Decoder/encoder for a single D890UV Roaming Channel record (0x40 bytes).
/// Byte layout transcribed from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (roamingchannel.cpp, decode_D890UV),
/// cross-validated 2026-07-15 against real hardware USB captures (that
/// reference's channel-name-field offset exactly matched our own
/// independent USB capture) for the record's overall alignment, then FULLY
/// confirmed 2026-08-07 via a live differential write capture (3 rows: RX
/// 136.00000/TX 400.00000/ColorCode 0/Slot 1/Name "TESTLOW", RX 145.00000/
/// TX 146.00000/ColorCode 15/Slot 2/Name "TESTHIGH", RX 435.00000/TX
/// 436.00000/ColorCode "No Use"/Slot "No Use"/Name "TESTNONE"). RX/TX BCD
/// offsets/scale were already right. ColorCode is a raw 0-15 byte plus a
/// 16th value (16 = "No Use", see CodeplugLimits.RoamingChannelColorCodeNoUseValue).
/// Slot was WRONG before this capture - it's a raw 0-INDEXED byte (0=Slot 1,
/// 1=Slot 2, 2="No Use", see CodeplugLimits.RoamingChannelSlotNoUseValue),
/// not the 1-or-2 originally assumed (which would have rejected the real
/// Slot 1 encoding of 0 as invalid).
/// </summary>
public static class RoamingChannelCodec
{
    public const int RecordLength = D890UvMemoryMap.RoamingChannelDataLength; // 0x40

    public static DecodedRoamingChannel Decode(ReadOnlySpan<byte> data, int index)
    {
        // Reference: `QString(data.mid(0,4).toHex()).toInt()` stored raw,
        // then `getRxFrequencyDouble()` divides by 100000.0 for MHz.
        var rxRaw = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x00, 4));
        var txRaw = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x04, 4));

        return new DecodedRoamingChannel(index)
        {
            RxFrequencyMhz = rxRaw / 100000.0,
            TxFrequencyMhz = txRaw / 100000.0,
            ColorCode = data[0x08],
            Slot = data[0x09],
            Name = TextFieldCodec.DecodeName(data.Slice(0x0a, 0x20))
        };
    }

    /// <summary>Inverse of <see cref="Decode"/> - see this class's own doc
    /// comment for the live-capture confirmation. Bytes 0x2a-0x3f (beyond
    /// the 32-byte Name field) are left untouched.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedRoamingChannel values)
    {
        var result = currentRecord.ToArray();

        BcdDecimalCodec.EncodeAsDecimal((long)Math.Round(values.RxFrequencyMhz * 100000.0), 4).CopyTo(result, 0x00);
        BcdDecimalCodec.EncodeAsDecimal((long)Math.Round(values.TxFrequencyMhz * 100000.0), 4).CopyTo(result, 0x04);
        result[0x08] = (byte)values.ColorCode;
        result[0x09] = (byte)values.Slot;
        TextFieldCodec.EncodeName(values.Name, 0x20).CopyTo(result, 0x0a);

        return result;
    }

    /// <summary>"0".."15" plus "No Use" - matches RoamingChannelEntry.ColorCodeOptions.</summary>
    public static string ColorCodeToString(int raw) =>
        raw == CodeplugLimits.RoamingChannelColorCodeNoUseValue ? "No Use" : raw.ToString();

    public static int? ParseColorCode(string value)
    {
        if (value == "No Use")
        {
            return CodeplugLimits.RoamingChannelColorCodeNoUseValue;
        }

        return int.TryParse(value, out var v) && v >= CodeplugLimits.ColorCodeMin && v <= CodeplugLimits.ColorCodeMax
            ? v
            : null;
    }

    /// <summary>Matches RoamingChannelEntry.SlotOptions.</summary>
    public static string SlotToString(int raw) => raw switch
    {
        0 => "Slot 1",
        1 => "Slot 2",
        _ => "No Use"
    };

    public static int? ParseSlot(string value) => value switch
    {
        "Slot 1" => 0,
        "Slot 2" => 1,
        "No Use" => CodeplugLimits.RoamingChannelSlotNoUseValue,
        _ => null
    };

    public sealed record DecodedRoamingChannel(int Index)
    {
        public double RxFrequencyMhz { get; init; }
        public double TxFrequencyMhz { get; init; }
        public int ColorCode { get; init; }
        public int Slot { get; init; }
        public string Name { get; init; } = "";
    }
}
