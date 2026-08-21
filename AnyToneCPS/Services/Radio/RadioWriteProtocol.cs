using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Pure builder for a single 16-byte write-block request. No I/O - kept
/// separate from <c>SerialRadioConnection</c> (which lives in the
/// Desktop-only head and isn't referenced by AnyToneCPS.Tests) specifically
/// so the byte-construction and checksum logic can be unit-tested offline,
/// without a real serial port or radio.
///
/// Format confirmed via a real USB capture of the vendor CPS's "Write To
/// Radio" against a real D890UV, 2026-07-17 - see
/// <c>Docs/AnyTone_D890UV/Capture_Findings.md</c>'s "WRITE protocol
/// confirmed byte-for-byte" section for the exact captured bytes this is
/// built on: 'W' + 4-byte big-endian address + 1-byte size (always 16) + 16
/// data bytes + 1-byte additive checksum (sum of address+size+data, mod
/// 256 - the same algorithm already confirmed for reads) + 1-byte fixed
/// trailer (0x06).
/// </summary>
public static class RadioWriteProtocol
{
    public const int BlockLength = 16;

    public static byte[] BuildBlockRequest(int address, ReadOnlySpan<byte> data)
    {
        if (data.Length != BlockLength)
        {
            throw new ArgumentException($"Write block data must be exactly {BlockLength} bytes.", nameof(data));
        }

        var request = new byte[6 + BlockLength + 2];
        request[0] = (byte)'W';
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(1, 4), address);
        request[5] = BlockLength;
        data.CopyTo(request.AsSpan(6, BlockLength));

        var sum = 0;
        for (var i = 1; i <= 5 + BlockLength; i++)
        {
            sum += request[i];
        }

        request[6 + BlockLength] = (byte)(sum & 0xFF);
        request[7 + BlockLength] = 0x06;

        return request;
    }
}
