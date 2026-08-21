using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for a single D890UV APRS Receive Filter record, 0x8 bytes -
/// a fixed 32-slot list (no bitmap). Byte layout transcribed from the
/// MIT-licensed reference project github.com/xbenkozx/anytone-cps
/// (aprs_receive_filter.cpp, decode()). Callsign is narrow ASCII, not
/// UTF-16LE - see <see cref="AsciiTextCodec"/> doc comment for why that's
/// correct here (unlike most other name fields in this codebase).
/// </summary>
public static class AprsReceiveFilterCodec
{
    public const int RecordLength = 0x8;
    public const int EntryCount = 32;

    public static DecodedAprsReceiveFilter Decode(ReadOnlySpan<byte> data, int index)
    {
        return new DecodedAprsReceiveFilter(index)
        {
            Enabled = data[0x0] != 0,
            Callsign = AsciiTextCodec.Decode(data.Slice(0x1, 6)),
            Ssid = data[0x7]
        };
    }

    public sealed record DecodedAprsReceiveFilter(int Index)
    {
        public bool Enabled { get; init; }
        public string Callsign { get; init; } = "";
        public byte Ssid { get; init; }
    }
}
