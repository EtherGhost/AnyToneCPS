using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>Codec for the D890UV's Talk Alias Settings (DMR Talker Alias,
/// distinct from the Digital Contact List) - just 2 adjacent bytes at the
/// tail of the shared "Optional Settings" block, no memory region of their
/// own. Confirmed 2026-08-09 via a live differential write: Display
/// Priority is a 3-valued enum (0=Off/1=Contact Alias/2=Air Alias DMR/NX) -
/// not the 5-valued guess previously assumed from a vendor .ini
/// string-table extraction that was never checked against the real UI.
/// Data Format's assumed 3-valued enum (0=ISO 8/1=ISO 7/2=Unicode) was
/// confirmed correct.</summary>
public static class TalkAliasSettingsCodec
{
    // Absolute addresses (not relative to a record base - there is only one
    // instance, no bitmap/list involved, same shape as MasterIdCodec).
    // Adjacent bytes, so Encode/the patcher treat them as one 2-byte record.
    public const int DisplayPriorityAddress = 0x35000ed;
    public const int DataFormatAddress = 0x35000ee;

    public static DecodedTalkAliasSettings Decode(byte displayPriorityByte, byte dataFormatByte)
    {
        return new DecodedTalkAliasSettings
        {
            DisplayPriority = displayPriorityByte,
            DataFormat = dataFormatByte
        };
    }

    public static byte[] Encode(DecodedTalkAliasSettings values) => [values.DisplayPriority, values.DataFormat];

    public sealed record DecodedTalkAliasSettings
    {
        public byte DisplayPriority { get; init; }
        public byte DataFormat { get; init; }
    }
}
